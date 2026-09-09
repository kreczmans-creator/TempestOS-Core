namespace Tempest.Workspace;

/// <summary>
/// What a <see cref="WorkspaceSnapshot"/> was asked to carry — one shape
/// per view that reads through it (`WP 18.1A`).
/// </summary>
public enum WorkspaceSnapshotKind
{
    /// <summary>The explorer tree: every live object's id, Kind, display name and parent.</summary>
    ExplorerTree,

    /// <summary>The cockpit's own minimal read model: counts, coherent as of one sequence.</summary>
    Cockpit,

    /// <summary>One object's durable state, for an open Object Editor to render.</summary>
    ObjectEditorState,

    /// <summary>One object's raw type-specific facet values.</summary>
    FacetSet,
}

/// <summary>
/// Names what a view needs from one <see cref="IWorkspaceSnapshotReader.ReadAsync"/>
/// call (`WP 18.1A`). Construct through the named factory methods, never
/// the constructor directly — each names exactly the parameters its own
/// <see cref="Kind"/> reads.
/// </summary>
public sealed class WorkspaceSnapshotRequest
{
    private WorkspaceSnapshotRequest(WorkspaceSnapshotKind kind, Guid? objectId, string? objectKind)
    {
        Kind = kind;
        ObjectId = objectId;
        ObjectKind = objectKind;
    }

    /// <summary>What shape of snapshot this request asks for.</summary>
    public WorkspaceSnapshotKind Kind { get; }

    /// <summary>The subject object's id. Populated for <see cref="WorkspaceSnapshotKind.ObjectEditorState"/> and <see cref="WorkspaceSnapshotKind.FacetSet"/>.</summary>
    public Guid? ObjectId { get; }

    /// <summary>The subject object's Kind, as a diagnostic name only — the read itself is Kind-agnostic. Populated alongside <see cref="ObjectId"/>.</summary>
    public string? ObjectKind { get; }

    /// <summary>A request for the whole explorer tree — every live object's identity and structural parent.</summary>
    public static WorkspaceSnapshotRequest ExplorerTree() => new(WorkspaceSnapshotKind.ExplorerTree, null, null);

    /// <summary>A request for the cockpit's own minimal, coherent counts.</summary>
    public static WorkspaceSnapshotRequest Cockpit() => new(WorkspaceSnapshotKind.Cockpit, null, null);

    /// <summary>A request for one object's durable state, for an open editor.</summary>
    public static WorkspaceSnapshotRequest ObjectEditorState(Guid objectId, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        return new WorkspaceSnapshotRequest(WorkspaceSnapshotKind.ObjectEditorState, objectId, kind);
    }

    /// <summary>A request for one object's raw type-specific facet values.</summary>
    public static WorkspaceSnapshotRequest FacetSet(Guid objectId, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        return new WorkspaceSnapshotRequest(WorkspaceSnapshotKind.FacetSet, objectId, kind);
    }
}
