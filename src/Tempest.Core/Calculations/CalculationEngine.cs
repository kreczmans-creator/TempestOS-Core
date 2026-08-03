using System.Collections.Concurrent;
using System.Text.Json;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
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

    private readonly ConcurrentDictionary<string, object> _definitions = new();
    private readonly IEngineeringDocumentStore _documentStore;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationEngine"/> class.
    /// </summary>
    /// <param name="documentStore">The store this instance's own calculation records are durably held in.</param>
    /// <param name="currentPrincipalAccessor">The service this instance resolves the acting principal from.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException"><paramref name="documentStore"/> or <paramref name="currentPrincipalAccessor"/> is <see langword="null"/>.</exception>
    public CalculationEngine(IEngineeringDocumentStore documentStore, ICurrentPrincipalAccessor currentPrincipalAccessor, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);

        _documentStore = documentStore;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _logger = logger;
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
            validation, context.ReferencedMaterialIds, executedAt, executedBy);

        var document = await _documentStore.CreateAsync(CalculationRecordDocumentKind, JsonSerializer.Serialize(dto), cancellationToken)
            .ConfigureAwait(false);

        _logger?.Information($"Calculation executed: '{calculationId}' (document '{document.Id}').");

        return new CalculationRecord<TResult>(
            document.Id, calculationId, result, definition.Metadata.Assumptions, context.IntermediateResults,
            validation, context.ReferencedMaterialIds, executedAt, executedBy, document.CurrentRevisionNumber);
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
