using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Populates the Project Explorer's own Calculations area from the real
/// Engineering Domain and <see cref="CalculationTemplateRegistry"/> — the
/// third <see cref="IProjectExplorerNodeProvider"/> backed by a real
/// Engineering discipline, after Mechanical's (`WP 9.0A`) and
/// Requirements' (`WP 9.1A`). Root nodes: a synthetic, read-only
/// <c>"Templates"</c> category node (children = every registered
/// <see cref="CalculationTemplateDescriptor"/> — Templates have no Domain
/// identity, only a registry-local Id), every live <c>"CalculationSet"</c>,
/// and every live, un-parented <c>"Calculation"</c>
/// (<see cref="IHasParent.ParentId"/> <see langword="null"/>). A Calculation
/// that is both a Set member and independently parented can appear under
/// both — the same multi-parent tree overlap
/// <see cref="Requirements.RequirementsNodeProvider"/> already establishes
/// for Requirement Collections, never a new tree concept.
/// </summary>
public sealed class CalculationsNodeProvider : IProjectExplorerNodeProvider
{
    /// <summary>The synthetic, stable node Id of the read-only "Templates" category node.</summary>
    public static readonly Guid TemplatesNodeId = new("00000000-0000-4000-8000-000000000001");

    private readonly EngineeringDomainContext _context;
    private readonly CalculationTemplateRegistry _templateRegistry;

    /// <summary>Initialises a new instance of the <see cref="CalculationsNodeProvider"/> class.</summary>
    /// <param name="kind">The top-level area this provider populates.</param>
    public CalculationsNodeProvider(string kind, EngineeringDomainContext context, CalculationTemplateRegistry templateRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(templateRegistry);

        Kind = kind;
        _context = context;
        _templateRegistry = templateRegistry;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var nodes = new List<ProjectExplorerNode>
        {
            new(TemplatesNodeId, "Templates", null, _templateRegistry.Templates.Count > 0, ProjectExplorerNodeType.Category),
        };

        var sets = await _context.Repository.ListByKindAsync("CalculationSet", cancellationToken).ConfigureAwait(false);
        foreach (var set in sets.Where(IsLive).OfType<ICalculationSet>().OrderBy(DisplayNameOf, StringComparer.Ordinal))
            nodes.Add(ToSetNode(set));

        var calculations = await _context.Repository.ListByKindAsync("Calculation", cancellationToken).ConfigureAwait(false);
        foreach (var calculation in calculations.Where(o => IsLive(o) && o is IHasParent { ParentId: null }).OrderBy(DisplayNameOf, StringComparer.Ordinal))
            nodes.Add(await ToCalculationNodeAsync(calculation, cancellationToken).ConfigureAwait(false));

        return nodes;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Calculations node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        if (nodeId == TemplatesNodeId)
        {
            return _templateRegistry.Templates
                .OrderBy(t => t.Metadata.Name, StringComparer.Ordinal)
                .Select(t => new ProjectExplorerNode(t.NodeId, t.Metadata.Name, "CalculationTemplate", false, ProjectExplorerNodeType.Object))
                .ToList();
        }

        if (_templateRegistry.FindByNodeId(nodeId) is not null)
            return []; // A Template node is always a leaf.

        var target = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (target is ICalculationSet set)
        {
            var nodes = new List<ProjectExplorerNode>();
            foreach (var memberId in set.MemberCalculationIds)
            {
                var member = await _context.Repository.FindAsync(memberId, cancellationToken).ConfigureAwait(false);
                if (member is not null && IsLive(member))
                    nodes.Add(await ToCalculationNodeAsync(member, cancellationToken).ConfigureAwait(false));
            }

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        if (target is ICalculation)
        {
            var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
            var children = all.Where(o => IsLive(o) && o is IHasParent { ParentId: { } parentId } && parentId == nodeId);

            var nodes = new List<ProjectExplorerNode>();
            foreach (var child in children)
                nodes.Add(await ToCalculationNodeAsync(child, cancellationToken).ConfigureAwait(false));

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        throw new ArgumentException($"'{nodeId}' is not a known Calculations node.", nameof(nodeId));
    }

    /// <summary>
    /// Walks <paramref name="objectId"/>'s own <see cref="IHasParent.ParentId"/>
    /// chain, root first — the Explorer's own breadcrumb source, mirroring
    /// <see cref="Mechanical.MechanicalProductStructureNodeProvider.GetAncestryAsync"/>'s
    /// own identical, additive convenience shape. A Template or a
    /// Set-membership edge is never part of this chain — only real
    /// <see cref="IHasParent"/> nesting between Calculations.
    /// </summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var ancestry = new List<ProjectExplorerNode>();
        var current = await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false);

        while (current is IHasParent { ParentId: { } parentId })
        {
            var parent = await _context.Repository.FindAsync(parentId, cancellationToken).ConfigureAwait(false);
            if (parent is null)
                break;

            ancestry.Insert(0, await ToCalculationNodeAsync(parent, cancellationToken).ConfigureAwait(false));
            current = parent;
        }

        return ancestry;
    }

    private static ProjectExplorerNode ToSetNode(ICalculationSet set)
    {
        var node = (IEngineeringObject)set;
        return new ProjectExplorerNode(node.Id, DisplayNameOf(node), node.Kind, set.MemberCalculationIds.Count > 0, ProjectExplorerNodeType.Collection);
    }

    private async Task<ProjectExplorerNode> ToCalculationNodeAsync(IEngineeringObject calculation, CancellationToken cancellationToken)
    {
        var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var hasChildren = all.Any(o => IsLive(o) && o is IHasParent { ParentId: { } parentId } && parentId == calculation.Id);

        return new ProjectExplorerNode(calculation.Id, DisplayNameOf(calculation), calculation.Kind, hasChildren, ProjectExplorerNodeType.Object, calculation is IHasLifecycle lifecycle ? lifecycle.Status : null);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
