namespace Tempest.Core.Modules;

/// <summary>
/// Describes the lifecycle state of a module known to the TempestOS runtime.
/// </summary>
/// <remarks>
/// The full lifecycle was established in WP 2.2 so that <see cref="RuntimeModule"/>
/// and <see cref="IRuntimeModuleManager"/> would have a stable API for future work
/// packages. WP 2.2 only ever produced modules in the <see cref="Discovered"/> or
/// <see cref="Registered"/> states; WP 2.3 (<see cref="IModuleLifecycleManager"/>)
/// exercises the remaining states, driving a module through them in order:
/// <see cref="Registered"/> → <see cref="Initialising"/> → <see cref="Initialised"/> →
/// <see cref="Starting"/> → <see cref="Running"/> → <see cref="Stopping"/> →
/// <see cref="Stopped"/> → <see cref="Disposed"/>, with <see cref="Failed"/> reachable
/// from any non-terminal state and <see cref="Disabled"/> reserved for future work.
/// </remarks>
public enum ModuleState
{
    /// <summary>
    /// The module has been found by discovery but has not yet been registered
    /// with the runtime module manager. This is the default (zero) value of
    /// <see cref="ModuleState"/>, so that an uninitialised <see cref="ModuleState"/>
    /// never appears to represent a module that is further along its lifecycle
    /// than it actually is.
    /// </summary>
    Discovered = 0,

    /// <summary>
    /// The module has been registered with the runtime module manager.
    /// </summary>
    Registered,

    /// <summary>
    /// The module's <see cref="IModuleLifecycle.InitialiseAsync"/> is in progress.
    /// </summary>
    Initialising,

    /// <summary>
    /// The module has completed initialisation and is ready to be started.
    /// </summary>
    Initialised,

    /// <summary>
    /// The module's <see cref="IModuleLifecycle.StartAsync"/> is in progress.
    /// </summary>
    Starting,

    /// <summary>
    /// The module is running.
    /// </summary>
    Running,

    /// <summary>
    /// The module's <see cref="IModuleLifecycle.StopAsync"/> is in progress.
    /// </summary>
    Stopping,

    /// <summary>
    /// The module has stopped after having run.
    /// </summary>
    Stopped,

    /// <summary>
    /// The module has been explicitly disabled. Reserved for a future work package;
    /// not produced by the runtime module manager or the lifecycle manager introduced
    /// in WP 2.3.
    /// </summary>
    Disabled,

    /// <summary>
    /// The module's <see cref="IModuleLifecycle.DisposeAsync"/> has completed. Terminal.
    /// </summary>
    Disposed,

    /// <summary>
    /// The module failed during a lifecycle operation. See
    /// <see cref="RuntimeModule.FailureReason"/> and
    /// <see cref="ModuleLifecycleStatus.FailureReason"/>.
    /// </summary>
    Failed
}
