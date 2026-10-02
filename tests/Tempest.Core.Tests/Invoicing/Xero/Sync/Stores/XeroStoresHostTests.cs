using Tempest.Workspace.Projects;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Persistence;
using Tempest.Core.Projects;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;
using static Tempest.Core.Tests.Invoicing.Xero.Sync.Stores.StoresFixtures;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

/// <summary>
/// `v0.24.0` B2 against the real platform: a request sent to Xero before
/// `v0.24.0`, persisted by the real repository, is imported as a link in the
/// real SQLite persistence store and survives a restart; and B2's services
/// resolve from the container, each registered once (`ADR-0122`).
/// </summary>
public sealed class XeroStoresHostTests
{
    private static readonly DateOnly Week = new(2026, 3, 2);

    [Fact]
    public async Task APreV024XeroSend_IsImportedFromTheRepository_AndItsOwnFieldsAreUnchanged()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var domain = InvoicingTestHost.Domain(host);
        var persistence = (IPersistenceStore)host.Services!.GetService(typeof(IPersistenceStore));
        var projectId = await SetUpBillableProjectAsync(host, "XLNK");

        // A v0.19–v0.23 send through the Xero connector, recorded exactly as
        // InvoicingService.SendAsync recorded it.
        var sent = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "XLNK-1");
        await sent.MoveToSendingAsync("Xero");
        await sent.MarkSentAsync("0b8b3e9a-1111-4000-8000-00000000abcd", "INV-0007", Start.AddDays(-10));

        // One still a draft: never sent, nothing to import.
        await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "XLNK-2");

        // B2's registration over the host's own persistence store and
        // domain context: the container builds the importer and link store.
        var services = new ServiceCollection();
        services.AddInstance(persistence);
        services.AddInstance(domain);
        services.AddInstance(InvoicingTestHost.Principals(host));
        XeroServiceRegistration.AddXeroStores(services);
        var provider = new TempestServiceProvider(services);

        var links = (IXeroLinkStore)provider.GetService(typeof(IXeroLinkStore));
        var importer = (XeroInvoiceLinkImporter)provider.GetService(typeof(XeroInvoiceLinkImporter));
        var report = await importer.ImportAsync(DemoTenant);

        Assert.Equal(1, report.Imported);
        var link = await links.FindAsync(DemoTenant, XeroDocumentRef.For(XeroDocumentKind.Invoice, sent.Id));
        Assert.NotNull(link);
        Assert.Equal("0b8b3e9a-1111-4000-8000-00000000abcd", link.XeroId);
        Assert.Equal("INV-0007", link.XeroNumber);
        Assert.Equal(Start.AddDays(-10), link.LinkedAtUtc);
        Assert.Equal(XeroInvoiceLinkImporter.ImportedLinkedBy, link.LinkedBy);

        var reloaded = (InvoiceRequest)(await domain.Repository.FindAsync(sent.Id))!;
        Assert.Equal(InvoiceRequestStatus.Sent, reloaded.Status);
        Assert.Equal("Xero", reloaded.Connector);
        Assert.Equal("0b8b3e9a-1111-4000-8000-00000000abcd", reloaded.ExternalId);

        Assert.Equal(0, (await importer.ImportAsync(DemoTenant)).Imported);
        Assert.Equal(0, (await new XeroInvoiceLinkImporter(domain, links).ImportAsync(DemoTenant)).Imported);

        await manager.ShutdownAsync();
        await host.DisposeAsync();

        // Restart over the same SQLite root: the link is still there.
        var (restarted, restartedManager) = await InvoicingTestHost.StartAsync(temp.Path);
        var restartedPersistence = (IPersistenceStore)restarted.Services!.GetService(typeof(IPersistenceStore));

        Assert.Equal(link, await new PersistenceXeroLinkStore(restartedPersistence).FindAsync(DemoTenant, XeroDocumentRef.For(XeroDocumentKind.Invoice, sent.Id)));

        await restartedManager.ShutdownAsync();
        await restarted.DisposeAsync();
    }

    [Fact]
    public void Registration_ResolvesTheStores_AndRegistersEachServiceOnce()
    {
        var services = new ServiceCollection();
        services.AddInstance<IPersistenceStore>(new YieldingInMemoryPersistenceStore());
        services.AddInstance<ICurrentPrincipalAccessor>(new CurrentPrincipalAccessor());

        XeroServiceRegistration.AddXeroStores(services);

        Assert.Throws<DuplicateServiceRegistrationException>(() => XeroServiceRegistration.AddXeroStores(services));
        Assert.Single(services.Descriptors, d => d.ServiceType == typeof(XeroInvoiceLinkImporter));
        Assert.Single(typeof(XeroInvoiceLinkImporter).GetConstructors());

        var provider = new TempestServiceProvider(services);
        Assert.IsType<PersistenceXeroLinkStore>(provider.GetService(typeof(IXeroLinkStore)));
        Assert.IsType<PersistenceXeroOutbox>(provider.GetService(typeof(IXeroOutbox)));
        Assert.IsType<PersistenceXeroOutbox>(provider.GetService(typeof(IXeroOutboxDrain)));
    }

    private static async Task<Guid> SetUpBillableProjectAsync(ITempestHost host, string suffix)
    {
        var organisationId = $"XLNK-CLIENT-{suffix}";
        var rateCardId = $"XLNK-CARD-{suffix}";

        var organisations = InvoicingTestHost.Organisations(host);
        var rateCards = InvoicingTestHost.RateCards(host);
        var commercial = InvoicingTestHost.ProjectCommercial(host);

        await organisations.RegisterAsync(organisationId, OperationsFixtures.Organisation(organisationId), OperationsFixtures.Verified());

        var card = new RateCard
        {
            Code = rateCardId,
            Name = "Xero link import rate card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            Currency = CurrencyCode.Gbp,
            Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
            Entries = [new RateCardEntry("ENG-1", "Senior engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), new Money(90m, CurrencyCode.Gbp), Grade: "Senior")],
        };
        await rateCards.RegisterAsync(rateCardId, card, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, rateCardId);

        var projectId = await InvoicingTestHost.CreateProjectAsync(host, $"XLNK-PRJ-{suffix}");

        Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
        Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);

        return projectId;
    }

    private static async Task<InvoiceRequest> RaiseSingleLineDraftRequestAsync(
        ITempestHost host, Guid projectId, EngineeringDomainContext domain, string suffix)
    {
        var deliverables = InvoicingTestHost.Deliverables(host);

        var milestoneService = new ProjectMilestoneService(domain);
        var milestone = await milestoneService.CreateMilestoneAsync(projectId, $"MS-{suffix}", $"Milestone {suffix}", Start.AddDays(30));
        var deliverable = await milestoneService.CreateDeliverableAsync(projectId, milestone.Id, $"DEL-{suffix}", $"Deliverable {suffix}");

        var completion = await deliverables.CompleteAsync(deliverable.Id, projectId, Week, fixedPriceValue: new Money(250m, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded);

        return await InvoicingTestHost.RequestRaisedByCompletionAsync(host, completion.Completion!.Id);
    }
}
