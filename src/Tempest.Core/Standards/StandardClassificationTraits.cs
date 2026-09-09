namespace Tempest.Core.Standards;

/// <summary>
/// Which parts of a standard record are meaningful for a given
/// <see cref="StandardClassification"/> — this library's own type-aware
/// modelling rule, stated once.
/// </summary>
/// <remarks>
/// The same discipline <see cref="Materials.MaterialFamilyTraits"/> applies
/// to material families and <c>BearingFamilyTraits</c> to bearing families.
/// Reading applicability from here lets a comparison report a missing value
/// as
/// <see cref="ReferenceData.ReferencePropertyAvailability.NotApplicable"/>
/// rather than as a data gap: a terminology standard states no conformity
/// requirements, and recording that fact is not the same as failing to
/// record them.
/// </remarks>
public static class StandardClassificationTraits
{
    /// <summary>Whether a standard of this kind states requirements something can conform to.</summary>
    public static bool StatesConformityRequirements(StandardClassification classification) => classification switch
    {
        StandardClassification.Specification or StandardClassification.ManagementSystem
            or StandardClassification.DimensionalStandard => true,
        _ => false
    };

    /// <summary>Whether a standard of this kind defines a measurement procedure.</summary>
    public static bool DefinesTestMethod(StandardClassification classification) =>
        classification is StandardClassification.TestMethod;

    /// <summary>
    /// Whether a standard of this kind is one another record would
    /// legitimately cite as the basis of a dimensioned value — the
    /// distinction between "this table came from a dimensional standard"
    /// and "this table came from a glossary".
    /// </summary>
    public static bool CanSourceEngineeringValues(StandardClassification classification) => classification switch
    {
        StandardClassification.Specification or StandardClassification.DimensionalStandard
            or StandardClassification.TestMethod => true,
        _ => false
    };

    /// <summary>
    /// Whether this table can speak for <paramref name="classification"/>
    /// at all. <see cref="StandardClassification.Unspecified"/> and
    /// <see cref="StandardClassification.Other"/> are unclassified by
    /// construction: every answer above is conservative for them and must
    /// be read as "not known to apply", never "known not to apply".
    /// </summary>
    public static bool IsApplicabilityKnown(StandardClassification classification) =>
        classification is not (StandardClassification.Unspecified or StandardClassification.Other);
}
