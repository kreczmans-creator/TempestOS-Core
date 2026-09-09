namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Resistance"/>.</summary>
/// <remarks>
/// See <see cref="LengthUnits"/>'s own remarks — the same "starting set,
/// purely additive" discipline applies. The base unit's symbol is spelled
/// <c>"ohm"</c> rather than the Greek capital omega (Ω), for consistency
/// with this framework's own ASCII-symbol convention elsewhere.
/// </remarks>
public static class ResistanceUnits
{
    /// <summary>The base unit of <see cref="Resistance"/> (SI).</summary>
    public static readonly Unit<Resistance> Ohm = new("ohm", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Resistance> Milliohm = new("mohm", 0.001);

    /// <summary>SI.</summary>
    public static readonly Unit<Resistance> Kiloohm = new("kohm", 1000.0);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Resistance>> All { get; } = [Ohm, Milliohm, Kiloohm];
}
