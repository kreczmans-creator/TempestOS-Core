namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Voltage"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class VoltageUnits
{
    /// <summary>The base unit of <see cref="Voltage"/> (SI).</summary>
    public static readonly Unit<Voltage> Volt = new("V", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Voltage> Millivolt = new("mV", 0.001);

    /// <summary>SI.</summary>
    public static readonly Unit<Voltage> Kilovolt = new("kV", 1000.0);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Voltage>> All { get; } = [Volt, Millivolt, Kilovolt];
}
