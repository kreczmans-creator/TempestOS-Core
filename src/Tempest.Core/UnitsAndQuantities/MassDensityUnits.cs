namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="MassDensity"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class MassDensityUnits
{
    /// <summary>The base unit of <see cref="MassDensity"/> (SI).</summary>
    public static readonly Unit<MassDensity> KilogramPerCubicMetre = new("kg/m3", 1.0);

    /// <summary>SI — the unit material datasheets most often quote.</summary>
    public static readonly Unit<MassDensity> GramPerCubicCentimetre = new("g/cm3", 1000.0);

    /// <summary>SI.</summary>
    public static readonly Unit<MassDensity> TonnePerCubicMetre = new("t/m3", 1000.0);

    /// <summary>Imperial.</summary>
    public static readonly Unit<MassDensity> PoundPerCubicInch = new("lb/in3", 27679.904710203122);

    /// <summary>Imperial.</summary>
    public static readonly Unit<MassDensity> PoundPerCubicFoot = new("lb/ft3", 16.018463373960142);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<MassDensity>> All { get; } =
        [KilogramPerCubicMetre, GramPerCubicCentimetre, TonnePerCubicMetre, PoundPerCubicInch, PoundPerCubicFoot];
}
