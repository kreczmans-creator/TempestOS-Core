using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `ADR-0083` addendum, `WP 20.3A`: the closed vocabulary a Bill of
/// Materials line's own <c>UnitOfMeasure</c> is validated and
/// canonicalised against.
/// </summary>
public class BomUnitsOfMeasureTests
{
    // ---- TryCanonicalise: leniency for a read ----

    [Theory]
    [InlineData("EA", "EA")]
    [InlineData("ea", "EA")]
    [InlineData("Each", "EA")]
    [InlineData(" ea ", "EA")] // surrounding whitespace ignored
    [InlineData("kg", "KG")]
    [InlineData("Kilogram", "KG")]
    [InlineData("m", "M")]
    [InlineData("Metre", "M")]
    [InlineData("Meter", "M")] // US spelling accepted as an alias
    public void TryCanonicalise_KnownAlias_ResolvesToTheOneCanonicalSymbol(string alias, string expectedSymbol)
    {
        Assert.True(BomUnitsOfMeasure.TryCanonicalise(alias, out var canonical));
        Assert.Equal(expectedSymbol, canonical);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("furlongs")]
    [InlineData("XYZ")]
    public void TryCanonicalise_UnknownOrBlank_ReturnsFalse_NeverThrows(string? value)
    {
        Assert.False(BomUnitsOfMeasure.TryCanonicalise(value, out var canonical));
        Assert.Null(canonical);
    }

    // ---- Canonicalise: refusal for a write ----

    [Fact]
    public void Canonicalise_KnownAlias_ReturnsTheCanonicalSymbol() =>
        Assert.Equal("EA", BomUnitsOfMeasure.Canonicalise("each"));

    [Fact]
    public void Canonicalise_UnknownUnit_ThrowsArgumentException_NamingTheValueAndTheKnownList()
    {
        var exception = Assert.Throws<ArgumentException>(() => BomUnitsOfMeasure.Canonicalise("furlongs"));

        Assert.Contains("furlongs", exception.Message, StringComparison.Ordinal);
        Assert.Contains("EA", exception.Message, StringComparison.Ordinal);
        Assert.Contains("KG", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Canonicalise_BlankValue_ThrowsArgumentException(string value) =>
        Assert.Throws<ArgumentException>(() => BomUnitsOfMeasure.Canonicalise(value));

    [Fact]
    public void Canonicalise_NullValue_ThrowsArgumentNullException() =>
        // ArgumentException.ThrowIfNullOrWhiteSpace's own documented shape:
        // ArgumentNullException specifically for null, ArgumentException
        // (the base type) for empty/whitespace - see the two Canonicalise_
        // BlankValue_ cases above.
        Assert.Throws<ArgumentNullException>(() => BomUnitsOfMeasure.Canonicalise(null!));

    // ---- Known: every alias reaches exactly one symbol, case-insensitively ----

    [Fact]
    public void Known_EveryDefinitionsOwnSymbol_IsAmongItsOwnAliases()
    {
        foreach (var definition in BomUnitsOfMeasure.Known)
            Assert.Contains(definition.Symbol, definition.Aliases);
    }

    [Fact]
    public void Known_NoAliasIsSharedAcrossTwoDefinitions()
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in BomUnitsOfMeasure.Known)
        {
            foreach (var alias in definition.Aliases)
            {
                Assert.True(
                    seen.TryAdd(alias, definition.Symbol) || seen[alias] == definition.Symbol,
                    $"Alias '{alias}' is claimed by both '{seen.GetValueOrDefault(alias)}' and '{definition.Symbol}'.");
            }
        }
    }

    [Fact]
    public void DescribeKnown_ListsEveryCanonicalSymbol()
    {
        var described = BomUnitsOfMeasure.DescribeKnown();

        foreach (var definition in BomUnitsOfMeasure.Known)
            Assert.Contains(definition.Symbol, described, StringComparison.Ordinal);
    }
}
