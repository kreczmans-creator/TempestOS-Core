using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Disciplines;

/// <summary>
/// Populates one discipline's Project Explorer area (`ADR-0163`): a folder
/// per Kind, each listing that Kind's live objects that are not nested
/// under another object of the same discipline; a nested object lists
/// under its parent instead.
/// </summary>
public sealed class DisciplineNodeProvider : IProjectExplorerNodeProvider
{
    private readonly DisciplineDefinition _definition;
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="DisciplineNodeProvider"/> class.</summary>
    public DisciplineNodeProvider(DisciplineDefinition definition, EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        _definition = definition;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind => _definition.NavigationItemId;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var objects = await LiveObjectsAsync(cancellationToken).ConfigureAwait(false);
        var ids = objects.Select(o => o.Id).ToHashSet();

        return
        [
            .. _definition.Kinds.Select(kind => new ProjectExplorerNode(
                kind.FolderNodeId, kind.Folder, null,
                objects.Any(o => IsFolderMember(o, kind, ids)), ProjectExplorerNodeType.Category)),
        ];
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> is neither one of this discipline's folders nor one of its objects.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var folder = _definition.Kinds.FirstOrDefault(k => k.FolderNodeId == nodeId);
        if (folder is not null)
        {
            var objects = await LiveObjectsAsync(cancellationToken).ConfigureAwait(false);
            var ids = objects.Select(o => o.Id).ToHashSet();
            return await ToNodesAsync(objects.Where(o => IsFolderMember(o, folder, ids)), cancellationToken).ConfigureAwait(false);
        }

        var target = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (target is not null && _definition.Find(target.Kind) is not null)
        {
            var childEntries = await _context.Repository.ListChildrenAsync(nodeId, cancellationToken).ConfigureAwait(false);
            var children = await _context.Repository.MaterialiseAsync<IEngineeringObject>(
                [.. childEntries.Where(entry => !entry.IsDeleted)], cancellationToken).ConfigureAwait(false);
            return await ToNodesAsync(children, cancellationToken).ConfigureAwait(false);
        }

        throw new ArgumentException($"'{nodeId}' is not a known {_definition.Category} node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s parent chain, root first — the Explorer's breadcrumb source.</summary>
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

    private static bool IsFolderMember(IEngineeringObject o, DisciplineKind folder, HashSet<Guid> liveIds) =>
        string.Equals(o.Kind, folder.Kind, StringComparison.Ordinal)
        && (o is not IHasParent { ParentId: { } parentId } || !liveIds.Contains(parentId));

    private async Task<IReadOnlyList<IEngineeringObject>> LiveObjectsAsync(CancellationToken cancellationToken)
    {
        var liveEntries = new List<EngineeringObjectIndexEntry>();
        foreach (var kind in _definition.KindNames)
        {
            var entries = await _context.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false);
            liveEntries.AddRange(entries.Where(entry => !entry.IsDeleted));
        }

        return await _context.Repository.MaterialiseAsync<IEngineeringObject>(liveEntries, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ProjectExplorerNode>> ToNodesAsync(IEnumerable<IEngineeringObject> objects, CancellationToken cancellationToken)
    {
        var nodes = new List<ProjectExplorerNode>();
        foreach (var o in objects)
            nodes.Add(await ToNodeAsync(o, cancellationToken).ConfigureAwait(false));

        return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
    }

    private async Task<ProjectExplorerNode> ToNodeAsync(IEngineeringObject o, CancellationToken cancellationToken)
    {
        var hasChildren = (await _context.Repository.ListChildrenAsync(o.Id, cancellationToken).ConfigureAwait(false)).Any(entry => !entry.IsDeleted);

        return new ProjectExplorerNode(
            o.Id, (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString(), o.Kind, hasChildren, ProjectExplorerNodeType.Object,
            o is IHasLifecycle lifecycle ? lifecycle.Status : null,
            (o as IHasBusinessIdentifier)?.Identifier);
    }
}
