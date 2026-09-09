namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The thermal-conductivity dimension. Base unit: watt per metre kelvin.</summary>
/// <remarks>Added by `Group A` (P01 Engineering Reference Data) — purely additive, exactly as <see cref="LengthUnits"/>'s own "starting set, extensible" remarks anticipate. Needed by <c>Tempest.Core.Materials</c> for a material's own thermal properties.</remarks>
public sealed class ThermalConductivity : IDimension
{
    /// <summary>The runtime dimension vector for ThermalConductivity â see <see cref="Dimensions.ThermalConductivity"/>.</summary>
    public static Dimension Vector => Dimensions.ThermalConductivity;

    private ThermalConductivity()
    {
    }
}
