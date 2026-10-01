namespace Tempest.Core.Requirements;

/// <summary>Thrown by <see cref="IRequirementsService.UndeleteGroupAsync"/> when the group's own parent group has itself been deleted in the meantime — a group restored under a gone parent would be reachable only by direct Id (`v1.0.0` RC, mirrors <see cref="RequirementGroupDeletedException"/>).</summary>
public sealed class RequirementParentGroupDeletedException : RequirementsException
{
    /// <summary>The group that was to be restored.</summary>
    public Guid GroupId { get; }

    /// <summary>Its parent group, which is deleted.</summary>
    public Guid ParentGroupId { get; }

    /// <summary>Initialises a new instance of the <see cref="RequirementParentGroupDeletedException"/> class.</summary>
    public RequirementParentGroupDeletedException(Guid groupId, Guid parentGroupId)
        : base($"Requirement group '{groupId}' cannot be restored: its parent group '{parentGroupId}' has since been deleted.")
    {
        GroupId = groupId;
        ParentGroupId = parentGroupId;
    }
}
