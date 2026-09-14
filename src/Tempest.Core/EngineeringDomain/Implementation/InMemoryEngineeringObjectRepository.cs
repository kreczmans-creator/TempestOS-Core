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
/// <para>
/// <b>Iteration order (`TD-27`).</b> Every list method below returns
/// objects in <i>registration order</i> — the order <see cref="Register"/>
/// first saw each object's id — rather than the unspecified bucket order a
/// bare <see cref="ConcurrentDictionary{TKey,TValue}"/> enumeration would
/// give. A later revision or a rehydration re-registering an id already
/// seen keeps that id's original position; the list never reshuffles
/// between renders just because an object was revised. The order is
/// tracked in <see cref="_registrationOrder"/>/<see cref="_registrationIndex"/>,
/// both guarded by <see cref="_sync"/> — the same lock <see cref="Register"/>
/// and <see cref="ParentChanged"/> take — so a concurrent registration
/// assigns each id exactly one position, never two, never none.
/// </para>
/// </remarks>
public sealed class InMemoryEngineeringObjectRepository : IEngineeringObjectRepository
{
    private readonly object _sync = new();
    private readonly ConcurrentDictionary<Guid, IEngineeringObject> _objectsById = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> _childrenByParent = new();
    private readonly ConcurrentDictionary<Guid, Guid> _indexedParentOf = new();

    // TD-27: registration order, maintained under `_sync` alongside the
    // writes it orders. `_registrationOrder` is the id sequence itself;
    // `_registrationIndex` is its inverse, so ListChildrenAsync can sort its
    // small (already-indexed) child set without a full-repository scan.
    private readonly List<Guid> _registrationOrder = [];
    private readonly Dictionary<Guid, int> _registrationIndex = [];

    public void Register(IEngineeringObject engineeringObject)
    {
        ArgumentNullException.ThrowIfNull(engineeringObject);

        lock (_sync)
        {
            _objectsById[engineeringObject.Id] = engineeringObject;

            // First registration only: a revision or a rehydration
            // re-registering an id already seen keeps its original
            // position rather than moving to the back of the order.
            if (!_registrationIndex.ContainsKey(engineeringObject.Id))
            {
                _registrationIndex[engineeringObject.Id] = _registrationOrder.Count;
                _registrationOrder.Add(engineeringObject.Id);
            }

            var parentId = (engineeringObject as IHasParent)?.ParentId;
            Reindex(engineeringObject.Id, parentId);
        }
    }

    public void ParentChanged(Guid objectId, Guid? newParentId)
    {
        lock (_sync)
        {
            Reindex(objectId, newParentId);
        }
    }

    public Task<IEngineeringObject?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _objectsById.TryGetValue(id, out var found);
        return Task.FromResult(found);
    }

    /// <summary>
    /// Every registered object of <paramref name="kind"/>, in registration
    /// order (`TD-27`) — see the class remarks.
    /// </summary>
    public Task<IReadOnlyList<IEngineeringObject>> ListByKindAsync(string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        IReadOnlyList<IEngineeringObject> matches = InRegistrationOrder(o => string.Equals(o.Kind, kind, StringComparison.Ordinal));
        return Task.FromResult(matches);
    }

    /// <summary>
    /// Every registered object, in registration order (`TD-27`) — see the
    /// class remarks.
    /// </summary>
    public Task<IReadOnlyList<IEngineeringObject>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IEngineeringObject> all = InRegistrationOrder(static _ => true);
        return Task.FromResult(all);
    }

    /// <summary>
    /// Every registered object whose live <see cref="IHasParent.ParentId"/>
    /// is <paramref name="parentId"/>, deleted or not (`WP 17.9.3`): an
    /// indexed lookup, never a scan of every object. Callers filter
    /// liveness as they always did. Returned in registration order
    /// (`TD-27`) — see the class remarks; the ordering step sorts only the
    /// children found, so it costs nothing beyond the indexed lookup
    /// itself.
    /// </summary>
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

        lock (_sync)
        {
            children.Sort((a, b) => RegistrationIndexOf(a.Id).CompareTo(RegistrationIndexOf(b.Id)));
        }

        return Task.FromResult<IReadOnlyList<IEngineeringObject>>(children);
    }

    /// <summary>
    /// Filters every registered object by <paramref name="predicate"/> and
    /// returns the matches in registration order. Snapshotted under
    /// <see cref="_sync"/> so a concurrent <see cref="Register"/> can never
    /// be observed as a torn, half-applied position.
    /// </summary>
    private List<IEngineeringObject> InRegistrationOrder(Func<IEngineeringObject, bool> predicate)
    {
        lock (_sync)
        {
            var result = new List<IEngineeringObject>(_registrationOrder.Count);
            foreach (var id in _registrationOrder)
            {
                if (_objectsById.TryGetValue(id, out var obj) && predicate(obj))
                    result.Add(obj);
            }

            return result;
        }
    }

    /// <summary>Must be called under <see cref="_sync"/>.</summary>
    private int RegistrationIndexOf(Guid id) => _registrationIndex.TryGetValue(id, out var index) ? index : int.MaxValue;

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
