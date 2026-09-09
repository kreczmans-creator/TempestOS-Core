namespace Tempest.Core.Standards;

/// <summary>
/// The diagnostic codes <see cref="IStandardValidationService"/> reports.
/// </summary>
/// <remarks>
/// Standards register-keeping only. The rules about being reference data
/// at all live in <see cref="ReferenceData.ReferenceValidationRules"/>'s
/// own <c>TEMPEST-REF-</c> series, shared with every Group A library, and
/// are not restated here.
/// </remarks>
public static class StandardValidationRules
{
    /// <summary>The record should record the standard's own published title.</summary>
    public const string TitleShouldBeRecorded = "TEMPEST-STD-001";

    /// <summary>The record states no edition, so it cannot be cited with precision.</summary>
    public const string EditionShouldBeRecorded = "TEMPEST-STD-002";

    /// <summary>The record states no classification, so which of its fields are meaningful cannot be determined.</summary>
    public const string ClassificationShouldBeStated = "TEMPEST-STD-003";

    /// <summary>A standard classified <c>Other</c>, or published by a body classified <c>Other</c>, must record the source's own classification wording.</summary>
    public const string OtherClassificationNeedsSourceClassification = "TEMPEST-STD-004";

    /// <summary>The publisher's own status for the standard is not recorded.</summary>
    public const string PublicationStatusShouldBeStated = "TEMPEST-STD-005";

    /// <summary>A standard the publisher has taken out of force records no withdrawal date.</summary>
    public const string WithdrawalDateShouldBeRecorded = "TEMPEST-STD-006";

    /// <summary>A standard the publisher still holds current records a withdrawal date.</summary>
    public const string CurrentStandardHasWithdrawalDate = "TEMPEST-STD-007";

    /// <summary>A date on the record precedes one it cannot precede.</summary>
    public const string DatesOutOfOrder = "TEMPEST-STD-008";

    /// <summary>The recorded scope summary is long enough to suggest the standard's own scope clause has been reproduced rather than summarised.</summary>
    public const string ScopeSummaryMayBeReproducedText = "TEMPEST-STD-009";

    /// <summary>The record names itself as an equivalent, a normative reference, or a designation it replaces.</summary>
    public const string SelfReference = "TEMPEST-STD-010";

    /// <summary>Two records share one body, designation and edition.</summary>
    public const string DuplicateDesignation = "TEMPEST-STD-011";

    /// <summary>The record is superseded in TempestOS but still records the publisher as holding the standard current.</summary>
    public const string SupersededRecordStillMarkedCurrent = "TEMPEST-STD-012";

    /// <summary>No discipline is recorded, so the standard cannot be found by subject.</summary>
    public const string DisciplineShouldBeRecorded = "TEMPEST-STD-013";

    /// <summary>An equivalence is recorded but its origin — who claimed it — is not.</summary>
    public const string EquivalenceOriginShouldBeRecorded = "TEMPEST-STD-014";
}
