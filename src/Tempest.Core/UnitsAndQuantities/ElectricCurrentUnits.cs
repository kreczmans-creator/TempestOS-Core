namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="ElectricCurrent"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class ElectricCurrentUnits
{
    /// <summary>The base unit of <see cref="ElectricCurrent"/> (SI).</summary>
    public static readonly Unit<ElectricCurrent> Ampere = new("A", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<ElectricCurrent> Milliampere = new("mA", 0.001);

    /// <summary>SI.</summary>
    public static readonly Unit<ElectricCurrent> Kiloampere = new("kA", 1000.0);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<ElectricCurrent>> All { get; } = [Ampere, Milliampere, Kiloampere];
}
