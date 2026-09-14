using Tempest.Core.EngineeringDomain;
using Tempest.Core.Quotations;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Quotations;

/// <summary>
/// Populates the Project Explorer's own Quotations area: project → status
/// group → quotation (`WP 19.5A`, `ADR-0152`). Mirrors
/// <c>Invoicing.InvoiceRequestNodeProvider</c>'s own "rooted at every live
/// Project, grouped by a value that is not itself a structural parent"
/// shape — grouped by <see cref="QuotationStatus"/>.
/// </summary>
public sealed class QuotationNodeProvider : IProjectExplorerNodeProvider
{
    // Written out, in QuotationStatus's own declaration order —
    // QuotationStatusTransitions.AllStatuses is internal to Tempest.Core
    // (InvoiceRequestStatusTransitions.AllStatuses is too; neither grants
    // Tempest.Workspace access), so this is this provider's own,
    // independent statement of the vocabulary it groups by.
    private static readonly IReadOnlyList<QuotationStatus> Statuses =
        [QuotationStatus.Draft, QuotationStatus.Sent, QuotationStatus.Accepted, QuotationStatus.Declined];

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="QuotationNodeProvider"/> class.</summary>
    public QuotationNodeProvider(string kind, EngineeringDomainContext context)
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
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Quotations node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var candidate = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (candidate is not null && string.Equals(candidate.Kind, MechanicalObjectFactoryRegistry.Project, StringComparison.Ordinal) && IsLive(candidate))
        {
            var underProject = await LiveQuotationsUnderProjectAsync(nodeId, cancellationToken).ConfigureAwait(false);

            var groupNodes = new List<ProjectExplorerNode>();
            foreach (var status in Statuses)
            {
                var count = underProject.Count(q => q.Status == status);
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

                var members = (await LiveQuotationsUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false))
                    .Where(q => q.Status == status)
                    .OrderByDescending(q => q.QuoteDate)
                    .ThenBy(q => q.DisplayName, StringComparer.Ordinal);

                return members.Select(ToQuotationNode).ToList();
            }
        }

        throw new ArgumentException($"'{nodeId}' is not a known Quotations node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s own ancestry, root first — the project, then (for a quotation) its own status group.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false) is not Quotation quotation)
            return [];

        var ancestry = new List<ProjectExplorerNode>();

        if (quotation.ParentId is { } projectId
            && await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project)
        {
            ancestry.Add(await ToProjectNodeAsync(project, cancellationToken).ConfigureAwait(false));
            ancestry.Add(new ProjectExplorerNode(GroupNodeId(projectId, quotation.Status), quotation.Status.ToString(), null, true, ProjectExplorerNodeType.Category));
        }

        return ancestry;
    }

    private async Task<List<IEngineeringObject>> LiveProjectsAsync(CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
        .Where(IsLive)
        .ToList();

    private async Task<List<Quotation>> LiveQuotationsUnderProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false))
        .OfType<Quotation>()
        .Where(IsLive)
        .ToList();

    private async Task<ProjectExplorerNode> ToProjectNodeAsync(IEngineeringObject project, CancellationToken cancellationToken)
    {
        var hasQuotations = (await LiveQuotationsUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false)).Count > 0;
        return new ProjectExplorerNode(
            project.Id, DisplayNameOf(project), project.Kind, hasQuotations, ProjectExplorerNodeType.Object,
            Identifier: (project as IHasBusinessIdentifier)?.Identifier);
    }

    private static ProjectExplorerNode ToQuotationNode(Quotation quotation) =>
        new(
            quotation.Id, $"{quotation.Reference} ({quotation.Total})", quotation.Kind, false, ProjectExplorerNodeType.Object,
            Identifier: quotation.Identifier);

    /// <summary>A deterministic, non-persisted node id for <paramref name="status"/>'s own group under <paramref name="projectId"/> — varies the project id's own first byte by the status's ordinal, so a reload derives the identical id without storing a mapping.</summary>
    private static Guid GroupNodeId(Guid projectId, QuotationStatus status)
    {
        var bytes = projectId.ToByteArray();
        bytes[0] = (byte)(bytes[0] ^ (byte)status ^ 0b0101_0011);
        return new Guid(bytes);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
