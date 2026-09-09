using System.Collections.Concurrent;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The in-memory cache of committed engineering objects (ADR-0145: populated
/// from committed state only, never a co-equal writer), with a by-parent
/// index (`WP 17.9.3`) so "the children of X" is a lookup rather than a scan
/// of every object.
/// </summary>
/// <remarks>
/// <para>
/// The design-freeze review of 2026-09-08 found (hazard H4) that every tree
/// provider and the delete guard answered "children of X" by copying the
/// whole repository and filtering in memory, once per rendered node: cost
/// on the order of nodes × objects, and the delete guard paid it while
/// holding the domain write lock.
/// </para>
/// <para>
/// <b>The index is maintained by the two events that change a parent</b>:
/// <see cref="Register"/> (creation, revision and rehydration all register
/// the instance with its parent already set) and <see cref="ParentChanged"/>,
/// which <c>EngineeringObjectBase.MoveAsync</c> raises after commit, in the
/// same lock hold as the state. ADR-0081 makes <c>MoveAsync</c> the only
/// mutator of <c>ParentId</c>. <b>The index is also self-healing</b>: a
/// lookup returns only objects whose live <c>ParentId</c> still equals the
/// key, so a stale entry can never produce a wrong child, only a wasted
/// comparison.
/// </para>
/// </remarks>
public sealed class InMemoryEngineeringObjectRepository : IEngineeringObjectRepository
{
    private readonly ConcurrentDictionary<Guid, IEngineeringObject> _objectsById = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> _childrenByParent = new();
    private readonly ConcurrentDictionary<Guid, Guid> _indexedParentOf = new();

    public void Register(IEngineeringObject engineeringObject)
    {
        ArgumentNullException.ThrowIfNull(engineeringObject);
        _objectsById[engineeringObject.Id] = engineeringObject;

        var parentId = (engineeringObject as IHasParent)?.ParentId;
        Reindex(engineeringObject.Id, parentId);
    }

    public void ParentChanged(Guid objectId, Guid? newParentId) => Reindex(objectId, newParentId);

    public Task<IEngineeringObject?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _objectsById.TryGetValue(id, out var found);
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<IEngineeringObject>> ListByKindAsync(string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        IReadOnlyList<IEngineeringObject> matches = _objectsById.Values
            .Where(o => string.Equals(o.Kind, kind, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<IEngineeringObject>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IEngineeringObject> all = _objectsById.Values.ToList();
        return Task.FromResult(all);
    }

    public Task<IReadOnlyList<IEngineeringObject>> ListChildrenAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        if (!_childrenByParent.TryGetValue(parentId, out var childIds))
            return Task.FromResult<IReadOnlyList<IEngineeringObject>>([]);

        var children = new List<IEngineeringObject>(childIds.Count);
        foreach (var childId in childIds.Keys)
        {
            // Self-healing: only an object whose live parent is still this
            // one counts, whatever the index says.
            if (_objectsById.TryGetValue(childId, out var child) && child is IHasParent { ParentId: { } pid } && pid == parentId)
                children.Add(child);
        }

        return Task.FromResult<IReadOnlyList<IEngineeringObject>>(children);
    }

    private void Reindex(Guid objectId, Guid? parentId)
    {
        if (_indexedParentOf.TryGetValue(objectId, out var previous) && previous != parentId
            && _childrenByParent.TryGetValue(previous, out var previousChildren))
        {
            previousChildren.TryRemove(objectId, out _);
        }

        if (parentId is { } pid)
        {
            _childrenByParent.GetOrAdd(pid, _ => new ConcurrentDictionary<Guid, byte>())[objectId] = 0;
            _indexedParentOf[objectId] = pid;
        }
        else
        {
            _indexedParentOf.TryRemove(objectId, out _);
        }
    }
}
