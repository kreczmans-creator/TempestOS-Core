using Tempest.Core.Logging;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Rebuilds the live engineering object graph from the durable store at
/// startup (`TD-85`) — the step that turns "the documents survived" into
/// "the engineering work survived".
/// </summary>
/// <remarks>
/// <para>
/// <b>What this closes.</b> `ADR-0077` recorded the in-memory
/// <see cref="IEngineeringObjectRepository"/>/<see cref="IEngineeringRelationshipRepository"/>
/// as an indexing layer over a durable document store, and disclosed the
/// consequence: "the repository layer's own state... is not itself durable
/// — restarting the Host loses it, even though the underlying documents
/// themselves survive". This service is that disclosed future Work
/// Package.
/// </para>
/// <para>
/// <b>Not a second authority.</b> Nothing here stores anything. It reads
/// <see cref="IEngineeringObjectStateStore"/> and
/// <see cref="EngineeringData.IEngineeringDocumentStore"/> — the two halves
/// of the one persistence authority — and repopulates the two in-memory
/// indexes from them. Persistence stays authoritative; the indexes stay
/// derived.
/// </para>
/// <para>
/// <b>Not a type map.</b> This service knows no Kind and no concrete type.
/// It resolves each object's rehydrator from
/// <see cref="IEngineeringObjectRehydratorRegistry"/>, and each type
/// reconstructs itself.
/// </para>
/// <para>
/// <b>Partial failure is survivable.</b> A state record whose document has
/// gone, a Kind no discipline registered, or a single object that throws
/// on reconstruction, is counted and reported — never allowed to cost the
/// user every other object they own (`TD-60`'s established discipline for
/// read paths, applied to startup).
/// </para>
/// <para>
/// <b>`TD-88`/`WP 20.1C2` — index-first.</b> Every call builds
/// <see cref="EngineeringObjectIndexEntry"/> rows for the whole persisted
/// estate as its own first step, straight from
/// <see cref="EngineeringObjectState"/> (no document read, no rehydrator),
/// deterministically sorted by <see cref="EngineeringObjectState.Id"/>, and
/// raises <see cref="IndexBuilt"/> with them before touching a single
/// document. That is the seam `WP 20.1A2`'s business-identifier index
/// rebuild runs from — it needs exactly <see cref="EngineeringObjectState.Id"/>,
/// <see cref="EngineeringObjectState.Kind"/> and
/// <see cref="EngineeringObjectState.Identifier"/>, all present on the
/// index row, before a single object is fully materialised.
/// </para>
/// <para>
/// <b>`TD-88`/`WP 21.5B` — lazy, project-scoped materialisation.</b> This
/// call no longer reconstructs the estate unconditionally. For every state
/// whose Kind a discipline registered, it opens the backing document only
/// far enough to learn it exists (<see cref="EngineeringData.IEngineeringDocumentStore.FindAsync"/>
/// — a single lightweight document record, no revision content: `TD-88`'s
/// own measured cost is dominated by <see cref="EngineeringData.IEngineeringDocumentStore.GetRevisionHistoryAsync"/>'s
/// per-revision content reads and the rehydrator's own type-specific
/// parsing, both of which this defers), then registers the object lazily
/// (<see cref="IEngineeringObjectRepository.RegisterLazy"/>) with a
/// materialiser that does the rest — <c>GetRevisionHistoryAsync</c> and
/// <c>IEngineeringObjectRehydrator.Rehydrate</c> — on first access, once,
/// no matter how many callers ask concurrently. <b>Unknown-Kind and
/// orphaned-document detection stay exactly as accurate as before</b> —
/// both are still checked for the whole estate inside this one call, since
/// neither needs more than the cheap existence read; <b>reconstruction
/// failure detection moves to first access</b> — a state whose document
/// exists but whose <c>Rehydrate</c> throws is caught by the loader itself
/// (`TD-60`'s established discipline for read paths, applied lazily
/// instead of eagerly) and is indistinguishable, from that point on, from
/// an id nothing ever registered. <see cref="EngineeringRehydrationResult.FailedObjectIds"/>
/// is therefore always empty on the result this method returns — it
/// reports what this call could and did check eagerly, not what a later
/// caller's own first read might still discover.
/// </para>
/// <para>
/// <b>Project scope (`WP 21.5B` Scope #2).</b> This service itself opens
/// no project — "the objects a user is about to touch" is a Workspace-layer
/// concern (a Project is a Kind string this Core-layer class does not
/// know) — but the seam it hands that layer is
/// <see cref="IEngineeringObjectRepository.MaterialiseSubtreeAsync"/>,
/// called once a project's own id is known. See
/// <c>Tempest.Workspace.Projects.ProjectContext.OpenAsync</c> for the one
/// caller.
/// </para>
/// <para>
/// <b>The kill switch, invoked for one class this Work Package could not
/// prove in the time available.</b> `WP 20.1A2`'s business-identifier index
/// rebuild (<see cref="RebuildBusinessIdentifierIndexAsync"/>) reads each
/// enforced-Kind object's own <see cref="IEngineeringObject.BusinessIdentifier"/>
/// — a computed, type-specific projection that does not exist on the index
/// row — so it still materialises every live object of an enforced Kind
/// through the loader, eagerly, inside this call, exactly as `WP 20.1C2`
/// left it. This is bounded (the enforced-Kind set is a fraction of the
/// canonical vocabulary — Part, Calculation/CalculationSet,
/// Document/Drawing/CadModel, the three Manufacturing Kinds,
/// VerificationActivity, Evidence — never Task, InvoiceRequest,
/// Quotation, or the other unenforced Kinds a large estate is mostly made
/// of), not unconditional, but it is real, eager materialisation this
/// method still pays for every startup, independent of which project (if
/// any) is opened afterwards. `BusinessIdentifierScope.ResolveProjectId`'s
/// own synchronous, blocking read of the repository (documented there as
/// safe only because the repository was, until `WP 21.5B`, a pure in-memory
/// lookup) is a second, disclosed reason the enforced-Kind materialisation
/// stays eager rather than lazy: that method is outside this Work
/// Package's files-you-own list (the business-identifier index's contract
/// is explicitly not to be touched), and a lazily-materialising ancestor
/// reached through it would turn a documented-synchronous call into a
/// blocking one. In ordinary use this is moot — an enforced-Kind object's
/// ancestor chain is, by construction, inside whatever project a user has
/// open, and Scope #2 already materialises that subtree eagerly — but a
/// caller that creates or renames an enforced-Kind object without a
/// project ever having been opened (most directly, `Tempest.Core.Tests`
/// fixtures that build an `EngineeringDomainContext` straight over a
/// rehydrated repository) can still reach a lazy ancestor there, and pays
/// a synchronous materialisation to resolve it. Named here, with the two
/// files it touches, per the brief's own kill-switch clause.
/// </para>
/// </remarks>
public sealed class EngineeringObjectRehydrationService
{
    private readonly EngineeringDomainContext _context;
    private readonly IEngineeringObjectRehydratorRegistry _rehydrators;
    private readonly ILogger? _logger;

    /// <summary>
    /// Raised once per <see cref="RehydrateAsync"/> call, immediately after
    /// the whole estate's <see cref="EngineeringObjectIndexEntry"/> rows
    /// have been built and deterministically sorted, and before any
    /// document is read or any object is reconstructed (`TD-88`,
    /// `WP 20.1C2`). <c>WP 20.1A2</c>'s business-identifier index rebuild
    /// subscribes here so it can run from id/Kind/Identifier alone, without
    /// waiting for full materialisation of an estate it does not need.
    /// Never raised with a live subscriber count assumption — a call with
    /// nothing subscribed pays only the cost of building the list.
    /// </summary>
    public event Action<IReadOnlyList<EngineeringObjectIndexEntry>>? IndexBuilt;

    /// <summary>Initialises a new instance of the <see cref="EngineeringObjectRehydrationService"/> class.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="rehydrators"/> is <see langword="null"/>.</exception>
    public EngineeringObjectRehydrationService(
        EngineeringDomainContext context,
        IEngineeringObjectRehydratorRegistry rehydrators,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rehydrators);

        _context = context;
        _rehydrators = rehydrators;
        _logger = logger;
    }

    /// <summary>
    /// Reconstructs every persisted engineering object and every
    /// relationship between the objects reconstructed, registering them in
    /// the live repositories.
    /// </summary>
    /// <returns>A full account of what came back and what did not.</returns>
    public async Task<EngineeringRehydrationResult> RehydrateAsync(CancellationToken cancellationToken = default)
    {
        if (_context.ObjectStateStore is not { } stateStore)
        {
            _logger?.Warning("Engineering object rehydration skipped — no durable object state store is composed.");
            return EngineeringRehydrationResult.Empty;
        }

        // `TD-27`: `ListAsync` makes no ordering promise (it reads whatever
        // order the backing store's own collection scan returns), so the
        // registration order this loop feeds `_context.Repository.Register`
        // would otherwise vary run to run for the identical durable state.
        // Sorted by id, every rehydration of the same disk state registers
        // in the same order, every time.
        var states = (await stateStore.ListAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(state => state.Id)
            .ToList();

        // `TD-88`/`WP 20.1C2` — the index stage: every row the whole
        // estate can offer without a single document read, in the same
        // id order the rest of this method already commits to (`TD-27`).
        // Raised before materialisation starts, so a subscriber never
        // waits on the cost this class exists to eventually avoid paying
        // eagerly.
        var index = states.ConvertAll(BuildIndexEntry);
        var indexById = index.ToDictionary(entry => entry.Id);
        IndexBuilt?.Invoke(index);

        // `TD-88`/`WP 21.5B`: ids this call registered (lazily) for later
        // materialisation, and each one's own document-level `CreatedAt` —
        // `RebuildRelationshipsAsync` needs both: the id alone to read
        // references, and `CreatedAt` only as `TD-85`'s existing fallback
        // for a reference whose own `CreatedAt` was written before
        // provenance became durable. Recording it here, from the document
        // this loop already reads, is exactly what a materialised
        // `instance.CreatedAt` would have answered (`Document.CreatedAt`)
        // without forcing the deferred half of reconstruction just to ask.
        var registeredIds = new List<Guid>(states.Count);
        var createdAtById = new Dictionary<Guid, DateTimeOffset>(states.Count);
        var unknownKinds = new SortedSet<string>(StringComparer.Ordinal);
        var orphanedStateIds = new List<Guid>();
        var alreadyLiveCount = 0;

        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // An object already in the repository is the same object, live —
            // possibly with mutations not yet written. Replacing it with a
            // snapshot read from disk would silently discard them, so
            // rehydration never overwrites a live object; it only fills in
            // what this process does not already have. On a repository with
            // nothing lazy registered yet (the ordinary, single-pass
            // startup case) this is a plain dictionary miss for every id —
            // no materialisation is triggered by asking.
            if (await _context.Repository.FindAsync(state.Id, cancellationToken).ConfigureAwait(false) is not null)
            {
                alreadyLiveCount++;
                continue;
            }

            var rehydrator = _rehydrators.Find(state.Kind);
            if (rehydrator is null)
            {
                // An Error, not a Warning, and deliberately actionable: a
                // persisted object whose Kind nothing can rebuild is
                // durable engineering work this process cannot show the
                // user. Recovery continues — refusing to start would lose
                // everything else that *can* be recovered — but the
                // outcome is stated loudly, named, and reported back
                // through `UnknownKinds` so a caller can surface it
                // rather than leaving it in a log nobody reads.
                unknownKinds.Add(state.Kind);
                _logger?.Error(
                    $"Engineering object '{state.Id}' has Kind '{state.Kind}', which no discipline registered for " +
                    "rehydration — it was NOT reconstructed and is not visible in this session. Register a rehydrator " +
                    "for this Kind in its owning discipline registry, or in CanonicalObjectKinds if it has no " +
                    "discipline yet.");
                continue;
            }

            // `TD-88`/`WP 21.5B`: this is the one document read that stays
            // eager — a single lightweight document record (id, Kind,
            // current revision number, created-at; no revision content) —
            // because it is the only way to know an object is orphaned, and
            // that classification has always been reported the moment
            // rehydration returns. Everything past it (revision content,
            // type-specific parsing) is deferred to the lazy materialiser
            // below.
            var document = await _context.Store.FindAsync(state.Id, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                orphanedStateIds.Add(state.Id);
                _logger?.Warning($"Engineering object state '{state.Id}' has no backing document — it was not reconstructed.");
                continue;
            }

            _context.Repository.RegisterLazy(indexById[state.Id], ct => MaterialiseAsync(state, document, rehydrator, ct));
            registeredIds.Add(state.Id);
            createdAtById[state.Id] = document.CreatedAt;
        }

        var relationshipCount = await RebuildRelationshipsAsync(registeredIds, createdAtById, cancellationToken).ConfigureAwait(false);

        // `TD-38`: the business-identifier index is a pure projection of
        // the live object set, exactly like `Repository`/`RelationshipRepository`
        // above — never persisted, so it is rebuilt wholesale from every
        // object now registered (not only this call's own `registeredIds`,
        // so an already-live object contributes its own claim too) rather
        // than incrementally maintained across a restart. `WP 21.5B`'s own
        // kill switch: this still materialises every enforced-Kind object
        // eagerly — see the class remarks.
        await RebuildBusinessIdentifierIndexAsync(cancellationToken).ConfigureAwait(false);

        // `FailedObjectIds` is always empty here (`WP 21.5B`): a
        // reconstruction failure can only be discovered by actually calling
        // `Rehydrate`, which this method no longer does eagerly — see the
        // class remarks. The loader reports it the same way an orphan
        // already was, by leaving `FindAsync` answering `null`.
        var result = new EngineeringRehydrationResult(
            registeredIds.Count, relationshipCount, [.. unknownKinds], orphanedStateIds, [], alreadyLiveCount);

        _logger?.Information(
            $"Engineering rehydration complete: {result.ObjectCount} object(s), {result.RelationshipCount} relationship(s) restored.");

        return result;
    }

    /// <summary>
    /// Rebuilds `TD-38`'s business-identifier index from every live
    /// object <see cref="EngineeringDomainContext.Repository"/> now holds.
    /// A soft-deleted object claims nothing, matching the rule's own
    /// "live objects only" scope; a Kind
    /// <see cref="BusinessIdentifierScope.EnforcedKinds"/> does not name
    /// is skipped, exactly as <see cref="EngineeringObjectFactory{T}.CreateAsync(string, Guid?, CancellationToken)"/>
    /// and <see cref="EngineeringObjectBase.RenameAsync"/> skip it.
    /// </summary>
    private async Task RebuildBusinessIdentifierIndexAsync(CancellationToken cancellationToken)
    {
        _context.BusinessIdentifierIndex.Clear();

        // `TD-88`/`WP 21.5B` kill switch: `BusinessIdentifier` is a
        // computed, type-specific projection (a Part's own Name, a
        // Document's own Number-or-Name…) that does not exist on
        // `EngineeringObjectIndexEntry`, so every enforced-Kind row is
        // materialised here — eagerly, through the loader — before its
        // identifier can be read. See the class remarks for why this one
        // caller was not moved to the index type.
        var everyIndexRow = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var enforcedKindEntries = everyIndexRow.Where(entry => BusinessIdentifierScope.EnforcedKinds.Contains(entry.Kind)).ToList();
        var enforcedKindObjects = await _context.Repository.MaterialiseAsync<IEngineeringObject>(enforcedKindEntries, cancellationToken).ConfigureAwait(false);

        foreach (var candidate in enforcedKindObjects)
        {
            if (candidate is IDeletable { IsDeleted: true })
                continue;

            var parentId = (candidate as IHasParent)?.ParentId;
            var projectScopeId = BusinessIdentifierScope.ResolveProjectId(parentId, _context.Repository);

            _context.BusinessIdentifierIndex.Claim(candidate.Kind, projectScopeId, candidate.BusinessIdentifier, candidate.Id);
        }
    }

    /// <summary>
    /// Rebuilds the in-memory relationship index from the durable
    /// per-document reference collections the document store already
    /// owns — the edges themselves were always persisted; only the index
    /// over them was lost on restart. Reads references by id alone
    /// (`TD-88`/`WP 21.5B`: never needed a materialised object — only
    /// <see cref="EngineeringData.IEngineeringDocumentStore.GetReferencesAsync"/>'s
    /// own id-keyed lookup — so relationship rebuild stays fully eager for
    /// the whole estate, unchanged in cost and unaffected by lazy
    /// materialisation elsewhere).
    /// </summary>
    private async Task<int> RebuildRelationshipsAsync(
        IReadOnlyList<Guid> registeredIds, IReadOnlyDictionary<Guid, DateTimeOffset> createdAtById, CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var id in registeredIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var references = await _context.Store.GetReferencesAsync(id, cancellationToken).ConfigureAwait(false);

            foreach (var reference in references)
            {
                _context.RelationshipRepository.Record(new EngineeringRelationship(
                    reference.SourceDocumentId,
                    reference.TargetDocumentId,
                    reference.RelationshipKind,
                    RelationshipKindCategoryMap.InferCategory(reference.RelationshipKind),
                    // Provenance is durable from `TD-85` onward; a link
                    // written before it reads back as the document store's
                    // own "unknown" principal rather than being falsely
                    // attributed to the current one.
                    reference.CreatedByPrincipalId ?? EngineeringData.EngineeringDocumentStore.UnknownAuthorPrincipalId,
                    reference.CreatedAt ?? createdAtById[id]));

                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// The deferred half of reconstruction (`TD-88`, `WP 21.5B`) — revision
    /// content and the rehydrator's own type-specific parsing, run only
    /// once <see cref="IEngineeringObjectRepository.FindAsync"/> is
    /// actually asked for this id. <paramref name="document"/> was already
    /// read eagerly (the cheap existence check); everything else happens
    /// here, on whichever thread first asks.
    /// </summary>
    private async Task<IEngineeringObject> MaterialiseAsync(
        EngineeringObjectState state, EngineeringData.IEngineeringDocument document, IEngineeringObjectRehydrator rehydrator, CancellationToken cancellationToken)
    {
        var revisions = await _context.Store.GetRevisionHistoryAsync(state.Id, cancellationToken).ConfigureAwait(false);
        return rehydrator.Rehydrate(state, document, revisions[^1]);
    }

    /// <summary>
    /// Projects one persisted <see cref="EngineeringObjectState"/> to its
    /// index row — every field it carries with no document read (`TD-88`,
    /// `WP 20.1C2`). <see cref="EngineeringObjectIndexEntry.ParentId"/> is
    /// the raw structural edge, not a resolved project id: resolving "which
    /// project" from it is a Workspace-layer concern (a Project is a Kind
    /// string this Core-layer class does not know), so a consumer that
    /// needs project membership walks the edge itself.
    /// </summary>
    private static EngineeringObjectIndexEntry BuildIndexEntry(EngineeringObjectState state) => new(
        state.Id, state.Kind, state.Identifier, state.DisplayName, state.ParentId, state.Status, state.IsDeleted);
}

/// <summary>
/// What one startup rehydration actually recovered — and, just as
/// importantly, what it could not (`TD-85`).
/// </summary>
/// <param name="ObjectCount">
/// How many engineering objects this call registered — lazily,
/// `WP 21.5B` onward, so this counts objects known to exist and eligible
/// for reconstruction (a known Kind, a backing document found) rather than
/// objects this call actually reconstructed; see
/// <see cref="EngineeringObjectRehydrationService"/>'s own remarks.
/// </param>
/// <param name="RelationshipCount">How many relationships were re-indexed from durable references.</param>
/// <param name="UnknownKinds">Kinds found on disk that no discipline registered a rehydrator for.</param>
/// <param name="OrphanedStateIds">Objects whose state survived but whose backing document did not.</param>
/// <param name="FailedObjectIds">
/// Always empty on the result <see cref="EngineeringObjectRehydrationService.RehydrateAsync"/>
/// itself returns (`WP 21.5B`: reconstruction is deferred, so a failure
/// there cannot yet be known) — kept on this record for callers that
/// materialise the whole estate themselves (<see cref="IEngineeringObjectRepository.MaterialiseAllAsync"/>)
/// and want a place to report what came back empty-handed.
/// </param>
/// <param name="AlreadyLiveCount">Objects this process had already loaded, and which were therefore left exactly as they are.</param>
public sealed record EngineeringRehydrationResult(
    int ObjectCount,
    int RelationshipCount,
    IReadOnlyList<string> UnknownKinds,
    IReadOnlyList<Guid> OrphanedStateIds,
    IReadOnlyList<Guid> FailedObjectIds,
    int AlreadyLiveCount = 0)
{
    /// <summary>The result of a rehydration that had nothing to do.</summary>
    public static readonly EngineeringRehydrationResult Empty = new(0, 0, [], [], []);

    /// <summary>Whether every persisted object came back — <see langword="false"/> if anything was skipped for any reason.</summary>
    public bool IsComplete => UnknownKinds.Count == 0 && OrphanedStateIds.Count == 0 && FailedObjectIds.Count == 0;
}
