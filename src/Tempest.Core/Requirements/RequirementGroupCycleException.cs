namespace Tempest.Core.Requirements;

/// <summary>
/// Thrown when <see cref="IRequirementsService.MoveGroupAsync"/> is asked
/// to move a group under itself or one of its own descendants (`TD-67`).
/// </summary>
/// <remarks>
/// No existing exception in this namespace's own family
/// (<see cref="RequirementsException"/> and its siblings —
/// <see cref="RequirementNotFoundException"/>,
/// <see cref="DuplicateRequirementIdentifierException"/>,
/// <see cref="InvalidRequirementStatusTransitionException"/>,
/// <see cref="RequirementGroupHasChildrenException"/>) covers a cycle in
/// the group hierarchy. The structurally closest analogue,
/// <c>EngineeringDomain.CircularParentAssignmentException</c>, belongs to
/// a different, architecturally separate implementation
/// (<c>EngineeringObjectBase</c>/<c>IHasParent</c>) that
/// <see cref="RequirementsService"/> does not otherwise depend on or
/// reference — this service is built directly on
/// <see cref="EngineeringData.IEngineeringDocumentStore"/>, never on the
/// Engineering Domain's own object/repository layer. Reusing it here
/// would be a new, one-off cross-layer dependency for a single exception
/// type; this sibling type instead follows this namespace's own
/// established idiom of one dedicated <see cref="RequirementsException"/>
/// subtype per invalid condition, exactly as
/// <see cref="RequirementGroupHasChildrenException"/> already does.
/// </remarks>
public sealed class RequirementGroupCycleException : RequirementsException
{
    /// <summary>The group Id the move was attempted against.</summary>
    public Guid GroupId { get; }

    /// <summary>The parent group Id that would have created the cycle.</summary>
    public Guid AttemptedParentGroupId { get; }

    /// <summary>Initialises a new instance of the <see cref="RequirementGroupCycleException"/> class.</summary>
    public RequirementGroupCycleException(Guid groupId, Guid attemptedParentGroupId)
        : base($"Requirement group '{groupId}' cannot be moved under '{attemptedParentGroupId}' — it is the group itself or one of its own descendants.")
    {
        GroupId = groupId;
        AttemptedParentGroupId = attemptedParentGroupId;
    }
}
