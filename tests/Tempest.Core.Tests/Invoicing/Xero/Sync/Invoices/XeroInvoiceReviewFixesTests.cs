using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;
using Tempest.Core.Tests.Invoicing.Xero.Api;

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
    public async Task M8_ACrashDuringTheCreate_RestartedAfterXeroForgotTheKey_IsLinkedByItsMatch_NeverDuplicated_NeverDeletedByTempestOS()
    {
        // F2 follow-up (design §6.4 lost-create recovery; review m16's smaller option, Build Decisions):
        // the key is gone, but a create TempestOS logged carried this number, reference and contact, so the
        // one live draft matching it is linked — as "matched", which TempestOS never deletes.
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("M8C");
        await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "M8C");
        await CrashDuringCreateAsync(kit, raised.Id, xeroCommits: true);
        var lost = Assert.Single(kit.SalesInvoices);

        // A long outage: Xero no longer holds the create's key.
        kit.Clock.Advance(TimeSpan.FromHours(2));
        kit.Simulator.ForgetIdempotencyKeys();

        var engine = Restart(kit);
        await engine.StartAsync();
        await SettleAsync(engine);

        var request = await kit.ReloadAsync(raised.Id);
        Assert.Equal(InvoiceRequestStatus.Sent, request.Status);
        Assert.Equal(lost.Id, request.ExternalId);
        var link = await kit.LinkAsync(raised.Id);
        Assert.Equal((lost.Id, XeroInvoiceDrafts.LinkedByMatched), (link!.XeroId, link.LinkedBy));
        Assert.Single(kit.SalesInvoices);

        // Voiding the request never deletes the matched draft: refused with the reason, nothing written.
        var mark = kit.Mark;
        var voided = await kit.Service.VoidAsync(raised.Id);
        Assert.False(voided.Succeeded);
        Assert.Contains("does not delete it", voided.Reason, StringComparison.Ordinal);
        Assert.Equal("DRAFT", kit.Invoice(lost.Id).Status);
        Assert.DoesNotContain(kit.RequestsSince(mark), r => r.Method != HttpMethod.Get);

        // Once the person deletes it in Xero, the void goes ahead.
        kit.Simulator.DeleteInXero("Invoices", lost.Id);
        var again = await kit.Service.VoidAsync(raised.Id);
        Assert.True(again.Succeeded, again.Reason);
        Assert.Equal(InvoiceRequestStatus.Voided, (await kit.ReloadAsync(raised.Id)).Status);
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

    [Fact]
    public async Task M16_AMatchWithNoLoggedCreate_StaysUnproven_EvenAfterALostCreateOfAnotherRequest()
    {
        // The smaller option adopts a match only after this request's own lost create.
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("M16C");
        var contactId = await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "M16C");
        var handEntered = await kit.EnterInvoiceInXeroAsync(contactId, "ACME1-BRIDG1-INV-001", "ACME1-BRIDG1 · Deliverable M16C");

        var sent = await kit.Service.SendAsync(raised.Id);

        Assert.Equal(InvoiceRequestStatus.Rejected, sent.Request!.Status);
        Assert.Null(await kit.LinkAsync(raised.Id));
        Assert.Equal("DRAFT", kit.Invoice(handEntered).Status);
    }

    // ------------------------------------------------------------------ F2 follow-ups

    [Fact]
    public async Task F2_OfflineFirstAttempt_ThenALostAnswerTenMinutesLater_RetrySecondsLater_IsRecoveredByItsKey()
    {
        // Verifier probe: the first send never reached Xero; the second, ten minutes later, was committed
        // but its answer lost; a retry 30 s later must replay the key Xero saw seconds ago.
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("PRB");
        await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "PRB");

        var dropNext = true;
        kit.Loss.BeforeSend = r =>
        {
            if (dropNext && r.Method == HttpMethod.Put)
            {
                dropNext = false;
                throw new HttpRequestException("offline");
            }
        };
        var first = await kit.Service.SendAsync(raised.Id);
        Assert.Equal(InvoiceRequestStatus.Draft, first.Request!.Status);
        Assert.Empty(kit.SalesInvoices);

        kit.Clock.Advance(TimeSpan.FromMinutes(10));
        kit.Loss.LoseInvoiceCreates = 1;
        var second = await kit.Service.SendAsync(raised.Id);
        Assert.Equal(InvoiceRequestStatus.Draft, second.Request!.Status);
        var made = Assert.Single(kit.SalesInvoices);

        kit.Clock.Advance(TimeSpan.FromSeconds(30));
        var third = await kit.Service.SendAsync(raised.Id);

        Assert.Equal(InvoiceRequestStatus.Sent, third.Request!.Status);
        Assert.Single(kit.SalesInvoices);
        var link = await kit.LinkAsync(raised.Id);
        Assert.Equal((made.Id, XeroInvoiceDrafts.LinkedByReconciled), (link!.XeroId, link.LinkedBy));
        kit.AssertSafe();
    }

    [Fact]
    public async Task F2_ACreateThatCouldNotLeaveTheMachine_TheSignInServiceUnreachable_IsNotLogged()
    {
        var tokenEndpoint = new TerminalHandler { Throw = new HttpRequestException("offline") };
        await using var kit = await InvoiceExportKit.CreateAsync(tokenEndpoint);
        var (projectId, organisationId) = await kit.AddProjectAsync("TOK");
        await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "TOK");

        // The token runs out between the number look-up and the create; it cannot be renewed.
        kit.Loss.BeforeSend = r =>
        {
            if (r.Method == HttpMethod.Get && r.RequestUri!.Query.Contains("InvoiceNumbers", StringComparison.Ordinal))
                kit.SecretStore.SetAsync("Invoicing:Xero:ExpiresAtUtc", DateTimeOffset.UtcNow.AddHours(-1).ToString("O")).GetAwaiter().GetResult();
        };

        var sent = await kit.Service.SendAsync(raised.Id);

        Assert.Equal(InvoiceRequestStatus.Draft, sent.Request!.Status);
        Assert.DoesNotContain(kit.Simulator.Requests, r => r.Method == HttpMethod.Put && r.Path == "Invoices");
        var log = new XeroPurchasingCreateLog(kit.Store);
        Assert.Empty(await log.ListSentAsync(InvoiceExportKit.TenantId, XeroInvoiceDrafts.DocumentFor(raised.Id)));
    }

    [Fact]
    public async Task F2_AnApprovedUnprovenHolder_TheAdviceIsToVoidThere_ThenVoidTheRequest_AndThatWorks()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("APR");
        var contactId = await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "APR");
        var handEntered = await kit.EnterInvoiceInXeroAsync(contactId, "ACME1-BRIDG1-INV-001", "ACME1-BRIDG1 · Deliverable APR");
        kit.Simulator.ApproveInXero(handEntered);

        var sent = await kit.Service.SendAsync(raised.Id);
        Assert.Equal(InvoiceRequestStatus.Rejected, sent.Request!.Status);
        Assert.Contains("can only be voided there", sent.Request.LastError, StringComparison.Ordinal);
        Assert.DoesNotContain("then Retry", sent.Request.LastError, StringComparison.Ordinal);

        // Voided in Xero: its number stays taken, so the advice is to void the request — which then works.
        kit.Simulator.VoidInXero(handEntered);
        var document = await kit.Service.ToDraftDocumentAsync(await kit.ReloadAsync(raised.Id), kit.Clock.GetUtcNow(), CancellationToken.None);
        var finding = await kit.Drafts.FindByInvoiceNumberAsync(document);
        Assert.Equal(ConnectorOutcome.Rejected, finding.Outcome);
        Assert.Contains("void this request", finding.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("then Retry", finding.Reason, StringComparison.Ordinal);

        var voided = await kit.Service.VoidAsync(raised.Id);
        Assert.True(voided.Succeeded, voided.Reason);
        Assert.Equal(InvoiceRequestStatus.Voided, (await kit.ReloadAsync(raised.Id)).Status);
        kit.AssertSafe();
    }

    [Fact]
    public async Task F2_AThrowWhileRecordingTheAnswer_NeverHidesTheRequestFromTheStartUpRecovery()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("THR");
        await kit.LinkClientAsync(organisationId);
        var raised = await kit.RaiseAsync(projectId, "THR");

        var throwing = new ThrowAfterCreateDrafts(kit.Drafts);
        var service = kit.NewService(throwing);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(raised.Id));
        Assert.Equal(InvoiceRequestStatus.Sending, (await kit.ReloadAsync(raised.Id)).Status);
        Assert.Single(kit.SalesInvoices);

        // Not "being sent right now" any more: the recovery takes it.
        Assert.Equal(1, await service.RecoverInterruptedSendsAsync());
        Assert.Equal(InvoiceRequestStatus.Unknown, (await kit.ReloadAsync(raised.Id)).Status);
    }

    [Fact]
    public async Task F2_TheInvoicePlannersStartUpHook_IsLabelledAsRecoveringInterruptedSends()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var parts = new XeroSyncParts(
            kit.Links, kit.Outbox, kit.SecretStore,
            invoicePlanner: new XeroInvoicePlanner(kit.Service, kit.Files),
            domain: kit.Domain);

        var slot = Assert.Single(parts.Planners);
        Assert.NotNull(slot.Prime);
        Assert.Contains("interrupted", slot.PrimeLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("automatic", slot.PrimeLabel, StringComparison.Ordinal);
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

    /// <summary>The kit's drafts seam, but reading <see cref="IInvoiceDraftSync.CreatedStatus"/> after a successful create throws — a failure while recording Sent.</summary>
    private sealed class ThrowAfterCreateDrafts(XeroInvoiceDrafts inner) : IInvoiceDraftSync
    {
        private bool _created;

        public string ConnectorName => inner.ConnectorName;

        public string CreatedStatus => _created ? throw new InvalidOperationException("Simulated: recording Sent failed.") : inner.CreatedStatus;

        public Task<string?> FindBlockingReasonAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) => inner.FindBlockingReasonAsync(document, cancellationToken);

        public async Task<ConnectorResult<CreatedInvoice>> CreateDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default)
        {
            var created = await inner.CreateDraftAsync(document, cancellationToken);
            _created = created.Outcome == ConnectorOutcome.Ok;
            return created;
        }

        public Task<ConnectorResult<CreatedInvoice?>> FindByInvoiceNumberAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) => inner.FindByInvoiceNumberAsync(document, cancellationToken);

        public Task<ConnectorResult<InvoiceNumberFinding>> FindNumberHolderAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) => inner.FindNumberHolderAsync(document, cancellationToken);

        public Task<ConnectorResult<InvoiceDraftChange>> UpdateDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) => inner.UpdateDraftAsync(document, cancellationToken);

        public Task<ConnectorResult<InvoiceDraftChange>> DeleteDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) => inner.DeleteDraftAsync(document, cancellationToken);

        public Task QueueSendAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default) => inner.QueueSendAsync(document, cancellationToken);

        public Task RecordStatusReadingAsync(InvoiceDraftDocument document, string externalStatus, CancellationToken cancellationToken = default) => inner.RecordStatusReadingAsync(document, externalStatus, cancellationToken);
    }

    private sealed class ZonedClock(TimeProvider inner, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
