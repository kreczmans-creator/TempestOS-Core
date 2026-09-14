namespace Tempest.Workspace;

/// <summary>
/// One live object as it appears in the explorer tree at a
/// <see cref="WorkspaceSnapshot.Sequence"/>.
/// </summary>
/// <param name="Id">The object's id.</param>
/// <param name="Kind">The object's canonical Kind.</param>
/// <param name="DisplayName">The object's display name at this sequence.</param>
/// <param name="ParentId">The object's structural parent, or <see langword="null"/> at the root.</param>
public sealed record WorkspaceSnapshotNode(Guid Id, string Kind, string DisplayName, Guid? ParentId);

/// <summary>
/// The cockpit's own minimal, coherent read model (`WP 18.1A`): counts
/// only, every one of them read from the same <see cref="WorkspaceSnapshot.Sequence"/>.
/// </summary>
/// <param name="LiveObjectCount">Every object that is not soft-deleted.</param>
/// <param name="DeletedObjectCount">Every object that is soft-deleted.</param>
public sealed record WorkspaceSnapshotCockpitCounts(int LiveObjectCount, int DeletedObjectCount);

/// <summary>
/// An immutable read taken inside one read transaction, at one store
/// sequence (`WP 18.1A`): the render a view produces from this can never
/// straddle a commit, because every field on it was read inside the same
/// <see cref="Tempest.Core.Persistence.IQueryablePersistenceStore.ExecuteInReadTransactionAsync{T}"/>
/// call.
/// </summary>
public sealed class WorkspaceSnapshot
{
    internal WorkspaceSnapshot(
        long sequence,
        WorkspaceSnapshotKind kind,
        IReadOnlyList<WorkspaceSnapshotNode>? explorerTree = null,
        WorkspaceSnapshotCockpitCounts? cockpit = null,
        IReadOnlyDictionary<string, string?>? objectState = null,
        IReadOnlyDictionary<string, string?>? facets = null)
    {
        Sequence = sequence;
        Kind = kind;
        ExplorerTree = explorerTree;
        Cockpit = cockpit;
        ObjectState = objectState;
        Facets = facets;
    }

    /// <summary>
    /// The store sequence this snapshot was read at (`SqlitePersistenceStore.CurrentSequence`,
    /// `ADR-0144` extended by `WP 18.1A`) — the same counter
    /// <see cref="Tempest.Core.Events.WorkspaceChange.Sequence"/> names, so
    /// a subscriber can check that what it rendered is at least as current
    /// as the event that asked for it.
    /// </summary>
    public long Sequence { get; }

    /// <summary>Which of this snapshot's members is populated.</summary>
    public WorkspaceSnapshotKind Kind { get; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="WorkspaceSnapshotKind.ExplorerTree"/>.</summary>
    public IReadOnlyList<WorkspaceSnapshotNode>? ExplorerTree { get; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="WorkspaceSnapshotKind.Cockpit"/>.</summary>
    public WorkspaceSnapshotCockpitCounts? Cockpit { get; }

    /// <summary>
    /// Populated when <see cref="Kind"/> is <see cref="WorkspaceSnapshotKind.ObjectEditorState"/>:
    /// the requested object's durable state, keyed by field name
    /// (<c>DisplayName</c>, <c>Kind</c>, <c>ParentId</c>, <c>Status</c>,
    /// <c>IsDeleted</c>), or <see langword="null"/> in every value's place
    /// if the object no longer exists at this sequence — an empty
    /// dictionary, never a null <see cref="ObjectState"/>, distinguishes
    /// "read and absent" from "never requested".
    /// </summary>
    public IReadOnlyDictionary<string, string?>? ObjectState { get; }

    /// <summary>
    /// Populated when <see cref="Kind"/> is <see cref="WorkspaceSnapshotKind.FacetSet"/>:
    /// the requested object's own type-specific facet values, exactly as
    /// its Kind wrote them.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Facets { get; }
}
