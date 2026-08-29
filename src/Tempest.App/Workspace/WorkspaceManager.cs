using Tempest.Core.Commands;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Navigation;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;

namespace Tempest.App.Workspace;

/// <summary>
/// The concrete <see cref="IWorkspaceManager"/> implementation — creates and
/// owns the lifecycle of the one running <see cref="IWorkspace"/>, and the
/// Workspace's own registration point for view/explorer-node extensibility
/// (`ADR-0067`).
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
    private readonly Dictionary<string, IPropertyFacetProvider> _facetProviders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<Guid, string, string, IWorkspaceCommand>> _renameFactories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<Guid, string, IWorkspaceCommand>> _deleteFactories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<Guid, string, string, IWorkspaceCommand>> _reviseFactories = new(StringComparer.Ordinal);
    private readonly WorkspaceContext _context = new();
    private readonly WorkspaceStatusBar _statusBar = new();

    private IEventBus? _eventBus;
    private PropertyInspector? _propertyInspector;
    private Task? _hostRunTask;
    private CommandHandlerTable? _commandHandlerTable;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceManager"/> class.</summary>
    /// <param name="host">The Runtime Host this Workspace constructs its own composition around. Must be in <see cref="HostState.Created"/> — not yet run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    public WorkspaceManager(ITempestHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;

        // `TD-75` phase 1: the forced load of Tempest.Samples that used to
        // stand here is gone. It existed because the six discipline
        // explorer modules lived in that assembly, so Discovery's own scan
        // had to be made to see it before the product's own navigation
        // would appear. Those modules are now declared by the disciplines
        // that own them, in this assembly, which Discovery already scans —
        // so the product no longer reaches into the sample harness to find
        // its own navigation.
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
        _commandHandlerTable = (CommandHandlerTable)services.GetService(typeof(CommandHandlerTable));
        var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var requirementsService = (IRequirementsService)services.GetService(typeof(IRequirementsService));
        var requirementValidationService = (IRequirementValidationService)services.GetService(typeof(IRequirementValidationService));
        _eventBus = eventBus;

        var navigationService = new NavigationService(navigationProvider, _viewFactories, _context);
        var projectExplorer = new ProjectExplorer(navigationService, _explorerProviders);
        var propertyInspector = new PropertyInspector(_facetProviders);
        _propertyInspector = propertyInspector;
        var cockpit = new EngineeringCockpit(navigationService, commandRegistry, domainContext, requirementsService, requirementValidationService);

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

    /// <summary>
    /// Registers <paramref name="provider"/> as the real facet source for
    /// every selected object of Kind <paramref name="kind"/> — the Property
    /// Inspector's own Kind-keyed extension point (`ADR-0067`, `WP 9.0A`),
    /// alongside <see cref="RegisterView"/>/<see cref="RegisterExplorerArea"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A facet provider is already registered for <paramref name="kind"/>.</exception>
    public void RegisterFacetProvider(string kind, IPropertyFacetProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(provider);

        if (!_facetProviders.TryAdd(kind, provider))
            throw new DuplicateWorkspaceRegistrationException(kind);
    }

    /// <inheritdoc />
    public void RegisterRenameFactory(string kind, Func<Guid, string, string, IWorkspaceCommand> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_renameFactories.TryAdd(kind, factory))
            throw new DuplicateWorkspaceRegistrationException(kind);
    }

    /// <inheritdoc />
    public void RegisterDeleteFactory(string kind, Func<Guid, string, IWorkspaceCommand> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_deleteFactories.TryAdd(kind, factory))
            throw new DuplicateWorkspaceRegistrationException(kind);
    }

    /// <inheritdoc />
    public void RegisterReviseFactory(string kind, Func<Guid, string, string, IWorkspaceCommand> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_reviseFactories.TryAdd(kind, factory))
            throw new DuplicateWorkspaceRegistrationException(kind);
    }

    /// <inheritdoc />
    public bool CanRename(string kind) => kind is not null && _renameFactories.ContainsKey(kind);

    /// <inheritdoc />
    public bool CanDelete(string kind) => kind is not null && _deleteFactories.ContainsKey(kind);

    /// <inheritdoc />
    public bool CanRevise(string kind) => kind is not null && _reviseFactories.ContainsKey(kind);

    /// <inheritdoc />
    public Task<CommandResult> RenameObjectAsync(Guid id, string kind, string newDisplayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(newDisplayName);

        if (!_renameFactories.TryGetValue(kind, out var factory))
            return Task.FromResult(CommandResult.Failure($"No rename capability is registered for Kind '{kind}'."));

        return DispatchObjectCommandAsync(factory(id, kind, newDisplayName), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CommandResult> DeleteObjectAsync(Guid id, string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        if (!_deleteFactories.TryGetValue(kind, out var factory))
            return CommandResult.Failure($"No delete capability is registered for Kind '{kind}'.");

        var result = await DispatchObjectCommandAsync(factory(id, kind), cancellationToken).ConfigureAwait(false);

        // A deleted object must not stay selected (`TD-58` stale-UI
        // closure): every deleting surface (Ribbon, Project Explorer
        // context menu, Delete key) converges here, so this one clear
        // keeps Delete/Rename enablement, the Property Inspector, and
        // any repeat-delete dispatch from acting on a dead Id.
        if (result.Succeeded && Current?.Selection.Current is { } selection && selection.ObjectId == id)
            await Current.Selection.ClearAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public Task<CommandResult> ReviseObjectAsync(Guid id, string kind, string newContent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(newContent);

        if (!_reviseFactories.TryGetValue(kind, out var factory))
            return Task.FromResult(CommandResult.Failure($"No revise capability is registered for Kind '{kind}'."));

        return DispatchObjectCommandAsync(factory(id, kind, newContent), cancellationToken);
    }

    /// <summary>
    /// Dispatches <paramref name="command"/> to its own real, already
    /// -registered handler, looked up by its own runtime concrete type —
    /// <see cref="CommandHandlerTable.DispatchAsync(ICommand, CancellationToken)"/>
    /// is the identical, unmodified primitive
    /// <see cref="ICommandRegistry.InvokeAsync"/> already uses internally
    /// for this exact reason (a caller with only a runtime-typed
    /// <see cref="ICommand"/>, not a compile-time <c>TCommand</c>) — no new
    /// dispatch mechanism, no reflection, introduced here.
    /// </summary>
    private Task<CommandResult> DispatchObjectCommandAsync(IWorkspaceCommand command, CancellationToken cancellationToken)
    {
        if (_commandHandlerTable is null)
            return Task.FromResult(CommandResult.Failure("The Workspace has not finished starting yet."));

        return _commandHandlerTable.DispatchAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Waits until <see cref="ITempestHost.Services"/> is resolvable
    /// <b>and</b> the Runtime Host has reached <see cref="HostState.Running"/>
    /// — i.e. every module's own <c>InitialiseAsync</c>/<c>StartAsync</c>
    /// has completed, not merely that the Dependency Injection container
    /// exists.
    /// </summary>
    /// <remarks>
    /// <b>`TD-26` fixed at its own source (`WP 10.1B`):</b> <c>TempestHost.cs</c>
    /// sets <see cref="ITempestHost.Services"/> during "Platform Services
    /// Registered" (Phase 6), several phases <em>before</em> Module
    /// Initialisation/Start runs — a caller that returned as soon as
    /// <see cref="ITempestHost.Services"/> became non-null, as this method
    /// did from `WP 9.0A` through `WP 10.1A`, could read a Workspace state
    /// (a module-registered <c>NavigationItem</c>, a sample module's own
    /// seeded Engineering Domain object) before it existed. `WP 10.0B`/
    /// `WP 10.1A` each mitigated this one layer up, in <c>Tempest.Desktop</c>
    /// alone, polling the same authoritative signal used here —
    /// <see cref="IDiagnosticsProvider.HostState"/> reaching
    /// <see cref="HostState.Running"/> — without ever fixing
    /// <see cref="WorkspaceManager"/> itself. This method now applies that
    /// identical, authoritative wait at its true source, so every
    /// <see cref="IWorkspaceManager"/> consumer — console, desktop, or any
    /// future presentation layer — receives the same guarantee without its
    /// own workaround. <c>Tempest.Desktop</c>'s own now-redundant
    /// <c>WorkspaceHost.WaitForHostRunningAsync</c> poll was removed in the
    /// same Work Package.
    /// </remarks>
    private async Task WaitForServicesAsync(CancellationToken cancellationToken)
    {
        while (_host.Services is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfHostRunFaulted();
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }

        var diagnosticsProvider = (IDiagnosticsProvider)_host.Services.GetService(typeof(IDiagnosticsProvider));

        while (diagnosticsProvider.HostState != HostState.Running)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfHostRunFaulted();
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Observes <see cref="_hostRunTask"/> without blocking: if the Runtime
    /// Host's own <see cref="ITempestHost.RunAsync"/> has already completed
    /// — necessarily a fault, since it cannot return successfully before
    /// <see cref="HostState.Running"/> is reached, the only condition
    /// <see cref="WaitForServicesAsync"/> is waiting for — this rethrows
    /// that fault immediately, rather than spinning forever waiting for a
    /// <see cref="HostState.Running"/> transition that will now never come.
    /// </summary>
    private void ThrowIfHostRunFaulted()
    {
        if (_hostRunTask is { IsCompleted: true } task)
            task.GetAwaiter().GetResult();
    }
}
