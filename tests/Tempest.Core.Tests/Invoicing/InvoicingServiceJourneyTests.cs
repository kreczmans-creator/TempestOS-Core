using Tempest.Workspace.Projects;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Projects;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// The `WP 19.1A` acceptance journey (`ADR-0151`): raise a request from a
/// completion (unbilled time plus a fixed price), send it through the fake
/// connector, watch every line's own source gain its <c>InvoicedBy</c>
/// link, prove a second raise on the same project finds nothing left to
/// bill, and prove the paid date arrives only from the connector — plus
/// every documented failure state (reject, time out, unavailable,
/// re-authorise) and the idempotency rule itself.
/// </summary>
public sealed class InvoicingServiceJourneyTests
{
    private static readonly DateOnly Week = new(2026, 3, 2);

    [Fact]
    public async Task CoreJourney_FourLines_RightTotal_SendLinksEveryLine_SecondRaiseFindsNothingToBill_PaidDateFromConnectorAlone()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "CORE");
        var domain = InvoicingTestHost.Domain(host);
        var timesheets = InvoicingTestHost.Timesheets(host);
        var deliverables = InvoicingTestHost.Deliverables(host);
        var invoicing = InvoicingTestHost.Invoicing(host);
        var connector = InvoicingTestHost.Connector(host);

        var e1 = await timesheets.RecordAsync(projectId, Week, 4m, billable: true, "Senior", "Design review");
        var e2 = await timesheets.RecordAsync(projectId, Week, 3m, billable: true, "Senior", "Detailed design");
        var e3 = await timesheets.RecordAsync(projectId, Week, 2m, billable: true, "Senior", "Drafting");
        Assert.True(e1.Succeeded && e2.Succeeded && e3.Succeeded);

        var deliverableId = await CreateDeliverableAsync(domain, projectId, "CORE-1");
        var completion = await deliverables.CompleteAsync(deliverableId, projectId, Week, fixedPriceValue: new Money(500m, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded);

        // Completing raised the request through the completion hook
        // (`ADR-0151` §7). Raising again by hand while it is still Draft
        // refuses, naming it, rather than raising a second Draft carrying
        // the same four lines (the v0.19.0 Desktop journey found that
        // second one, one run in two, before this guard).
        var request = await InvoicingTestHost.RequestRaisedByCompletionAsync(host, completion.Completion!.Id);
        var raisedAgain = await invoicing.RaiseFromCompletionAsync(completion.Completion.Id);
        Assert.False(raisedAgain.Succeeded);
        Assert.Equal(InvoiceRequestRefusal.AlreadyInvoiced, raisedAgain.Refusal);
        Assert.Equal(request.Id, raisedAgain.Request!.Id);
        Assert.Single((await domain.Repository.ListChildrenAsync(projectId)).OfType<InvoiceRequest>());

        Assert.Equal(4, request.Lines.Count);
        Assert.Equal(new Money(150m * 4 + 150m * 3 + 150m * 2 + 500m, CurrencyCode.Gbp), request.Total);
        Assert.Equal(CurrencyCode.Gbp, request.Currency);
        Assert.Equal(InvoiceRequestStatus.Draft, request.Status);

        var sent = await invoicing.SendAsync(request.Id);
        Assert.True(sent.Succeeded, sent.Reason);
        Assert.Equal(InvoiceRequestStatus.Sent, sent.Request!.Status);
        Assert.NotNull(sent.Request.ExternalId);
        Assert.Equal("Fake", sent.Request.Connector);

        // Every line's own source now carries InvoicedBy.
        var reloadedEntry = (TimesheetEntry)(await domain.Repository.FindAsync(e1.Entry!.Id))!;
        Assert.Equal(request.Id, reloadedEntry.InvoicedBy);
        var reloadedCompletion = (DeliverableCompletion)(await domain.Repository.FindAsync(completion.Completion.Id))!;
        Assert.Equal(request.Id, reloadedCompletion.InvoicedBy);

        // A second completion under the same project, with no fixed price
        // and no unbilled time left, finds nothing to bill.
        var deliverableId2 = await CreateDeliverableAsync(domain, projectId, "CORE-2");
        var completion2 = await deliverables.CompleteAsync(deliverableId2, projectId, Week);
        Assert.True(completion2.Succeeded);

        var secondRaise = await invoicing.RaiseFromCompletionAsync(completion2.Completion!.Id);
        Assert.False(secondRaise.Succeeded);
        Assert.Equal(InvoiceRequestRefusal.NothingToBill, secondRaise.Refusal);

        // The fake reports paid -> ReconcileAsync shows the paid date;
        // nobody in TempestOS could set it directly (no mutator, service
        // method or command exists to do so — InvoicingService's own
        // remarks).
        var paidDate = Week.AddDays(14);
        connector.ScriptStatus(sent.Request.ExternalId!, new InvoiceStatusReading("PAID", sent.Request.ExternalId, Week, paidDate));

        var reconciled = await invoicing.ReconcileAsync(request.Id);
        Assert.True(reconciled.Succeeded, reconciled.Reason);
        Assert.Equal(InvoiceRequestStatus.Accepted, reconciled.Request!.Status);
        Assert.Equal(paidDate, reconciled.Request.PaidDate);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task RaiseFromCompletionAsync_Refuses_WhenTheProjectHasNoClient()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var domain = InvoicingTestHost.Domain(host);
        var deliverables = InvoicingTestHost.Deliverables(host);
        var invoicing = InvoicingTestHost.Invoicing(host);

        var projectId = await InvoicingTestHost.CreateProjectAsync(host, "INV-NOCLIENT");
        var deliverableId = await CreateDeliverableAsync(domain, projectId, "NC");
        var completion = await deliverables.CompleteAsync(deliverableId, projectId, Week, fixedPriceValue: new Money(100m, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded);

        var raised = await invoicing.RaiseFromCompletionAsync(completion.Completion!.Id);
        Assert.False(raised.Succeeded);
        Assert.Equal(InvoiceRequestRefusal.NoClient, raised.Refusal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_Rejected_ReasonShown_LineIsNotLinked()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "REJ");
        var domain = InvoicingTestHost.Domain(host);
        var invoicing = InvoicingTestHost.Invoicing(host);
        var connector = InvoicingTestHost.Connector(host);

        var request = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "REJ");
        connector.ScriptNextCreate(ConnectorOutcome.Rejected, "Contact not found in the accounting system.");

        var sent = await invoicing.SendAsync(request.Id);
        Assert.True(sent.Succeeded);
        Assert.Equal(InvoiceRequestStatus.Rejected, sent.Request!.Status);
        Assert.Equal("Contact not found in the accounting system.", sent.Request.LastError);

        var completion = (DeliverableCompletion)(await domain.Repository.FindAsync(request.Lines.Single().SourceId))!;
        Assert.Null(completion.InvoicedBy);

        // A Rejected request can be voided locally.
        var voided = await invoicing.VoidAsync(request.Id);
        Assert.True(voided.Succeeded);
        Assert.Equal(InvoiceRequestStatus.Voided, voided.Request!.Status);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_TimesOut_ThenReconciliationFindsItByReference_MovesToSentWithLinks()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "TOF");
        var domain = InvoicingTestHost.Domain(host);
        var invoicing = InvoicingTestHost.Invoicing(host);
        var connector = InvoicingTestHost.Connector(host);

        var request = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "TOF");
        connector.ScriptNextCreate(ConnectorOutcome.Unknown);

        var sent = await invoicing.SendAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Unknown, sent.Request!.Status);

        var completionBefore = (DeliverableCompletion)(await domain.Repository.FindAsync(request.Lines.Single().SourceId))!;
        Assert.Null(completionBefore.InvoicedBy);

        var reconciled = await invoicing.ReconcileAsync(request.Id);
        Assert.True(reconciled.Succeeded);
        Assert.Equal(InvoiceRequestStatus.Sent, reconciled.Request!.Status);
        Assert.NotNull(reconciled.Request.ExternalId);

        var completionAfter = (DeliverableCompletion)(await domain.Repository.FindAsync(request.Lines.Single().SourceId))!;
        Assert.Equal(request.Id, completionAfter.InvoicedBy);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_TimesOut_ThenReconciliationDoesNotFindIt_RevertsToDraft()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "TOD");
        var domain = InvoicingTestHost.Domain(host);
        var invoicing = InvoicingTestHost.Invoicing(host);
        var connector = InvoicingTestHost.Connector(host);

        var request = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "TOD");
        connector.ScriptNextCreate(ConnectorOutcome.Unknown);

        var sent = await invoicing.SendAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Unknown, sent.Request!.Status);

        // Genuinely lost — not even the provider has it.
        connector.ForgetReference(request.Id.ToString());

        var reconciled = await invoicing.ReconcileAsync(request.Id);
        Assert.True(reconciled.Succeeded);
        Assert.Equal(InvoiceRequestStatus.Draft, reconciled.Request!.Status);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_Unavailable_StaysDraft_NoLink()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "UNA");
        var domain = InvoicingTestHost.Domain(host);
        var invoicing = InvoicingTestHost.Invoicing(host);
        var connector = InvoicingTestHost.Connector(host);

        var request = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "UNA");
        connector.ScriptNextCreate(ConnectorOutcome.Unavailable, "Network unreachable.");

        var sent = await invoicing.SendAsync(request.Id);
        Assert.True(sent.Succeeded);
        Assert.Equal(InvoiceRequestStatus.Draft, sent.Request!.Status);
        Assert.Equal("Network unreachable.", sent.Request.LastError);

        var completion = (DeliverableCompletion)(await domain.Repository.FindAsync(request.Lines.Single().SourceId))!;
        Assert.Null(completion.InvoicedBy);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_ReauthoriseNeeded_NothingSentUntilStateIsCleared()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "REAUTH");
        var domain = InvoicingTestHost.Domain(host);
        var invoicing = InvoicingTestHost.Invoicing(host);
        var connector = InvoicingTestHost.Connector(host);

        var request = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "REAUTH");
        connector.ScriptNextCreate(ConnectorOutcome.Reauthorise, "Token expired.");

        var sent = await invoicing.SendAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Reauthorise, sent.Request!.Status);
        Assert.Equal("Token expired.", sent.Request.LastError);

        // No transition in this Work Package's own scope moves a request
        // out of Reauthorise — ReconcileAsync (the poller's own act)
        // refuses it, leaving it exactly where it is.
        var reconciled = await invoicing.ReconcileAsync(request.Id);
        Assert.False(reconciled.Succeeded);
        Assert.Equal(InvoiceRequestRefusal.TransitionNotPermitted, reconciled.Refusal);
        Assert.Equal(InvoiceRequestStatus.Reauthorise, reconciled.Request!.Status);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task IdempotencyKey_IsTheRequestsOwnId_AndIsWhatTheConnectorReceived()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "IDEM");
        var domain = InvoicingTestHost.Domain(host);
        var invoicing = InvoicingTestHost.Invoicing(host);
        var connector = InvoicingTestHost.Connector(host);

        var request = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "IDEM");
        await invoicing.SendAsync(request.Id);

        var call = Assert.Single(
            connector.Calls, c => c.Member == nameof(Core.Invoicing.IInvoicingConnector.CreateDraftInvoiceAsync));
        Assert.Equal(request.Id.ToString(), call.Argument);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ALineOnASentRequest_CannotBePutOnANewRequest()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "REUSE");
        var domain = InvoicingTestHost.Domain(host);
        var timesheets = InvoicingTestHost.Timesheets(host);
        var deliverables = InvoicingTestHost.Deliverables(host);
        var invoicing = InvoicingTestHost.Invoicing(host);

        var entry = await timesheets.RecordAsync(projectId, Week, 2m, billable: true, "Senior", "Billable work");
        Assert.True(entry.Succeeded);

        var deliverableId = await CreateDeliverableAsync(domain, projectId, "REUSE-1");
        var completion = await deliverables.CompleteAsync(deliverableId, projectId, Week);
        Assert.True(completion.Succeeded);

        // Completing raised the request through the completion hook; with
        // no fixed price it carries the entry alone, so a second raise by
        // hand finds that entry already on it and nothing left to bill.
        var firstRequest = await InvoicingTestHost.SingleRequestUnderProjectAsync(host, projectId);
        Assert.Single(firstRequest.Lines);
        var raisedAgain = await invoicing.RaiseFromCompletionAsync(completion.Completion!.Id);
        Assert.Equal(InvoiceRequestRefusal.NothingToBill, raisedAgain.Refusal);
        Assert.Equal(firstRequest.Id, raisedAgain.Request!.Id);

        var sent = await invoicing.SendAsync(firstRequest.Id);
        Assert.Equal(InvoiceRequestStatus.Sent, sent.Request!.Status);

        // The entry now carries InvoicedBy; a second deliverable completed
        // under the same project, with no fixed price, finds it not among
        // the unbilled entries any more.
        var deliverableId2 = await CreateDeliverableAsync(domain, projectId, "REUSE-2");
        var completion2 = await deliverables.CompleteAsync(deliverableId2, projectId, Week);
        Assert.True(completion2.Succeeded);

        var secondRaise = await invoicing.RaiseFromCompletionAsync(completion2.Completion!.Id);
        Assert.False(secondRaise.Succeeded);
        Assert.Equal(InvoiceRequestRefusal.NothingToBill, secondRaise.Refusal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    /// <summary>
    /// `WP 19.1A-R1` disclosure #3: <c>SendAsync</c> resolves
    /// <see cref="InvoiceRequestSnapshot.ClientName"/> from the real
    /// Organisation catalogue (registered by <see cref="SetUpBillableProjectAsync"/>,
    /// exactly as a real deployment would) before a connector is ever
    /// called — proved here against a <see cref="SpyConnector"/> that
    /// captures the snapshot it actually received, since
    /// <c>FakeInvoicingConnector</c> itself only records the idempotency
    /// key, never the whole snapshot.
    /// </summary>
    [Fact]
    public async Task SendAsync_ResolvesTheClientNameFromTheOrganisationCatalogue_BeforeCallingTheConnector()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "CLIENTNAME");
        var domain = InvoicingTestHost.Domain(host);
        var request = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "CLIENTNAME");

        var spy = new SpyConnector();
        var directInvoicing = new InvoicingService(
            domain, InvoicingTestHost.RateCards(host), InvoicingTestHost.Timesheets(host), InvoicingTestHost.Deliverables(host),
            spy, InvoicingTestHost.Organisations(host));

        var sent = await directInvoicing.SendAsync(request.Id);

        Assert.True(sent.Succeeded, sent.Reason);
        Assert.NotNull(spy.LastRequest);
        Assert.Equal("INV-CLIENT-CLIENTNAME", spy.LastRequest!.ClientOrganisationId);
        Assert.Equal("Fictional Client Ltd", spy.LastRequest.ClientName);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    /// <summary>Captures the snapshot the last <see cref="CreateDraftInvoiceAsync"/> call received, so a test can inspect what <see cref="InvoicingService"/> actually filled onto it — <c>InvoiceReconciliationServiceTests.ThrowingConnector</c>'s own direct-construction pattern, applied here to observe rather than to fail.</summary>
    private sealed class SpyConnector : IInvoicingConnector
    {
        public string Name => "Spy";

        public InvoiceRequestSnapshot? LastRequest { get; private set; }

        public Task<ConnectorResult<CreatedInvoice>> CreateDraftInvoiceAsync(InvoiceRequestSnapshot request, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(ConnectorResult<CreatedInvoice>.Ok(new CreatedInvoice($"spy-{idempotencyKey}", null, idempotencyKey)));
        }

        public Task<ConnectorResult<InvoiceStatusReading>> ReadStatusAsync(string externalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConnectorResult<InvoiceStatusReading>.Ok(new InvoiceStatusReading("SUBMITTED", null, null, null)));

        public Task<ConnectorResult<CreatedInvoice?>> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConnectorResult<CreatedInvoice?>.Ok(null));

        public Task<ConnectorResult<IReadOnlyList<ConnectorContact>>> ListContactsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ConnectorResult<IReadOnlyList<ConnectorContact>>.Ok([]));

        public Task<ConnectorAuthorisationState> AuthorisationStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorAuthorisationState(ConnectorAuthorisation.Authorised));
    }

    // ====================================================================
    // Fixtures
    // ====================================================================

    private static async Task<Guid> SetUpBillableProjectAsync(ITempestHost host, string suffix)
    {
        var organisationId = $"INV-CLIENT-{suffix}";
        var rateCardId = $"INV-CARD-{suffix}";

        var organisations = InvoicingTestHost.Organisations(host);
        var rateCards = InvoicingTestHost.RateCards(host);
        var commercial = InvoicingTestHost.ProjectCommercial(host);

        await organisations.RegisterAsync(organisationId, OperationsFixtures.Organisation(organisationId), OperationsFixtures.Verified());

        var card = OneGradeCard(rateCardId, "Senior", 150m, 90m);
        await rateCards.RegisterAsync(rateCardId, card, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, rateCardId);

        var projectId = await InvoicingTestHost.CreateProjectAsync(host, $"INV-PRJ-{suffix}");

        Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
        Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);

        return projectId;
    }

    private static async Task<InvoiceRequest> RaiseSingleLineDraftRequestAsync(
        ITempestHost host, Guid projectId, EngineeringDomainContext domain, string suffix)
    {
        var deliverables = InvoicingTestHost.Deliverables(host);

        var deliverableId = await CreateDeliverableAsync(domain, projectId, suffix);
        var completion = await deliverables.CompleteAsync(deliverableId, projectId, Week, fixedPriceValue: new Money(250m, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded);

        // Completing raised it through the completion hook (`ADR-0151` §7).
        return await InvoicingTestHost.RequestRaisedByCompletionAsync(host, completion.Completion!.Id);
    }

    private static async Task<Guid> CreateDeliverableAsync(EngineeringDomainContext domain, Guid projectId, string suffix)
    {
        var milestoneService = new ProjectMilestoneService(domain);
        var milestone = await milestoneService.CreateMilestoneAsync(projectId, $"MS-{suffix}", $"Milestone {suffix}", DateTimeOffset.UtcNow.AddDays(30));
        var deliverable = await milestoneService.CreateDeliverableAsync(projectId, milestone.Id, $"DEL-{suffix}", $"Deliverable {suffix}");
        return deliverable.Id;
    }

    private static RateCard OneGradeCard(string code, string grade, decimal billing, decimal cost) => new()
    {
        Code = code,
        Name = "Invoicing journey rate card",
        EffectivePeriod = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
        Currency = CurrencyCode.Gbp,
        Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
        Entries =
        [
            new RateCardEntry(
                "ENG-1", $"{grade} engineering", PricingBasis.Hourly, new Money(billing, CurrencyCode.Gbp), new Money(cost, CurrencyCode.Gbp),
                Grade: grade),
        ],
    };
}
