namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="RotationalSpeed"/>.</summary>
/// <remarks>
/// See <see cref="LengthUnits"/>'s own remarks — the same "starting set,
/// purely additive" discipline applies. The base unit is the revolution
/// per second rather than the radian per second: this dimension counts
/// whole revolutions (what a bearing catalogue's own speed rating means),
/// and <see cref="RadianPerSecond"/> is provided as an ordinary converted
/// unit alongside it.
/// </remarks>
public static class RotationalSpeedUnits
{
    /// <summary>The base unit of <see cref="RotationalSpeed"/> — one whole revolution per second.</summary>
    public static readonly Unit<RotationalSpeed> RevolutionPerSecond = new("r/s", 1.0);

    /// <summary>Revolutions per minute — the unit bearing catalogues almost universally quote speed ratings in.</summary>
    public static readonly Unit<RotationalSpeed> RevolutionPerMinute = new("r/min", 1.0 / 60.0);

    /// <summary>Radians per second (SI coherent for angular velocity), expressed against this dimension's own revolution-counting base.</summary>
    public static readonly Unit<RotationalSpeed> RadianPerSecond = new("rad/s", 1.0 / (2.0 * Math.PI));

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<RotationalSpeed>> All { get; } = [RevolutionPerSecond, RevolutionPerMinute, RadianPerSecond];
}
