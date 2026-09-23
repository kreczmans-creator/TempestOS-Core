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
/// <see cref="Register"/>/<see cref="RegisterLazy"/> (creation, revision and
/// rehydration all register an id with its parent already known) and
/// <see cref="ParentChanged"/>, which <c>EngineeringObjectBase.MoveAsync</c>
/// raises after commit, in the same lock hold as the state. ADR-0081 makes
/// <c>MoveAsync</c> the only mutator of <c>ParentId</c>. <b>The index is
/// also self-healing</b>: a lookup returns only objects whose live parent
/// still equals the key, so a stale entry can never produce a wrong child,
/// only a wasted comparison.
/// </para>
/// <para>
/// <b>Iteration order (`TD-27`).</b> Every list method below returns
/// objects in <i>registration order</i> — the order an id was first
/// registered, lazily or not — rather than the unspecified bucket order a
/// bare <see cref="ConcurrentDictionary{TKey,TValue}"/> enumeration would
/// give. A later revision, materialisation, or a rehydration re-registering
/// an id already seen keeps that id's original position; the list never
/// reshuffles between renders just because an object was revised or
/// materialised. The order is tracked in <see cref="_registrationOrder"/>/
/// <see cref="_registrationIndex"/>, both guarded by <see cref="_sync"/> —
/// the same lock every registration and reindex takes — so a concurrent
/// registration assigns each id exactly one position, never two, never
/// none.
/// </para>
/// <para>
/// <b>`TD-88`/`WP 21.5B` — the index is the list, lazily materialised.</b>
/// An id can be registered two ways: <see cref="Register"/> (already a live
/// <see cref="IEngineeringObject"/> — creation, revision, eager
/// materialisation) or <see cref="RegisterLazy"/> (an index row and a
/// deferred materialiser — most of rehydration). <see cref="ListAllAsync"/>/
/// <see cref="ListByKindAsync"/>/<see cref="ListChildrenAsync"/> answer from
/// whichever <see cref="EngineeringObjectIndexEntry"/> is current for each
/// id — computed live from the materialised object when one exists (so a
/// rename, a lifecycle transition, or a delete is reflected immediately;
/// nothing here ever caches a value that can go stale under a live object),
/// or from the row <see cref="RegisterLazy"/> was given otherwise, since an
/// unmaterialised object cannot have changed since rehydration read its
/// state. <see cref="FindAsync"/> is the one seam that turns a lazy row
/// into a live object, once, no matter how many callers ask concurrently
/// (single-flight via <see cref="_inFlight"/>), and the result joins
/// <see cref="_objectsById"/> — the identity map — permanently: this
/// repository evicts nothing (`WP 21.5B` ships no eviction).
/// </para>
/// </remarks>
public sealed class InMemoryEngineeringObjectRepository : IEngineeringObjectRepository
{
    private readonly object _sync = new();
    private readonly ConcurrentDictionary<Guid, IEngineeringObject> _objectsById = new();
    private readonly ConcurrentDictionary<Guid, EngineeringObjectIndexEntry> _lazyIndexById = new();
    private readonly ConcurrentDictionary<Guid, Func<CancellationToken, Task<IEngineeringObject>>> _materialisers = new();
    private readonly ConcurrentDictionary<Guid, Lazy<Task<IEngineeringObject?>>> _inFlight = new();
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

            // A live registration always wins over — and clears — any
            // earlier lazy one: this id is no longer a row waiting to be
            // materialised, it *is* materialised.
            _materialisers.TryRemove(engineeringObject.Id, out _);
            _inFlight.TryRemove(engineeringObject.Id, out _);

            RecordRegistrationOrder(engineeringObject.Id);

            var parentId = (engineeringObject as IHasParent)?.ParentId;
            Reindex(engineeringObject.Id, parentId);
        }
    }

    public void RegisterLazy(EngineeringObjectIndexEntry entry, Func<CancellationToken, Task<IEngineeringObject>> materialise)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(materialise);

        lock (_sync)
        {
            // Never downgrade a live object back to a lazy row: a live
            // instance may carry mutations this call knows nothing about
            // (the same reason `EngineeringObjectRehydrationService` never
            // re-registers an id it finds already live).
            if (_objectsById.ContainsKey(entry.Id))
                return;

            _lazyIndexById[entry.Id] = entry;
            _materialisers[entry.Id] = materialise;
            _inFlight.TryRemove(entry.Id, out _);

            RecordRegistrationOrder(entry.Id);
            Reindex(entry.Id, entry.ParentId);
        }
    }

    /// <summary>Must be called under <see cref="_sync"/>.</summary>
    private void RecordRegistrationOrder(Guid id)
    {
        // First registration only: a revision, a re-materialisation, or a
        // rehydration re-registering an id already seen keeps its original
        // position rather than moving to the back of the order.
        if (!_registrationIndex.ContainsKey(id))
        {
            _registrationIndex[id] = _registrationOrder.Count;
            _registrationOrder.Add(id);
        }
    }

    public void ParentChanged(Guid objectId, Guid? newParentId)
    {
        lock (_sync)
        {
            Reindex(objectId, newParentId);
        }
    }

    /// <summary>
    /// The materialising loader (`TD-88`, `WP 21.5B`) — see the class
    /// remarks.
    /// </summary>
    public Task<IEngineeringObject?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_objectsById.TryGetValue(id, out var live))
            return Task.FromResult<IEngineeringObject?>(live);

        if (!_materialisers.ContainsKey(id))
            return Task.FromResult<IEngineeringObject?>(null);

        return AwaitMaterialisationAsync(id, cancellationToken);
    }

    private async Task<IEngineeringObject?> AwaitMaterialisationAsync(Guid id, CancellationToken cancellationToken)
    {
        // Single-flight: `Lazy<T>` with `ExecutionAndPublication` runs its
        // factory at most once even under concurrent first callers, and
        // every caller — this one included — awaits the very same `Task`,
        // so they all see the same resulting instance (or the same `null`)
        // no matter how many arrived before materialisation finished.
        var lazy = _inFlight.GetOrAdd(id, key => new Lazy<Task<IEngineeringObject?>>(
            () => MaterialiseAsync(key),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the one registered materialiser for <paramref name="id"/>
    /// (`TD-88`, `WP 21.5B`) and promotes the result into the identity map.
    /// Invoked at most once per id — see <see cref="AwaitMaterialisationAsync"/>
    /// — with its own <see cref="CancellationToken.None"/>, deliberately:
    /// the work is shared by every concurrent caller, so no single caller's
    /// cancellation may abort it out from under the others; each caller's
    /// own token instead governs only its own wait, in
    /// <see cref="AwaitMaterialisationAsync"/>.
    /// </summary>
    private async Task<IEngineeringObject?> MaterialiseAsync(Guid id)
    {
        if (!_materialisers.TryGetValue(id, out var materialise))
        {
            // Lost a race with another path that already resolved this id
            // (e.g. an explicit `Register` for the same id arriving between
            // `FindAsync`'s two checks) — answer whatever is now live, or
            // `null` if genuinely nothing is.
            return _objectsById.TryGetValue(id, out var already) ? already : null;
        }

        IEngineeringObject instance;
        try
        {
            instance = await materialise(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never surfaced to a reader (`TD-60`'s established discipline
            // for read paths, applied here instead of at startup): a lazy
            // row that cannot be brought back behaves exactly like an
            // object rehydration never even registered — `FindAsync`
            // answers `null` for it, permanently (the source state on disk
            // has not changed since, so a retry could only fail the same
            // way).
            _materialisers.TryRemove(id, out _);
            return null;
        }

        lock (_sync)
        {
            _objectsById[id] = instance;
        }

        _materialisers.TryRemove(id, out _);
        return instance;
    }

    /// <summary>
    /// Every registered object of <paramref name="kind"/>'s own index row,
    /// in registration order (`TD-27`) — see the class remarks.
    /// </summary>
    public Task<IReadOnlyList<EngineeringObjectIndexEntry>> ListByKindAsync(string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        IReadOnlyList<EngineeringObjectIndexEntry> matches = InRegistrationOrder(e => string.Equals(e.Kind, kind, StringComparison.Ordinal));
        return Task.FromResult(matches);
    }

    /// <summary>
    /// Every registered object's own index row, in registration order
    /// (`TD-27`) — see the class remarks.
    /// </summary>
    public Task<IReadOnlyList<EngineeringObjectIndexEntry>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EngineeringObjectIndexEntry> all = InRegistrationOrder(static _ => true);
        return Task.FromResult(all);
    }

    /// <summary>
    /// Every registered object whose live parent is <paramref name="parentId"/>,
    /// deleted or not (`WP 17.9.3`): an indexed lookup, never a scan of
    /// every object. Callers filter liveness as they always did. Returned
    /// in registration order (`TD-27`) — see the class remarks; the
    /// ordering step sorts only the children found, so it costs nothing
    /// beyond the indexed lookup itself.
    /// </summary>
    public Task<IReadOnlyList<EngineeringObjectIndexEntry>> ListChildrenAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        if (!_childrenByParent.TryGetValue(parentId, out var childIds))
            return Task.FromResult<IReadOnlyList<EngineeringObjectIndexEntry>>([]);

        var children = new List<EngineeringObjectIndexEntry>(childIds.Count);
        foreach (var childId in childIds.Keys)
        {
            // Self-healing: only an id whose live parent is still this one
            // counts, whatever the structural index says.
            if (EntryFor(childId) is { ParentId: { } pid } entry && pid == parentId)
                children.Add(entry);
        }

        lock (_sync)
        {
            children.Sort((a, b) => RegistrationIndexOf(a.Id).CompareTo(RegistrationIndexOf(b.Id)));
        }

        return Task.FromResult<IReadOnlyList<EngineeringObjectIndexEntry>>(children);
    }

    /// <summary>
    /// Materialises <paramref name="rootId"/>, if registered, and every
    /// descendant reachable from it through the structural (parent) index —
    /// the project-open eager path (`WP 21.5B` Scope #2). Breadth-first over
    /// <see cref="_childrenByParent"/>, which is already maintained
    /// regardless of whether each id is lazy or live, so this never needs a
    /// document read merely to discover the shape of the subtree — only to
    /// materialise what it finds.
    /// </summary>
    public async Task MaterialiseSubtreeAsync(Guid rootId, CancellationToken cancellationToken = default)
    {
        var toVisit = new Queue<Guid>();
        var visited = new HashSet<Guid> { rootId };
        toVisit.Enqueue(rootId);

        while (toVisit.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = toVisit.Dequeue();

            if (EntryFor(id) is not null)
                await FindAsync(id, cancellationToken).ConfigureAwait(false);

            foreach (var childId in ChildIdsOf(id))
            {
                if (visited.Add(childId))
                    toVisit.Enqueue(childId);
            }
        }
    }

    /// <summary>
    /// The kill-switch escape hatch (`WP 21.5B`) — see the interface
    /// remarks. Materialises every registered id, in registration order
    /// (`TD-27`), and returns the ones that came back — exactly what the
    /// pre-`WP 21.5B` eager <c>ListAllAsync</c> always returned, and in the
    /// same order.
    /// </summary>
    public async Task<IReadOnlyList<IEngineeringObject>> MaterialiseAllAsync(CancellationToken cancellationToken = default)
    {
        List<Guid> ids;
        lock (_sync)
        {
            ids = [.. _registrationOrder];
        }

        var result = new List<IEngineeringObject>(ids.Count);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await FindAsync(id, cancellationToken).ConfigureAwait(false) is { } obj)
                result.Add(obj);
        }

        return result;
    }

    /// <inheritdoc />
    public EngineeringObjectIndexEntry? PeekIndexEntry(Guid id) => EntryFor(id);

    private IReadOnlyCollection<Guid> ChildIdsOf(Guid parentId) =>
        _childrenByParent.TryGetValue(parentId, out var childIds) ? (IReadOnlyCollection<Guid>)childIds.Keys : [];

    /// <summary>
    /// The current index row for <paramref name="id"/>, or <see langword="null"/>
    /// if this id has never been registered at all. Computed live from the
    /// materialised object when one exists — so it can never go stale under
    /// a rename, a lifecycle transition, or a delete — otherwise from the
    /// row <see cref="RegisterLazy"/> was given, which cannot itself have
    /// gone stale: nothing can mutate an object that has never been
    /// materialised.
    /// </summary>
    private EngineeringObjectIndexEntry? EntryFor(Guid id)
    {
        if (_objectsById.TryGetValue(id, out var live))
            return BuildIndexEntry(live);

        return _lazyIndexById.TryGetValue(id, out var cached) ? cached : null;
    }

    /// <summary>
    /// Projects a live object to its current index row, the same shape
    /// <see cref="EngineeringDomain.Implementation.EngineeringObjectRehydrationService"/>
    /// projects an <see cref="EngineeringObjectState"/> to. <see cref="IHasBusinessIdentifier"/>,
    /// <see cref="IHasLifecycle"/>, <see cref="IHasParent"/> and
    /// <see cref="IDeletable"/> are each optional facets, exactly as the
    /// rest of this repository already treats <see cref="IHasParent"/> —
    /// an object that implements none of them (a minimal test double, most
    /// commonly) still gets an honest row: its own
    /// <see cref="IEngineeringObject.BusinessIdentifier"/> as display name,
    /// no identifier, no parent, <see cref="LifecycleState.Draft"/>, not
    /// deleted.
    /// </summary>
    private static EngineeringObjectIndexEntry BuildIndexEntry(IEngineeringObject obj) => new(
        obj.Id,
        obj.Kind,
        (obj as IHasBusinessIdentifier)?.Identifier,
        (obj as IHasBusinessIdentifier)?.DisplayName ?? obj.BusinessIdentifier,
        (obj as IHasParent)?.ParentId,
        (obj as IHasLifecycle)?.Status ?? LifecycleState.Draft,
        (obj as IDeletable)?.IsDeleted ?? false);

    /// <summary>
    /// Filters every registered id's current index row by
    /// <paramref name="predicate"/> and returns the matches in registration
    /// order. Snapshotted under <see cref="_sync"/> so a concurrent
    /// registration can never be observed as a torn, half-applied position.
    /// </summary>
    private List<EngineeringObjectIndexEntry> InRegistrationOrder(Func<EngineeringObjectIndexEntry, bool> predicate)
    {
        List<Guid> order;
        lock (_sync)
        {
            order = [.. _registrationOrder];
        }

        var result = new List<EngineeringObjectIndexEntry>(order.Count);
        foreach (var id in order)
        {
            if (EntryFor(id) is { } entry && predicate(entry))
                result.Add(entry);
        }

        return result;
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
