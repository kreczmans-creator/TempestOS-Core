namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Dimensionless"/>.</summary>
/// <remarks>
/// A dimensionless quantity still carries a unit here, deliberately: a
/// ratio recorded as <c>0.02</c> and one recorded as <c>2 %</c> are the
/// same quantity written two ways, and this framework's own discipline is
/// that a value never travels without the unit it was written in.
/// </remarks>
public static class DimensionlessUnits
{
    /// <summary>The base unit of <see cref="Dimensionless"/> — the unit one.</summary>
    public static readonly Unit<Dimensionless> One = new("1", 1.0);

    /// <summary>Parts per hundred.</summary>
    public static readonly Unit<Dimensionless> Percent = new("%", 0.01);

    /// <summary>Parts per thousand.</summary>
    public static readonly Unit<Dimensionless> PerMille = new("ppt", 0.001);

    /// <summary>Parts per million.</summary>
    public static readonly Unit<Dimensionless> PartsPerMillion = new("ppm", 0.000001);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Dimensionless>> All { get; } = [One, Percent, PerMille, PartsPerMillion];
}
