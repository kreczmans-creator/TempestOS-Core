using Tempest.Core.Evidence;
using Tempest.Core.EngineeringDomain;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Evidence;

/// <summary>
/// Populates the Project Explorer's own Evidence area: project →
/// classification group → evidence, titles carrying <see cref="EvidenceStatus"/>
/// (`ADR-0148`). Mirrors <see cref="MechanicalProductStructureNodeProvider"/>'s
/// own "rooted at every live Project" shape, and
/// <see cref="Verification.VerificationActivityNodeProvider"/>'s own
/// synthetic category-node grouping — combined, because evidence is both
/// project-scoped (its own <see cref="IHasParent.ParentId"/> is the project,
/// via <c>CreationPlacement</c>) and grouped by a value that is not itself a
/// structural parent.
/// </summary>
public sealed class EvidenceNodeProvider : IProjectExplorerNodeProvider
{
    private static readonly IReadOnlyList<EvidenceClassification> Classifications =
        [EvidenceClassification.Calculation, EvidenceClassification.Drawing, EvidenceClassification.Report, EvidenceClassification.Test, EvidenceClassification.Other];

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="EvidenceNodeProvider"/> class.</summary>
    /// <param name="kind">The top-level area this provider populates.</param>
    public EvidenceNodeProvider(string kind, EngineeringDomainContext context)
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
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Evidence node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var candidate = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (candidate is not null && string.Equals(candidate.Kind, MechanicalObjectFactoryRegistry.Project, StringComparison.Ordinal) && IsLive(candidate))
        {
            var underProject = await LiveEvidenceUnderProjectAsync(nodeId, cancellationToken).ConfigureAwait(false);

            var groupNodes = new List<ProjectExplorerNode>();
            foreach (var classification in Classifications)
            {
                var count = underProject.Count(e => e.Classification == classification);
                groupNodes.Add(new ProjectExplorerNode(GroupNodeId(nodeId, classification), classification.ToString(), null, count > 0, ProjectExplorerNodeType.Category));
            }

            return groupNodes;
        }

        // Not a project — try every live project's own five group ids
        // (`GroupNodeId` is a deterministic, non-persisted derivation, so
        // there is nothing to look up but everything to recompute).
        var projects = await LiveProjectsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var project in projects)
        {
            foreach (var classification in Classifications)
            {
                if (GroupNodeId(project.Id, classification) != nodeId)
                    continue;

                var members = (await LiveEvidenceUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false))
                    .Where(e => e.Classification == classification)
                    .OrderBy(e => e.DisplayName, StringComparer.Ordinal);

                return members.Select(ToEvidenceNode).ToList();
            }
        }

        throw new ArgumentException($"'{nodeId}' is not a known Evidence node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s own ancestry, root first — the project, then (for an evidence record) its own classification group.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false) is not Core.Evidence.Evidence evidence)
            return [];

        var ancestry = new List<ProjectExplorerNode>();

        if (evidence.ParentId is { } projectId
            && await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is { } project)
        {
            ancestry.Add(await ToProjectNodeAsync(project, cancellationToken).ConfigureAwait(false));
            ancestry.Add(new ProjectExplorerNode(GroupNodeId(projectId, evidence.Classification), evidence.Classification.ToString(), null, true, ProjectExplorerNodeType.Category));
        }

        return ancestry;
    }

    private async Task<List<IEngineeringObject>> LiveProjectsAsync(CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
        .Where(IsLive)
        .ToList();

    private async Task<List<Core.Evidence.Evidence>> LiveEvidenceUnderProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(Core.Evidence.Evidence.CanonicalKind, cancellationToken).ConfigureAwait(false))
        .OfType<Core.Evidence.Evidence>()
        .Where(e => IsLive(e) && e.ParentId == projectId)
        .ToList();

    private async Task<ProjectExplorerNode> ToProjectNodeAsync(IEngineeringObject project, CancellationToken cancellationToken)
    {
        var hasEvidence = (await LiveEvidenceUnderProjectAsync(project.Id, cancellationToken).ConfigureAwait(false)).Count > 0;
        return new ProjectExplorerNode(project.Id, DisplayNameOf(project), project.Kind, hasEvidence, ProjectExplorerNodeType.Object);
    }

    private static ProjectExplorerNode ToEvidenceNode(Core.Evidence.Evidence evidence) =>
        new(evidence.Id, $"{evidence.DisplayName} ({evidence.Status})", evidence.Kind, false, ProjectExplorerNodeType.Object);

    /// <summary>
    /// A deterministic, non-persisted node id for <paramref name="classification"/>'s own group under <paramref name="projectId"/> —
    /// varies the project id's own first byte by the classification's ordinal, so a reload derives the identical id without storing a mapping.
    /// </summary>
    private static Guid GroupNodeId(Guid projectId, EvidenceClassification classification)
    {
        var bytes = projectId.ToByteArray();
        bytes[0] = (byte)(bytes[0] ^ (byte)classification ^ 0b1001_0110);
        return new Guid(bytes);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
