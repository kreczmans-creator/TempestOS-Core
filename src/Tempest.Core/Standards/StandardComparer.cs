using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>The stable property keys a standards comparison uses for its own rows.</summary>
public static class StandardComparisonProperties
{
    /// <summary>The publishing body's own code.</summary>
    public const string Body = "Body";

    /// <summary>The standard's designation.</summary>
    public const string Designation = "Designation";

    /// <summary>The edition this record describes.</summary>
    public const string Edition = "Edition";

    /// <summary>The published title.</summary>
    public const string Title = "Title";

    /// <summary>What kind of document the standard is.</summary>
    public const string Classification = "Classification";

    /// <summary>The engineering subjects it covers.</summary>
    public const string Disciplines = "Disciplines";

    /// <summary>The publisher's own status — never the record's validation state.</summary>
    public const string PublicationStatus = "PublicationStatus";

    /// <summary>The publication date.</summary>
    public const string PublicationDate = "PublicationDate";

    /// <summary>The date the standard took effect.</summary>
    public const string EffectiveDate = "EffectiveDate";

    /// <summary>The date the publisher withdrew it.</summary>
    public const string WithdrawalDate = "WithdrawalDate";

    /// <summary>Whether the standard states requirements something can conform to.</summary>
    public const string StatesConformityRequirements = "StatesConformityRequirements";

    /// <summary>The number of standards recorded as equivalent.</summary>
    public const string EquivalenceCount = "EquivalenceCount";

    /// <summary>The number of normative references recorded.</summary>
    public const string NormativeReferenceCount = "NormativeReferenceCount";

    /// <summary>TempestOS's own record validation state — a different question from <see cref="PublicationStatus"/>.</summary>
    public const string RecordValidationState = "RecordValidationState";

    /// <summary>Every property key, in the order a comparison lays its rows out.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Body, Designation, Edition, Title, Classification, Disciplines,
        PublicationStatus, PublicationDate, EffectiveDate, WithdrawalDate,
        StatesConformityRequirements, EquivalenceCount, NormativeReferenceCount, RecordValidationState,
    ];
}

/// <summary>Builds a structured, side-by-side comparison of standard records.</summary>
/// <remarks>
/// Pure and synchronous, and states no verdict: it says what each record
/// holds, never which standard applies, which is more authoritative, or
/// which an engineer should work to. Publisher status and record
/// validation state appear as two separate rows precisely so a reader
/// cannot mistake one for the other.
/// </remarks>
public static class StandardComparer
{
    /// <summary>Compares <paramref name="standards"/> across every property in <see cref="StandardComparisonProperties.All"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="standards"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="standards"/> is empty, or contains a <see langword="null"/>.</exception>
    public static ReferenceComparisonResult Compare(IReadOnlyList<IReferenceRecord<StandardDefinition>> standards) =>
        ReferenceComparer.Compare(
            standards,
            StandardComparisonProperties.All,
            CellFor,
            standard => standard.Definition.Classification.ToString());

    private static ReferenceComparisonCell CellFor(IReferenceRecord<StandardDefinition> standard, string property)
    {
        var definition = standard.Definition;
        var classification = definition.Classification;
        var applicabilityKnown = StandardClassificationTraits.IsApplicabilityKnown(classification);

        return property switch
        {
            StandardComparisonProperties.Body => ReferenceComparisonCell.Text(definition.Body.Code),
            StandardComparisonProperties.Designation => ReferenceComparisonCell.Text(definition.Designation),
            StandardComparisonProperties.Edition => ReferenceComparisonCell.Text(definition.Edition),
            StandardComparisonProperties.Title => ReferenceComparisonCell.Text(definition.Title),
            StandardComparisonProperties.Classification => classification == StandardClassification.Unspecified
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(classification.ToString()),
            StandardComparisonProperties.Disciplines => definition.Disciplines.Count == 0
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(string.Join(", ", definition.Disciplines)),
            StandardComparisonProperties.PublicationStatus => StandardPublicationStatuses.IsKnown(definition.PublicationStatus)
                ? ReferenceComparisonCell.Text(definition.PublicationStatus.ToString())
                : ReferenceComparisonCell.NotRecorded,
            StandardComparisonProperties.PublicationDate => Date(definition.PublicationDate),
            StandardComparisonProperties.EffectiveDate => Date(definition.EffectiveDate),

            // A withdrawal date is not a gap on a standard the publisher
            // still holds current — there is nothing to record.
            StandardComparisonProperties.WithdrawalDate =>
                StandardPublicationStatuses.IsCurrent(definition.PublicationStatus)
                    ? ReferenceComparisonCell.NotApplicable
                    : Date(definition.WithdrawalDate),

            StandardComparisonProperties.StatesConformityRequirements => applicabilityKnown
                ? ReferenceComparisonCell.Text(StandardClassificationTraits.StatesConformityRequirements(classification) ? "Yes" : "No")
                : ReferenceComparisonCell.NotRecorded,

            StandardComparisonProperties.EquivalenceCount => Count(definition.Equivalences.Count),
            StandardComparisonProperties.NormativeReferenceCount => Count(definition.NormativeReferences.Count),
            StandardComparisonProperties.RecordValidationState => ReferenceComparisonCell.Text(standard.ValidationState.ToString()),
            _ => ReferenceComparisonCell.NotRecorded,
        };
    }

    private static ReferenceComparisonCell Date(DateOnly? date) =>
        date is null
            ? ReferenceComparisonCell.NotRecorded
            : new ReferenceComparisonCell(
                ReferencePropertyAvailability.Recorded,
                date.Value.ToString("O"),
                date.Value.DayNumber);

    /// <summary>
    /// A count is always recorded, including when it is zero: "this record
    /// lists no equivalences" is a fact the record states, unlike a value
    /// nobody supplied.
    /// </summary>
    private static ReferenceComparisonCell Count(int count) =>
        new(ReferencePropertyAvailability.Recorded, count.ToString(), count);
}
