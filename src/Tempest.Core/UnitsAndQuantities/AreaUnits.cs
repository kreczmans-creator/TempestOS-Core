namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Area"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class AreaUnits
{
    /// <summary>The base unit of <see cref="Area"/> (SI, derived).</summary>
    public static readonly Unit<Area> SquareMetre = new("m²", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Area> SquareMillimetre = new("mm²", 0.000001);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Area> SquareFoot = new("ft²", 0.09290304);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Area>> All { get; } = [SquareMetre, SquareMillimetre, SquareFoot];
}
