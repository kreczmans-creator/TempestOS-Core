namespace Tempest.Core.Materials;

/// <summary>
/// Which properties are meaningful for a given <see cref="MaterialFamily"/>
/// — this library's own type-aware modelling rule, and the single place it
/// is stated.
/// </summary>
/// <remarks>
/// The same discipline A4 established for bearing families. Reading
/// applicability from here lets a missing value be reported as
/// <see cref="ReferenceData.ReferencePropertyAvailability.NotApplicable"/>
/// — genuinely distinct from a data gap.
/// </remarks>
public static class MaterialFamilyTraits
{
    /// <summary>Whether <paramref name="family"/> is a metal.</summary>
    public static bool IsMetal(MaterialFamily family) => family switch
    {
        MaterialFamily.Steel or MaterialFamily.StainlessSteel or MaterialFamily.CastIron
            or MaterialFamily.Aluminium or MaterialFamily.CopperAlloy or MaterialFamily.Titanium
            or MaterialFamily.NickelAlloy or MaterialFamily.OtherMetal => true,
        _ => false
    };

    /// <summary>Whether <paramref name="family"/> is a polymer or elastomer.</summary>
    public static bool IsPolymer(MaterialFamily family) =>
        family is MaterialFamily.Thermoplastic or MaterialFamily.Thermoset or MaterialFamily.Elastomer;

    /// <summary>
    /// Whether a yield strength is a meaningful property of
    /// <paramref name="family"/>. Ceramics and glasses fail in a brittle
    /// manner without a yield point, so a yield strength recorded against
    /// one is a modelling error rather than a data gap.
    /// </summary>
    public static bool HasYieldStrength(MaterialFamily family) => family switch
    {
        MaterialFamily.Ceramic or MaterialFamily.Glass => false,
        _ => true
    };

    /// <summary>
    /// Whether a heat-treatment condition or temper is a meaningful part of
    /// this family's own specification.
    /// </summary>
    public static bool HasHeatTreatmentCondition(MaterialFamily family) => IsMetal(family);

    /// <summary>Whether an anisotropic material may legitimately record direction-dependent properties.</summary>
    public static bool MayBeAnisotropic(MaterialFamily family) =>
        family is MaterialFamily.Composite or MaterialFamily.Thermoplastic or MaterialFamily.Other;

    /// <summary>
    /// Whether this table can speak for <paramref name="family"/> at all.
    /// <see cref="MaterialFamily.Unspecified"/> and
    /// <see cref="MaterialFamily.Other"/> are unclassified by construction:
    /// every answer above is conservative for them and must be read as "not
    /// known to apply", never "known not to apply".
    /// </summary>
    public static bool IsApplicabilityKnown(MaterialFamily family) =>
        family is not (MaterialFamily.Unspecified or MaterialFamily.Other);
}
