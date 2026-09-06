namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Power"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class PowerUnits
{
    /// <summary>The base unit of <see cref="Power"/> (SI, derived).</summary>
    public static readonly Unit<Power> Watt = new("W", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Power> Kilowatt = new("kW", 1000.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Power> Megawatt = new("MW", 1000000.0);

    /// <summary>
    /// Mechanical horsepower, as catalogues that quote it define it.
    /// Metric horsepower is a different unit with a different factor and
    /// is deliberately not offered under the same name: a source quoting
    /// one must not be silently read as quoting the other.
    /// </summary>
    public static readonly Unit<Power> MechanicalHorsepower = new("hp", 745.6998715822702);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Power>> All { get; } = [Watt, Kilowatt, Megawatt, MechanicalHorsepower];
}
