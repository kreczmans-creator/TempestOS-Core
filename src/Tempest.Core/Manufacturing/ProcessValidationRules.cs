namespace Tempest.Core.Manufacturing;

/// <summary>
/// The diagnostic codes <see cref="IProcessValidationService"/> reports.
/// </summary>
/// <remarks>
/// Manufacturing process reference-keeping only. The rules about being
/// reference data at all live in
/// <see cref="ReferenceData.ReferenceValidationRules"/>'s own
/// <c>TEMPEST-REF-</c> series, shared with every Group A library, and are
/// not restated here — including the inverted-range rule, which every
/// capability band is checked against.
/// </remarks>
public static class ProcessValidationRules
{
    /// <summary>The record states no process family — without one, nothing else on it can be interpreted.</summary>
    public const string FamilyMustBeStated = "TEMPEST-MFG-001";

    /// <summary>A process classified <see cref="ProcessFamily.Other"/> must record the source's own classification wording.</summary>
    public const string OtherFamilyNeedsSourceClassification = "TEMPEST-MFG-002";

    /// <summary>The record states no capability at all.</summary>
    public const string NoCapabilityRecorded = "TEMPEST-MFG-003";

    /// <summary>A capability whose physical meaning requires a positive value is recorded as zero or negative.</summary>
    public const string CapabilityMustBePositive = "TEMPEST-MFG-004";

    /// <summary>A capability is recorded for a process it does not describe.</summary>
    public const string CapabilityNotApplicableToFamily = "TEMPEST-MFG-005";

    /// <summary>A recorded capability band does not say where it came from.</summary>
    public const string CapabilityOriginShouldBeRecorded = "TEMPEST-MFG-006";

    /// <summary>A recorded capability band does not say what conditions it holds under.</summary>
    public const string CapabilityConditionsShouldBeRecorded = "TEMPEST-MFG-007";

    /// <summary>A material compatibility entry names no material, no family and no designation.</summary>
    public const string CompatibilityMustNameAMaterial = "TEMPEST-MFG-008";

    /// <summary>A material compatibility entry does not say whether the pairing works.</summary>
    public const string CompatibilitySuitabilityShouldBeStated = "TEMPEST-MFG-009";

    /// <summary>A conditionally suitable pairing does not state the conditions that make it so.</summary>
    public const string ConditionalCompatibilityNeedsConditions = "TEMPEST-MFG-010";

    /// <summary>The record states two contradictory things about the same material.</summary>
    public const string ContradictoryCompatibility = "TEMPEST-MFG-011";

    /// <summary>The record states the same thing about the same material twice.</summary>
    public const string DuplicateCompatibilityEntry = "TEMPEST-MFG-012";

    /// <summary>The record associates the process with no material at all.</summary>
    public const string NoMaterialCompatibilityRecorded = "TEMPEST-MFG-013";

    /// <summary>The record associates the process with no production scale.</summary>
    public const string ProductionScaleShouldBeRecorded = "TEMPEST-MFG-014";

    /// <summary>A production scale list records <see cref="ProductionScale.Unspecified"/> alongside a real scale, which says two things at once.</summary>
    public const string UnspecifiedProductionScaleAlongsideAReal = "TEMPEST-MFG-015";

    /// <summary>A constraint does not say what kind of limitation it describes.</summary>
    public const string ConstraintKindShouldBeStated = "TEMPEST-MFG-016";

    /// <summary>Two records share one process identity key.</summary>
    public const string DuplicateProcessIdentity = "TEMPEST-MFG-017";
}
