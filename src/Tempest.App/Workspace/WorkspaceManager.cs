using Tempest.Core.Commands;
using Tempest.Core.Events;
using Tempest.Core.Navigation;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Samples;

namespace Tempest.App.Workspace;

/// <summary>
/// The concrete <see cref="IWorkspaceManager"/> implementation — creates and
/// owns the lifecycle of the one running <see cref="IWorkspace"/>, exactly
/// as <see cref="Tempest.App.Shell.TempestShell"/> creates and owns the
/// lifecycle of its own console presentation, and the Workspace's own
/// registration point for view/explorer-node extensibility (`ADR-0067`).
/// </summary>
/// <remarks>
/// <para>
/// A composition-root component (`ADR-0062`), not a Platform Service —
/// constructed directly by <c>Tempest.App</c>'s own entry point, never
/// resolved through <see cref="ITempestHost.Services"/>.
/// </para>
/// <para>
/// <b>Disclosed implementation-phase finding:</b> `WP8.0B Lifecycle
/// Definitions.md` §1 described <see cref="IWorkspaceManager"/> as
/// "restart-tolerant... `StartAsync` is not a one-shot operation," by loose
/// analogy to <see cref="ITempestHost"/>. The real, shipped
/// <see cref="ITempestHost"/> contract is explicitly single-use ("
/// <see cref="ITempestHost.RunAsync"/> may be called at most once per
/// instance"), confirmed directly against its own XML documentation during
/// this Work Package's own implementation. <see cref="StartAsync"/>
/// therefore throws <see cref="InvalidOperationException"/> if called a
/// second time on the same instance — a genuine, disclosed correction to
/// the contract-stage document, not a defect (`WP8.1A Implementation
/// Report.md`).
/// </para>
/// </remarks>
public sealed class WorkspaceManager : IWorkspaceManager, IAsyncDisposable
{
    private readonly ITempestHost _host;
    private readonly Dictionary<string, IWorkspaceViewFactory> _viewFactories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IProjectExplorerNodeProvider> _explorerProviders = new(StringComparer.Ordinal);
    private readonly WorkspaceContext _context = new();
    private readonly WorkspaceStatusBar _statusBar = new();

    private IEventBus? _eventBus;
    private PropertyInspector? _propertyInspector;
    private Task? _hostRunTask;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceManager"/> class.</summary>
    /// <param name="host">The Runtime Host this Workspace constructs its own composition around. Must be in <see cref="HostState.Created"/> — not yet run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    public WorkspaceManager(ITempestHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;

        // Forces Tempest.Samples to load before Discovery runs — mirrors
        // TempestShell's own identical, documented necessity (WP 5.0D):
        // reading a const member alone does not force the assembly to load,
        // and Discovery's own AppDomain scan would otherwise find zero
        // Tempest.Samples modules.
        _ = typeof(NavigationSampleModule).Assembly;
    }

    /// <inheritdoc />
    public IWorkspace? Current { get; private set; }

    /// <summary>Gets the Status Bar's own current text — internal, since no public contract among the twelve `WP8.0B Workspace Contracts.md` names one; consumed directly by a same-assembly presentation layer such as <see cref="WorkspaceShell"/>.</summary>
    internal WorkspaceStatusBar StatusBar => _statusBar;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">This instance has already been started.</exception>
    public async Task<IWorkspace> StartAsync(CancellationToken cancellationToken = default)
    {
        if (Current is not null)
            throw new InvalidOperationException("This WorkspaceManager has already been started — ITempestHost is single-use; construct a new WorkspaceManager over a new ITempestHost to start again.");

        _hostRunTask = _host.RunAsync(cancellationToken);

        await WaitForServicesAsync(cancellationToken).ConfigureAwait(false);

        var services = _host.Services!;
        var navigationProvider = (INavigationProvider)services.GetService(typeof(INavigationProvider));
        var eventBus = (IEventBus)services.GetService(typeof(IEventBus));
        var settingsProvider = (ISettingsProvider)services.GetService(typeof(ISettingsProvider));
        var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));
        _eventBus = eventBus;

        var navigationService = new NavigationService(navigationProvider, _viewFactories, _context);
        var projectExplorer = new ProjectExplorer(navigationService, _explorerProviders);
        var propertyInspector = new PropertyInspector();
        _propertyInspector = propertyInspector;
        var cockpit = new EngineeringCockpit(navigationService, commandRegistry);

        var defaultPlacements = new List<WorkspacePanelPlacement>
        {
            new(projectExplorer.Id, WorkspaceDockPosition.Left, 30, true),
            new(propertyInspector.Id, WorkspaceDockPosition.Right, 30, true),
        };

        var state = new WorkspaceState(settingsProvider, defaultPlacements);
        await state.LoadAsync(cancellationToken).ConfigureAwait(false);

        var selectionService = new SelectionService(eventBus, _context);

        var workspace = new Workspace(state, navigationService, selectionService, projectExplorer, propertyInspector, cockpit);

        eventBus.Subscribe(propertyInspector);
        eventBus.Subscribe(_statusBar);

        Current = workspace;
        return workspace;
    }

    /// <inheritdoc />
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (Current is not { State: WorkspaceState state } workspace)
            return;

        state.SetOpenViewIds(workspace.OpenViews.Select(v => v.Id).ToList());
        state.SetLastSelection(workspace.Selection.Current);
        await state.SaveAsync(cancellationToken).ConfigureAwait(false);

        if (_eventBus is not null)
        {
            if (_propertyInspector is not null)
                _eventBus.Unsubscribe(_propertyInspector);

            _eventBus.Unsubscribe(_statusBar);
        }

        await _host.StopAsync().ConfigureAwait(false);

        if (_hostRunTask is not null)
            await _hostRunTask.ConfigureAwait(false);

        Current = null;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public void RegisterView(string kind, IWorkspaceViewFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_viewFactories.TryAdd(kind, factory))
            throw new DuplicateWorkspaceRegistrationException(kind);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public void RegisterExplorerArea(string kind, IProjectExplorerNodeProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(provider);

        if (!_explorerProviders.TryAdd(kind, provider))
            throw new DuplicateWorkspaceRegistrationException(kind);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    private async Task WaitForServicesAsync(CancellationToken cancellationToken)
    {
        while (_host.Services is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }
}
