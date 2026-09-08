namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="ElectricCharge"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class ElectricChargeUnits
{
    /// <summary>The base unit of <see cref="ElectricCharge"/> (SI, derived).</summary>
    public static readonly Unit<ElectricCharge> Coulomb = new("C", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<ElectricCharge> Millicoulomb = new("mC", 0.001);

    /// <summary>Widely used in battery capacity ratings.</summary>
    public static readonly Unit<ElectricCharge> AmpereHour = new("Ah", 3600.0);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<ElectricCharge>> All { get; } = [Coulomb, Millicoulomb, AmpereHour];
}
