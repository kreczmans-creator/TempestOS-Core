using Tempest.Core.EngineeringDomain;
using Tempest.Core.Tasks;

namespace Tempest.Workspace.Tasks;

/// <summary>
/// Populates the Project Explorer's own Tasks area: an Open group and a
/// Done group, each listing its own manual tasks (`WP 19.5C`) — simpler
/// than <c>Quotations.QuotationNodeProvider</c>'s own per-project grouping,
/// because a <see cref="ManualTask"/> may belong to no project at all.
/// </summary>
public sealed class TaskNodeProvider : IProjectExplorerNodeProvider
{
    private static readonly Guid OpenGroupNodeId = new("00000000-0000-0000-0000-00000000005f");
    private static readonly Guid DoneGroupNodeId = new("00000000-0000-0000-0000-000000000d05");

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="TaskNodeProvider"/> class.</summary>
    public TaskNodeProvider(string kind, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);

        Kind = kind;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await LiveTasksAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            new ProjectExplorerNode(OpenGroupNodeId, "Open", null, tasks.Any(t => !t.Done), ProjectExplorerNodeType.Category),
            new ProjectExplorerNode(DoneGroupNodeId, "Done", null, tasks.Any(t => t.Done), ProjectExplorerNodeType.Category),
        ];
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Tasks node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        if (nodeId != OpenGroupNodeId && nodeId != DoneGroupNodeId)
            throw new ArgumentException($"'{nodeId}' is not a known Tasks node.", nameof(nodeId));

        var wantDone = nodeId == DoneGroupNodeId;
        var tasks = await LiveTasksAsync(cancellationToken).ConfigureAwait(false);

        return tasks
            .Where(t => t.Done == wantDone)
            .OrderBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ThenBy(t => t.DisplayName, StringComparer.Ordinal)
            .Select(ToTaskNode)
            .ToList();
    }

    /// <summary>A task's own ancestry is just its own group — Open or Done.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false) is not ManualTask task)
            return [];

        return task.Done
            ? [new ProjectExplorerNode(DoneGroupNodeId, "Done", null, true, ProjectExplorerNodeType.Category)]
            : [new ProjectExplorerNode(OpenGroupNodeId, "Open", null, true, ProjectExplorerNodeType.Category)];
    }

    private async Task<List<ManualTask>> LiveTasksAsync(CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(ManualTask.CanonicalKind, cancellationToken).ConfigureAwait(false))
        .OfType<ManualTask>()
        .Where(IsLive)
        .ToList();

    private static ProjectExplorerNode ToTaskNode(ManualTask task) =>
        new(
            task.Id, task.DueDate is { } due ? $"{task.DisplayName} (due {due:yyyy-MM-dd})" : task.DisplayName, task.Kind, false,
            ProjectExplorerNodeType.Object, Identifier: task.Identifier);

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
