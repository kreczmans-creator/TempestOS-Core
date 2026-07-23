namespace Tempest.Core.Modules;

/// <summary>
/// The single orchestration point for module execution: drives every module
/// registered with an <see cref="IRuntimeModuleManager"/> through initialisation,
/// startup, shutdown, and disposal.
/// </summary>
/// <remarks>
/// Modules are initialised and started in ascending ordinal order by
/// <see cref="ModuleDescriptor.Id"/>, and stopped and disposed in the reverse of
/// that order. A module that throws during a lifecycle operation is marked
/// <see cref="ModuleState.Failed"/> and logged; other modules continue to be
/// processed in order. This type performs no reflection-based discovery, no module
/// registration, and no dependency injection — it only drives the lifecycle of
/// modules already registered with the <see cref="IRuntimeModuleManager"/> supplied
/// at construction.
/// <para>
/// <b>Ordering is currently an implementation convenience, not a permanent design
/// commitment:</b> ordering by <see cref="ModuleDescriptor.Id"/> was chosen because
/// no dedicated ordering metadata exists on <see cref="IModule"/>/<see cref="ModuleDescriptor"/>
/// today, and it mirrors the ordering convention <c>ReflectionFrameworkDiscoveryService</c>
/// already uses. Future work may introduce dedicated startup-priority metadata (for
/// example a <c>Priority</c> or <c>StartupOrder</c> property) without changing this
/// manager's contract or behaviour — only the sort key it derives its order from.
/// </para>
/// </remarks>
public interface IModuleLifecycleManager
{
    /// <summary>
    /// Gets the current lifecycle status of every module this manager is tracking,
    /// in ascending order by <see cref="ModuleDescriptor.Id"/>.
    /// </summary>
    /// <remarks>
    /// The returned collection is a read-only snapshot; it cannot be used to mutate
    /// this manager's internal state.
    /// </remarks>
    IReadOnlyCollection<ModuleLifecycleStatus> Modules { get; }

    /// <summary>
    /// Gets the current lifecycle state of the module with the given ID.
    /// </summary>
    /// <param name="moduleId">The module ID to look up.</param>
    /// <returns>The module's current <see cref="ModuleState"/>.</returns>
    /// <exception cref="ArgumentException">No module with <paramref name="moduleId"/> is tracked.</exception>
    ModuleState GetState(string moduleId);

    /// <summary>
    /// Initialises every tracked module currently in the <see cref="ModuleState.Registered"/>
    /// state, in ascending order by ID.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <remarks>
    /// If a module throws during initialisation, it is marked <see cref="ModuleState.Failed"/>
    /// and logged, and the remaining modules are still processed. If
    /// <paramref name="cancellationToken"/> is already cancelled, or becomes cancelled between
    /// modules, an <see cref="OperationCanceledException"/> propagates immediately and no
    /// further modules in this call are processed.
    /// </remarks>
    Task InitialiseAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts every tracked module currently in the <see cref="ModuleState.Initialised"/>
    /// state, in ascending order by ID.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <remarks>
    /// Failure and cancellation behaviour matches <see cref="InitialiseAllAsync"/>.
    /// </remarks>
    Task StartAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops every tracked module currently in the <see cref="ModuleState.Running"/>
    /// state, in descending order by ID.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <remarks>
    /// Failure and cancellation behaviour matches <see cref="InitialiseAllAsync"/>.
    /// </remarks>
    Task StopAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Disposes every tracked module not already <see cref="ModuleState.Disposed"/>,
    /// in descending order by ID.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <remarks>
    /// Unlike the other three operations, disposal is valid from any state other than
    /// <see cref="ModuleState.Disposed"/> itself — including <see cref="ModuleState.Failed"/>
    /// — since resources may need releasing regardless of which lifecycle stage a module
    /// reached. Failure and cancellation behaviour otherwise matches
    /// <see cref="InitialiseAllAsync"/>.
    /// </remarks>
    Task DisposeAllAsync(CancellationToken cancellationToken);
}
