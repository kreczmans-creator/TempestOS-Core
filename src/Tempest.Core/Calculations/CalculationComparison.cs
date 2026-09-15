namespace Tempest.Core.Calculations;

/// <summary>One field that differs between two compared records — its own name, and its old and new display values (units included, where the field is a quantity).</summary>
/// <param name="FieldName">The differing property's own name.</param>
/// <param name="OldDisplay">The first record's own value, formatted for display, or <see langword="null"/> if the field itself was <see langword="null"/>.</param>
/// <param name="NewDisplay">The second record's own value, formatted for display, or <see langword="null"/> if the field itself was <see langword="null"/>.</param>
public sealed record CalculationFieldDiff(string FieldName, string? OldDisplay, string? NewDisplay);

/// <summary>
/// A typed diff between two <see cref="CalculationRecord{TResult}"/>s
/// produced by the same calculation (`TD-29`) — which input and result
/// fields changed, old and new, with units where the field is a quantity.
/// </summary>
/// <param name="RecordIdA">The first record compared.</param>
/// <param name="RecordIdB">The second record compared.</param>
/// <param name="InputComparisonNote">
/// Explains why <see cref="InputChanges"/> is empty when it is not because
/// nothing changed — set when either record predates input retention
/// (`TD-29`) and carries no input. <see langword="null"/> when both
/// records had input to compare.
/// </param>
/// <param name="InputChanges">Every input field that differs between the two records. Empty if none do, or if <see cref="InputComparisonNote"/> explains why input could not be compared at all.</param>
/// <param name="ResultChanges">Every result field that differs between the two records. Empty if none do.</param>
public sealed record CalculationComparison(
    Guid RecordIdA,
    Guid RecordIdB,
    string? InputComparisonNote,
    IReadOnlyList<CalculationFieldDiff> InputChanges,
    IReadOnlyList<CalculationFieldDiff> ResultChanges);
