using Tempest.Workspace;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Verification;

namespace Tempest.Workspace.Verification;

/// <summary>
/// The Verification discipline's own Engineering Cockpit read-model —
/// extracted, `WP 12.0B` (`ADR-0103`), from <see cref="EngineeringCockpit"/>'s
/// own previous Verification-specific members, unmodified in behaviour.
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
/// persistence-backed read this discipline needs — the live Activity
/// listing, and one <see cref="VerificationRecordReader"/> read per
/// Activity — happens there, awaited once per Cockpit render
/// (<see cref="EngineeringCockpit.PrimeAsync"/>). Every property below is
/// now a pure, in-memory read of what <see cref="LoadAsync"/> last
/// loaded — still computed once per render, never per property, but
/// without a single blocking call left in this file's own source.
/// </remarks>
internal sealed class VerificationCockpitReadModel
{
    private readonly EngineeringDomainContext _domainContext;
    private IReadOnlyList<IEngineeringObject> _liveActivities = [];
    private IReadOnlyList<(IEngineeringObject Activity, VerificationRecordSnapshot? LatestRecord)> _snapshots = [];
    private int _totalRecords;

    /// <summary>Initialises a new instance of the <see cref="VerificationCockpitReadModel"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this read-model queries directly.</param>
    public VerificationCockpitReadModel(EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(domainContext);

        _domainContext = domainContext;
    }

    /// <summary>Loads every live Verification Activity, its own most recent recorded result, and the total record count across all of them — the three reads every property below is derived from.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var activities = await _domainContext.Repository.ListByKindAsync(VerificationActivityFactoryRegistry.SupportedKind, cancellationToken).ConfigureAwait(false);
        var liveActivities = activities.Where(o => o is not IDeletable { IsDeleted: true }).ToList();
        _liveActivities = liveActivities;

        var snapshots = new List<(IEngineeringObject Activity, VerificationRecordSnapshot? LatestRecord)>(liveActivities.Count);
        var totalRecords = 0;

        foreach (var activity in liveActivities)
        {
            var latest = await VerificationRecordReader.GetLatestAsync(_domainContext, activity.Id, cancellationToken).ConfigureAwait(false);
            snapshots.Add((activity, latest));

            totalRecords += (await VerificationRecordReader.GetResultHistoryAsync(_domainContext, activity.Id, cancellationToken).ConfigureAwait(false)).Count;
        }

        _snapshots = snapshots;
        _totalRecords = totalRecords;
    }

    /// <summary>Gets every live (non-deleted) Verification Activity — loaded by <see cref="LoadAsync"/>.</summary>
    public IReadOnlyList<IEngineeringObject> LiveVerificationActivities => _liveActivities;

    /// <summary>Gets the number of live Verification Activities — the Cockpit's own cross-discipline KPI summary reads this directly.</summary>
    public int Count => LiveVerificationActivities.Count;

    /// <summary>
    /// Gets every live Verification Activity paired with its own most
    /// recent recorded <see cref="VerificationRecordSnapshot"/> — read
    /// via <see cref="VerificationRecordReader"/>, the same generic,
    /// type-erased record read the Property Inspector uses, never a new
    /// traversal. <see langword="null"/> for an Activity never recorded
    /// against.
    /// </summary>
    private IReadOnlyList<(IEngineeringObject Activity, VerificationRecordSnapshot? LatestRecord)> LiveVerificationSnapshots =>
        _snapshots;

    /// <summary>Gets the number of live Verification Activities whose own most recent recorded result has <see cref="VerificationOutcome.Fail"/> — the Cockpit's own "Failed" signal.</summary>
    private int FailedVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Fail);

    /// <summary>Gets the number of live Verification Activities whose own most recent recorded result has <see cref="VerificationOutcome.Conditional"/> — the Cockpit's own "Conditional" signal.</summary>
    private int ConditionalVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Conditional);

    /// <summary>Gets the number of live Verification Activities whose own most recent recorded result has <see cref="VerificationOutcome.Pass"/> — the Cockpit's own "Passed" signal.</summary>
    private int PassedVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord?.Outcome == VerificationOutcome.Pass);

    /// <summary>Gets the number of live Verification Activities with no recorded result yet and <see cref="LifecycleState.Draft"/> status — the Cockpit's own "Planned" signal (`ADR-0090`: a Draft Activity with no result is a Verification Plan).</summary>
    private int PlannedVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord is null && s.Activity is IHasLifecycle { Status: LifecycleState.Draft });

    /// <summary>Gets the number of live Verification Activities with no recorded result yet and <see cref="LifecycleState.InReview"/> (or later) status — the Cockpit's own "In Progress" signal.</summary>
    private int InProgressVerificationCount =>
        LiveVerificationSnapshots.Count(s => s.LatestRecord is null && s.Activity is IHasLifecycle { Status: not LifecycleState.Draft });

    /// <summary>Gets the number of live Verification Activities that are <see cref="LifecycleState.InReview"/> with no recorded result yet, plus every <see cref="FailedVerificationCount"/> — the Cockpit's own "Outstanding" signal.</summary>
    public int OutstandingActions
    {
        get
        {
            var snapshots = LiveVerificationSnapshots;
            var awaitingResult = snapshots.Count(s => s.LatestRecord is null && s.Activity is IHasLifecycle { Status: LifecycleState.InReview });

            return awaitingResult + FailedVerificationCount;
        }
    }

    /// <summary>Gets the total number of real <see cref="IVerificationRecord"/>s recorded across every live Verification Activity — the Cockpit's own "Total Verification Records" KPI, distinct from the Activity count itself.</summary>
    private int TotalVerificationRecordsCount => _totalRecords;

    /// <summary>
    /// Gets the Verification discipline's own status:
    /// <see cref="EngineeringHealthStatus.Unknown"/> if no live
    /// Verification Activity exists yet;
    /// <see cref="EngineeringHealthStatus.Blocked"/> if any live
    /// Activity's own most recent recorded result has
    /// <see cref="VerificationOutcome.Fail"/>;
    /// <see cref="EngineeringHealthStatus.Attention"/> if any is
    /// <see cref="OutstandingActions"/> with no Fail present;
    /// <see cref="EngineeringHealthStatus.Healthy"/> otherwise.
    /// </summary>
    public EngineeringHealthStatus Status
    {
        get
        {
            if (LiveVerificationActivities.Count == 0)
                return EngineeringHealthStatus.Unknown;

            if (FailedVerificationCount > 0)
                return EngineeringHealthStatus.Blocked;

            return OutstandingActions > 0
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Verification discipline's own dedicated KPI card set:
    /// Total Verification Records, Planned, In Progress, Passed, Failed,
    /// Conditional, Outstanding, Verification Coverage, Project
    /// Verification Health.
    /// </summary>
    public IReadOnlyList<CockpitKpiCard> KpiCards
    {
        get
        {
            var snapshots = LiveVerificationSnapshots;
            var total = snapshots.Count;
            var recorded = snapshots.Count(s => s.LatestRecord is not null);

            return
            [
                new("Total Verification Records", TotalVerificationRecordsCount.ToString(), IsPlaceholder: false),
                new("Planned", PlannedVerificationCount.ToString(), IsPlaceholder: false),
                new("In Progress", InProgressVerificationCount.ToString(), IsPlaceholder: false),
                new("Passed", PassedVerificationCount.ToString(), IsPlaceholder: false),
                new("Failed", FailedVerificationCount.ToString(), IsPlaceholder: false),
                new("Conditional", ConditionalVerificationCount.ToString(), IsPlaceholder: false),
                new("Outstanding", OutstandingActions.ToString(), IsPlaceholder: false),
                new("Verification Coverage", CockpitFormatting.FormatCoverage(recorded, total), IsPlaceholder: false, CockpitFormatting.PercentOf(recorded, total)),
                new("Project Verification Health", Status.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>Gets this discipline's own "What Needs Attention" contribution — a base entry, plus a conditional second entry when <see cref="OutstandingActions"/> is non-zero.</summary>
    public IReadOnlyList<CockpitAttentionItem> GetAttentionItems()
    {
        var items = new List<CockpitAttentionItem>
        {
            LiveVerificationActivities.Count > 0
                ? new("Verification is live", $"{LiveVerificationActivities.Count} Verification Activity(ies) registered - the Project Explorer's own Verification area and the Engineering Cockpit's own Verification KPIs reflect real Engineering Domain/Verification Framework data (WP 9.3A).")
                : new("No Verification Activities registered yet", "The Verification area has no live Verification Activity yet - this is expected, not a defect."),
        };

        if (OutstandingActions > 0)
        {
            items.Add(new(
                "Verification needs attention",
                $"{OutstandingActions} Verification Activity(ies) awaiting a recorded result or with a Fail outcome across {LiveVerificationActivities.Count} live activity(ies). See the Verification area's own Property Inspector for detail."));
        }

        return items;
    }

    /// <summary>Gets this discipline's own "Open Actions" triage entry, or <see langword="null"/> if nothing is currently outstanding.</summary>
    public CockpitActionItem? GetOpenActionItem() =>
        OutstandingActions > 0
            ? new($"Triage {OutstandingActions} outstanding Verification Activity(ies) (awaiting result or Failed)", "Engineer")
            : null;

    /// <summary>Gets this discipline's own "Blocked Items" contribution — one message per live Verification Activity with a Fail outcome.</summary>
    public IReadOnlyList<string> GetBlockedMessages() =>
        LiveVerificationSnapshots
            .Where(s => s.LatestRecord?.Outcome == VerificationOutcome.Fail)
            .Select(s => $"Verification Activity '{((IHasBusinessIdentifier)s.Activity).DisplayName}' recorded a Fail outcome.")
            .ToList();
}
