using Tempest.Core.Events;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Quotations;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` X6, the whole journey over a real workspace and the simulator,
/// run by the engine alone: a quote exported, sent and accepted in TempestOS
/// is followed in Xero; the invoice raised from the accepted work is a Xero
/// DRAFT with its PDF; the Product Owner approves and is paid in Xero, and
/// both are read back — to the badge and to the invoice request itself.
/// </summary>
public sealed class XeroSyncLifecycleTests
{
    [Fact]
    public async Task QuoteAccepted_InvoiceDraft_ApprovedAndPaidInXero_IsReadBack()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("LC1");
        await kit.LinkClientAsync(organisationId);
        var engine = Build(kit, out var quotes, out var quoteFiles);

        // Quote: approved R1 and exported, then sent and accepted in TempestOS.
        var quoteId = Guid.NewGuid();
        quotes[quoteId] = QuoteSyncTestKit.Quote(quoteId, client: organisationId, reference: "ACME1-BRIDG1-Q-001");
        quoteFiles.Store(quoteId, "R1 sheet");
        await engine.PlanDocumentAsync(XeroDocumentRef.For(XeroDocumentKind.Quote, quoteId));
        await SettleAsync(engine);
        var xeroQuote = Assert.Single(kit.Simulator.All("Quotes"));
        Assert.Equal("DRAFT", xeroQuote.Status);
        Assert.Single(xeroQuote.Attachments);

        foreach (var status in new[] { QuotationStatus.Sent, QuotationStatus.Accepted })
        {
            quotes[quoteId] = quotes[quoteId] with { Status = status };
            await engine.PlanDocumentAsync(XeroDocumentRef.For(XeroDocumentKind.Quote, quoteId));
            await SettleAsync(engine);
        }

        Assert.Equal("ACCEPTED", kit.Simulator.Find("Quotes", xeroQuote.Id)!.Status);

        // The accepted work is invoiced: a numbered DRAFT in Xero (D3), never sent to the client (D4).
        var raised = await kit.RaiseAsync(projectId, "LC1");
        var sent = await kit.Service.SendAsync(raised.Id);
        Assert.Equal(InvoiceRequestStatus.Sent, sent.Request!.Status);
        var invoiceRef = XeroInvoiceDrafts.DocumentFor(raised.Id);
        var draft = await engine.GetDocumentStatusAsync(invoiceRef);
        Assert.Equal(XeroSyncBadge.InXeroDraft, draft.Status.Badge);
        Assert.Contains("review and send from Xero", draft.Status.Reason, StringComparison.Ordinal);

        // Its PDF follows by itself.
        kit.Files.Save(raised.Id, "ACME1-BRIDG1-0001.pdf", "invoice sheet");
        await engine.PlanDocumentAsync(invoiceRef);
        await SettleAsync(engine);
        var invoice = Assert.Single(kit.LiveSalesInvoices);
        Assert.Equal("DRAFT", invoice.Status);
        Assert.Single(invoice.Attachments);

        // Approved in Xero → Awaiting payment; paid in Xero → Paid. Read back, never set.
        kit.Simulator.ApproveInXero(invoice.Id);
        kit.Clock.Advance(engine.Options.ReadBackInterval);
        await engine.RunCycleAsync();
        Assert.Equal(XeroSyncBadge.AwaitingPayment, (await engine.GetStatusAsync(invoiceRef)).Badge);
        Assert.Equal(InvoiceRequestStatus.Accepted, (await kit.ReloadAsync(raised.Id)).Status);

        kit.Simulator.PayInXero(invoice.Id, new DateOnly(2026, 10, 30));
        kit.Clock.Advance(engine.Options.ReadBackInterval);
        await engine.RunCycleAsync();
        var paid = await engine.GetDocumentStatusAsync(invoiceRef);
        Assert.Equal(XeroSyncBadge.Paid, paid.Status.Badge);
        Assert.Equal("Paid", paid.Label);
        var request = await kit.ReloadAsync(raised.Id);
        Assert.Equal(new DateOnly(2026, 10, 30), request.PaidDate);

        Assert.Single(kit.SalesInvoices, i => i.Number == invoice.Number);
        kit.AssertSafe();
    }

    [Fact]
    public async Task AnInvoiceWhoseSendAnswerWasLost_IsQueued_AndTheEngineMakesItOneDraft()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("LC2");
        await kit.LinkClientAsync(organisationId);
        var engine = Build(kit, out _, out _);

        var raised = await kit.RaiseAsync(projectId, "LC2");
        kit.Loss.LoseInvoiceCreates = 1;
        var sent = await kit.Service.SendAsync(raised.Id);
        Assert.NotEqual(InvoiceRequestStatus.Sent, sent.Request!.Status);
        Assert.Single(kit.SalesInvoices);

        var queued = await engine.GetStatusAsync(XeroInvoiceDrafts.DocumentFor(raised.Id));
        Assert.Equal(XeroSyncBadge.Queued, queued.Badge);

        await SettleAsync(engine);

        var invoice = Assert.Single(kit.SalesInvoices);
        Assert.Equal("DRAFT", invoice.Status);
        Assert.Equal(InvoiceRequestStatus.Sent, (await kit.ReloadAsync(raised.Id)).Status);
        Assert.Equal(invoice.Id, (await kit.LinkAsync(raised.Id))!.XeroId);
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await engine.GetStatusAsync(XeroInvoiceDrafts.DocumentFor(raised.Id))).Badge);
        kit.AssertSafe();
    }

    [Fact]
    public async Task ACommittedInvoiceRequest_IsSeenThroughTheWorkspaceChangeFeed()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("LC3");
        await kit.LinkClientAsync(organisationId);
        var feed = (IWorkspaceChanges)kit.Host.Services!.GetService(typeof(IWorkspaceChanges));
        var observer = new XeroChangeObserver(feed);
        var engine = Build(kit, out _, out _, observer);
        await engine.StartAsync();
        Assert.True(observer.IsListening);

        var raised = await kit.RaiseAsync(projectId, "LC3");
        var batch = observer.TakeAll();
        Assert.Contains(batch.Changes, c => c.ObjectId == raised.Id && c.CanonicalKind == InvoiceRequest.CanonicalKind);

        engine.StopListening();
        Assert.False(observer.IsListening);
    }

    private static XeroSyncService Build(InvoiceExportKit kit, out FakeQuoteSource quotes, out FakeDocumentFileSource quoteFiles, XeroChangeObserver? observer = null)
    {
        quotes = new FakeQuoteSource();
        quoteFiles = new FakeDocumentFileSource();
        var taxTypes = new XeroTaxTypeResolver(kit.Reader, kit.Settings);
        var accounts = new XeroAccountCodeMap(kit.Reader, kit.Settings);
        var quotePlanner = new XeroQuotePlanner(
            quotes, kit.Links, kit.Outbox, kit.Store, kit.SecretStore, quoteFiles, kit.Audit, kit.Clock, new XeroQuotePlannerOptions { AutomaticFromUtc = DateTimeOffset.MinValue });

        var parts = new XeroSyncParts(
            kit.Links, kit.Outbox, kit.SecretStore,
            quotePlanner, quotes,
            new XeroQuotePushHandler(kit.Api, kit.Links, quotes, kit.Store, kit.Linker, taxTypes, accounts, kit.Audit, kit.Clock),
            new XeroQuoteAttachmentHandler(kit.Api, kit.Links, quotes, quoteFiles, kit.Clock),
            new XeroInvoicePlanner(kit.Service, kit.Files),
            new XeroInvoicePushHandler(kit.Service, kit.Drafts),
            new XeroInvoiceAttachmentPushHandler(kit.Drafts),
            kit.Domain);

        return new XeroSyncService(
            parts, kit.Outbox, kit.Store, kit.Reader, new FakeConnectionState(), rateLimiter: null,
            readBack: new XeroReadBack(kit.Api, kit.Links, kit.Service, null, kit.Audit, kit.Clock),
            observer: observer, importer: new XeroInvoiceLinkImporter(kit.Domain, kit.Links), audit: kit.Audit,
            timeProvider: kit.Clock, options: new XeroSyncOptions { Jitter = () => 0.5 });
    }

    private static async Task SettleAsync(XeroSyncService engine)
    {
        for (var i = 0; i < 20; i++)
        {
            var report = await engine.RunCycleAsync();
            if (report.Drain.Attempted == 0 && report.Planned == 0)
                return;
        }
    }
}
