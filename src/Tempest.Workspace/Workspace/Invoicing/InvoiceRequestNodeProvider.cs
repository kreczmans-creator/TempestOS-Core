using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Invoicing;

/// <summary>
/// Populates the Project Explorer's own Invoicing area: project → status
/// group → request (`WP 19.1A`, `ADR-0151`). Mirrors
/// <c>Evidence.EvidenceNodeProvider</c>'s own "rooted at every live
/// Project, grouped by a value that is not itself a structural parent"
/// shape — grouped by <see cref="InvoiceRequestStatus"/>.
/// </summary>
public sealed class InvoiceRequestNodeProvider : IProjectExplorerNodeProvider
{
    // Written out, in InvoiceRequestStatus's own declaration order —
    // InvoiceRequestStatusTransitions.AllStatuses is internal to
    // Tempest.Core (Evidence.EvidenceStatusTransitions is too; neither
    // grants Tempest.Workspace access), so this is this provider's own,
    // independent statement of the vocabulary it groups by.
    private static readonly IReadOnlyList<InvoiceRequestStatus> Statuses =
    [
        InvoiceRequestStatus.Draft, InvoiceRequestStatus.Sending, InvoiceRequestStatus.Sent, InvoiceRequestStatus.Accepted,
        InvoiceRequestStatus.Rejected, InvoiceRequestStatus.Voided, InvoiceRequestStatus.Unknown,
        InvoiceRequestStatus.Reauthorise, InvoiceRequestStatus.Unavailable,
    ];

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="InvoiceRequestNodeProvider"/> class.</summary>
    public InvoiceRequestNodeProvider(string kind, EngineeringDomainContext context)
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
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Invoicing node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var candidate = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (candidate is not null && string.Equals(candidate.Kind, MechanicalObjectFactoryRegistry.Project, StringComparison.Ordinal) && IsLive(candidate))
        {
            var underProject = await LiveRequestsUnderProjectAsync(nodeId, cancellationToken).ConfigureAwait(false);

            var groupNodes = new List<ProjectExplorerNode>();
            foreach (var status in Statuses)
            {
                var count = underProject.Count(r => r.Status == status);
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

                var members = (await LiveRequestsUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false))
                    .Where(r => r.Status == status)
                    .OrderByDescending(r => r.SentAtUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(r => r.DisplayName, StringComparer.Ordinal);

                return members.Select(ToRequestNode).ToList();
            }
        }

        throw new ArgumentException($"'{nodeId}' is not a known Invoicing node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s own ancestry, root first — the project, then (for a request) its own status group.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false) is not InvoiceRequest request)
            return [];

        var ancestry = new List<ProjectExplorerNode>();

        if (request.ParentId is { } projectId
            && await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project)
        {
            ancestry.Add(await ToProjectNodeAsync(project, cancellationToken).ConfigureAwait(false));
            ancestry.Add(new ProjectExplorerNode(GroupNodeId(projectId, request.Status), request.Status.ToString(), null, true, ProjectExplorerNodeType.Category));
        }

        return ancestry;
    }

    private async Task<List<IEngineeringObject>> LiveProjectsAsync(CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
        .Where(IsLive)
        .ToList();

    private async Task<List<InvoiceRequest>> LiveRequestsUnderProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false))
        .OfType<InvoiceRequest>()
        .Where(IsLive)
        .ToList();

    private async Task<ProjectExplorerNode> ToProjectNodeAsync(IEngineeringObject project, CancellationToken cancellationToken)
    {
        var hasRequests = (await LiveRequestsUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false)).Count > 0;
        return new ProjectExplorerNode(
            project.Id, DisplayNameOf(project), project.Kind, hasRequests, ProjectExplorerNodeType.Object,
            Identifier: (project as IHasBusinessIdentifier)?.Identifier);
    }

    private static ProjectExplorerNode ToRequestNode(InvoiceRequest request) =>
        new(
            request.Id, $"{request.DisplayName} ({request.Total})", request.Kind, false, ProjectExplorerNodeType.Object,
            Identifier: request.Identifier);

    /// <summary>A deterministic, non-persisted node id for <paramref name="status"/>'s own group under <paramref name="projectId"/> — varies the project id's own first byte by the status's ordinal, so a reload derives the identical id without storing a mapping.</summary>
    private static Guid GroupNodeId(Guid projectId, InvoiceRequestStatus status)
    {
        var bytes = projectId.ToByteArray();
        bytes[0] = (byte)(bytes[0] ^ (byte)status ^ 0b0110_1001);
        return new Guid(bytes);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
