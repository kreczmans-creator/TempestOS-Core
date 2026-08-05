namespace Tempest.Core.EngineeringDomain;

public class EngineeringDomainException : Exception
{
    public EngineeringDomainException(string message) : base(message)
    {
    }

    public EngineeringDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class EngineeringObjectNotFoundException : EngineeringDomainException
{
    public Guid ObjectId { get; }

    public EngineeringObjectNotFoundException(Guid objectId)
        : base($"No Engineering Object with Id '{objectId}' was found.")
    {
        ObjectId = objectId;
    }
}

public sealed class InvalidLifecycleTransitionException : EngineeringDomainException
{
    public LifecycleState From { get; }
    public LifecycleState To { get; }

    public InvalidLifecycleTransitionException(LifecycleState from, LifecycleState to)
        : base($"The lifecycle transition from '{from}' to '{to}' is not permitted.")
    {
        From = from;
        To = to;
    }
}

public sealed class SelfReferentialRelationshipException : EngineeringDomainException
{
    public Guid ObjectId { get; }

    public SelfReferentialRelationshipException(Guid objectId)
        : base($"An Engineering Object cannot hold a relationship to itself ('{objectId}') — {StructuralValidationRules.NoSelfReference}.")
    {
        ObjectId = objectId;
    }
}

/// <summary>`WP 9.0A`: <see cref="IHasParent.MoveAsync"/> was asked to move an object under itself or one of its own descendants.</summary>
public sealed class CircularParentAssignmentException : EngineeringDomainException
{
    public Guid ObjectId { get; }
    public Guid AttemptedParentId { get; }

    public CircularParentAssignmentException(Guid objectId, Guid attemptedParentId)
        : base($"'{objectId}' cannot be moved under '{attemptedParentId}' — it is the object itself or one of its own descendants ({StructuralValidationRules.NoCircularParent}).")
    {
        ObjectId = objectId;
        AttemptedParentId = attemptedParentId;
    }
}

/// <summary>`WP 9.0A`: <see cref="IDeletable.DeleteAsync"/> was asked to delete an object that still has live (non-deleted) children.</summary>
public sealed class EngineeringObjectHasChildrenException : EngineeringDomainException
{
    public Guid ObjectId { get; }
    public int LiveChildCount { get; }

    public EngineeringObjectHasChildrenException(Guid objectId, int liveChildCount)
        : base($"'{objectId}' cannot be deleted — it still has {liveChildCount} live child object(s); move or delete them first ({StructuralValidationRules.NoDeleteWithLiveChildren}).")
    {
        ObjectId = objectId;
        LiveChildCount = liveChildCount;
    }
}
