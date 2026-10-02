using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Secrets;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Invoices;

/// <summary>
/// `v0.24.0` X4's planner and push handlers (design §6.2, §6.3) and its
/// registration hook: what each invoice state plans, what each handler sends
/// over the simulator, and the container composition.
/// </summary>
public sealed class XeroInvoiceSyncTests
{
    [Fact]
    public async Task Planner_ADraftNeverSentThroughXero_PlansNothing()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, _) = await kit.AddProjectAsync("PL0");
        var request = await kit.RaiseAsync(projectId, "PL0");

        Assert.Empty(await new XeroInvoicePlanner(kit.Service, kit.Files).PlanAsync(request.Id, link: null));
        Assert.Empty(await new XeroInvoicePlanner(kit.Service, kit.Files).PlanAsync(Guid.NewGuid(), link: null));
    }

    [Fact]
    public async Task Planner_ASentInvoice_PlansAnUpdateWhenItsContentDiffers_AndAnUploadWhenItsPdfDiffers()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("PL1");
        var planner = new XeroInvoicePlanner(kit.Service, kit.Files);
        var link = (await kit.LinkAsync(request.Id))!;

        Assert.Empty(await planner.PlanAsync(request.Id, link));

        var stale = link with { LastPushedContentHash = "0000" };
        var update = Assert.Single(await planner.PlanAsync(request.Id, stale));
        Assert.Equal(XeroOperation.UpdateInvoiceDraft, update.Operation);
        Assert.Empty(await planner.PlanAsync(request.Id, stale with { LastKnownXeroStatus = "AUTHORISED" }));

        var file = kit.Files.Save(request.Id, "ACME1-BRIDG1-INV-001.pdf", "%PDF later");
        var upload = Assert.Single(await planner.PlanAsync(request.Id, link));
        Assert.Equal(XeroOperation.UploadAttachment, upload.Operation);
        Assert.Equal(file.Sha256, upload.ContentHash);
        Assert.Equal(file.FileName, upload.Argument);
        Assert.Empty(await planner.PlanAsync(request.Id, link with { AttachmentContentHash = file.Sha256 }));
        Assert.Empty(await planner.PlanAsync(request.Id, link with { LastKnownXeroStatus = "DELETED" }));
    }

    [Fact]
    public async Task Planner_AVoidedRequestWhoseXeroCopyIsStillADraft_PlansTheDelete()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("PL2");
        var link = (await kit.LinkAsync(request.Id))!;
        kit.Simulator.DeleteInXero("Invoices", request.ExternalId!);
        await kit.Service.ReconcileAsync(request.Id); // Voided, read back

        var planned = Assert.Single(await new XeroInvoicePlanner(kit.Service).PlanAsync(request.Id, link)); // the stale link still says DRAFT
        Assert.Equal(XeroOperation.DeleteInvoiceDraft, planned.Operation);
        Assert.Empty(await new XeroInvoicePlanner(kit.Service).PlanAsync(request.Id, (await kit.LinkAsync(request.Id))!));
    }

    [Fact]
    public async Task Handler_UpdateInvoiceDraft_SendsWhileDraft_AndIsRejectedWithTheReasonOnceApproved()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("HD1");
        var handler = new XeroInvoicePushHandler(kit.Service, kit.Drafts);
        var entry = await kit.Outbox.EnqueueAsync(XeroOperation.UpdateInvoiceDraft, XeroInvoiceDrafts.DocumentFor(request.Id), "hash-1");

        Assert.Equal(XeroPushOutcome.Succeeded, (await handler.PushAsync(InvoiceExportKit.TenantId, entry)).Outcome);

        kit.Simulator.ApproveInXero(request.ExternalId!);
        var refused = await handler.PushAsync(InvoiceExportKit.TenantId, entry);

        Assert.Equal(XeroPushOutcome.Rejected, refused.Outcome);
        Assert.Equal("Xero holds it as AUTHORISED; change it in Xero.", refused.Reason);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Handler_DeleteInvoiceDraft_DeletesADraft_ButNeverAnApprovedInvoice()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (first, _) = await kit.SendNewAsync("HD2", projectIdentifier: "ACME1-FIRST1");
        var handler = new XeroInvoicePushHandler(kit.Service, kit.Drafts);

        var deleteFirst = await kit.Outbox.EnqueueAsync(XeroOperation.DeleteInvoiceDraft, XeroInvoiceDrafts.DocumentFor(first.Id), "h");
        Assert.Equal(XeroPushOutcome.Succeeded, (await handler.PushAsync(InvoiceExportKit.TenantId, deleteFirst)).Outcome);
        Assert.Equal("DELETED", kit.Invoice(first.ExternalId!).Status);
        Assert.Equal(XeroPushOutcome.NothingToDo, (await handler.PushAsync(InvoiceExportKit.TenantId, deleteFirst)).Outcome);

        var (projectId, organisationId) = await kit.AddProjectAsync("HD2B", "ACME1-SECND1");
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync(organisationId, kit.Simulator.SeedContact("Second Client Ltd"))).Outcome);
        var second = await kit.RaiseAsync(projectId, "HD2B");
        Assert.Equal(InvoiceRequestStatus.Sent, (await kit.Service.SendAsync(second.Id)).Request!.Status);
        var secondId = (await kit.ReloadAsync(second.Id)).ExternalId!;
        kit.Simulator.ApproveInXero(secondId);

        var deleteSecond = await kit.Outbox.EnqueueAsync(XeroOperation.DeleteInvoiceDraft, XeroInvoiceDrafts.DocumentFor(second.Id), "h");
        var refused = await handler.PushAsync(InvoiceExportKit.TenantId, deleteSecond);

        Assert.Equal(XeroPushOutcome.Rejected, refused.Outcome);
        Assert.Contains("void it in Xero", refused.Reason, StringComparison.Ordinal);
        Assert.Equal("AUTHORISED", kit.Invoice(secondId).Status);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Handlers_RefuseEntriesThatAreNotTheirs()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var handler = new XeroInvoicePushHandler(kit.Service, kit.Drafts);
        var attachments = new XeroInvoiceAttachmentPushHandler(kit.Drafts);
        var quote = await kit.Outbox.EnqueueAsync(XeroOperation.UploadAttachment, XeroDocumentRef.For(XeroDocumentKind.Quote, Guid.NewGuid()), "h", "Q.pdf");
        var otherTenant = await kit.Outbox.EnqueueAsync(XeroOperation.PushInvoiceDraft, XeroInvoiceDrafts.DocumentFor(Guid.NewGuid()), "h");
        var gone = await kit.Outbox.EnqueueAsync(XeroOperation.UpdateInvoiceDraft, XeroInvoiceDrafts.DocumentFor(Guid.NewGuid()), "h");

        Assert.Equal(XeroPushOutcome.Rejected, (await attachments.PushAsync(InvoiceExportKit.TenantId, quote)).Outcome);
        Assert.Equal(XeroPushOutcome.Rejected, (await handler.PushAsync(InvoiceExportKit.TenantId, quote)).Outcome);
        Assert.Equal(XeroPushOutcome.Rejected, (await handler.PushAsync("another-tenant", otherTenant)).Outcome);
        Assert.Equal(XeroPushOutcome.NothingToDo, (await handler.PushAsync(InvoiceExportKit.TenantId, gone)).Outcome);
        Assert.Equal(XeroPushOutcome.Blocked, (await attachments.PushAsync(InvoiceExportKit.TenantId, await kit.Outbox.EnqueueAsync(
            XeroOperation.UploadAttachment, XeroInvoiceDrafts.DocumentFor(Guid.NewGuid()), "h2", "x.pdf"))).Outcome);
        Assert.DoesNotContain(kit.Simulator.Requests, r => r.Method != HttpMethod.Get);
        kit.AssertSafe();
    }

    [Fact]
    public async Task ARevisedPdf_ReplacesTheAttachmentByName()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("PDF");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "PDF");
        kit.Files.Save(request.Id, "ACME1-BRIDG1-INV-001.pdf", "%PDF first");
        await kit.Service.SendAsync(request.Id);
        var revised = kit.Files.Save(request.Id, "ACME1-BRIDG1-INV-001.pdf", "%PDF second, longer");

        var pushed = await kit.Drafts.UploadAttachmentAsync(InvoiceExportKit.TenantId, request.Id);

        Assert.Equal(XeroPushOutcome.Succeeded, pushed.Outcome);
        Assert.Contains(kit.Simulator.Requests, r => r.Method == HttpMethod.Post && r.Path.EndsWith("/Attachments/ACME1-BRIDG1-INV-001.pdf", StringComparison.Ordinal));
        Assert.Equal(revised.Sha256, (await kit.LinkAsync(request.Id))!.AttachmentContentHash);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Registration_ComposesTheSeam_SoInvoicingServiceSendsNumberedDrafts_AndEachTypeIsRegisteredOnce()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();

        var services = new ServiceCollection();
        services.AddInstance(kit.Api);
        services.AddInstance<IXeroLinkStore>(kit.Links);
        services.AddInstance<IXeroOutbox>(kit.Outbox);
        services.AddInstance<IOrganisationCatalog>(InvoicingTestHost.Organisations(kit.Host));
        services.AddInstance<ISecretStore>(kit.SecretStore);
        services.AddInstance<IXeroSettingsReader>(kit.Reader);
        services.AddInstance<ISettingsProvider>(kit.Settings);
        services.AddInstance<IXeroDocumentFileSource>(kit.Files);
        services.AddInstance<IAuditRecorder>(kit.Audit);
        services.AddInstance<IInvoicingService>(kit.Service);
        services.Singleton<XeroTaxTypeResolver>();
        services.Singleton<XeroAccountCodeMap>();
        XeroServiceRegistration.AddXeroContacts(services);
        XeroServiceRegistration.AddXeroInvoices(services, kit.Connector);
        var provider = new TempestServiceProvider(services);

        var seam = (IInvoiceDraftSync)provider.GetService(typeof(IInvoiceDraftSync));
        Assert.Equal("Xero", seam.ConnectorName);
        Assert.Equal("DRAFT", seam.CreatedStatus);
        Assert.Same(kit.Connector, provider.GetService(typeof(XeroConnector)));
        Assert.IsType<XeroInvoicePushHandler>(provider.GetService(typeof(XeroInvoicePushHandler)));
        Assert.IsType<XeroInvoiceAttachmentPushHandler>(provider.GetService(typeof(XeroInvoiceAttachmentPushHandler)));
        var planner = (XeroInvoicePlanner)provider.GetService(typeof(XeroInvoicePlanner));
        Assert.Equal(XeroDocumentKind.Invoice, planner.Kind);
        Assert.Equal(InvoiceRequest.CanonicalKind, planner.CanonicalKind);

        var (projectId, organisationId) = await kit.AddProjectAsync("REG");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "REG");
        var sent = await kit.NewService(seam).SendAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Sent, sent.Request!.Status);
        Assert.Equal("ACME1-BRIDG1-INV-001", Assert.Single(kit.SalesInvoices).Number);

        Assert.ThrowsAny<ServiceRegistrationException>(() => XeroServiceRegistration.AddXeroInvoices(services, kit.Connector));
        kit.AssertSafe();
    }

    [Fact]
    public async Task TheHost_ComposedForXero_ResolvesTheInvoicingServiceWithTheSeam_AndTheX4Handlers()
    {
        using var temp = new Plugins.TempDirectory();
        var (host, manager) = await Connectors.ConnectorHostFixture.StartAsync(
            temp.Path, new KeyValuePair<string, string>(InvoicingService.ConnectorConfigurationKey, "Xero"));

        var services = host.Services!;
        Assert.IsType<InvoicingService>(services.GetService(typeof(IInvoicingService)));
        Assert.Equal("Xero", ((IInvoiceDraftSync)services.GetService(typeof(IInvoiceDraftSync))).ConnectorName);
        Assert.Same(services.GetService(typeof(IInvoicingConnector)), services.GetService(typeof(XeroConnector)));
        Assert.NotNull(services.GetService(typeof(XeroInvoicePushHandler)));
        Assert.NotNull(services.GetService(typeof(XeroInvoiceAttachmentPushHandler)));
        Assert.NotNull(services.GetService(typeof(XeroInvoicePlanner)));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public void ContentHash_IsStable_AndFollowsEveryLineField()
    {
        var line = new InvoiceRequestLine("TimesheetEntry", Guid.Parse("11111111-1111-1111-1111-111111111111"), "Design", 2m, new Money(100m, CurrencyCode.Gbp), new Money(200m, CurrencyCode.Gbp));
        var document = new InvoiceDraftDocument(
            Guid.Parse("22222222-2222-2222-2222-222222222222"), "ACME1-BRIDG1-INV-001", "ORG-1", "ORG-1", "Client", CurrencyCode.Gbp,
            [line], new Money(200m, CurrencyCode.Gbp), new DateOnly(2026, 10, 2), new DateOnly(2026, 11, 1), "ACME1-BRIDG1 · D", null);

        var hash = XeroInvoiceContent.ContentHash(document);

        Assert.Equal(hash, XeroInvoiceContent.ContentHash(document with { Date = new DateOnly(2027, 1, 1) })); // when it was sent is not content
        Assert.NotEqual(hash, XeroInvoiceContent.ContentHash(document with { Lines = [line with { Quantity = 3m }] }));
        Assert.NotEqual(hash, XeroInvoiceContent.ContentHash(document with { Lines = [line with { VatRate = VatRate.Standard }] }));
        Assert.NotEqual(hash, XeroInvoiceContent.ContentHash(document with { Reference = "other" }));
        Assert.Equal(64, hash.Length);
    }
}
