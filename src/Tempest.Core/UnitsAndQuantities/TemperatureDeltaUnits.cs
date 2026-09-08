namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="TemperatureDelta"/>.</summary>
/// <remarks>
/// Every unit here is a pure scale factor with no offset — unlike
/// <see cref="TemperatureUnits"/>'s own affine Celsius/Fahrenheit members,
/// an <em>interval</em> has no zero point to place on the scale. A rise of
/// one degree Celsius is exactly a rise of one kelvin; a rise of one
/// degree Fahrenheit is five-ninths of that, the same factor
/// <see cref="ThermalExpansionUnits"/> already uses for the identical
/// reason.
/// </remarks>
public static class TemperatureDeltaUnits
{
    /// <summary>The base unit of <see cref="TemperatureDelta"/> (SI).</summary>
    public static readonly Unit<TemperatureDelta> Kelvin = new("K", 1.0);

    /// <summary>A one-degree-Celsius interval — exactly one kelvin.</summary>
    public static readonly Unit<TemperatureDelta> CelsiusDegree = new("ΔdegC", 1.0);

    /// <summary>A one-degree-Fahrenheit interval — five-ninths of one kelvin.</summary>
    public static readonly Unit<TemperatureDelta> FahrenheitDegree = new("ΔdegF", 5.0 / 9.0);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<TemperatureDelta>> All { get; } = [Kelvin, CelsiusDegree, FahrenheitDegree];
}
