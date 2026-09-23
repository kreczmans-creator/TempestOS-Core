using System.Globalization;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

// Tests for the non-generic, runtime-dimensioned `Quantity` struct that
// lives in QuantityVector.cs -- the ADR-0147 counterpart to
// Quantity<TDimension> (tested in QuantityTests.cs). Named after its own
// source file so the two are easy to tell apart. WP 21.5D: this type had
// no dedicated test file at all before this one.
public class QuantityVectorTests
{
    // ----------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_ValidValue_SetsProperties()
    {
        var quantity = new Quantity(5.0, LengthUnits.Metre.UnitDefinition);

        Assert.Equal(5.0, quantity.Value);
        Assert.Equal(LengthUnits.Metre.UnitDefinition, quantity.Unit);
    }

    [Fact]
    public void Constructor_ZeroValue_IsAccepted() =>
        Assert.Equal(0.0, new Quantity(0.0, LengthUnits.Metre.UnitDefinition).Value);

    [Fact]
    public void Constructor_NegativeValue_IsAccepted() =>
        Assert.Equal(-5.0, new Quantity(-5.0, LengthUnits.Metre.UnitDefinition).Value);

    [Fact]
    public void Constructor_NaN_ThrowsArgumentOutOfRangeException_WithAMessageAboutFiniteness()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Quantity(double.NaN, LengthUnits.Metre.UnitDefinition));

        Assert.Contains("finite number", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_PositiveInfinity_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(double.PositiveInfinity, LengthUnits.Metre.UnitDefinition));

    [Fact]
    public void Constructor_NegativeInfinity_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(double.NegativeInfinity, LengthUnits.Metre.UnitDefinition));

    // ----------------------------------------------------------------
    // BaseValue
    // ----------------------------------------------------------------

    [Fact]
    public void BaseValue_NonUnityFactor_ScalesByFactor()
    {
        var quantity = new Quantity(2.0, LengthUnits.Foot.UnitDefinition);

        Assert.Equal(0.6096, quantity.BaseValue, 9);
    }

    // ----------------------------------------------------------------
    // ConvertTo
    // ----------------------------------------------------------------

    [Fact]
    public void ConvertTo_DifferentDimension_ThrowsIncompatibleUnitsException()
    {
        var length = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);

        Assert.Throws<IncompatibleUnitsException>(() => length.ConvertTo(MassUnits.Kilogram.UnitDefinition));
    }

    [Fact]
    public void ConvertTo_SameUnit_ReturnsExactSameValue_NoConversionRoundingApplied()
    {
        // The fast path (`if (Unit == targetUnit) return this;`) is only
        // observable against a non-unity factor, where the general
        // ToBase/FromBase round trip could otherwise differ from the
        // stored value at the bit level.
        var quantity = new Quantity(1.0, LengthUnits.Foot.UnitDefinition);

        var converted = quantity.ConvertTo(LengthUnits.Foot.UnitDefinition);

        Assert.Equal(quantity.Value, converted.Value); // bit-exact, no tolerance
    }

    [Fact]
    public void ConvertTo_DifferentUnit_SameDimension_ProducesExpectedValue()
    {
        var oneMetre = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);

        var inFeet = oneMetre.ConvertTo(LengthUnits.Foot.UnitDefinition);

        Assert.Equal(1.0 / 0.3048, inFeet.Value, 9);
        Assert.Equal(LengthUnits.Foot.UnitDefinition, inFeet.Unit);
    }

    // ----------------------------------------------------------------
    // Addition / subtraction -- non-affine
    // ----------------------------------------------------------------

    [Fact]
    public void Addition_SameUnit_SumsValues()
    {
        var a = new Quantity(2.0, LengthUnits.Metre.UnitDefinition);
        var b = new Quantity(3.0, LengthUnits.Metre.UnitDefinition);

        var sum = a + b;

        Assert.Equal(5.0, sum.Value);
        Assert.Equal(LengthUnits.Metre.UnitDefinition, sum.Unit);
    }

    [Fact]
    public void Addition_SameNonUnityUnit_TakesTheFastPath_NoRoundTripRoundingApplied()
    {
        // Exercises the `left.Unit == right.Unit` branch of the internal
        // ternary against a factor/value combination where the general
        // FromBase(ToBase(...)) round trip is not bit-exact — verified
        // empirically (7.0 through the 0.3048 Foot factor loses a ulp;
        // smaller integers do not).
        var a = new Quantity(1.0, LengthUnits.Foot.UnitDefinition);
        var b = new Quantity(7.0, LengthUnits.Foot.UnitDefinition);

        Assert.Equal(8.0, (a + b).Value); // bit-exact
    }

    [Fact]
    public void Addition_DifferentUnits_ConvertsAutomatically_AndReturnsResultInLeftOperandsUnit()
    {
        var oneMetre = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);
        var oneFoot = new Quantity(1.0, LengthUnits.Foot.UnitDefinition);

        var sum = oneMetre + oneFoot;

        Assert.Equal(1.3048, sum.Value, 9);
        Assert.Equal(LengthUnits.Metre.UnitDefinition, sum.Unit);
    }

    [Fact]
    public void Subtraction_SameUnit_SubtractsValues()
    {
        var a = new Quantity(5.0, LengthUnits.Metre.UnitDefinition);
        var b = new Quantity(3.0, LengthUnits.Metre.UnitDefinition);

        Assert.Equal(2.0, (a - b).Value);
    }

    [Fact]
    public void Subtraction_DifferentUnits_ConvertsAutomatically()
    {
        var threeFeet = new Quantity(3.0, LengthUnits.Foot.UnitDefinition);
        var oneMetre = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);

        var difference = threeFeet - oneMetre;

        Assert.Equal(3.0 - (1.0 / 0.3048), difference.Value, 9);
        Assert.Equal(LengthUnits.Foot.UnitDefinition, difference.Unit);
    }

    [Fact]
    public void AddOrSubtract_DifferentDimensions_ThrowsIncompatibleUnitsException()
    {
        var length = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);
        var mass = new Quantity(1.0, MassUnits.Kilogram.UnitDefinition);

        Assert.Throws<IncompatibleUnitsException>(() => length + mass);
        Assert.Throws<IncompatibleUnitsException>(() => length - mass);
    }

    [Fact]
    public void RequireSameDimension_Message_NamesBothDimensions()
    {
        var length = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);
        var mass = new Quantity(1.0, MassUnits.Kilogram.UnitDefinition);

        var exception = Assert.Throws<IncompatibleUnitsException>(() => length.ConvertTo(MassUnits.Kilogram.UnitDefinition));

        Assert.Contains("not the same physical dimension", exception.Message, StringComparison.Ordinal);
        Assert.Contains(length.Unit.Dimension.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(mass.Unit.Dimension.ToString(), exception.Message, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // Addition / subtraction -- affine (this type's own two carve-outs
    // beyond Quantity<TDimension>'s flat refusal; see its own remarks)
    // ----------------------------------------------------------------

    [Fact]
    public void Addition_BothAffine_Throws_WithAMessageNamingTheProblem()
    {
        var a = new Quantity(20.0, TemperatureUnits.DegreeCelsius.UnitDefinition);
        var b = new Quantity(5.0, TemperatureUnits.DegreeCelsius.UnitDefinition);

        var exception = Assert.Throws<IncompatibleUnitsException>(() => a + b);
        Assert.Contains("affine scale", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtraction_BothAffine_ReturnsATemperatureDeltaInKelvin()
    {
        var hot = new Quantity(20.0, TemperatureUnits.DegreeCelsius.UnitDefinition);
        var cold = new Quantity(5.0, TemperatureUnits.DegreeCelsius.UnitDefinition);

        var delta = hot - cold;

        // BaseValue(20 degC) - BaseValue(5 degC) = 293.15 - 278.15 = 15.
        Assert.Equal(15.0, delta.Value, 9);
        Assert.Equal(TemperatureDeltaUnits.Kelvin.UnitDefinition, delta.Unit);
    }

    [Fact]
    public void Addition_LeftAffine_RightDelta_TreatsTheDeltaAsAnOffset_ResultInTheAffineOperandsUnit()
    {
        var absolute = new Quantity(20.0, TemperatureUnits.DegreeCelsius.UnitDefinition);
        var delta = new Quantity(10.0, TemperatureDeltaUnits.Kelvin.UnitDefinition);

        var sum = absolute + delta;

        Assert.Equal(30.0, sum.Value, 9);
        Assert.Equal(TemperatureUnits.DegreeCelsius.UnitDefinition, sum.Unit);
    }

    [Fact]
    public void Addition_LeftDelta_RightAffine_TreatsTheDeltaAsAnOffset_ResultInTheAffineOperandsUnit()
    {
        var delta = new Quantity(10.0, TemperatureDeltaUnits.Kelvin.UnitDefinition);
        var absolute = new Quantity(20.0, TemperatureUnits.DegreeCelsius.UnitDefinition);

        var sum = delta + absolute;

        Assert.Equal(30.0, sum.Value, 9);
        Assert.Equal(TemperatureUnits.DegreeCelsius.UnitDefinition, sum.Unit);
    }

    [Fact]
    public void Subtraction_LeftAffine_RightNonAffine_ReturnsATemperatureDeltaInKelvin()
    {
        var absolute = new Quantity(20.0, TemperatureUnits.DegreeCelsius.UnitDefinition);
        var delta = new Quantity(10.0, TemperatureDeltaUnits.Kelvin.UnitDefinition);

        var result = absolute - delta;

        // left.BaseValue - right.BaseValue = 293.15 - 10 = 283.15.
        Assert.Equal(283.15, result.Value, 9);
        Assert.Equal(TemperatureDeltaUnits.Kelvin.UnitDefinition, result.Unit);
    }

    // ----------------------------------------------------------------
    // Scalar multiplication / division
    // ----------------------------------------------------------------

    [Fact]
    public void ScalarMultiplication_ScalesValue_PreservesUnit()
    {
        var quantity = new Quantity(2.0, LengthUnits.Metre.UnitDefinition);

        Assert.Equal(6.0, (quantity * 3.0).Value);
        Assert.Equal(6.0, (3.0 * quantity).Value);
        Assert.Equal(LengthUnits.Metre.UnitDefinition, (quantity * 3.0).Unit);
    }

    [Fact]
    public void ScalarMultiplication_AffineUnit_Throws_WithAMessageAboutBeingScaled()
    {
        var quantity = new Quantity(20.0, TemperatureUnits.DegreeCelsius.UnitDefinition);

        var exception = Assert.Throws<IncompatibleUnitsException>(() => quantity * 2.0);
        Assert.Contains("scaled", exception.Message, StringComparison.Ordinal);

        Assert.Throws<IncompatibleUnitsException>(() => 2.0 * quantity);
    }

    [Fact]
    public void ScalarDivision_ScalesValue_PreservesUnit()
    {
        var quantity = new Quantity(6.0, LengthUnits.Metre.UnitDefinition);

        var result = quantity / 3.0;

        Assert.Equal(2.0, result.Value);
        Assert.Equal(LengthUnits.Metre.UnitDefinition, result.Unit);
    }

    [Fact]
    public void ScalarDivision_AffineUnit_Throws_WithAMessageAboutBeingScaled()
    {
        var quantity = new Quantity(20.0, TemperatureUnits.DegreeCelsius.UnitDefinition);

        var exception = Assert.Throws<IncompatibleUnitsException>(() => quantity / 2.0);
        Assert.Contains("scaled", exception.Message, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // Cross-dimension multiplication / division
    // ----------------------------------------------------------------

    [Fact]
    public void Multiplication_ComposesUnitSymbolAndDimensionAndFactor()
    {
        // Both operand factors are non-unity (Kilonewton 1000, Foot
        // 0.3048) so a mutation swapping * for / in the factor composition
        // is observable: with a unity factor on either side, * and /
        // coincide and the mutation would go unnoticed.
        var force = new Quantity(6.0, ForceUnits.Kilonewton.UnitDefinition);
        var length = new Quantity(2.0, LengthUnits.Foot.UnitDefinition);

        var torque = force * length;

        Assert.Equal(12.0, torque.Value);
        Assert.Equal("kN.ft", torque.Unit.Symbol);
        Assert.Equal(Dimensions.Force * Dimensions.Length, torque.Unit.Dimension);
        Assert.Equal(
            ForceUnits.Kilonewton.UnitDefinition.ToBaseFactor * LengthUnits.Foot.UnitDefinition.ToBaseFactor,
            torque.Unit.ToBaseFactor);
    }

    [Fact]
    public void Multiplication_EitherOperandAffine_Throws_WithAMessageAboutBeingMultiplied()
    {
        var temperature = new Quantity(20.0, TemperatureUnits.DegreeCelsius.UnitDefinition);
        var length = new Quantity(2.0, LengthUnits.Metre.UnitDefinition);

        var leftException = Assert.Throws<IncompatibleUnitsException>(() => temperature * length);
        Assert.Contains("multiplied", leftException.Message, StringComparison.Ordinal);

        var rightException = Assert.Throws<IncompatibleUnitsException>(() => length * temperature);
        Assert.Contains("multiplied", rightException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Division_ForceOverArea_DiscoversPressureAtRunTime()
    {
        // Both operand factors are non-unity (Kilonewton 1000, square
        // millimetre 1e-6) so a mutation swapping / for * in the factor
        // composition is observable — a unity factor on either side would
        // make * and / coincide.
        var force = new Quantity(200.0, ForceUnits.Kilonewton.UnitDefinition);
        var area = new Quantity(2.0, AreaUnits.SquareMillimetre.UnitDefinition);

        var pressure = force / area;

        Assert.Equal(100.0, pressure.Value);
        Assert.Equal("kN/mm²", pressure.Unit.Symbol);
        Assert.Equal(Dimensions.Pressure, pressure.Unit.Dimension);
        Assert.Equal(
            ForceUnits.Kilonewton.UnitDefinition.ToBaseFactor / AreaUnits.SquareMillimetre.UnitDefinition.ToBaseFactor,
            pressure.Unit.ToBaseFactor);
    }

    [Fact]
    public void Division_ComposesUnitSymbolAndDimension_ForAnotherPair()
    {
        var distance = new Quantity(10.0, LengthUnits.Metre.UnitDefinition);
        var time = new Quantity(2.0, DurationUnits.Second.UnitDefinition);

        var velocity = distance / time;

        Assert.Equal(5.0, velocity.Value);
        Assert.Equal("m/s", velocity.Unit.Symbol);
        Assert.Equal(Dimensions.Velocity, velocity.Unit.Dimension);
    }

    [Fact]
    public void Division_EitherOperandAffine_Throws_WithAMessageAboutBeingDivided()
    {
        var temperature = new Quantity(20.0, TemperatureUnits.DegreeCelsius.UnitDefinition);
        var length = new Quantity(2.0, LengthUnits.Metre.UnitDefinition);

        var leftException = Assert.Throws<IncompatibleUnitsException>(() => temperature / length);
        Assert.Contains("divided", leftException.Message, StringComparison.Ordinal);

        var rightException = Assert.Throws<IncompatibleUnitsException>(() => length / temperature);
        Assert.Contains("divided", rightException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Division_ByZeroValue_ThrowsArgumentOutOfRangeException_WithAMessageNamingTheReason()
    {
        var force = new Quantity(10.0, ForceUnits.Newton.UnitDefinition);
        var zero = new Quantity(0.0, LengthUnits.Metre.UnitDefinition);

        // Distinguishes the explicit zero-divisor guard from the
        // constructor's own non-finite-value guard, which an infinite
        // quotient would otherwise also trip with a different message.
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => force / zero);
        Assert.Contains("whose own value is zero", exception.Message, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // Comparison
    // ----------------------------------------------------------------

    [Fact]
    public void Comparison_OrdersByValue_AndTreatsEqualValuesConsistently()
    {
        var small = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);
        var equalToSmall = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);
        var large = new Quantity(2.0, LengthUnits.Metre.UnitDefinition);

        Assert.True(small < large);
        Assert.False(large < small);
        Assert.False(small < equalToSmall);

        Assert.True(large > small);
        Assert.False(small > large);
        Assert.False(small > equalToSmall);

        Assert.True(small <= equalToSmall);
        Assert.True(small <= large);
        Assert.False(large <= small);

        Assert.True(small >= equalToSmall);
        Assert.True(large >= small);
        Assert.False(small >= large);
    }

    [Fact]
    public void CompareTo_DifferentUnits_ComparesByBaseValue()
    {
        var oneMetre = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);
        var oneFoot = new Quantity(1.0, LengthUnits.Foot.UnitDefinition);

        Assert.True(oneMetre.CompareTo(oneFoot) > 0);
        Assert.True(oneFoot < oneMetre);
        Assert.True(oneMetre > oneFoot);
    }

    [Fact]
    public void CompareTo_DifferentDimensions_ThrowsIncompatibleUnitsException()
    {
        var length = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);
        var mass = new Quantity(1.0, MassUnits.Kilogram.UnitDefinition);

        Assert.Throws<IncompatibleUnitsException>(() => length.CompareTo(mass));
    }

    // ----------------------------------------------------------------
    // Equality
    // ----------------------------------------------------------------

    [Fact]
    public void Equality_SameValueAndUnit_AreEqual()
    {
        var a = new Quantity(5.0, LengthUnits.Metre.UnitDefinition);
        var b = new Quantity(5.0, LengthUnits.Metre.UnitDefinition);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equality_PhysicallyEquivalentButDifferentUnit_AreEqual()
    {
        var fiveHundredCentimetres = new Quantity(500.0, LengthUnits.Centimetre.UnitDefinition);
        var fiveMetres = new Quantity(5.0, LengthUnits.Metre.UnitDefinition);

        Assert.Equal(fiveMetres, fiveHundredCentimetres);
        Assert.True(fiveMetres == fiveHundredCentimetres);
        Assert.False(fiveMetres != fiveHundredCentimetres);
        Assert.Equal(fiveMetres.GetHashCode(), fiveHundredCentimetres.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new Quantity(5.0, LengthUnits.Metre.UnitDefinition);
        var b = new Quantity(6.0, LengthUnits.Metre.UnitDefinition);

        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equality_DifferentDimensions_Throws()
    {
        var length = new Quantity(1.0, LengthUnits.Metre.UnitDefinition);
        var mass = new Quantity(1.0, MassUnits.Kilogram.UnitDefinition);

        Assert.Throws<IncompatibleUnitsException>(() => length.Equals(mass));
        Assert.Throws<IncompatibleUnitsException>(() => length == mass);
    }

    [Fact]
    public void Equals_ObjectOverload_NonQuantity_ReturnsFalse()
    {
        var quantity = new Quantity(5.0, LengthUnits.Metre.UnitDefinition);

        Assert.False(quantity.Equals("not a quantity"));
        Assert.False(quantity.Equals(null));
    }

    // ----------------------------------------------------------------
    // Formatting
    // ----------------------------------------------------------------

    [Fact]
    public void ToString_ProducesValueAndSymbol()
    {
        var quantity = new Quantity(5.0, LengthUnits.Metre.UnitDefinition);

        Assert.Equal("5 m", quantity.ToString());
    }

    [Fact]
    public void ToString_WithFormat_AppliesFormatToValue()
    {
        var quantity = new Quantity(5.0, LengthUnits.Metre.UnitDefinition);

        Assert.Equal("5.00 m", quantity.ToString("F2", null));
    }

    [Fact]
    public void ToString_IsCultureInvariant()
    {
        var quantity = new Quantity(1234.5, LengthUnits.Metre.UnitDefinition);

        var result = quantity.ToString("F1", new CultureInfo("de-DE"));

        Assert.Equal("1234.5 m", result);
    }

    // ----------------------------------------------------------------
    // Parsing
    // ----------------------------------------------------------------

    private static IReadOnlyList<UnitDefinition> LengthDefinitions { get; } =
        [.. LengthUnits.All.Select(u => u.UnitDefinition)];

    [Theory]
    [InlineData("5 m", 5.0, "m")]
    [InlineData("-5 m", -5.0, "m")]
    [InlineData("0 m", 0.0, "m")]
    [InlineData("  5   ft", 5.0, "ft")]
    [InlineData("1.5e3 mm", 1500.0, "mm")]
    public void TryParse_RecognisedInput_ReturnsExpectedQuantity(string input, double expectedValue, string expectedSymbol)
    {
        var succeeded = Quantity.TryParse(input, LengthDefinitions, out var result);

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
        var succeeded = Quantity.TryParse(input, LengthDefinitions, out var result);

        Assert.False(succeeded);
        Assert.Equal(default, result);
    }

    [Fact]
    public void Parse_RecognisedInput_ReturnsQuantity()
    {
        var result = Quantity.Parse("5 m", LengthDefinitions);

        Assert.Equal(5.0, result.Value);
        Assert.Equal(LengthUnits.Metre.UnitDefinition, result.Unit);
    }

    [Fact]
    public void Parse_UnrecognisedInput_ThrowsFormatException_NamingTheInput()
    {
        var exception = Assert.Throws<FormatException>(() => Quantity.Parse("nonsense", LengthDefinitions));

        Assert.Contains("nonsense", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not a recognised quantity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_FormatThenParse_RoundTrips()
    {
        var original = new Quantity(12.5, LengthUnits.Kilometre.UnitDefinition);

        var succeeded = Quantity.TryParse(original.ToString(), LengthDefinitions, out var parsed);

        Assert.True(succeeded);
        Assert.Equal(original, parsed);
    }
}
