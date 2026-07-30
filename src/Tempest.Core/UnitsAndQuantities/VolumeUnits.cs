namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Volume"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class VolumeUnits
{
    /// <summary>The base unit of <see cref="Volume"/> (SI, derived).</summary>
    public static readonly Unit<Volume> CubicMetre = new("m³", 1.0);

    /// <summary>Metric, non-SI, in wide engineering use.</summary>
    public static readonly Unit<Volume> Litre = new("L", 0.001);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Volume> CubicFoot = new("ft³", 0.028316846592);

    /// <summary>US customary.</summary>
    public static readonly Unit<Volume> UsGallon = new("gal", 0.003785411784);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Volume>> All { get; } = [CubicMetre, Litre, CubicFoot, UsGallon];
}
