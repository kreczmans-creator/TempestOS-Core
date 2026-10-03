using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Invoices;

/// <summary>
/// `v0.24.0` review-board fixes F2 for invoices, end to end over the
/// simulator: M8 (a request left Sending by a crash during its create is
/// recovered through the lost-create recovery — never re-sent blindly, never
/// stuck), m16 (an invoice is TempestOS's own only when proven: its link, or
/// a logged create whose key Xero replays to it — never by number, reference
/// and contact alone), m18 (the invoice date is the workspace's local date)
/// and n4 (one VAT inference for an expense's bill and its recharge line).
/// </summary>
public sealed class XeroInvoiceReviewFixesTests
{
    // ------------------------------------------------------------------ M8

    [Fact]
    public async Task M8_ACrashDuringTheCreate_XeroCommittedIt_RestartFindsItsOwnDraft_OneInvoice_Sent()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("M8A");
        await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "M8A");
        await CrashDuringCreateAsync(kit, raised.Id, xeroCommits: true);

        Assert.Equal(InvoiceRequestStatus.Sending, (await kit.ReloadAsync(raised.Id)).Status);
        var lost = Assert.Single(kit.SalesInvoices);

        // TempestOS starts again: a fresh seam, service and engine over the same stores.
        var engine = Restart(kit);
        await engine.StartAsync();
        await SettleAsync(engine);

        var after = await kit.ReloadAsync(raised.Id);
        Assert.Equal(InvoiceRequestStatus.Sent, after.Status);
        Assert.Equal(lost.Id, after.ExternalId);
        Assert.Equal(lost.Id, (await kit.LinkAsync(raised.Id))!.XeroId);
        Assert.Single(kit.SalesInvoices);

        // Never re-sent blindly: every create carried the one key (the replay is Xero's cached answer).
        Assert.Single(kit.Simulator.Requests.Where(r => r.Method == HttpMethod.Put && r.Path == "Invoices").Select(r => r.IdempotencyKey).Distinct());
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await engine.GetStatusAsync(XeroInvoiceDrafts.DocumentFor(raised.Id))).Badge);
        kit.AssertSafe();
    }

    [Fact]
    public async Task M8_ACrashBeforeTheCreateReachedXero_RestartLooksItUpFirst_ThenSendsIt_OneInvoice()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("M8B");
        await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "M8B");
        await CrashDuringCreateAsync(kit, raised.Id, xeroCommits: false);
        Assert.Empty(kit.SalesInvoices);

        var engine = Restart(kit);
        await engine.StartAsync();
        await SettleAsync(engine);

        Assert.Equal(InvoiceRequestStatus.Sent, (await kit.ReloadAsync(raised.Id)).Status);
        Assert.Single(kit.SalesInvoices);

        // Looked up before the create was sent again.
        var lookUp = kit.Simulator.Requests.ToList().FindIndex(r => r.Method == HttpMethod.Get && r.Query.ContainsKey("InvoiceNumbers"));
        var create = kit.Simulator.Requests.ToList().FindIndex(r => r.Method == HttpMethod.Put && r.Path == "Invoices");
        Assert.InRange(lookUp, 0, create - 1);
        kit.AssertSafe();
    }

    [Fact]
    public async Task M8_ACrashDuringTheCreate_RestartedAfterXeroForgotTheKey_IsAFailedDecision_NeverStuck_NeverDuplicated()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("M8C");
        await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "M8C");
        await CrashDuringCreateAsync(kit, raised.Id, xeroCommits: true);

        // A long outage: Xero no longer holds the create's key, so nothing proves the draft TempestOS's own.
        kit.Clock.Advance(TimeSpan.FromHours(2));
        kit.Simulator.ForgetIdempotencyKeys();

        var engine = Restart(kit);
        await engine.StartAsync();
        await SettleAsync(engine);

        var request = await kit.ReloadAsync(raised.Id);
        Assert.Equal(InvoiceRequestStatus.Unknown, request.Status);
        var status = await engine.GetDocumentStatusAsync(XeroInvoiceDrafts.DocumentFor(raised.Id));
        Assert.Equal(XeroSyncBadge.Failed, status.Status.Badge);
        Assert.Contains("cannot prove it created it", status.Status.Reason, StringComparison.Ordinal);
        Assert.Single(kit.SalesInvoices);
        Assert.Null(await kit.LinkAsync(raised.Id));
        Assert.DoesNotContain(kit.Simulator.Requests, r => r.Method == HttpMethod.Post && r.Path.StartsWith("Invoices/", StringComparison.Ordinal));
        kit.AssertSafe();
    }

    [Fact]
    public async Task M8_ARequestBeingSentRightNow_IsNeverMovedByTheRecovery()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("M8D");
        await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "M8D");

        // The recovery runs while this instance's send is in flight (between Sending and Xero's answer).
        int? movedDuringSend = null;
        kit.Loss.BeforeSend = request =>
        {
            if (request.Method == HttpMethod.Put && movedDuringSend is null)
                movedDuringSend = kit.Service.RecoverInterruptedSendsAsync().GetAwaiter().GetResult();
        };

        var sent = await kit.Service.SendAsync(raised.Id);

        Assert.Equal(0, movedDuringSend);
        Assert.Equal(InvoiceRequestStatus.Sent, sent.Request!.Status);
        Assert.Equal(0, await kit.Service.RecoverInterruptedSendsAsync());
        kit.AssertSafe();
    }

    // ------------------------------------------------------------------ m16

    [Fact]
    public async Task M16_AnInvoiceEnteredByHandUnderTheSameNumberReferenceAndContact_IsNeverAdopted_ChangedOrDeleted()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("M16");
        var contactId = await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "M16");
        var handEntered = await kit.EnterInvoiceInXeroAsync(contactId, "ACME1-BRIDG1-INV-001", "ACME1-BRIDG1 · Deliverable M16");
        var mark = kit.Mark;

        var sent = await kit.Service.SendAsync(raised.Id);

        Assert.Equal(InvoiceRequestStatus.Rejected, sent.Request!.Status);
        Assert.Contains("cannot prove it created it", sent.Request.LastError, StringComparison.Ordinal);
        Assert.Null(await kit.LinkAsync(raised.Id));
        Assert.Single(kit.SalesInvoices);
        Assert.Equal("Hand-entered work", kit.Invoice(handEntered).Body["LineItems"]![0]!["Description"]!.GetValue<string>());
        Assert.DoesNotContain(kit.RequestsSince(mark), r => r.Method != HttpMethod.Get);

        // Voiding the request is refused while it is there (TempestOS cannot tell whether that draft holds this
        // work, so it neither deletes it nor frees the lines over it), and the reason says what unblocks it.
        var voided = await kit.Service.VoidAsync(raised.Id);
        Assert.False(voided.Succeeded);
        Assert.Contains("cannot prove it created it", voided.Reason, StringComparison.Ordinal);
        Assert.Equal("DRAFT", kit.Invoice(handEntered).Status);
        Assert.DoesNotContain(kit.RequestsSince(mark), r => r.Method != HttpMethod.Get);
        kit.AssertSafe();
    }

    [Fact]
    public async Task M16_AnUnknownSendWhoseNumberIsHeldByAnUnprovenInvoice_IsRefusedWithTheReason_NotLinked()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("M16B");
        var contactId = await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "M16B");
        var request = await kit.ReloadAsync(raised.Id);
        await request.MoveToSendingAsync(kit.Connector.Name, "ACME1-BRIDG1 · Deliverable M16B");
        await request.MarkUnknownAsync("lost");
        var handEntered = await kit.EnterInvoiceInXeroAsync(contactId, "ACME1-BRIDG1-INV-001", "ACME1-BRIDG1 · Deliverable M16B");

        var handler = new XeroInvoicePushHandler(kit.Service, kit.Drafts);
        var entry = await kit.Outbox.EnqueueAsync(XeroOperation.PushInvoiceDraft, XeroInvoiceDrafts.DocumentFor(raised.Id), "c1");
        var pushed = await handler.PushAsync(InvoiceExportKit.TenantId, entry);

        Assert.Equal(XeroPushOutcome.Rejected, pushed.Outcome);
        Assert.Contains("cannot prove it created it", pushed.Reason, StringComparison.Ordinal);
        Assert.Equal(InvoiceRequestStatus.Unknown, (await kit.ReloadAsync(raised.Id)).Status);
        Assert.Null(await kit.LinkAsync(raised.Id));
        Assert.Equal("DRAFT", kit.Invoice(handEntered).Status);
        kit.AssertSafe();
    }

    // ------------------------------------------------------------------ m18

    [Fact]
    public async Task M18_ASendAtHalfPastMidnightLocalTime_IsDatedThatDay_NotTheDayBeforeInUtc()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("M18");
        await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "M18");

        var bst = TimeZoneInfo.CreateCustomTimeZone("Test BST", TimeSpan.FromHours(1), "Test BST", "Test BST");
        var service = new InvoicingService(
            kit.Domain, InvoicingTestHost.RateCards(kit.Host), InvoicingTestHost.Timesheets(kit.Host), InvoicingTestHost.Deliverables(kit.Host),
            kit.Connector, InvoicingTestHost.Organisations(kit.Host), new ZonedClock(kit.Clock, bst), expenses: null, draftSync: kit.Drafts);

        var halfPastMidnightBst = new DateTimeOffset(2026, 7, 15, 23, 30, 0, TimeSpan.Zero);
        var document = await service.ToDraftDocumentAsync(await kit.ReloadAsync(raised.Id), halfPastMidnightBst, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 16), document.Date);
    }

    // ------------------------------------------------------------------ n4

    [Theory]
    [InlineData(100, 20, VatRate.Standard)]
    [InlineData(100, 5, VatRate.Reduced)]
    [InlineData(100, 0, VatRate.OutOfScope)]
    [InlineData(100, 1, VatRate.Reduced)]      // an odd ratio still charges VAT: the receipt recorded some
    [InlineData(0, 10, VatRate.Standard)]
    [InlineData(100, 12.5, VatRate.Standard)]  // the tie goes to standard
    public void N4_TheBillAndTheRechargeLine_InferTheSameRate(decimal net, decimal vat, VatRate expected)
    {
        Assert.Equal(expected, ExpenseVatInference.Infer(net, vat));
        Assert.Equal(expected, XeroPurchasingMapper.InferVatRate(net, vat));
    }

    // ------------------------------------------------------------------ fixtures

    /// <summary>
    /// TempestOS stops mid-send: the request is moved to Sending and its create
    /// is logged and sent; Xero commits it (or never sees it), and the process
    /// ends before the answer is acted on — the request stays Sending.
    /// </summary>
    private static async Task CrashDuringCreateAsync(InvoiceExportKit kit, Guid requestId, bool xeroCommits)
    {
        var request = await kit.ReloadAsync(requestId);
        var document = await kit.Service.ToDraftDocumentAsync(request, kit.Clock.GetUtcNow(), CancellationToken.None);
        await request.MoveToSendingAsync(kit.Connector.Name, document.Reference);

        if (xeroCommits)
        {
            kit.Loss.LoseInvoiceCreates = 1;
            await kit.Drafts.CreateDraftAsync(document); // the answer is lost; the process "ends" before it is handled
        }
    }

    private static XeroSyncService Restart(InvoiceExportKit kit)
    {
        var drafts = kit.NewDrafts();
        var service = kit.NewService(drafts);
        var parts = new XeroSyncParts(
            kit.Links, kit.Outbox, kit.SecretStore,
            invoicePlanner: new XeroInvoicePlanner(service, kit.Files),
            invoiceHandler: new XeroInvoicePushHandler(service, drafts),
            invoiceAttachments: new XeroInvoiceAttachmentPushHandler(drafts),
            domain: kit.Domain);

        return new XeroSyncService(
            parts, kit.Outbox, kit.Store, kit.Reader, new FakeConnectionState(), rateLimiter: null,
            readBack: null, observer: null, importer: null, audit: kit.Audit,
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

    private sealed class ZonedClock(TimeProvider inner, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
