using Tempest.Workspace;
using Tempest.Workspace.Kpi;
using Tempest.Workspace.Projects;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Invoicing;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Projects;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// <see cref="WorkspaceSnapshotReader"/>'s own <see cref="WorkspaceSnapshotKind.Kpi"/>
/// case (`WP 19.1B`): real <c>TimesheetEntry</c>/<c>DeliverableCompletion</c>/
/// <c>InvoiceRequest</c>/<c>Evidence</c> records, created through the real
/// services, parsed correctly into the facts <see cref="KpiEquations"/>
/// computes from — and read as one coherent transaction, however much
/// data exists.
/// </summary>
public sealed class KpiSnapshotReaderTests : IAsyncLifetime
{
    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();
        (_host, _manager) = await ProjectCommercialTestHost.StartAsync(_temp.Path);
        ProjectCommercialTestHost.SignIn(_host);
    }

    public async Task DisposeAsync()
    {
        await _manager.ShutdownAsync();
        await _host.DisposeAsync();
        _temp.Dispose();
    }

    private async Task<Guid> SetUpPricedProjectAsync()
    {
        var projectId = await ProjectCommercialTestHost.CreateProjectAsync(_host);

        var rateCards = ProjectCommercialTestHost.RateCards(_host);
        var card = Tests.BusinessGovernance.BusinessGovernanceFixtures.Card("KPI-READER-CARD") with
        {
            Entries =
            [
                new RateCardEntry(
                    "ENG-1", "Senior engineering", PricingBasis.Hourly,
                    new Money(120m, CurrencyCode.Gbp), new Money(70m, CurrencyCode.Gbp), Grade: "Senior"),
            ],
        };
        await rateCards.RegisterAsync("KPI-READER-CARD", card, Tests.BusinessGovernance.BusinessGovernanceFixtures.Verified());
        await Tests.BusinessGovernance.BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, "KPI-READER-CARD");

        var commercial = ProjectCommercialTestHost.ProjectCommercial(_host);
        await commercial.PinRateCardAsync(projectId, "KPI-READER-CARD");

        return projectId;
    }

    [Fact]
    public async Task ReadKpiAsync_ParsesEveryRealPersistedKind_ThroughTheServiceLayer_IntoTheEquationsRead()
    {
        var projectId = await SetUpPricedProjectAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var period = KpiPeriod.Custom(today.AddDays(-1), today.AddDays(1));
        var domain = ProjectCommercialTestHost.Domain(_host);

        var timesheets = ProjectCommercialTestHost.Timesheets(_host);
        var inPeriod = await timesheets.RecordAsync(projectId, today, 4m, billable: true, "Senior", "In-period work");
        Assert.True(inPeriod.Succeeded, inPeriod.Reason);
        var outOfPeriod = await timesheets.RecordAsync(projectId, today.AddDays(-30), 3m, billable: true, "Senior", "Out-of-period work");
        Assert.True(outOfPeriod.Succeeded, outOfPeriod.Reason);

        var milestoneService = new ProjectMilestoneService(domain);
        var milestone = await milestoneService.CreateMilestoneAsync(projectId, "MS-KPI-READER", "Milestone", DateTimeOffset.UtcNow.AddDays(30));
        var deliverable = await milestoneService.CreateDeliverableAsync(projectId, milestone.Id, "DEL-KPI-READER", "KPI reader test deliverable");
        var deliverables = ProjectCommercialTestHost.Deliverables(_host);
        var completion = await deliverables.CompleteAsync(deliverable.Id, projectId, today, fixedPriceValue: new Money(500m, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded, completion.Reason);

        // A real InvoiceRequest, constructed directly (as `InvoicingService`
        // itself does) already `Sent`, issued 10 days ago and paid 2 days
        // ago — proving InvoiceRequest's own TypeState fields parse
        // correctly, without needing the full connector send/reconcile flow
        // this Work Package does not own.
        await new EngineeringObjectFactory<InvoiceRequest>(
                InvoiceRequest.CanonicalKind, domain,
                (doc, rev) => new InvoiceRequest(
                    doc, rev, domain, identifier: null, "KPI reader test invoice", EngineeringObjectMetadata.Empty,
                    "org-1", null, CurrencyCode.Gbp, [], Money.Zero(CurrencyCode.Gbp),
                    InvoiceRequestStatus.Sent, externalId: "ext-1", issuedDate: today.AddDays(-10), paidDate: today.AddDays(-2)))
            .CreateAsync("KPI reader test invoice raised.");

        var evidenceService = (IEvidenceService)_host.Services!.GetService(typeof(IEvidenceService));
        var evidence = await evidenceService.CreateAsync(projectId, "KPI reader test evidence", EvidenceClassification.Calculation);
        await evidenceService.RecordCheckAsync(evidence.Id, "Checker", "Checking Org", "Looks right.", CheckOutcome.Accepted);
        await evidenceService.IssueAsync(evidence.Id, "ISS-KPI-1", "A", "Client Co");

        var queryableStore = (IQueryablePersistenceStore)_host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var workingPatterns = (IWorkingPatternProvider)_host.Services!.GetService(typeof(IWorkingPatternProvider));
        var service = new KpiSnapshotService(
            new WorkspaceSnapshotReader(queryableStore), workingPatterns,
            now: () => new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        var snapshot = await service.GetSnapshotAsync(period);

        // Utilisation: only the 4h in-period entry counts (the 3h entry
        // dated 30 days ago falls outside `period`).
        var utilisation = Assert.Single(snapshot.Utilisation, u => u.PrincipalIdentityId == ProjectCommercialTestHost.PrincipalId);
        Assert.Equal(4m, utilisation.BillableHours);

        // Margin: revenue = 4h × £120 (in-period entry) + £500 (in-period
        // deliverable) = £980; cost = 4h × £70 (in-period entry only) = £280.
        var margin = Assert.Single(snapshot.MarginByProject);
        Assert.Equal(new Money(980m, CurrencyCode.Gbp), margin.Revenue);
        Assert.Equal(new Money(280m, CurrencyCode.Gbp), margin.Cost);
        Assert.False(margin.AnyMissingCostRate);

        // Work in progress: never period-scoped — both timesheet entries
        // (4h + 3h, £120/h = £840) and the £500 deliverable are still
        // unbilled: £840 + £500 = £1340.
        var wip = Assert.Single(snapshot.WorkInProgress);
        Assert.Equal(new Money(1340m, CurrencyCode.Gbp), wip.Value);

        // Days sales outstanding: one Sent invoice, issued 10 days ago and
        // paid 2 days ago — settled in 8 days.
        Assert.True(snapshot.DaysSalesOutstanding.IsAvailable);
        Assert.Equal(1, snapshot.DaysSalesOutstanding.InvoiceCount);
        Assert.Equal(8m, snapshot.DaysSalesOutstanding.AverageDays);

        // Calc throughput: the one Evidence record issued today, inside `period`.
        Assert.Equal(1, snapshot.CalcThroughput);
    }

    [Fact]
    public async Task ReadKpiAsync_WithNoCommercialDataAtAll_IsHonestlyEmpty_NeverThrows()
    {
        var queryableStore = (IQueryablePersistenceStore)_host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var reader = new WorkspaceSnapshotReader(queryableStore);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var snapshot = await reader.ReadAsync(WorkspaceSnapshotRequest.Kpi(KpiPeriod.ThisWeek(today), today));

        Assert.Equal(WorkspaceSnapshotKind.Kpi, snapshot.Kind);
        var financials = snapshot.Kpi!;
        Assert.Empty(financials.BillableHoursByPrincipal);
        Assert.Empty(financials.MarginByProject);
        Assert.Empty(financials.WorkInProgress);
        Assert.False(financials.DaysSalesOutstanding.IsAvailable);
        Assert.Equal(0, financials.CalcThroughput);
    }

    // ----------------------------------------------------------------
    // Coherence: one call, however much data exists.
    // ----------------------------------------------------------------

    /// <summary>
    /// A real <see cref="IQueryablePersistenceStore"/> that counts how many
    /// times <see cref="ExecuteInReadTransactionAsync{T}"/> is entered —
    /// the same counting-decorator technique
    /// <c>CockpitReadScopeTests.CountingPersistenceStore</c> already
    /// established, applied to the query interface the KPI read actually
    /// goes through (<c>CockpitReadScopeTests</c>'s own decorator wraps
    /// <see cref="IPersistenceStore"/>, which this read never calls).
    /// </summary>
    private sealed class CountingQueryableStore(IQueryablePersistenceStore inner) : IQueryablePersistenceStore
    {
        public int ReadTransactions { get; private set; }

        public long CurrentSequence => inner.CurrentSequence;

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) =>
            inner.ListKeysAsync(collection, keyPrefix, cancellationToken);

        public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) =>
            inner.ReadAllAsync(collection, cancellationToken);

        public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
            inner.ReadManyAsync(collection, keys, cancellationToken);

        public Task ExecuteInTransactionAsync(Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            inner.ExecuteInTransactionAsync(work, cancellationToken);

        public Task<T> ExecuteInReadTransactionAsync<T>(Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default)
        {
            ReadTransactions++;
            return inner.ExecuteInReadTransactionAsync(read, cancellationToken);
        }

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            inner.SearchAsync(query, limit, cancellationToken);

        public Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default) => inner.IsSearchIndexEmptyAsync(cancellationToken);
    }

    [Fact]
    public async Task ReadKpiAsync_PerformsExactlyOneReadTransaction_NoMatterHowManyRecordsExist()
    {
        var projectId = await SetUpPricedProjectAsync();
        var timesheets = ProjectCommercialTestHost.Timesheets(_host);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i < 6; i++)
            await timesheets.RecordAsync(projectId, today, 1m, billable: true, "Senior", $"Entry {i}");

        var innerStore = (IQueryablePersistenceStore)_host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var counting = new CountingQueryableStore(innerStore);
        var reader = new WorkspaceSnapshotReader(counting);

        await reader.ReadAsync(WorkspaceSnapshotRequest.Kpi(KpiPeriod.ThisWeek(today), today));

        Assert.Equal(1, counting.ReadTransactions);
    }
}
