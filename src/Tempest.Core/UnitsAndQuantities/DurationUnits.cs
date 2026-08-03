namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Duration"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class DurationUnits
{
    /// <summary>The base unit of <see cref="Duration"/> (SI).</summary>
    public static readonly Unit<Duration> Second = new("s", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Duration> Millisecond = new("ms", 0.001);

    /// <summary>SI-accepted.</summary>
    public static readonly Unit<Duration> Minute = new("min", 60.0);

    /// <summary>SI-accepted.</summary>
    public static readonly Unit<Duration> Hour = new("h", 3600.0);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Duration>> All { get; } = [Second, Millisecond, Minute, Hour];
}
