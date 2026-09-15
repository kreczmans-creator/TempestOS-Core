using Tempest.Core.EngineeringDomain;
using Tempest.Core.PurchaseOrders;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.PurchaseOrders;

/// <summary>
/// Populates the Project Explorer's own Purchase orders area: project →
/// status group → order (`WP 21.3B`). Mirrors
/// <c>Quotations.QuotationNodeProvider</c>'s own "rooted at every live
/// Project, grouped by a value that is not itself a structural parent"
/// shape — grouped by <see cref="PurchaseOrderStatus"/>.
/// </summary>
public sealed class PurchaseOrderNodeProvider : IProjectExplorerNodeProvider
{
    private static readonly IReadOnlyList<PurchaseOrderStatus> Statuses =
        [PurchaseOrderStatus.Draft, PurchaseOrderStatus.Issued, PurchaseOrderStatus.Received, PurchaseOrderStatus.Closed, PurchaseOrderStatus.Cancelled];

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrderNodeProvider"/> class.</summary>
    public PurchaseOrderNodeProvider(string kind, EngineeringDomainContext context)
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
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Purchase orders node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var candidate = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (candidate is not null && string.Equals(candidate.Kind, MechanicalObjectFactoryRegistry.Project, StringComparison.Ordinal) && IsLive(candidate))
        {
            var underProject = await LiveOrdersUnderProjectAsync(nodeId, cancellationToken).ConfigureAwait(false);

            var groupNodes = new List<ProjectExplorerNode>();
            foreach (var status in Statuses)
            {
                var count = underProject.Count(o => o.Status == status);
                groupNodes.Add(new ProjectExplorerNode(GroupNodeId(nodeId, status), status.ToString(), null, count > 0, ProjectExplorerNodeType.Category));
            }

            return groupNodes;
        }

        var projects = await LiveProjectsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var project in projects)
        {
            foreach (var status in Statuses)
            {
                if (GroupNodeId(project.Id, status) != nodeId)
                    continue;

                var members = (await LiveOrdersUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false))
                    .Where(o => o.Status == status)
                    .OrderByDescending(o => o.Reference, StringComparer.Ordinal);

                return members.Select(ToOrderNode).ToList();
            }
        }

        throw new ArgumentException($"'{nodeId}' is not a known Purchase orders node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s own ancestry, root first — the project, then (for an order) its own status group.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false) is not PurchaseOrder order)
            return [];

        var ancestry = new List<ProjectExplorerNode>();

        if (order.ParentId is { } projectId
            && await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project)
        {
            ancestry.Add(await ToProjectNodeAsync(project, cancellationToken).ConfigureAwait(false));
            ancestry.Add(new ProjectExplorerNode(GroupNodeId(projectId, order.Status), order.Status.ToString(), null, true, ProjectExplorerNodeType.Category));
        }

        return ancestry;
    }

    private async Task<List<IEngineeringObject>> LiveProjectsAsync(CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
        .Where(IsLive)
        .ToList();

    private async Task<List<PurchaseOrder>> LiveOrdersUnderProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false))
        .OfType<PurchaseOrder>()
        .Where(IsLive)
        .ToList();

    private async Task<ProjectExplorerNode> ToProjectNodeAsync(IEngineeringObject project, CancellationToken cancellationToken)
    {
        var hasOrders = (await LiveOrdersUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false)).Count > 0;
        return new ProjectExplorerNode(
            project.Id, DisplayNameOf(project), project.Kind, hasOrders, ProjectExplorerNodeType.Object,
            Identifier: (project as IHasBusinessIdentifier)?.Identifier);
    }

    private static ProjectExplorerNode ToOrderNode(PurchaseOrder order) =>
        new(order.Id, $"{order.Reference} ({order.GrossTotal})", order.Kind, false, ProjectExplorerNodeType.Object, Identifier: order.Identifier);

    /// <summary>A deterministic, non-persisted node id for <paramref name="status"/>'s own group under <paramref name="projectId"/> — varies the project id's own first byte by the status's ordinal, mirroring <c>QuotationNodeProvider.GroupNodeId</c>'s own identical shape.</summary>
    private static Guid GroupNodeId(Guid projectId, PurchaseOrderStatus status)
    {
        var bytes = projectId.ToByteArray();
        bytes[0] = (byte)(bytes[0] ^ (byte)status ^ 0b0111_1001);
        return new Guid(bytes);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
