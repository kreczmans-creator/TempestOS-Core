namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Frequency"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class FrequencyUnits
{
    /// <summary>The base unit of <see cref="Frequency"/> (SI).</summary>
    public static readonly Unit<Frequency> Hertz = new("Hz", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Frequency> Kilohertz = new("kHz", 1000.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Frequency> Megahertz = new("MHz", 1_000_000.0);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Frequency>> All { get; } = [Hertz, Kilohertz, Megahertz];
}
