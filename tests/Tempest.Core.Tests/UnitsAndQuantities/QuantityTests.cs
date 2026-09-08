using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

public class QuantityTests
{
    // ----------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_ValidValue_SetsProperties()
    {
        var quantity = new Quantity<Length>(5.0, LengthUnits.Metre);

        Assert.Equal(5.0, quantity.Value);
        Assert.Equal(LengthUnits.Metre, quantity.Unit);
    }

    [Fact]
    public void Constructor_ZeroValue_IsAccepted()
    {
        var quantity = new Quantity<Length>(0.0, LengthUnits.Metre);

        Assert.Equal(0.0, quantity.Value);
    }

    [Fact]
    public void Constructor_NegativeValue_IsAccepted()
    {
        var quantity = new Quantity<Length>(-5.0, LengthUnits.Metre);

        Assert.Equal(-5.0, quantity.Value);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_NonFiniteValue_ThrowsArgumentOutOfRangeException(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity<Length>(value, LengthUnits.Metre));
    }

    // ----------------------------------------------------------------
    // ConvertTo — round-trip correctness
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(5.0)]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(1e-9)]
    [InlineData(1e12)]
    public void ConvertTo_ThenBack_RecoversOriginalValue_WithinFloatingPointTolerance(double value)
    {
        var original = new Quantity<Length>(value, LengthUnits.Metre);

        var roundTripped = original.ConvertTo(LengthUnits.Foot).ConvertTo(LengthUnits.Metre);

        Assert.Equal(original.Value, roundTripped.Value, precision: 9);
    }

    [Fact]
    public void ConvertTo_MetreToFoot_ProducesExpectedValue()
    {
        var oneMetre = new Quantity<Length>(1.0, LengthUnits.Metre);

        var inFeet = oneMetre.ConvertTo(LengthUnits.Foot);

        Assert.Equal(1.0 / 0.3048, inFeet.Value, precision: 9);
        Assert.Equal(LengthUnits.Foot, inFeet.Unit);
    }

    [Fact]
    public void ConvertTo_SameUnit_ReturnsEquivalentValue()
    {
        var quantity = new Quantity<Length>(42.0, LengthUnits.Metre);

        var converted = quantity.ConvertTo(LengthUnits.Metre);

        Assert.Equal(42.0, converted.Value);
    }

    [Fact]
    public void ConvertTo_SiToImperial_MassRoundTrips()
    {
        var oneKilogram = new Quantity<Mass>(1.0, MassUnits.Kilogram);

        var roundTripped = oneKilogram.ConvertTo(MassUnits.Pound).ConvertTo(MassUnits.Kilogram);

        Assert.Equal(1.0, roundTripped.Value, precision: 9);
    }

    // ----------------------------------------------------------------
    // Arithmetic
    // ----------------------------------------------------------------

    [Fact]
    public void Addition_SameUnit_SumsValues()
    {
        var a = new Quantity<Length>(2.0, LengthUnits.Metre);
        var b = new Quantity<Length>(3.0, LengthUnits.Metre);

        var sum = a + b;

        Assert.Equal(5.0, sum.Value);
        Assert.Equal(LengthUnits.Metre, sum.Unit);
    }

    [Fact]
    public void Subtraction_SameUnit_SubtractsValues()
    {
        var a = new Quantity<Length>(5.0, LengthUnits.Metre);
        var b = new Quantity<Length>(3.0, LengthUnits.Metre);

        var difference = a - b;

        Assert.Equal(2.0, difference.Value);
    }

    // ADR-0147 reverses ADR-0054's original "exact same unit only" rule:
    // quantities of the same dimension now convert automatically, through
    // the base unit and back to the left operand's own unit.

    [Fact]
    public void Addition_DifferentUnits_ConvertsAutomatically_AndReturnsResultInLeftOperandsUnit()
    {
        var oneMetre = new Quantity<Length>(1.0, LengthUnits.Metre);
        var oneFoot = new Quantity<Length>(1.0, LengthUnits.Foot);

        var sum = oneMetre + oneFoot;

        Assert.Equal(1.3048, sum.Value, precision: 9);
        Assert.Equal(LengthUnits.Metre, sum.Unit);
    }

    [Fact]
    public void Subtraction_DifferentUnits_ConvertsAutomatically_AndReturnsResultInLeftOperandsUnit()
    {
        var threeFeet = new Quantity<Length>(3.0, LengthUnits.Foot);
        var oneMetre = new Quantity<Length>(1.0, LengthUnits.Metre);

        var difference = threeFeet - oneMetre;

        Assert.Equal(3.0 - (1.0 / 0.3048), difference.Value, precision: 9);
        Assert.Equal(LengthUnits.Foot, difference.Unit);
    }

    [Fact]
    public void ScalarMultiplication_ScalesValue_PreservesUnit()
    {
        var quantity = new Quantity<Length>(2.0, LengthUnits.Metre);

        Assert.Equal(6.0, (quantity * 3.0).Value);
        Assert.Equal(6.0, (3.0 * quantity).Value);
        Assert.Equal(LengthUnits.Metre, (quantity * 3.0).Unit);
    }

    [Fact]
    public void ScalarDivision_ScalesValue_PreservesUnit()
    {
        var quantity = new Quantity<Length>(6.0, LengthUnits.Metre);

        var result = quantity / 3.0;

        Assert.Equal(2.0, result.Value);
        Assert.Equal(LengthUnits.Metre, result.Unit);
    }

    [Fact]
    public void ScalarDivision_ByZero_ThrowsArgumentOutOfRangeException()
    {
        var quantity = new Quantity<Length>(6.0, LengthUnits.Metre);

        Assert.Throws<ArgumentOutOfRangeException>(() => quantity / 0.0);
    }

    // ----------------------------------------------------------------
    // Comparison
    // ----------------------------------------------------------------

    [Fact]
    public void CompareTo_SameUnit_OrdersByValue()
    {
        var small = new Quantity<Length>(1.0, LengthUnits.Metre);
        var equalToSmall = new Quantity<Length>(1.0, LengthUnits.Metre);
        var large = new Quantity<Length>(2.0, LengthUnits.Metre);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.True(small <= equalToSmall);
        Assert.True(small >= equalToSmall);
    }

    [Fact]
    public void CompareTo_DifferentUnits_ComparesByBaseValue()
    {
        // 1 m is greater than 1 ft: ADR-0147's own same-dimension
        // automatic conversion applies to comparison too.
        var oneMetre = new Quantity<Length>(1.0, LengthUnits.Metre);
        var oneFoot = new Quantity<Length>(1.0, LengthUnits.Foot);

        Assert.True(oneMetre.CompareTo(oneFoot) > 0);
        Assert.True(oneFoot < oneMetre);
        Assert.True(oneMetre > oneFoot);
    }

    // ----------------------------------------------------------------
    // Equality
    // ----------------------------------------------------------------

    [Fact]
    public void Equality_SameValueAndUnit_AreEqual()
    {
        var a = new Quantity<Length>(5.0, LengthUnits.Metre);
        var b = new Quantity<Length>(5.0, LengthUnits.Metre);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_PhysicallyEquivalentButDifferentUnit_AreEqual()
    {
        // ADR-0147: equality compares BaseValue, so 5 m and 500 cm — the
        // same physical length written two ways — are now equal.
        var fiveHundredCentimetres = new Quantity<Length>(500.0, LengthUnits.Centimetre);
        var fiveMetres = new Quantity<Length>(5.0, LengthUnits.Metre);

        Assert.Equal(fiveMetres, fiveHundredCentimetres);
        Assert.True(fiveMetres == fiveHundredCentimetres);
        Assert.Equal(fiveMetres.GetHashCode(), fiveHundredCentimetres.GetHashCode());
    }

    // ----------------------------------------------------------------
    // Formatting
    // ----------------------------------------------------------------

    [Fact]
    public void ToString_ProducesValueAndSymbol()
    {
        var quantity = new Quantity<Length>(5.0, LengthUnits.Metre);

        Assert.Equal("5 m", quantity.ToString());
    }

    [Fact]
    public void ToString_WithFormat_AppliesFormatToValue()
    {
        var quantity = new Quantity<Length>(5.0, LengthUnits.Metre);

        Assert.Equal("5.00 m", quantity.ToString("F2", null));
    }

    [Fact]
    public void ToString_IsCultureInvariant()
    {
        var quantity = new Quantity<Length>(1234.5, LengthUnits.Metre);

        var result = quantity.ToString("F1", new System.Globalization.CultureInfo("de-DE"));

        Assert.Equal("1234.5 m", result);
    }
}
