using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;
using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Requirements;

namespace Tempest.Workspace;

/// <summary>
/// One entry in <see cref="EngineeringCockpit.RecentlyChanged"/> (`WP 18.1B`
/// §4): a live object's own title, Kind, what kind of change it was, and
/// when — the Home cockpit's own "Recently changed" card.
/// </summary>
/// <param name="ObjectId">The changed object's own id.</param>
/// <param name="Title">The changed object's own current title.</param>
/// <param name="Kind">The changed object's own canonical Kind.</param>
/// <param name="ChangeType">A short, human-readable description of what changed — "Created", "Renamed", "Status changed", and so on.</param>
/// <param name="When">When the change was recorded.</param>
public sealed record CockpitRecentChange(Guid ObjectId, string Title, string Kind, string ChangeType, DateTimeOffset When);

/// <summary>
/// The Engineering Cockpit — the Workspace's own default landing screen
/// (`ADR-0069`) and, per its own `WP 8.1C` controlling instruction, the
/// answer to four questions on every visit: where am I, what needs
/// attention, is the project healthy, and what should I do next. Not one
/// of the twelve `WP8.0B Workspace Contracts.md` interfaces — a genuine,
/// disclosed implementation-phase addition, reached only through
/// <see cref="Workspace.Cockpit"/> internally, mirroring
/// <see cref="WorkspaceManager.StatusBar"/>'s own `WP 8.1A` precedent.
/// </summary>
/// <remarks>
/// <para>
/// **Composition root, `WP 12.0B` (`ADR-0103`).** Every per-discipline
/// read (Mechanical/Requirements/Calculations/Documents/Verification/
/// Manufacturing) previously lived directly on this class; each now
/// lives in its own discipline collaborator
/// (<see cref="MechanicalCockpitReadModel"/>,
/// <see cref="RequirementsCockpitReadModel"/>,
/// <see cref="CalculationsCockpitReadModel"/>,
/// <see cref="DocumentsCockpitReadModel"/>,
/// <see cref="VerificationCockpitReadModel"/>,
/// <see cref="ManufacturingCockpitReadModel"/>), constructed once here
/// with <c>new</c> and never DI-registered, per <c>ADR-0103</c>'s own
/// rules. This class's own public surface — every property, every
/// signature, every return value — is unchanged: each single-discipline
/// member now delegates to its own collaborator; the genuinely
/// cross-discipline members (<see cref="Health"/>,
/// <see cref="HealthScoreDisplay"/>, <see cref="KpiCards"/>,
/// <see cref="AttentionItems"/>, <see cref="OpenActions"/>,
/// <see cref="BlockedItems"/>) remain here, reading from more than one
/// collaborator at once — the composition root's own "wire the
/// cross-collaborator bridges that have no single natural owner"
/// responsibility (`ADR-0103`), not a gap in the decomposition. Governance
/// & Risk family reads (Decisions/Risks/Milestones/Tasks) and genuinely
/// cross-cutting Workspace reads (Navigation/Commands) are not part of
/// any of the six named disciplines and remain directly on this class
/// too, unchanged.
/// </para>
/// <para>
/// Introduces no calculation, verification, or Digital Thread traversal
/// logic of its own (`WP 8.1C`'s own explicit scope boundary) — every
/// region that would need one of those services today shows fixed,
/// representative placeholder content instead, disclosed either via
/// <see cref="CockpitKpiCard.IsPlaceholder"/>,
/// <see cref="EngineeringHealthStatus.Unknown"/>, or by this class's own
/// XML documentation.
/// </para>
/// <para>
/// <b>Real vs. placeholder, stated once, plainly:</b> <see cref="RecentActivity"/>,
/// <see cref="ContinueWhereILeftOff"/>, <see cref="AreaCount"/>,
/// <see cref="OpenDocumentCount"/>, and <see cref="AvailableCommands"/> are
/// live reads of real Workspace state. Requirements/Calculations/
/// Documents/Verification/Manufacturing each carry real reads, sourced
/// from their own dedicated discipline collaborator; Materials, Risk,
/// Decision, and Milestone reads remain out of scope for the six named
/// disciplines and are read directly here (Decisions/Risks/Milestones
/// are real; Materials is not wired to the Workspace at all).
/// </para>
/// </remarks>
public sealed class EngineeringCockpit
{
    private readonly NavigationService _navigationService;
    private readonly ICommandRegistry _commandRegistry;
    private readonly EngineeringDomainContext _domainContext;
    private readonly Func<DateTimeOffset> _now;
    private readonly IRequirementValidationService _requirementValidationService;

    private readonly MechanicalCockpitReadModel _mechanical;
    private readonly RequirementsCockpitReadModel _requirements;
    private readonly CalculationsCockpitReadModel _calculations;
    private readonly DocumentsCockpitReadModel _documents;
    private readonly VerificationCockpitReadModel _verification;
    private readonly ManufacturingCockpitReadModel _manufacturing;
    private readonly IAuditQuery? _auditQuery;

    // `WP 18.1A-R1` — every cross-cutting read this composition root itself
    // performs (Decisions/Risks/Milestones/Tasks/DigitalThread/RecentlyChanged)
    // now loads inside PrimeAsync, into these fields, rather than blocking
    // synchronously on every property access (TD-108, TD-118). Defaulted to
    // an honest empty/zero state so a caller that reads a property before
    // ever calling PrimeAsync sees "nothing yet" rather than a null
    // reference — the same "honest empty" discipline every other Cockpit
    // region already follows.
    private IReadOnlyList<IDecision> _liveDecisions = [];
    private IReadOnlyList<IRisk> _liveRisks = [];
    private IReadOnlyList<IMilestone> _liveMilestones = [];
    private IReadOnlyList<ITask> _liveTasks = [];
    private string _digitalThreadSummary = "0 links tracked (no live Engineering objects exist yet).";
    private IReadOnlyList<CockpitRecentChange> _recentlyChanged = [];

    /// <summary>
    /// Initialises a new instance of the <see cref="EngineeringCockpit"/>
    /// class — internal: only <see cref="WorkspaceManager.StartAsync"/>
    /// ever constructs one, and its <see cref="NavigationService"/>
    /// parameter is itself internal (not one of the twelve `WP8.0B
    /// Workspace Contracts.md` interfaces), so the class is public (`WP
    /// 17.2B` — reached through <see cref="IWorkspace.Cockpit"/>) while
    /// this constructor stays same-assembly-only.
    /// </summary>
    /// <param name="auditQuery">
    /// The durable source <see cref="RecentlyChanged"/> reads (`WP 18.1B`
    /// §4) — <see langword="null"/> (the default, so every existing caller
    /// and test compiles unchanged) leaves that card honestly empty rather
    /// than failing.
    /// </param>
    internal EngineeringCockpit(
        NavigationService navigationService, ICommandRegistry commandRegistry, EngineeringDomainContext domainContext,
        IRequirementsService requirementsService, IRequirementValidationService requirementValidationService,
        Func<DateTimeOffset>? now = null, IAuditQuery? auditQuery = null)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(requirementValidationService);

        _navigationService = navigationService;
        _commandRegistry = commandRegistry;
        _domainContext = domainContext;
        _requirementValidationService = requirementValidationService;
        _auditQuery = auditQuery;

        // The clock "overdue" is measured against. Optional, so every
        // existing caller is unchanged; injectable so a test can state the
        // date rather than depend on the day it runs.
        _now = now ?? (() => DateTimeOffset.UtcNow);

        // Composition root (ADR-0103): each collaborator is constructed
        // exactly once, with `new`, receiving only the dependencies it
        // actually requires — never the whole set above "in case."
        _mechanical = new MechanicalCockpitReadModel(domainContext);
        _requirements = new RequirementsCockpitReadModel(requirementsService, requirementValidationService);
        _calculations = new CalculationsCockpitReadModel(domainContext);
        _documents = new DocumentsCockpitReadModel(domainContext);
        _verification = new VerificationCockpitReadModel(domainContext);
        _manufacturing = new ManufacturingCockpitReadModel(domainContext);
    }

    /// <summary>
    /// Loads one coherent Cockpit render pass (`WP 18.1A-R1`, superseding
    /// `WP-E`'s own <see cref="IDisposable"/> read-scope handle): awaits
    /// every persistence-backed read this Cockpit and its six discipline
    /// collaborators need, exactly once, before returning — no property on
    /// this class or on any collaborator performs I/O of its own any more.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A surface that renders the whole Cockpit — <c>CockpitView.RefreshAsync</c>
    /// (`Tempest.Desktop`) is the one that does — awaits this before
    /// reading anything else. Skipping it leaves every property at its
    /// last-loaded value (an honest empty/zero state before the first
    /// call), never at a value read live and never blocking: `TD-108`/`TD-118`
    /// found `WP-E`'s own scope still blocked synchronously on every read
    /// taken outside an open pass, which is the shape this method exists
    /// to remove.
    /// </para>
    /// <para>
    /// Each discipline collaborator's own <c>LoadAsync</c> runs in turn
    /// rather than concurrently — a render is not latency-critical enough
    /// to justify the added complexity of fanning out across six
    /// independent I/O sources that do not share a transaction, and
    /// sequencing keeps the failure mode simple: the first collaborator to
    /// fault is the one <c>CockpitView.RefreshAsync</c> reports.
    /// </para>
    /// </remarks>
    public async Task PrimeAsync(CancellationToken cancellationToken = default)
    {
        await _mechanical.LoadAsync(cancellationToken).ConfigureAwait(false);
        await _requirements.LoadAsync(cancellationToken).ConfigureAwait(false);
        await _calculations.LoadAsync(cancellationToken).ConfigureAwait(false);
        await _documents.LoadAsync(cancellationToken).ConfigureAwait(false);
        await _verification.LoadAsync(cancellationToken).ConfigureAwait(false);
        await _manufacturing.LoadAsync(cancellationToken).ConfigureAwait(false);

        _liveDecisions = (await _domainContext.Repository.ListByKindAsync("Decision", cancellationToken).ConfigureAwait(false))
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<IDecision>()
            .ToList();

        var risks = new List<IRisk>();
        foreach (var kind in new[] { "Risk", "Hazard" })
        {
            risks.AddRange((await _domainContext.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false))
                .Where(o => o is not IDeletable { IsDeleted: true })
                .OfType<IRisk>());
        }

        _liveRisks = risks;

        _liveMilestones = (await _domainContext.Repository.ListByKindAsync("Milestone", cancellationToken).ConfigureAwait(false))
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<IMilestone>()
            .ToList();

        var tasks = new List<ITask>();
        foreach (var kind in new[] { CanonicalObjectKinds.Task, CanonicalObjectKinds.Action })
        {
            tasks.AddRange((await _domainContext.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false))
                .Where(o => o is not IDeletable { IsDeleted: true })
                .OfType<ITask>());
        }

        _liveTasks = tasks;

        var liveObjects = (await _domainContext.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false))
            .Where(o => o is not IDeletable { IsDeleted: true })
            .ToList();

        if (liveObjects.Count == 0)
        {
            _digitalThreadSummary = "0 links tracked (no live Engineering objects exist yet).";
        }
        else
        {
            var totalLinks = 0;
            foreach (var liveObject in liveObjects)
                totalLinks += (await _domainContext.RelationshipRepository.GetOutgoingAsync(liveObject.Id, cancellationToken).ConfigureAwait(false)).Count;

            _digitalThreadSummary = $"{totalLinks} link(s) tracked across {liveObjects.Count} live object(s).";
        }

        _recentlyChanged = await LoadRecentlyChangedAsync(cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------
    // Where am I?
    // ------------------------------------------------------------

    /// <summary>Gets the most-recently-created live Mechanical Product Structure <c>Project</c>'s own display name — a real read, honestly reporting "No Mechanical Project yet" if none exists.</summary>
    public string ProjectName => _mechanical.ProjectName;

    /// <summary>
    /// Gets the most-recently-opened or jumped-to object - a real read of
    /// <see cref="NavigationService.RecentItems"/>'s own first (most
    /// recent) entry, or <see langword="null"/> if nothing has been opened
    /// yet this session. The Cockpit's own "Continue Where I Left Off."
    /// </summary>
    public RecentNavigationItem? ContinueWhereILeftOff => _navigationService.RecentItems.Count > 0
        ? _navigationService.RecentItems[0]
        : null;

    /// <summary>Gets every live Mechanical Product Structure <c>Project</c>'s own display name — a real read; empty, honestly, if none exist yet.</summary>
    public IReadOnlyList<string> RecentProjects => _mechanical.RecentProjects;

    /// <summary>Gets the Requirements discipline's own status — see <see cref="RequirementsCockpitReadModel.Status"/>.</summary>
    public EngineeringHealthStatus RequirementsStatus => _requirements.Status;

    /// <summary>Gets the Verification discipline's own status — see <see cref="VerificationCockpitReadModel.Status"/>.</summary>
    public EngineeringHealthStatus VerificationStatus => _verification.Status;

    /// <summary>Gets the Calculations discipline's own status — see <see cref="CalculationsCockpitReadModel.Status"/>.</summary>
    public EngineeringHealthStatus CalculationStatus => _calculations.Status;

    /// <summary>Gets the Documentation discipline's own status — see <see cref="DocumentsCockpitReadModel.Status"/>.</summary>
    public EngineeringHealthStatus DocumentationStatus => _documents.Status;

    /// <summary>Gets the Review discipline's own status - always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus ReviewStatus => EngineeringHealthStatus.Unknown;

    /// <summary>Gets the Manufacturing discipline's own status — see <see cref="ManufacturingCockpitReadModel.Status"/>.</summary>
    public EngineeringHealthStatus ManufacturingStatus => _manufacturing.Status;

    /// <summary>Gets the total count of Requirements validation findings (errors plus warnings) across every live requirement - the Cockpit's own "Outstanding Actions" KPI.</summary>
    public int OutstandingRequirementActions => _requirements.OutstandingActions;

    /// <summary>Gets the number of live Calculations that are <see cref="LifecycleState.InReview"/> or out-of-date - the Cockpit's own "Calculations awaiting review"/"Outstanding Actions" signal.</summary>
    public int OutstandingCalculationActions => _calculations.OutstandingActions;

    /// <summary>Gets the number of live Documents that are <see cref="LifecycleState.InReview"/> - the Cockpit's own "Outstanding Reviews" KPI/"Outstanding Actions" signal.</summary>
    public int OutstandingDocumentReviews => _documents.OutstandingReviews;

    /// <summary>Gets the number of live Documents that are <see cref="LifecycleState.InReview"/> or have missing evidence - the Cockpit's own "Documents need attention"/"Outstanding Actions" signal.</summary>
    public int OutstandingDocumentActions => _documents.OutstandingActions;

    /// <summary>Gets the number of live Verification Activities that are <see cref="LifecycleState.InReview"/> with no recorded result yet, plus every Failed - the Cockpit's own "Outstanding" signal.</summary>
    public int OutstandingVerificationActions => _verification.OutstandingActions;

    /// <summary>Gets the number of outstanding Manufacturing items awaiting action - the Cockpit's own combined "awaiting action" signal.</summary>
    public int OutstandingManufacturingActions => _manufacturing.OutstandingActions;

    /// <summary>
    /// Gets the favourited projects list - always empty today: favouriting
    /// is not a capability this platform has built anywhere yet, so an
    /// honest empty state is shown rather than fabricated sample favourites.
    /// </summary>
    public IReadOnlyList<string> FavouriteProjects { get; } = [];

    // ------------------------------------------------------------
    // What needs attention?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the "What Needs Attention" region's own entries
    /// (`WP8.0C Engineering Cockpit Specification.md` §3) — each
    /// discipline's own contribution, concatenated in fixed order
    /// (Mechanical, Requirements, Calculations, Documents, Verification,
    /// Manufacturing), plus a trailing fixed entry for every discipline
    /// still not wired to the Workspace. This composition root's own
    /// cross-collaborator wiring (`ADR-0103`) — each collaborator decides
    /// its own contribution's content; only the concatenation order lives
    /// here.
    /// </summary>
    public IReadOnlyList<CockpitAttentionItem> AttentionItems
    {
        get
        {
            var items = new List<CockpitAttentionItem>();

            items.AddRange(_mechanical.GetAttentionItems());
            items.AddRange(_requirements.GetAttentionItems());
            items.AddRange(_calculations.GetAttentionItems());
            items.AddRange(_documents.GetAttentionItems());
            items.AddRange(_verification.GetAttentionItems());
            items.AddRange(_manufacturing.GetAttentionItems());

            items.Add(new("Other disciplines still placeholder", "Materials remain out of the Workspace's own scope until their own Work Package integrates them."));

            return items;
        }
    }

    /// <summary>Gets every live (non-deleted) Decision - loaded by <see cref="PrimeAsync"/>. The first Cockpit consumer of the Governance &amp; Risk family (<see cref="IDecision"/>, `WP 8.2C`) - previously compiled but never read by any Workspace surface until `WP 10.1A`. Not one of the six named `ADR-0103` disciplines - remains a direct, cross-cutting read on this composition root.</summary>
    private IReadOnlyList<IDecision> LiveDecisions => _liveDecisions;

    /// <summary>Gets every live (non-deleted) Risk-family object (`"Risk"`/`"Hazard"` Kinds - <see cref="IHazard"/> is itself an <see cref="IRisk"/>) - loaded by <see cref="PrimeAsync"/>.</summary>
    private IReadOnlyList<IRisk> LiveRisks => _liveRisks;

    /// <summary>Gets every live (non-deleted) Milestone - loaded by <see cref="PrimeAsync"/>.</summary>
    private IReadOnlyList<IMilestone> LiveMilestones => _liveMilestones;

    /// <summary>
    /// Gets the "Open Decisions" region's own entries - a real read of
    /// live <see cref="LiveDecisions"/>, each shown as its own display
    /// name plus <see cref="IDecision.Rationale"/>. Honestly empty if
    /// none exist.
    /// </summary>
    public IReadOnlyList<string> OpenDecisions =>
        LiveDecisions.Select(d => $"{((IHasBusinessIdentifier)d).DisplayName} — {d.Rationale}").ToList();

    /// <summary>
    /// Gets the "Blocked Items" region's own entries - a real, disclosed
    /// synthesis, not a native Domain concept: the concrete, real objects
    /// whose own most recent evidence is exactly why their own
    /// discipline already reports <see cref="EngineeringHealthStatus.Blocked"/>.
    /// Cross-discipline aggregation (`ADR-0103`): each of the four
    /// contributing disciplines' own collaborator formats its own
    /// contribution; this composition root only concatenates, in the
    /// same fixed order the pre-decomposition implementation always used.
    /// </summary>
    public IReadOnlyList<string> BlockedItems
    {
        get
        {
            var items = new List<string>();

            items.AddRange(_requirements.GetBlockedMessages());
            items.AddRange(_calculations.GetBlockedMessages());
            items.AddRange(_verification.GetBlockedMessages());
            items.AddRange(_manufacturing.GetBlockedMessages());

            return items;
        }
    }

    /// <summary>
    /// Gets the "Overdue Actions" region's own entries — real overdue work,
    /// read from the domain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property was an empty list for as long as it existed, and said
    /// so: "a disclosed, honest placeholder, deliberately not upgraded: no
    /// due-date field exists anywhere in this Domain to compute overdue
    /// from honestly". That was the right call then. It is wired up now
    /// because the reason is gone — <see cref="EngineeringTask.DueDate"/>
    /// is real, durable state, and <see cref="EngineeringTask.IsOverdue"/>
    /// is the domain's own answer rather than this card's guess.
    /// </para>
    /// <para>
    /// An empty list is still the common case and still correct: a project
    /// with nothing overdue has nothing to show here. What changed is that
    /// the emptiness now means "nothing is overdue" rather than "we cannot
    /// tell".
    /// </para>
    /// </remarks>
    public IReadOnlyList<CockpitActionItem> OverdueActions
    {
        get
        {
            var asOf = _now();

            return
            [
                .. LiveTasks
                    .OfType<EngineeringTask>()
                    .Where(t => t.IsOverdue(asOf))
                    .OrderBy(t => t.DueDate)
                    .ThenBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(t => new CockpitActionItem(
                        t.DisplayName,
                        t.AssignedToPrincipalId ?? CockpitActionItem.NobodyAssigned,
                        t.DueDate!.Value,
                        (int)Math.Floor((asOf - t.DueDate!.Value).TotalDays))),
            ];
        }
    }

    /// <summary>
    /// Gets the Overdue Actions card's own lines, ready to render.
    /// </summary>
    /// <remarks>
    /// A formatted projection rather than the records themselves, because
    /// <c>CockpitActionItem</c> is internal to this assembly and every
    /// other Cockpit region already hands the view scalars
    /// (<see cref="HealthScoreDisplay"/> is the same shape). Making the
    /// record public to render four fields would widen this assembly's own
    /// surface for one card.
    /// </remarks>
    public IReadOnlyList<string> OverdueActionLines =>
    [
        .. OverdueActions.Select(a =>
            $"{a.Title} — {a.Owner} · due {a.DueDate:yyyy-MM-dd} ({a.DaysOverdue} day(s) overdue)"),
    ];

    /// <summary>Gets every live (non-deleted) Task/Action (`"Task"`/`"Action"` Kinds) - loaded by <see cref="PrimeAsync"/>.</summary>
    private IReadOnlyList<ITask> LiveTasks => _liveTasks;

    /// <summary>Gets the number of live Tasks/Actions that still need doing.</summary>
    /// <remarks>
    /// Counted from the task family's own <see cref="TaskWorkState"/> where
    /// the object is a real <see cref="EngineeringTask"/>, falling back to
    /// the canonical lifecycle for anything else implementing
    /// <see cref="ITask"/>. Both say the same thing —
    /// <see cref="TaskWorkStates"/> maps Done to
    /// <see cref="LifecycleState.Released"/> — but asking the task family
    /// directly means a reopened task counts as open again, which the
    /// canonical lifecycle alone could never express.
    /// </remarks>
    public int OpenTaskCount =>
        LiveTasks.Count(t => t is EngineeringTask task
            ? TaskWorkStates.IsOpen(task.WorkState)
            : t is IHasLifecycle { Status: not (LifecycleState.Released or LifecycleState.Archived or LifecycleState.Obsolete or LifecycleState.Cancelled) });

    // ------------------------------------------------------------
    // Is the project healthy?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the project's own overall health - a real rollup across
    /// every per-discipline status this Cockpit's own collaborators
    /// compute (Requirements/Calculations/Verification/Documentation/
    /// Manufacturing - <see cref="ReviewStatus"/> is deliberately
    /// excluded, and Mechanical was never included, both unchanged from
    /// the pre-decomposition implementation): <see cref="EngineeringHealthStatus.Blocked"/>
    /// if any included discipline reports it; else
    /// <see cref="EngineeringHealthStatus.Attention"/> if any does; else
    /// <see cref="EngineeringHealthStatus.Unknown"/> if every included
    /// discipline itself reports Unknown; else <see cref="EngineeringHealthStatus.Healthy"/>.
    /// </summary>
    public EngineeringHealthStatus Health
    {
        get
        {
            var statuses = new[] { RequirementsStatus, CalculationStatus, VerificationStatus, DocumentationStatus, ManufacturingStatus };

            if (statuses.Any(s => s == EngineeringHealthStatus.Blocked))
                return EngineeringHealthStatus.Blocked;

            if (statuses.Any(s => s == EngineeringHealthStatus.Attention))
                return EngineeringHealthStatus.Attention;

            return statuses.All(s => s == EngineeringHealthStatus.Unknown)
                ? EngineeringHealthStatus.Unknown
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Engineering Health Score's own display text - a real,
    /// honest fraction: how many of the five disciplines <see cref="Health"/>
    /// rolls up currently report real data at all, and how many of those
    /// are <see cref="EngineeringHealthStatus.Healthy"/>.
    /// </summary>
    public string HealthScoreDisplay
    {
        get
        {
            var statuses = new[] { RequirementsStatus, CalculationStatus, VerificationStatus, DocumentationStatus, ManufacturingStatus };
            var withData = statuses.Count(s => s != EngineeringHealthStatus.Unknown);

            return withData == 0
                ? "— (no Engineering data yet)"
                : $"{statuses.Count(s => s == EngineeringHealthStatus.Healthy)}/{withData} healthy ({withData}/5 disciplines reporting)";
        }
    }

    /// <summary>Gets the Requirements discipline's own dedicated KPI card set — see <see cref="RequirementsCockpitReadModel.KpiCards"/>.</summary>
    public IReadOnlyList<CockpitKpiCard> RequirementsKpiCards => _requirements.KpiCards;

    /// <summary>
    /// Gets the Engineering Health Summary's own per-discipline KPI cards
    /// — a real, cross-discipline aggregation (`ADR-0103`): the live
    /// object count each contributing collaborator already computes, or
    /// a disclosed placeholder if none exist yet. "Review" sums each
    /// discipline's own already-computed in-review count; "Risks" is
    /// <see cref="LiveRisks"/>'s own real count, the identical read
    /// <see cref="RiskSummary"/> already uses.
    /// </summary>
    public IReadOnlyList<CockpitKpiCard> KpiCards
    {
        get
        {
            var totalRequirements = _requirements.Count;
            var totalCalculations = _calculations.Count;
            var totalDocuments = _documents.Count;
            var totalVerificationActivities = _verification.Count;
            var totalInReview = _requirements.InReviewCount + _calculations.InReviewCount + _documents.OutstandingReviews;
            var totalRisks = LiveRisks.Count;

            return
            [
                totalRequirements > 0 ? new("Requirements", $"{totalRequirements} total", IsPlaceholder: false) : new("Requirements", "—", IsPlaceholder: true),
                totalVerificationActivities > 0 ? new("Verification", $"{totalVerificationActivities} total", IsPlaceholder: false) : new("Verification", "—", IsPlaceholder: true),
                totalCalculations > 0 ? new("Calculations", $"{totalCalculations} total", IsPlaceholder: false) : new("Calculations", "—", IsPlaceholder: true),
                totalDocuments > 0 ? new("Documentation", $"{totalDocuments} total", IsPlaceholder: false) : new("Documentation", "—", IsPlaceholder: true),
                totalInReview > 0 ? new("Review", $"{totalInReview} total", IsPlaceholder: false) : new("Review", "—", IsPlaceholder: true),
                totalRisks > 0 ? new("Risks", $"{totalRisks} total", IsPlaceholder: false) : new("Risks", "—", IsPlaceholder: true),
            ];
        }
    }

    /// <summary>Gets the Manufacturing discipline's own dedicated seven-card KPI set — see <see cref="ManufacturingCockpitReadModel.KpiCards"/>.</summary>
    public IReadOnlyList<CockpitKpiCard> ManufacturingKpiCards => _manufacturing.KpiCards;

    /// <summary>Gets the Verification discipline's own dedicated KPI card set — see <see cref="VerificationCockpitReadModel.KpiCards"/>.</summary>
    public IReadOnlyList<CockpitKpiCard> VerificationKpiCards => _verification.KpiCards;

    /// <summary>Gets the Documents discipline's own dedicated KPI card set — see <see cref="DocumentsCockpitReadModel.KpiCards"/>.</summary>
    public IReadOnlyList<CockpitKpiCard> DocumentsKpiCards => _documents.KpiCards;

    /// <summary>Gets the Calculations discipline's own dedicated KPI card set — see <see cref="CalculationsCockpitReadModel.KpiCards"/>.</summary>
    public IReadOnlyList<CockpitKpiCard> CalculationsKpiCards => _calculations.KpiCards;

    /// <summary>
    /// Gets the Risk Summary's own display text - a real read of
    /// <see cref="LiveRisks"/>, bucketed by <see cref="IRisk.Severity"/>.
    /// Honestly "0 open" if none exist.
    /// </summary>
    public string RiskSummary
    {
        get
        {
            var risks = LiveRisks;
            if (risks.Count == 0)
                return "0 open (no live Risk/Hazard recorded yet).";

            var bySeverity = risks
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Severity) ? "Unspecified" : r.Severity!)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Count()} {g.Key}");

            return $"{risks.Count} open — {string.Join(", ", bySeverity)}.";
        }
    }

    /// <summary>
    /// Gets the Digital Thread Summary's own display text - a real,
    /// honest aggregate: the total number of outgoing relationship links
    /// recorded across every live Engineering object platform-wide, a
    /// direct-link count, never a multi-hop traversal. Loaded by
    /// <see cref="PrimeAsync"/>.
    /// </summary>
    public string DigitalThreadSummary => _digitalThreadSummary;

    /// <summary>
    /// Gets the Upcoming Milestones region's own entries - a real read of
    /// <see cref="LiveMilestones"/> whose own <see cref="IMilestone.TargetDate"/>
    /// is not yet past, soonest first. Honestly empty if none are
    /// upcoming.
    /// </summary>
    public IReadOnlyList<string> UpcomingMilestones =>
        LiveMilestones
            .Where(m => m.TargetDate >= DateTimeOffset.UtcNow)
            .OrderBy(m => m.TargetDate)
            .Select(m => $"{((IHasBusinessIdentifier)m).DisplayName} — due {m.TargetDate:yyyy-MM-dd}")
            .ToList();

    // ------------------------------------------------------------
    // What should I do next?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the "Open Actions" region's own entries - each contributing
    /// discipline's own conditional triage entry (`ADR-0103`
    /// cross-collaborator aggregation), in fixed order
    /// (Requirements/Calculations/Documents/Verification/Manufacturing),
    /// plus two fixed, representative placeholder entries.
    /// </summary>
    public IReadOnlyList<CockpitActionItem> OpenActions
    {
        get
        {
            var actions = new List<CockpitActionItem>();

            if (_requirements.GetOpenActionItem() is { } requirementsAction)
                actions.Add(requirementsAction);

            if (_calculations.GetOpenActionItem() is { } calculationsAction)
                actions.Add(calculationsAction);

            if (_documents.GetOpenActionItem() is { } documentsAction)
                actions.Add(documentsAction);

            if (_verification.GetOpenActionItem() is { } verificationAction)
                actions.Add(verificationAction);

            if (_manufacturing.GetOpenActionItem() is { } manufacturingAction)
                actions.Add(manufacturingAction);

            actions.Add(new("Review the Project Explorer's own sample content", "Engineer"));
            actions.Add(new("Await the next real engineering discipline module", "Product Owner"));

            return actions;
        }
    }

    /// <summary>
    /// Gets a short, contextual "what to do next" hint list - computed
    /// from real Workspace state (never fixed placeholder text): whether
    /// there is somewhere to continue, an area to browse, or a command to
    /// run right now.
    /// </summary>
    public IReadOnlyList<string> QuickActions
    {
        get
        {
            var actions = new List<string>();

            if (ContinueWhereILeftOff is not null)
                actions.Add($"Continue: {ContinueWhereILeftOff.Title}");

            if (AreaCount > 0)
                actions.Add("Browse an Area below to explore the Project Explorer.");

            // Whether any command is available *right now* depends on a
            // context this read model deliberately does not hold (`WP-A1`).
            // This hint only claims the Global Commands section exists, so
            // it asks the question it can actually answer.
            if (_commandRegistry.Items.Count > 0)
                actions.Add("Run a Global Command below.");

            return actions;
        }
    }

    /// <summary>
    /// Gets the Recent Activity region's own entries - a real read from
    /// <see cref="NavigationService.RecentItems"/>, most recent first.
    /// </summary>
    public IReadOnlyList<RecentNavigationItem> RecentActivity => _navigationService.RecentItems;

    /// <summary>The most audit rows a single <see cref="RecentlyChanged"/> read shows.</summary>
    public const int RecentlyChangedLimit = 10;

    /// <summary>
    /// Gets the "Recently changed" card's own entries (`WP 18.1B` §4): the
    /// last ten committed changes, newest first, each object's own current
    /// title, Kind, what changed and when — read from the durable audit
    /// trail every mutator already writes (`ADR-0145`), never a
    /// session-only list, so this survives a restart exactly as durably as
    /// the changes themselves did. Honestly empty if no
    /// <see cref="Audit.IAuditQuery"/> was supplied at construction, or if
    /// nothing has changed yet.
    /// </summary>
    /// <remarks>
    /// Each row's own title is a live read of the object as it stands
    /// right now (<see cref="EngineeringDomainContext.Repository"/>), not
    /// the name it had at the moment of that particular change — a later
    /// rename still shows its current name against every one of its own
    /// earlier audit rows, which is what a person expects "Recently
    /// changed" to show them, not a historical snapshot.
    /// </remarks>
    public IReadOnlyList<CockpitRecentChange> RecentlyChanged => _recentlyChanged;

    /// <summary>Loads <see cref="RecentlyChanged"/> — every audit row projected to a <see cref="CockpitRecentChange"/>, newest first, capped at <see cref="RecentlyChangedLimit"/>. Called once per <see cref="PrimeAsync"/> pass.</summary>
    private async Task<IReadOnlyList<CockpitRecentChange>> LoadRecentlyChangedAsync(CancellationToken cancellationToken)
    {
        if (_auditQuery is null)
            return [];

        var records = await _auditQuery.QueryAsync(new AuditQueryCriteria(), cancellationToken).ConfigureAwait(false);

        var changes = new List<CockpitRecentChange>(records.Count);
        foreach (var record in records)
        {
            if (await ToRecentChangeAsync(record, cancellationToken).ConfigureAwait(false) is { } change)
                changes.Add(change);
        }

        return changes
            .OrderByDescending(c => c.When)
            .Take(RecentlyChangedLimit)
            .ToList();
    }

    /// <summary>
    /// Projects one audit row into a <see cref="CockpitRecentChange"/>, or
    /// <see langword="null"/> if it does not name an engineering object
    /// (a row an unrelated service wrote directly through
    /// <see cref="Audit.IAuditRecorder"/>, never through
    /// <c>AuditTransactionWriter</c>) or that object can no longer be
    /// read.
    /// </summary>
    private async Task<CockpitRecentChange?> ToRecentChangeAsync(IAuditRecord record, CancellationToken cancellationToken)
    {
        // Mirrors AuditTransactionWriter's own well-known Detail keys —
        // that class is internal to Tempest.Core and not referenceable
        // here, so the two literal keys it writes are named directly.
        if (!record.Detail.TryGetValue("ObjectId", out var objectIdText) || !Guid.TryParse(objectIdText, out var objectId))
            return null;

        var kind = record.Detail.TryGetValue("Kind", out var k) ? k : "Unknown";

        var found = await _domainContext.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false);
        var title = (found as IHasBusinessIdentifier)?.DisplayName ?? objectId.ToString();

        return new CockpitRecentChange(objectId, title, kind, FriendlyChangeType(record.Action), record.OccurredAt);
    }

    /// <summary>A short, human-readable label for one of <see cref="EngineeringAuditActions"/>'s own action constants.</summary>
    private static string FriendlyChangeType(string action) => action switch
    {
        EngineeringAuditActions.Created => "Created",
        EngineeringAuditActions.Renamed => "Renamed",
        EngineeringAuditActions.Revised => "Revised",
        EngineeringAuditActions.Transitioned => "Status changed",
        EngineeringAuditActions.Linked => "Linked",
        EngineeringAuditActions.Attached or EngineeringAuditActions.ContentAttached => "Attachment added",
        EngineeringAuditActions.Moved => "Moved",
        EngineeringAuditActions.Deleted => "Deleted",
        EngineeringAuditActions.BomLineSet => "BOM updated",
        EngineeringAuditActions.StateChanged => "Updated",
        _ => "Changed",
    };

    /// <summary>
    /// Re-opens or focuses the <paramref name="index"/>-th entry in
    /// <see cref="RecentlyChanged"/> (1-based) - the Cockpit's own
    /// "Recently changed" navigation gesture (`WP 18.1B` §4), a real
    /// dispatch through <see cref="NavigationService.OpenAsync"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public Task<IWorkspaceView> OpenRecentlyChangedAsync(int index, CancellationToken cancellationToken = default)
    {
        var items = RecentlyChanged;

        if (index < 1 || index > items.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Must be between 1 and {items.Count}.");

        var item = items[index - 1];
        return _navigationService.OpenAsync(item.ObjectId, item.Kind, cancellationToken);
    }

    /// <summary>Gets the number of areas currently registered - a real Workspace status indicator.</summary>
    public int AreaCount => _navigationService.Areas.Count;

    /// <summary>Gets the number of documents currently open - a real Workspace status indicator.</summary>
    public int OpenDocumentCount => _navigationService.OpenViews.Count;

    /// <summary>
    /// Gets every currently-available global command - the Cockpit's own
    /// Command Palette integration (`ADR-0070`): a real, live read of
    /// <see cref="ICommandRegistry.Items"/>, filtered by
    /// <see cref="ICommandRegistry.Evaluate"/> against <paramref name="context"/>.
    /// </summary>
    /// <param name="context">
    /// The caller's own context. The Cockpit is a read model and holds no
    /// selection of its own (`WP-A1`): whoever is presenting it owns the
    /// context and supplies it here, which is also what keeps this list
    /// honest — a command is listed only if it could actually run right now.
    /// </param>
    /// <returns>Every command <see cref="ICommandRegistry.Evaluate"/> reports as available.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <b>This used to filter on <see cref="CommandDescriptor.CanExecute"/>
    /// alone</b>, which no production descriptor sets — so it reported all
    /// seventy-four discipline commands as "available" and
    /// <see cref="InvokeCommandAsync"/> then threw
    /// <see cref="CommandException"/> on every one of them, because it
    /// invoked through the Id-only overload that needs a
    /// <see cref="CommandDescriptor.CreateDefault"/> none of them has. The
    /// name and the behaviour now agree.
    /// </remarks>
    public IReadOnlyList<CommandDescriptor> AvailableCommands(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return [.. _commandRegistry.Items.Where(d => _commandRegistry.Evaluate(d.Id, context).IsAvailable)];
    }

    /// <summary>
    /// Invokes the <paramref name="index"/>-th command in
    /// <see cref="AvailableCommands"/> for <paramref name="context"/> (1-based).
    /// </summary>
    /// <param name="index">The 1-based position in <see cref="AvailableCommands"/>.</param>
    /// <param name="context">
    /// The caller's own context — the same one <see cref="AvailableCommands"/>
    /// was listed for, so the command invoked is the command shown.
    /// </param>
    /// <param name="prompt">
    /// Collects any values the command's own binding declares.
    /// <see langword="null"/> — a caller with no input surface — reports that
    /// honestly rather than invoking without asking.
    /// </param>
    /// <param name="cancellationToken">A token observed while invoking.</param>
    /// <returns>Whether the command ran, was declined, or could not be invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public Task<CommandInvocation> InvokeCommandAsync(
        int index,
        CommandContext context,
        CommandParameterPrompt? prompt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var commands = AvailableCommands(context);

        if (index < 1 || index > commands.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Must be between 1 and {commands.Count}.");

        return _commandRegistry.InvokeAsync(commands[index - 1].Id, context, prompt, cancellationToken);
    }

    /// <summary>
    /// Re-opens or focuses <see cref="ContinueWhereILeftOff"/> - the
    /// Cockpit's own "Continue Where I Left Off" navigation gesture, a
    /// real dispatch through <see cref="NavigationService.OpenAsync"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="ContinueWhereILeftOff"/> is <see langword="null"/>.</exception>
    public Task<IWorkspaceView> ContinueAsync(CancellationToken cancellationToken = default)
    {
        var item = ContinueWhereILeftOff
            ?? throw new InvalidOperationException("Nothing to continue - no object has been opened yet this session.");

        return _navigationService.OpenAsync(item.ObjectId, item.Kind, cancellationToken);
    }

    /// <summary>
    /// Re-opens or focuses the <paramref name="index"/>-th entry in
    /// <see cref="RecentActivity"/> (1-based) - the Cockpit's own Recent
    /// Activity navigation gesture, a real dispatch through
    /// <see cref="NavigationService.OpenAsync"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public Task<IWorkspaceView> OpenRecentAsync(int index, CancellationToken cancellationToken = default)
    {
        var items = RecentActivity;

        if (index < 1 || index > items.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Must be between 1 and {items.Count}.");

        var item = items[index - 1];
        return _navigationService.OpenAsync(item.ObjectId, item.Kind, cancellationToken);
    }
}
