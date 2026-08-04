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
/// Mirrors <see cref="Tempest.App.Shell.TempestShell"/>'s own
/// <c>StartAsync</c>/<c>RunInputLoopAsync</c>/<c>HandleInputAsync</c>/
/// <c>StopAsync</c> shape directly — the identical console interaction
/// model, extended from two regions (Navigation, Content) to five
/// (Areas, Project Explorer, Documents, Properties, Status Bar).
/// </remarks>
public sealed class WorkspaceShell : IAsyncDisposable
{
    private readonly WorkspaceManager _manager;
    private readonly TextWriter _output;
    private readonly TextReader _input;

    private IWorkspace? _workspace;

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

        _workspace = await _manager.StartAsync(cancellationToken).ConfigureAwait(false);

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
    /// Interprets a single line of input against the Areas region's current
    /// numbering: <c>0</c> requests exit; a number within range switches to
    /// the corresponding area (<see cref="INavigationService.SwitchAreaAsync"/>);
    /// anything else is an invalid selection, reported and otherwise ignored.
    /// </summary>
    /// <returns><see langword="false"/> if exit was requested (or input ended); otherwise <see langword="true"/>.</returns>
    public async Task<bool> HandleInputAsync(string? input, CancellationToken cancellationToken = default)
    {
        if (input is null || _workspace is null)
            return false;

        var trimmed = input.Trim();

        if (trimmed == "0")
            return false;

        var areas = _workspace.Navigation.Areas;

        if (int.TryParse(trimmed, out var selection) && selection >= 1 && selection <= areas.Count)
        {
            await _workspace.Navigation.SwitchAreaAsync(areas[selection - 1].Id, cancellationToken).ConfigureAwait(false);
            _manager.StatusBar.SetStatus($"Viewing: {areas[selection - 1].Title}");
        }
        else
        {
            _output.WriteLine("Invalid selection.");
        }

        return true;
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

    private void Render()
    {
        if (_workspace is null)
            return;

        _output.WriteLine();
        _output.WriteLine("Areas");
        _output.WriteLine("-----");

        var areas = _workspace.Navigation.Areas;
        for (var i = 0; i < areas.Count; i++)
            _output.WriteLine($"{i + 1} - {areas[i].Title}");

        _output.WriteLine("0 - Exit");

        _output.WriteLine();
        _output.WriteLine($"Project Explorer ({(_workspace.ProjectExplorer.IsVisible ? "visible" : "hidden")})");
        _output.WriteLine("----------------");
        _output.WriteLine("(no items — no engineering module registered yet)");

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
}
