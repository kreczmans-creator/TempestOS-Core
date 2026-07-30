namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Mass"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class MassUnits
{
    /// <summary>The base unit of <see cref="Mass"/> (SI).</summary>
    public static readonly Unit<Mass> Kilogram = new("kg", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Mass> Gram = new("g", 0.001);

    /// <summary>SI.</summary>
    public static readonly Unit<Mass> Milligram = new("mg", 0.000001);

    /// <summary>SI (metric).</summary>
    public static readonly Unit<Mass> Tonne = new("t", 1000.0);

    /// <summary>Imperial (avoirdupois).</summary>
    public static readonly Unit<Mass> Pound = new("lb", 0.45359237);

    /// <summary>Imperial (avoirdupois).</summary>
    public static readonly Unit<Mass> Ounce = new("oz", 0.028349523125);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Mass>> All { get; } = [Kilogram, Gram, Milligram, Tonne, Pound, Ounce];
}
