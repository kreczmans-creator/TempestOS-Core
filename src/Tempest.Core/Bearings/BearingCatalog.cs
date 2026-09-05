using System.Text.Json;
using Tempest.Core.Concurrency;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.Bearings;

/// <summary>
/// The concrete <see cref="IBearingCatalog"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// <b>A thin, typed index over <see cref="IEngineeringDocumentStore"/>,
/// not a second storage mechanism</b> — the pattern
/// <see cref="Materials.MaterialCatalog"/> established under `ADR-0055`
/// and <see cref="Requirements.RequirementsService"/> repeated under
/// `ADR-0058`. Every bearing record is an <see cref="IEngineeringDocument"/>
/// of <c>Kind = "BearingReference"</c>, with the record serialised as JSON
/// into the document's own <see cref="IDocumentRevision.Content"/>, so
/// revision history, authorship and document relationships all come from
/// the shared store rather than from anything invented here.
/// </para>
/// <para>
/// <b>Two indexes, for the same disclosed reason Materials needs one.</b>
/// <see cref="IEngineeringDocumentStore"/> can neither look a document up
/// by an arbitrary caller-chosen string nor enumerate documents of a
/// given <c>Kind</c>. This class therefore also depends directly on
/// <see cref="IPersistenceStore"/> for a <c>bearingId</c>-to-document
/// index (<see cref="IndexCollectionName"/>), exactly as
/// <see cref="Materials.MaterialCatalog"/> does — and for a second,
/// manufacturer-and-part-number index
/// (<see cref="PartNumberIndexCollectionName"/>), which is what makes
/// <see cref="FindByPartNumberAsync"/> and the duplicate-part-number guard
/// possible without loading and scanning the whole catalogue on every
/// write.
/// </para>
/// <para>
/// <b>Write atomicity.</b> A per-<c>bearingId</c>
/// <see cref="AsyncKeyedLock"/> serialises every check-then-write
/// sequence, mirroring <see cref="Materials.MaterialCatalog"/>'s own
/// registration lock and <see cref="EngineeringDocumentStore"/>'s own
/// per-document lock. A second lock guards the part-number index, which is
/// keyed differently from the first and so cannot be protected by it: two
/// concurrent registrations of different <c>bearingId</c>s carrying the
/// same part number would otherwise both pass their own duplicate check.
/// </para>
/// <para>
/// <b>Search is a filtered enumeration, deliberately.</b>
/// <see cref="SearchAsync"/> lists and filters in memory rather than
/// introducing a query engine or a third index. That is a disclosed,
/// deliberate scope boundary — reference-data catalogues are small, the
/// result is exactly deterministic, and nothing about
/// <see cref="BearingQuery"/> would have to change if a future Work
/// Package did add an index behind it.
/// </para>
/// </remarks>
public sealed class BearingCatalog : IBearingCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every bearing record's own backing document carries.</summary>
    public const string BearingDocumentKind = "BearingReference";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>bearingId</c> to its own backing document Id.</summary>
    public const string IndexCollectionName = "Bearings.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each manufacturer-and-part-number key to the <c>bearingId</c> holding it.</summary>
    public const string PartNumberIndexCollectionName = "Bearings.PartNumberIndex";

    private readonly IEngineeringDocumentStore _documentStore;
    private readonly IPersistenceStore _persistenceStore;
    private readonly ILogger? _logger;
    private readonly AsyncKeyedLock _bearingLock = new();
    private readonly AsyncKeyedLock _partNumberLock = new();

    /// <summary>
    /// Initialises a new instance of the <see cref="BearingCatalog"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own bearing records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own two indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="documentStore"/> or <paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    public BearingCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(persistenceStore);

        _documentStore = documentStore;
        _persistenceStore = persistenceStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IBearing> RegisterAsync(string bearingId, BearingDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearingId);
        ArgumentNullException.ThrowIfNull(definition);

        var partNumberKey = definition.Identity.PartNumberKey;

        using (await _partNumberLock.AcquireAsync(partNumberKey, cancellationToken).ConfigureAwait(false))
        using (await _bearingLock.AcquireAsync(bearingId, cancellationToken).ConfigureAwait(false))
        {
            if (await ReadDocumentIdAsync(bearingId, cancellationToken).ConfigureAwait(false) is not null)
                throw new DuplicateBearingException(bearingId);

            await RequirePartNumberFreeAsync(definition, bearingId, cancellationToken).ConfigureAwait(false);

            var dto = new BearingDocumentDto(bearingId, definition, BearingValidationState.Draft, SupersededByBearingId: null);
            var document = await _documentStore
                .CreateAsync(BearingDocumentKind, Serialise(dto), cancellationToken)
                .ConfigureAwait(false);

            await _persistenceStore.WriteAsync(IndexCollectionName, bearingId, document.Id.ToString("N"), cancellationToken).ConfigureAwait(false);
            await _persistenceStore.WriteAsync(PartNumberIndexCollectionName, partNumberKey, bearingId, cancellationToken).ConfigureAwait(false);

            _logger?.Information($"Bearing registered: '{bearingId}' (document '{document.Id}').");

            return new Bearing(bearingId, definition, BearingValidationState.Draft, null, document.Id, document.CurrentRevisionNumber);
        }
    }

    /// <inheritdoc />
    public async Task<IBearing?> FindAsync(string bearingId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearingId);

        var documentId = await ReadDocumentIdAsync(bearingId, cancellationToken).ConfigureAwait(false);
        if (documentId is null)
            return null;

        return await ReadBearingAsync(bearingId, documentId.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IBearing?> FindByPartNumberAsync(string manufacturer, string partNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manufacturer);
        ArgumentException.ThrowIfNullOrWhiteSpace(partNumber);

        var key = $"{manufacturer.Trim()} {partNumber.Trim()}".ToUpperInvariant();
        var bearingId = await _persistenceStore.ReadAsync(PartNumberIndexCollectionName, key, cancellationToken).ConfigureAwait(false);
        if (bearingId is null)
            return null;

        return await FindAsync(bearingId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IBearing>> ListAsync(CancellationToken cancellationToken = default)
    {
        var bearingIds = await _persistenceStore.ListKeysAsync(IndexCollectionName, cancellationToken).ConfigureAwait(false);
        var bearings = new List<IBearing>(bearingIds.Count);

        foreach (var bearingId in bearingIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            var documentId = await ReadDocumentIdAsync(bearingId, cancellationToken).ConfigureAwait(false);
            if (documentId is null)
                continue;

            // A stale index entry (its backing document gone, or of another
            // Kind) is skipped rather than aborting the whole listing —
            // mirroring MaterialCatalog.ListAsync's own identical guard.
            var bearing = await ReadBearingAsync(bearingId, documentId.Value, cancellationToken).ConfigureAwait(false);
            if (bearing is not null)
                bearings.Add(bearing);
        }

        return bearings;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IBearing>> SearchAsync(BearingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await ListAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(bearing => BearingQueryEvaluator.Matches(bearing, query)).ToList();
    }

    /// <inheritdoc />
    public async Task<IBearing> ReviseAsync(string bearingId, BearingDefinition definition, string? changeSummary, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearingId);
        ArgumentNullException.ThrowIfNull(definition);

        var partNumberKey = definition.Identity.PartNumberKey;

        using (await _partNumberLock.AcquireAsync(partNumberKey, cancellationToken).ConfigureAwait(false))
        using (await _bearingLock.AcquireAsync(bearingId, cancellationToken).ConfigureAwait(false))
        {
            var (documentId, current) = await RequireAsync(bearingId, cancellationToken).ConfigureAwait(false);

            if (!BearingValidationStates.IsRevisable(current.ValidationState))
                throw new ReleasedBearingImmutableException(bearingId, current.ValidationState);

            await RequirePartNumberFreeAsync(definition, bearingId, cancellationToken).ConfigureAwait(false);

            var previousKey = current.Definition.Identity.PartNumberKey;
            var revised = current with { Definition = definition };
            var revision = await _documentStore
                .ReviseAsync(documentId, Serialise(revised), changeSummary, cancellationToken)
                .ConfigureAwait(false);

            if (!string.Equals(previousKey, partNumberKey, StringComparison.Ordinal))
            {
                // The part-number index is rewritten only after the record
                // itself is durably revised: a crash between the two leaves
                // a stale index entry pointing at a real bearing (which
                // resolves, and which the reconciliation-shaped
                // ListAsync/Find guards already tolerate), never an index
                // entry pointing at a part number no record carries.
                await _persistenceStore.DeleteAsync(PartNumberIndexCollectionName, previousKey, cancellationToken).ConfigureAwait(false);
                await _persistenceStore.WriteAsync(PartNumberIndexCollectionName, partNumberKey, bearingId, cancellationToken).ConfigureAwait(false);
            }

            _logger?.Information($"Bearing revised: '{bearingId}' (revision {revision.RevisionNumber}).");

            return new Bearing(bearingId, definition, current.ValidationState, current.SupersededByBearingId, documentId, revision.RevisionNumber);
        }
    }

    /// <inheritdoc />
    public async Task<IBearing> SetValidationStateAsync(string bearingId, BearingValidationState state, string? changeSummary, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearingId);

        if (state == BearingValidationState.Superseded)
            throw new ArgumentException(
                $"Use {nameof(SupersedeAsync)} to supersede a bearing — supersession must record which bearing replaced it, which this method cannot.",
                nameof(state));

        using (await _bearingLock.AcquireAsync(bearingId, cancellationToken).ConfigureAwait(false))
        {
            var (documentId, current) = await RequireAsync(bearingId, cancellationToken).ConfigureAwait(false);

            if (!BearingValidationStates.IsPermitted(current.ValidationState, state))
                throw new InvalidBearingValidationStateTransitionException(bearingId, current.ValidationState, state);

            RequireProvenanceFor(bearingId, current.Definition.Provenance, state);

            var updated = current with { ValidationState = state };
            var revision = await _documentStore
                .ReviseAsync(documentId, Serialise(updated), changeSummary, cancellationToken)
                .ConfigureAwait(false);

            _logger?.Information($"Bearing '{bearingId}' validation state: {current.ValidationState} -> {state}.");

            return new Bearing(bearingId, current.Definition, state, current.SupersededByBearingId, documentId, revision.RevisionNumber);
        }
    }

    /// <inheritdoc />
    public async Task<IBearing> SupersedeAsync(string bearingId, string replacementBearingId, string? changeSummary, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementBearingId);

        if (string.Equals(bearingId, replacementBearingId, StringComparison.Ordinal))
            throw new ArgumentException("A bearing cannot supersede itself.", nameof(replacementBearingId));

        var replacementDocumentId = await ReadDocumentIdAsync(replacementBearingId, cancellationToken).ConfigureAwait(false)
            ?? throw new BearingNotFoundException(replacementBearingId);

        using (await _bearingLock.AcquireAsync(bearingId, cancellationToken).ConfigureAwait(false))
        {
            var (documentId, current) = await RequireAsync(bearingId, cancellationToken).ConfigureAwait(false);

            if (!BearingValidationStates.IsPermitted(current.ValidationState, BearingValidationState.Superseded))
                throw new InvalidBearingValidationStateTransitionException(bearingId, current.ValidationState, BearingValidationState.Superseded);

            var updated = current with
            {
                ValidationState = BearingValidationState.Superseded,
                SupersededByBearingId = replacementBearingId,
            };

            var revision = await _documentStore
                .ReviseAsync(documentId, Serialise(updated), changeSummary, cancellationToken)
                .ConfigureAwait(false);

            // The replacement links to the record it supersedes, not the
            // other way round: that is the direction, and the kind, this
            // platform already uses for supersession
            // (`Decision.SupersedesAsync`), and inventing a second value
            // for one concept is exactly the vocabulary drift `ADR-0073`
            // names as the cost of an open relationship vocabulary. The
            // superseded record still names its own replacement directly,
            // in `SupersededByBearingId`, so nothing is lost by following
            // the established direction.
            await _documentStore
                .LinkAsync(replacementDocumentId, documentId, GovernanceRelationshipKinds.Supersedes, cancellationToken)
                .ConfigureAwait(false);

            _logger?.Information($"Bearing '{bearingId}' superseded by '{replacementBearingId}'.");

            return new Bearing(bearingId, current.Definition, BearingValidationState.Superseded, replacementBearingId, documentId, revision.RevisionNumber);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IDocumentRevision>> GetHistoryAsync(string bearingId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearingId);

        var documentId = await ReadDocumentIdAsync(bearingId, cancellationToken).ConfigureAwait(false)
            ?? throw new BearingNotFoundException(bearingId);

        return await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IBearing> GetRevisionAsync(string bearingId, int revisionNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearingId);

        var documentId = await ReadDocumentIdAsync(bearingId, cancellationToken).ConfigureAwait(false)
            ?? throw new BearingNotFoundException(bearingId);

        var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
        var revision = history.FirstOrDefault(r => r.RevisionNumber == revisionNumber)
            ?? throw new ArgumentOutOfRangeException(
                nameof(revisionNumber),
                revisionNumber,
                $"Bearing '{bearingId}' has no revision {revisionNumber} (revisions 1 to {history.Count} exist).");

        var dto = Deserialise(bearingId, documentId, revision.Content);
        return new Bearing(dto.BearingId, dto.Definition, dto.ValidationState, dto.SupersededByBearingId, documentId, revision.RevisionNumber);
    }

    /// <summary>
    /// Refuses a state a record's own provenance cannot support. This is
    /// the enforcement point for A4's own central rule: reference data
    /// earns its status from its source, never from a caller asserting one.
    /// </summary>
    private static void RequireProvenanceFor(string bearingId, BearingProvenance provenance, BearingValidationState state)
    {
        if (state == BearingValidationState.Draft)
            return;

        if (!provenance.IdentifiesASource)
            throw new BearingProvenanceIncompleteException(
                bearingId,
                state,
                "its provenance names neither a source organisation nor a source document, so nothing about it can be checked.");

        if (state == BearingValidationState.Released && !provenance.IsVerified)
            throw new BearingProvenanceIncompleteException(
                bearingId,
                state,
                "release requires provenance verified against the source by a named reviewer on a recorded date; being imported is not being verified.");
    }

    private async Task RequirePartNumberFreeAsync(BearingDefinition definition, string bearingId, CancellationToken cancellationToken)
    {
        var key = definition.Identity.PartNumberKey;
        var holder = await _persistenceStore.ReadAsync(PartNumberIndexCollectionName, key, cancellationToken).ConfigureAwait(false);

        if (holder is not null && !string.Equals(holder, bearingId, StringComparison.Ordinal))
            throw new DuplicateBearingPartNumberException(
                definition.Identity.Manufacturer,
                definition.Identity.ManufacturerPartNumber,
                holder);
    }

    private async Task<(Guid DocumentId, BearingDocumentDto Current)> RequireAsync(string bearingId, CancellationToken cancellationToken)
    {
        var documentId = await ReadDocumentIdAsync(bearingId, cancellationToken).ConfigureAwait(false)
            ?? throw new BearingNotFoundException(bearingId);

        var dto = await ReadDtoAsync(bearingId, documentId, cancellationToken).ConfigureAwait(false)
            ?? throw new BearingNotFoundException(bearingId);

        return (documentId, dto);
    }

    /// <summary>
    /// Resolves <paramref name="bearingId"/>'s backing document Id from the
    /// index. A malformed index value throws a controlled
    /// <see cref="BearingsException"/> naming the entry — never a raw
    /// <see cref="FormatException"/>, and never <see langword="null"/>,
    /// which would silently misreport corruption as "no such bearing"
    /// (`TD-60`'s own lesson, applied here from the outset).
    /// </summary>
    private async Task<Guid?> ReadDocumentIdAsync(string bearingId, CancellationToken cancellationToken)
    {
        var value = await _persistenceStore.ReadAsync(IndexCollectionName, bearingId, cancellationToken).ConfigureAwait(false);
        if (value is null)
            return null;

        if (!Guid.TryParseExact(value, "N", out var documentId))
            throw new BearingsException($"Bearing index entry for '{bearingId}' is corrupted: '{value}' is not a valid document Id.");

        return documentId;
    }

    private async Task<BearingDocumentDto?> ReadDtoAsync(string bearingId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documentStore.FindAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, BearingDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (history.Count == 0)
            throw new BearingsException($"Bearing '{bearingId}' (document '{documentId}') has no revisions.");

        return Deserialise(bearingId, documentId, history[^1].Content);
    }

    private async Task<IBearing?> ReadBearingAsync(string bearingId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _documentStore.FindAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (document is null || !string.Equals(document.Kind, BearingDocumentKind, StringComparison.Ordinal))
            return null;

        var history = await _documentStore.GetRevisionHistoryAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (history.Count == 0)
            throw new BearingsException($"Bearing '{bearingId}' (document '{documentId}') has no revisions.");

        var currentRevision = history[^1];
        var dto = Deserialise(bearingId, documentId, currentRevision.Content);

        return new Bearing(dto.BearingId, dto.Definition, dto.ValidationState, dto.SupersededByBearingId, documentId, currentRevision.RevisionNumber);
    }

    private static string Serialise(BearingDocumentDto dto) =>
        JsonSerializer.Serialize(dto, BearingSerialisation.Options);

    /// <summary>Deserialises one revision's content, converting any malformed-content failure into a controlled <see cref="BearingsException"/> rather than a raw <see cref="JsonException"/>.</summary>
    private static BearingDocumentDto Deserialise(string bearingId, Guid documentId, string content)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<BearingDocumentDto>(content, BearingSerialisation.Options)
                ?? throw new BearingsException($"Bearing '{bearingId}' (document '{documentId}') could not be deserialised.");

            // A structurally-valid JSON object missing the record itself
            // would otherwise surface later as a raw NullReferenceException
            // in whatever read it.
            if (dto.Definition is null)
                throw new BearingsException($"Bearing '{bearingId}' (document '{documentId}') is missing its definition.");

            return dto;
        }
        catch (JsonException ex)
        {
            throw new BearingsException($"Bearing '{bearingId}' (document '{documentId}') could not be deserialised.", ex);
        }
    }
}
