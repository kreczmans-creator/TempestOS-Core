using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.Templates;

/// <summary>A deterministic filter over the template library.</summary>
public sealed record TemplateQuery
{
    /// <summary>Matches any template whose reference, name or purpose contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<TemplateKind> Kinds { get; init; } = [];

    /// <summary>Matches templates applying to this enquiry. <see langword="null"/> to leave every dimension open.</summary>
    public AssetEnquiry? Enquiry { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The engineering template library.</summary>
public interface ITemplateCatalog : IReferenceDataCatalog<EngineeringTemplate>
{
    /// <summary>Returns the template registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<EngineeringTemplate>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered template matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<EngineeringTemplate>>> SearchAsync(TemplateQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every released template that applies to <paramref name="enquiry"/>,
    /// most specific first.
    /// </summary>
    /// <remarks>
    /// Ordered so a template written for this discipline outranks one
    /// that restricts nothing. Released only: a draft template has not
    /// been checked by anybody and must not silently shape engineering
    /// work.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<EngineeringTemplate>>> FindApplicableAsync(
        AssetEnquiry enquiry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins the template as it stands now, for a caller about to work
    /// from it.
    /// </summary>
    /// <remarks>
    /// The seam that makes `E1`'s central promise keepable. A caller that
    /// pins through this method cannot accidentally record a revision it
    /// did not read, because the revision comes from the record rather
    /// than from the caller.
    /// </remarks>
    /// <param name="reference">The template to pin.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pin, or <see langword="null"/> where no such template is registered.</returns>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<ReferencePin?> PinAsync(string reference, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ITemplateCatalog"/> implementation.</summary>
public sealed class TemplateCatalog : ReferenceDataCatalog<EngineeringTemplate>, ITemplateCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every template's own backing document carries.</summary>
    public const string TemplateDocumentKind = "EngineeringTemplate";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string TemplateLibraryName = "EngineeringTemplates";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>templateId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "EngineeringTemplates.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each template reference to the <c>templateId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "EngineeringTemplates.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="TemplateCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own templates are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public TemplateCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => TemplateLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => TemplateDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<EngineeringTemplate>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(EngineeringTemplate.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<EngineeringTemplate>>> SearchAsync(
        TemplateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<EngineeringTemplate>>> FindApplicableAsync(
        AssetEnquiry enquiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        var applicable = await FilterAsync(
            record => record.ValidationState == ReferenceValidationState.Released
                      && record.Definition.Applicability.AppliesTo(enquiry),
            cancellationToken).ConfigureAwait(false);

        return applicable
            .OrderByDescending(r => Specificity(r.Definition.Applicability))
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ReferencePin?> PinAsync(string reference, CancellationToken cancellationToken = default)
    {
        var record = await FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false);

        return record is null ? null : ReferencePin.For(LibraryName, record);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(EngineeringTemplate definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(EngineeringTemplate definition) => $"Template reference '{definition.Reference}'";

    private static int Specificity(AssetApplicability applicability) =>
        applicability.Disciplines.Count
        + applicability.ProjectIdentifiers.Count
        + applicability.SubjectKinds.Count
        + (applicability.Validity is null ? 0 : 1);

    private static bool Matches(IReferenceRecord<EngineeringTemplate> record, TemplateQuery query)
    {
        var template = record.Definition;

        if (query.TextContains is { } text
            && !template.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !template.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !template.Purpose.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Kinds.Count > 0 && !query.Kinds.Contains(template.Kind))
            return false;

        if (query.Enquiry is { } enquiry && !template.Applicability.AppliesTo(enquiry))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes E1's validation service reports.</summary>
public static class TemplateValidationRules
{
    /// <summary>The template has no sections, so it structures nothing.</summary>
    public const string TemplateHasNoStructure = "TEMPEST-EAT-001";

    /// <summary>Two sections or fields share one reference.</summary>
    public const string DuplicateTemplateReference = "TEMPEST-EAT-002";

    /// <summary>A section asks for nothing and contains nothing.</summary>
    public const string SectionIsEmpty = "TEMPEST-EAT-003";

    /// <summary>A field offers a closed set of answers but names none of them.</summary>
    public const string ChoiceFieldHasNoChoices = "TEMPEST-EAT-004";

    /// <summary>A quantity field does not say what dimension it expects.</summary>
    public const string QuantityFieldHasNoDimension = "TEMPEST-EAT-005";

    /// <summary>A mandatory section contains no required field, so nothing about it is actually mandatory.</summary>
    public const string MandatorySectionRequiresNothing = "TEMPEST-EAT-006";

    /// <summary>The template does not say what kind of work it structures.</summary>
    public const string TemplateKindNotStated = "TEMPEST-EAT-007";

    /// <summary>The template names a predecessor the library does not hold.</summary>
    public const string SupersededTemplateMustResolve = "TEMPEST-EAT-008";

    /// <summary>The template has run past its own effective period.</summary>
    public const string TemplateHasExpired = "TEMPEST-EAT-009";

    /// <summary>The template nests sections deeply enough that nobody will fill it in.</summary>
    public const string TemplateIsDeeplyNested = "TEMPEST-EAT-010";
}

/// <summary>Governance of the template library itself.</summary>
public interface ITemplateValidationService : IReferenceValidationService<EngineeringTemplate>
{
}

/// <summary>The concrete <see cref="ITemplateValidationService"/> implementation.</summary>
/// <remarks>
/// The findings are about whether a template will actually structure
/// anything. A template with a mandatory section that requires nothing,
/// or a choice field offering no choices, is well-formed and useless, and
/// neither is something the service repairs.
/// </remarks>
public sealed class TemplateValidationService : ReferenceValidationService<EngineeringTemplate>, ITemplateValidationService
{
    /// <summary>How deep a template may nest before it stops being fillable.</summary>
    public const int MaximumUsefulDepth = 4;

    private readonly ITemplateCatalog _templates;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="TemplateValidationService"/> class.</summary>
    /// <param name="catalog">The template library whose records this service validates.</param>
    /// <param name="timeProvider">The clock expiry checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public TemplateValidationService(ITemplateCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _templates = catalog;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        EngineeringTemplate definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Template '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (!definition.IsStructured)
            errors.Add(AssetGovernanceValidation.Diagnostic(
                TemplateValidationRules.TemplateHasNoStructure,
                $"{subject} asks for nothing. A template with no fields structures no work."));

        AssetGovernanceValidation.EvaluateDuplicateReferences(
            definition.AllSections.Select(s => s.Reference),
            $"{subject} has two sections sharing the reference",
            errors);

        AssetGovernanceValidation.EvaluateDuplicateReferences(
            definition.AllFields.Select(f => f.Reference),
            $"{subject} has two fields sharing the reference",
            errors);

        foreach (var section in definition.AllSections)
        {
            if (section.IsEmpty)
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    TemplateValidationRules.SectionIsEmpty,
                    $"{subject} section '{section.Reference}' asks for nothing and contains nothing."));

            if (section.IsMandatory && !section.Fields.Any(f => f.IsRequired) && section.Subsections.Count == 0)
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    TemplateValidationRules.MandatorySectionRequiresNothing,
                    $"{subject} marks section '{section.Reference}' mandatory but requires nothing in it, so nothing "
                    + "about it is actually mandatory."));
        }

        foreach (var field in definition.AllFields)
        {
            if (field.IsUnusableChoice)
                errors.Add(AssetGovernanceValidation.Diagnostic(
                    TemplateValidationRules.ChoiceFieldHasNoChoices,
                    $"{subject} field '{field.Reference}' offers a closed set of answers and names none of them."));

            if (field.Kind == TemplateFieldKind.Quantity && string.IsNullOrWhiteSpace(field.ExpectedDimension))
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    TemplateValidationRules.QuantityFieldHasNoDimension,
                    $"{subject} field '{field.Reference}' expects a quantity but does not say of what dimension, so a "
                    + "length could be entered where a pressure was meant."));
        }

        if (definition.Kind == TemplateKind.Unspecified)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TemplateValidationRules.TemplateKindNotStated,
                $"{subject} does not say what kind of work it structures."));

        var depth = definition.Sections.Select(Depth).DefaultIfEmpty(0).Max();

        if (depth > MaximumUsefulDepth)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TemplateValidationRules.TemplateIsDeeplyNested,
                $"{subject} nests {depth} levels deep. Beyond {MaximumUsefulDepth}, templates stop being filled in."));

        if (definition.Applicability.IsExpiredAt(today))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TemplateValidationRules.TemplateHasExpired,
                $"{subject} ran past its own effective period on {definition.Applicability.Validity!.To:O}."));

        AssetGovernanceValidation.Evaluate(definition.Governance, subject, errors, warnings);

        if (definition.SupersedesReference is { } predecessor)
        {
            var found = await _templates.FindByReferenceAsync(predecessor, cancellationToken).ConfigureAwait(false);

            if (found is null)
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    TemplateValidationRules.SupersededTemplateMustResolve,
                    $"{subject} replaces template '{predecessor}', which the library does not hold."));
        }
    }

    private static int Depth(TemplateSection section) =>
        1 + section.Subsections.Select(Depth).DefaultIfEmpty(0).Max();
}
