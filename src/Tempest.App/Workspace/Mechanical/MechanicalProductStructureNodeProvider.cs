using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Populates the Project Explorer's own Mechanical area from the real
/// Engineering Domain — the first <see cref="IProjectExplorerNodeProvider"/>
/// backed by <see cref="EngineeringDomainContext"/> rather than fixed
/// sample content (`ADR-0067`, `WP 9.0A`). Rooted at every live
/// <c>"Project"</c>; every other node's own parent/child edge comes from
/// <see cref="IHasParent.ParentId"/> — the one live pointer `WP 9.0A`
/// introduces — never from the frozen, construction-time-only
/// <c>IAssembly.ChildIds</c>/<c>ISubAssembly.ParentAssemblyId</c>. This is
/// the Bill of Materials view, not a separate one (`WP 9.0B`): a node's own
/// title is prefixed with its own Item Number when set, and a sibling group
/// is ordered by Item Number (numeric-aware) when every member has one —
/// the existing tree <em>is</em> the BOM hierarchy, never a second,
/// competing structure.
/// </summary>
public sealed class MechanicalProductStructureNodeProvider : IProjectExplorerNodeProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="MechanicalProductStructureNodeProvider"/> class.</summary>
    /// <param name="kind">The top-level area this provider populates.</param>
    public MechanicalProductStructureNodeProvider(string kind, EngineeringDomainContext context)
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
        var projects = await _context.Repository.ListByKindAsync("Project", cancellationToken).ConfigureAwait(false);

        var nodes = new List<ProjectExplorerNode>();
        foreach (var project in OrderForBom(projects.Where(IsLive)))
            nodes.Add(await ToNodeAsync(project, cancellationToken).ConfigureAwait(false));

        return nodes;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Mechanical Product Structure node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var parent = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (parent is null)
            throw new ArgumentException($"'{nodeId}' is not a known Mechanical Product Structure node.", nameof(nodeId));

        var children = await GetLiveChildrenAsync(nodeId, cancellationToken).ConfigureAwait(false);

        var nodes = new List<ProjectExplorerNode>();
        foreach (var child in OrderForBom(children))
            nodes.Add(await ToNodeAsync(child, cancellationToken).ConfigureAwait(false));

        return nodes;
    }

    /// <summary>
    /// Walks <paramref name="objectId"/>'s own parent chain, root first — the
    /// Explorer's own breadcrumb source. Needs no change to
    /// <see cref="NavigationService"/>/<see cref="IWorkspaceContext"/> (both
    /// frozen `WP8.0B` contracts): the ancestry walk lives here, next to the
    /// live <see cref="IHasParent.ParentId"/> data it reads.
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

            ancestry.Insert(0, await ToNodeAsync(parent, cancellationToken).ConfigureAwait(false));
            current = parent;
        }

        return ancestry;
    }

    private async Task<IReadOnlyList<IEngineeringObject>> GetLiveChildrenAsync(Guid parentId, CancellationToken cancellationToken)
    {
        var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);

        return all.Where(o => o is IHasParent { ParentId: { } pid } && pid == parentId && IsLive(o)).ToList();
    }

    private async Task<ProjectExplorerNode> ToNodeAsync(IEngineeringObject o, CancellationToken cancellationToken)
    {
        var children = await GetLiveChildrenAsync(o.Id, cancellationToken).ConfigureAwait(false);
        var title = BuildBomTitle(o);

        return new ProjectExplorerNode(o.Id, title, o.Kind, children.Count > 0, ProjectExplorerNodeType.Object, o is IHasLifecycle lifecycle ? lifecycle.Status : null);
    }

    /// <summary>
    /// Builds a node's own display title — the plain <c>DisplayName</c> for
    /// an object with no BOM line data yet, or, once one has been set
    /// (`WP 9.0B`), <c>"&lt;Item Number&gt; ×&lt;Quantity&gt; &lt;Name&gt;"</c>
    /// (Item Number omitted if unset). The Property Inspector remains the
    /// authoritative, complete facet source (Find Number, Unit of Measure,
    /// Reference Designator all shown there); this is a compact, at-a-glance
    /// tree label only.
    /// </summary>
    private static string BuildBomTitle(IEngineeringObject o)
    {
        var displayName = (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

        if (o is not IHasBomLine { } bomLine || (bomLine.ItemNumber is null && bomLine.Quantity == 1m))
            return displayName;

        var prefix = bomLine.ItemNumber is { } itemNumber ? $"{itemNumber} " : string.Empty;

        return $"{prefix}×{bomLine.Quantity.ToString("0.####")} {displayName}";
    }

    /// <summary>
    /// Orders a sibling group by <see cref="IHasBomLine.ItemNumber"/> when
    /// every member has one set, numerically where every Item Number
    /// parses as a number (the common BOM convention — 10, 20, 30, ...),
    /// lexically otherwise. Left in whatever order the repository itself
    /// returned them in — <em>not</em> a claimed insertion order;
    /// <c>InMemoryEngineeringObjectRepository</c>'s own backing
    /// <c>ConcurrentDictionary</c> makes no such guarantee — the moment any
    /// member has no Item Number, so a partially-numbered BOM is never
    /// reordered around the numbered subset, avoiding a confusing partial
    /// sort.
    /// </summary>
    private static IReadOnlyList<IEngineeringObject> OrderForBom(IEnumerable<IEngineeringObject> objects)
    {
        var list = objects.ToList();
        var itemNumbers = list.Select(o => (o as IHasBomLine)?.ItemNumber).ToList();

        if (itemNumbers.Any(n => n is null))
            return list;

        if (itemNumbers.All(n => decimal.TryParse(n, out _)))
            return list.OrderBy(o => decimal.Parse(((IHasBomLine)o).ItemNumber!)).ToList();

        return list.OrderBy(o => ((IHasBomLine)o).ItemNumber, StringComparer.Ordinal).ToList();
    }

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
