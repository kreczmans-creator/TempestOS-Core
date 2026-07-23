namespace Tempest.Core.Runtime;

/// <summary>
/// Describes the state of a <see cref="ITempestHost"/>'s own lifecycle,
/// independent of any individual module's <see cref="Modules.ModuleState"/>.
/// </summary>
/// <remarks>
/// See <c>Runtime State Machine.md</c> (ADR-0012) for the full state diagram,
/// transition table, and illegal-transitions list this enum implements exactly.
/// A host's own state answers "what phase of its own lifecycle is the host in";
/// it is never derived from, and never derives, the state of any individual
/// module.
/// </remarks>
public enum HostState
{
    /// <summary>
    /// The host object exists; nothing has been built. This is the default
    /// (zero) value, so that an uninitialised <see cref="HostState"/> never
    /// appears to represent a host further along its lifecycle than it
    /// actually is.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Any lifecycle phase from Configuration Built through Module
    /// Initialisation is in progress.
    /// </summary>
    Starting,

    /// <summary>
    /// Module Initialisation completed; the platform is up. Does not imply
    /// every module succeeded — see ADR-0013.
    /// </summary>
    Running,

    /// <summary>
    /// Controlled shutdown in progress: Module Disposal, then Service
    /// Disposal. Entered from <see cref="Running"/> (a graceful shutdown
    /// request) or from <see cref="Starting"/> (startup cancellation, or an
    /// early shutdown request — ADR-0018); the procedure is identical either
    /// way.
    /// </summary>
    Stopping,

    /// <summary>
    /// Controlled shutdown completed — whether it was a graceful,
    /// post-<see cref="Running"/> shutdown or an interrupted startup torn
    /// down via <see cref="Stopping"/>.
    /// </summary>
    Stopped,

    /// <summary>
    /// A platform-service failure aborted startup, or a genuine host-level
    /// defect occurred during <see cref="Running"/> or <see cref="Stopping"/>.
    /// </summary>
    Faulted,

    /// <summary>
    /// Terminal. Every resource that could be released has had release
    /// attempted. No outgoing transition exists from this state.
    /// </summary>
    Disposed
}
