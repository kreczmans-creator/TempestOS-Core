using Tempest.Workspace.Projects;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Projects;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Logging;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// `InvoiceReconciliationService`'s own acceptance (`WP 19.1A` #5,
/// `ADR-0151`): ticking a manually-driven <see cref="TimeProvider"/>
/// reconciles a pending request, and a connector failure inside the poll
/// is isolated — logged, and the host keeps running.
/// </summary>
public sealed class InvoiceReconciliationServiceTests
{
    private static readonly DateOnly Week = new(2026, 3, 2);

    [Fact]
    public async Task TickingTheTimeProvider_ReconcilesAnUnknownRequest()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "POLL");
        var domain = InvoicingTestHost.Domain(host);
        var invoicing = InvoicingTestHost.Invoicing(host);
        var connector = InvoicingTestHost.Connector(host);
        var configuration = (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider));

        var request = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "POLL");
        connector.ScriptNextCreate(ConnectorOutcome.Unknown);
        await invoicing.SendAsync(request.Id);

        var reloaded = (InvoiceRequest)(await domain.Repository.FindAsync(request.Id))!;
        Assert.Equal(InvoiceRequestStatus.Unknown, reloaded.Status);

        var timeProvider = new ManualTimeProvider();
        var poller = new InvoiceReconciliationService(invoicing, domain, configuration, timeProvider: timeProvider);

        await poller.StartAsync(CancellationToken.None);
        timeProvider.Tick();
        await poller.WaitForPendingTickAsync();
        await poller.StopAsync(CancellationToken.None);

        var afterPoll = (InvoiceRequest)(await domain.Repository.FindAsync(request.Id))!;
        Assert.Equal(InvoiceRequestStatus.Sent, afterPoll.Status);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task AConnectorFailureInsideThePoll_IsIsolated_LoggedAndTheHostKeepsRunning()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var projectId = await SetUpBillableProjectAsync(host, "POLLFAIL");
        var domain = InvoicingTestHost.Domain(host);
        var rateCards = InvoicingTestHost.RateCards(host);
        var timesheets = InvoicingTestHost.Timesheets(host);
        var deliverables = InvoicingTestHost.Deliverables(host);
        var configuration = (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider));

        // A real request, raised and sent through the real (fake) connector
        // so it exists as a Sent request for the throwing connector below
        // to fail on.
        var realInvoicing = InvoicingTestHost.Invoicing(host);
        var realConnector = InvoicingTestHost.Connector(host);
        var request = await RaiseSingleLineDraftRequestAsync(host, projectId, domain, "POLLFAIL");
        await realInvoicing.SendAsync(request.Id);

        var reloaded = (InvoiceRequest)(await domain.Repository.FindAsync(request.Id))!;
        Assert.Equal(InvoiceRequestStatus.Sent, reloaded.Status);
        Assert.NotEmpty(realConnector.Calls);

        // A second InvoicingService, over the same real domain and
        // Timesheets/Deliverables but a connector that always throws —
        // proves the poller isolates a defect in a connector
        // implementation exactly as any other outcome.
        var organisations = InvoicingTestHost.Organisations(host);
        var throwingInvoicing = new InvoicingService(domain, rateCards, timesheets, deliverables, new ThrowingConnector(), organisations);
        var recordingLogger = new RecordingLogger();
        var poller = new InvoiceReconciliationService(throwingInvoicing, domain, configuration, recordingLogger);

        var exception = await Record.ExceptionAsync(() => poller.RunOnceAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Contains(recordingLogger.Messages, m => m.Contains(request.Id.ToString(), StringComparison.Ordinal));

        // The request itself is untouched — the failed reconciliation
        // changed nothing.
        var stillSent = (InvoiceRequest)(await domain.Repository.FindAsync(request.Id))!;
        Assert.Equal(InvoiceRequestStatus.Sent, stillSent.Status);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
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

        var card = new RateCard
        {
            Code = rateCardId,
            Name = "Poller test rate card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            Currency = CurrencyCode.Gbp,
            Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
            Entries = [new RateCardEntry("ENG-1", "Senior engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), new Money(90m, CurrencyCode.Gbp), Grade: "Senior")],
        };
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

        var milestoneService = new ProjectMilestoneService(domain);
        var milestone = await milestoneService.CreateMilestoneAsync(projectId, $"MS-{suffix}", $"Milestone {suffix}", DateTimeOffset.UtcNow.AddDays(30));
        var deliverable = await milestoneService.CreateDeliverableAsync(projectId, milestone.Id, $"DEL-{suffix}", $"Deliverable {suffix}");

        var completion = await deliverables.CompleteAsync(deliverable.Id, projectId, Week, fixedPriceValue: new Money(250m, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded);

        // Completing raised it through the completion hook (`ADR-0151` §7).
        return await InvoicingTestHost.RequestRaisedByCompletionAsync(host, completion.Completion!.Id);
    }

    /// <summary>A connector that always throws — proves the poller isolates a defect in an implementation, not just an ordinary <see cref="ConnectorOutcome"/>.</summary>
    private sealed class ThrowingConnector : IInvoicingConnector
    {
        public string Name => "Throwing";

        public Task<ConnectorResult<CreatedInvoice>> CreateDraftInvoiceAsync(InvoiceRequestSnapshot request, string idempotencyKey, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The connector broke.");

        public Task<ConnectorResult<InvoiceStatusReading>> ReadStatusAsync(string externalId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The connector broke.");

        public Task<ConnectorResult<CreatedInvoice?>> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The connector broke.");

        public Task<ConnectorResult<IReadOnlyList<ConnectorContact>>> ListContactsAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The connector broke.");

        public Task<ConnectorAuthorisationState> AuthorisationStateAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The connector broke.");
    }

    /// <summary>
    /// A <see cref="TimeProvider"/> whose <see cref="CreateTimer"/> hands
    /// back a controllable timer a test ticks directly, rather than one
    /// driven by real wall-clock time — the seam
    /// <see cref="InvoiceReconciliationService"/>'s own remarks describe.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private TimerCallback? _callback;
        private object? _state;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            _state = state;

            return new ManualTimer();
        }

        public void Tick() => _callback?.Invoke(_state);

        private sealed class ManualTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
