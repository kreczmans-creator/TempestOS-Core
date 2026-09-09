using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Materials;

/// <summary>
/// The well-known material property names, and the dimension each must
/// carry.
/// </summary>
/// <remarks>
/// <para>
/// <b>A controlled vocabulary, not a closed one.</b> `ADR-0055` explicitly
/// rejected fixing the set of property names, on the grounds that closing
/// it would encode assumptions about which properties matter. That
/// decision stands: a caller may still record any name it likes, and this
/// library will store it. What this type adds is that the names engineers
/// actually share have one spelling and one expected dimension, so a
/// density recorded as a pressure is caught rather than stored.
/// </para>
/// <para>
/// An unknown name is not an error and never will be — see
/// <see cref="ExpectedDimensionOf"/>, which simply returns
/// <see langword="null"/> for one.
/// </para>
/// </remarks>
public static class MaterialPropertyNames
{
    /// <summary>Mass per unit volume.</summary>
    public const string Density = "Density";

    /// <summary>Young's modulus (modulus of elasticity in tension).</summary>
    public const string YoungsModulus = "YoungsModulus";

    /// <summary>Shear modulus (modulus of rigidity).</summary>
    public const string ShearModulus = "ShearModulus";

    /// <summary>Poisson's ratio — dimensionless.</summary>
    public const string PoissonsRatio = "PoissonsRatio";

    /// <summary>Yield strength (0.2% proof stress, where the source states one).</summary>
    public const string YieldStrength = "YieldStrength";

    /// <summary>Ultimate tensile strength.</summary>
    public const string UltimateTensileStrength = "UltimateTensileStrength";

    /// <summary>Compressive strength.</summary>
    public const string CompressiveStrength = "CompressiveStrength";

    /// <summary>Fatigue or endurance strength, at the cycle count the source states in the property's own conditions.</summary>
    public const string FatigueStrength = "FatigueStrength";

    /// <summary>Elongation at break — dimensionless (a ratio, or a percentage).</summary>
    public const string ElongationAtBreak = "ElongationAtBreak";

    /// <summary>Notched impact energy, at the temperature the source states in the property's own conditions.</summary>
    public const string ImpactEnergy = "ImpactEnergy";

    /// <summary>Thermal conductivity.</summary>
    public const string ThermalConductivity = "ThermalConductivity";

    /// <summary>Coefficient of linear thermal expansion.</summary>
    public const string ThermalExpansionCoefficient = "ThermalExpansionCoefficient";

    /// <summary>Specific heat capacity.</summary>
    public const string SpecificHeatCapacity = "SpecificHeatCapacity";

    /// <summary>Melting point, or the solidus where the source states a range.</summary>
    public const string MeltingPoint = "MeltingPoint";

    /// <summary>Maximum continuous service temperature, as the source states it.</summary>
    public const string MaximumServiceTemperature = "MaximumServiceTemperature";

    /// <summary>Minimum service temperature, as the source states it.</summary>
    public const string MinimumServiceTemperature = "MinimumServiceTemperature";

    private static readonly IReadOnlyDictionary<string, string> Expected = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Density] = nameof(MassDensity),
        [YoungsModulus] = nameof(Pressure),
        [ShearModulus] = nameof(Pressure),
        [PoissonsRatio] = nameof(Dimensionless),
        [YieldStrength] = nameof(Pressure),
        [UltimateTensileStrength] = nameof(Pressure),
        [CompressiveStrength] = nameof(Pressure),
        [FatigueStrength] = nameof(Pressure),
        [ElongationAtBreak] = nameof(Dimensionless),
        [ImpactEnergy] = nameof(Energy),
        [ThermalConductivity] = nameof(UnitsAndQuantities.ThermalConductivity),
        [ThermalExpansionCoefficient] = nameof(ThermalExpansion),
        [SpecificHeatCapacity] = nameof(UnitsAndQuantities.SpecificHeatCapacity),
        [MeltingPoint] = nameof(Temperature),
        [MaximumServiceTemperature] = nameof(Temperature),
        [MinimumServiceTemperature] = nameof(Temperature),
    };

    /// <summary>Every well-known name, in a fixed order.</summary>
    public static IReadOnlyList<string> All { get; } = Expected.Keys.ToList();

    /// <summary>
    /// The dimension <paramref name="propertyName"/> must carry, or
    /// <see langword="null"/> where the name is not one this vocabulary
    /// knows — which is legitimate, not an error.
    /// </summary>
    public static string? ExpectedDimensionOf(string propertyName) =>
        Expected.TryGetValue(propertyName, out var dimension) ? dimension : null;

    /// <summary>Whether <paramref name="propertyName"/> is one this vocabulary names.</summary>
    public static bool IsWellKnown(string propertyName) => Expected.ContainsKey(propertyName);
}
