namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Pressure"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class PressureUnits
{
    /// <summary>The base unit of <see cref="Pressure"/> (SI, derived).</summary>
    public static readonly Unit<Pressure> Pascal = new("Pa", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Pressure> Kilopascal = new("kPa", 1000.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Pressure> Megapascal = new("MPa", 1_000_000.0);

    /// <summary>Metric, non-SI, in wide engineering use.</summary>
    public static readonly Unit<Pressure> Bar = new("bar", 100_000.0);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Pressure> Psi = new("psi", 6894.757293168);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Pressure>> All { get; } = [Pascal, Kilopascal, Megapascal, Bar, Psi];
}
