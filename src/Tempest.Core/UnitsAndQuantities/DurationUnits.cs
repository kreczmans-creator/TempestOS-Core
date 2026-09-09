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

    /// <summary>A calendar day — 24 hours exactly.</summary>
    /// <remarks>
    /// A <em>calendar</em> day, not a working one. A working day is not a
    /// duration at all: how much elapsed time five working days represent
    /// depends on a calendar, a country and a shift pattern, none of which
    /// this platform holds. `P03`'s lead-time model keeps the two apart
    /// (`ADR-0133`) and never converts between them.
    /// </remarks>
    public static readonly Unit<Duration> Day = new("d", 86_400.0);

    /// <summary>A calendar week — seven calendar days.</summary>
    /// <remarks>See <see cref="Day"/>: calendar, not working.</remarks>
    public static readonly Unit<Duration> Week = new("wk", 604_800.0);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Duration>> All { get; } = [Second, Millisecond, Minute, Hour, Day, Week];
}
