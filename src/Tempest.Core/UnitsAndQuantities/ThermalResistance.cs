namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The thermal-resistance dimension. Base unit: kelvin per watt.</summary>
/// <remarks>
/// Added for the thermal calculation modules (recovered from the v0.16.0
/// release-candidate calculation suite): a thermal resistance multiplied by
/// a heat flow is a temperature difference. Purely additive, exactly as
/// <see cref="LengthUnits"/>'s own "starting set, extensible" remarks
/// anticipate.
/// <para>
/// A thermal resistance is a temperature <em>difference</em> per watt, so
/// its units are purely multiplicative: one degree Celsius per watt is one
/// kelvin per watt, with none of <see cref="TemperatureUnits"/>'s 273.15
/// offset.
/// </para>
/// </remarks>
public sealed class ThermalResistance : IDimension
{
    /// <summary>The runtime dimension vector for ThermalResistance — see <see cref="Dimensions.ThermalResistance"/>.</summary>
    public static Dimension Vector => Dimensions.ThermalResistance;

    private ThermalResistance()
    {
    }
}
