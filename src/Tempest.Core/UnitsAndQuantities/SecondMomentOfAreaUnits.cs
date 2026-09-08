namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="SecondMomentOfArea"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class SecondMomentOfAreaUnits
{
    /// <summary>The base unit of <see cref="SecondMomentOfArea"/> (SI, derived).</summary>
    public static readonly Unit<SecondMomentOfArea> MetreToTheFourth = new("m^4", 1.0);

    /// <summary>SI — the unit a rolled-section catalogue most often quotes.</summary>
    public static readonly Unit<SecondMomentOfArea> MillimetreToTheFourth = new("mm^4", 1e-12);

    /// <summary>SI.</summary>
    public static readonly Unit<SecondMomentOfArea> CentimetreToTheFourth = new("cm^4", 1e-8);

    /// <summary>Imperial.</summary>
    public static readonly Unit<SecondMomentOfArea> InchToTheFourth = new("in^4", 4.162314256e-7);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<SecondMomentOfArea>> All { get; } =
        [MetreToTheFourth, MillimetreToTheFourth, CentimetreToTheFourth, InchToTheFourth];
}
