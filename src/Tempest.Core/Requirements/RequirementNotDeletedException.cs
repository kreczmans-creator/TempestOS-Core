namespace Tempest.Core.Requirements;

/// <summary>
/// Thrown when <see cref="IRequirementsService.UndeleteAsync"/> is asked to
/// restore a requirement that is not currently deleted (`WP 21.6A`, mirrors
/// <c>EngineeringDomain.EngineeringObjectNotDeletedException</c>'s own
/// identical reasoning).
/// </summary>
public sealed class RequirementNotDeletedException : RequirementsException
{
    /// <summary>The requirement Id that is not currently deleted.</summary>
    public Guid RequirementId { get; }

    /// <summary>Initialises a new instance of the <see cref="RequirementNotDeletedException"/> class.</summary>
    public RequirementNotDeletedException(Guid requirementId)
        : base($"Requirement '{requirementId}' is not deleted, so it cannot be restored.")
    {
        RequirementId = requirementId;
    }
}
