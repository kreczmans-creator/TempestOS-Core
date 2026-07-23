namespace Tempest.Core.Modules;

/// <summary>
/// Thrown when a lifecycle operation is attempted on a module that is not in a
/// valid precondition state for that operation.
/// </summary>
/// <remarks>
/// For example, attempting to start a module before it has been initialised, or
/// initialising a module that has already been initialised.
/// </remarks>
public sealed class InvalidModuleLifecycleTransitionException : ModuleLifecycleException
{
    /// <summary>
    /// Initialises a new instance of the
    /// <see cref="InvalidModuleLifecycleTransitionException"/> class.
    /// </summary>
    /// <param name="moduleId">The ID of the module the operation was attempted on.</param>
    /// <param name="currentState">The module's actual state at the time of the attempt.</param>
    /// <param name="attemptedOperation">The name of the operation that was attempted.</param>
    public InvalidModuleLifecycleTransitionException(string moduleId, ModuleState currentState, string attemptedOperation)
        : base($"Module '{moduleId}' cannot perform '{attemptedOperation}' while in state '{currentState}'.")
    {
        ModuleId = moduleId;
        CurrentState = currentState;
        AttemptedOperation = attemptedOperation;
    }

    /// <summary>
    /// Gets the ID of the module the operation was attempted on.
    /// </summary>
    public string ModuleId { get; }

    /// <summary>
    /// Gets the module's actual state at the time of the attempt.
    /// </summary>
    public ModuleState CurrentState { get; }

    /// <summary>
    /// Gets the name of the operation that was attempted (for example, "Initialise").
    /// </summary>
    public string AttemptedOperation { get; }
}
