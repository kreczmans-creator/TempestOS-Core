namespace Tempest.Core.Modules;

/// <summary>
/// Describes the lifecycle state of a module known to the TempestOS runtime.
/// </summary>
/// <remarks>
/// The full lifecycle is established now so that <see cref="RuntimeModule"/> and
/// <see cref="IRuntimeModuleManager"/> have a stable API for future work packages.
/// The Runtime Module Manager (WP 2.2) only ever produces modules in the
/// <see cref="Discovered"/> or <see cref="Registered"/> states; the remaining
/// states are reserved for the lifecycle manager introduced in later work and are
/// not exercised by this work package.
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
    /// The module has completed initialisation. Reserved for the lifecycle manager.
    /// </summary>
    Initialised,

    /// <summary>
    /// The module is running. Reserved for the lifecycle manager.
    /// </summary>
    Running,

    /// <summary>
    /// The module has been explicitly disabled. Reserved for the lifecycle manager.
    /// </summary>
    Disabled,

    /// <summary>
    /// The module failed during initialisation or execution. Reserved for the
    /// lifecycle manager. See <see cref="RuntimeModule.FailureReason"/>.
    /// </summary>
    Failed
}
