using Tempest.Workspace;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Calculations;

/// <summary>
/// The Calculations discipline's own Engineering Cockpit read-model —
/// extracted, `WP 12.0B` (`ADR-0103`), from <see cref="EngineeringCockpit"/>'s
/// own previous Calculations-specific members, unmodified in behaviour.
/// A collaborator under `ADR-0103`: constructed once by
/// <see cref="EngineeringCockpit"/> (the composition root), declaring
/// only the one dependency it actually needs, never DI-registered, never
/// referencing <see cref="EngineeringCockpit"/> or any sibling
/// discipline collaborator back.
/// </summary>
/// <remarks>
/// <b>`WP 18.1A-R1`.</b> Supersedes `WP-E`'s own <see cref="CockpitReadScope"/>-backed
/// memoisation (a lazy cell, computed once per open <c>Begin()</c> pass
/// but still blocked on synchronously outside one — the exact shape
/// `TD-108`/`TD-118` found) with an eager <see cref="LoadAsync"/>: every
/// persistence-backed read this discipline needs — the live Calculation
/// listing, one <see cref="CalculationRecordReader"/> read per
/// Calculation, and one revision history read per Calculation — happens
/// there, awaited once per Cockpit render
/// (<see cref="EngineeringCockpit.PrimeAsync"/>). Every property below is
/// now a pure, in-memory read of what <see cref="LoadAsync"/> last
/// loaded — still computed once per render, never per property, but
/// without a single blocking call left in this file's own source.
/// </remarks>
internal sealed class CalculationsCockpitReadModel
{
    private readonly EngineeringDomainContext _domainContext;
    private IReadOnlyList<ICalculation> _liveCalculations = [];
    private IReadOnlyList<(ICalculation Calculation, CalculationRecordSnapshot? LatestRecord)> _snapshots = [];
    private IReadOnlyDictionary<Guid, DateTimeOffset> _latestRevisedAt = new Dictionary<Guid, DateTimeOffset>();

    /// <summary>Initialises a new instance of the <see cref="CalculationsCockpitReadModel"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this read-model queries directly.</param>
    public CalculationsCockpitReadModel(EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(domainContext);

        _domainContext = domainContext;
    }

    /// <summary>Loads every live Calculation, its own most recent executed record, and its own most recent revision timestamp — the three reads every property below is derived from.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var calculations = await _domainContext.Repository.ListByKindAsync("Calculation", cancellationToken).ConfigureAwait(false);
        var live = calculations
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<ICalculation>()
            .ToList();
        _liveCalculations = live;

        var snapshots = new List<(ICalculation Calculation, CalculationRecordSnapshot? LatestRecord)>(live.Count);
        var revisedAt = new Dictionary<Guid, DateTimeOffset>();

        foreach (var calculation in live)
        {
            var latest = await CalculationRecordReader.GetLatestAsync(_domainContext, calculation.Id, cancellationToken).ConfigureAwait(false);
            snapshots.Add((calculation, latest));

            var revisions = await _domainContext.Store.GetRevisionHistoryAsync(calculation.Id, cancellationToken).ConfigureAwait(false);
            revisedAt[calculation.Id] = revisions.Count > 0 ? revisions[^1].CreatedAt : calculation.CreatedAt;
        }

        _snapshots = snapshots;
        _latestRevisedAt = revisedAt;
    }

    /// <summary>Gets every live (non-deleted) Calculation — loaded by <see cref="LoadAsync"/>.</summary>
    public IReadOnlyList<ICalculation> LiveCalculations => _liveCalculations;

    /// <summary>Gets the number of live Calculations — the Cockpit's own cross-discipline KPI summary reads this directly.</summary>
    public int Count => LiveCalculations.Count;

    /// <summary>
    /// Gets every live Calculation paired with its own most recent
    /// executed <see cref="CalculationRecordSnapshot"/> — read via
    /// <see cref="CalculationRecordReader"/>, the same generic,
    /// type-erased record read the Property Inspector uses, never a new
    /// traversal. <see langword="null"/> for a Calculation never
    /// executed.
    /// </summary>
    private IReadOnlyList<(ICalculation Calculation, CalculationRecordSnapshot? LatestRecord)> LiveCalculationSnapshots =>
        _snapshots;

    /// <summary>
    /// Gets whether <paramref name="calculation"/> has been revised more
    /// recently than <paramref name="latestRecord"/> was executed — a
    /// disclosed heuristic for "Out-of-date": the object's own written
    /// content has changed since its own most recent evidentiary
    /// execution, so that execution's own result no longer necessarily
    /// reflects it. <see langword="false"/> if never executed.
    /// </summary>
    private bool IsOutOfDate(ICalculation calculation, CalculationRecordSnapshot? latestRecord)
    {
        if (latestRecord is null)
            return false;

        var latestRevisedAt = _latestRevisedAt.TryGetValue(calculation.Id, out var revisedAt)
            ? revisedAt
            : calculation.CreatedAt;

        return latestRevisedAt > latestRecord.ExecutedAt;
    }

    /// <summary>Gets the number of live Calculations whose own most recent execution recorded a <see cref="CalculationValidationOutcome.Conditional"/> outcome — the Cockpit's own "Failed" signal.</summary>
    private int FailedCalculationsCount =>
        LiveCalculationSnapshots.Count(s => s.LatestRecord?.Outcome == CalculationValidationOutcome.Conditional);

    /// <summary>Gets the number of live Calculations that are <see cref="LifecycleState.InReview"/>.</summary>
    public int InReviewCount => LiveCalculationSnapshots.Count(s => s.Calculation is IHasLifecycle { Status: LifecycleState.InReview });

    /// <summary>Gets the number of live Calculations that are <see cref="LifecycleState.InReview"/> or <see cref="IsOutOfDate"/> — the Cockpit's own "Calculations awaiting review"/"Outstanding Actions" signal.</summary>
    public int OutstandingActions
    {
        get
        {
            var snapshots = LiveCalculationSnapshots;
            var awaitingReview = snapshots.Count(s => s.Calculation is IHasLifecycle { Status: LifecycleState.InReview });
            var outOfDate = snapshots.Count(s => IsOutOfDate(s.Calculation, s.LatestRecord));

            return awaitingReview + outOfDate;
        }
    }

    /// <summary>
    /// Gets the Calculations discipline's own status: <see cref="EngineeringHealthStatus.Unknown"/>
    /// if no live Calculation exists yet; <see cref="EngineeringHealthStatus.Blocked"/>
    /// if any live Calculation's own most recent execution recorded a
    /// <see cref="CalculationValidationOutcome.Conditional"/> outcome
    /// ("Failed"); <see cref="EngineeringHealthStatus.Attention"/> if any
    /// is awaiting review or out-of-date, with no failure present;
    /// <see cref="EngineeringHealthStatus.Healthy"/> otherwise.
    /// </summary>
    public EngineeringHealthStatus Status
    {
        get
        {
            if (LiveCalculations.Count == 0)
                return EngineeringHealthStatus.Unknown;

            if (FailedCalculationsCount > 0)
                return EngineeringHealthStatus.Blocked;

            return OutstandingActions > 0
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Calculations discipline's own dedicated KPI card set:
    /// Total, Draft, Review, Approved, Failed, Out-of-date, Verification
    /// Coverage, Calculation Health.
    /// </summary>
    public IReadOnlyList<CockpitKpiCard> KpiCards
    {
        get
        {
            var snapshots = LiveCalculationSnapshots;
            var total = snapshots.Count;

            int CountStatus(LifecycleState status) =>
                snapshots.Count(s => s.Calculation is IHasLifecycle lifecycle && lifecycle.Status == status);

            var executed = snapshots.Count(s => s.LatestRecord is not null);
            var outOfDate = snapshots.Count(s => IsOutOfDate(s.Calculation, s.LatestRecord));

            return
            [
                new("Total Calculations", total.ToString(), IsPlaceholder: false),
                new("Draft", CountStatus(LifecycleState.Draft).ToString(), IsPlaceholder: false),
                new("Review", InReviewCount.ToString(), IsPlaceholder: false),
                new("Approved", CountStatus(LifecycleState.Approved).ToString(), IsPlaceholder: false),
                new("Failed", FailedCalculationsCount.ToString(), IsPlaceholder: false),
                new("Out-of-date", outOfDate.ToString(), IsPlaceholder: false),
                new("Verification Coverage", CockpitFormatting.FormatCoverage(executed, total), IsPlaceholder: false, CockpitFormatting.PercentOf(executed, total)),
                new("Calculation Health", Status.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>Gets this discipline's own "What Needs Attention" contribution — a base entry, plus a conditional second entry when <see cref="OutstandingActions"/> is non-zero.</summary>
    public IReadOnlyList<CockpitAttentionItem> GetAttentionItems()
    {
        var items = new List<CockpitAttentionItem>
        {
            LiveCalculations.Count > 0
                ? new("Calculations are live", $"{LiveCalculations.Count} Calculation(s) registered - the Project Explorer's own Calculations area and the Engineering Cockpit's own Calculations KPIs reflect real Calculation Framework data (WP 9.2A).")
                : new("No Calculations registered yet", "The Calculations area has no live Calculation yet - this is expected, not a defect."),
        };

        if (OutstandingActions > 0)
        {
            items.Add(new(
                "Calculations need attention",
                $"{OutstandingActions} Calculation(s) awaiting review or out-of-date across {LiveCalculations.Count} live calculation(s). See the Calculations area's own Property Inspector for detail."));
        }

        return items;
    }

    /// <summary>Gets this discipline's own "Open Actions" triage entry, or <see langword="null"/> if nothing is currently outstanding.</summary>
    public CockpitActionItem? GetOpenActionItem() =>
        OutstandingActions > 0
            ? new($"Triage {OutstandingActions} outstanding Calculation(s) (awaiting review or out-of-date)", "Engineer")
            : null;

    /// <summary>Gets this discipline's own "Blocked Items" contribution — one message per live Calculation with a Conditional (Failed) outcome.</summary>
    public IReadOnlyList<string> GetBlockedMessages() =>
        LiveCalculationSnapshots
            .Where(s => s.LatestRecord?.Outcome == CalculationValidationOutcome.Conditional)
            .Select(s => $"Calculation '{((IHasBusinessIdentifier)s.Calculation).DisplayName}' recorded a Conditional (Failed) outcome.")
            .ToList();
}
