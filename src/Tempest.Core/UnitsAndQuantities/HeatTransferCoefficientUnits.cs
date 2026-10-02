namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="HeatTransferCoefficient"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class HeatTransferCoefficientUnits
{
    /// <summary>The base unit of <see cref="HeatTransferCoefficient"/> (SI, derived).</summary>
    public static readonly Unit<HeatTransferCoefficient> WattPerSquareMetreKelvin = new("W/(m².K)", 1.0);

    /// <summary>Watt per square centimetre kelvin — ten thousand of the base unit.</summary>
    public static readonly Unit<HeatTransferCoefficient> WattPerSquareCentimetreKelvin = new("W/(cm².K)", 10000.0);

    /// <summary>Imperial: BTU (international table) per hour square foot degree Fahrenheit.</summary>
    public static readonly Unit<HeatTransferCoefficient> BtuPerHourSquareFootDegreeFahrenheit = new("BTU/(h.ft².degF)", 5.678263341113487);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<HeatTransferCoefficient>> All { get; } =
        [WattPerSquareMetreKelvin, WattPerSquareCentimetreKelvin, BtuPerHourSquareFootDegreeFahrenheit];
}
