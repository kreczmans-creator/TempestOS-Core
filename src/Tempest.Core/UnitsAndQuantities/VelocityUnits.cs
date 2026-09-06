namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Velocity"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class VelocityUnits
{
    /// <summary>The base unit of <see cref="Velocity"/> (SI, derived).</summary>
    public static readonly Unit<Velocity> MetrePerSecond = new("m/s", 1.0);

    /// <summary>SI — the unit machining data most often quotes a cutting speed in.</summary>
    public static readonly Unit<Velocity> MetrePerMinute = new("m/min", 1.0 / 60.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Velocity> MillimetrePerSecond = new("mm/s", 0.001);

    /// <summary>SI.</summary>
    public static readonly Unit<Velocity> KilometrePerHour = new("km/h", 1.0 / 3.6);

    /// <summary>Imperial — surface feet per minute.</summary>
    public static readonly Unit<Velocity> FootPerMinute = new("ft/min", 0.00508);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Velocity>> All { get; } =
        [MetrePerSecond, MetrePerMinute, MillimetrePerSecond, KilometrePerHour, FootPerMinute];
}
