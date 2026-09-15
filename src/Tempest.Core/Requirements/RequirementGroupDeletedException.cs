namespace Tempest.Core.Requirements;

/// <summary>
/// Thrown when <see cref="IRequirementsService.UndeleteAsync"/> is asked to
/// restore a requirement whose own current group has itself been deleted
/// in the meantime (`WP 21.6A`, mirrors
/// <c>EngineeringDomain.EngineeringObjectParentDeletedException</c>'s own
/// identical reasoning) — restoring the requirement structurally under a
/// group that no longer exists would leave it reachable only by direct Id.
/// </summary>
public sealed class RequirementGroupDeletedException : RequirementsException
{
    /// <summary>The requirement Id that could not be restored.</summary>
    public Guid RequirementId { get; }

    /// <summary>The group Id that no longer exists (soft-deleted or gone).</summary>
    public Guid GroupId { get; }

    /// <summary>Initialises a new instance of the <see cref="RequirementGroupDeletedException"/> class.</summary>
    public RequirementGroupDeletedException(Guid requirementId, Guid groupId)
        : base($"Requirement '{requirementId}' cannot be restored — its own group '{groupId}' no longer exists.")
    {
        RequirementId = requirementId;
        GroupId = groupId;
    }
}
