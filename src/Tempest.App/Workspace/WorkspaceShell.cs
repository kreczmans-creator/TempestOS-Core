using Tempest.Core.Commands;
namespace Tempest.App.Workspace;

/// <summary>
/// The Workspace's own terminal presentation — a hand-rolled console
/// renderer, exactly one of the three options `ADR-0066` itself named,
/// chosen over adding this platform's first-ever GUI/TUI-library
/// dependency for a Work Package whose own scope is shell infrastructure,
/// not a rendering-technology evaluation (`WP8.1A Implementation
/// Report.md`). Built entirely on the rendering-agnostic
/// <see cref="IWorkspaceManager"/>/<see cref="IWorkspace"/> contracts —
/// this class, not those interfaces, is where "terminal-based" actually
/// becomes concrete. Since `v0.10.0` (`ADR-0092`) `Tempest.Desktop` is
/// TempestOS's shipped graphical application; this class is now
/// TempestOS's Internal Engineering Harness (`ADR-0101`) — a fast,
/// scriptable surface for verifying the Runtime Host and Workspace
/// domain layer compose and run correctly, not a shipped product of its
/// own.
/// </summary>
/// <remarks>
/// <para>
/// Extends `WP 5.0D`'s own console interaction model (a two-region
/// Navigation/Content shell) to five regions (Areas, Project Explorer,
/// Documents, Properties, Status Bar).
/// </para>
/// <para>
/// <b>`WP 8.1B` — Navigation &amp; Project Explorer.</b> A bare number
/// still switches areas, unchanged from `WP 8.1A`. Everything the Project
/// Explorer now needs (drill-down, filter, back/forward, context menus) is
/// reached through a small word-command vocabulary (<c>open &lt;N&gt;</c>,
/// <c>up</c>, <c>close &lt;N&gt;</c>, <c>filter [text]</c>, <c>back</c>,
/// <c>forward</c>, <c>menu &lt;N&gt;</c>) — a disclosed, terminal-appropriate
/// realisation of `WP8.0C Interaction Specification.md`'s own richer
/// keyboard-shortcut/mouse-gesture model, not a literal binding of it (the
/// literal bindings were always deferred to a future rendering-technology
/// choice, `WP8.0C UX Specification.md` §5).
/// </para>
/// <para>
/// <b>`WP 8.1C` — Engineering Cockpit.</b> This Shell now starts on, and
/// can return to, a second screen — the Engineering Cockpit (`ADR-0069`)
/// — toggled by <see cref="_onCockpit"/>: a bare number still switches
/// areas from either screen (leaving the Cockpit if on it); <c>cockpit</c>
/// returns to it from an area; <c>run &lt;N&gt;</c> invokes one of the
/// Cockpit's own currently-available global commands
/// (<see cref="EngineeringCockpit.AvailableCommands"/>, the Cockpit's own
/// Command Palette integration, `ADR-0070`).
/// </para>
/// </remarks>
public sealed class WorkspaceShell : IAsyncDisposable
{
    private readonly WorkspaceManager _manager;
    private readonly TextWriter _output;
    private readonly TextReader _input;

    private IWorkspace? _workspace;
    private Workspace? _workspaceConcrete;
    private IReadOnlyList<ProjectExplorerNode> _explorerNodes = [];
    private string? _activeFilter;
    private bool _onCockpit = true;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceShell"/> class.</summary>
    /// <param name="manager">The Workspace manager this Shell starts, renders, and shuts down.</param>
    /// <param name="output">The writer this Shell renders into.</param>
    /// <param name="input">The reader this Shell reads area selections from.</param>
    /// <exception cref="ArgumentNullException">Any parameter is <see langword="null"/>.</exception>
    public WorkspaceShell(WorkspaceManager manager, TextWriter output, TextReader input)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(input);

        _manager = manager;
        _output = output;
        _input = input;
    }

    /// <summary>Runs the Workspace end to end: starts it, renders the initial frame, runs the input loop until exit is requested, then shuts down.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
        await RunInputLoopAsync(cancellationToken).ConfigureAwait(false);
        await StopAsync().ConfigureAwait(false);
    }

    /// <summary>Starts the Workspace (<see cref="IWorkspaceManager.StartAsync"/>) and renders the initial frame.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _output.WriteLine("====================================");
        _output.WriteLine(" TempestOS — Engineering Workspace");
        _output.WriteLine("====================================");

        var workspace = await _manager.StartAsync(cancellationToken).ConfigureAwait(false);
        _workspace = workspace;
        _workspaceConcrete = (Workspace)workspace;

        await RefreshExplorerNodesAsync(cancellationToken).ConfigureAwait(false);

        Render();
    }

    /// <summary>Reads area selections from this Shell's own input reader until an exit is requested (selection <c>0</c>), re-rendering after each processed selection.</summary>
    public async Task RunInputLoopAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await _input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            var shouldContinue = await HandleInputAsync(line, cancellationToken).ConfigureAwait(false);

            if (!shouldContinue)
                break;

            Render();
        }
    }

    /// <summary>
    /// Interprets a single line of input. <c>0</c> requests exit,
    /// regardless of which screen is currently shown. Otherwise, dispatches
    /// to <see cref="HandleCockpitInputAsync"/> or
    /// <see cref="HandleAreaInputAsync"/> depending on <see cref="_onCockpit"/>.
    /// </summary>
    /// <returns><see langword="false"/> if exit was requested (or input ended); otherwise <see langword="true"/>.</returns>
    public async Task<bool> HandleInputAsync(string? input, CancellationToken cancellationToken = default)
    {
        if (input is null || _workspace is null || _workspaceConcrete is null)
            return false;

        var trimmed = input.Trim();

        if (trimmed == "0")
            return false;

        return _onCockpit
            ? await HandleCockpitInputAsync(trimmed, cancellationToken).ConfigureAwait(false)
            : await HandleAreaInputAsync(trimmed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Interprets a single line of input while the Engineering Cockpit is
    /// shown. A bare number within range switches areas, leaving the
    /// Cockpit. <c>run &lt;N&gt;</c> invokes one of the Cockpit's own
    /// currently-available global commands. Anything else is an invalid
    /// selection, reported and otherwise ignored.
    /// </summary>
    private async Task<bool> HandleCockpitInputAsync(string trimmed, CancellationToken cancellationToken)
    {
        var areas = _workspace!.Navigation.Areas;

        if (int.TryParse(trimmed, out var areaSelection) && areaSelection >= 1 && areaSelection <= areas.Count)
        {
            await _workspace.Navigation.SwitchAreaAsync(areas[areaSelection - 1].Id, cancellationToken).ConfigureAwait(false);
            _manager.StatusBar.SetStatus($"Viewing: {areas[areaSelection - 1].Title}");
            _activeFilter = null;
            _onCockpit = false;
            await RefreshExplorerNodesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verb = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var argument = parts.Length > 1 ? parts[1] : null;

        switch (verb)
        {
            case "run" when int.TryParse(argument, out var runIndex):
                await HandleRunCommandAsync(runIndex, cancellationToken).ConfigureAwait(false);
                return true;

            case "continue":
                await HandleContinueAsync(cancellationToken).ConfigureAwait(false);
                return true;

            case "recent" when int.TryParse(argument, out var recentIndex):
                await HandleOpenRecentAsync(recentIndex, cancellationToken).ConfigureAwait(false);
                return true;

            default:
                _output.WriteLine("Invalid selection.");
                return true;
        }
    }

    /// <summary>
    /// Interprets a single line of input while an area is shown. A bare
    /// number within range switches areas (unchanged from `WP 8.1A`).
    /// Otherwise, the first word is matched against a small vocabulary:
    /// <c>open &lt;N&gt;</c>, <c>up</c>, <c>close &lt;N&gt;</c>,
    /// <c>filter [text]</c>, <c>back</c>, <c>forward</c>,
    /// <c>menu &lt;N&gt;</c>, <c>cockpit</c> — anything else is an invalid
    /// selection, reported and otherwise ignored.
    /// </summary>
    private async Task<bool> HandleAreaInputAsync(string trimmed, CancellationToken cancellationToken)
    {
        var areas = _workspace!.Navigation.Areas;

        if (int.TryParse(trimmed, out var areaSelection) && areaSelection >= 1 && areaSelection <= areas.Count)
        {
            await _workspace.Navigation.SwitchAreaAsync(areas[areaSelection - 1].Id, cancellationToken).ConfigureAwait(false);
            _manager.StatusBar.SetStatus($"Viewing: {areas[areaSelection - 1].Title}");
            _activeFilter = null;
            await RefreshExplorerNodesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verb = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var argument = parts.Length > 1 ? parts[1] : null;

        switch (verb)
        {
            case "open" when int.TryParse(argument, out var openIndex):
                await HandleOpenAsync(openIndex, cancellationToken).ConfigureAwait(false);
                return true;

            case "up":
                _explorerNodes = await _workspaceConcrete!.ProjectExplorerConcrete.ExitAsync(cancellationToken).ConfigureAwait(false);
                _activeFilter = null;
                return true;

            case "close" when int.TryParse(argument, out var closeIndex):
                await HandleCloseAsync(closeIndex, cancellationToken).ConfigureAwait(false);
                return true;

            case "filter":
                await HandleFilterAsync(argument, cancellationToken).ConfigureAwait(false);
                return true;

            case "back":
                await HandleGoBackAsync(cancellationToken).ConfigureAwait(false);
                return true;

            case "forward":
                await HandleGoForwardAsync(cancellationToken).ConfigureAwait(false);
                return true;

            case "menu" when int.TryParse(argument, out var menuIndex):
                RenderContextMenu(menuIndex);
                return true;

            case "cockpit":
                _onCockpit = true;
                return true;

            default:
                _output.WriteLine("Invalid selection.");
                return true;
        }
    }

    /// <summary>Shuts the Workspace down (<see cref="IWorkspaceManager.ShutdownAsync"/>).</summary>
    public async Task StopAsync()
    {
        await _manager.ShutdownAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync().ConfigureAwait(false);
    }

    private async Task HandleOpenAsync(int index, CancellationToken cancellationToken)
    {
        if (index < 1 || index > _explorerNodes.Count)
        {
            _output.WriteLine("Invalid selection.");
            return;
        }

        var node = _explorerNodes[index - 1];

        if (node.NodeType != ProjectExplorerNodeType.Object)
        {
            _explorerNodes = await _workspaceConcrete!.ProjectExplorerConcrete.EnterAsync(node, cancellationToken).ConfigureAwait(false);
            _activeFilter = null;
            _manager.StatusBar.SetStatus($"Viewing: {node.Title}");
            return;
        }

        await _workspace!.Selection.SelectAsync(node.Id, node.Kind!, cancellationToken).ConfigureAwait(false);
        var view = await _workspace.Navigation.OpenAsync(node.Id, node.Kind!, cancellationToken).ConfigureAwait(false);
        _manager.StatusBar.SetStatus($"Opened: {view.Title}");
    }

    private async Task HandleCloseAsync(int index, CancellationToken cancellationToken)
    {
        var openViews = _workspace!.OpenViews;

        if (index < 1 || index > openViews.Count)
        {
            _output.WriteLine("Invalid selection.");
            return;
        }

        var view = openViews[index - 1];
        await _workspace.Navigation.CloseAsync(view.Id, cancellationToken).ConfigureAwait(false);
        _manager.StatusBar.SetStatus($"Closed: {view.Title}");
    }

    private async Task HandleRunCommandAsync(int index, CancellationToken cancellationToken)
    {
        var cockpit = _workspaceConcrete!.Cockpit;
        var context = CommandContextForShell();
        var commands = cockpit.AvailableCommands(context);

        if (index < 1 || index > commands.Count)
        {
            _output.WriteLine("Invalid selection.");
            return;
        }

        var descriptor = commands[index - 1];

        // No prompt is supplied: this shell has no value-collection surface,
        // so a command declaring values is reported as needing one rather
        // than invoked without asking. That is an honest outcome — before
        // `WP-A1` this path threw CommandException for all seventy-four
        // production commands, which the caller then had no way to report.
        var invocation = await cockpit
            .InvokeCommandAsync(index, context, prompt: null, cancellationToken)
            .ConfigureAwait(false);

        _manager.StatusBar.SetStatus(invocation.Outcome switch
        {
            CommandOutcome.Executed when invocation.Result!.Succeeded =>
                $"{descriptor.DisplayName}: {invocation.Result.Message ?? "Succeeded."}",
            CommandOutcome.Executed =>
                $"{descriptor.DisplayName} failed: {invocation.Result!.Message}",
            CommandOutcome.Cancelled =>
                _manager.StatusBar.StatusText,
            _ => $"{descriptor.DisplayName}: {invocation.Reason}",
        });
    }

    /// <summary>
    /// The Workspace's own live selection, as the Command Framework sees it
    /// (`WP-A1`).
    /// </summary>
    /// <remarks>
    /// <b>This shell owns the context, not the Cockpit.</b>
    /// <see cref="EngineeringCockpit"/> is a read model and deliberately holds
    /// no <see cref="ISelectionService"/>; whoever presents it is the thing
    /// that knows what the user has selected. Built through the one shared
    /// <see cref="WorkspaceCommandContext"/> adapter the Ribbon and the
    /// Command Palette already use, rather than a second context-building
    /// mechanism.
    /// </remarks>
    private CommandContext CommandContextForShell() =>
        _workspace is null ? CommandContext.Empty : WorkspaceCommandContext.From(_workspace.Selection);

    private async Task HandleContinueAsync(CancellationToken cancellationToken)
    {
        var cockpit = _workspaceConcrete!.Cockpit;

        if (cockpit.ContinueWhereILeftOff is null)
        {
            _output.WriteLine("Nothing to continue yet.");
            return;
        }

        var view = await cockpit.ContinueAsync(cancellationToken).ConfigureAwait(false);
        _manager.StatusBar.SetStatus($"Continued: {view.Title}");
    }

    private async Task HandleOpenRecentAsync(int index, CancellationToken cancellationToken)
    {
        var cockpit = _workspaceConcrete!.Cockpit;

        if (index < 1 || index > cockpit.RecentActivity.Count)
        {
            _output.WriteLine("Invalid selection.");
            return;
        }

        var view = await cockpit.OpenRecentAsync(index, cancellationToken).ConfigureAwait(false);
        _manager.StatusBar.SetStatus($"Opened: {view.Title}");
    }

    private async Task HandleFilterAsync(string? argument, CancellationToken cancellationToken)
    {
        var explorer = _workspaceConcrete!.ProjectExplorerConcrete;

        if (string.IsNullOrWhiteSpace(argument))
        {
            _activeFilter = null;
            await RefreshExplorerNodesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        _activeFilter = argument;
        _explorerNodes = await explorer.FilterAsync(argument, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleGoBackAsync(CancellationToken cancellationToken)
    {
        if (await _workspaceConcrete!.NavigationServiceConcrete.GoBackAsync(cancellationToken).ConfigureAwait(false))
        {
            _activeFilter = null;
            await RefreshExplorerNodesAsync(cancellationToken).ConfigureAwait(false);
            _manager.StatusBar.SetStatus("Navigated back.");
        }
        else
        {
            _output.WriteLine("Nothing to go back to.");
        }
    }

    private async Task HandleGoForwardAsync(CancellationToken cancellationToken)
    {
        if (await _workspaceConcrete!.NavigationServiceConcrete.GoForwardAsync(cancellationToken).ConfigureAwait(false))
        {
            _activeFilter = null;
            await RefreshExplorerNodesAsync(cancellationToken).ConfigureAwait(false);
            _manager.StatusBar.SetStatus("Navigated forward.");
        }
        else
        {
            _output.WriteLine("Nothing to go forward to.");
        }
    }

    private void RenderContextMenu(int index)
    {
        if (index < 1 || index > _explorerNodes.Count)
        {
            _output.WriteLine("Invalid selection.");
            return;
        }

        var node = _explorerNodes[index - 1];
        _output.WriteLine($"Actions for '{node.Title}':");

        if (node.NodeType != ProjectExplorerNodeType.Object)
        {
            _output.WriteLine(node.HasChildren ? "  open <N> - enter" : "  (no actions)");
            return;
        }

        var isOpen = _workspace!.OpenViews.Any(v => v.ObjectId == node.Id && v.ObjectKind == node.Kind);
        _output.WriteLine(isOpen ? "  open <N> - focus (already open)" : "  open <N> - open");

        if (isOpen)
            _output.WriteLine("  close <N> - close (N from the Documents list)");
    }

    private async Task RefreshExplorerNodesAsync(CancellationToken cancellationToken)
    {
        var explorer = _workspaceConcrete!.ProjectExplorerConcrete;
        var path = explorer.CurrentPath;

        _explorerNodes = path.Count == 0
            ? await explorer.GetRootNodesAsync(cancellationToken).ConfigureAwait(false)
            : await explorer.GetChildrenAsync(path[^1].Id, cancellationToken).ConfigureAwait(false);
    }

    private void Render()
    {
        if (_workspace is null || _workspaceConcrete is null)
            return;

        if (_onCockpit)
            RenderCockpit();
        else
            RenderArea();
    }

    private void RenderCockpit()
    {
        var cockpit = _workspaceConcrete!.Cockpit;
        var areas = _workspace!.Navigation.Areas;

        // ---- Where am I? ----
        _output.WriteLine();
        _output.WriteLine("Engineering Cockpit");
        _output.WriteLine("====================");
        _output.WriteLine($"Project: {cockpit.ProjectName}  {FormatStatus(cockpit.Health)}");

        _output.WriteLine();
        _output.WriteLine("Continue Where I Left Off");
        _output.WriteLine("--------------------------");
        _output.WriteLine(cockpit.ContinueWhereILeftOff is { } continueItem
            ? $"continue - {continueItem.Title} ({continueItem.Kind})"
            : "(nothing yet — open something from an Area to continue it next time)");

        _output.WriteLine();
        _output.WriteLine("Recent Projects");
        _output.WriteLine("----------------");
        foreach (var project in cockpit.RecentProjects)
            _output.WriteLine($"- {project}");

        _output.WriteLine();
        _output.WriteLine("Favourite Projects");
        _output.WriteLine("-------------------");
        _output.WriteLine(cockpit.FavouriteProjects.Count == 0
            ? "(none — favouriting is not yet implemented)"
            : string.Join(", ", cockpit.FavouriteProjects));

        // ---- What needs attention? ----
        _output.WriteLine();
        _output.WriteLine("What Needs Attention");
        _output.WriteLine("---------------------");

        if (cockpit.AttentionItems.Count == 0)
        {
            _output.WriteLine("(nothing needs attention)");
        }
        else
        {
            foreach (var item in cockpit.AttentionItems)
                _output.WriteLine($"- {item.Title}: {item.Detail}");
        }

        _output.WriteLine();
        _output.WriteLine("Open Decisions");
        _output.WriteLine("---------------");
        _output.WriteLine(cockpit.OpenDecisions.Count == 0 ? "(none)" : string.Join("; ", cockpit.OpenDecisions));

        _output.WriteLine();
        _output.WriteLine("Blocked Items");
        _output.WriteLine("--------------");
        _output.WriteLine(cockpit.BlockedItems.Count == 0 ? "(none)" : string.Join("; ", cockpit.BlockedItems));

        _output.WriteLine();
        _output.WriteLine("Overdue Actions");
        _output.WriteLine("----------------");
        _output.WriteLine(cockpit.OverdueActions.Count == 0
            ? "(none)"
            : string.Join("; ", cockpit.OverdueActions.Select(a => $"{a.Title} ({a.Owner})")));

        // ---- Is the project healthy? ----
        _output.WriteLine();
        _output.WriteLine("Project Health Dashboard");
        _output.WriteLine("-------------------------");
        _output.WriteLine($"Engineering Health Score: {cockpit.HealthScoreDisplay}");
        _output.WriteLine($"Requirements:  {FormatStatus(cockpit.RequirementsStatus)}");
        _output.WriteLine($"Verification:  {FormatStatus(cockpit.VerificationStatus)}");
        _output.WriteLine($"Calculations:  {FormatStatus(cockpit.CalculationStatus)}");
        _output.WriteLine($"Documentation: {FormatStatus(cockpit.DocumentationStatus)}");
        _output.WriteLine($"Review:        {FormatStatus(cockpit.ReviewStatus)}");

        _output.WriteLine();
        _output.WriteLine("Engineering Health Summary (KPI Cards)");
        _output.WriteLine("----------------------------------------");
        foreach (var kpi in cockpit.KpiCards)
            _output.WriteLine($"{kpi.Label}: {kpi.Value}{(kpi.IsPlaceholder ? " (placeholder)" : string.Empty)}");

        _output.WriteLine();
        _output.WriteLine("Risk Summary");
        _output.WriteLine("-------------");
        _output.WriteLine(cockpit.RiskSummary);

        _output.WriteLine();
        _output.WriteLine("Digital Thread Summary");
        _output.WriteLine("------------------------");
        _output.WriteLine(cockpit.DigitalThreadSummary);

        _output.WriteLine();
        _output.WriteLine("Upcoming Milestones");
        _output.WriteLine("--------------------");
        _output.WriteLine(cockpit.UpcomingMilestones.Count == 0 ? "(none)" : string.Join("; ", cockpit.UpcomingMilestones));

        // ---- What should I do next? ----
        _output.WriteLine();
        _output.WriteLine("Recent Engineering Activity");
        _output.WriteLine("-----------------------------");

        if (cockpit.RecentActivity.Count == 0)
        {
            _output.WriteLine("(none)");
        }
        else
        {
            for (var i = 0; i < cockpit.RecentActivity.Count; i++)
            {
                var item = cockpit.RecentActivity[i];
                _output.WriteLine($"{i + 1} - {item.Title} ({item.Kind})");
            }
        }

        _output.WriteLine();
        _output.WriteLine("Workspace Status");
        _output.WriteLine("-----------------");
        _output.WriteLine($"Areas: {cockpit.AreaCount}");
        _output.WriteLine($"Open documents: {cockpit.OpenDocumentCount}");

        _output.WriteLine();
        _output.WriteLine("Open Actions");
        _output.WriteLine("------------");

        if (cockpit.OpenActions.Count == 0)
        {
            _output.WriteLine("(none)");
        }
        else
        {
            foreach (var action in cockpit.OpenActions)
                _output.WriteLine($"- {action.Title} ({action.Owner})");
        }

        _output.WriteLine();
        _output.WriteLine("Quick Actions");
        _output.WriteLine("--------------");

        if (cockpit.QuickActions.Count == 0)
        {
            _output.WriteLine("(none)");
        }
        else
        {
            foreach (var hint in cockpit.QuickActions)
                _output.WriteLine($"- {hint}");
        }

        _output.WriteLine();
        _output.WriteLine("Navigation Shortcuts (Areas)");
        _output.WriteLine("------------------------------");

        var currentAreaId = _workspaceConcrete.NavigationServiceConcrete.CurrentAreaId;
        for (var i = 0; i < areas.Count; i++)
        {
            var marker = areas[i].Id == currentAreaId ? "*" : " ";
            _output.WriteLine($"{marker}{i + 1} - {areas[i].Title}");
        }

        _output.WriteLine("0 - Exit");

        _output.WriteLine();
        _output.WriteLine("Global Commands (Command Palette)");
        _output.WriteLine("------------------------------------");

        var commands = cockpit.AvailableCommands(CommandContextForShell());
        if (commands.Count == 0)
        {
            _output.WriteLine("(none available)");
        }
        else
        {
            for (var i = 0; i < commands.Count; i++)
                _output.WriteLine($"{i + 1} - {commands[i].DisplayName}");
        }

        _output.WriteLine();
        _output.WriteLine("------------------------------------");
        _output.WriteLine($"Status: {_manager.StatusBar.StatusText}");
        _output.Write("> ");
    }

    private static string FormatStatus(EngineeringHealthStatus status) => status switch
    {
        EngineeringHealthStatus.Blocked => "[BLOCKED]",
        EngineeringHealthStatus.Attention => "[ATTENTION]",
        EngineeringHealthStatus.Healthy => "[HEALTHY]",
        _ => "[UNKNOWN]",
    };

    private void RenderArea()
    {
        var nav = _workspaceConcrete!.NavigationServiceConcrete;
        var explorer = _workspaceConcrete.ProjectExplorerConcrete;
        var areas = _workspace!.Navigation.Areas;

        _output.WriteLine();
        _output.WriteLine("Areas");
        _output.WriteLine("-----");

        for (var i = 0; i < areas.Count; i++)
        {
            var marker = areas[i].Id == nav.CurrentAreaId ? "*" : " ";
            _output.WriteLine($"{marker}{i + 1} - {areas[i].Title}");
        }

        _output.WriteLine("0 - Exit");

        _output.WriteLine();
        _output.WriteLine($"Project Explorer ({(_workspace.ProjectExplorer.IsVisible ? "visible" : "hidden")})");
        _output.WriteLine("----------------");

        if (nav.CurrentAreaId is not null)
        {
            _output.WriteLine($"Path: {BuildBreadcrumb(areas, nav.CurrentAreaId, explorer.CurrentPath)}");

            if (_activeFilter is not null)
                _output.WriteLine($"Filter: \"{_activeFilter}\"");
        }

        if (_explorerNodes.Count == 0)
        {
            _output.WriteLine("(no items — no engineering module registered yet)");
        }
        else
        {
            for (var i = 0; i < _explorerNodes.Count; i++)
            {
                var node = _explorerNodes[i];
                var marker = node.NodeType == ProjectExplorerNodeType.Object && _workspace.Selection.Current?.ObjectId == node.Id
                    ? "*"
                    : " ";
                var suffix = node.HasChildren ? " >" : string.Empty;
                _output.WriteLine($"{marker}{i + 1} - {node.Title}{suffix}");
            }
        }

        _output.WriteLine();
        _output.WriteLine("Recent");
        _output.WriteLine("------");

        if (nav.RecentItems.Count == 0)
        {
            _output.WriteLine("(none)");
        }
        else
        {
            foreach (var item in nav.RecentItems)
                _output.WriteLine($"{item.Title} ({item.Kind})");
        }

        _output.WriteLine();
        _output.WriteLine("Documents");
        _output.WriteLine("---------");

        if (_workspace.OpenViews.Count == 0)
        {
            _output.WriteLine("(no documents open)");
        }
        else
        {
            foreach (var view in _workspace.OpenViews)
                _output.WriteLine($"{(ReferenceEquals(view, _workspace.ActiveView) ? "*" : " ")} {view.Title}");
        }

        _output.WriteLine();
        _output.WriteLine($"Properties ({(_workspace.PropertyInspector.IsVisible ? "visible" : "hidden")})");
        _output.WriteLine("----------");

        if (_workspace.PropertyInspector.CurrentFacets.Count == 0)
        {
            _output.WriteLine("(nothing selected)");
        }
        else
        {
            foreach (var facet in _workspace.PropertyInspector.CurrentFacets)
                _output.WriteLine($"{facet.Name}: {facet.Value}");
        }

        _output.WriteLine();
        _output.WriteLine("------------------------------------");
        _output.WriteLine($"Status: {_manager.StatusBar.StatusText}");
        _output.Write("> ");
    }

    private static string BuildBreadcrumb(IReadOnlyList<Tempest.Core.Navigation.NavigationItem> areas, string currentAreaId, IReadOnlyList<ProjectExplorerNode> path)
    {
        var areaTitle = areas.FirstOrDefault(a => a.Id == currentAreaId)?.Title ?? currentAreaId;
        var segments = new List<string> { areaTitle };
        segments.AddRange(path.Select(n => n.Title));

        return string.Join(" › ", segments);
    }
}
