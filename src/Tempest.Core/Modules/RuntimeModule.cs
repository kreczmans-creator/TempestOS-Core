namespace Tempest.Core.Modules;

/// <summary>
/// Represents the runtime state of a module once it has been registered with an
/// <see cref="IRuntimeModuleManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is immutable and its constructor is <see langword="internal"/>: instances
/// are only ever created by the runtime module infrastructure (currently
/// <see cref="RuntimeModuleManager"/>), never directly by consumers. This keeps the
/// runtime module manager the single authoritative source of runtime module state.
/// </para>
/// <para>
/// The public surface exposes only the metadata this work package requires
/// (<see cref="Descriptor"/>, <see cref="State"/>, <see cref="RegisteredAt"/>,
/// <see cref="FailureReason"/>). Because construction is internal and every property
/// is a simple get-only value, later work packages can add further runtime metadata
/// (health, metrics, dependencies, configuration, permissions, etc.) as additional
/// properties without breaking this public API.
/// </para>
/// </remarks>
public sealed class RuntimeModule
{
    internal RuntimeModule(
        ModuleDescriptor descriptor,
        ModuleState state,
        DateTimeOffset registeredAt,
        Exception? failureReason = null)
    {
        Descriptor = descriptor;
        State = state;
        RegisteredAt = registeredAt;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets the discovery-time metadata this runtime module was registered from.
    /// </summary>
    public ModuleDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the current lifecycle state of the module.
    /// </summary>
    public ModuleState State { get; }

    /// <summary>
    /// Gets the point in time at which the module was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; }

    /// <summary>
    /// Gets the exception that caused the module to enter the
    /// <see cref="ModuleState.Failed"/> state, if any.
    /// </summary>
    /// <remarks>
    /// Always <see langword="null"/> for modules produced by this work package,
    /// since only the <see cref="ModuleState.Discovered"/> and
    /// <see cref="ModuleState.Registered"/> states are exercised here. Reserved
    /// for the lifecycle manager introduced in later work.
    /// </remarks>
    public Exception? FailureReason { get; }
}
