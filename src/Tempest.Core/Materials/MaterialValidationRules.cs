namespace Tempest.Core.Materials;

/// <summary>
/// The diagnostic codes <see cref="IMaterialValidationService"/> reports.
/// </summary>
/// <remarks>
/// Materials engineering only. The rules about being reference data at all
/// live in <see cref="ReferenceData.ReferenceValidationRules"/>'s own
/// <c>TEMPEST-REF-</c> series, shared with every Group A library.
/// </remarks>
public static class MaterialValidationRules
{
    /// <summary>The record must state a material family — without one, nothing else on it can be interpreted.</summary>
    public const string FamilyMustBeStated = "TEMPEST-MAT-001";

    /// <summary>A material classified <see cref="MaterialFamily.Other"/> must record the source's own classification wording.</summary>
    public const string OtherFamilyNeedsSourceClassification = "TEMPEST-MAT-002";

    /// <summary>A record should carry a designation as well as a name.</summary>
    public const string DesignationShouldBeRecorded = "TEMPEST-MAT-003";

    /// <summary>A well-known property is recorded with the wrong dimension for what it names.</summary>
    public const string PropertyDimensionMismatch = "TEMPEST-MAT-004";

    /// <summary>A property whose physical meaning forbids a negative value is recorded as negative.</summary>
    public const string PropertyMustNotBeNegative = "TEMPEST-MAT-005";

    /// <summary>A property whose physical meaning requires a positive value is recorded as zero or negative.</summary>
    public const string PropertyMustBePositive = "TEMPEST-MAT-006";

    /// <summary>A yield strength is recorded on a family that has no yield point.</summary>
    public const string YieldStrengthNotApplicableToFamily = "TEMPEST-MAT-007";

    /// <summary>A yield strength exceeds the ultimate tensile strength of the same material.</summary>
    public const string YieldStrengthExceedsUltimate = "TEMPEST-MAT-008";

    /// <summary>A minimum service temperature is above the maximum.</summary>
    public const string ServiceTemperatureRangeInverted = "TEMPEST-MAT-009";

    /// <summary>Poisson's ratio lies outside the range a real isotropic material can occupy.</summary>
    public const string PoissonsRatioOutOfRange = "TEMPEST-MAT-010";

    /// <summary>A heat-treatment condition is recorded on a family that has none.</summary>
    public const string ConditionNotApplicableToFamily = "TEMPEST-MAT-011";

    /// <summary>Two records share one supplier and designation.</summary>
    public const string DuplicateDesignation = "TEMPEST-MAT-012";

    /// <summary>A material record carries no engineering property at all.</summary>
    public const string NoPropertiesRecorded = "TEMPEST-MAT-013";
}
