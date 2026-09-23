namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Gives an Engineering Object a mutable display name — a genuine,
/// disclosed <c>WP 9.0A</c> extension to the Domain, additive over
/// <see cref="IHasBusinessIdentifier"/> (never a reopening of its frozen
/// <c>WP8.2B</c> shape: <see cref="IHasBusinessIdentifier.DisplayName"/>
/// stays a read-only property; this facet only adds a way to change the
/// value it returns). Composed only into the object families this Work
/// Package's own Product Structure scope actually needs to rename
/// (<c>ADR-0075</c>'s composition model, applied a second time).
/// </summary>
public interface IRenamable : IEngineeringObject, IHasBusinessIdentifier
{
    /// <summary>Changes <see cref="IHasBusinessIdentifier.DisplayName"/>. Never changes <see cref="IHasBusinessIdentifier.Identifier"/> — the business key is not in this facet's scope.</summary>
    /// <exception cref="ArgumentException"><paramref name="newDisplayName"/> is null, empty, or whitespace.</exception>
    Task RenameAsync(string newDisplayName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gives an Engineering Object a live, mutable structural parent — a
/// genuine, disclosed <c>WP 9.0A</c> extension to the Domain. <see cref="ParentId"/>
/// is the one authoritative <em>current</em> pointer the Workspace tree
/// renders from; every <see cref="MoveAsync"/> also records a permanent
/// <c>"groupedUnder"</c> <see cref="IEngineeringRelationship"/> to the new
/// parent through the existing, reused <see cref="IHasRelationships.LinkAsync"/>
/// — the old link is never removed, so the append-only Relationship
/// framework keeps the full move history (Digital Thread compatibility)
/// even though <see cref="ParentId"/> itself only ever reflects the latest
/// move.
/// </summary>
/// <remarks>
/// Deliberately independent of <c>IAssembly.ChildIds</c>/<c>ISubAssembly.ParentAssemblyId</c>
/// (frozen <c>WP8.2B</c> shape) — those remain exactly what they always
/// were, a snapshot fixed at construction time. <see cref="ParentId"/> is
/// the only field a mutation after construction ever updates.
/// </remarks>
public interface IHasParent : IEngineeringObject
{
    /// <summary>Gets this object's own current structural parent, or <see langword="null"/> if it has none (a top-level object).</summary>
    Guid? ParentId { get; }

    /// <summary>
    /// Reparents this object under <paramref name="newParentId"/> (or clears its parent, if <see langword="null"/>).
    /// </summary>
    /// <exception cref="CircularParentAssignmentException"><paramref name="newParentId"/> is this object itself, or a descendant of it.</exception>
    Task MoveAsync(Guid? newParentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gives an Engineering Object a soft-delete state — a genuine, disclosed
/// <c>WP 9.0A</c> extension to the Domain. Deliberately not a new
/// <see cref="LifecycleState"/> member: deletion is a structural fact, not
/// a lifecycle stage, and <see cref="LifecycleState"/> is a platform-wide
/// frozen vocabulary (<c>ADR-0074</c>). Never erases the underlying
/// document/revision/relationship history — matches every other Domain
/// mutation's own append-only, nothing-silently-destroyed ethos.
/// </summary>
public interface IDeletable : IEngineeringObject
{
    /// <summary>Gets whether this object has been deleted.</summary>
    bool IsDeleted { get; }

    /// <summary>Marks this object deleted.</summary>
    /// <exception cref="EngineeringObjectHasChildrenException">A live (non-deleted) <see cref="IHasParent"/> object still reports this object as its <see cref="IHasParent.ParentId"/>.</exception>
    Task DeleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores this object from deletion — `WP 21.1A`'s own closing of the
    /// gap `ADR-0098` disclosed at `v0.10.0` ("Delete is already a soft
    /// delete ... with no 'restore' operation anywhere in this platform to
    /// invert into"). Commits through the identical state path
    /// <see cref="DeleteAsync"/> itself uses — one transaction, one audit
    /// row — never a second mechanism.
    /// </summary>
    /// <remarks>
    /// Never restores attachment payload bytes <see cref="DeleteAsync"/>
    /// already removed durably: this reverses the structural fact
    /// (<see cref="IsDeleted"/>), not the purge <see cref="DeleteAsync"/>'s
    /// own remarks already record as irreversible. A disclosed, honest
    /// limitation, not a silent gap.
    /// </remarks>
    /// <exception cref="InvalidOperationException">This object is not currently deleted.</exception>
    /// <exception cref="EngineeringObjectParentDeletedException">This object's own current <see cref="IHasParent.ParentId"/> (if it has one) is itself deleted.</exception>
    Task UndeleteAsync(CancellationToken cancellationToken = default);
}
