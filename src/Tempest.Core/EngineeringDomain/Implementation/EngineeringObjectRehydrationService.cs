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
/// <b>`TD-88`/`WP 20.1C2` — index-first, not (yet) lazy.</b> Every call
/// still builds <see cref="EngineeringObjectIndexEntry"/> rows for the
/// whole persisted estate as its own first step, straight from
/// <see cref="EngineeringObjectState"/> (no document read, no rehydrator),
/// deterministically sorted by <see cref="EngineeringObjectState.Id"/>, and
/// raises <see cref="IndexBuilt"/> with them before touching a single
/// document. That is the seam `WP 20.1A2`'s business-identifier index
/// rebuild runs from — it needs exactly <see cref="EngineeringObjectState.Id"/>,
/// <see cref="EngineeringObjectState.Kind"/> and
/// <see cref="EngineeringObjectState.Identifier"/>, all present on the
/// index row, before a single object is fully materialised. <b>Full
/// materialisation stays eager</b> — every object below is still
/// reconstructed unconditionally in the same call, not deferred to
/// <c>FindAsync</c> or to <c>ProjectContext</c> opening a project. The
/// brief's own kill switch was invoked here: dozens of existing callers
/// (`EngineeringCockpit.PrimeAsync`'s `.OfType&lt;IRisk&gt;()`/`IDecision&gt;()`/etc.
/// projections, `InvoicingService.ListCarriedSourcesAsync`'s
/// `.OfType&lt;InvoiceRequest&gt;()` then `request.Lines`,
/// `MechanicalPropertyFacetProvider.GetBaselineDisplayAsync`'s
/// `.OfType&lt;IConfiguration&gt;()` then `c.MemberRevisions`, and roughly
/// sixty more sites across `Tempest.Core`/`Tempest.Workspace`/`Tempest.Desktop`)
/// read full, type-specific object state directly off
/// <c>IEngineeringObjectRepository.ListAllAsync</c>/<c>ListByKindAsync</c>/
/// <c>ListChildrenAsync</c> results with no intervening <c>FindAsync</c>,
/// and every one of those files sits outside this Work Package's "files you
/// own" list. Deferring materialisation behind those three methods without
/// touching those callers could not be proven behaviourally equivalent in
/// the time available, so `TD-88` (eager, unconditional full-estate
/// materialisation) remains open; this change ships only the index-first
/// structure, the hook, and the measurement the brief allows as the
/// reduced deliverable.
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
        IndexBuilt?.Invoke(index);

        var rehydrated = new List<IEngineeringObject>(states.Count);
        var unknownKinds = new SortedSet<string>(StringComparer.Ordinal);
        var orphanedStateIds = new List<Guid>();
        var failedObjectIds = new List<Guid>();
        var alreadyLiveCount = 0;

        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // An object already in the repository is the same object, live —
            // possibly with mutations not yet written. Replacing it with a
            // snapshot read from disk would silently discard them, so
            // rehydration never overwrites a live object; it only fills in
            // what this process does not already have.
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

            var document = await _context.Store.FindAsync(state.Id, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                orphanedStateIds.Add(state.Id);
                _logger?.Warning($"Engineering object state '{state.Id}' has no backing document — it was not reconstructed.");
                continue;
            }

            try
            {
                var revisions = await _context.Store.GetRevisionHistoryAsync(state.Id, cancellationToken).ConfigureAwait(false);
                var instance = rehydrator.Rehydrate(state, document, revisions[^1]);

                _context.Repository.Register(instance);
                rehydrated.Add(instance);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failedObjectIds.Add(state.Id);
                _logger?.Warning($"Engineering object '{state.Id}' (Kind '{state.Kind}') could not be reconstructed and was skipped.", ex);
            }
        }

        var relationshipCount = await RebuildRelationshipsAsync(rehydrated, cancellationToken).ConfigureAwait(false);

        var result = new EngineeringRehydrationResult(
            rehydrated.Count, relationshipCount, [.. unknownKinds], orphanedStateIds, failedObjectIds, alreadyLiveCount);

        _logger?.Information(
            $"Engineering rehydration complete: {result.ObjectCount} object(s), {result.RelationshipCount} relationship(s) restored.");

        return result;
    }

    /// <summary>
    /// Rebuilds the in-memory relationship index from the durable
    /// per-document reference collections the document store already
    /// owns — the edges themselves were always persisted; only the index
    /// over them was lost on restart.
    /// </summary>
    private async Task<int> RebuildRelationshipsAsync(IReadOnlyList<IEngineeringObject> rehydrated, CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var instance in rehydrated)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var references = await _context.Store.GetReferencesAsync(instance.Id, cancellationToken).ConfigureAwait(false);

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
                    reference.CreatedAt ?? instance.CreatedAt));

                count++;
            }
        }

        return count;
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
/// One object's index-stage row (`TD-88`, `WP 20.1C2`) — everything
/// <see cref="EngineeringObjectRehydrationService.RehydrateAsync"/> can read
/// about a persisted object without opening its document: the fields the
/// brief names as what a tree, a lookup or a business-identifier index
/// needs before — or instead of — full materialisation.
/// </summary>
/// <param name="Id">The object's own identity.</param>
/// <param name="Kind">The object's own Kind.</param>
/// <param name="Identifier">The business identifier, or <see langword="null"/> if the object never had one.</param>
/// <param name="DisplayName">The current display name.</param>
/// <param name="ParentId">The raw structural parent edge — not a resolved project id; see the remarks on <see cref="EngineeringObjectRehydrationService.BuildIndexEntry"/>.</param>
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

/// <summary>
/// What one startup rehydration actually recovered — and, just as
/// importantly, what it could not (`TD-85`).
/// </summary>
/// <param name="ObjectCount">How many engineering objects were reconstructed and registered.</param>
/// <param name="RelationshipCount">How many relationships were re-indexed from durable references.</param>
/// <param name="UnknownKinds">Kinds found on disk that no discipline registered a rehydrator for.</param>
/// <param name="OrphanedStateIds">Objects whose state survived but whose backing document did not.</param>
/// <param name="FailedObjectIds">Objects whose reconstruction threw and were skipped.</param>
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
