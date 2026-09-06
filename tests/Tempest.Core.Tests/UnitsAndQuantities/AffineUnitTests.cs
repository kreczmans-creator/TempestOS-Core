using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

// ADR-0125 / FCR-0034: Unit<TDimension> gained an offset so a genuinely
// affine scale (degrees Celsius, degrees Fahrenheit) can be expressed at
// all. These tests hold the new behaviour to two standards: it must be
// correct for affine units, and it must be indistinguishable from the old
// behaviour for every multiplicative one.
public class AffineUnitTests
{
    // ----------------------------------------------------------------
    // The offset is additive: nothing that existed before behaves differently
    // ----------------------------------------------------------------

    [Fact]
    public void AMultiplicativeUnit_DefaultsToZeroOffset_AndIsNotAffine()
    {
        Assert.Equal(0.0, LengthUnits.Millimetre.ToBaseUnitOffset);
        Assert.False(LengthUnits.Millimetre.IsAffine);
        Assert.False(ForceUnits.Kilonewton.IsAffine);
    }

    [Fact]
    public void AMultiplicativeConversion_IsUnchangedByTheOffsetMechanism()
    {
        var length = new Quantity<Length>(2500, LengthUnits.Millimetre);

        Assert.Equal(2.5, length.ConvertTo(LengthUnits.Metre).Value, 12);
        Assert.Equal(2.5, length.BaseValue, 12);
    }

    [Fact]
    public void AMultiplicativeUnit_StillSupportsArithmetic()
    {
        var a = new Quantity<Force>(3, ForceUnits.Kilonewton);
        var b = new Quantity<Force>(4, ForceUnits.Kilonewton);

        Assert.Equal(7, (a + b).Value);
        Assert.Equal(1, (b - a).Value);
        Assert.Equal(6, (a * 2).Value);
        Assert.Equal(2, (b / 2).Value);
    }

    // ----------------------------------------------------------------
    // Affine conversion
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(0.0, 273.15)]
    [InlineData(100.0, 373.15)]
    [InlineData(-273.15, 0.0)]
    [InlineData(20.0, 293.15)]
    public void DegreesCelsius_ConvertToKelvinAcrossTheOffset(double celsius, double expectedKelvin)
    {
        var temperature = new Quantity<Temperature>(celsius, TemperatureUnits.DegreeCelsius);

        Assert.Equal(expectedKelvin, temperature.ConvertTo(TemperatureUnits.Kelvin).Value, 9);
    }

    [Theory]
    [InlineData(32.0, 0.0)]
    [InlineData(212.0, 100.0)]
    [InlineData(-40.0, -40.0)]
    public void DegreesFahrenheit_ConvertToDegreesCelsius(double fahrenheit, double expectedCelsius)
    {
        var temperature = new Quantity<Temperature>(fahrenheit, TemperatureUnits.DegreeFahrenheit);

        Assert.Equal(expectedCelsius, temperature.ConvertTo(TemperatureUnits.DegreeCelsius).Value, 9);
    }

    [Fact]
    public void DegreesRankine_IsAbsoluteAndCarriesNoOffset()
    {
        Assert.False(TemperatureUnits.DegreeRankine.IsAffine);
        Assert.Equal(0.0, new Quantity<Temperature>(0, TemperatureUnits.DegreeRankine).ConvertTo(TemperatureUnits.Kelvin).Value, 9);
        Assert.Equal(491.67, new Quantity<Temperature>(273.15, TemperatureUnits.Kelvin).ConvertTo(TemperatureUnits.DegreeRankine).Value, 6);
    }

    [Fact]
    public void AnAffineConversion_RoundTrips()
    {
        var original = new Quantity<Temperature>(150.0, TemperatureUnits.DegreeCelsius);

        var roundTripped = original
            .ConvertTo(TemperatureUnits.Kelvin)
            .ConvertTo(TemperatureUnits.DegreeFahrenheit)
            .ConvertTo(TemperatureUnits.DegreeCelsius);

        Assert.Equal(150.0, roundTripped.Value, 9);
    }

    // ----------------------------------------------------------------
    // Affine arithmetic is refused, not silently wrong
    // ----------------------------------------------------------------

    [Fact]
    public void AddingTwoAffineQuantities_Throws()
    {
        // Twenty degrees Celsius plus five degrees Celsius is not a
        // temperature. Returning 25 would look like an answer.
        var a = new Quantity<Temperature>(20, TemperatureUnits.DegreeCelsius);
        var b = new Quantity<Temperature>(5, TemperatureUnits.DegreeCelsius);

        Assert.Throws<IncompatibleUnitsException>(() => a + b);
    }

    [Fact]
    public void SubtractingTwoAffineQuantities_Throws()
    {
        var a = new Quantity<Temperature>(20, TemperatureUnits.DegreeCelsius);
        var b = new Quantity<Temperature>(5, TemperatureUnits.DegreeCelsius);

        Assert.Throws<IncompatibleUnitsException>(() => a - b);
    }

    [Fact]
    public void ScalingAnAffineQuantity_Throws()
    {
        var a = new Quantity<Temperature>(20, TemperatureUnits.DegreeCelsius);

        Assert.Throws<IncompatibleUnitsException>(() => a * 2);
        Assert.Throws<IncompatibleUnitsException>(() => a / 2);
    }

    [Fact]
    public void ArithmeticOnAnAbsoluteTemperature_IsPermitted()
    {
        // Kelvin is not affine, so the operation has a meaning and the
        // guard must not fire.
        var a = new Quantity<Temperature>(300, TemperatureUnits.Kelvin);
        var b = new Quantity<Temperature>(10, TemperatureUnits.Kelvin);

        Assert.Equal(310, (a + b).Value);
        Assert.Equal(600, (a * 2).Value);
    }

    [Fact]
    public void ComparingTwoAffineQuantitiesInTheSameUnit_IsPermitted()
    {
        // Comparison is a question about position on the scale, which is
        // meaningful even where addition is not.
        var cold = new Quantity<Temperature>(20, TemperatureUnits.DegreeCelsius);
        var hot = new Quantity<Temperature>(150, TemperatureUnits.DegreeCelsius);

        Assert.True(hot > cold);
        Assert.True(cold < hot);
    }

    // ----------------------------------------------------------------
    // Construction and serialisation
    // ----------------------------------------------------------------

    [Fact]
    public void AUnit_RefusesANonFiniteOffset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Unit<Temperature>("bad", 1.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Unit<Temperature>("bad", 1.0, double.PositiveInfinity));
    }

    [Fact]
    public void TwoUnits_DifferingOnlyByOffset_AreNotEqual()
    {
        Assert.NotEqual(new Unit<Temperature>("x", 1.0), new Unit<Temperature>("x", 1.0, 273.15));
    }

    [Fact]
    public void AnAffineQuantity_RoundTripsThroughJson()
    {
        var original = new Quantity<Temperature>(150.0, TemperatureUnits.DegreeCelsius);

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var restored = System.Text.Json.JsonSerializer.Deserialize<Quantity<Temperature>>(json);

        Assert.Equal(original, restored);
        Assert.Equal(273.15, restored.Unit.ToBaseUnitOffset, 9);
    }

    [Fact]
    public void AUnitSerialisedBeforeOffsetsExisted_DeserialisesWithZeroOffset()
    {
        // The exact JSON shape every already-stored unit carries: a symbol
        // and a factor, and no offset field at all.
        const string legacy = "{\"Value\":5,\"Unit\":{\"Symbol\":\"mm\",\"ToBaseUnitFactor\":0.001}}";

        var restored = System.Text.Json.JsonSerializer.Deserialize<Quantity<Length>>(legacy);

        Assert.Equal(0.0, restored.Unit.ToBaseUnitOffset);
        Assert.False(restored.Unit.IsAffine);
        Assert.Equal(0.005, restored.BaseValue, 12);
    }

    // ----------------------------------------------------------------
    // Catalogue consistency for every dimension Group A added
    // ----------------------------------------------------------------

    [Fact]
    public void TemperatureUnits_AreInternallyConsistent() => AssertCatalogueConsistent(TemperatureUnits.All, TemperatureUnits.Kelvin);

    [Fact]
    public void MassDensityUnits_AreInternallyConsistent() => AssertCatalogueConsistent(MassDensityUnits.All, MassDensityUnits.KilogramPerCubicMetre);

    [Fact]
    public void StiffnessUnits_AreInternallyConsistent() => AssertCatalogueConsistent(StiffnessUnits.All, StiffnessUnits.NewtonPerMetre);

    [Fact]
    public void TorqueUnits_AreInternallyConsistent() => AssertCatalogueConsistent(TorqueUnits.All, TorqueUnits.NewtonMetre);

    [Fact]
    public void ThermalConductivityUnits_AreInternallyConsistent() => AssertCatalogueConsistent(ThermalConductivityUnits.All, ThermalConductivityUnits.WattPerMetreKelvin);

    [Fact]
    public void ThermalExpansionUnits_AreInternallyConsistent() => AssertCatalogueConsistent(ThermalExpansionUnits.All, ThermalExpansionUnits.PerKelvin);

    [Fact]
    public void SpecificHeatCapacityUnits_AreInternallyConsistent() => AssertCatalogueConsistent(SpecificHeatCapacityUnits.All, SpecificHeatCapacityUnits.JoulePerKilogramKelvin);

    [Fact]
    public void AccelerationUnits_AreInternallyConsistent() => AssertCatalogueConsistent(AccelerationUnits.All, AccelerationUnits.MetrePerSecondSquared);

    [Fact]
    public void EnergyUnits_AreInternallyConsistent() => AssertCatalogueConsistent(EnergyUnits.All, EnergyUnits.Joule);

    [Fact]
    public void VelocityUnits_AreInternallyConsistent() => AssertCatalogueConsistent(VelocityUnits.All, VelocityUnits.MetrePerSecond);

    [Fact]
    public void DimensionlessUnits_AreInternallyConsistent() => AssertCatalogueConsistent(DimensionlessUnits.All, DimensionlessUnits.One);

    [Fact]
    public void OnlyTemperatureCarriesAffineUnits()
    {
        // The one place in this framework where an offset is legitimate,
        // asserted so a future dimension cannot quietly acquire one.
        Assert.All(LengthUnits.All, u => Assert.False(u.IsAffine));
        Assert.All(MassDensityUnits.All, u => Assert.False(u.IsAffine));
        Assert.All(ThermalExpansionUnits.All, u => Assert.False(u.IsAffine));
        Assert.All(DimensionlessUnits.All, u => Assert.False(u.IsAffine));
        Assert.Equal(2, TemperatureUnits.All.Count(u => u.IsAffine));
    }

    private static void AssertCatalogueConsistent<TDimension>(IReadOnlyList<Unit<TDimension>> all, Unit<TDimension> baseUnit)
        where TDimension : IDimension
    {
        Assert.Equal(1.0, baseUnit.ToBaseUnitFactor);
        Assert.Equal(0.0, baseUnit.ToBaseUnitOffset);
        Assert.Contains(baseUnit, all);

        var symbols = all.Select(u => u.Symbol).ToList();
        Assert.Equal(symbols.Distinct(StringComparer.Ordinal).Count(), symbols.Count);

        foreach (var unit in all)
        {
            var quantity = new Quantity<TDimension>(3.0, unit);
            var roundTripped = quantity.ConvertTo(baseUnit).ConvertTo(unit);

            Assert.Equal(quantity.Value, roundTripped.Value, precision: 6);
        }
    }
}
