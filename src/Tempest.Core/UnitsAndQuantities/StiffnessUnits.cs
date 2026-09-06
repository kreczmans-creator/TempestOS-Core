namespace Tempest.Core.UnitsAndQuantities;

/// <summary>The starting catalogue of <see cref="Unit{TDimension}"/> values for <see cref="Stiffness"/>.</summary>
/// <remarks>See <see cref="LengthUnits"/>'s own remarks — the same "starting set, purely additive" discipline applies.</remarks>
public static class StiffnessUnits
{
    /// <summary>The base unit of <see cref="Stiffness"/> (SI, derived).</summary>
    public static readonly Unit<Stiffness> NewtonPerMetre = new("N/m", 1.0);

    /// <summary>SI — the unit spring catalogues most often quote a rate in.</summary>
    public static readonly Unit<Stiffness> NewtonPerMillimetre = new("N/mm", 1000.0);

    /// <summary>SI.</summary>
    public static readonly Unit<Stiffness> KilonewtonPerMillimetre = new("kN/mm", 1000000.0);

    /// <summary>Imperial.</summary>
    public static readonly Unit<Stiffness> PoundForcePerInch = new("lbf/in", 175.12683524647637);

    /// <summary>Every unit in this catalogue, for use with <see cref="Quantity{TDimension}.TryParse"/>.</summary>
    public static IReadOnlyList<Unit<Stiffness>> All { get; } =
        [NewtonPerMetre, NewtonPerMillimetre, KilonewtonPerMillimetre, PoundForcePerInch];
}
