namespace Tempest.Core.Requirements;

/// <summary>Thrown by <see cref="IRequirementsService.UndeleteGroupAsync"/> when the group is not currently deleted — the Undo of a group Delete can only restore what that Delete removed (`v1.0.0` RC, mirrors <see cref="RequirementNotDeletedException"/>).</summary>
public sealed class RequirementGroupNotDeletedException : RequirementsException
{
    /// <summary>The group Id that is not currently deleted.</summary>
    public Guid GroupId { get; }

    /// <summary>Initialises a new instance of the <see cref="RequirementGroupNotDeletedException"/> class.</summary>
    public RequirementGroupNotDeletedException(Guid groupId)
        : base($"Requirement group '{groupId}' is not deleted, so it cannot be restored.")
    {
        GroupId = groupId;
    }
}
