namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Acceleration"/>.</summary>
/// <remarks>
/// <see cref="StandardGravity"/> is a <em>unit</em> here, not a constant:
/// the constant itself — standard acceleration due to gravity, with its
/// own source and provenance — belongs in
/// <c>Tempest.Core.Constants</c>, and this unit exists only so a
/// quantity can be expressed in multiples of it.
/// </remarks>
public static class AccelerationUnits
{
    /// <summary>The base unit of <see cref="Acceleration"/> (SI, derived).</summary>
    public static readonly Unit<Acceleration> MetrePerSecondSquared = new("m/s2", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Acceleration> MillimetrePerSecondSquared = new("mm/s2", 0.001);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Acceleration> FootPerSecondSquared = new("ft/s2", 0.3048);

    /// <summary>Multiples of standard gravity, as defined by the CGPM's own conventional value.</summary>
    public static readonly Unit<Acceleration> StandardGravity = new("gn", 9.80665);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Acceleration>> All { get; } =
        [MetrePerSecondSquared, MillimetrePerSecondSquared, FootPerSecondSquared, StandardGravity];
}
