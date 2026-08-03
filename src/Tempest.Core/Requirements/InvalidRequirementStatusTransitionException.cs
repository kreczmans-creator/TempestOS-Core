namespace Tempest.Core.Requirements;

/// <summary>
/// Thrown when <see cref="IRequirementsService.SetStatusAsync"/> is asked to
/// perform a transition not permitted by <c>WP7.2C Requirement Lifecycle
/// Model.md</c>'s own transition table.
/// </summary>
public sealed class InvalidRequirementStatusTransitionException : RequirementsException
{
    /// <summary>The requirement's own status at the time the transition was attempted.</summary>
    public RequirementStatus FromStatus { get; }

    /// <summary>The requested, forbidden target status.</summary>
    public RequirementStatus ToStatus { get; }

    /// <summary>Initialises a new instance of the <see cref="InvalidRequirementStatusTransitionException"/> class.</summary>
    public InvalidRequirementStatusTransitionException(RequirementStatus fromStatus, RequirementStatus toStatus)
        : base($"Transitioning a requirement from '{fromStatus}' to '{toStatus}' is not permitted.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}
