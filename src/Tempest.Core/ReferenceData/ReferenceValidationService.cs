using Tempest.Core.EngineeringDomain;
using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// The validation machinery every Group A library shares: the rules about
/// being reference data at all, plus the plumbing that lets a library add
/// its own domain rules without restating any of it.
/// </summary>
/// <remarks>
/// <para>
/// A read-only service over a catalogue, mirroring
/// <see cref="Requirements.RequirementValidationService"/>'s own shape: it
/// stores nothing, changes nothing, and never repairs what it finds.
/// </para>
/// <para>
/// <see cref="IMaterialCatalog"/> and the Standards resolver are
/// <em>optional</em> collaborators. With them, a record's own material and
/// standard references are confirmed to resolve; without them, those rules
/// are simply not evaluated. Optional rather than required deliberately —
/// a fastener must be recordable and checkable before the material it
/// names has been registered, and P01 must not make any library a hard
/// prerequisite for holding data in another.
/// </para>
/// </remarks>
/// <typeparam name="TDefinition">The domain's own engineering description type.</typeparam>
public abstract class ReferenceValidationService<TDefinition> : IReferenceValidationService<TDefinition>
    where TDefinition : class
{
    private readonly IReferenceDataCatalog<TDefinition> _catalog;

    /// <summary>
    /// Initialises a new instance of the <see cref="ReferenceValidationService{TDefinition}"/> class.
    /// </summary>
    /// <param name="catalog">The catalogue whose records this service validates.</param>
    /// <param name="materialCatalog">The canonical Materials catalogue, for confirming material references resolve. Optional.</param>
    /// <param name="standardResolver">Resolves a <c>standardId</c> to a registered Standards Library record. Optional.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    protected ReferenceValidationService(
        IReferenceDataCatalog<TDefinition> catalog,
        IMaterialCatalog? materialCatalog = null,
        IStandardResolver? standardResolver = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
        MaterialCatalog = materialCatalog;
        StandardResolver = standardResolver;
    }

    /// <summary>The canonical Materials catalogue, or <see langword="null"/> where none was supplied.</summary>
    protected IMaterialCatalog? MaterialCatalog { get; }

    /// <summary>The Standards Library resolver, or <see langword="null"/> where none was supplied.</summary>
    protected IStandardResolver? StandardResolver { get; }

    /// <summary>The library this service validates.</summary>
    protected IReferenceDataCatalog<TDefinition> Catalog => _catalog;

    /// <inheritdoc />
    public async Task<IValidationResult> ValidateAsync(string recordId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var record = await _catalog.FindAsync(recordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(_catalog.LibraryName, recordId);

        return await ValidateAsync(record, library: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IValidationResult> ValidateDefinitionAsync(
        TDefinition definition,
        ReferenceProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(provenance);

        var errors = new List<IValidationDiagnostic>();
        var warnings = new List<IValidationDiagnostic>();

        EvaluateProvenance(provenance, errors, warnings);
        await EvaluateDefinitionAsync(definition, errors, warnings, cancellationToken).ConfigureAwait(false);

        return new ValidationResult(errors, warnings);
    }

    /// <inheritdoc />
    public async Task<ReferenceDataQualityReport> ValidateLibraryAsync(CancellationToken cancellationToken = default)
    {
        // Read the whole library once and pass it down, so validating N
        // records costs one enumeration rather than N.
        var records = await _catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<ReferenceDataQualityFinding>();

        foreach (var record in records)
        {
            var result = await ValidateAsync(record, records, cancellationToken).ConfigureAwait(false);
            if (result.Errors.Count > 0 || result.Warnings.Count > 0)
                findings.Add(new ReferenceDataQualityFinding(record.Id, record.ValidationState, result));
        }

        return new ReferenceDataQualityReport(_catalog.LibraryName, findings, records.Count);
    }

    /// <summary>
    /// The domain's own rules, reading only the record's own engineering
    /// content. Every library overrides this; nothing else is required of
    /// it.
    /// </summary>
    /// <param name="definition">The definition to evaluate.</param>
    /// <param name="errors">Errors found, appended to.</param>
    /// <param name="warnings">Warnings found, appended to.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    protected abstract Task EvaluateDefinitionAsync(
        TDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken);

    /// <summary>
    /// The domain's own rules that need the registered record rather than
    /// just its content. Overriding is optional.
    /// </summary>
    /// <param name="record">The record to evaluate.</param>
    /// <param name="library">Every record in the library, already loaded, where the caller had it; otherwise <see langword="null"/>.</param>
    /// <param name="errors">Errors found, appended to.</param>
    /// <param name="warnings">Warnings found, appended to.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    protected virtual Task EvaluateRecordAsync(
        IReferenceRecord<TDefinition> record,
        IReadOnlyList<IReferenceRecord<TDefinition>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Confirms each cited standard resolves, where a resolver was supplied. A citation that does not resolve is a warning, never an error — a standard nobody has registered yet is a gap in A2, not a defect in the citing record.</summary>
    protected async Task EvaluateStandardReferencesAsync(
        IEnumerable<StandardReference> standards,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (StandardResolver is null)
            return;

        foreach (var standard in standards)
        {
            if (!standard.IsResolved)
                continue;

            if (!await StandardResolver.ExistsAsync(standard.StandardId!, cancellationToken).ConfigureAwait(false))
                warnings.Add(Diagnostic(
                    ReferenceValidationRules.StandardReferenceUnresolved,
                    $"Standard '{standard.Designation}' cites registered standard '{standard.StandardId}', which is not in the Standards Library."));
        }
    }

    /// <summary>Confirms each referenced material resolves, where a Materials catalogue was supplied.</summary>
    protected async Task EvaluateMaterialReferencesAsync(
        IEnumerable<string> materialIds,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (MaterialCatalog is null)
            return;

        foreach (var materialId in materialIds.Distinct(StringComparer.Ordinal))
        {
            var material = await MaterialCatalog.FindAsync(materialId, cancellationToken).ConfigureAwait(false);
            if (material is null)
                warnings.Add(Diagnostic(
                    ReferenceValidationRules.MaterialReferenceUnresolved,
                    $"Material '{materialId}' is referenced but is not registered in the Materials catalogue."));
        }
    }

    /// <summary>Reports an inverted range — a maximum below its own minimum, which describes no real range.</summary>
    protected static void EvaluateRange<TDimension>(
        ReferenceRange<TDimension>? range,
        string label,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
        where TDimension : UnitsAndQuantities.IDimension
    {
        if (range is null)
            return;

        if (range.IsInverted)
            errors.Add(Diagnostic(
                ReferenceValidationRules.RangeInverted,
                $"{label} has a maximum ({range.Maximum}) below its own minimum ({range.Minimum})."));

        if (range.Origin == ReferenceValueOrigin.DerivedByTempestOS)
            warnings.Add(Diagnostic(
                ReferenceValidationRules.DerivedValuePresent,
                $"{label} is derived by TempestOS and must not be presented as source reference data."));
    }

    /// <summary>Reports a non-positive value where the domain requires a positive one, and flags a derived value.</summary>
    protected static void EvaluatePositiveValue<TDimension>(
        ReferenceValue<TDimension>? value,
        string label,
        string nonPositiveRuleCode,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
        where TDimension : UnitsAndQuantities.IDimension
    {
        if (value is null)
            return;

        if (value.CanonicalValue <= 0)
            errors.Add(Diagnostic(
                nonPositiveRuleCode,
                $"{label} is {value.Value}; a recorded value here must be greater than zero. Omit it instead if the source gave none."));

        if (value.IsDerived)
            warnings.Add(Diagnostic(
                ReferenceValidationRules.DerivedValuePresent,
                $"{label} is derived by TempestOS and must not be presented as source reference data."));
    }

    /// <summary>Builds a diagnostic.</summary>
    protected static IValidationDiagnostic Diagnostic(string code, string message) => new ValidationDiagnostic(code, message);

    private async Task<IValidationResult> ValidateAsync(
        IReferenceRecord<TDefinition> record,
        IReadOnlyList<IReferenceRecord<TDefinition>>? library,
        CancellationToken cancellationToken)
    {
        var errors = new List<IValidationDiagnostic>();
        var warnings = new List<IValidationDiagnostic>();

        EvaluateProvenance(record.Provenance, errors, warnings);
        EvaluateSupersession(record, warnings);
        await EvaluateDefinitionAsync(record.Definition, errors, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateRecordAsync(record, library, errors, warnings, cancellationToken).ConfigureAwait(false);

        return new ValidationResult(errors, warnings);
    }

    private static void EvaluateProvenance(ReferenceProvenance provenance, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (!provenance.IdentifiesASource)
            warnings.Add(Diagnostic(
                ReferenceValidationRules.ProvenanceMustIdentifyASource,
                "Provenance names neither a source organisation nor a source document; this record cannot leave Draft until it does."));

        if (provenance.VerificationStatus == ReferenceVerificationStatus.VerifiedAgainstSource && !provenance.IsVerified)
            errors.Add(Diagnostic(
                ReferenceValidationRules.VerificationMustBeAttributable,
                "The record is marked verified against its source but names no reviewer, no verification date, or neither."));
    }

    private static void EvaluateSupersession(IReferenceRecord<TDefinition> record, List<IValidationDiagnostic> warnings)
    {
        if (record.ValidationState == ReferenceValidationState.Superseded && record.SupersededByRecordId is null)
            warnings.Add(Diagnostic(
                ReferenceValidationRules.SupersededWithoutReplacement,
                $"Record '{record.Id}' is superseded but names no replacement."));
    }
}
