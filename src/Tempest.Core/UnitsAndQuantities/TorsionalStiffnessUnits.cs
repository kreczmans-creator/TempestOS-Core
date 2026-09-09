namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="TorsionalStiffness"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class TorsionalStiffnessUnits
{
    /// <summary>The base unit of <see cref="TorsionalStiffness"/> (SI, derived).</summary>
    public static readonly Unit<TorsionalStiffness> NewtonMetrePerRadian = new("N.m/rad", 1.0);

    /// <summary>SI, in the per-degree form spring and coupling catalogues most often publish a rate in.</summary>
    public static readonly Unit<TorsionalStiffness> NewtonMetrePerDegree = new("N.m/deg", 180.0 / Math.PI);

    /// <summary>SI, the small-component form.</summary>
    public static readonly Unit<TorsionalStiffness> NewtonMillimetrePerDegree = new("N.mm/deg", 0.180 / Math.PI);

    /// <summary>Imperial.</summary>
    public static readonly Unit<TorsionalStiffness> PoundForceInchPerDegree = new("lbf.in/deg", 0.1129848290276167 * 180.0 / Math.PI);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<TorsionalStiffness>> All { get; } =
        [NewtonMetrePerRadian, NewtonMetrePerDegree, NewtonMillimetrePerDegree, PoundForceInchPerDegree];
}
