using Tempest.Core.Commands;

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
/// Introduces no calculation, requirements, verification, or Digital
/// Thread traversal logic of its own (`WP 8.1C`'s own explicit scope
/// boundary) — every region that would need one of those services today
/// shows fixed, representative placeholder content instead, disclosed
/// either via <see cref="CockpitKpiCard.IsPlaceholder"/>,
/// <see cref="EngineeringHealthStatus.Unknown"/>, or by this class's own
/// XML documentation.
/// </para>
/// <para>
/// <b>Real vs. placeholder, stated once, plainly:</b> <see cref="RecentActivity"/>,
/// <see cref="ContinueWhereILeftOff"/>, <see cref="AreaCount"/>,
/// <see cref="OpenDocumentCount"/>, and <see cref="AvailableCommands"/> are
/// live reads of real Workspace state. Every other member on this class is
/// fixed, representative sample content — no Requirements, Materials,
/// Calculations, Verification, Digital Thread, Project, Risk, Decision, or
/// Milestone service exists anywhere in this platform for a real value to
/// come from yet.
/// </para>
/// </remarks>
internal sealed class EngineeringCockpit
{
    private readonly NavigationService _navigationService;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>Initialises a new instance of the <see cref="EngineeringCockpit"/> class.</summary>
    public EngineeringCockpit(NavigationService navigationService, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _navigationService = navigationService;
        _commandRegistry = commandRegistry;
    }

    // ------------------------------------------------------------
    // Where am I?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the current project's own display name. Fixed, representative
    /// placeholder text: no <c>IProject</c> concept exists anywhere in
    /// this platform yet for a real value to come from.
    /// </summary>
    public string ProjectName => "Sample Engineering Project";

    /// <summary>
    /// Gets the most-recently-opened or jumped-to object — a real read of
    /// <see cref="NavigationService.RecentItems"/>'s own first (most
    /// recent) entry, or <see langword="null"/> if nothing has been opened
    /// yet this session. The Cockpit's own "Continue Where I Left Off."
    /// </summary>
    public RecentNavigationItem? ContinueWhereILeftOff => _navigationService.RecentItems.Count > 0
        ? _navigationService.RecentItems[0]
        : null;

    /// <summary>
    /// Gets the recently-opened projects list. Fixed, representative
    /// placeholder content: no <c>IProject</c> concept, and therefore no
    /// real multi-project history, exists anywhere in this platform yet.
    /// </summary>
    public IReadOnlyList<string> RecentProjects { get; } = ["Sample Engineering Project (this session)"];

    /// <summary>
    /// Gets the favourited projects list — always empty today: favouriting
    /// is not a capability this platform has built anywhere yet, so an
    /// honest empty state is shown rather than fabricated sample favourites.
    /// </summary>
    public IReadOnlyList<string> FavouriteProjects { get; } = [];

    // ------------------------------------------------------------
    // What needs attention?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the "What Needs Attention" region's own entries — fixed,
    /// representative placeholder content (`WP8.0C Engineering Cockpit
    /// Specification.md` §3).
    /// </summary>
    public IReadOnlyList<CockpitAttentionItem> AttentionItems { get; } =
    [
        new("No engineering discipline registered yet", "Requirements, Materials, and Calculations are not yet presented in the Workspace — this is expected, not a defect."),
        new("Sample content only", "The Project Explorer's own tree is fictional sample data (WP 8.1B), not a real engineering project."),
    ];

    /// <summary>
    /// Gets the "Open Decisions" region's own entries — fixed,
    /// representative placeholder content: no decision-tracking service
    /// exists anywhere in this platform yet.
    /// </summary>
    public IReadOnlyList<string> OpenDecisions { get; } =
    [
        "Which Engineering Discipline Module ships first — pending Product Owner decision.",
    ];

    /// <summary>
    /// Gets the "Blocked Items" region's own entries — fixed,
    /// representative placeholder content.
    /// </summary>
    public IReadOnlyList<string> BlockedItems { get; } = [];

    /// <summary>
    /// Gets the "Overdue Actions" region's own entries — fixed,
    /// representative placeholder content, distinct from
    /// <see cref="OpenActions"/> (not yet due).
    /// </summary>
    public IReadOnlyList<CockpitActionItem> OverdueActions { get; } = [];

    // ------------------------------------------------------------
    // Is the project healthy?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the project's own overall health — always
    /// <see cref="EngineeringHealthStatus.Unknown"/> today, honestly: no
    /// Requirements/Verification/Calculation signal is wired to the
    /// Workspace yet for this to be derived from.
    /// </summary>
    public EngineeringHealthStatus Health => EngineeringHealthStatus.Unknown;

    /// <summary>
    /// Gets the Engineering Health Score's own display text — always a
    /// disclosed placeholder today, distinct from <see cref="Health"/>'s
    /// own closed four-value status vocabulary.
    /// </summary>
    public string HealthScoreDisplay => "— (not yet available)";

    /// <summary>Gets the Requirements discipline's own status — always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus RequirementsStatus => EngineeringHealthStatus.Unknown;

    /// <summary>Gets the Verification discipline's own status — always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus VerificationStatus => EngineeringHealthStatus.Unknown;

    /// <summary>Gets the Calculations discipline's own status — always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus CalculationStatus => EngineeringHealthStatus.Unknown;

    /// <summary>Gets the Documentation discipline's own status — always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus DocumentationStatus => EngineeringHealthStatus.Unknown;

    /// <summary>Gets the Review discipline's own status — always <see cref="EngineeringHealthStatus.Unknown"/> today.</summary>
    public EngineeringHealthStatus ReviewStatus => EngineeringHealthStatus.Unknown;

    /// <summary>
    /// Gets the Engineering Health Summary's own KPI cards — always
    /// placeholder today (<see cref="CockpitKpiCard.IsPlaceholder"/>), one
    /// per discipline this platform's own Systems Engineering Foundation
    /// names.
    /// </summary>
    public IReadOnlyList<CockpitKpiCard> KpiCards { get; } =
    [
        new("Requirements", "—", IsPlaceholder: true),
        new("Verification", "—", IsPlaceholder: true),
        new("Calculations", "—", IsPlaceholder: true),
        new("Documentation", "—", IsPlaceholder: true),
        new("Review", "—", IsPlaceholder: true),
        new("Risks", "—", IsPlaceholder: true),
    ];

    /// <summary>
    /// Gets the Risk Summary's own display text — always a disclosed
    /// placeholder today: no Risk service exists anywhere in this
    /// platform yet.
    /// </summary>
    public string RiskSummary => "0 open (placeholder — Risk tracking is not yet wired to the Workspace).";

    /// <summary>
    /// Gets the Digital Thread Summary's own display text — always a
    /// disclosed placeholder today, and always will be a summary count
    /// only, never a live traversal (`WP 8.1C`'s own explicit "no Digital
    /// Thread traversal" scope boundary).
    /// </summary>
    public string DigitalThreadSummary => "0 links tracked (placeholder — no traversal is performed by the Cockpit).";

    /// <summary>
    /// Gets the Upcoming Milestones region's own entries — fixed,
    /// representative placeholder content.
    /// </summary>
    public IReadOnlyList<string> UpcomingMilestones { get; } = [];

    // ------------------------------------------------------------
    // What should I do next?
    // ------------------------------------------------------------

    /// <summary>
    /// Gets the "Open Actions" region's own entries — fixed,
    /// representative placeholder content.
    /// </summary>
    public IReadOnlyList<CockpitActionItem> OpenActions { get; } =
    [
        new("Review the Project Explorer's own sample content", "Engineer"),
        new("Await the first real engineering discipline module", "Product Owner"),
    ];

    /// <summary>
    /// Gets a short, contextual "what to do next" hint list — computed
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
    /// Gets the Recent Activity region's own entries — a real read from
    /// <see cref="NavigationService.RecentItems"/> (`WP 8.1B`), most
    /// recent first.
    /// </summary>
    public IReadOnlyList<RecentNavigationItem> RecentActivity => _navigationService.RecentItems;

    /// <summary>Gets the number of areas currently registered — a real Workspace status indicator.</summary>
    public int AreaCount => _navigationService.Areas.Count;

    /// <summary>Gets the number of documents currently open — a real Workspace status indicator.</summary>
    public int OpenDocumentCount => _navigationService.OpenViews.Count;

    /// <summary>
    /// Gets every currently-available global command — the Cockpit's own
    /// Command Palette integration (`ADR-0070`): a real, live read of
    /// <see cref="ICommandRegistry.Items"/>, filtered by each descriptor's
    /// own <see cref="CommandDescriptor.CanExecute"/>, exactly as
    /// `WP8.0C Interaction Specification.md` §1 specifies ("a command not
    /// yet applicable... appears but is shown disabled" — here narrowed to
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
    /// Re-opens or focuses <see cref="ContinueWhereILeftOff"/> — the
    /// Cockpit's own "Continue Where I Left Off" navigation gesture, a
    /// real dispatch through <see cref="NavigationService.OpenAsync"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="ContinueWhereILeftOff"/> is <see langword="null"/>.</exception>
    public Task<IWorkspaceView> ContinueAsync(CancellationToken cancellationToken = default)
    {
        var item = ContinueWhereILeftOff
            ?? throw new InvalidOperationException("Nothing to continue — no object has been opened yet this session.");

        return _navigationService.OpenAsync(item.ObjectId, item.Kind, cancellationToken);
    }

    /// <summary>
    /// Re-opens or focuses the <paramref name="index"/>-th entry in
    /// <see cref="RecentActivity"/> (1-based) — the Cockpit's own Recent
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
