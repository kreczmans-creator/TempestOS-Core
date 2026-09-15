namespace Tempest.Core.Requirements;

/// <summary>
/// The <see cref="Audit.IAuditRecord.Action"/> values <see cref="RequirementsService"/>
/// writes, one per committed mutator (`WP 21.6A`, OSA-15) — named in the
/// same style <see cref="EngineeringDomain.EngineeringAuditActions"/>
/// already established for <c>EngineeringObjectBase</c>, so a query for
/// "every Requirement rename" and a query for "every requirement revise"
/// are written against the same kind of string.
/// </summary>
public static class RequirementsAuditActions
{
    /// <summary>A requirement was created.</summary>
    public const string Created = "requirements.created";

    /// <summary>A requirement's statement was revised.</summary>
    public const string Revised = "requirements.revised";

    /// <summary>A requirement's lifecycle status changed.</summary>
    public const string StatusChanged = "requirements.status-changed";

    /// <summary>A requirement's owner changed.</summary>
    public const string OwnerChanged = "requirements.owner-changed";

    /// <summary>A requirement's priority changed.</summary>
    public const string PriorityChanged = "requirements.priority-changed";

    /// <summary>A requirement was soft-deleted.</summary>
    public const string Deleted = "requirements.deleted";

    /// <summary>A requirement was restored from deletion (`WP 21.6A`).</summary>
    public const string Undeleted = "requirements.undeleted";

    /// <summary>A requirement moved into a different group, or was ungrouped.</summary>
    public const string Moved = "requirements.moved";

    /// <summary>A typed relationship was recorded from a requirement.</summary>
    public const string Linked = "requirements.linked";

    /// <summary>A requirement collection was created.</summary>
    public const string CollectionCreated = "requirements.collection-created";

    /// <summary>A requirement collection was soft-deleted.</summary>
    public const string CollectionDeleted = "requirements.collection-deleted";

    /// <summary>A requirement was added to a collection.</summary>
    public const string AddedToCollection = "requirements.added-to-collection";

    /// <summary>A requirement group was created.</summary>
    public const string GroupCreated = "requirements.group-created";

    /// <summary>A requirement group was reparented, or made a root group.</summary>
    public const string GroupMoved = "requirements.group-moved";

    /// <summary>A requirement group was soft-deleted.</summary>
    public const string GroupDeleted = "requirements.group-deleted";
}
