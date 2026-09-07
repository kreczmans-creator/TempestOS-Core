namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="ThermalExpansion"/>.</summary>
/// <remarks>
/// Every unit here is purely multiplicative, unlike
/// <see cref="TemperatureUnits"/>'s own: this dimension measures a
/// coefficient per temperature <em>interval</em>, and an interval of one
/// degree Fahrenheit is exactly five-ninths of an interval of one kelvin
/// regardless of where on the scale it is measured.
/// </remarks>
public static class ThermalExpansionUnits
{
    /// <summary>The base unit of <see cref="ThermalExpansion"/> (SI, derived).</summary>
    public static readonly Unit<ThermalExpansion> PerKelvin = new("1/K", 1.0);

    /// <summary>SI — the form material datasheets most often quote.</summary>
    public static readonly Unit<ThermalExpansion> MicrometrePerMetreKelvin = new("um/(m.K)", 0.000001);

    /// <summary>Imperial — one reciprocal Fahrenheit interval is 1.8 reciprocal kelvin.</summary>
    public static readonly Unit<ThermalExpansion> PerDegreeFahrenheit = new("1/degF", 1.8);

    /// <summary>Imperial.</summary>
    public static readonly Unit<ThermalExpansion> MicroinchPerInchDegreeFahrenheit = new("uin/(in.degF)", 0.0000018);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<ThermalExpansion>> All { get; } =
        [PerKelvin, MicrometrePerMetreKelvin, PerDegreeFahrenheit, MicroinchPerInchDegreeFahrenheit];
}
