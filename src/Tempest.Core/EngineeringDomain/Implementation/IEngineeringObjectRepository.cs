namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// An in-memory, Kind-queryable index over engineering objects — the
/// "In-memory repositories" deliverable named by WP 8.2C. Not proposed by
/// WP8.2B (its own Dependency Rules §8 explicitly left registration/indexing
/// to a future implementation Work Package) and never a replacement for
/// <see cref="EngineeringData.IEngineeringDocumentStore"/>, which remains the
/// sole source of durable document/revision/relationship truth per
/// ADR-0072 — this repository only adds the by-Kind lookup that store
/// cannot offer.
/// </summary>
/// <remarks>
/// <b>The index is the list (`TD-88`, `WP 21.5B`).</b>
/// <see cref="ListAllAsync"/>/<see cref="ListByKindAsync"/>/<see cref="ListChildrenAsync"/>
/// answer from <see cref="EngineeringObjectIndexEntry"/> rows — every field
/// obtainable with no document read — rather than fully materialised
/// objects. An entry materialises its full <see cref="IEngineeringObject"/>
/// on first access through <see cref="FindAsync"/>, the one loader every
/// other read (a typed read, a navigation) goes through; a materialised
/// object stays in this repository's identity map for the rest of the
/// session (one instance per id — the supersession guards depend on it).
/// See <see cref="RegisterLazy"/> for the registration half of this
/// contract, and <see cref="EngineeringDomain.Implementation.EngineeringObjectRehydrationService"/>
/// for the only caller that uses it today.
/// </remarks>
public interface IEngineeringObjectRepository
{
    /// <summary>
    /// Registers <paramref name="engineeringObject"/> as already
    /// materialised — creation, revision, and eager (project-scope, or
    /// kill-switch) rehydration all call this with a fully constructed
    /// instance. Replaces any lazy registration this id may have had.
    /// </summary>
    void Register(IEngineeringObject engineeringObject);

    /// <summary>
    /// Registers <paramref name="entry"/>'s id without materialising it
    /// (`TD-88`, `WP 21.5B`) — every list operation can already answer
    /// "what/where/kind/status" for this id from <paramref name="entry"/>
    /// alone. <paramref name="materialise"/> is invoked at most once for
    /// this id, the first time <see cref="FindAsync"/> is asked for it:
    /// concurrent first reads share the one in-flight call (single-flight)
    /// and see the same resulting instance, which is then registered in
    /// this repository's identity map exactly as <see cref="Register"/>
    /// would have. A materialisation that throws is treated the same way
    /// startup rehydration already treats an object it cannot bring back —
    /// swallowed, never surfaced as an exception to a reader — and simply
    /// leaves <see cref="FindAsync"/> answering <see langword="null"/> for
    /// this id, exactly as an unknown id already does.
    /// </summary>
    void RegisterLazy(EngineeringObjectIndexEntry entry, Func<CancellationToken, Task<IEngineeringObject>> materialise);

    /// <summary>
    /// The one materialising loader (`TD-88`, `WP 21.5B`): an id registered
    /// lazily (see <see cref="RegisterLazy"/>) materialises on this call,
    /// once, no matter how many callers ask concurrently; an id already
    /// materialised — created this session, or read before — returns the
    /// exact same instance every time (the identity map); an id never
    /// registered at all — lazily or otherwise — answers
    /// <see langword="null"/>, exactly as it always has.
    /// </summary>
    Task<IEngineeringObject?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered object of <paramref name="kind"/>'s own index row,
    /// in registration order (`TD-27`) — from the index alone, no
    /// materialisation (`TD-88`, `WP 21.5B`). A caller that needs full
    /// state materialises each id of interest through <see cref="FindAsync"/>.
    /// </summary>
    Task<IReadOnlyList<EngineeringObjectIndexEntry>> ListByKindAsync(string kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered object's own index row, in registration order
    /// (`TD-27`) — from the index alone, no materialisation (`TD-88`,
    /// `WP 21.5B`).
    /// </summary>
    Task<IReadOnlyList<EngineeringObjectIndexEntry>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered object whose live <see cref="EngineeringObjectIndexEntry.ParentId"/>
    /// is <paramref name="parentId"/>, deleted or not (`WP 17.9.3`): an
    /// indexed lookup, never a scan of every object — from the index
    /// alone, no materialisation (`TD-88`, `WP 21.5B`). Callers filter
    /// liveness as they always did.
    /// </summary>
    Task<IReadOnlyList<EngineeringObjectIndexEntry>> ListChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The current index row for <paramref name="id"/> — synchronous, no
    /// materialisation, no document read (`TD-88`, `WP 21.5B`): the seam
    /// for a caller that only needs an id's own Kind, parent, status or
    /// liveness, most commonly to keep walking a structural chain (a
    /// project-membership walk, most directly) without paying to
    /// materialise every object it merely passes through on the way.
    /// <see langword="null"/> if this id has never been registered at all
    /// — lazily or otherwise.
    /// </summary>
    EngineeringObjectIndexEntry? PeekIndexEntry(Guid id);

    /// <summary>
    /// Materialises every object whose index row's structural parent chain
    /// (walked through this same index, no document read per hop) reaches
    /// <paramref name="rootId"/>, <paramref name="rootId"/> included if it
    /// is itself registered — the project-open eager path (`WP 21.5B`
    /// Scope #2): "the objects a user is about to touch". A no-op for an id
    /// with no registered descendants. Materialising twice (a project
    /// re-opened) costs nothing beyond the walk itself — every id already
    /// materialised is skipped.
    /// </summary>
    Task MaterialiseSubtreeAsync(Guid rootId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The kill-switch escape hatch (`WP 21.5B`): materialises every
    /// registered object — lazy entries included — and returns the full,
    /// fully-materialised set, exactly like the pre-`WP 21.5B` eager
    /// <c>ListAllAsync</c> always did. For the caller classes this Work
    /// Package could not prove behaviourally identical against the index
    /// alone in the time available (named in its report) — an explicit,
    /// visible fallback, never an implicit one.
    /// </summary>
    Task<IReadOnlyList<IEngineeringObject>> MaterialiseAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells the cache that <paramref name="objectId"/>'s parent is now
    /// <paramref name="newParentId"/> (`WP 17.9.3`). Raised by the one mutator
    /// of a parent, <c>MoveAsync</c>, after its transaction commits and inside
    /// the same lock hold as the state it applies.
    /// </summary>
    void ParentChanged(Guid objectId, Guid? newParentId);
}

/// <summary>
/// One object's index-stage row (`TD-88`, `WP 20.1C2`, `WP 21.5B`) —
/// everything a tree, a lookup, or a business-identifier index needs before
/// — or instead of — full materialisation: the fields obtainable with no
/// document read, whether the source is a freshly-read
/// <see cref="EngineeringObjectState"/> (rehydration) or an already-live
/// <see cref="IEngineeringObject"/> (creation, revision).
/// </summary>
/// <param name="Id">The object's own identity.</param>
/// <param name="Kind">The object's own Kind.</param>
/// <param name="Identifier">The business identifier, or <see langword="null"/> if the object never had one.</param>
/// <param name="DisplayName">The current display name.</param>
/// <param name="ParentId">The raw structural parent edge — not a resolved project id; a consumer that needs project membership walks the edge itself.</param>
/// <param name="Status">The current lifecycle state.</param>
/// <param name="IsDeleted">Whether the object has been soft-deleted.</param>
public sealed record EngineeringObjectIndexEntry(
    Guid Id,
    string Kind,
    string? Identifier,
    string DisplayName,
    Guid? ParentId,
    LifecycleState Status,
    bool IsDeleted);
