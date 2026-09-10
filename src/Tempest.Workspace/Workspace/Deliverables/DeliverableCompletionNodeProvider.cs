using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Deliverables;

/// <summary>
/// Populates the Project Explorer's own Deliverables area: project →
/// deliverable → completion (`WP 19.0A`, `ADR-0150`). Mirrors
/// <c>Evidence.EvidenceNodeProvider</c>'s own "rooted at every live
/// Project, grouped by a value that is not itself a structural parent"
/// shape — grouped by the completed <c>Deliverable</c>'s own id.
/// </summary>
public sealed class DeliverableCompletionNodeProvider : IProjectExplorerNodeProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="DeliverableCompletionNodeProvider"/> class.</summary>
    public DeliverableCompletionNodeProvider(string kind, EngineeringDomainContext context)
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
        var projects = await LiveProjectsAsync(cancellationToken).ConfigureAwait(false);

        var nodes = new List<ProjectExplorerNode>();
        foreach (var project in projects.OrderBy(DisplayNameOf, StringComparer.Ordinal))
            nodes.Add(await ToProjectNodeAsync(project, cancellationToken).ConfigureAwait(false));

        return nodes;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Deliverables node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var candidate = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (candidate is not null && string.Equals(candidate.Kind, MechanicalObjectFactoryRegistry.Project, StringComparison.Ordinal) && IsLive(candidate))
        {
            var underProject = await LiveCompletionsUnderProjectAsync(nodeId, cancellationToken).ConfigureAwait(false);

            var groupNodes = new List<ProjectExplorerNode>();
            foreach (var deliverableId in underProject.Select(c => c.DeliverableId).Distinct())
            {
                var deliverable = await _context.Repository.FindAsync(deliverableId, cancellationToken).ConfigureAwait(false);
                var title = deliverable is IHasBusinessIdentifier identity ? identity.DisplayName : deliverableId.ToString();
                groupNodes.Add(new ProjectExplorerNode(GroupNodeId(nodeId, deliverableId), title, null, true, ProjectExplorerNodeType.Category));
            }

            return groupNodes;
        }

        var projects = await LiveProjectsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var project in projects)
        {
            var underProject = await LiveCompletionsUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false);

            foreach (var deliverableId in underProject.Select(c => c.DeliverableId).Distinct())
            {
                if (GroupNodeId(project.Id, deliverableId) != nodeId)
                    continue;

                return underProject
                    .Where(c => c.DeliverableId == deliverableId)
                    .OrderByDescending(c => c.CompletedOn)
                    .Select(ToCompletionNode)
                    .ToList();
            }
        }

        throw new ArgumentException($"'{nodeId}' is not a known Deliverables node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s own ancestry, root first — the project, then (for a completion) its own deliverable group.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false) is not DeliverableCompletion completion)
            return [];

        var ancestry = new List<ProjectExplorerNode>();

        if (completion.ParentId is { } projectId
            && await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project)
        {
            ancestry.Add(await ToProjectNodeAsync(project, cancellationToken).ConfigureAwait(false));

            var deliverable = await _context.Repository.FindAsync(completion.DeliverableId, cancellationToken).ConfigureAwait(false);
            var title = deliverable is IHasBusinessIdentifier identity ? identity.DisplayName : completion.DeliverableId.ToString();
            ancestry.Add(new ProjectExplorerNode(GroupNodeId(projectId, completion.DeliverableId), title, null, true, ProjectExplorerNodeType.Category));
        }

        return ancestry;
    }

    private async Task<List<IEngineeringObject>> LiveProjectsAsync(CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
        .Where(IsLive)
        .ToList();

    private async Task<List<DeliverableCompletion>> LiveCompletionsUnderProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false))
        .OfType<DeliverableCompletion>()
        .Where(IsLive)
        .ToList();

    private async Task<ProjectExplorerNode> ToProjectNodeAsync(IEngineeringObject project, CancellationToken cancellationToken)
    {
        var hasCompletions = (await LiveCompletionsUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false)).Count > 0;
        return new ProjectExplorerNode(
            project.Id, DisplayNameOf(project), project.Kind, hasCompletions, ProjectExplorerNodeType.Object,
            Identifier: (project as IHasBusinessIdentifier)?.Identifier);
    }

    private static ProjectExplorerNode ToCompletionNode(DeliverableCompletion completion) =>
        new(completion.Id, $"Completed {completion.CompletedOn:yyyy-MM-dd}", completion.Kind, false, ProjectExplorerNodeType.Object, Identifier: completion.Identifier);

    /// <summary>A deterministic, non-persisted node id for <paramref name="deliverableId"/>'s own group under <paramref name="projectId"/>.</summary>
    private static Guid GroupNodeId(Guid projectId, Guid deliverableId)
    {
        var projectBytes = projectId.ToByteArray();
        var deliverableBytes = deliverableId.ToByteArray();
        var combined = new byte[16];

        for (var i = 0; i < 16; i++)
            combined[i] = (byte)(projectBytes[i] ^ deliverableBytes[i]);

        return new Guid(combined);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
