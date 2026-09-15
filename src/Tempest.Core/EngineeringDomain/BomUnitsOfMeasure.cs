namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The closed, small vocabulary a Bill of Materials line's own
/// <see cref="IHasBomLine.UnitOfMeasure"/> is validated and canonicalised
/// against (`ADR-0083` addendum, `WP 20.3A`) — deliberately separate from
/// <see cref="Tempest.Core.UnitsAndQuantities"/>'s own dimensional unit
/// catalogues (<c>ADR-0054</c>). A BOM count is not a physical dimension:
/// this vocabulary exists only so <c>"EA"</c>, <c>"ea"</c> and <c>"Each"</c>
/// canonicalise to the one unit they name, never to convert or
/// arithmetically combine a quantity the way <c>Quantity&lt;TDimension&gt;</c>
/// does for a calculation.
/// </summary>
/// <remarks>
/// Deliberately small and closed, per `ADR-0083`'s own disclosed Future
/// Capability ("a small closed vocabulary/lookup") rather than a
/// speculative catalogue: every entry is either one of `ADR-0083`'s own
/// worked examples (<c>"EA"</c>, <c>"M"</c>, <c>"KG"</c>) or an equally
/// ordinary mechanical/electrical BOM unit — count, length, mass, volume,
/// and the handful of packaging/labour units a consultancy's own BOM
/// lines and calc sheets actually use.
/// </remarks>
public static class BomUnitsOfMeasure
{
    /// <summary>One canonical unit of measure and every alternate spelling this platform recognises as naming it.</summary>
    /// <param name="Symbol">The canonical, stored form — what a recognised alias canonicalises to.</param>
    /// <param name="Name">The unit's own plain-English name.</param>
    /// <param name="Aliases">Every spelling — case-insensitive — this platform accepts as naming <paramref name="Symbol"/>, including <paramref name="Symbol"/> itself.</param>
    public sealed record Definition(string Symbol, string Name, IReadOnlyList<string> Aliases);

    /// <summary>Every known unit, canonical-symbol order.</summary>
    public static IReadOnlyList<Definition> Known { get; } =
    [
        new("EA", "Each", ["EA", "EACH"]),
        new("SET", "Set", ["SET", "SETS"]),
        new("PR", "Pair", ["PR", "PAIR", "PAIRS"]),
        new("BOX", "Box", ["BOX", "BOXES"]),
        new("ROLL", "Roll", ["ROLL", "ROLLS"]),
        new("SHT", "Sheet", ["SHT", "SHEET", "SHEETS"]),
        new("M", "Metre", ["M", "METRE", "METRES", "METER", "METERS"]),
        new("MM", "Millimetre", ["MM", "MILLIMETRE", "MILLIMETRES", "MILLIMETER", "MILLIMETERS"]),
        new("KG", "Kilogram", ["KG", "KILOGRAM", "KILOGRAMS", "KILOGRAMME", "KILOGRAMMES"]),
        new("G", "Gram", ["G", "GRAM", "GRAMS", "GRAMME", "GRAMMES"]),
        new("L", "Litre", ["L", "LITRE", "LITRES", "LITER", "LITERS"]),
        new("HR", "Hour", ["HR", "HOUR", "HOURS"]),
    ];

    private static readonly IReadOnlyDictionary<string, string> SymbolByAlias = BuildAliasLookup();

    private static IReadOnlyDictionary<string, string> BuildAliasLookup()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in Known)
            foreach (var alias in definition.Aliases)
                map[alias] = definition.Symbol;

        return map;
    }

    /// <summary>
    /// Resolves <paramref name="value"/> — any known alias, in any case,
    /// with surrounding whitespace ignored — to its one canonical
    /// <see cref="Definition.Symbol"/>: <c>"ea"</c>, <c>"EA"</c> and
    /// <c>"Each"</c> all resolve to <c>"EA"</c>. Never throws:
    /// <see langword="null"/>, empty, whitespace-only, or a string this
    /// vocabulary does not recognise all simply return
    /// <see langword="false"/> — the right shape for a read of an
    /// already-stored value, which must never refuse.
    /// </summary>
    public static bool TryCanonicalise(string? value, out string? canonical)
    {
        if (!string.IsNullOrWhiteSpace(value) && SymbolByAlias.TryGetValue(value.Trim(), out var found))
        {
            canonical = found;
            return true;
        }

        canonical = null;
        return false;
    }

    /// <summary>
    /// Resolves <paramref name="value"/> to its canonical symbol for a BOM
    /// line write (<see cref="IHasBomLine.SetBomLineAsync"/>), or throws,
    /// naming every known unit — never for a read of an already-stored
    /// value, which <see cref="TryCanonicalise"/> handles leniently
    /// instead. Refused as an exception, not a refusal result, mirroring
    /// <see cref="Tempest.Core.Evidence.EvidenceUnitCatalog.Parse"/>'s own
    /// distinction: an unrecognised unit is a caller programming/typing
    /// error, not an engineering-governance finding a surface shows in its
    /// status bar.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is null, blank, or does not name a known unit.</exception>
    public static string Canonicalise(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (TryCanonicalise(value, out var canonical))
            return canonical!;

        throw new ArgumentException(
            $"'{value}' is not a unit of measure this platform knows. Use one of: {DescribeKnown()}.", nameof(value));
    }

    /// <summary>Every canonical symbol, comma-separated, for an unknown-unit refusal message.</summary>
    public static string DescribeKnown() => string.Join(", ", Known.Select(d => d.Symbol));
}
