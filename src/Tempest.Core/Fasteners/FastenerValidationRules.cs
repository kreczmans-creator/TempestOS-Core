namespace Tempest.Core.Fasteners;

/// <summary>
/// The diagnostic codes <see cref="IFastenerValidationService"/> reports.
/// </summary>
/// <remarks>
/// Fastener engineering only. The rules about being reference data at all
/// live in <see cref="ReferenceData.ReferenceValidationRules"/>'s own
/// <c>TEMPEST-REF-</c> series, shared with every Group A library, and are
/// not restated here.
/// </remarks>
public static class FastenerValidationRules
{
    /// <summary>The record states no fastener family — without one, nothing else on it can be interpreted.</summary>
    public const string FamilyMustBeStated = "TEMPEST-FST-001";

    /// <summary>A fastener classified <c>Other</c> in any of its taxonomies must record the source's own wording.</summary>
    public const string OtherClassificationNeedsSourceClassification = "TEMPEST-FST-002";

    /// <summary>A threaded family records no thread specification.</summary>
    public const string ThreadMustBeRecordedForAThreadedFamily = "TEMPEST-FST-003";

    /// <summary>A thread is recorded on a family that has none.</summary>
    public const string ThreadNotApplicableToFamily = "TEMPEST-FST-004";

    /// <summary>A head type other than <see cref="FastenerHeadType.None"/> is recorded on a family that has no head.</summary>
    public const string HeadNotApplicableToFamily = "TEMPEST-FST-005";

    /// <summary>A drive type other than <see cref="FastenerDriveType.None"/> is recorded on a family that has no driving feature.</summary>
    public const string DriveNotApplicableToFamily = "TEMPEST-FST-006";

    /// <summary>A dimension whose physical meaning requires a positive value is recorded as zero or negative.</summary>
    public const string DimensionMustBePositive = "TEMPEST-FST-007";

    /// <summary>A mechanical property whose physical meaning requires a positive value is recorded as zero or negative.</summary>
    public const string MechanicalValueMustBePositive = "TEMPEST-FST-008";

    /// <summary>A thread pitch is not smaller than the nominal diameter it belongs to.</summary>
    public const string PitchExceedsNominalDiameter = "TEMPEST-FST-009";

    /// <summary>A width across corners is not greater than the width across flats of the same fastener.</summary>
    public const string WidthAcrossCornersNotGreaterThanFlats = "TEMPEST-FST-010";

    /// <summary>A yield or proof strength exceeds the tensile strength of the same fastener.</summary>
    public const string StrengthExceedsTensile = "TEMPEST-FST-011";

    /// <summary>A proof load exceeds the minimum breaking load of the same fastener.</summary>
    public const string ProofLoadExceedsBreakingLoad = "TEMPEST-FST-012";

    /// <summary>A property class is recorded on a family that carries none.</summary>
    public const string PropertyClassNotApplicableToFamily = "TEMPEST-FST-013";

    /// <summary>A tightening torque is recorded without the friction or lubrication conditions it was published for.</summary>
    public const string TorqueReferenceStatesNoConditions = "TEMPEST-FST-014";

    /// <summary>A tightening torque is recorded on a family that is not tightened.</summary>
    public const string TorqueNotApplicableToFamily = "TEMPEST-FST-015";

    /// <summary>A hardness band's own maximum is below its own minimum.</summary>
    public const string HardnessBandInverted = "TEMPEST-FST-016";

    /// <summary>A material is named in text but not linked to a registered A1 record.</summary>
    public const string MaterialShouldBeLinked = "TEMPEST-FST-017";

    /// <summary>Two records share one fastener identity key.</summary>
    public const string DuplicateIdentity = "TEMPEST-FST-018";

    /// <summary>The record carries no dimension and no mechanical property at all.</summary>
    public const string NoEngineeringDataRecorded = "TEMPEST-FST-019";

    /// <summary>A thread's handedness is not recorded, so a left-hand thread cannot be told from a right-hand one.</summary>
    public const string ThreadHandednessShouldBeRecorded = "TEMPEST-FST-020";
}
