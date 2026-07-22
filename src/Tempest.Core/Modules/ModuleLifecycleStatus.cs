namespace Tempest.Core.Modules;

/// <summary>
/// A point-in-time snapshot of a module's current lifecycle status, as tracked by
/// an <see cref="IModuleLifecycleManager"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="RuntimeModule"/> (an immutable record of registration, whose
/// <see cref="RuntimeModule.State"/> only ever reflects the moment it was
/// registered), a module's lifecycle status changes over time as an
/// <see cref="IModuleLifecycleManager"/> drives it through <see cref="ModuleState"/>.
/// Each <see cref="ModuleLifecycleStatus"/> instance is itself an immutable snapshot;
/// querying <see cref="IModuleLifecycleManager.Modules"/> again returns fresh ones.
/// </remarks>
public sealed class ModuleLifecycleStatus
{
    internal ModuleLifecycleStatus(ModuleDescriptor descriptor, ModuleState state, Exception? failureReason)
    {
        Descriptor = descriptor;
        State = state;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets the descriptor of the module this status describes.
    /// </summary>
    public ModuleDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the module's current lifecycle state.
    /// </summary>
    public ModuleState State { get; }

    /// <summary>
    /// Gets the exception that caused the module to enter the
    /// <see cref="ModuleState.Failed"/> state, if any.
    /// </summary>
    public Exception? FailureReason { get; }
}
