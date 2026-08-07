using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Manufacturing;

/// <summary>
/// Populates the Project Explorer's own Manufacturing area from the real
/// Engineering Domain — the sixth <see cref="IProjectExplorerNodeProvider"/>
/// backed by a real Engineering discipline. Root nodes: one synthetic,
/// read-only category node per <see cref="ManufacturingCategory"/> label
/// (mirrors <see cref="Documents.DocumentsNodeProvider"/>'s own category
/// precedent exactly), each containing every live, un-parented
/// Manufacturing object that falls into it, across all three Manufacturing
/// Kinds together (<c>"ManufacturingOperation"</c>/<c>"WorkInstruction"</c>/
/// <c>"Inspection"</c>). A Routing's own real <see cref="IHasParent"/>
/// children (its own sequenced Operation steps, `ADR-0091`) are reachable
/// by drilling into the Routing itself, exactly like
/// <see cref="Documents.DocumentsNodeProvider"/>'s own established
/// real-parent nesting.
/// </summary>
public sealed class ManufacturingNodeProvider : IProjectExplorerNodeProvider
{
    private static readonly IReadOnlyDictionary<string, Guid> CategoryNodeIds = new Dictionary<string, Guid>(StringComparer.Ordinal)
    {
        ["Routings"] = new("00000000-0000-4003-8000-000000000001"),
        ["Operations"] = new("00000000-0000-4003-8000-000000000002"),
        ["Supplier Operations"] = new("00000000-0000-4003-8000-000000000003"),
        ["Work Instructions"] = new("00000000-0000-4003-8000-000000000004"),
        ["Inspections"] = new("00000000-0000-4003-8000-000000000005"),
    };

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ManufacturingNodeProvider"/> class.</summary>
    /// <param name="kind">The top-level area this provider populates.</param>
    public ManufacturingNodeProvider(string kind, EngineeringDomainContext context)
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
        var objects = await LiveManufacturingObjectsAsync(cancellationToken).ConfigureAwait(false);
        var byCategory = objects.ToLookup(ManufacturingCategory.Of);

        var nodes = new List<ProjectExplorerNode>();
        foreach (var label in ManufacturingCategory.Labels)
        {
            var count = byCategory[label].Count(o => o is IHasParent { ParentId: null });
            nodes.Add(new(CategoryNodeIds[label], label, null, count > 0, ProjectExplorerNodeType.Category));
        }

        return nodes;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Manufacturing node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var categoryLabel = CategoryNodeIds.FirstOrDefault(kv => kv.Value == nodeId).Key;
        if (categoryLabel is not null)
        {
            var objects = await LiveManufacturingObjectsAsync(cancellationToken).ConfigureAwait(false);
            var members = objects.Where(o => ManufacturingCategory.Of(o) == categoryLabel && o is IHasParent { ParentId: null });

            var nodes = new List<ProjectExplorerNode>();
            foreach (var member in members)
                nodes.Add(await ToManufacturingNodeAsync(member, cancellationToken).ConfigureAwait(false));

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        var target = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (target is IManufacturingOperation or IWorkInstruction || (target is IEngineeringObject e && string.Equals(e.Kind, "Inspection", StringComparison.Ordinal)))
        {
            var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
            var children = all.Where(o => IsLive(o) && o is IHasParent { ParentId: { } parentId } && parentId == nodeId);

            var nodes = new List<ProjectExplorerNode>();
            foreach (var child in children)
                nodes.Add(await ToManufacturingNodeAsync(child, cancellationToken).ConfigureAwait(false));

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        throw new ArgumentException($"'{nodeId}' is not a known Manufacturing node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s own <see cref="IHasParent.ParentId"/> chain, root first — the Explorer's own breadcrumb source, mirroring every other real discipline's own identical, additive convenience shape.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var ancestry = new List<ProjectExplorerNode>();
        var current = await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false);

        while (current is IHasParent { ParentId: { } parentId })
        {
            var parent = await _context.Repository.FindAsync(parentId, cancellationToken).ConfigureAwait(false);
            if (parent is null)
                break;

            ancestry.Insert(0, await ToManufacturingNodeAsync(parent, cancellationToken).ConfigureAwait(false));
            current = parent;
        }

        return ancestry;
    }

    private async Task<IReadOnlyList<IEngineeringObject>> LiveManufacturingObjectsAsync(CancellationToken cancellationToken)
    {
        var all = new List<IEngineeringObject>();
        foreach (var kind in ManufacturingObjectFactoryRegistry.SupportedKinds)
            all.AddRange(await _context.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false));

        return all.Where(IsLive).ToList();
    }

    private async Task<ProjectExplorerNode> ToManufacturingNodeAsync(IEngineeringObject manufacturingObject, CancellationToken cancellationToken)
    {
        var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var hasChildren = all.Any(o => IsLive(o) && o is IHasParent { ParentId: { } parentId } && parentId == manufacturingObject.Id);

        return new ProjectExplorerNode(manufacturingObject.Id, DisplayNameOf(manufacturingObject), manufacturingObject.Kind, hasChildren, ProjectExplorerNodeType.Object);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}

/// <summary>
/// Maps a live Manufacturing Domain object onto one of
/// <see cref="Labels"/> — <c>"WorkInstruction"</c>/<c>"Inspection"</c> map
/// from their own real, distinct Kind directly; a plain
/// <c>"ManufacturingOperation"</c> maps from its own
/// <see cref="IHasMetadata.Classification"/>
/// (<see cref="ManufacturingObjectFactoryRegistry.Routing"/>/
/// <see cref="ManufacturingObjectFactoryRegistry.Operation"/>/
/// <see cref="ManufacturingObjectFactoryRegistry.SupplierOperation"/>,
/// `ADR-0091`); anything else — an unset or unrecognised Classification —
/// falls into <c>"Operations"</c>, honestly, never silently dropped.
/// </summary>
public static class ManufacturingCategory
{
    public static readonly IReadOnlyList<string> Labels = ["Routings", "Operations", "Supplier Operations", "Work Instructions", "Inspections"];

    public static string Of(IEngineeringObject manufacturingObject) => manufacturingObject switch
    {
        IWorkInstruction => "Work Instructions",
        _ when string.Equals(manufacturingObject.Kind, "Inspection", StringComparison.Ordinal) => "Inspections",
        IHasMetadata { Classification: ManufacturingObjectFactoryRegistry.Routing } => "Routings",
        IHasMetadata { Classification: ManufacturingObjectFactoryRegistry.SupplierOperation } => "Supplier Operations",
        _ => "Operations",
    };
}
