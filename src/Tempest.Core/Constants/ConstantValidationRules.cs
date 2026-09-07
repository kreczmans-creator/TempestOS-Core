namespace Tempest.Core.Constants;

/// <summary>
/// The diagnostic codes <see cref="IConstantValidationService"/> reports.
/// </summary>
/// <remarks>
/// Constants-library rules only. The rules about being reference data at
/// all live in <see cref="ReferenceData.ReferenceValidationRules"/>'s own
/// <c>TEMPEST-REF-</c> series, shared with every Group A library, and are
/// not restated here.
/// </remarks>
public static class ConstantValidationRules
{
    /// <summary>The record states no value, so it is not a constant.</summary>
    public const string ValueMustBeRecorded = "TEMPEST-CON-001";

    /// <summary>The record states no category, so where its authority comes from cannot be determined.</summary>
    public const string CategoryShouldBeStated = "TEMPEST-CON-002";

    /// <summary>A constant categorised <see cref="ConstantCategory.Other"/> must record the source's own classification wording.</summary>
    public const string OtherCategoryNeedsSourceClassification = "TEMPEST-CON-003";

    /// <summary>The source stated nothing about how well the value is known.</summary>
    public const string UncertaintyShouldBeRecorded = "TEMPEST-CON-004";

    /// <summary>A constant recorded as exact also carries an uncertainty figure.</summary>
    public const string ExactConstantCarriesUncertainty = "TEMPEST-CON-005";

    /// <summary>An uncertainty figure is negative, which describes nothing.</summary>
    public const string UncertaintyMustNotBeNegative = "TEMPEST-CON-006";

    /// <summary>An absolute uncertainty does not carry the same dimension as the value it qualifies.</summary>
    public const string UncertaintyDimensionMismatch = "TEMPEST-CON-007";

    /// <summary>An expanded uncertainty records no coverage factor, so what it expands by is unknown.</summary>
    public const string ExpandedUncertaintyNeedsCoverageFactor = "TEMPEST-CON-008";

    /// <summary>A coverage factor is zero or negative.</summary>
    public const string CoverageFactorMustBePositive = "TEMPEST-CON-009";

    /// <summary>A relative uncertainty of one or more means the value is not known at all, which no published constant is.</summary>
    public const string RelativeUncertaintyImplausible = "TEMPEST-CON-010";

    /// <summary>A mathematical constant carries a dimension, which no mathematical constant has.</summary>
    public const string MathematicalConstantMustBeDimensionless = "TEMPEST-CON-011";

    /// <summary>A category whose values are true only within a convention records no statement of where it applies.</summary>
    public const string ApplicabilityShouldBeRecorded = "TEMPEST-CON-012";

    /// <summary>Two records share one symbol.</summary>
    public const string DuplicateSymbol = "TEMPEST-CON-013";

    /// <summary>One record's own symbol is listed as another record's alternative symbol, so a lookup on it is ambiguous to a reader even where the index is not.</summary>
    public const string SymbolCollidesWithAnAlternative = "TEMPEST-CON-014";
}
