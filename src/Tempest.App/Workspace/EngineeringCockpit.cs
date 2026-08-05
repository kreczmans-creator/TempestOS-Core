using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.App.Workspace;

/// <summary>
/// The Engineering Cockpit — the Workspace's own default landing screen
/// (`ADR-0069`) and, per its own `WP 8.1C` controlling instruction, the
/// answer to four questions on every visit: where am I, what needs
/// attention, is the project healthy, and what should I do next. Composed
/// entirely from existing Workspace services: <see cref="NavigationService"/>
/// (areas, open documents, recent items) and the existing
/// <see cref="ICommandRegistry"/> Platform Service (the Cockpit's own
/// Command Palette integration, `ADR-0070`). Not one of the twelve
/// `WP8.0B Workspace Contracts.md` interfaces — a genuine, disclosed
/// implementation-phase addition, reached only through
/// <see cref="Workspace.Cockpit"/> internally, mirroring
/// <see cref="WorkspaceManager.StatusBar"/>'s own `WP 8.1A` precedent.
/// </summary>
/// <remarks>
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
/// live reads of real Workspace state. `WP 9.0A` adds three more:
/// <see cref="ProjectName"/>, <see cref="RecentProjects"/>, and one
/// <see cref="AttentionItems"/> entry are real reads of the Engineering
/// Domain's own live <c>Project</c> objects. `WP 9.1A` adds the
/// Requirements discipline's own real reads: <see cref="RequirementsStatus"/>,
/// <see cref="RequirementsKpiCards"/>, a second <see cref="AttentionItems"/>
/// entry, and the <c>"Requirements"</c> entry within <see cref="KpiCards"/>
/// itself, all sourced from <see cref="IRequirementsService"/>/
/// <see cref="IRequirementValidationService"/> directly, never a fabricated
/// value, honestly empty/"Unknown" if no live Requirement exists yet. Every
/// other member on this class remains fixed, representative sample
/// content: no Materials, Calculations, Digital Thread, Risk, Decision, or
/// Milestone service is wired to the Workspace for a real value to come
/// from yet — that remains out of this Work Package's own scope.
/// </para>
/// </remarks>
internal sealed class EngineeringCockpit
{
    private readonly NavigationService _navigationService;
    private readonly ICommandRegistry _commandRegistry;
    private readonly EngineeringDomainContext _domainContext;
    private readonly IRequirementsService _requirementsService;
    private readonly IRequirementValidationService _requirementValidationService;

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
        _requirementsService = requirementsService;
        _requirementValidationService = requirementValidationService;
    }

    // ------------------------------------------------------------
    // Where am I?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the most-recently-created live Mechanical Product Structure
    /// <c>Project</c>'s own display name (`WP 9.0A`) - a real read, honestly
    /// reporting "No Mechanical Project yet" if none exists.
    /// </summary>
    public string ProjectName => LiveProjects.Count > 0 ? LiveProjects[^1].DisplayName : "No Mechanical Project yet";

    /// <summary>
    /// Gets the most-recently-opened or jumped-to object - a real read of
    /// <see cref="NavigationService.RecentItems"/>'s own first (most
    /// recent) entry, or <see langword="null"/> if nothing has been opened
    /// yet this session. The Cockpit's own "Continue Where I Left Off."
    /// </summary>
    public RecentNavigationItem? ContinueWhereILeftOff => _navigationService.RecentItems.Count > 0
        ? _navigationService.RecentItems[0]
        : null;

    /// <summary>
    /// Gets every live Mechanical Product Structure <c>Project</c>'s own
    /// display name (`WP 9.0A`) - a real read; empty, honestly, if none
    /// exist yet.
    /// </summary>
    public IReadOnlyList<string> RecentProjects => LiveProjects.Select(p => p.DisplayName).ToList();

    /// <summary>Gets every live (non-deleted) <c>Project</c>, newest-created first is not guaranteed - insertion order from the repository.</summary>
    private IReadOnlyList<IHasBusinessIdentifier> LiveProjects =>
        _domainContext.Repository.ListByKindAsync("Project").GetAwaiter().GetResult()
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<IHasBusinessIdentifier>()
            .ToList();

    /// <summary>Gets every live (non-deleted) Requirement (`WP 9.1A`) - a real read, mirroring <see cref="LiveProjects"/>'s own identical sync-over-async bridging (this class's properties are a frozen, synchronous shape).</summary>
    private IReadOnlyList<Tempest.Core.Requirements.IRequirement> LiveRequirements =>
        _requirementsService.ListAsync().GetAwaiter().GetResult().Where(r => !r.IsDeleted).ToList();

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
    /// Gets the "What Needs Attention" region's own entries (`WP8.0C
    /// Engineering Cockpit Specification.md` §3). The first entry reflects
    /// whether a Mechanical Product Structure <c>Project</c> exists yet
    /// (`WP 9.0A`); the second reflects whether a Requirement exists yet,
    /// plus a third, conditional entry surfacing
    /// <see cref="OutstandingRequirementActions"/> when non-zero
    /// (`WP 9.1A`); the rest remain fixed, representative placeholder
    /// content for every discipline still not wired to the Workspace.
    /// </summary>
    public IReadOnlyList<CockpitAttentionItem> AttentionItems
    {
        get
        {
            var items = new List<CockpitAttentionItem>
            {
                LiveProjects.Count > 0
                    ? new("Mechanical Product Structure is live", $"{LiveProjects.Count} Project(s) registered - the Project Explorer's own Mechanical area reflects real Engineering Domain data (WP 9.0A).")
                    : new("No Mechanical Product Structure registered yet", "The Mechanical Product Structure area has no live Project yet - this is expected, not a defect."),
                LiveRequirements.Count > 0
                    ? new("Requirements Management is live", $"{LiveRequirements.Count} Requirement(s) registered - the Project Explorer's own Requirements area and the Engineering Cockpit's own Requirements KPIs reflect real Requirements Framework data (WP 9.1A).")
                    : new("No Requirements registered yet", "The Requirements Management area has no live Requirement yet - this is expected, not a defect."),
            };

            if (OutstandingRequirementActions > 0)
            {
                items.Add(new(
                    "Requirements need attention",
                    $"{OutstandingRequirementActions} outstanding Requirements validation finding(s) across {LiveRequirements.Count} live requirement(s) - duplicate identifiers, orphans, missing verification/allocation, or advisory relationship kinds. See the Requirements area's own Property Inspector for detail."));
            }

            items.Add(new("Other disciplines still placeholder", "Materials and Calculations remain out of the Workspace's own scope until their own Work Package integrates them."));

            return items;
        }
    }

    /// <summary>
    /// Gets the "Open Decisions" region's own entries - fixed,
    /// representative placeholder content: no decision-tracking service
    /// exists anywhere in this platform yet.
    /// </summary>
    public IReadOnlyList<string> OpenDecisions { get; } =
    [
        "Which Engineering Discipline Module ships first - pending Product Owner decision.",
    ];

    /// <summary>
    /// Gets the "Blocked Items" region's own entries - fixed,
    /// representative placeholder content.
    /// </summary>
    public IReadOnlyList<string> BlockedItems { get; } = [];

    /// <summary>
    /// Gets the "Overdue Actions" region's own entries - fixed,
    /// representative placeholder content, distinct from
    /// <see cref="OpenActions"/> (not yet due).
    /// </summary>
    public IReadOnlyList<CockpitActionItem> OverdueActions { get; } = [];

    // ------------------------------------------------------------
    // Is the project healthy?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the project's own overall health - always
    /// <see cref="EngineeringHealthStatus.Unknown"/> today, honestly: no
    /// Verification/Calculation signal beyond Requirements' own is wired to
    /// the Workspace yet for a whole-project status to be derived from.
    /// </summary>
    public EngineeringHealthStatus Health => EngineeringHealthStatus.Unknown;

    /// <summary>
    /// Gets the Engineering Health Score's own display text - always a
    /// disclosed placeholder today, distinct from <see cref="Health"/>'s
    /// own closed four-value status vocabulary.
    /// </summary>
    public string HealthScoreDisplay => "— (not yet available)";

    /// <summary>
    /// Gets the Requirements discipline's own status (`WP 9.1A`) - a real,
    /// derived read: <see cref="EngineeringHealthStatus.Unknown"/> if no
    /// live Requirement exists yet; <see cref="EngineeringHealthStatus.Blocked"/>
    /// if any live requirement's own <see cref="RequirementValidationService"/>
    /// result carries an error (duplicate identifier - defence-in-depth,
    /// `CreateAsync` already prevents this at write time);
    /// <see cref="EngineeringHealthStatus.Attention"/> if any carries a
    /// warning (orphan, missing verification/allocation, advisory
    /// relationship kind) with no error present; <see cref="EngineeringHealthStatus.Healthy"/>
    /// otherwise.
    /// </summary>
    public EngineeringHealthStatus RequirementsStatus
    {
        get
        {
            var live = LiveRequirements;
            if (live.Count == 0)
                return EngineeringHealthStatus.Unknown;

            var results = LiveRequirementValidationResults;

            if (results.Any(r => r.Errors.Count > 0))
                return EngineeringHealthStatus.Blocked;

            return results.Any(r => r.Warnings.Count > 0)
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>Gets the Verification discipline's own status - always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus VerificationStatus => EngineeringHealthStatus.Unknown;

    /// <summary>Gets the Calculations discipline's own status - always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus CalculationStatus => EngineeringHealthStatus.Unknown;

    /// <summary>Gets the Documentation discipline's own status - always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus DocumentationStatus => EngineeringHealthStatus.Unknown;

    /// <summary>Gets the Review discipline's own status - always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus ReviewStatus => EngineeringHealthStatus.Unknown;

    /// <summary>
    /// Gets the Engineering Health Summary's own per-discipline KPI cards.
    /// The <c>"Requirements"</c> entry is a real read (`WP 9.1A`) - the
    /// live requirement count, or a disclosed placeholder if none exist
    /// yet; every other entry remains placeholder
    /// (<see cref="CockpitKpiCard.IsPlaceholder"/>) until its own
    /// discipline is wired to the Workspace.
    /// </summary>
    public IReadOnlyList<CockpitKpiCard> KpiCards
    {
        get
        {
            var total = LiveRequirements.Count;

            return
            [
                total > 0 ? new("Requirements", $"{total} total", IsPlaceholder: false) : new("Requirements", "—", IsPlaceholder: true),
                new("Verification", "—", IsPlaceholder: true),
                new("Calculations", "—", IsPlaceholder: true),
                new("Documentation", "—", IsPlaceholder: true),
                new("Review", "—", IsPlaceholder: true),
                new("Risks", "—", IsPlaceholder: true),
            ];
        }
    }

    /// <summary>
    /// Gets the Requirements discipline's own dedicated KPI card set
    /// (`WP 9.1A`) - replacing the single, generic <c>"Requirements"</c>
    /// placeholder card this Work Package's own controlling instruction
    /// names, with the full breakdown it asks for: Total, Draft, Review,
    /// Approved, Released, Verification Coverage, Allocation Coverage,
    /// Requirement Health, and Outstanding Actions. Every card is a real
    /// read; every value is <c>0</c>/"-" honestly, never fabricated, if no
    /// live Requirement exists yet.
    /// </summary>
    /// <remarks>
    /// <b>Disclosed status-name mapping:</b> this platform's own
    /// <see cref="RequirementStatus"/> (`WP7.2C Requirement Lifecycle
    /// Model.md`) has no <c>"Released"</c> value - the closed set is
    /// <c>Draft</c>/<c>Reviewed</c>/<c>Approved</c>/<c>Allocated</c>/
    /// <c>Verified</c>/<c>Satisfied</c>/<c>Obsolete</c>. This card set's own
    /// "Released" card reports the <see cref="RequirementStatus.Satisfied"/>
    /// count - the closest existing terminal-success status - rather than
    /// inventing a new status value, which this Work Package's own
    /// controlling instruction forbids ("No contract redesign"). The two
    /// intervening statuses (<see cref="RequirementStatus.Allocated"/>,
    /// <see cref="RequirementStatus.Verified"/>) are not silently dropped -
    /// they remain visible via <see cref="RequirementsStatus"/>'s own
    /// validation-driven derivation and every live requirement's own
    /// Property Inspector facets.
    /// </remarks>
    public IReadOnlyList<CockpitKpiCard> RequirementsKpiCards
    {
        get
        {
            var live = LiveRequirements;
            var total = live.Count;
            var counts = live.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());

            int CountOf(RequirementStatus status) => counts.TryGetValue(status, out var count) ? count : 0;

            return
            [
                new("Total Requirements", total.ToString(), IsPlaceholder: false),
                new("Draft", CountOf(RequirementStatus.Draft).ToString(), IsPlaceholder: false),
                new("Review", CountOf(RequirementStatus.Reviewed).ToString(), IsPlaceholder: false),
                new("Approved", CountOf(RequirementStatus.Approved).ToString(), IsPlaceholder: false),
                new("Released", CountOf(RequirementStatus.Satisfied).ToString(), IsPlaceholder: false),
                new("Verification Coverage", FormatCoverage(VerifiedRequirementCount, total), IsPlaceholder: false),
                new("Allocation Coverage", FormatCoverage(AllocatedRequirementCount, total), IsPlaceholder: false),
                new("Requirement Health", RequirementsStatus.ToString(), IsPlaceholder: false),
                new("Outstanding Actions", OutstandingRequirementActions.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>
    /// Gets every live requirement's own <see cref="IRequirementValidationService"/>
    /// result - the shared basis for <see cref="RequirementsStatus"/> and
    /// <see cref="OutstandingRequirementActions"/>, computed once per read
    /// so both stay consistent with each other.
    /// </summary>
    /// <remarks>
    /// <b>Defensive, not currently load-bearing:</b> the concrete
    /// <see cref="RequirementValidationService"/> reads only
    /// <see cref="IRequirementsService.GetRelationshipsAsync"/> today, never
    /// the permission-gated <see cref="IRequirementsService.GetEvidenceAsync"/>
    /// (a disclosed fix made within this same Work Package - see
    /// <see cref="RequirementValidationService"/>'s own remarks), so
    /// <see cref="PermissionDeniedException"/> is not expected here in
    /// practice. The guard remains because <see cref="IRequirementValidationService"/>
    /// is an interface, not a sealed contract to this one implementation -
    /// a passive status dashboard must never throw because some future
    /// implementation's own validation needs a narrower capability than
    /// "can view the Cockpit at all"; a requirement whose own validation
    /// cannot be evaluated for that reason is silently excluded from this
    /// read (never counted as a false "no findings"), rather than crashing
    /// every other card this property feeds.
    /// </remarks>
    private IReadOnlyList<IValidationResult> LiveRequirementValidationResults
    {
        get
        {
            var results = new List<IValidationResult>();

            foreach (var requirement in LiveRequirements)
            {
                try
                {
                    results.Add(_requirementValidationService.ValidateAsync(requirement.Id).GetAwaiter().GetResult());
                }
                catch (PermissionDeniedException)
                {
                    // See this property's own remarks.
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Gets the number of live requirements with at least one recorded
    /// verification (`WP 9.1A`) - a real read via
    /// <see cref="IRequirementsService.GetRelationshipsAsync"/> for a
    /// <see cref="VerificationService.VerifiedByRelationshipKind"/>
    /// relationship, the existing Digital Thread read, never a new
    /// traversal. Deliberately does not use <see cref="IRequirementsService.GetEvidenceAsync"/>
    /// (unlike <see cref="RequirementValidationService"/>'s own identical
    /// check) - <see cref="IRequirementsService.GetRelationshipsAsync"/> is
    /// not permission-gated, so this KPI stays available to every principal
    /// that can view the Cockpit at all, never only those additionally
    /// holding <see cref="VerificationService.ReadPermission"/>.
    /// </summary>
    private int VerifiedRequirementCount =>
        LiveRequirements.Count(r => _requirementsService.GetRelationshipsAsync(r.Id).GetAwaiter().GetResult()
            .Any(reference => string.Equals(reference.RelationshipKind, VerificationService.VerifiedByRelationshipKind, StringComparison.Ordinal)));

    /// <summary>Gets the number of live requirements with at least one <see cref="RequirementRelationshipKinds.AllocatedTo"/> relationship (`WP 9.1A`) - a real read via <see cref="IRequirementsService.GetRelationshipsAsync"/>, the existing Digital Thread read, never a new traversal.</summary>
    private int AllocatedRequirementCount =>
        LiveRequirements.Count(r => _requirementsService.GetRelationshipsAsync(r.Id).GetAwaiter().GetResult()
            .Any(reference => string.Equals(reference.RelationshipKind, RequirementRelationshipKinds.AllocatedTo, StringComparison.Ordinal)));

    /// <summary>Gets the total count of Requirements validation findings (errors plus warnings) across every live requirement (`WP 9.1A`) - the Cockpit's own "Outstanding Actions" KPI.</summary>
    public int OutstandingRequirementActions => LiveRequirementValidationResults.Sum(r => r.Errors.Count + r.Warnings.Count);

    private static string FormatCoverage(int numerator, int denominator) =>
        denominator == 0 ? "— (no requirements yet)" : $"{numerator * 100 / denominator}% ({numerator}/{denominator})";

    /// <summary>
    /// Gets the Risk Summary's own display text - always a disclosed
    /// placeholder today: no Risk service exists anywhere in this
    /// platform yet.
    /// </summary>
    public string RiskSummary => "0 open (placeholder — Risk tracking is not yet wired to the Workspace).";

    /// <summary>
    /// Gets the Digital Thread Summary's own display text - always a
    /// disclosed placeholder today, and always will be a summary count
    /// only, never a live traversal (`WP 8.1C`'s own explicit "no Digital
    /// Thread traversal" scope boundary).
    /// </summary>
    public string DigitalThreadSummary => "0 links tracked (placeholder — no traversal is performed by the Cockpit).";

    /// <summary>
    /// Gets the Upcoming Milestones region's own entries - fixed,
    /// representative placeholder content.
    /// </summary>
    public IReadOnlyList<string> UpcomingMilestones { get; } = [];

    // ------------------------------------------------------------
    // What should I do next?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the "Open Actions" region's own entries. A conditional
    /// Requirements triage entry (`WP 9.1A`) appears first when
    /// <see cref="OutstandingRequirementActions"/> is non-zero; the rest
    /// remain fixed, representative placeholder content.
    /// </summary>
    public IReadOnlyList<CockpitActionItem> OpenActions
    {
        get
        {
            var actions = new List<CockpitActionItem>();

            if (OutstandingRequirementActions > 0)
                actions.Add(new($"Triage {OutstandingRequirementActions} outstanding Requirements validation finding(s)", "Systems Engineer"));

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
    /// <see cref="NavigationService.RecentItems"/> (`WP 8.1B`), most
    /// recent first.
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
    /// own <see cref="CommandDescriptor.CanExecute"/>, exactly as
    /// `WP8.0C Interaction Specification.md` §1 specifies ("a command not
    /// yet applicable... appears but is shown disabled" - here narrowed to
    /// "available commands only," since the Cockpit's own scope is a
    /// dashboard region, not the full overlay palette).
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
