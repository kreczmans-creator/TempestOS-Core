using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace;

/// <summary>One node in the <see cref="IProjectExplorer"/>'s own tree.</summary>
/// <param name="Id">The node's own unique identifier — an engineering object's own Id for a <see cref="ProjectExplorerNodeType.Object"/> node, or a stable, provider-assigned Id otherwise.</param>
/// <param name="Title">The node's own display label.</param>
/// <param name="Kind">The backing object's own <c>Kind</c>, or <see langword="null"/> for a <see cref="ProjectExplorerNodeType.Category"/> node with no backing object.</param>
/// <param name="HasChildren">Whether <see cref="IProjectExplorer.GetChildrenAsync"/> may return a non-empty result for this node.</param>
/// <param name="NodeType">What kind of thing this node represents.</param>
/// <param name="Lifecycle">
/// The backing object's own <see cref="IHasLifecycle.Status"/> (`WP
/// 10.5C`, "coloured object states, lifecycle indicators") —
/// <see langword="null"/> for any node with no backing object, or whose
/// backing object does not carry a lifecycle at all (confirmed per-Kind,
/// never guessed). A trailing, defaulted, additive parameter — every
/// pre-existing call site across all six real node providers compiles
/// unchanged; each was individually revisited to pass its own
/// already-in-scope object's own real status at zero extra read cost
/// (the object is already held locally to read <c>Title</c>/<c>Kind</c>
/// from), never a new fetch.
/// </param>
public sealed record ProjectExplorerNode(
    Guid Id,
    string Title,
    string? Kind,
    bool HasChildren,
    ProjectExplorerNodeType NodeType,
    LifecycleState? Lifecycle = null);
