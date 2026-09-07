namespace Tempest.Core.Components;

/// <summary>
/// The diagnostic codes <see cref="IComponentValidationService"/> reports.
/// </summary>
/// <remarks>
/// Mechanical component engineering only. The rules about being reference
/// data at all live in
/// <see cref="ReferenceData.ReferenceValidationRules"/>'s own
/// <c>TEMPEST-REF-</c> series, shared with every Group A library, and are
/// not restated here.
/// </remarks>
public static class ComponentValidationRules
{
    /// <summary>The record states no component family — without one, nothing else on it can be interpreted.</summary>
    public const string FamilyMustBeStated = "TEMPEST-CMP-001";

    /// <summary>A component classified <see cref="ComponentFamily.Other"/> must record the source's own classification wording.</summary>
    public const string OtherFamilyNeedsSourceClassification = "TEMPEST-CMP-002";

    /// <summary>A typed detail record is present for a family it does not describe.</summary>
    public const string DetailNotApplicableToFamily = "TEMPEST-CMP-003";

    /// <summary>More than one typed detail record is present, so the record describes two different kinds of component at once.</summary>
    public const string MultipleDetailsRecorded = "TEMPEST-CMP-004";

    /// <summary>A family that has a typed detail record records none.</summary>
    public const string DetailShouldBeRecordedForFamily = "TEMPEST-CMP-005";

    /// <summary>A dimension or rate whose physical meaning requires a positive value is recorded as zero or negative.</summary>
    public const string ValueMustBePositive = "TEMPEST-CMP-006";

    /// <summary>A spring's own rate is recorded in the wrong form for its family — a torsional rate on a translational spring, or the reverse.</summary>
    public const string SpringRateFormDoesNotMatchFamily = "TEMPEST-CMP-007";

    /// <summary>A spring's own solid length is not shorter than its free length.</summary>
    public const string SolidLengthNotShorterThanFreeLength = "TEMPEST-CMP-008";

    /// <summary>A spring's own active coil count exceeds its total coil count.</summary>
    public const string ActiveCoilsExceedTotalCoils = "TEMPEST-CMP-009";

    /// <summary>A helical spring's own inside diameter is not smaller than its outside diameter.</summary>
    public const string InsideDiameterNotSmallerThanOutside = "TEMPEST-CMP-010";

    /// <summary>A helical spring's own wire diameter is not consistent with its own recorded coil diameters.</summary>
    public const string WireDiameterInconsistentWithCoilDiameters = "TEMPEST-CMP-011";

    /// <summary>A gear's own tooth count is not a positive whole number.</summary>
    public const string ToothCountMustBePositive = "TEMPEST-CMP-012";

    /// <summary>A gear's own pressure angle lies outside the range a real involute gear can occupy.</summary>
    public const string PressureAngleOutOfRange = "TEMPEST-CMP-013";

    /// <summary>A helix angle is recorded on a family whose teeth are not on a helix, or is zero where the family requires one.</summary>
    public const string HelixAngleDoesNotMatchFamily = "TEMPEST-CMP-014";

    /// <summary>A gear's own outside diameter is not greater than its pitch diameter, which no external gear's tips can be.</summary>
    public const string OutsideDiameterNotGreaterThanPitchDiameter = "TEMPEST-CMP-015";

    /// <summary>A bore is not smaller than the outside diameter of the same component.</summary>
    public const string BoreNotSmallerThanOutsideDiameter = "TEMPEST-CMP-016";

    /// <summary>A bore is recorded on a family that has none.</summary>
    public const string BoreNotApplicableToFamily = "TEMPEST-CMP-017";

    /// <summary>A speed rating is recorded on a family that does not rotate.</summary>
    public const string SpeedRatingNotApplicableToFamily = "TEMPEST-CMP-018";

    /// <summary>A torque rating is recorded on a family that transmits none.</summary>
    public const string TorqueRatingNotApplicableToFamily = "TEMPEST-CMP-019";

    /// <summary>A rated torque exceeds the maximum torque of the same component.</summary>
    public const string RatedTorqueExceedsMaximumTorque = "TEMPEST-CMP-020";

    /// <summary>A helical spring records no winding direction, or a helical gear no helix hand.</summary>
    public const string HandednessShouldBeRecorded = "TEMPEST-CMP-021";

    /// <summary>A material is named in text but not linked to a registered A1 record.</summary>
    public const string MaterialShouldBeLinked = "TEMPEST-CMP-022";

    /// <summary>Two records share one component identity key.</summary>
    public const string DuplicateIdentity = "TEMPEST-CMP-023";

    /// <summary>The record carries no dimension, no rating and no typed detail at all.</summary>
    public const string NoEngineeringDataRecorded = "TEMPEST-CMP-024";
}
