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
/// </remarks>
public sealed class EngineeringObjectRehydrationService
{
    private readonly EngineeringDomainContext _context;
    private readonly IEngineeringObjectRehydratorRegistry _rehydrators;
    private readonly ILogger? _logger;

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

        var states = await stateStore.ListAsync(cancellationToken).ConfigureAwait(false);
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
}

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
