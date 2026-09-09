namespace Tempest.Core.ReferenceData;

/// <summary>
/// The diagnostic codes every Group A library shares — the rules that are
/// about being reference data at all, rather than about any one domain.
/// </summary>
/// <remarks>
/// Numbered in a <c>TEMPEST-REF-</c> series of their own rather than
/// continuing <see cref="EngineeringDomain.StructuralValidationRules"/>'s
/// <c>TEMPEST-VAL-</c> numbering: those codes are scoped to
/// <see cref="EngineeringDomain.IEngineeringObject"/> structural
/// integrity, which no reference record is, and interleaving unrelated
/// rule families in one number space is exactly how
/// <c>TEMPEST-VAL-002</c>'s own documented collision happened. Each
/// library adds its own domain codes in its own series.
/// </remarks>
public static class ReferenceValidationRules
{
    /// <summary>Provenance must name a source organisation and a source document.</summary>
    public const string ProvenanceMustIdentifyASource = "TEMPEST-REF-001";

    /// <summary>A record marked verified must name the reviewer and the date of verification.</summary>
    public const string VerificationMustBeAttributable = "TEMPEST-REF-002";

    /// <summary>A superseded record does not name the record that replaced it.</summary>
    public const string SupersededWithoutReplacement = "TEMPEST-REF-003";

    /// <summary>A value is marked as derived by TempestOS, and so must not be read as source reference data.</summary>
    public const string DerivedValuePresent = "TEMPEST-REF-004";

    /// <summary>A recorded range's own maximum is below its own minimum.</summary>
    public const string RangeInverted = "TEMPEST-REF-005";

    /// <summary>A cited standard does not resolve to a registered Standards Library record.</summary>
    public const string StandardReferenceUnresolved = "TEMPEST-REF-006";

    /// <summary>A referenced <c>materialId</c> is not registered in the canonical Materials catalogue.</summary>
    public const string MaterialReferenceUnresolved = "TEMPEST-REF-007";

    /// <summary>Two records in one library share a secondary key the library enforces as unique.</summary>
    public const string DuplicateSecondaryKey = "TEMPEST-REF-008";
}
