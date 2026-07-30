using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

/// <summary>
/// Confirms every per-dimension catalogue (<see cref="LengthUnits"/>, and
/// so on) is internally consistent: exactly one base unit (factor 1.0),
/// no duplicate symbols, and every unit round-trips through the base unit
/// without loss beyond floating-point tolerance.
/// </summary>
public class DimensionCatalogueTests
{
    [Fact]
    public void LengthUnits_AreInternallyConsistent() => AssertCatalogueConsistent(LengthUnits.All, LengthUnits.Metre);

    [Fact]
    public void MassUnits_AreInternallyConsistent() => AssertCatalogueConsistent(MassUnits.All, MassUnits.Kilogram);

    [Fact]
    public void DurationUnits_AreInternallyConsistent() => AssertCatalogueConsistent(DurationUnits.All, DurationUnits.Second);

    [Fact]
    public void ForceUnits_AreInternallyConsistent() => AssertCatalogueConsistent(ForceUnits.All, ForceUnits.Newton);

    [Fact]
    public void PressureUnits_AreInternallyConsistent() => AssertCatalogueConsistent(PressureUnits.All, PressureUnits.Pascal);

    [Fact]
    public void AreaUnits_AreInternallyConsistent() => AssertCatalogueConsistent(AreaUnits.All, AreaUnits.SquareMetre);

    [Fact]
    public void VolumeUnits_AreInternallyConsistent() => AssertCatalogueConsistent(VolumeUnits.All, VolumeUnits.CubicMetre);

    private static void AssertCatalogueConsistent<TDimension>(IReadOnlyList<Unit<TDimension>> all, Unit<TDimension> baseUnit)
        where TDimension : IDimension
    {
        Assert.Equal(1.0, baseUnit.ToBaseUnitFactor);
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
