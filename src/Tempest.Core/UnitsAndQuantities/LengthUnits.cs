namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Length"/>.</summary>
/// <remarks>
/// A deliberate, disclosed starting set, not a claim of completeness
/// (`WP7.0C Engineering Foundation Contracts.md`'s own "starting set only,
/// extensible" framing) — additional units are purely additive and require
/// no change to <see cref="Unit{TDimension}"/> or <see cref="Quantity{TDimension}"/> themselves.
/// </remarks>
public static class LengthUnits
{
    /// <summary>The base unit of <see cref="Length"/> (SI).</summary>
    public static readonly Unit<Length> Metre = new("m", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Length> Millimetre = new("mm", 0.001);

    /// <summary>SI.</summary>
    public static readonly Unit<Length> Centimetre = new("cm", 0.01);

    /// <summary>SI.</summary>
    public static readonly Unit<Length> Kilometre = new("km", 1000.0);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Length> Inch = new("in", 0.0254);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Length> Foot = new("ft", 0.3048);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Length> Yard = new("yd", 0.9144);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Length> Mile = new("mi", 1609.344);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Length>> All { get; } = [Metre, Millimetre, Centimetre, Kilometre, Inch, Foot, Yard, Mile];
}
