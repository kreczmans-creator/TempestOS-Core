namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Torque"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class TorqueUnits
{
    /// <summary>The base unit of <see cref="Torque"/> (SI, derived).</summary>
    public static readonly Unit<Torque> NewtonMetre = new("N.m", 1.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Torque> NewtonMillimetre = new("N.mm", 0.001);

    /// <summary>SI.</summary>
    public static readonly Unit<Torque> KilonewtonMetre = new("kN.m", 1000.0);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Torque> PoundForceFoot = new("lbf.ft", 1.3558179483314004);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Torque> PoundForceInch = new("lbf.in", 0.1129848290276167);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Torque>> All { get; } =
        [NewtonMetre, NewtonMillimetre, KilonewtonMetre, PoundForceFoot, PoundForceInch];
}
