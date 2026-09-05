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

/// <summary>
/// `WP 16.4B-R4`: a durable write was attempted on an Engineering Object
/// instance that <see cref="IHasRevisions.ReviseAsync"/> has already
/// superseded.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IHasRevisions.ReviseAsync"/> constructs a second,
/// independently-mutable instance for the same Id and registers it in the
/// repository in place of its predecessor. Both instances then map onto
/// one durable record. Serialising their writes is not enough: the
/// successor was built from a snapshot of the predecessor taken at
/// revision time, so a predecessor write that lands <em>after</em> that
/// snapshot is invisible to the successor, and the successor's next
/// mutation overwrites the whole record — silently discarding it.
/// </para>
/// <para>
/// This exception is that overwrite made loud. A predecessor is marked
/// superseded inside the same per-object write lock that guards every
/// durable write, so the mark and any competing write are strictly
/// ordered: a write that acquires the lock first succeeds and is carried
/// into the successor's snapshot; one that acquires it after the
/// revision throws this, having changed nothing on disk. The alternative
/// — letting it succeed — is the permanent, silent loss of whatever it
/// wrote, and where the lost field is an attachment reference the
/// reconciliation sweep then deletes the file's bytes as an orphan.
/// </para>
/// <para>
/// Recovering from this is to re-fetch the object from
/// <c>IEngineeringObjectRepository</c> and re-apply the change to the
/// instance it returns. A stale handle cannot be repaired in place,
/// because the edit it carries was computed against a state that is no
/// longer current.
/// </para>
/// </remarks>
public sealed class SupersededEngineeringObjectException : EngineeringDomainException
{
    /// <summary>The Id shared by the superseded instance and its successor.</summary>
    public Guid ObjectId { get; }

    /// <summary>The revision number the successor carries.</summary>
    public int SuccessorRevisionNumber { get; }

    /// <summary>Initialises a new instance of the <see cref="SupersededEngineeringObjectException"/> class.</summary>
    public SupersededEngineeringObjectException(Guid objectId, int successorRevisionNumber)
        : base($"This instance of Engineering Object '{objectId}' was superseded by revision {successorRevisionNumber} and can no longer be written to durable state \u2014 re-fetch the object from the repository and re-apply the change to the instance it returns.")
    {
        ObjectId = objectId;
        SuccessorRevisionNumber = successorRevisionNumber;
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
