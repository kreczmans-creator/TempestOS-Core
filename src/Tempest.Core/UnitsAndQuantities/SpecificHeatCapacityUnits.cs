namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="SpecificHeatCapacity"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class SpecificHeatCapacityUnits
{
    /// <summary>The base unit of <see cref="SpecificHeatCapacity"/> (SI, derived).</summary>
    public static readonly Unit<SpecificHeatCapacity> JoulePerKilogramKelvin = new("J/(kg.K)", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<SpecificHeatCapacity> KilojoulePerKilogramKelvin = new("kJ/(kg.K)", 1000.0);

    /// <summary>Imperial.</summary>
    public static readonly Unit<SpecificHeatCapacity> BtuPerPoundDegreeFahrenheit = new("BTU/(lb.degF)", 4186.8);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<SpecificHeatCapacity>> All { get; } =
        [JoulePerKilogramKelvin, KilojoulePerKilogramKelvin, BtuPerPoundDegreeFahrenheit];
}
