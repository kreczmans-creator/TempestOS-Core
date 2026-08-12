using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace;

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
internal sealed class EngineeringCockpit
{
    private readonly NavigationService _navigationService;
    private readonly ICommandRegistry _commandRegistry;
    private readonly EngineeringDomainContext _domainContext;
    private readonly IRequirementValidationService _requirementValidationService;

    private readonly MechanicalCockpitReadModel _mechanical;
    private readonly RequirementsCockpitReadModel _requirements;
    private readonly CalculationsCockpitReadModel _calculations;
    private readonly DocumentsCockpitReadModel _documents;
    private readonly VerificationCockpitReadModel _verification;
    private readonly ManufacturingCockpitReadModel _manufacturing;

    /// <summary>Initialises a new instance of the <see cref="EngineeringCockpit"/> class.</summary>
    public EngineeringCockpit(
        NavigationService navigationService, ICommandRegistry commandRegistry, EngineeringDomainContext domainContext,
        IRequirementsService requirementsService, IRequirementValidationService requirementValidationService)
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

    /// <summary>Gets every live (non-deleted) Decision - a real read via <see cref="EngineeringDomainContext.Repository"/>. The first Cockpit consumer of the Governance &amp; Risk family (<see cref="IDecision"/>, `WP 8.2C`) - previously compiled but never read by any Workspace surface until `WP 10.1A`. Not one of the six named `ADR-0103` disciplines - remains a direct, cross-cutting read on this composition root.</summary>
    private IReadOnlyList<IDecision> LiveDecisions =>
        _domainContext.Repository.ListByKindAsync("Decision").GetAwaiter().GetResult()
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<IDecision>()
            .ToList();

    /// <summary>Gets every live (non-deleted) Risk-family object (`"Risk"`/`"Hazard"` Kinds - <see cref="IHazard"/> is itself an <see cref="IRisk"/>) - a real read via <see cref="EngineeringDomainContext.Repository"/>.</summary>
    private IReadOnlyList<IRisk> LiveRisks =>
        new[] { "Risk", "Hazard" }
            .SelectMany(kind => _domainContext.Repository.ListByKindAsync(kind).GetAwaiter().GetResult())
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<IRisk>()
            .ToList();

    /// <summary>Gets every live (non-deleted) Milestone - a real read via <see cref="EngineeringDomainContext.Repository"/>.</summary>
    private IReadOnlyList<IMilestone> LiveMilestones =>
        _domainContext.Repository.ListByKindAsync("Milestone").GetAwaiter().GetResult()
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<IMilestone>()
            .ToList();

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
    /// Gets the "Overdue Actions" region's own entries - a disclosed,
    /// honest placeholder, deliberately not upgraded: no due-date field
    /// exists anywhere in this Domain to compute "overdue" from honestly.
    /// </summary>
    public IReadOnlyList<CockpitActionItem> OverdueActions { get; } = [];

    /// <summary>Gets every live (non-deleted) Task/Action (`"Task"`/`"Action"` Kinds) - a real read, the honest substitute named in <see cref="OverdueActions"/>'s own remarks.</summary>
    private IReadOnlyList<ITask> LiveTasks =>
        new[] { "Task", "Action" }
            .SelectMany(kind => _domainContext.Repository.ListByKindAsync(kind).GetAwaiter().GetResult())
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<ITask>()
            .ToList();

    /// <summary>Gets the number of live Tasks/Actions not yet <see cref="LifecycleState.Released"/>, <see cref="LifecycleState.Archived"/>, <see cref="LifecycleState.Obsolete"/>, or <see cref="LifecycleState.Cancelled"/> - real, honest "open" count, distinct from "overdue".</summary>
    public int OpenTaskCount =>
        LiveTasks.Count(t => t is IHasLifecycle { Status: not (LifecycleState.Released or LifecycleState.Archived or LifecycleState.Obsolete or LifecycleState.Cancelled) });

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
    /// direct-link count, never a multi-hop traversal.
    /// </summary>
    public string DigitalThreadSummary
    {
        get
        {
            var liveObjects = _domainContext.Repository.ListAllAsync().GetAwaiter().GetResult()
                .Where(o => o is not IDeletable { IsDeleted: true })
                .ToList();

            if (liveObjects.Count == 0)
                return "0 links tracked (no live Engineering objects exist yet).";

            var totalLinks = liveObjects.Sum(o => _domainContext.RelationshipRepository.GetOutgoingAsync(o.Id).GetAwaiter().GetResult().Count);

            return $"{totalLinks} link(s) tracked across {liveObjects.Count} live object(s).";
        }
    }

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

            if (AvailableCommands.Count > 0)
                actions.Add("Run a Global Command below.");

            return actions;
        }
    }

    /// <summary>
    /// Gets the Recent Activity region's own entries - a real read from
    /// <see cref="NavigationService.RecentItems"/>, most recent first.
    /// </summary>
    public IReadOnlyList<RecentNavigationItem> RecentActivity => _navigationService.RecentItems;

    /// <summary>Gets the number of areas currently registered - a real Workspace status indicator.</summary>
    public int AreaCount => _navigationService.Areas.Count;

    /// <summary>Gets the number of documents currently open - a real Workspace status indicator.</summary>
    public int OpenDocumentCount => _navigationService.OpenViews.Count;

    /// <summary>
    /// Gets every currently-available global command - the Cockpit's own
    /// Command Palette integration (`ADR-0070`): a real, live read of
    /// <see cref="ICommandRegistry.Items"/>, filtered by each descriptor's
    /// own <see cref="CommandDescriptor.CanExecute"/>.
    /// </summary>
    public IReadOnlyList<CommandDescriptor> AvailableCommands =>
        _commandRegistry.Items.Where(d => d.CanExecute is null || d.CanExecute()).ToList();

    /// <summary>
    /// Invokes the <paramref name="index"/>-th command in
    /// <see cref="AvailableCommands"/> (1-based).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public Task<CommandResult> InvokeCommandAsync(int index, CancellationToken cancellationToken = default)
    {
        var commands = AvailableCommands;

        if (index < 1 || index > commands.Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Must be between 1 and {commands.Count}.");

        return _commandRegistry.InvokeAsync(commands[index - 1].Id, cancellationToken);
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
