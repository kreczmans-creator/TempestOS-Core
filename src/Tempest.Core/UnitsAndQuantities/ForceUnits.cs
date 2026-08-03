namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Force"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class ForceUnits
{
    /// <summary>The base unit of <see cref="Force"/> (SI, derived).</summary>
    public static readonly Unit<Force> Newton = new("N", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Force> Kilonewton = new("kN", 1000.0);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Force> PoundForce = new("lbf", 4.4482216152605);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Force>> All { get; } = [Newton, Kilonewton, PoundForce];
}
