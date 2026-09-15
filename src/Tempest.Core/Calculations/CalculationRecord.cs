namespace Tempest.Core.Calculations;

/// <summary>
/// An immutable record of one calculation's execution — engineering
/// evidence, not merely a numerical answer: every assumption the
/// calculation relied on, every intermediate value it computed, its own
/// validation outcome, and every material it referenced all travel with
/// the final result.
/// </summary>
/// <param name="Id">
/// This record's own stable identity — also the Id of the
/// <c>EngineeringData.IEngineeringDocument</c> this record is durably
/// stored as, usable directly with
/// <c>EngineeringData.IEngineeringDocumentStore</c> for revision history
/// or typed references this framework does not itself duplicate.
/// </param>
/// <param name="CalculationId">The <see cref="ICalculationDefinition{TInput, TResult}.CalculationId"/> that produced this record.</param>
/// <param name="Result">The calculated result.</param>
/// <param name="Assumptions">Every assumption the producing definition's own <see cref="CalculationMetadata"/> declared, copied at execution time so this record remains self-contained evidence.</param>
/// <param name="IntermediateResults">Every intermediate value the definition recorded while computing <paramref name="Result"/>. Never <see langword="null"/>; empty if none were recorded.</param>
/// <param name="Validation">This execution's own validation outcome.</param>
/// <param name="ReferencedMaterialIds">Every material Id the definition recorded as referenced during this execution. Never <see langword="null"/>; empty if none.</param>
/// <param name="ExecutedAt">When this calculation was executed.</param>
/// <param name="ExecutedByPrincipalId">Who executed this calculation.</param>
/// <param name="RevisionNumber">The underlying document's own current revision number — always <c>1</c> for a record <see cref="ICalculationEngine.ExecuteAsync{TInput, TResult}"/> has just produced, since each execution creates a fresh record rather than revising an existing one.</param>
/// <param name="Input">
/// The input this calculation was run with (`TD-29`) — the real CLR
/// <c>TInput</c> object for a record just returned by
/// <see cref="ICalculationEngine.ExecuteAsync{TInput, TResult}"/>, a boxed
/// <see cref="System.Text.Json.JsonElement"/> for one read back by
/// <see cref="ICalculationEngine.FindRecordAsync{TResult}"/> (read it back
/// with <see cref="ICalculationEngine.ReRunAsync{TInput, TResult}(Guid, CancellationToken)"/>
/// or <see cref="CalculationComparer.Compare{TInput, TResult}"/>, never a
/// direct cast). <see langword="null"/> for a record executed before this
/// field existed — never thrown for; a caller comparing or re-running such
/// a record is told so rather than failing on a cast.
/// </param>
/// <param name="InputTypeName">The full name of <typeparamref name="TResult"/>'s own sibling <c>TInput</c> type this record was executed with, or <see langword="null"/> alongside a <see langword="null"/> <paramref name="Input"/>.</param>
/// <param name="PredecessorRecordId">The record this one was re-run from (`TD-29`), via <see cref="ICalculationEngine.ReRunAsync{TInput, TResult}(Guid, CancellationToken)"/> or its changed-input overload — <see langword="null"/> for a record produced by a first <see cref="ICalculationEngine.ExecuteAsync{TInput, TResult}"/> execution.</param>
public sealed record CalculationRecord<TResult>(
    Guid Id,
    string CalculationId,
    TResult Result,
    IReadOnlyList<CalculationAssumption> Assumptions,
    IReadOnlyList<CalculationIntermediateResult> IntermediateResults,
    CalculationValidationResult Validation,
    IReadOnlyList<string> ReferencedMaterialIds,
    DateTimeOffset ExecutedAt,
    string ExecutedByPrincipalId,
    int RevisionNumber,
    object? Input = null,
    string? InputTypeName = null,
    Guid? PredecessorRecordId = null);
