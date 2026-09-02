using Tempest.App.Workspace;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

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
internal sealed class CalculationsCockpitReadModel
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly CockpitReadCell<IReadOnlyList<ICalculation>> _liveCalculations;
    private readonly CockpitReadCell<IReadOnlyList<(ICalculation Calculation, CalculationRecordSnapshot? LatestRecord)>> _snapshots;
    private readonly CockpitReadCell<IReadOnlyDictionary<Guid, DateTimeOffset>> _latestRevisedAt;

    /// <summary>Initialises a new instance of the <see cref="CalculationsCockpitReadModel"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this read-model queries directly.</param>
    /// <param name="scope">The Cockpit's own per-refresh read scope (`WP-E`) — see <see cref="CockpitReadScope"/>.</param>
    public CalculationsCockpitReadModel(EngineeringDomainContext domainContext, CockpitReadScope scope)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(scope);

        _domainContext = domainContext;

        _liveCalculations = scope.Cell<IReadOnlyList<ICalculation>>(() =>
            _domainContext.Repository.ListByKindAsync("Calculation").GetAwaiter().GetResult()
                .Where(o => o is not IDeletable { IsDeleted: true })
                .OfType<ICalculation>()
                .ToList());

        // The two persistence-backed leaves (`WP-E`): one
        // CalculationRecordReader read per Calculation, and one revision
        // history per Calculation behind IsOutOfDate — each previously
        // repeated by every count and card set derived from it.
        _snapshots = scope.Cell<IReadOnlyList<(ICalculation Calculation, CalculationRecordSnapshot? LatestRecord)>>(() =>
            LiveCalculations
                .Select(c => (c, CalculationRecordReader.GetLatestAsync(_domainContext, c.Id).GetAwaiter().GetResult()))
                .ToList());

        _latestRevisedAt = scope.Cell<IReadOnlyDictionary<Guid, DateTimeOffset>>(ReadLatestRevisedAt);
    }

    /// <summary>Gets every live (non-deleted) Calculation — a real read via <see cref="EngineeringDomainContext.Repository"/>.</summary>
    public IReadOnlyList<ICalculation> LiveCalculations => _liveCalculations.Value;

    /// <summary>
    /// Every live Calculation's own most recent revision timestamp, read
    /// once (`WP-E`) — falling back to the object's own
    /// <see cref="IEngineeringObject.CreatedAt"/> where it has never been
    /// revised, exactly as <see cref="IsOutOfDate"/> did inline before.
    /// </summary>
    private IReadOnlyDictionary<Guid, DateTimeOffset> ReadLatestRevisedAt()
    {
        var revisedAt = new Dictionary<Guid, DateTimeOffset>();

        foreach (var calculation in LiveCalculations)
        {
            var revisions = _domainContext.Store.GetRevisionHistoryAsync(calculation.Id).GetAwaiter().GetResult();
            revisedAt[calculation.Id] = revisions.Count > 0 ? revisions[^1].CreatedAt : calculation.CreatedAt;
        }

        return revisedAt;
    }

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
        _snapshots.Value;

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

        var latestRevisedAt = _latestRevisedAt.Value.TryGetValue(calculation.Id, out var revisedAt)
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
