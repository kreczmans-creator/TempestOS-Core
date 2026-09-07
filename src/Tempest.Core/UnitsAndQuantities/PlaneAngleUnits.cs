namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="PlaneAngle"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class PlaneAngleUnits
{
    /// <summary>The base unit of <see cref="PlaneAngle"/> (SI, derived).</summary>
    public static readonly Unit<PlaneAngle> Radian = new("rad", 1.0);

    /// <summary>SI-accepted — the unit bearing catalogues quote contact angles in.</summary>
    public static readonly Unit<PlaneAngle> Degree = new("deg", Math.PI / 180.0);

    /// <summary>SI-accepted.</summary>
    public static readonly Unit<PlaneAngle> ArcMinute = new("arcmin", Math.PI / 10800.0);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<PlaneAngle>> All { get; } = [Radian, Degree, ArcMinute];
}
