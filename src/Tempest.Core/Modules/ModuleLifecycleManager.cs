using System.Collections.ObjectModel;
using Tempest.Core.Logging;

namespace Tempest.Core.Modules;

/// <summary>
/// The concrete <see cref="IModuleLifecycleManager"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// On construction, takes an ordered snapshot (ascending, ordinal, by
/// <see cref="ModuleDescriptor.Id"/>) of every module currently registered with the
/// supplied <see cref="IRuntimeModuleManager"/>. Modules registered afterwards are
/// not picked up; this mirrors the intended pipeline (discovery → registration →
/// lifecycle), where registration is expected to be complete before lifecycle
/// management begins.
/// </para>
/// <para>
/// A module implementing only <see cref="IModule"/> (not <see cref="IModuleLifecycle"/>)
/// is still driven through <see cref="ModuleState"/>, but no instance is constructed for
/// it and no lifecycle method is invoked — it is treated as having no lifecycle
/// behaviour. An instance is created (via <see cref="Activator.CreateInstance(Type)"/>,
/// matching the instantiation approach already used by
/// <see cref="ReflectionFrameworkDiscoveryService"/>) once, the first time the module is
/// initialised, and reused for its subsequent start, stop, and dispose calls.
/// </para>
/// <para>
/// <b>Failure handling:</b> if a module throws during a lifecycle operation, the failure
/// is logged, the module is marked <see cref="ModuleState.Failed"/> with the exception
/// captured as <see cref="ModuleLifecycleStatus.FailureReason"/>, and the exception is
/// rethrown to the immediate caller. The <c>*AllAsync</c> batch methods catch this per
/// module and continue with the remaining modules in order, so one module's failure does
/// not prevent others from being initialised, started, stopped, or disposed.
/// <see cref="OperationCanceledException"/> is treated differently: it is never swallowed
/// and always propagates immediately out of a batch method, stopping that batch from
/// processing any further modules, since cancellation is a caller-driven request to stop,
/// not a module failure.
/// </para>
/// <para>
/// <b>Invalid transitions:</b> each lifecycle operation has exactly one valid precondition
/// state (for example, <see cref="InitialiseModuleAsync"/> requires
/// <see cref="ModuleState.Registered"/>), except <see cref="DisposeModuleAsync"/>, which is
/// permitted from any state other than <see cref="ModuleState.Disposed"/> itself, since
/// resources may need releasing regardless of which stage a module reached, including
/// <see cref="ModuleState.Failed"/>. The <c>*AllAsync</c> batch methods only attempt an
/// operation on modules already in the correct precondition state for it, so a module
/// simply not yet eligible for a phase is skipped rather than treated as an error.
/// </para>
/// </remarks>
public sealed class ModuleLifecycleManager : IModuleLifecycleManager
{
    private readonly object _gate = new();
    private readonly List<TrackedModule> _orderedModules;
    private readonly Dictionary<string, TrackedModule> _modulesById;
    private readonly LoggingService? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleLifecycleManager"/> class,
    /// taking an ordered snapshot of every module currently registered with
    /// <paramref name="runtimeModuleManager"/>.
    /// </summary>
    /// <param name="runtimeModuleManager">The runtime module manager to source registered modules from.</param>
    /// <param name="logger">
    /// An optional logger used to record lifecycle transitions via the existing TempestOS
    /// logging infrastructure. May be <see langword="null"/> if logging is not required.
    /// </param>
    public ModuleLifecycleManager(IRuntimeModuleManager runtimeModuleManager, LoggingService? logger = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeModuleManager);

        _logger = logger;

        _orderedModules = runtimeModuleManager.GetAll()
            .Select(runtimeModule => new TrackedModule(runtimeModule.Descriptor))
            .OrderBy(tracked => tracked.Descriptor.Id, StringComparer.Ordinal)
            .ToList();

        _modulesById = _orderedModules.ToDictionary(tracked => tracked.Descriptor.Id, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ModuleLifecycleStatus> Modules
    {
        get
        {
            lock (_gate)
            {
                var snapshot = _orderedModules
                    .Select(tracked => new ModuleLifecycleStatus(tracked.Descriptor, tracked.State, tracked.FailureReason))
                    .ToList();

                return new ReadOnlyCollection<ModuleLifecycleStatus>(snapshot);
            }
        }
    }

    /// <inheritdoc />
    public ModuleState GetState(string moduleId)
    {
        lock (_gate)
        {
            return GetTracked(moduleId).State;
        }
    }

    /// <inheritdoc />
    public Task InitialiseAllAsync(CancellationToken cancellationToken) =>
        RunBatchAsync(_orderedModules, tracked => InitialiseModuleAsync(tracked.Descriptor.Id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task StartAllAsync(CancellationToken cancellationToken) =>
        RunBatchAsync(_orderedModules, tracked => StartModuleAsync(tracked.Descriptor.Id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task StopAllAsync(CancellationToken cancellationToken) =>
        RunBatchAsync(Enumerable.Reverse(_orderedModules), tracked => StopModuleAsync(tracked.Descriptor.Id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task DisposeAllAsync(CancellationToken cancellationToken) =>
        RunBatchAsync(Enumerable.Reverse(_orderedModules), tracked => DisposeModuleAsync(tracked.Descriptor.Id, cancellationToken), cancellationToken);

    private static async Task RunBatchAsync(
        IEnumerable<TrackedModule> modules,
        Func<TrackedModule, Task> operation,
        CancellationToken cancellationToken)
    {
        foreach (var tracked in modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await operation(tracked).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Either the module was not eligible for this phase yet
                // (InvalidModuleLifecycleTransitionException) or it failed while
                // running (already logged and marked Failed by the per-module
                // operation). Either way, continue with the remaining modules.
            }
        }
    }

    /// <summary>
    /// Initialises a single tracked module.
    /// </summary>
    /// <remarks>
    /// Internal seam used by <see cref="InitialiseAllAsync"/> and directly by unit tests
    /// to exercise individual transitions — including invalid ones — deterministically.
    /// </remarks>
    internal Task InitialiseModuleAsync(string moduleId, CancellationToken cancellationToken) =>
        TransitionAsync(
            moduleId,
            requiredState: ModuleState.Registered,
            transitioningState: ModuleState.Initialising,
            completedState: ModuleState.Initialised,
            operationName: "Initialise",
            createInstance: true,
            invoke: (instance, token) => instance.InitialiseAsync(token),
            cancellationToken);

    /// <inheritdoc cref="InitialiseModuleAsync"/>
    internal Task StartModuleAsync(string moduleId, CancellationToken cancellationToken) =>
        TransitionAsync(
            moduleId,
            requiredState: ModuleState.Initialised,
            transitioningState: ModuleState.Starting,
            completedState: ModuleState.Running,
            operationName: "Start",
            createInstance: false,
            invoke: (instance, token) => instance.StartAsync(token),
            cancellationToken);

    /// <inheritdoc cref="InitialiseModuleAsync"/>
    internal Task StopModuleAsync(string moduleId, CancellationToken cancellationToken) =>
        TransitionAsync(
            moduleId,
            requiredState: ModuleState.Running,
            transitioningState: ModuleState.Stopping,
            completedState: ModuleState.Stopped,
            operationName: "Stop",
            createInstance: false,
            invoke: (instance, token) => instance.StopAsync(token),
            cancellationToken);

    /// <summary>
    /// Disposes a single tracked module. Valid from any state other than
    /// <see cref="ModuleState.Disposed"/>. See the type-level remarks for why disposal's
    /// precondition differs from the other three operations.
    /// </summary>
    internal async Task DisposeModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TrackedModule tracked;

        lock (_gate)
        {
            tracked = GetTracked(moduleId);

            if (tracked.State == ModuleState.Disposed)
                throw new InvalidModuleLifecycleTransitionException(moduleId, tracked.State, "Dispose");
        }

        _logger?.Information($"Module '{moduleId}' -> Disposing.");

        try
        {
            if (tracked.Instance is not null)
                await tracked.Instance.DisposeAsync(cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                tracked.State = ModuleState.Disposed;
            }

            _logger?.Information($"Module '{moduleId}' -> Disposed.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                tracked.State = ModuleState.Failed;
                tracked.FailureReason = ex;
            }

            _logger?.Information($"Module '{moduleId}' -> Failed during disposal: {ex.Message}");
            throw;
        }
    }

    private async Task TransitionAsync(
        string moduleId,
        ModuleState requiredState,
        ModuleState transitioningState,
        ModuleState completedState,
        string operationName,
        bool createInstance,
        Func<IModuleLifecycle, CancellationToken, Task> invoke,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TrackedModule tracked;

        lock (_gate)
        {
            tracked = GetTracked(moduleId);

            if (tracked.State != requiredState)
                throw new InvalidModuleLifecycleTransitionException(moduleId, tracked.State, operationName);

            tracked.State = transitioningState;

            if (createInstance)
                tracked.Instance = CreateInstance(tracked.Descriptor);
        }

        _logger?.Information($"Module '{moduleId}' -> {transitioningState}.");

        try
        {
            if (tracked.Instance is not null)
                await invoke(tracked.Instance, cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                tracked.State = completedState;
            }

            _logger?.Information($"Module '{moduleId}' -> {completedState}.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                tracked.State = ModuleState.Failed;
                tracked.FailureReason = ex;
            }

            _logger?.Information($"Module '{moduleId}' -> Failed during {operationName}: {ex.Message}");
            throw;
        }
    }

    private static IModuleLifecycle? CreateInstance(ModuleDescriptor descriptor)
    {
        if (!typeof(IModuleLifecycle).IsAssignableFrom(descriptor.ModuleType))
            return null;

        return (IModuleLifecycle)Activator.CreateInstance(descriptor.ModuleType)!;
    }

    private TrackedModule GetTracked(string moduleId)
    {
        if (_modulesById.TryGetValue(moduleId, out var tracked))
            return tracked;

        throw new ArgumentException(
            $"No module with ID '{moduleId}' is tracked by this lifecycle manager.",
            nameof(moduleId));
    }

    private sealed class TrackedModule
    {
        public TrackedModule(ModuleDescriptor descriptor)
        {
            Descriptor = descriptor;
            State = ModuleState.Registered;
        }

        public ModuleDescriptor Descriptor { get; }

        public ModuleState State { get; set; }

        public IModuleLifecycle? Instance { get; set; }

        public Exception? FailureReason { get; set; }
    }
}
