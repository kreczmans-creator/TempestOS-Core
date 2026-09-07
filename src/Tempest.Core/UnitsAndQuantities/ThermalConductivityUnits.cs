namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="ThermalConductivity"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class ThermalConductivityUnits
{
    /// <summary>The base unit of <see cref="ThermalConductivity"/> (SI, derived).</summary>
    public static readonly Unit<ThermalConductivity> WattPerMetreKelvin = new("W/(m.K)", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<ThermalConductivity> WattPerCentimetreKelvin = new("W/(cm.K)", 100.0);

    /// <summary>Imperial.</summary>
    public static readonly Unit<ThermalConductivity> BtuPerHourFootDegreeFahrenheit = new("BTU/(h.ft.degF)", 1.7307346664744324);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<ThermalConductivity>> All { get; } =
        [WattPerMetreKelvin, WattPerCentimetreKelvin, BtuPerHourFootDegreeFahrenheit];
}
