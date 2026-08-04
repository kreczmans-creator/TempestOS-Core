namespace Tempest.App.Workspace;

/// <summary>One node in the <see cref="IProjectExplorer"/>'s own tree.</summary>
/// <param name="Id">The node's own unique identifier — an engineering object's own Id for a <see cref="ProjectExplorerNodeType.Object"/> node, or a stable, provider-assigned Id otherwise.</param>
/// <param name="Title">The node's own display label.</param>
/// <param name="Kind">The backing object's own <c>Kind</c>, or <see langword="null"/> for a <see cref="ProjectExplorerNodeType.Category"/> node with no backing object.</param>
/// <param name="HasChildren">Whether <see cref="IProjectExplorer.GetChildrenAsync"/> may return a non-empty result for this node.</param>
/// <param name="NodeType">What kind of thing this node represents.</param>
public sealed record ProjectExplorerNode(
    Guid Id,
    string Title,
    string? Kind,
    bool HasChildren,
    ProjectExplorerNodeType NodeType);
