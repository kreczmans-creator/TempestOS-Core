using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>The concrete <see cref="IStandardValidationService"/> implementation.</summary>
/// <remarks>
/// Provenance, verification attributability, supersession and cited-standard
/// resolution are checked by
/// <see cref="ReferenceValidationService{TDefinition}"/>, shared with every
/// Group A library. Everything below is standards register-keeping: the
/// bibliographic identity a citable record needs, the date orderings that
/// cannot hold, the self-references that describe nothing, and the
/// copyright guard on <see cref="StandardDefinition.ScopeSummary"/>.
/// </remarks>
public sealed class StandardValidationService : ReferenceValidationService<StandardDefinition>, IStandardValidationService
{
    /// <summary>
    /// The length above which a scope summary is reported as likely
    /// reproduced text.
    /// </summary>
    /// <remarks>
    /// <b>A heuristic, and reported as a warning for exactly that reason.</b>
    /// No length test can tell a long summary from a short quotation, so
    /// this never claims the text was copied — it asks a person to look.
    /// The threshold is set where a genuine one- or two-sentence summary
    /// comfortably fits and a reproduced scope clause generally does not.
    /// </remarks>
    public const int ScopeSummaryLengthWarningThreshold = 600;

    /// <summary>
    /// Initialises a new instance of the <see cref="StandardValidationService"/> class.
    /// </summary>
    /// <param name="catalog">The register whose records this service validates.</param>
    /// <remarks>
    /// Takes no <see cref="IStandardResolver"/>: A2 resolves its own
    /// normative references against its own catalogue, so passing itself
    /// through the shared optional-resolver seam would be an indirection
    /// with no purpose.
    /// </remarks>
    public StandardValidationService(IStandardCatalog catalog)
        : base(catalog, materialCatalog: null, standardResolver: catalog)
    {
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        StandardDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateIdentity(definition, errors, warnings);
        EvaluatePublicationStatus(definition, errors, warnings);
        EvaluateDates(definition, errors);
        EvaluateRelationships(definition, errors, warnings);

        await EvaluateStandardReferencesAsync(definition.NormativeReferences, warnings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<StandardDefinition> record,
        IReadOnlyList<IReferenceRecord<StandardDefinition>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        // The two axes are independent, and this is the one place they
        // legitimately inform each other: a record replaced here almost
        // always means the publisher issued something newer, so a
        // superseded record still describing its standard as current is
        // worth a person's attention. It is a warning, never an error —
        // the record could have been superseded by a better-sourced record
        // of the same, still-current edition.
        if (record.ValidationState == ReferenceValidationState.Superseded
            && StandardPublicationStatuses.IsCurrent(record.Definition.PublicationStatus))
            warnings.Add(Diagnostic(
                StandardValidationRules.SupersededRecordStillMarkedCurrent,
                $"Record '{record.Id}' has been superseded, but still records the publisher as holding "
                + $"{record.Definition.FullDesignation} {record.Definition.PublicationStatus}. Confirm the publisher's own status."));

        var key = record.Definition.DesignationKey;

        // Defence in depth: the catalogue already prevents this at write
        // time. Confirming it on read catches an index written before that
        // guard existed, or corrupted since.
        var others = library ?? await Catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var collisions = others
            .Where(other => !string.Equals(other.Id, record.Id, StringComparison.Ordinal))
            .Where(other => string.Equals(other.Definition.DesignationKey, key, StringComparison.Ordinal))
            .Select(other => other.Id)
            .ToList();

        if (collisions.Count > 0)
            errors.Add(Diagnostic(
                StandardValidationRules.DuplicateDesignation,
                $"Standard '{record.Definition.FullDesignation}' is also registered as: {string.Join(", ", collisions)}."));
    }

    private static void EvaluateIdentity(StandardDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (string.IsNullOrWhiteSpace(definition.Title))
            warnings.Add(Diagnostic(
                StandardValidationRules.TitleShouldBeRecorded,
                $"Standard '{definition.FullDesignation}' records no title, so it cannot be cited bibliographically."));

        if (definition.Edition is null)
            warnings.Add(Diagnostic(
                StandardValidationRules.EditionShouldBeRecorded,
                $"Standard '{definition.Designation}' records no edition. A standard cited without an edition cannot be tied to the requirements a design was checked against."));

        if (definition.Classification == StandardClassification.Unspecified)
            warnings.Add(Diagnostic(
                StandardValidationRules.ClassificationShouldBeStated,
                $"Standard '{definition.FullDesignation}' records no classification, so which of its fields are meaningful cannot be determined."));

        var needsSourceWording = definition.Classification == StandardClassification.Other
            || definition.Body.Kind == StandardsBodyKind.Other;

        if (needsSourceWording && string.IsNullOrWhiteSpace(definition.SourceClassification))
            errors.Add(Diagnostic(
                StandardValidationRules.OtherClassificationNeedsSourceClassification,
                $"Standard '{definition.FullDesignation}' is classified 'Other' but records none of the source's own classification wording in SourceClassification."));

        if (definition.Disciplines.Count == 0)
            warnings.Add(Diagnostic(
                StandardValidationRules.DisciplineShouldBeRecorded,
                $"Standard '{definition.FullDesignation}' records no discipline, so it cannot be found by subject."));

        if (definition.ScopeSummary is { Length: > ScopeSummaryLengthWarningThreshold } summary)
            warnings.Add(Diagnostic(
                StandardValidationRules.ScopeSummaryMayBeReproducedText,
                $"The scope summary for '{definition.FullDesignation}' is {summary.Length} characters. "
                + $"A2 records a summary in the recorder's own words, never the standard's own scope clause; "
                + $"anything above {ScopeSummaryLengthWarningThreshold} characters needs a person to confirm it is not reproduced text."));
    }

    private static void EvaluatePublicationStatus(StandardDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (!StandardPublicationStatuses.IsKnown(definition.PublicationStatus))
        {
            warnings.Add(Diagnostic(
                StandardValidationRules.PublicationStatusShouldBeStated,
                $"The publisher's own status for '{definition.FullDesignation}' is not recorded. "
                + "This is not the same question as the record's own validation state, and neither answers the other."));
            return;
        }

        if (StandardPublicationStatuses.ExpectsWithdrawalDate(definition.PublicationStatus) && definition.WithdrawalDate is null)
            warnings.Add(Diagnostic(
                StandardValidationRules.WithdrawalDateShouldBeRecorded,
                $"Standard '{definition.FullDesignation}' is recorded as {definition.PublicationStatus} but records no withdrawal date."));

        if (StandardPublicationStatuses.IsCurrent(definition.PublicationStatus) && definition.WithdrawalDate is { } withdrawn)
            errors.Add(Diagnostic(
                StandardValidationRules.CurrentStandardHasWithdrawalDate,
                $"Standard '{definition.FullDesignation}' is recorded as {definition.PublicationStatus} but also as withdrawn on {withdrawn:O}. Both cannot be true."));
    }

    private static void EvaluateDates(StandardDefinition definition, List<IValidationDiagnostic> errors)
    {
        if (definition.PublicationDate is { } published)
        {
            if (definition.EffectiveDate is { } effective && effective < published)
                errors.Add(Diagnostic(
                    StandardValidationRules.DatesOutOfOrder,
                    $"'{definition.FullDesignation}' takes effect on {effective:O}, before it was published on {published:O}."));

            if (definition.WithdrawalDate is { } withdrawn && withdrawn < published)
                errors.Add(Diagnostic(
                    StandardValidationRules.DatesOutOfOrder,
                    $"'{definition.FullDesignation}' was withdrawn on {withdrawn:O}, before it was published on {published:O}."));

            if (definition.ConfirmationDate is { } confirmed && confirmed < published)
                errors.Add(Diagnostic(
                    StandardValidationRules.DatesOutOfOrder,
                    $"'{definition.FullDesignation}' was confirmed on {confirmed:O}, before it was published on {published:O}."));
        }

        if (definition.WithdrawalDate is { } end && definition.EffectiveDate is { } start && end < start)
            errors.Add(Diagnostic(
                StandardValidationRules.DatesOutOfOrder,
                $"'{definition.FullDesignation}' was withdrawn on {end:O}, before it took effect on {start:O}."));
    }

    private static void EvaluateRelationships(StandardDefinition definition, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        var self = definition.DesignationKey;

        foreach (var equivalence in definition.Equivalences)
        {
            if (SameStandard(self, equivalence.Body ?? definition.Body.Code, equivalence.Designation))
                errors.Add(Diagnostic(
                    StandardValidationRules.SelfReference,
                    $"'{definition.FullDesignation}' records itself as an equivalent standard, which describes nothing."));

            if (equivalence.IsDerived)
                warnings.Add(Diagnostic(
                    ReferenceValidationRules.DerivedValuePresent,
                    $"The equivalence to '{equivalence.Designation}' was derived by TempestOS. "
                    + "Standards equivalence is a judgement belonging to the publishing bodies and must not be presented as source reference data."));
            else if (equivalence.Origin == ReferenceValueOrigin.Unknown)
                warnings.Add(Diagnostic(
                    StandardValidationRules.EquivalenceOriginShouldBeRecorded,
                    $"The equivalence to '{equivalence.Designation}' records no origin, so who claimed it is unknown."));
        }

        foreach (var reference in definition.NormativeReferences)
        {
            if (SameStandard(self, reference.Body ?? definition.Body.Code, reference.Designation))
                errors.Add(Diagnostic(
                    StandardValidationRules.SelfReference,
                    $"'{definition.FullDesignation}' records itself as one of its own normative references."));
        }

        foreach (var replaced in definition.ReplacesDesignations)
        {
            if (SameStandard(self, definition.Body.Code, replaced))
                errors.Add(Diagnostic(
                    StandardValidationRules.SelfReference,
                    $"'{definition.FullDesignation}' records itself as a designation it replaces."));
        }
    }

    /// <summary>
    /// Whether a cited designation names the record doing the citing.
    /// Compares against the undated key as well as this record's own, so a
    /// citation that omits the edition is still caught.
    /// </summary>
    private static bool SameStandard(string selfKey, string bodyCode, string designation)
    {
        if (string.IsNullOrWhiteSpace(bodyCode) || string.IsNullOrWhiteSpace(designation))
            return false;

        var cited = StandardDefinition.DesignationKeyFor(bodyCode, designation);
        var selfUndated = selfKey[..(selfKey.LastIndexOf(':') + 1)];

        return string.Equals(cited, selfKey, StringComparison.Ordinal)
            || string.Equals(cited, selfUndated, StringComparison.Ordinal);
    }
}
