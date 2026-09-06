namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Temperature"/>.</summary>
/// <remarks>
/// <para>
/// The only catalogue in this framework containing affine units
/// (`ADR-0125`). Degrees Celsius and degrees Fahrenheit carry an offset as
/// well as a factor, because their zero is not the dimension's own zero;
/// kelvin and degrees Rankine are absolute and carry no offset.
/// </para>
/// <para>
/// A quantity expressed in an affine unit converts and compares normally
/// but cannot be added, subtracted or scaled — see
/// <see cref="Unit{TDimension}.IsAffine"/>. A caller needing to express a
/// temperature <em>interval</em> rather than a position on a scale must
/// use an absolute unit.
/// </para>
/// </remarks>
public static class TemperatureUnits
{
    /// <summary>The base unit of <see cref="Temperature"/> (SI). Absolute.</summary>
    public static readonly Unit<Temperature> Kelvin = new("K", 1.0);

    /// <summary>SI-accepted. <b>Affine</b> — zero degrees Celsius is 273.15 K.</summary>
    public static readonly Unit<Temperature> DegreeCelsius = new("degC", 1.0, 273.15);

    /// <summary>Absolute, imperial — the Fahrenheit-sized degree measured from absolute zero.</summary>
    public static readonly Unit<Temperature> DegreeRankine = new("degR", 5.0 / 9.0);

    /// <summary>Imperial. <b>Affine</b> — zero degrees Fahrenheit is 459.67 degrees Rankine.</summary>
    public static readonly Unit<Temperature> DegreeFahrenheit = new("degF", 5.0 / 9.0, 273.15 - (32.0 * 5.0 / 9.0));

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Temperature>> All { get; } = [Kelvin, DegreeCelsius, DegreeRankine, DegreeFahrenheit];
}
