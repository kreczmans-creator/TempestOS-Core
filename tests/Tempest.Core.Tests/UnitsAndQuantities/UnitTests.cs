using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

public class UnitTests
{
    [Fact]
    public void Constructor_ValidArguments_SetsProperties()
    {
        var unit = new Unit<Length>("m", 1.0);

        Assert.Equal("m", unit.Symbol);
        Assert.Equal(1.0, unit.ToBaseUnitFactor);
    }

    [Fact]
    public void Constructor_NullSymbol_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Unit<Length>(null!, 1.0));
    }

    [Fact]
    public void Constructor_WhitespaceSymbol_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Unit<Length>("   ", 1.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_NonPositiveOrNonFiniteFactor_ThrowsArgumentOutOfRangeException(double factor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Unit<Length>("m", factor));
    }

    [Fact]
    public void Equality_SameSymbolAndFactor_AreEqual()
    {
        var a = new Unit<Length>("m", 1.0);
        var b = new Unit<Length>("m", 1.0);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentFactor_AreNotEqual()
    {
        var a = new Unit<Length>("m", 1.0);
        var b = new Unit<Length>("m", 1.001);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }
}
