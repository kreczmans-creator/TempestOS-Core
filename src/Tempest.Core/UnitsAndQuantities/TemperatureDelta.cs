namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// A temperature <em>difference</em> — an interval on the temperature
/// scale, rather than a position on it. Base unit: kelvin.
/// </summary>
/// <remarks>
/// <para>
/// Added by `WP 17.3A` (`ADR-0147`), extending `ADR-0125`. Shares its
/// dimension vector exactly with <see cref="Temperature"/> (Θ) — the two
/// are dimensionally identical and the compile-time facade exists
/// precisely so they are not interchangeable engineering quantities all
/// the same, the same discipline <see cref="Torque"/>/<see cref="Energy"/>
/// and <see cref="SectionModulus"/>/<see cref="Volume"/> already apply.
/// </para>
/// <para>
/// Twenty degrees Celsius is a <em>position</em> on the Celsius scale;
/// twenty kelvin of temperature <em>rise</em> is an interval, and the two
/// are not the same kind of quantity even though both might be written
/// with a similar-looking number. <see cref="TemperatureDeltaUnits"/>
/// holds every unit as a pure scale factor with no offset — the interval
/// itself has no zero point to place — and the non-generic <see cref="Quantity"/>
/// (`ADR-0147`) produces a <see cref="TemperatureDelta"/>-shaped result
/// automatically when subtracting two affine temperatures.
/// </para>
/// </remarks>
public sealed class TemperatureDelta : IDimension
{
    /// <summary>The runtime dimension vector for <see cref="TemperatureDelta"/> — see <see cref="Dimensions.TemperatureDelta"/>.</summary>
    public static Dimension Vector => Dimensions.TemperatureDelta;

    private TemperatureDelta()
    {
    }
}
