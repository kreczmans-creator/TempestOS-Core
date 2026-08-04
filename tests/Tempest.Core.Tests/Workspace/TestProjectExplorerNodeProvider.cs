using Tempest.App.Workspace;

namespace Tempest.Core.Tests.Workspace;

// A real, minimal IProjectExplorerNodeProvider — proves ADR-0067's own
// second extension point (tree population) end to end.
public sealed class TestProjectExplorerNodeProvider(string kind, IReadOnlyList<ProjectExplorerNode> rootNodes) : IProjectExplorerNodeProvider
{
    public string Kind { get; } = kind;

    public Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(rootNodes);

    public Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProjectExplorerNode>>([]);
}
