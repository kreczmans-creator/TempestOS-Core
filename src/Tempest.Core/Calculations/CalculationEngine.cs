using System.Collections.Concurrent;
using System.Text.Json;
using Tempest.Core.Audit;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Logging;

namespace Tempest.Core.Calculations;

/// <summary>
/// The concrete <see cref="ICalculationEngine"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registration</b> (`ADR-0056`) uses a type-erased
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> keyed by
/// <c>CalculationId</c>, storing each boxed
/// <see cref="ICalculationDefinition{TInput, TResult}"/> as
/// <see cref="object"/> — mirroring the Command Framework's own
/// type-keyed-then-cast dispatch shape (`ADR-0037`). A mismatched
/// <typeparamref name="TInput"/>/<typeparamref name="TResult"/> pair at
/// <see cref="ExecuteAsync{TInput, TResult}"/> is treated identically to
/// an unregistered Id — both surface as
/// <see cref="CalculationDefinitionNotFoundException"/>.
/// </para>
/// <para>
/// <b>Every execution is durably recorded as an <see cref="IEngineeringDocument"/>
/// of <c>Kind = "CalculationRecord"</c></b> (`ADR-0056`, resolving
/// `WP7.0C Engineering Foundation Contracts.md`'s own "plausible, not
/// mandatory" integration in favour of mandatory) — giving every
/// <see cref="CalculationRecord{TResult}"/> a stable identity
/// (<see cref="CalculationRecord{TResult}.Id"/>, the document's own Id)
/// and genuine revision capability inherited directly from
/// <see cref="IEngineeringDocumentStore"/>, with no new storage
/// mechanism introduced. Unlike <see cref="Materials.MaterialCatalog"/>,
/// this engine needs no direct <see cref="Persistence.IPersistenceStore"/>
/// dependency of its own: each execution always creates a brand new
/// document (an append-only evidentiary event, never looked up later by
/// a caller-chosen key), so no <c>calculationId</c>-to-<c>documentId</c>
/// index is required.
/// </para>
/// </remarks>
public sealed class CalculationEngine : ICalculationEngine
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every calculation execution's own backing document carries.</summary>
    public const string CalculationRecordDocumentKind = "CalculationRecord";

    /// <summary>The <see cref="CalculationRecord{TResult}.ExecutedByPrincipalId"/> recorded when no principal is currently established.</summary>
    public const string UnknownExecutorPrincipalId = "unknown";

    /// <summary>The <see cref="Audit.IAuditRecord.Action"/> recorded for every calculation execution (`WP 17.2A`).</summary>
    public const string CalculationExecutedActionName = "calculation.executed";

    /// <summary>
    /// The index collection every executed record's identity is written to,
    /// so <see cref="ListRecordsAsync"/> can answer "what has been
    /// calculated" without a second store.
    /// </summary>
    /// <remarks>
    /// <b>Added because the premise this class's own remarks recorded stopped
    /// being true.</b> They said no index was required because a record is
    /// "never looked up later by a caller-chosen key" — true while the only
    /// consumer was the caller that had just executed it and still held the
    /// Id. A Desktop surface that lists what exists cannot hold every Id, and
    /// neither <see cref="IEngineeringDocumentStore"/> nor this interface
    /// could enumerate them. This is the same
    /// <see cref="IPersistenceStore"/> index convention every reference
    /// catalogue already uses — the existing mechanism, not a new one, and
    /// the records themselves still live where they always did.
    /// </remarks>
    public const string RecordIndexCollection = "Calculations.RecordIndex";

    private readonly ConcurrentDictionary<string, object> _definitions = new();
    private readonly IEngineeringDocumentStore _documentStore;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly ILogger? _logger;
    private readonly IPersistenceStore? _recordIndex;
    private readonly IAuditRecorder? _auditRecorder;

    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationEngine"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own calculation records are durably held in.</param>
    /// <param name="currentPrincipalAccessor">The service this instance resolves the acting principal from.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <param name="recordIndex">
    /// The store this engine records executed-record identities in, so a
    /// surface can list what has been calculated. Optional: without it the
    /// engine behaves exactly as before, writing no index and listing
    /// nothing.
    /// </param>
    /// <param name="auditRecorder">
    /// Records a <c>calculation.executed</c> audit row for every execution
    /// (`WP 17.2A`, ADR-0146). Optional and nullable, defaulting to
    /// <see langword="null"/>, so a hand-assembled test context keeps
    /// working unchanged; without it, execution behaves exactly as before
    /// and writes no audit row.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="documentStore"/> or <paramref name="currentPrincipalAccessor"/> is <see langword="null"/>.</exception>
    public CalculationEngine(
        IEngineeringDocumentStore documentStore,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        ILogger? logger = null,
        IPersistenceStore? recordIndex = null,
        IAuditRecorder? auditRecorder = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);

        _documentStore = documentStore;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _logger = logger;
        _recordIndex = recordIndex;
        _auditRecorder = auditRecorder;
    }

    /// <inheritdoc />
    public void RegisterDefinition<TInput, TResult>(ICalculationDefinition<TInput, TResult> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.CalculationId);

        if (!_definitions.TryAdd(definition.CalculationId, definition))
            throw new DuplicateCalculationException(definition.CalculationId);

        _logger?.Information($"Calculation registered: '{definition.CalculationId}'.");
    }

    /// <inheritdoc />
    public async Task<CalculationRecord<TResult>> ExecuteAsync<TInput, TResult>(
        string calculationId, TInput input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(calculationId);

        if (!_definitions.TryGetValue(calculationId, out var boxed) || boxed is not ICalculationDefinition<TInput, TResult> definition)
            throw new CalculationDefinitionNotFoundException(calculationId);

        var context = new CalculationContext();
        var result = definition.Calculate(input, context);

        var executedAt = DateTimeOffset.UtcNow;
        var executedBy = ResolveExecutorPrincipalId();
        var validation = BuildValidationResult(context);

        var dto = new CalculationRecordDto<TResult>(
            calculationId, result, definition.Metadata.Assumptions, context.IntermediateResults,
            validation, context.ReferencedMaterialIds, executedAt, executedBy, typeof(TResult).FullName);

        var document = await _documentStore.CreateAsync(CalculationRecordDocumentKind, JsonSerializer.Serialize(dto), cancellationToken)
            .ConfigureAwait(false);

        if (_recordIndex is not null)
        {
            // Value is the calculation Id so a listing can name the type
            // without opening every document. Written after the document is
            // durable: an index entry pointing at nothing would be worse
            // than a record the listing cannot see, and the record itself is
            // still findable by Id either way.
            await _recordIndex
                .WriteAsync(RecordIndexCollection, document.Id.ToString("N"), calculationId, cancellationToken)
                .ConfigureAwait(false);
        }

        _logger?.Information($"Calculation executed: '{calculationId}' (document '{document.Id}').");

        if (_auditRecorder is not null)
        {
            await _auditRecorder.RecordAsync(
                CalculationExecutedActionName,
                new Dictionary<string, string>
                {
                    ["Subject"] = document.Id.ToString(),
                    ["CalculationId"] = calculationId,
                },
                cancellationToken).ConfigureAwait(false);
        }

        return new CalculationRecord<TResult>(
            document.Id, calculationId, result, definition.Metadata.Assumptions, context.IntermediateResults,
            validation, context.ReferencedMaterialIds, executedAt, executedBy, document.CurrentRevisionNumber);
    }

    /// <inheritdoc />
    public async Task<CalculationRecord<TResult>?> FindRecordAsync<TResult>(
        Guid recordId, CancellationToken cancellationToken = default)
    {
        var document = await _documentStore.FindAsync(recordId, cancellationToken).ConfigureAwait(false);

        if (document is null)
            return null;

        if (!string.Equals(document.Kind, CalculationRecordDocumentKind, StringComparison.Ordinal))
        {
            throw new CalculationException(
                $"Document '{recordId}' is a '{document.Kind}', not a {CalculationRecordDocumentKind}.");
        }

        var revisions = await _documentStore.GetRevisionHistoryAsync(recordId, cancellationToken).ConfigureAwait(false);

        if (revisions.Count == 0)
            return null;

        CalculationRecordDto<TResult>? dto;

        try
        {
            dto = JsonSerializer.Deserialize<CalculationRecordDto<TResult>>(revisions[^1].Content);
        }
        catch (JsonException exception)
        {
            // A stored record that will not deserialise is corruption or a
            // result-type mismatch. Either way it is reported rather than
            // returned as null, which a caller would read as "no such
            // calculation" and quietly move past.
            throw new CalculationException(
                $"Calculation record '{recordId}' could not be read as {typeof(TResult).Name}: {exception.Message}");
        }

        if (dto is null)
            throw new CalculationException($"Calculation record '{recordId}' deserialised to nothing.");

        // A record written by a different calculation deserialises happily
        // into the wrong result type, producing a well-formed object full of
        // defaults. Refuse it: a plausible-looking zero is the worst answer
        // an engineering tool can give.
        if (dto.ResultTypeName is { } storedType && !string.Equals(storedType, typeof(TResult).FullName, StringComparison.Ordinal))
        {
            throw new CalculationException(
                $"Calculation record '{recordId}' holds a '{storedType}' result and was asked for a "
                + $"'{typeof(TResult).FullName}'. Reading it as the requested type would return defaults "
                + "rather than an answer.");
        }

        return new CalculationRecord<TResult>(
            recordId,
            dto.CalculationId,
            dto.Result,
            dto.Assumptions,
            dto.IntermediateResults,
            dto.Validation,
            dto.ReferencedMaterialIds,
            dto.ExecutedAt,
            dto.ExecutedByPrincipalId,
            revisions[^1].RevisionNumber);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalculationRecordSummary>> ListRecordsAsync(CancellationToken cancellationToken = default)
    {
        if (_recordIndex is null)
            return [];

        var keys = await _recordIndex.ListKeysAsync(RecordIndexCollection, cancellationToken).ConfigureAwait(false);
        var summaries = new List<CalculationRecordSummary>(keys.Count);

        foreach (var key in keys)
        {
            if (!Guid.TryParseExact(key, "N", out var recordId))
                continue;

            var document = await _documentStore.FindAsync(recordId, cancellationToken).ConfigureAwait(false);

            // A stale index entry — its document gone, or of another Kind —
            // is skipped rather than aborting the whole listing, mirroring
            // the guard every reference catalogue's own ListAsync applies.
            if (document is null || !string.Equals(document.Kind, CalculationRecordDocumentKind, StringComparison.Ordinal))
                continue;

            var revisions = await _documentStore.GetRevisionHistoryAsync(recordId, cancellationToken).ConfigureAwait(false);

            if (revisions.Count == 0)
                continue;

            var summary = ReadSummary(recordId, revisions[^1].Content, document.CurrentRevisionNumber);

            if (summary is not null)
                summaries.Add(summary);
        }

        return [.. summaries.OrderByDescending(s => s.ExecutedAt)];
    }

    /// <summary>Reads the fields every record carries, whatever its result type is.</summary>
    /// <remarks>
    /// Parsed as a document rather than deserialised into
    /// <c>CalculationRecordDto&lt;TResult&gt;</c>, because a listing has no
    /// TResult to name. A record that cannot be parsed is omitted rather
    /// than throwing: one unreadable entry must not cost the engineer the
    /// whole list.
    /// </remarks>
    private static CalculationRecordSummary? ReadSummary(Guid recordId, string content, int revisionNumber)
    {
        try
        {
            using var parsed = JsonDocument.Parse(content);
            var root = parsed.RootElement;

            if (!root.TryGetProperty(nameof(CalculationRecord<object>.CalculationId), out var calculationId))
                return null;

            return new CalculationRecordSummary(
                recordId,
                calculationId.GetString() ?? string.Empty,
                root.TryGetProperty(nameof(CalculationRecord<object>.ExecutedAt), out var at) && at.TryGetDateTimeOffset(out var executedAt)
                    ? executedAt
                    : default,
                root.TryGetProperty(nameof(CalculationRecord<object>.ExecutedByPrincipalId), out var by)
                    ? by.GetString() ?? UnknownExecutorPrincipalId
                    : UnknownExecutorPrincipalId,
                revisionNumber,
                root.TryGetProperty("ResultTypeName", out var typeName) ? typeName.GetString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string ResolveExecutorPrincipalId() =>
        _currentPrincipalAccessor.Current?.Identity.Id ?? UnknownExecutorPrincipalId;

    private static CalculationValidationResult BuildValidationResult(CalculationContext context)
    {
        var outcome = context.ConstraintChecks.Any(check => !check.IsSatisfied)
            ? CalculationValidationOutcome.Conditional
            : CalculationValidationOutcome.Valid;

        return new CalculationValidationResult(outcome, context.ConstraintChecks);
    }
}
