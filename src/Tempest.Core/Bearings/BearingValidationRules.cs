namespace Tempest.Core.Bearings;

/// <summary>
/// The diagnostic codes <see cref="IBearingValidationService"/> reports,
/// catalogued in one place so a consumer can act on a code rather than on
/// a message string.
/// </summary>
/// <remarks>
/// Bearing engineering only. The rules about being reference data at all
/// — provenance completeness, verification attributability, supersession,
/// derived values, unresolved material and standard references — are
/// <see cref="ReferenceData.ReferenceValidationRules"/>'s own
/// <c>TEMPEST-REF-</c> series, shared with every Group A library and not
/// restated here. Five codes this library once declared for those rules
/// (<c>TEMPEST-BRG-010</c>, <c>-011</c>, <c>-018</c>, <c>-019</c>,
/// <c>-020</c>) moved there when the shared layer was extracted; their
/// numbers are deliberately left unreused, so a reader meeting one in an
/// older report can still find what it meant.
/// </remarks>
public static class BearingValidationRules
{
    /// <summary>Bore diameter must be greater than zero where it is recorded.</summary>
    public const string BoreMustBePositive = "TEMPEST-BRG-001";

    /// <summary>Outside diameter must be greater than the bore.</summary>
    public const string OutsideDiameterMustExceedBore = "TEMPEST-BRG-002";

    /// <summary>Width must be greater than zero where it is recorded.</summary>
    public const string WidthMustBePositive = "TEMPEST-BRG-003";

    /// <summary>A recorded load rating must be greater than zero — a rating of zero is not a rating.</summary>
    public const string LoadRatingMustBePositive = "TEMPEST-BRG-004";

    /// <summary>A recorded speed rating must be greater than zero.</summary>
    public const string SpeedRatingMustBePositive = "TEMPEST-BRG-005";

    /// <summary>Mass must not be negative where it is recorded.</summary>
    public const string MassMustNotBeNegative = "TEMPEST-BRG-006";

    /// <summary>The record must state a bearing family — without one, nothing else on it can be interpreted.</summary>
    public const string FamilyMustBeStated = "TEMPEST-BRG-007";

    /// <summary>A bearing classified <see cref="BearingFamily.Other"/> must record the source's own wording for its type.</summary>
    public const string OtherFamilyNeedsDesignation = "TEMPEST-BRG-008";

    /// <summary>A record should carry a designation as well as a part number.</summary>
    public const string DesignationShouldBeRecorded = "TEMPEST-BRG-009";

    /// <summary>A contact angle is recorded on a family for which a nominal contact angle is not a defining characteristic.</summary>
    public const string ContactAngleNotApplicableToFamily = "TEMPEST-BRG-012";

    /// <summary>A recorded contact angle must lie between zero and ninety degrees exclusive of zero.</summary>
    public const string ContactAngleOutOfRange = "TEMPEST-BRG-013";

    /// <summary>An internal clearance or preload class is recorded on a family that has no rolling elements.</summary>
    public const string ClearanceNotApplicableToFamily = "TEMPEST-BRG-014";

    /// <summary>A rolling-element material is recorded on a family that has no rolling elements.</summary>
    public const string RollingElementNotApplicableToFamily = "TEMPEST-BRG-015";

    /// <summary>The maximum radial internal clearance must not be less than the minimum.</summary>
    public const string ClearanceRangeInverted = "TEMPEST-BRG-016";

    /// <summary>Two records share one manufacturer and manufacturer part number.</summary>
    public const string DuplicatePartNumber = "TEMPEST-BRG-017";

    /// <summary>An overall width, where recorded, must not be less than the nominal width.</summary>
    public const string OverallWidthLessThanWidth = "TEMPEST-BRG-021";

    /// <summary>A rolling-element bearing records no load rating of any kind.</summary>
    public const string NoLoadRatingRecorded = "TEMPEST-BRG-022";
}
