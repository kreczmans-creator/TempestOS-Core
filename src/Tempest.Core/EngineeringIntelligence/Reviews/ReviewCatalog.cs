using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Reviews;

/// <summary>A deterministic filter over the review-definition library.</summary>
public sealed record ReviewDefinitionQuery
{
    /// <summary>Matches any review whose code contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CodeContains { get; init; }

    /// <summary>Matches any review whose name or purpose contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches reviews written for this subject kind, and reviews not tied to one. <see langword="null"/> to match any.</summary>
    public string? SubjectKind { get; init; }

    /// <summary>Matches any review checking at least one criterion in these areas. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReviewArea> Areas { get; init; } = [];

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of governed engineering review definitions.</summary>
public interface IReviewDefinitionCatalog : IReferenceDataCatalog<ReviewDefinition>
{
    /// <summary>Returns the review registered under <paramref name="code"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<ReviewDefinition>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Every registered review matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<ReviewDefinition>>> SearchAsync(
        ReviewDefinitionQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IReviewDefinitionCatalog"/> implementation.</summary>
public sealed class ReviewDefinitionCatalog : ReferenceDataCatalog<ReviewDefinition>, IReviewDefinitionCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every review-definition record's own backing document carries.</summary>
    /// <remarks>
    /// Deliberately not <c>Review</c>: that name belongs to the platform's
    /// own <see cref="IReview"/> lifecycle gate, which records who reviewed
    /// an object. This is the definition of what an engineering review
    /// checks. One value, one meaning.
    /// </remarks>
    public const string ReviewDefinitionDocumentKind = "EngineeringReviewDefinition";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>reviewId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "EngineeringReviews.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each review code to the <c>reviewId</c> holding it.</summary>
    public const string CodeIndexCollection = "EngineeringReviews.CodeIndex";

    /// <summary>Initialises a new instance of the <see cref="ReviewDefinitionCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own review records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public ReviewDefinitionCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "EngineeringReviews";

    /// <inheritdoc />
    public override string DocumentKind => ReviewDefinitionDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => CodeIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<ReviewDefinition>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(ReviewDefinition.CodeKeyFor(code), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<ReviewDefinition>>> SearchAsync(
        ReviewDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(ReviewDefinition definition) => definition.CodeKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(ReviewDefinition definition) => $"Review code '{definition.Code}'";

    private static bool Matches(IReferenceRecord<ReviewDefinition> record, ReviewDefinitionQuery query)
    {
        var review = record.Definition;

        if (query.CodeContains is not null && !review.Code.Contains(query.CodeContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.TextContains is { } text
            && !review.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !review.Purpose.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.SubjectKind is { } kind
            && review.SubjectKind is not null
            && !string.Equals(review.SubjectKind, kind, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Areas.Count > 0 && !review.Criteria.Any(c => query.Areas.Contains(c.Area)))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes <see cref="IReviewDefinitionValidationService"/> reports.</summary>
public static class ReviewValidationRules
{
    /// <summary>The review checks nothing.</summary>
    public const string ReviewMustHaveCriteria = "TEMPEST-EIV-001";

    /// <summary>Two criteria in one review share a code, so a finding cannot be tied to one of them.</summary>
    public const string DuplicateCriterionCode = "TEMPEST-EIV-002";

    /// <summary>A criterion names a `P02` rule the rule library does not hold.</summary>
    public const string RuleCodeMustResolve = "TEMPEST-EIV-003";

    /// <summary>A criterion does not say what area of the design it is about.</summary>
    public const string CriterionAreaShouldBeStated = "TEMPEST-EIV-004";

    /// <summary>A manual criterion does not say what evidence would settle it, so a reviewer cannot tell when it is answered.</summary>
    public const string ManualCriterionShouldStateEvidence = "TEMPEST-EIV-005";

    /// <summary>The review records no rationale.</summary>
    public const string RationaleShouldBeRecorded = "TEMPEST-EIV-006";

    /// <summary>Two reviews share one review code.</summary>
    public const string DuplicateReviewCode = "TEMPEST-EIV-007";

    /// <summary>The review names a subject kind no reference library produces.</summary>
    public const string UnknownSubjectKind = "TEMPEST-EIV-008";

    /// <summary>A criterion checking a safety-critical characteristic is not binding, so failing it would raise no defect.</summary>
    public const string SafetyCriterionShouldBeBinding = "TEMPEST-EIV-009";
}

/// <summary>Governance of review definitions themselves.</summary>
public interface IReviewDefinitionValidationService : IReferenceValidationService<ReviewDefinition>
{
}

/// <summary>The concrete <see cref="IReviewDefinitionValidationService"/> implementation.</summary>
public sealed class ReviewDefinitionValidationService
    : ReferenceValidationService<ReviewDefinition>, IReviewDefinitionValidationService
{
    private readonly IRuleCatalog? _rules;

    /// <summary>Initialises a new instance of the <see cref="ReviewDefinitionValidationService"/> class.</summary>
    /// <param name="catalog">The review library whose records this service validates.</param>
    /// <param name="rules">The rule library, for confirming that a criterion's named rule exists. Optional: a review must be authorable before the rule it names has been written.</param>
    /// <param name="standardResolver">Resolves a cited standard against `A2`. Optional.</param>
    public ReviewDefinitionValidationService(
        IReviewDefinitionCatalog catalog,
        IRuleCatalog? rules = null,
        IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog: null, standardResolver)
    {
        _rules = rules;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        ReviewDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (definition.Criteria.Count == 0)
            errors.Add(Diagnostic(
                ReviewValidationRules.ReviewMustHaveCriteria,
                $"Review '{definition.Code}' checks nothing, so carrying it out would establish nothing."));

        var duplicates = definition.Criteria
            .GroupBy(c => c.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var duplicate in duplicates)
            errors.Add(Diagnostic(
                ReviewValidationRules.DuplicateCriterionCode,
                $"Review '{definition.Code}' declares criterion '{duplicate}' more than once, so a finding cannot be tied to one of them."));

        foreach (var criterion in definition.Criteria)
        {
            if (criterion.Area == ReviewArea.Unspecified)
                warnings.Add(Diagnostic(
                    ReviewValidationRules.CriterionAreaShouldBeStated,
                    $"Criterion '{criterion.Code}' in review '{definition.Code}' does not say what area of the design it is about."));

            if (!criterion.IsAutomated && string.IsNullOrWhiteSpace(criterion.EvidenceExpected))
                warnings.Add(Diagnostic(
                    ReviewValidationRules.ManualCriterionShouldStateEvidence,
                    $"Criterion '{criterion.Code}' in review '{definition.Code}' must be answered by a person but does not say "
                    + "what evidence would settle it, so a reviewer cannot tell when it is answered."));

            if (criterion.Area == ReviewArea.SafetyCritical && !RuleSeverities.IsBinding(criterion.Severity))
                warnings.Add(Diagnostic(
                    ReviewValidationRules.SafetyCriterionShouldBeBinding,
                    $"Criterion '{criterion.Code}' in review '{definition.Code}' checks a safety-critical characteristic but is "
                    + $"{criterion.Severity}, so failing it would raise no defect."));

            if (criterion.RuleCode is { } ruleCode && _rules is not null
                && await _rules.FindByCodeAsync(ruleCode, cancellationToken).ConfigureAwait(false) is null)
                warnings.Add(Diagnostic(
                    ReviewValidationRules.RuleCodeMustResolve,
                    $"Criterion '{criterion.Code}' in review '{definition.Code}' names rule '{ruleCode}', which the rule library "
                    + "does not hold. Carrying out the review will report that criterion as unevaluated."));
        }

        if (definition.SubjectKind is { } kind && !AssessmentSubjectKinds.All.Contains(kind, StringComparer.OrdinalIgnoreCase))
            warnings.Add(Diagnostic(
                ReviewValidationRules.UnknownSubjectKind,
                $"Review '{definition.Code}' is written for subject kind '{kind}', which no reference library produces."));

        if (string.IsNullOrWhiteSpace(definition.Rationale))
            warnings.Add(Diagnostic(
                ReviewValidationRules.RationaleShouldBeRecorded,
                $"Review '{definition.Code}' records no rationale."));

        await EvaluateStandardReferencesAsync(definition.Standards, warnings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<ReviewDefinition> record,
        IReadOnlyList<IReferenceRecord<ReviewDefinition>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var key = record.Definition.CodeKey;
        var others = library ?? await Catalog.ListAsync(cancellationToken).ConfigureAwait(false);

        var collisions = others
            .Where(other => !string.Equals(other.Id, record.Id, StringComparison.Ordinal))
            .Where(other => string.Equals(other.Definition.CodeKey, key, StringComparison.Ordinal))
            .Select(other => other.Id)
            .ToList();

        if (collisions.Count > 0)
            errors.Add(Diagnostic(
                ReviewValidationRules.DuplicateReviewCode,
                $"Review code '{record.Definition.Code}' is also registered as: {string.Join(", ", collisions)}."));
    }
}
