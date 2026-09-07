namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The linear thermal-expansion-coefficient dimension. Base unit: reciprocal kelvin.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. A per-temperature-interval quantity, so its units are multiplicative even though <see cref="Temperature"/>'s own are not: one reciprocal degree Fahrenheit is exactly 1.8 reciprocal kelvin, with no offset.</remarks>
public sealed class ThermalExpansion : IDimension
{
    private ThermalExpansion()
    {
    }
}
