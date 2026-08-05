namespace Tempest.Core.Requirements;

/// <summary>
/// Thrown when <see cref="IRequirementsService.DeleteGroupAsync"/> is asked
/// to delete a group that still has live (non-deleted) requirements
/// grouped directly under it, or live (non-deleted) sub-groups parented
/// directly under it (`WP 9.1A`, mirrors
/// <c>EngineeringDomain.EngineeringObjectHasChildrenException</c>'s own
/// identical reasoning). See
/// <c>RequirementsService.CountLiveGroupChildrenAsync</c>'s own remarks for
/// this guard's short-lived, disclosed history — it originally checked
/// grouped requirements only, before <see cref="IRequirementsService.ListGroupsAsync"/>
/// existed to make sub-group discovery possible.
/// </summary>
public sealed class RequirementGroupHasChildrenException : RequirementsException
{
    /// <summary>The group Id that still has live children.</summary>
    public Guid GroupId { get; }

    /// <summary>The number of live grouped requirements found.</summary>
    public int LiveChildCount { get; }

    /// <summary>Initialises a new instance of the <see cref="RequirementGroupHasChildrenException"/> class.</summary>
    public RequirementGroupHasChildrenException(Guid groupId, int liveChildCount)
        : base($"Requirement group '{groupId}' cannot be deleted — it still has {liveChildCount} live grouped requirement(s); move or delete them first.")
    {
        GroupId = groupId;
        LiveChildCount = liveChildCount;
    }
}
