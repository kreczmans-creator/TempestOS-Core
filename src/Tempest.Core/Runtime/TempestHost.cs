using Tempest.Core.Audit;
using Tempest.Core.BackgroundServices;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Diagnostics;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Persistence;
using Tempest.Core.Plugins;
using Tempest.Core.Settings;
using Tempest.Core.Versioning;

namespace Tempest.Core.Runtime;

/// <summary>
/// The concrete <see cref="ITempestHost"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Constructed only by <see cref="TempestHostBuilder"/> — the constructor is
/// <see langword="internal"/>, so no other component can construct the
/// runtime. Configuration, Logging, Discovery, Registration, and Dependency
/// Injection are all constructed directly by this class, in that order,
/// during <see cref="RunAsync"/> — see <c>Runtime Host Architecture.md</c>'s
/// "Relationship to Existing Services" section. Discovery, Registration, and
/// Lifecycle are held as private fields and never registered into the
/// dependency injection container (ADR-0017): a module has no path back into
/// the machinery orchestrating it.
/// </para>
/// <para>
/// <b>Startup cancellation and shutdown requests</b> (ADR-0014) are observed
/// through a single linked token for implementation simplicity — ADR-0014
/// explicitly permits satisfying both signals through one underlying trigger
/// without merging the concepts (see its Positive consequences) — but remain
/// two distinct triggers: the caller's own <see cref="CancellationToken"/>
/// passed to <see cref="RunAsync"/>, and an internal signal raised by
/// <see cref="StopAsync"/>. Both are handled identically once observed during
/// <see cref="HostState.Starting"/> (ADR-0018): control passes to the same
/// controlled-shutdown procedure used by a graceful, post-<see cref="HostState.Running"/>
/// stop. <see cref="RunAsync"/> only rethrows <see cref="OperationCanceledException"/>
/// when the caller's own token was the trigger — a shutdown requested via
/// <see cref="StopAsync"/> is a deliberate, successful stop, and
/// <see cref="RunAsync"/> completes normally for it, matching the established
/// .NET generic-host convention for exactly this scenario.
/// </para>
/// <para>
/// <b>Disposal is always an explicit, separate call</b> (ADR-0019):
/// <see cref="RunAsync"/> never disposes the host automatically, whether it
/// ends at <see cref="HostState.Stopped"/> or <see cref="HostState.Faulted"/>.
/// <see cref="DisposeAsync"/> is idempotent — safe to call more than once,
/// including once the host is already <see cref="HostState.Disposed"/> —
/// matching the standard <see cref="IAsyncDisposable"/> convention.
/// </para>
/// </remarks>
public sealed class TempestHost : ITempestHost
{
    private readonly object _gate = new();
    private readonly IReadOnlyList<IConfigurationSource> _configurationSources;
    private readonly IEnumerable<Type>? _discoveryCandidateTypesOverride;
    private readonly string? _pluginsRootPathOverride;
    private readonly IEnumerable<Type>? _hostedServiceCandidateTypesOverride;
    private readonly CancellationTokenSource _shutdownRequested = new();
    private readonly CancellationTokenSource _stopEscalation = new();
    private readonly TaskCompletionSource _runCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HostState _state = HostState.Created;
    private ILogger? _logger;
    private IRuntimeModuleManager? _moduleManager;
    private IModuleLifecycleManager? _lifecycleManager;
    private IHostedServiceManager? _hostedServiceManager;
    private ITempestServiceProvider? _services;

    internal TempestHost(
        IReadOnlyList<IConfigurationSource> configurationSources,
        IEnumerable<Type>? discoveryCandidateTypesOverride,
        string? pluginsRootPathOverride,
        IEnumerable<Type>? hostedServiceCandidateTypesOverride)
    {
        _configurationSources = configurationSources;
        _discoveryCandidateTypesOverride = discoveryCandidateTypesOverride;
        _pluginsRootPathOverride = pluginsRootPathOverride;
        _hostedServiceCandidateTypesOverride = hostedServiceCandidateTypesOverride;
    }

    /// <inheritdoc />
    public HostState State
    {
        get
        {
            lock (_gate)
                return _state;
        }
    }

    /// <inheritdoc />
    public ITempestServiceProvider? Services
    {
        get
        {
            lock (_gate)
                return _services;
        }
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        EnterStarting();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownRequested.Token);
        var runToken = linkedCts.Token;

        try
        {
            await ExecuteStartupPhasesAsync(runToken).ConfigureAwait(false);

            EnterRunning();

            // Always throws: the only way out of Running is a cancellation,
            // either the caller's own token or an internal shutdown request.
            await AwaitShutdownSignalAsync(runToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var shutdownFault = await StopInternalAsync().ConfigureAwait(false);

            // A critical hosted service's StopAsync failure (ADR-0021/0029)
            // is Host-fatal and takes priority over the cancellation itself -
            // the Host ended Faulted, not Stopped, so RunAsync must reflect
            // that rather than completing as if shutdown succeeded.
            if (shutdownFault is not null)
                throw shutdownFault;

            // The caller's own token, as opposed to an internal shutdown
            // request raised via StopAsync(), is the only trigger this method
            // treats as a cancellation to propagate — a StopAsync() shutdown
            // is a deliberate, successful stop (ADR-0013, ADR-0018: never a
            // fault), and RunAsync completes normally for it, matching the
            // established .NET generic-host convention for this scenario.
            if (cancellationToken.IsCancellationRequested)
                throw;
        }
        catch (Exception ex)
        {
            EnterFaulted(ex);
            throw;
        }
        finally
        {
            _runCompletion.TrySetResult();
        }
    }

    private async Task ExecuteStartupPhasesAsync(CancellationToken runToken)
    {
        runToken.ThrowIfCancellationRequested();

        var configurationBuilder = new ConfigurationBuilder();

        foreach (var source in _configurationSources)
            configurationBuilder.AddSource(source);

        var configuration = configurationBuilder.Build();

        ILogSink sink = new ConsoleLogSink();
        ILoggerFactory loggerFactory = new LoggerFactory(configuration, sink);
        var logger = loggerFactory.CreateLogger(LoggingServiceCollectionExtensions.DefaultLoggerCategory);
        _logger = logger;

        logger.Information("Host lifecycle phase completed: Configuration Built.");
        logger.Information("Host lifecycle phase completed: Logging Built.");

        runToken.ThrowIfCancellationRequested();

        // ADR-0026: PlatformVersionProvider's construction moves here, ahead
        // of Plugin Discovery, since the MinimumPlatformVersion compatibility
        // check (ADR-0025, category 4) needs it. Its DI registration remains
        // at Platform Services Registered, below - construction and
        // registration are separable concerns, and only construction needed
        // to move.
        IPlatformVersionProvider platformVersionProvider = new PlatformVersionProvider(logger);

        var pluginDiscoveryService = _pluginsRootPathOverride is not null
            ? new PluginManifestDiscoveryService(_pluginsRootPathOverride, platformVersionProvider, logger)
            : new PluginManifestDiscoveryService(platformVersionProvider, logger);

        var pluginManifests = pluginDiscoveryService.DiscoverManifests();
        logger.Information($"Host lifecycle phase completed: Plugin Discovery. {pluginManifests.Count} plugin(s) eligible.");

        runToken.ThrowIfCancellationRequested();

        IPluginAssemblyLoader pluginAssemblyLoader = new PluginAssemblyLoader(logger);
        var loadedPluginAssemblies = pluginAssemblyLoader.LoadPlugins(pluginManifests);
        logger.Information($"Host lifecycle phase completed: Plugin Loading. {loadedPluginAssemblies.Count} plugin assembly(ies) loaded.");

        runToken.ThrowIfCancellationRequested();

        var discovery = new ReflectionFrameworkDiscoveryService(logger);

        var descriptors = _discoveryCandidateTypesOverride is not null
            ? discovery.DiscoverModules(_discoveryCandidateTypesOverride)
            : discovery.DiscoverModules();

        logger.Information("Host lifecycle phase completed: Module Discovery.");

        runToken.ThrowIfCancellationRequested();

        var moduleManager = new RuntimeModuleManager(logger);

        foreach (var descriptor in descriptors)
            moduleManager.Register(descriptor);

        _moduleManager = moduleManager;
        logger.Information("Host lifecycle phase completed: Module Registration.");

        runToken.ThrowIfCancellationRequested();

        var hostedServiceDiscovery = new HostedServiceDiscoveryService(logger);

        var hostedServiceTypes = _hostedServiceCandidateTypesOverride is not null
            ? hostedServiceDiscovery.DiscoverHostedServiceTypes(_hostedServiceCandidateTypesOverride)
            : hostedServiceDiscovery.DiscoverHostedServiceTypes();

        var services = new ServiceCollection(logger);
        services.AddInstance(configuration);
        services.AddInstance(sink);
        services.AddInstance(loggerFactory);
        services.AddInstance(logger);
        services.AddInstance(platformVersionProvider);
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();

        // ADR-0044: CurrentPrincipalAccessor is constructed directly, once,
        // and registered under both its own concrete type and
        // ICurrentPrincipalAccessor - the same already-built instance under
        // two service-type keys - so IdentityService (which needs write
        // access via the concrete type) and every ordinary consumer
        // (which resolves only the read-only interface) share the exact
        // same object, never two independently-constructed ones. See
        // CurrentPrincipalAccessor's own remarks.
        var currentPrincipalAccessor = new CurrentPrincipalAccessor();
        services.AddInstance<ICurrentPrincipalAccessor>(currentPrincipalAccessor);
        services.AddInstance(currentPrincipalAccessor);
        services.Singleton<IRoleProvider, RoleProvider>();
        services.Singleton<IPermissionEvaluator, PermissionEvaluator>();
        services.Singleton<IIdentityService, IdentityService>();

        // ADR-0041: Persistence is established here, as part of Settings'
        // own scope, ahead of Settings' own registration so the container
        // can resolve IPersistenceStore for SettingsProvider's constructor.
        services.Singleton<IPersistenceStore, PersistenceStore>();
        services.Singleton<ISettingsProvider, SettingsProvider>();

        // ADR-0041/ADR-0045: Audit reuses the same IPersistenceStore
        // Settings established, rather than introducing a second
        // storage mechanism - registered after Persistence and Identity
        // & Permissions, both of which it depends on.
        services.Singleton<IAuditRecorder, AuditRecorder>();
        services.Singleton<IAuditQuery, AuditQuery>();

        // Composition Root pattern (ADR-0009), like Configuration/Logging/
        // PlatformVersionProvider above: DiagnosticsProvider needs references
        // to _lifecycleManager/_hostedServiceManager, both Host-owned and
        // never added to this container (ADR-0017), and neither constructed
        // yet at this point in the phase table - so it is built here,
        // directly, with Func<T> accessors closing over this instance's own
        // fields, and registered as an already-constructed instance rather
        // than a container-constructed singleton. See ADR-0039.
        IDiagnosticsProvider diagnosticsProvider = new DiagnosticsProvider(
            () => State,
            () => { lock (_gate) return _lifecycleManager; },
            () => { lock (_gate) return _hostedServiceManager; });
        services.AddInstance(diagnosticsProvider);

        services.AddDiscoveredModules(moduleManager.GetAll().Select(module => module.Descriptor));
        services.AddDiscoveredHostedServices(hostedServiceTypes);
        logger.Information(
            $"Host lifecycle phase completed: Platform Services Registered. " +
            $"{hostedServiceTypes.Count} hosted service(s) discovered.");

        runToken.ThrowIfCancellationRequested();

        ITempestServiceProvider serviceProvider = new TempestServiceProvider(services, logger);

        lock (_gate)
            _services = serviceProvider;

        logger.Information("Host lifecycle phase completed: Dependency Injection Built.");

        runToken.ThrowIfCancellationRequested();

        var lifecycleManager = new ModuleLifecycleManager(moduleManager, serviceProvider, logger);
        _lifecycleManager = lifecycleManager;

        await lifecycleManager.InitialiseAllAsync(runToken).ConfigureAwait(false);
        await lifecycleManager.StartAllAsync(runToken).ConfigureAwait(false);
        logger.Information("Host lifecycle phase completed: Module Initialisation.");

        runToken.ThrowIfCancellationRequested();

        var hostedServiceManager = new HostedServiceManager(hostedServiceTypes, serviceProvider, logger);
        _hostedServiceManager = hostedServiceManager;

        await hostedServiceManager.StartAllAsync(runToken).ConfigureAwait(false);
        logger.Information("Host lifecycle phase completed: Hosted Services Started.");
    }

    private static async Task AwaitShutdownSignalAsync(CancellationToken runToken)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using (runToken.Register(() => signal.TrySetResult()))
        {
            await signal.Task.ConfigureAwait(false);
        }

        runToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        bool alreadyTerminal;

        lock (_gate)
        {
            if (_state == HostState.Created)
                throw new InvalidHostStateTransitionException(_state, "Stop");

            if (_state == HostState.Disposed)
                throw new InvalidHostStateTransitionException(_state, "Stop");

            alreadyTerminal = _state is HostState.Stopped or HostState.Faulted;
        }

        if (alreadyTerminal)
            return;

        try
        {
            if (!_shutdownRequested.IsCancellationRequested)
                _shutdownRequested.Cancel();
            else if (!_stopEscalation.IsCancellationRequested)
                _stopEscalation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The host finished disposing between the state check above and
            // this call - nothing further to signal.
            return;
        }

        await _runCompletion.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        HostState stateAtEntry;

        lock (_gate)
        {
            stateAtEntry = _state;

            if (stateAtEntry == HostState.Disposed)
                return;
        }

        if (stateAtEntry is HostState.Starting or HostState.Running or HostState.Stopping)
        {
            try
            {
                if (!_shutdownRequested.IsCancellationRequested)
                    _shutdownRequested.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            await _runCompletion.Task.ConfigureAwait(false);

            lock (_gate)
                stateAtEntry = _state;
        }

        if (stateAtEntry != HostState.Stopped && _hostedServiceManager is not null)
        {
            try
            {
                await _hostedServiceManager.StopAllAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // A second, critical hosted-service failure discovered during
                // post-fault teardown itself must not prevent the Host from
                // still reaching Disposed - the original fault is already
                // recorded; this is logged, not rethrown (FOUNDATION.md
                // principle 5: cleanup is always guaranteed, never
                // conditional on how far execution got).
                _logger?.Critical(
                    "A critical hosted service failed to stop during post-fault teardown.",
                    ex);
            }
        }

        if (stateAtEntry != HostState.Stopped && _lifecycleManager is not null)
            await _lifecycleManager.DisposeAllAsync(CancellationToken.None).ConfigureAwait(false);

        // Service Disposal: no-op today - Configuration, Logging, and the DI
        // container implement no IDisposable/IAsyncDisposable (see
        // Failure Behaviour.md and the WP 2.7 Architectural Debt Assessment).

        lock (_gate)
            _state = HostState.Disposed;

        _logger?.Information("Host -> Disposed.");

        _shutdownRequested.Dispose();
        _stopEscalation.Dispose();
    }

    private void EnterStarting()
    {
        lock (_gate)
        {
            if (_state != HostState.Created)
                throw new InvalidHostStateTransitionException(_state, "Run");

            _state = HostState.Starting;
        }
    }

    private void EnterRunning()
    {
        lock (_gate)
            _state = HostState.Running;

        _logger?.Information("Host -> Running.");
    }

    private void EnterFaulted(Exception exception)
    {
        lock (_gate)
            _state = HostState.Faulted;

        _logger?.Critical("Host -> Faulted.", exception);
    }

    private async Task<Exception?> StopInternalAsync()
    {
        lock (_gate)
            _state = HostState.Stopping;

        _logger?.Information("Host -> Stopping.");

        Exception? criticalHostedServiceFault = null;

        if (_hostedServiceManager is not null)
        {
            try
            {
                await _hostedServiceManager.StopAllAsync(_stopEscalation.Token).ConfigureAwait(false);
                _logger?.Information("Host lifecycle phase completed: Hosted Services Stopped.");
            }
            catch (OperationCanceledException)
            {
                _logger?.Information(
                    "Hosted service stop sequence escalated before every service finished " +
                    "stopping; proceeding directly to module disposal.");
            }
            catch (Exception ex)
            {
                // A critical hosted service's StopAsync failure is Host-fatal
                // (ADR-0021/ADR-0029) - recorded here, but the remainder of
                // shutdown (module stop/dispose, service disposal) is still
                // attempted, per ADR-0004/ADR-0019's cleanup guarantee.
                criticalHostedServiceFault = ex;
                _logger?.Critical(
                    "A critical hosted service failed to stop; the Host will fault once " +
                    "shutdown completes.",
                    ex);
            }
        }

        if (_lifecycleManager is not null)
        {
            try
            {
                await _lifecycleManager.StopAllAsync(_stopEscalation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.Information(
                    "Module stop sequence escalated before every module finished stopping; " +
                    "proceeding directly to disposal.");
            }

            _logger?.Information("Host lifecycle phase completed: Module Disposal (Stop).");

            await _lifecycleManager.DisposeAllAsync(CancellationToken.None).ConfigureAwait(false);
            _logger?.Information("Host lifecycle phase completed: Module Disposal (Dispose).");
        }

        // Service Disposal: no-op today - see the remarks on DisposeAsync above.
        _logger?.Information("Host lifecycle phase completed: Service Disposal.");

        _logger?.Information("Shutdown complete.");

        if (criticalHostedServiceFault is not null)
        {
            EnterFaulted(criticalHostedServiceFault);
            return criticalHostedServiceFault;
        }

        lock (_gate)
            _state = HostState.Stopped;

        _logger?.Information("Host -> Stopped.");

        return null;
    }
}
