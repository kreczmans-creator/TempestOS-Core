using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

public class QuantityParsingTests
{
    [Theory]
    [InlineData("5 m", 5.0, "m")]
    [InlineData("-5 m", -5.0, "m")]
    [InlineData("0 m", 0.0, "m")]
    [InlineData("  5   ft", 5.0, "ft")]
    [InlineData("1.5e3 mm", 1500.0, "mm")]
    public void TryParse_RecognisedInput_ReturnsExpectedQuantity(string input, double expectedValue, string expectedSymbol)
    {
        var succeeded = Quantity<Length>.TryParse(input, LengthUnits.All, out var result);

        Assert.True(succeeded);
        Assert.Equal(expectedValue, result.Value);
        Assert.Equal(expectedSymbol, result.Unit.Symbol);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("5")]
    [InlineData("m")]
    [InlineData("5 furlongs")]
    [InlineData("five m")]
    public void TryParse_UnrecognisedInput_ReturnsFalse(string? input)
    {
        var succeeded = Quantity<Length>.TryParse(input, LengthUnits.All, out var result);

        Assert.False(succeeded);
        Assert.Equal(default, result);
    }

    [Fact]
    public void Parse_RecognisedInput_ReturnsQuantity()
    {
        var result = Quantity<Length>.Parse("5 m", LengthUnits.All);

        Assert.Equal(5.0, result.Value);
        Assert.Equal(LengthUnits.Metre, result.Unit);
    }

    [Fact]
    public void Parse_UnrecognisedInput_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Quantity<Length>.Parse("nonsense", LengthUnits.All));
    }

    [Fact]
    public void TryParse_FormatThenParse_RoundTrips()
    {
        var original = new Quantity<Length>(12.5, LengthUnits.Kilometre);

        var succeeded = Quantity<Length>.TryParse(original.ToString(), LengthUnits.All, out var parsed);

        Assert.True(succeeded);
        Assert.Equal(original, parsed);
    }
}
