using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>
/// The canonical engineering description of one constant: its symbol, its
/// dimensioned value, how well that value is known, and where it applies.
/// </summary>
/// <remarks>
/// <para>
/// <b>The value is a dimensioned quantity, always.</b> A constant recorded
/// as a bare number is the single most dangerous thing a reference library
/// can hold: it invites use in the wrong unit system and gives nothing
/// that could catch the mistake. <see cref="Value"/> is therefore a
/// <see cref="ReferenceQuantityValue"/> — a quantity of whatever dimension
/// the constant has, boxed through the shared codec because that dimension
/// varies from record to record, which is one of the two cases the codec
/// exists for (`ADR-0124`). Mathematical constants are dimensionless
/// quantities, not bare doubles.
/// </para>
/// <para>
/// <b>Where a constant applies is part of the constant.</b> A conventional
/// reference value is exact within the convention that adopted it and true
/// of nowhere in particular; recording the number without the convention
/// makes it look universal. <see cref="Applicability"/> is warned about
/// when a category expects one.
/// </para>
/// <para>
/// Carries no TempestOS identity, no provenance, no validation state and
/// no revision number — those belong to the registered record
/// (<see cref="IReferenceRecord{TDefinition}"/>).
/// </para>
/// </remarks>
public sealed record ConstantDefinition
{
    /// <summary>
    /// The symbol the constant is known and looked up by. Required, and
    /// unique across the whole library.
    /// </summary>
    /// <remarks>
    /// Uniqueness is enforced on the symbol alone rather than on symbol and
    /// category together, deliberately: a calculation asking for a symbol
    /// must get exactly one answer, and a library that could return two
    /// would be worse than one that returns none. Where two constants
    /// genuinely share a symbol in the literature, whoever records them
    /// disambiguates — and <see cref="AlternativeSymbols"/> keeps the
    /// original wording of each.
    /// </remarks>
    public required string Symbol { get; init; }

    /// <summary>The constant's own name. Required.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The constant's own value, as a quantity of whatever dimension it
    /// has. <see langword="null"/> only where a record has been created
    /// before its value was transcribed, which validation reports as an
    /// error rather than tolerating.
    /// </summary>
    public ReferenceQuantityValue? Value { get; init; }

    /// <summary>How well the value is known. Never <see langword="null"/>; defaults to not recorded, never to exact.</summary>
    public ConstantUncertainty Uncertainty { get; init; } = ConstantUncertainty.NotRecorded;

    /// <summary>What kind of constant this is.</summary>
    public ConstantCategory Category { get; init; } = ConstantCategory.Unspecified;

    /// <summary>
    /// Where and under what convention the constant applies, in the
    /// source's own terms. <see langword="null"/> where the source stated
    /// none.
    /// </summary>
    public string? Applicability { get; init; }

    /// <summary>Other symbols the same constant appears under, kept so a search on any of them finds it. Never <see langword="null"/>; empty if none.</summary>
    public IReadOnlyList<string> AlternativeSymbols { get; init; } = [];

    /// <summary>
    /// The constant's own defining relationship or wording, where the
    /// source gives one. Recorded verbatim as description — never parsed,
    /// never evaluated. <see langword="null"/> if none was given.
    /// </summary>
    public string? DefiningStatement { get; init; }

    /// <summary>Every standard or publication this record's own value is stated against. Never <see langword="null"/>; empty if none is cited.</summary>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>
    /// The source's own classification wording, verbatim — the honest home
    /// for a category this taxonomy classifies as
    /// <see cref="ConstantCategory.Other"/>. <see langword="null"/> if the
    /// source gave none.
    /// </summary>
    public string? SourceClassification { get; init; }

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>The date from which this record's own value is effective, where the source states one — the date an adjustment took effect. <see langword="null"/> if it does not.</summary>
    public DateOnly? EffectiveDate { get; init; }

    /// <summary>The key symbol uniqueness is enforced on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string SymbolKey => SymbolKeyFor(Symbol);

    /// <summary>
    /// Builds the uniqueness key from a symbol that is not (yet) a record
    /// — the lookup path.
    /// </summary>
    /// <remarks>
    /// Whitespace is trimmed but case is <b>not</b> folded: a constant's
    /// symbol is case-significant, and treating an upper-case and a
    /// lower-case symbol as one would silently merge two different
    /// constants.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty, or whitespace.</exception>
    public static string SymbolKeyFor(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        return symbol.Trim();
    }
}
