using CsCheck;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

/// <summary>
/// Property-based tests (CsCheck) over every unit catalogue this framework
/// holds — `WP 17.3A` (`ADR-0147`).
/// </summary>
/// <remarks>
/// One property, run against every dimension's own catalogue: a value
/// converted from a randomly chosen unit to another randomly chosen unit
/// of the same dimension, and back again, recovers its original value to
/// within a relative tolerance of 1e-12. This subsumes
/// <see cref="DimensionCatalogueTests"/>'s own base-unit-only round trip
/// (which stays, unchanged, as a fast example-based check) by exercising
/// every unit pair, not only every unit against the base unit.
/// </remarks>
public class PropertyTests
{
    private const double RelativeTolerance = 1e-12;

    [Fact]
    public void Length_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(LengthUnits.All);

    [Fact]
    public void Mass_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(MassUnits.All);

    [Fact]
    public void Duration_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(DurationUnits.All);

    [Fact]
    public void Force_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(ForceUnits.All);

    [Fact]
    public void Pressure_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(PressureUnits.All);

    [Fact]
    public void Area_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(AreaUnits.All);

    [Fact]
    public void Volume_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(VolumeUnits.All);

    [Fact]
    public void Velocity_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(VelocityUnits.All);

    [Fact]
    public void Acceleration_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(AccelerationUnits.All);

    [Fact]
    public void MassDensity_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(MassDensityUnits.All);

    [Fact]
    public void Stiffness_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(StiffnessUnits.All);

    [Fact]
    public void TorsionalStiffness_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(TorsionalStiffnessUnits.All);

    [Fact]
    public void Torque_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(TorqueUnits.All);

    [Fact]
    public void Energy_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(EnergyUnits.All);

    [Fact]
    public void Power_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(PowerUnits.All);

    [Fact]
    public void RotationalSpeed_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(RotationalSpeedUnits.All);

    [Fact]
    public void PlaneAngle_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(PlaneAngleUnits.All);

    [Fact]
    public void SpecificHeatCapacity_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(SpecificHeatCapacityUnits.All);

    [Fact]
    public void ThermalConductivity_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(ThermalConductivityUnits.All);

    [Fact]
    public void ThermalExpansion_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(ThermalExpansionUnits.All);

    [Fact]
    public void Dimensionless_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(DimensionlessUnits.All);

    [Fact]
    public void SecondMomentOfArea_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(SecondMomentOfAreaUnits.All);

    [Fact]
    public void SectionModulus_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(SectionModulusUnits.All);

    [Fact]
    public void Frequency_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(FrequencyUnits.All);

    [Fact]
    public void ElectricCurrent_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(ElectricCurrentUnits.All);

    [Fact]
    public void Voltage_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(VoltageUnits.All);

    [Fact]
    public void Resistance_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(ResistanceUnits.All);

    [Fact]
    public void ElectricCharge_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(ElectricChargeUnits.All);

    [Fact]
    public void TemperatureDelta_EveryUnitPair_RoundTripsWithinRelativeTolerance() => AssertRoundTrips(TemperatureDeltaUnits.All);

    [Fact]
    public void Temperature_EveryUnitPair_RoundTripsWithinRelativeTolerance()
    {
        // Temperature carries affine units (ADR-0125): the round trip
        // still holds — ConvertTo honours the offset in both directions —
        // but the generated value is kept within a physically sensible
        // range so an intermediate Kelvin value never goes negative for
        // an affine source unit, which is not a rounding concern this
        // property is about.
        var units = TemperatureUnits.All;
        var unitIndex = Gen.Int[0, units.Count - 1];
        var gen = Gen.Select(Gen.Double[-200, 2000], unitIndex, unitIndex);

        gen.Sample(t =>
        {
            var (value, fromIndex, toIndex) = t;
            var original = new Quantity<Temperature>(value, units[fromIndex]);
            var roundTripped = original.ConvertTo(units[toIndex]).ConvertTo(units[fromIndex]);

            AssertWithinRelativeTolerance(original.Value, roundTripped.Value);
        });
    }

    private static void AssertRoundTrips<TDimension>(IReadOnlyList<Unit<TDimension>> units)
        where TDimension : IDimension
    {
        var unitIndex = Gen.Int[0, units.Count - 1];
        var gen = Gen.Select(Gen.Double[-1_000_000, 1_000_000], unitIndex, unitIndex);

        gen.Sample(t =>
        {
            var (value, fromIndex, toIndex) = t;
            var original = new Quantity<TDimension>(value, units[fromIndex]);
            var roundTripped = original.ConvertTo(units[toIndex]).ConvertTo(units[fromIndex]);

            AssertWithinRelativeTolerance(original.Value, roundTripped.Value);
        });
    }

    private static void AssertWithinRelativeTolerance(double expected, double actual)
    {
        var scale = Math.Max(1.0, Math.Abs(expected));
        Assert.True(
            Math.Abs(actual - expected) <= RelativeTolerance * scale,
            $"Expected {actual} to be within a relative tolerance of {RelativeTolerance} of {expected}.");
    }
}
