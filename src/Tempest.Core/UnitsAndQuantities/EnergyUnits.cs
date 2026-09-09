namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Energy"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class EnergyUnits
{
    /// <summary>The base unit of <see cref="Energy"/> (SI, derived).</summary>
    public static readonly Unit<Energy> Joule = new("J", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Energy> Kilojoule = new("kJ", 1000.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Energy> Megajoule = new("MJ", 1000000.0);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Energy> FootPoundForce = new("ft.lbf", 1.3558179483314004);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Energy>> All { get; } = [Joule, Kilojoule, Megajoule, FootPoundForce];
}
