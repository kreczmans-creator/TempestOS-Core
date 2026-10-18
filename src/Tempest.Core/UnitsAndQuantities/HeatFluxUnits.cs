namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="HeatFlux"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class HeatFluxUnits
{
    /// <summary>The base unit of <see cref="HeatFlux"/> (SI, derived).</summary>
    public static readonly Unit<HeatFlux> WattPerSquareMetre = new("W/m²", 1.0);

    /// <summary>Kilowatt per square metre.</summary>
    public static readonly Unit<HeatFlux> KilowattPerSquareMetre = new("kW/m²", 1000.0);

    /// <summary>Watt per square centimetre.</summary>
    public static readonly Unit<HeatFlux> WattPerSquareCentimetre = new("W/cm²", 10000.0);

    /// <summary>Imperial: BTU (international table) per hour square foot.</summary>
    public static readonly Unit<HeatFlux> BtuPerHourSquareFoot = new("BTU/(h.ft²)", 3.1545907450630484);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<HeatFlux>> All { get; } =
        [WattPerSquareMetre, KilowattPerSquareMetre, WattPerSquareCentimetre, BtuPerHourSquareFoot];
}
