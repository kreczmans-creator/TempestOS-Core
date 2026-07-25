namespace Tempest.Core.BackgroundServices;

/// <summary>
/// An immutable snapshot of one hosted service's own lifecycle status at the moment it
/// was queried.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Modules.ModuleLifecycleStatus"/>'s own shape: a read-only snapshot
/// of state <see cref="HostedServiceManager"/> tracks internally and mutates over time,
/// handed out only as an immutable value so a caller can never observe it changing
/// mid-read. <see langword="internal"/> constructor — only <see cref="HostedServiceManager"/>
/// can create one.
/// </remarks>
public sealed class HostedServiceStatus
{
    internal HostedServiceStatus(Type serviceType, HostedServiceState state, Exception? failureReason)
    {
        ServiceType = serviceType;
        State = state;
        FailureReason = failureReason;
    }

    /// <summary>Gets the discovered hosted service's own concrete type.</summary>
    public Type ServiceType { get; }

    /// <summary>Gets the service's current lifecycle state.</summary>
    public HostedServiceState State { get; }

    /// <summary>
    /// Gets the exception captured when <see cref="State"/> is
    /// <see cref="HostedServiceState.Failed"/>, or <see langword="null"/> otherwise.
    /// </summary>
    public Exception? FailureReason { get; }
}
