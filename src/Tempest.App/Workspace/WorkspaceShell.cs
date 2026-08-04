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
/// becomes concrete.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <see cref="Tempest.App.Shell.TempestShell"/>'s own
/// <c>StartAsync</c>/<c>RunInputLoopAsync</c>/<c>HandleInputAsync</c>/
/// <c>StopAsync</c> shape directly — the identical console interaction
/// model, extended from two regions (Navigation, Content) to five
/// (Areas, Project Explorer, Documents, Properties, Status Bar).
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
    /// Interprets a single line of input. <c>0</c> requests exit. A bare
    /// number within range switches areas (unchanged from `WP 8.1A`).
    /// Otherwise, the first word is matched against a small vocabulary:
    /// <c>open &lt;N&gt;</c>, <c>up</c>, <c>close &lt;N&gt;</c>,
    /// <c>filter [text]</c>, <c>back</c>, <c>forward</c>,
    /// <c>menu &lt;N&gt;</c> — anything else is an invalid selection,
    /// reported and otherwise ignored.
    /// </summary>
    /// <returns><see langword="false"/> if exit was requested (or input ended); otherwise <see langword="true"/>.</returns>
    public async Task<bool> HandleInputAsync(string? input, CancellationToken cancellationToken = default)
    {
        if (input is null || _workspace is null || _workspaceConcrete is null)
            return false;

        var trimmed = input.Trim();

        if (trimmed == "0")
            return false;

        var areas = _workspace.Navigation.Areas;

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
                _explorerNodes = await _workspaceConcrete.ProjectExplorerConcrete.ExitAsync(cancellationToken).ConfigureAwait(false);
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

        var nav = _workspaceConcrete.NavigationServiceConcrete;
        var explorer = _workspaceConcrete.ProjectExplorerConcrete;
        var areas = _workspace.Navigation.Areas;

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
