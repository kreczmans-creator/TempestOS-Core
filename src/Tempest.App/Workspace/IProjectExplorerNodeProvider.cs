namespace Tempest.App.Workspace;

/// <summary>
/// Populates the <see cref="IProjectExplorer"/>'s own tree for one specific
/// top-level area — a future Engineering Discipline Module's own answer to
/// "how does my area's own tree get populated" (`ADR-0067`).
/// </summary>
public interface IProjectExplorerNodeProvider
{
    /// <summary>
    /// Gets the single top-level area <see cref="Tempest.Core.Navigation.NavigationItem.Id"/>
    /// this provider populates.
    /// </summary>
    string Kind { get; }

    /// <summary>Gets this area's own root nodes.</summary>
    Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets <paramref name="nodeId"/>'s own children.</summary>
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known node.</exception>
    Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default);
}
