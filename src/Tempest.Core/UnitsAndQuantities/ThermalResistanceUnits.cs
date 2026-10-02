namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="ThermalResistance"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies. Every unit is multiplicative: a resistance is an interval per watt, never a temperature position.</remarks>
public static class ThermalResistanceUnits
{
    /// <summary>The base unit of <see cref="ThermalResistance"/> (SI, derived).</summary>
    public static readonly Unit<ThermalResistance> KelvinPerWatt = new("K/W", 1.0);

    /// <summary>Kelvin per milliwatt — a thousand kelvin per watt.</summary>
    public static readonly Unit<ThermalResistance> KelvinPerMilliwatt = new("K/mW", 1000.0);

    /// <summary>
    /// Degree Celsius per watt, as semiconductor datasheets print it —
    /// numerically identical to <see cref="KelvinPerWatt"/>, kept as its own
    /// symbol only so a value transcribed in the datasheet's unit parses
    /// without being reinterpreted.
    /// </summary>
    public static readonly Unit<ThermalResistance> DegreeCelsiusPerWatt = new("degC/W", 1.0);

    /// <summary>Imperial: degree Fahrenheit hour per BTU (international table BTU), the reciprocal of BTU/(h.degF).</summary>
    public static readonly Unit<ThermalResistance> DegreeFahrenheitHourPerBtu = new("degF.h/BTU", 1.8956342406266344);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<ThermalResistance>> All { get; } =
        [KelvinPerWatt, KelvinPerMilliwatt, DegreeCelsiusPerWatt, DegreeFahrenheitHourPerBtu];
}
