namespace Tempest.App.Workspace;

/// <summary>
/// The object tree for the currently selected top-level area. Never calls
/// any Engineering Core service directly — every read delegates to
/// whichever <see cref="IProjectExplorerNodeProvider"/> is registered for
/// the current area (`ADR-0067`).
/// </summary>
public interface IProjectExplorer : IWorkspacePanel
{
    /// <summary>
    /// Gets the current area's own root nodes — empty if no
    /// <see cref="IProjectExplorerNodeProvider"/> is registered for it.
    /// </summary>
    Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default);

    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known node.</exception>
    Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default);

    /// <summary>Re-reads the current area's own tree from its registered provider.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
