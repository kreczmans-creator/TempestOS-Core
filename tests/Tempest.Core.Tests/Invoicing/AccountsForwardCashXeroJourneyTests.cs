using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Quotations;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Tests.Quotations;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// `v0.24.0` X7 end to end over the Xero simulator: the accounts reading is
/// the real <see cref="AccountsRefreshService"/> over the real
/// <c>XeroConnector</c> (bank summary, bills, repeating bills); the sales
/// invoice is a TempestOS request sent as a Xero DRAFT (X4), approved and
/// paid in Xero's back office and read back; the pipeline is an accepted
/// quote. The forward cash picture follows each step — and the simulator
/// records no contract or safety violation (D3/D4/D7).
/// </summary>
public sealed class AccountsForwardCashXeroJourneyTests
{
    [Fact]
    public async Task ForwardCash_FollowsXero_DraftApprovedPaid_KeepsTheLastReadingOffline_AndNeverCountsAnInvoicedMilestoneTwice()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var asOf = DateOnly.FromDateTime(kit.Clock.GetUtcNow().UtcDateTime);

        var (projectId, organisationId) = await kit.AddProjectAsync("FC");
        await kit.LinkClientAsync(organisationId);

        // ---- an accepted quote: design (1,000 net, standard VAT) and survey (500 net, standard VAT) ----
        var quotations = (IQuotationService)kit.Host.Services!.GetService(typeof(IQuotationService));
        var created = await quotations.CreateAsync(projectId);
        Assert.True(created.Succeeded, created.Reason);
        var quoteId = created.Quotation!.Id;
        await quotations.AddLineAsync(quoteId, "Detailed design", null, null, new Money(1_000m, CurrencyCode.Gbp), vatRate: VatRate.Standard);
        await quotations.AddLineAsync(quoteId, "Site survey", null, null, new Money(500m, CurrencyCode.Gbp), vatRate: VatRate.Standard);
        await QuotationReviewTestSupport.ApproveAndSendAsync(quotations, InvoicingTestHost.Principals(kit.Host), quoteId);
        var quote = (await quotations.AcceptAsync(quoteId)).Quotation!;

        // ---- the accounts reading, from Xero ----
        var store = new FileAccountsReadingStore(Path.Combine(kit.Temp.Path, "forward-cash"));
        var refresh = new AccountsRefreshService(kit.Connector, store, EmptyConfiguration(), timeProvider: kit.Clock);
        await refresh.RefreshNowAsync();
        Assert.Null(refresh.LastFailureReason);
        var firstReadAt = kit.Clock.GetUtcNow();

        var readModel = new AccountsReadModel(store, kit.Domain, refresh, kit.Clock, new LinkStoreSyncStatus(kit.Links));

        var picture = await readModel.ReadForwardCashAsync();
        Assert.Equal(firstReadAt, picture.AccountsReadAt);
        Assert.Equal("Xero", picture.AccountsConnector);
        Assert.Equal(new Money(0m, CurrencyCode.Gbp), picture.Months[0].OpeningCash.Amount);
        Assert.Equal("Xero bank balances, read at 2026-10-02 09:00 UTC", picture.Months[0].OpeningCash.Source.Describe());
        Assert.Equal(2, AccountsForwardCashReadModelTests.PipelineItems(picture).Count);
        Assert.Contains(AccountsForwardCashReadModelTests.PipelineItems(picture), i => i.Gross == new Money(600m, CurrencyCode.Gbp));

        // ---- the survey is done: its invoice goes to Xero as a DRAFT ----
        var surveyDeliverable = quote.Lines.Single(l => l.Description == "Site survey").DeliverableId!.Value;
        var completion = await InvoicingTestHost.Deliverables(kit.Host).CompleteAsync(surveyDeliverable, projectId, asOf, fixedPriceValue: new Money(500m, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded, completion.Reason);
        var request = await InvoicingTestHost.RequestRaisedByCompletionAsync(kit.Host, completion.Completion!.Id);
        var sent = await kit.Service.SendAsync(request.Id);
        Assert.True(sent.Succeeded, sent.Reason);
        Assert.Equal("DRAFT", kit.Invoice(sent.Request!.ExternalId!).Status);

        picture = await readModel.ReadForwardCashAsync();
        var pipeline = AccountsForwardCashReadModelTests.PipelineItems(picture);
        var design = Assert.Single(pipeline);
        Assert.EndsWith("Detailed design", design.Description, StringComparison.Ordinal);
        Assert.Equal(new Money(1_200m, CurrencyCode.Gbp), design.Gross);
        var draft = Assert.Single(InvoiceItems(picture));
        Assert.Equal(sent.Request.GrossTotal, draft.Gross);
        Assert.Equal(sent.Request.DueOn, draft.Date);
        Assert.Contains("not yet approved", draft.Note, StringComparison.Ordinal);

        // ---- approved in Xero (by the Product Owner, not TempestOS), read back ----
        kit.Simulator.ApproveInXero(sent.Request.ExternalId!);
        kit.Clock.Advance(TimeSpan.FromMinutes(5));
        var approved = await kit.Service.ReconcileAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Accepted, approved.Request!.Status);

        picture = await readModel.ReadForwardCashAsync();
        var awaiting = Assert.Single(InvoiceItems(picture));
        Assert.Contains("awaiting payment", awaiting.Note, StringComparison.Ordinal);
        Assert.NotNull(awaiting.Source.ReadAt);
        var dueMonth = picture.Months.Single(m => m.Items.Contains(awaiting));
        Assert.Equal(awaiting.Gross, dueMonth.InvoicesDue.Amount);
        Assert.StartsWith("Xero invoices raised in TempestOS", dueMonth.InvoicesDue.Source.Describe(), StringComparison.Ordinal);
        AssertMonthsChain(picture);

        // ---- offline: the refresh fails, the last reading is used and the failure is said ----
        kit.Clock.Advance(TimeSpan.FromHours(1));
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable));
        await refresh.RefreshNowAsync();
        Assert.NotNull(refresh.LastFailureReason);

        picture = await readModel.ReadForwardCashAsync();
        Assert.Equal(firstReadAt, picture.AccountsReadAt);
        Assert.Equal(refresh.LastFailureReason, picture.RefreshFailureReason);
        Assert.True(picture.Months[0].OpeningCash.IsAvailable);
        Assert.True(picture.Months[^1].ClosingCash.IsAvailable);
        Assert.Single(InvoiceItems(picture));

        // ---- paid in Xero, read back: no longer money to come ----
        kit.Simulator.PayInXero(sent.Request.ExternalId!, asOf);
        await kit.Service.ReconcileAsync(request.Id);

        picture = await readModel.ReadForwardCashAsync();
        Assert.Empty(InvoiceItems(picture));
        Assert.Single(AccountsForwardCashReadModelTests.PipelineItems(picture));

        kit.AssertSafe();
        Assert.Empty(kit.Simulator.Violations);
    }

    private static IReadOnlyList<ForwardCashItem> InvoiceItems(ForwardCashPicture picture) =>
        [.. picture.Months.SelectMany(m => m.Items).Concat(picture.Later).Where(i => i.Kind == ForwardCashItemKind.InvoiceDue)];

    private static void AssertMonthsChain(ForwardCashPicture picture)
    {
        for (var i = 0; i < picture.Months.Count; i++)
        {
            var month = picture.Months[i];
            Assert.Equal(month.OpeningCash.Amount!.Value + month.MoneyIn.Amount!.Value - month.MoneyOut.Amount!.Value, month.ClosingCash.Amount);
            if (i > 0)
                Assert.Equal(picture.Months[i - 1].ClosingCash.Amount, month.OpeningCash.Amount);
        }
    }

    private static IConfigurationProvider EmptyConfiguration() =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([])).Build();

    /// <summary>
    /// The X6 badge for an invoice from the link store alone, as
    /// <c>XeroSyncService.GetStatusAsync</c> answers it with nothing queued:
    /// Xero's last-read status word and when it was read. Only the badge is
    /// needed here; the drain itself is X6's (and is not run).
    /// </summary>
    private sealed class LinkStoreSyncStatus(IXeroLinkStore links) : IXeroSyncService
    {
        public Task<XeroDrainReport> DrainAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ReadBackAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<XeroSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
        {
            var link = await links.FindAsync(InvoiceExportKit.TenantId, document, cancellationToken);
            if (link is null)
                return new XeroSyncStatus(XeroSyncBadge.NotSent);

            var badge = link.LastKnownXeroStatus switch
            {
                "PAID" => XeroSyncBadge.Paid,
                "VOIDED" or "DELETED" => XeroSyncBadge.Voided,
                "AUTHORISED" => XeroSyncBadge.AwaitingPayment,
                _ => XeroSyncBadge.InXeroDraft,
            };
            return new XeroSyncStatus(badge, XeroNumber: link.XeroNumber, XeroStatus: link.LastKnownXeroStatus, AsOfUtc: link.LastReadAtUtc ?? link.LinkedAtUtc);
        }
    }
}
