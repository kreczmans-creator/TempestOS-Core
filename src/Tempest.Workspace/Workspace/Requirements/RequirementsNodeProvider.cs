using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Populates the Project Explorer's own Requirements area from the real
/// Requirements Framework (`WP 7.3A`) — the second
/// <see cref="IProjectExplorerNodeProvider"/> backed by a real Engineering
/// discipline, after Mechanical's own (`ADR-0067`, `WP 9.0A`/`WP 9.1A`).
/// Rooted at every live <see cref="IRequirementCollection"/> and every live
/// root <see cref="IRequirementGroup"/> (<c>ParentGroupId is null</c>) — a
/// collection's own children are its own
/// <see cref="IRequirementCollection.MemberRequirementIds"/>; a group's own
/// children are its live sub-groups, plus every live requirement whose own
/// <see cref="IRequirement.GroupId"/> points to it. A requirement node is
/// always a leaf — requirements do not themselves contain other
/// requirements in this tree (parent/child *requirement* relationships are
/// a Digital Thread traceability concern, surfaced through the Property
/// Inspector, never a second competing tree structure here).
/// </summary>
/// <remarks>
/// Each node's own <see cref="ProjectExplorerNode.Kind"/> is the real
/// backing document Kind (<c>"Requirement"</c>/<c>"RequirementCollection"</c>/
/// <c>"RequirementGroup"</c>) — never this provider's own area
/// <see cref="Kind"/> — mirroring <c>MechanicalProductStructureNodeProvider</c>'s
/// own identical convention: it is this per-node Kind the Workspace uses to
/// route to the right <see cref="IWorkspaceViewFactory"/>/
/// <see cref="IPropertyFacetProvider"/> once a node is selected.
/// </remarks>
public sealed class RequirementsNodeProvider : IProjectExplorerNodeProvider
{
    private readonly IRequirementsService _requirementsService;

    /// <summary>Initialises a new instance of the <see cref="RequirementsNodeProvider"/> class.</summary>
    /// <param name="kind">The top-level area this provider populates.</param>
    public RequirementsNodeProvider(string kind, IRequirementsService requirementsService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(requirementsService);

        Kind = kind;
        _requirementsService = requirementsService;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _requirementsService.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);
        var groups = await _requirementsService.ListGroupsAsync(cancellationToken).ConfigureAwait(false);
        var requirements = await _requirementsService.ListAsync(cancellationToken).ConfigureAwait(false);

        var nodes = new List<ProjectExplorerNode>();

        foreach (var collection in collections.Where(c => !c.IsDeleted).OrderBy(c => c.Name, StringComparer.Ordinal))
            nodes.Add(ToCollectionNode(collection));

        foreach (var group in groups.Where(g => !g.IsDeleted && g.ParentGroupId is null).OrderBy(g => g.Name, StringComparer.Ordinal))
            nodes.Add(ToGroupNode(group, groups, requirements));

        return nodes;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Requirements node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        if (await _requirementsService.FindCollectionAsync(nodeId, cancellationToken).ConfigureAwait(false) is { } collection)
        {
            var nodes = new List<ProjectExplorerNode>();
            foreach (var requirementId in collection.MemberRequirementIds)
            {
                var requirement = await _requirementsService.FindAsync(requirementId, cancellationToken).ConfigureAwait(false);
                if (requirement is { IsDeleted: false })
                    nodes.Add(ToRequirementNode(requirement));
            }

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        if (await _requirementsService.FindGroupAsync(nodeId, cancellationToken).ConfigureAwait(false) is not null)
        {
            var groups = await _requirementsService.ListGroupsAsync(cancellationToken).ConfigureAwait(false);
            var requirements = await _requirementsService.ListAsync(cancellationToken).ConfigureAwait(false);

            var nodes = new List<ProjectExplorerNode>();
            foreach (var subGroup in groups.Where(g => !g.IsDeleted && g.ParentGroupId == nodeId))
                nodes.Add(ToGroupNode(subGroup, groups, requirements));

            foreach (var requirement in requirements.Where(r => !r.IsDeleted && r.GroupId == nodeId))
                nodes.Add(ToRequirementNode(requirement));

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        if (await _requirementsService.FindAsync(nodeId, cancellationToken).ConfigureAwait(false) is not null)
            return []; // A Requirement node is always a leaf.

        throw new ArgumentException($"'{nodeId}' is not a known Requirements node.", nameof(nodeId));
    }

    /// <summary>
    /// Walks <paramref name="objectId"/>'s own group chain, root first — the
    /// Explorer's own breadcrumb source, mirroring
    /// <c>MechanicalProductStructureNodeProvider.GetAncestryAsync</c>'s own
    /// identical, additive (not interface-declared) convenience shape.
    /// </summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var groups = await _requirementsService.ListGroupsAsync(cancellationToken).ConfigureAwait(false);
        var requirements = await _requirementsService.ListAsync(cancellationToken).ConfigureAwait(false);

        Guid? currentGroupId =
            requirements.FirstOrDefault(r => r.Id == objectId)?.GroupId
            ?? groups.FirstOrDefault(g => g.Id == objectId)?.ParentGroupId;

        var ancestry = new List<ProjectExplorerNode>();
        while (currentGroupId is { } groupId)
        {
            var group = groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null)
                break;

            ancestry.Insert(0, ToGroupNode(group, groups, requirements));
            currentGroupId = group.ParentGroupId;
        }

        return ancestry;
    }

    private ProjectExplorerNode ToCollectionNode(IRequirementCollection collection) =>
        new(collection.Id, collection.Name, RequirementsService.RequirementCollectionDocumentKind, collection.MemberRequirementIds.Count > 0, ProjectExplorerNodeType.Collection);

    private static ProjectExplorerNode ToGroupNode(IRequirementGroup group, IReadOnlyList<IRequirementGroup> allGroups, IReadOnlyList<IRequirement> allRequirements)
    {
        var hasChildren =
            allGroups.Any(g => !g.IsDeleted && g.ParentGroupId == group.Id) ||
            allRequirements.Any(r => !r.IsDeleted && r.GroupId == group.Id);

        return new ProjectExplorerNode(group.Id, group.Name, RequirementsService.RequirementGroupDocumentKind, hasChildren, ProjectExplorerNodeType.Group);
    }

    private static ProjectExplorerNode ToRequirementNode(IRequirement requirement) =>
        new(requirement.Id, BuildRequirementTitle(requirement), RequirementsService.RequirementDocumentKind, false, ProjectExplorerNodeType.Object);

    /// <summary>Builds a requirement node's own display title — <c>"&lt;Identifier&gt; — &lt;Statement, truncated&gt;"</c>. The Property Inspector remains the authoritative, complete source for the full statement.</summary>
    private static string BuildRequirementTitle(IRequirement requirement)
    {
        const int maxStatementLength = 60;
        var statement = requirement.Statement.Length > maxStatementLength
            ? string.Concat(requirement.Statement.AsSpan(0, maxStatementLength), "…")
            : requirement.Statement;

        return $"{requirement.Identifier} — {statement}";
    }
}
