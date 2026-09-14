using System.Text.Json;
using Tempest.Core.Concurrency;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// The storage, revision, lifecycle and supersession machinery every Group
/// A reference library shares. A domain library derives from this and adds
/// only its own engineering semantics.
/// </summary>
/// <remarks>
/// <para>
/// <b>A thin, typed index over <see cref="IEngineeringDocumentStore"/>,
/// not a second storage mechanism</b> — the pattern
/// <see cref="Materials.MaterialCatalog"/> established under `ADR-0055`,
/// <see cref="Requirements.RequirementsService"/> repeated under
/// `ADR-0058`, and A4 repeated a third time before it was extracted to
/// here (`ADR-0126`). Every record is an <see cref="IEngineeringDocument"/>
/// of the library's own <see cref="DocumentKind"/>, with the record
/// serialised as JSON into the document's own
/// <see cref="IDocumentRevision.Content"/>, so revision history,
/// authorship and document relationships all come from the shared store
/// rather than from anything invented per library.
/// </para>
/// <para>
/// <b>Indexes, for the reason `ADR-0055` disclosed.</b>
/// <see cref="IEngineeringDocumentStore"/> can neither look a document up
/// by an arbitrary caller-chosen string nor enumerate documents of a given
/// <c>Kind</c>. This class therefore also depends directly on
/// <see cref="IPersistenceStore"/> for a record-Id index. A library that
/// also has an outward-facing identity to keep unique — a manufacturer
/// part number, a standard designation, a constant's own symbol —
/// overrides <see cref="GetSecondaryKey"/> and gets a second index for
/// free.
/// </para>
/// <para>
/// <b>Write atomicity.</b> A per-record <see cref="AsyncKeyedLock"/>
/// serialises every check-then-write sequence, mirroring
/// <see cref="EngineeringDocumentStore"/>'s own per-document lock. A
/// second lock guards the secondary index, which is keyed differently and
/// so cannot be protected by the first: two concurrent registrations of
/// different Ids carrying the same designation would otherwise both pass
/// their own duplicate check.
/// </para>
/// </remarks>
/// <typeparam name="TDefinition">The domain's own engineering description type.</typeparam>
public abstract class ReferenceDataCatalog<TDefinition> : IReferenceDataCatalog<TDefinition>
    where TDefinition : class
{
    private readonly IEngineeringDocumentStore _documentStore;
    private readonly IPersistenceStore _persistenceStore;
    private readonly IQueryablePersistenceStore _transactionalStore;
    private readonly ITransactionalDocumentWriter _documentWriter;
    private readonly ILogger? _logger;
    private readonly AsyncKeyedLock _recordLock = new();
    private readonly AsyncKeyedLock _secondaryKeyLock = new();

    /// <summary>
    /// Initialises a new instance of the <see cref="ReferenceDataCatalog{TDefinition}"/> class.
    /// </summary>
    /// <param name="documentStore">The store this catalogue's own records are backed by.</param>
    /// <param name="persistenceStore">The store this catalogue's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="documentStore"/> or <paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="persistenceStore"/> does not also implement
    /// <see cref="IQueryablePersistenceStore"/>, or <paramref name="documentStore"/>
    /// does not also implement <see cref="ITransactionalDocumentWriter"/> —
    /// either way, this catalogue's composed writes (`TD-158`) could only be
    /// made transactional by silently falling back to the old, unsafe
    /// sequential path, which this constructor refuses to do. Every store
    /// this platform ships (production and test alike) implements both.
    /// </exception>
    protected ReferenceDataCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(persistenceStore);

        _documentStore = documentStore;
        _persistenceStore = persistenceStore;
        _logger = logger;

        // `TD-158`: RegisterAsync, ReviseAsync and SupersedeAsync each
        // compose more than one durable write (the document/revision, the
        // primary index entry, and — on revise or supersede — the
        // secondary index entry) and must land as one transaction or not
        // at all. `IPersistenceStore` alone cannot express that, so the
        // narrowest widening is a runtime type check against the same
        // store instance for the transactional query surface
        // (`IQueryablePersistenceStore`, `ADR-0144`) and, for the document
        // half of the write, the internal transactional writer contract
        // `EngineeringDocumentStore` already implements for
        // `EngineeringObjectBase` (`ADR-0145`). Both shipped stores —
        // production's `SqlitePersistenceStore` and every in-memory test
        // double this suite uses — implement both, so this is never
        // reached outside a deliberately narrow test double; where it is
        // reached, refusing loudly is the only acceptable outcome (never a
        // silent, non-transactional fallback that would reintroduce the
        // defect this constructor exists to close).
        _transactionalStore = persistenceStore as IQueryablePersistenceStore
            ?? throw new ArgumentException(
                $"'{persistenceStore.GetType().Name}' does not implement '{nameof(IQueryablePersistenceStore)}', so " +
                $"{GetType().Name} cannot compose its durable writes into one transaction (`TD-158`). Use the store " +
                "this platform ships, or widen the store passed here to also implement the transactional query surface.",
                nameof(persistenceStore));

        _documentWriter = documentStore as ITransactionalDocumentWriter
            ?? throw new ArgumentException(
                $"'{documentStore.GetType().Name}' does not implement '{nameof(ITransactionalDocumentWriter)}', so " +
                $"{GetType().Name} cannot compose its durable writes into one transaction (`TD-158`). Use the " +
                "document store this platform ships.",
                nameof(documentStore));
    }

    /// <inheritdoc />
    public abstract string LibraryName { get; }

    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every record in this library carries.</summary>
    public abstract string DocumentKind { get; }

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered record Id to its own backing document Id.</summary>
    public abstract string IndexCollectionName { get; }

    /// <summary>
    /// The <see cref="IPersistenceStore"/> collection mapping this
    /// library's own secondary uniqueness key to the record Id holding it.
    /// Unused where <see cref="GetSecondaryKey"/> always returns
    /// <see langword="null"/>.
    /// </summary>
    public virtual string SecondaryIndexCollectionName => $"{IndexCollectionName}.SecondaryKey";

    /// <summary>
    /// The outward-facing identity this library enforces as unique, or
    /// <see langword="null"/> where it enforces none. Case- and
    /// whitespace-normalisation is the override's own responsibility, so
    /// each library decides what "the same designation" means for it.
    /// </summary>
    /// <param name="definition">The definition to derive a key from.</param>
    protected virtual string? GetSecondaryKey(TDefinition definition) => null;

    /// <summary>
    /// A human-readable description of <paramref name="definition"/>'s own
    /// secondary key, used in the duplicate exception's own message so a
    /// reader sees what actually collided.
    /// </summary>
    /// <param name="definition">The definition whose key collided.</param>
    protected virtual string DescribeSecondaryKey(TDefinition definition) =>
        $"Key '{GetSecondaryKey(definition)}'";

    /// <inheritdoc />
    public Task<IReferenceRecord<TDefinition>> RegisterAsync(
        string recordId,
        TDefinition definition,
        ReferenceProvenance provenance,
        CancellationToken cancellationToken = default) =>
        RegisterAsync(recordId, definition, provenance, source: null, cancellationToken);

    /// <inheritdoc />
    public async Task<IReferenceRecord<TDefinition>> RegisterAsync(
        string recordId,
        TDefinition definition,
        ReferenceProvenance provenance,
        SourceCitation? source,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(provenance);

        var secondaryKey = GetSecondaryKey(definition);

        using (await AcquireSecondaryLockAsync(secondaryKey, cancellationToken).ConfigureAwait(false))
        using (await _recordLock.AcquireAsync(recordId, cancellationToken).ConfigureAwait(false))
        {
            if (await ReadDocumentIdAsync(recordId, cancellationToken).ConfigureAwait(false) is not null)
                throw new DuplicateReferenceRecordException(LibraryName, recordId);

            await RequireSecondaryKeyFreeAsync(definition, recordId, cancellationToken).ConfigureAwait(false);

            var dto = new ReferenceDocumentDto<TDefinition>(recordId, definition, provenance, ReferenceValidationState.Draft, null, source);
            var content = Serialise(dto);
            var documentId = Guid.NewGuid();
            IDocumentRevision? revision = null;

            // `TD-158`: the document, its revision 1, the primary index
            // entry and the secondary index entry (if any) are one
            // transaction — a fault after any of them leaves none durable.
            await _transactionalStore.ExecuteInTransactionAsync(async (transaction, ct) =>
            {
                var creation = await _documentWriter.CreateAsync(transaction, documentId, DocumentKind, content, ct).ConfigureAwait(false);
                revision = creation.Revision;

                await transaction.WriteAsync(IndexCollectionName, recordId, documentId.ToString("N"), ct).ConfigureAwait(false);
                if (secondaryKey is not null)
                    await transaction.WriteAsync(SecondaryIndexCollectionName, secondaryKey, recordId, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            _logger?.Information($"{LibraryName} record registered: '{recordId}' (document '{documentId}').");

            return new ReferenceRecord<TDefinition>(
                recordId, definition, provenance, ReferenceValidationState.Draft, null, documentId, revision!.RevisionNumber, source);
        }
    }

    /// <inheritdoc />
    public async Task<IReferenceRecord<TDefinition>?> FindAsync(string recordId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var documentId = await ReadDocumentIdAsync(recordId, cancellationToken).ConfigureAwait(false);
        if (documentId is null)
            return null;

        return await ReadRecordAsync(recordId, documentId.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the record holding <paramref name="secondaryKey"/>, or
    /// <see langword="null"/> if none does. The key must already be
    /// normalised the same way <see cref="GetSecondaryKey"/> normalises it.
    /// </summary>
    protected async Task<IReferenceRecord<TDefinition>?> FindBySecondaryKeyAsync(string secondaryKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secondaryKey);

        var recordId = await _persistenceStore.ReadAsync(SecondaryIndexCollectionName, secondaryKey, cancellationToken).ConfigureAwait(false);
        if (recordId is null)
            return null;

        return await FindAsync(recordId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<TDefinition>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var recordIds = await _persistenceStore.ListKeysAsync(IndexCollectionName, cancellationToken).ConfigureAwait(false);
        var records = new List<IReferenceRecord<TDefinition>>(recordIds.Count);

        foreach (var recordId in recordIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            var documentId = await ReadDocumentIdAsync(recordId, cancellationToken).ConfigureAwait(false);
            if (documentId is null)
                continue;

            // A stale index entry (its backing document gone, or of another
            // Kind) is skipped rather than aborting the whole listing —
            // mirroring MaterialCatalog.ListAsync's own identical guard.
            var record = await ReadRecordAsync(recordId, documentId.Value, cancellationToken).ConfigureAwait(false);
            if (record is not null)
                records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Every registered record matching <paramref name="predicate"/>, in
    /// the same order <see cref="ListAsync"/> uses.
    /// </summary>
    /// <remarks>
    /// A filtered enumeration, deliberately: reference-data catalogues are
    /// small, the result is exactly deterministic, and no library's own
    /// query type would have to change if a future Work Package added an
    /// index behind this. Each library exposes its own typed query over
    /// this rather than leaking a predicate to its callers.
    /// </remarks>
    protected async Task<IReadOnlyList<IReferenceRecord<TDefinition>>> FilterAsync(
        Func<IReferenceRecord<TDefinition>, bool> predicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var all = await ListAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(predicate).ToList();
    }

    /// <inheritdoc />
    public Task<IReferenceRecord<TDefinition>> ReviseAsync(
        string recordId,
        TDefinition definition,
        ReferenceProvenance provenance,
        string? changeSummary,
        CancellationToken cancellationToken = default) =>
        ReviseCoreAsync(recordId, definition, provenance, changeSummary, sourceSpecified: false, source: null, cancellationToken);

    /// <inheritdoc />
    public Task<IReferenceRecord<TDefinition>> ReviseAsync(
        string recordId,
        TDefinition definition,
        ReferenceProvenance provenance,
        string? changeSummary,
        SourceCitation? source,
        CancellationToken cancellationToken = default) =>
        ReviseCoreAsync(recordId, definition, provenance, changeSummary, sourceSpecified: true, source, cancellationToken);

    /// <summary>
    /// The whole of <see cref="ReviseAsync(string,TDefinition,ReferenceProvenance,string?,CancellationToken)"/>
    /// and its citation-aware overload. <paramref name="sourceSpecified"/>
    /// tells the two apart: the citation-unaware overload carries the
    /// record's own current citation forward untouched (it does not know
    /// citations exist), while the citation-aware one always writes
    /// <paramref name="source"/> as given, including <see langword="null"/>
    /// to withdraw a citation that no longer holds.
    /// </summary>
    private async Task<IReferenceRecord<TDefinition>> ReviseCoreAsync(
        string recordId,
        TDefinition definition,
        ReferenceProvenance provenance,
        string? changeSummary,
        bool sourceSpecified,
        SourceCitation? source,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(provenance);

        var secondaryKey = GetSecondaryKey(definition);

        using (await AcquireSecondaryLockAsync(secondaryKey, cancellationToken).ConfigureAwait(false))
        using (await _recordLock.AcquireAsync(recordId, cancellationToken).ConfigureAwait(false))
        {
            var (documentId, current) = await RequireAsync(recordId, cancellationToken).ConfigureAwait(false);

            if (!ReferenceValidationStates.IsRevisable(current.ValidationState))
                throw new ReleasedReferenceImmutableException(LibraryName, recordId, current.ValidationState);

            await RequireSecondaryKeyFreeAsync(definition, recordId, cancellationToken).ConfigureAwait(false);

            var previousKey = GetSecondaryKey(current.Definition);
            var effectiveSource = sourceSpecified ? source : current.Source;
            var revised = current with { Definition = definition, Provenance = provenance, Source = effectiveSource };
            var content = Serialise(revised);
            IDocumentRevision? revision = null;

            // `TD-158`: the revision and the secondary-index move (if the
            // key changed) are one transaction — a fault between the two
            // used to be able to leave a stale index entry pointing at a
            // key no record carries; now either both land or neither does.
            await _transactionalStore.ExecuteInTransactionAsync(async (transaction, ct) =>
            {
                revision = await _documentWriter.ReviseAsync(transaction, documentId, content, changeSummary, ct).ConfigureAwait(false);

                if (!string.Equals(previousKey, secondaryKey, StringComparison.Ordinal))
                {
                    if (previousKey is not null)
                        await transaction.DeleteAsync(SecondaryIndexCollectionName, previousKey, ct).ConfigureAwait(false);
                    if (secondaryKey is not null)
                        await transaction.WriteAsync(SecondaryIndexCollectionName, secondaryKey, recordId, ct).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);

            var revisionNumber = revision!.RevisionNumber;
            _logger?.Information($"{LibraryName} record revised: '{recordId}' (revision {revisionNumber}).");

            return new ReferenceRecord<TDefinition>(
                recordId, definition, provenance, current.ValidationState, current.SupersededByRecordId, documentId, revisionNumber,
                effectiveSource);
        }
    }

    /// <inheritdoc />
    public async Task<IReferenceRecord<TDefinition>> SetValidationStateAsync(
        string recordId,
        ReferenceValidationState state,
        string? changeSummary,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        if (state == ReferenceValidationState.Superseded)
            throw new ArgumentException(
                $"Use {nameof(SupersedeAsync)} to supersede a record — supersession must record which record replaced it, which this method cannot.",
                nameof(state));

        using (await _recordLock.AcquireAsync(recordId, cancellationToken).ConfigureAwait(false))
        {
            var (documentId, current) = await RequireAsync(recordId, cancellationToken).ConfigureAwait(false);

            if (!ReferenceValidationStates.IsPermitted(current.ValidationState, state))
                throw new InvalidReferenceStateTransitionException(LibraryName, recordId, current.ValidationState, state);

            if (ReferenceValidationStates.DescribeProvenanceShortfall(current.Provenance, state) is { } shortfall)
                throw new ReferenceProvenanceIncompleteException(LibraryName, recordId, state, shortfall);

            var updated = current with { ValidationState = state };
            var revision = await _documentStore
                .ReviseAsync(documentId, Serialise(updated), changeSummary, cancellationToken)
                .ConfigureAwait(false);

            _logger?.Information($"{LibraryName} record '{recordId}' validation state: {current.ValidationState} -> {state}.");

            return new ReferenceRecord<TDefinition>(
                recordId, current.Definition, current.Provenance, state, current.SupersededByRecordId, documentId, revision.RevisionNumber,
                current.Source);
        }
    }

    /// <inheritdoc />
    public async Task<IReferenceRecord<TDefinition>> SupersedeAsync(
        string recordId,
        string replacementRecordId,
        string? changeSummary,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementRecordId);

        if (string.Equals(recordId, replacementRecordId, StringComparison.Ordinal))
            throw new ArgumentException("A record cannot supersede itself.", nameof(replacementRecordId));

        var replacementDocumentId = await ReadDocumentIdAsync(replacementRecordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(LibraryName, replacementRecordId);

        using (await _recordLock.AcquireAsync(recordId, cancellationToken).ConfigureAwait(false))
        {
            var (documentId, current) = await RequireAsync(recordId, cancellationToken).ConfigureAwait(false);

            if (!ReferenceValidationStates.IsPermitted(current.ValidationState, ReferenceValidationState.Superseded))
                throw new InvalidReferenceStateTransitionException(LibraryName, recordId, current.ValidationState, ReferenceValidationState.Superseded);

            var updated = current with
            {
                ValidationState = ReferenceValidationState.Superseded,
                SupersededByRecordId = replacementRecordId,
            };
            var content = Serialise(updated);
            IDocumentRevision? revision = null;

            // `TD-158`: the revision and the Supersedes link are one
            // transaction — a fault between the two used to be able to
            // leave a durably superseded record with no recorded
            // replacement, or a link naming a document that was never
            // actually revised.
            //
            // The secondary index is deliberately left untouched here — a
            // superseded record keeps resolving by its own former key,
            // exactly as it keeps resolving by its own Id: retained, not
            // deleted, because its history is itself engineering data
            // (`SupersedeAsync_LeavesTheSupersededValuesReadable`,
            // `ASupersededConstantStopsBeingHandedToCalculations`). What
            // `TD-156` actually closes is `RequireSecondaryKeyFreeAsync`
            // below, which used to treat that key as permanently held even
            // once its holder was superseded, so no later record could
            // ever legitimately claim it — see the remarks there.
            await _transactionalStore.ExecuteInTransactionAsync(async (transaction, ct) =>
            {
                revision = await _documentWriter.ReviseAsync(transaction, documentId, content, changeSummary, ct).ConfigureAwait(false);

                // The replacement links to the record it supersedes, not the
                // other way round: that is the direction, and the kind, this
                // platform already uses (`Decision.SupersedesAsync`). The
                // superseded record still names its own replacement directly,
                // so nothing is lost by following the established direction,
                // and no library invents a second value for one concept
                // (`ADR-0073`).
                await _documentWriter.LinkAsync(
                    transaction, replacementDocumentId, documentId, GovernanceRelationshipKinds.Supersedes, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            _logger?.Information($"{LibraryName} record '{recordId}' superseded by '{replacementRecordId}'.");

            return new ReferenceRecord<TDefinition>(
                recordId,
                current.Definition,
                current.Provenance,
                ReferenceValidationState.Superseded,
                replacementRecordId,
                documentId,
                revision!.RevisionNumber,
                current.Source);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IDocumentRevision>> GetHistoryAsync(string recordId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var documentId = await ReadDocumentIdAsync(recordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(LibraryName, recordId);

        return await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReferenceRecord<TDefinition>> GetRevisionAsync(string recordId, int revisionNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var documentId = await ReadDocumentIdAsync(recordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(LibraryName, recordId);

        var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
        var revision = history.FirstOrDefault(r => r.RevisionNumber == revisionNumber)
            ?? throw new ArgumentOutOfRangeException(
                nameof(revisionNumber),
                revisionNumber,
                $"{LibraryName} record '{recordId}' has no revision {revisionNumber} (revisions 1 to {history.Count} exist).");

        var dto = Deserialise(recordId, documentId, revision.Content);
        return new ReferenceRecord<TDefinition>(
            dto.RecordId, dto.Definition, dto.Provenance, dto.ValidationState, dto.SupersededByRecordId, documentId, revision.RevisionNumber,
            dto.Source);
    }

    private async Task<IDisposable> AcquireSecondaryLockAsync(string? secondaryKey, CancellationToken cancellationToken) =>
        await _secondaryKeyLock.AcquireAsync(secondaryKey ?? string.Empty, cancellationToken).ConfigureAwait(false);

    /// <remarks>
    /// <b>`TD-156`.</b> A key nominally still indexed to a now-superseded
    /// record is not actually held by anything current, so it must not go
    /// on blocking every later record from claiming it — which is exactly
    /// what happened before this fix: <c>SupersedeAsync</c> never touches
    /// the secondary index (deliberately — see its own remarks), so
    /// without this check a designation's replacement record could never
    /// legitimately be revised onto the very designation it replaces, and
    /// nothing "newer" could ever be found by that key again. The
    /// superseded holder itself is unaffected — it keeps its own Id, its
    /// own revision history, and keeps resolving by this same key for as
    /// long as nothing else claims it (retained, never deleted).
    /// </remarks>
    private async Task RequireSecondaryKeyFreeAsync(TDefinition definition, string recordId, CancellationToken cancellationToken)
    {
        var key = GetSecondaryKey(definition);
        if (key is null)
            return;

        var holder = await _persistenceStore.ReadAsync(SecondaryIndexCollectionName, key, cancellationToken).ConfigureAwait(false);

        if (holder is null || string.Equals(holder, recordId, StringComparison.Ordinal))
            return;

        var holderRecord = await FindAsync(holder, cancellationToken).ConfigureAwait(false);
        if (holderRecord is { ValidationState: ReferenceValidationState.Superseded })
            return;

        throw new DuplicateReferenceKeyException(LibraryName, DescribeSecondaryKey(definition), holder);
    }

    private async Task<(Guid DocumentId, ReferenceDocumentDto<TDefinition> Current)> RequireAsync(string recordId, CancellationToken cancellationToken)
    {
        var documentId = await ReadDocumentIdAsync(recordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(LibraryName, recordId);

        var dto = await ReadDtoAsync(recordId, documentId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(LibraryName, recordId);

        return (documentId, dto);
    }

    /// <summary>
    /// Resolves <paramref name="recordId"/>'s backing document Id from the
    /// index. A malformed index value throws a controlled
    /// <see cref="ReferenceDataException"/> naming the entry — never a raw
    /// <see cref="FormatException"/>, and never <see langword="null"/>,
    /// which would silently misreport corruption as "no such record"
    /// (`TD-60`'s own lesson).
    /// </summary>
    private async Task<Guid?> ReadDocumentIdAsync(string recordId, CancellationToken cancellationToken)
    {
        var value = await _persistenceStore.ReadAsync(IndexCollectionName, recordId, cancellationToken).ConfigureAwait(false);
        if (value is null)
            return null;

        if (!Guid.TryParseExact(value, "N", out var documentId))
            throw new ReferenceDataException(
                LibraryName,
                $"{LibraryName} index entry for '{recordId}' is corrupted: '{value}' is not a valid document Id.");

        return documentId;
    }

    private async Task<ReferenceDocumentDto<TDefinition>?> ReadDtoAsync(string recordId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documentStore.FindAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, DocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (history.Count == 0)
            throw new ReferenceDataException(LibraryName, $"{LibraryName} record '{recordId}' (document '{documentId}') has no revisions.");

        return Deserialise(recordId, documentId, history[^1].Content);
    }

    private async Task<IReferenceRecord<TDefinition>?> ReadRecordAsync(string recordId, Guid documentId, CancellationToken cancellationToken)
    {
        var dto = await ReadDtoAsync(recordId, documentId, cancellationToken).ConfigureAwait(false);
        if (dto is null)
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);

        return new ReferenceRecord<TDefinition>(
            dto.RecordId, dto.Definition, dto.Provenance, dto.ValidationState, dto.SupersededByRecordId, documentId, history[^1].RevisionNumber,
            dto.Source);
    }

    private static string Serialise(ReferenceDocumentDto<TDefinition> dto) =>
        JsonSerializer.Serialize(dto, ReferenceSerialisation.Options);

    /// <summary>Deserialises one revision's content, converting any malformed-content failure into a controlled <see cref="ReferenceDataException"/> rather than a raw <see cref="JsonException"/>.</summary>
    private ReferenceDocumentDto<TDefinition> Deserialise(string recordId, Guid documentId, string content)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<ReferenceDocumentDto<TDefinition>>(content, ReferenceSerialisation.Options)
                ?? throw new ReferenceDataException(LibraryName, $"{LibraryName} record '{recordId}' (document '{documentId}') could not be deserialised.");

            // A structurally-valid JSON object missing the record itself
            // would otherwise surface later as a raw NullReferenceException
            // in whatever read it.
            if (dto.Definition is null)
                throw new ReferenceDataException(LibraryName, $"{LibraryName} record '{recordId}' (document '{documentId}') is missing its definition.");

            if (dto.Provenance is null)
                throw new ReferenceDataException(LibraryName, $"{LibraryName} record '{recordId}' (document '{documentId}') is missing its provenance.");

            return dto;
        }
        catch (JsonException ex)
        {
            throw new ReferenceDataException(LibraryName, $"{LibraryName} record '{recordId}' (document '{documentId}') could not be deserialised.", ex);
        }
    }
}
