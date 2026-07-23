namespace Tempest.Core.Runtime;

/// <summary>
/// Thrown when an operation is attempted on a <see cref="ITempestHost"/> that
/// is not in a valid precondition state for that operation.
/// </summary>
/// <remarks>
/// For example, calling <see cref="ITempestHost.RunAsync"/> a second time on a
/// host that has already run (restart is prohibited — see ADR-0015), or
/// calling <see cref="ITempestHost.StopAsync"/> on a host that has not yet
/// started. See <c>Runtime State Machine.md</c>'s "Illegal Transitions"
/// section for the full list this exception guards.
/// </remarks>
public sealed class InvalidHostStateTransitionException : HostException
{
    /// <summary>
    /// Initialises a new instance of the
    /// <see cref="InvalidHostStateTransitionException"/> class.
    /// </summary>
    /// <param name="currentState">The host's actual state at the time of the attempt.</param>
    /// <param name="attemptedOperation">The name of the operation that was attempted.</param>
    public InvalidHostStateTransitionException(HostState currentState, string attemptedOperation)
        : base($"The host cannot perform '{attemptedOperation}' while in state '{currentState}'.")
    {
        CurrentState = currentState;
        AttemptedOperation = attemptedOperation;
    }

    /// <summary>
    /// Gets the host's actual state at the time of the attempt.
    /// </summary>
    public HostState CurrentState { get; }

    /// <summary>
    /// Gets the name of the operation that was attempted (for example, "Run").
    /// </summary>
    public string AttemptedOperation { get; }
}
