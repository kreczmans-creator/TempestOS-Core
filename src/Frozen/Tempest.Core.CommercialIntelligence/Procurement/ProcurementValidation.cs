using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Procurement;

/// <summary>The diagnostic codes D5's validation services report.</summary>
public static class ProcurementValidationRules
{
    /// <summary>The requirement states no criteria, so nothing decides anything.</summary>
    public const string RequirementHasNoCriteria = "TEMPEST-CIS-001";

    /// <summary>Two criteria share one code.</summary>
    public const string DuplicateCriterionCode = "TEMPEST-CIS-002";

    /// <summary>A criterion is marked as scoring but carries no weight, so it cannot affect the ranking.</summary>
    public const string WeightedCriterionHasNoWeight = "TEMPEST-CIS-003";

    /// <summary>The requirement states weights but no mandatory criterion, so nothing can eliminate anybody.</summary>
    public const string NoMandatoryCriteria = "TEMPEST-CIS-004";

    /// <summary>One criterion carries most of the weight, so the comparison turns on a single axis.</summary>
    public const string SingleCriterionDominates = "TEMPEST-CIS-005";

    /// <summary>The requirement names no comparison currency, so candidates quoting differently cannot be ranked on cost.</summary>
    public const string ComparisonCurrencyNotStated = "TEMPEST-CIS-006";

    /// <summary>Nobody is named as having raised the requirement.</summary>
    public const string RequirementIsUnattributed = "TEMPEST-CIS-007";

    /// <summary>The comparison names no candidates.</summary>
    public const string ComparisonHasNoCandidates = "TEMPEST-CIS-008";

    /// <summary>Two candidates share one code.</summary>
    public const string DuplicateCandidateCode = "TEMPEST-CIS-009";

    /// <summary>Two candidates name one supplier.</summary>
    public const string DuplicateCandidateSupplier = "TEMPEST-CIS-010";

    /// <summary>The comparison leaves only one candidate in contention, so there is no comparison.</summary>
    public const string SingleCandidateInContention = "TEMPEST-CIS-011";

    /// <summary>An assessment cites a criterion the requirement does not state.</summary>
    public const string AssessmentCriterionUnresolved = "TEMPEST-CIS-012";

    /// <summary>A candidate names a supplier the supplier database does not hold.</summary>
    public const string CandidateSupplierMustResolve = "TEMPEST-CIS-013";

    /// <summary>A candidate's price is stated in a currency the requirement is not comparing in.</summary>
    public const string CandidateCurrencyMismatch = "TEMPEST-CIS-014";

    /// <summary>Candidates' lead times are stated in units that cannot be compared with each other.</summary>
    public const string LeadTimesNotComparable = "TEMPEST-CIS-015";

    /// <summary>A candidate's assessment rests on nothing anybody can check.</summary>
    public const string AssessmentIsUnsupported = "TEMPEST-CIS-016";

    /// <summary>The comparison recommends a candidate it also excluded.</summary>
    public const string RecommendedCandidateIsExcluded = "TEMPEST-CIS-017";

    /// <summary>The comparison recommends a candidate it does not hold.</summary>
    public const string RecommendedCandidateUnresolved = "TEMPEST-CIS-018";

    /// <summary>The comparison recommends somebody on information with material gaps in it.</summary>
    public const string RecommendationRestsOnGaps = "TEMPEST-CIS-019";

    /// <summary>The comparison recommends somebody but says nothing about why.</summary>
    public const string RecommendationHasNoRationale = "TEMPEST-CIS-020";

    /// <summary>A decision is recorded but nobody is named as having taken it.</summary>
    /// <remarks>
    /// The heaviest finding in `D5`. A procurement decision the
    /// organisation cannot attribute to a person is exactly what
    /// `ADR-0135` exists to prevent, and it is an error rather than a
    /// warning.
    /// </remarks>
    public const string DecisionNeedsAuthority = "TEMPEST-CIS-021";

    /// <summary>A decision is recorded but names no candidate chosen.</summary>
    public const string DecisionNeedsChosenCandidate = "TEMPEST-CIS-022";

    /// <summary>A person went against the recommendation without saying why.</summary>
    public const string DepartureNeedsRationale = "TEMPEST-CIS-023";

    /// <summary>A decision was taken on a comparison the service rated insufficient.</summary>
    public const string DecisionOnInsufficientComparison = "TEMPEST-CIS-024";

    /// <summary>A candidate was excluded by a person's judgement without naming the person.</summary>
    public const string JudgementExclusionNeedsPrincipal = "TEMPEST-CIS-025";
}

/// <summary>Governance of the sourcing-requirement library itself.</summary>
public interface ISourcingRequirementValidationService : IReferenceValidationService<SourcingRequirement>
{
}

/// <summary>The concrete <see cref="ISourcingRequirementValidationService"/> implementation.</summary>
public sealed class SourcingRequirementValidationService
    : ReferenceValidationService<SourcingRequirement>, ISourcingRequirementValidationService
{
    /// <summary>The share of the total weight one criterion may carry before the comparison is really about that one thing.</summary>
    public const decimal DominantWeightShare = 0.6m;

    /// <summary>Initialises a new instance of the <see cref="SourcingRequirementValidationService"/> class.</summary>
    /// <param name="catalog">The requirement library whose records this service validates.</param>
    public SourcingRequirementValidationService(ISourcingRequirementCatalog catalog)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        SourcingRequirement definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Sourcing requirement '{definition.Reference}'";

        if (definition.Criteria.Count == 0)
            errors.Add(Diagnostic(
                ProcurementValidationRules.RequirementHasNoCriteria,
                $"{subject} states no criteria, so nothing decides anything."));

        foreach (var duplicate in definition.Criteria
                     .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key)
                     .OrderBy(c => c, StringComparer.Ordinal))
            errors.Add(Diagnostic(
                ProcurementValidationRules.DuplicateCriterionCode,
                $"{subject} states two criteria sharing the code '{duplicate}'."));

        foreach (var criterion in definition.Criteria
                     .Where(c => c.Role == SourcingCriterionRole.Weighted && c.Weight <= 0m))
            warnings.Add(Diagnostic(
                ProcurementValidationRules.WeightedCriterionHasNoWeight,
                $"{subject} criterion '{criterion.Code}' is marked as scoring but carries no weight, so it cannot affect the ranking."));

        if (definition.Criteria.Count > 0 && !definition.MandatoryCriteria.Any())
            warnings.Add(Diagnostic(
                ProcurementValidationRules.NoMandatoryCriteria,
                $"{subject} states no mandatory criterion, so a candidate that cannot do the work at all can still be ranked."));

        var total = definition.TotalWeight;

        if (total > 0m)
        {
            var dominant = definition.WeightedCriteria.FirstOrDefault(c => c.Weight / total >= DominantWeightShare);

            if (dominant is not null)
                warnings.Add(Diagnostic(
                    ProcurementValidationRules.SingleCriterionDominates,
                    $"{subject} gives criterion '{dominant.Code}' {dominant.Weight / total:P0} of the weight. The comparison "
                    + "will turn on that one thing, whatever the others say."));
        }

        if (definition.ComparisonCurrency is null && definition.Criteria.Any(c => c.Kind == SourcingCriterionKind.Cost))
            warnings.Add(Diagnostic(
                ProcurementValidationRules.ComparisonCurrencyNotStated,
                $"{subject} judges candidates on cost but names no comparison currency. Candidates quoting in different "
                + "currencies cannot be ranked, and TempestOS does not convert them."));

        if (string.IsNullOrWhiteSpace(definition.RaisedByPrincipalId))
            warnings.Add(Diagnostic(
                ProcurementValidationRules.RequirementIsUnattributed,
                $"{subject} names nobody who raised it."));

        return Task.CompletedTask;
    }
}

/// <summary>Governance of the sourcing-comparison library itself.</summary>
public interface ISourcingComparisonValidationService : IReferenceValidationService<SourcingComparison>
{
}

/// <summary>The concrete <see cref="ISourcingComparisonValidationService"/> implementation.</summary>
/// <remarks>
/// The findings divide into two kinds. Most are about whether a
/// comparison can be relied on: gaps in what was established, a
/// single-candidate "comparison", assessments nothing supports. The rest
/// are about authority, and those are errors — a recorded procurement
/// decision that names nobody is the one thing `D5` must never hold
/// quietly.
/// </remarks>
public sealed class SourcingComparisonValidationService
    : ReferenceValidationService<SourcingComparison>, ISourcingComparisonValidationService
{
    private readonly ISourcingRequirementCatalog? _requirements;
    private readonly ISupplierCatalog? _suppliers;

    /// <summary>Initialises a new instance of the <see cref="SourcingComparisonValidationService"/> class.</summary>
    /// <param name="catalog">The comparison library whose records this service validates.</param>
    /// <param name="requirements">The requirement library, for checking assessments against stated criteria. Optional.</param>
    /// <param name="suppliers">The supplier database, for confirming that named candidates exist. Optional.</param>
    public SourcingComparisonValidationService(
        ISourcingComparisonCatalog catalog,
        ISourcingRequirementCatalog? requirements = null,
        ISupplierCatalog? suppliers = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _requirements = requirements;
        _suppliers = suppliers;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        SourcingComparison definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Sourcing comparison '{definition.Reference}'";

        EvaluateCandidates(definition, subject, errors, warnings);
        EvaluateRecommendation(definition, subject, errors, warnings);
        EvaluateDecision(definition, subject, errors, warnings);

        await EvaluateAgainstRequirementAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateSuppliersAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private static void EvaluateCandidates(
        SourcingComparison definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (definition.Candidates.Count == 0)
        {
            errors.Add(Diagnostic(
                ProcurementValidationRules.ComparisonHasNoCandidates,
                $"{subject} names no candidates."));
            return;
        }

        foreach (var duplicate in definition.Candidates
                     .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key)
                     .OrderBy(c => c, StringComparer.Ordinal))
            errors.Add(Diagnostic(
                ProcurementValidationRules.DuplicateCandidateCode,
                $"{subject} holds two candidates sharing the code '{duplicate}'."));

        foreach (var duplicate in definition.Candidates
                     .GroupBy(c => c.SupplierRecordId, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key)
                     .OrderBy(c => c, StringComparer.Ordinal))
            warnings.Add(Diagnostic(
                ProcurementValidationRules.DuplicateCandidateSupplier,
                $"{subject} holds two candidates naming supplier '{duplicate}'."));

        if (definition.CandidatesInContention.Count() == 1)
            warnings.Add(Diagnostic(
                ProcurementValidationRules.SingleCandidateInContention,
                $"{subject} leaves one candidate in contention, so it recommends the only option rather than the best one."));

        foreach (var candidate in definition.Candidates)
        {
            foreach (var assessment in candidate.Assessments.Where(a => a.IsEstablished && !a.IsSupported))
                warnings.Add(Diagnostic(
                    ProcurementValidationRules.AssessmentIsUnsupported,
                    $"{subject} candidate '{candidate.Code}' is assessed as {assessment.Standing} on criterion "
                    + $"'{assessment.CriterionCode}' with nothing supporting it."));

            if (candidate.Exclusion is { IsAutomatic: false, ExcludedByPrincipalId: null })
                warnings.Add(Diagnostic(
                    ProcurementValidationRules.JudgementExclusionNeedsPrincipal,
                    $"{subject} excludes candidate '{candidate.Code}' by judgement rather than by a mandatory criterion, "
                    + "but names nobody who made that judgement."));
        }

        EvaluateLeadTimeComparability(definition, subject, warnings);
    }

    private static void EvaluateLeadTimeComparability(
        SourcingComparison definition,
        string subject,
        List<IValidationDiagnostic> warnings)
    {
        var leadTimes = definition.CandidatesInContention
            .Select(c => c.LeadTime)
            .OfType<LeadTimeDuration>()
            .ToList();

        if (leadTimes.Count > 1 && !leadTimes.All(d => d.IsComparableWith(leadTimes[0])))
            warnings.Add(Diagnostic(
                ProcurementValidationRules.LeadTimesNotComparable,
                $"{subject} holds candidate lead times in units that cannot be compared with each other. Working days and "
                + "calendar time are not interchangeable, and TempestOS will not assume a shift pattern to make them so."));
    }

    private static void EvaluateRecommendation(
        SourcingComparison definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (definition.RecommendedCandidateCode is not { } recommended)
            return;

        var candidate = definition.FindCandidate(recommended);

        if (candidate is null)
        {
            errors.Add(Diagnostic(
                ProcurementValidationRules.RecommendedCandidateUnresolved,
                $"{subject} recommends candidate '{recommended}', which it does not hold."));
            return;
        }

        if (!candidate.IsInContention)
            errors.Add(Diagnostic(
                ProcurementValidationRules.RecommendedCandidateIsExcluded,
                $"{subject} recommends candidate '{recommended}', which it also excluded: {candidate.Exclusion!.Reason}"));

        if (definition.Strength is RecommendationStrength.Insufficient or RecommendationStrength.Provisional)
            warnings.Add(Diagnostic(
                ProcurementValidationRules.RecommendationRestsOnGaps,
                $"{subject} recommends candidate '{recommended}' on a {definition.Strength} comparison. "
                + $"{definition.OutstandingQuestions.Count} question(s) remain unanswered."));

        if (string.IsNullOrWhiteSpace(definition.RecommendationRationale))
            warnings.Add(Diagnostic(
                ProcurementValidationRules.RecommendationHasNoRationale,
                $"{subject} recommends candidate '{recommended}' but says nothing about why."));
    }

    private static void EvaluateDecision(
        SourcingComparison definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (!definition.HasBeenDecided)
            return;

        if (definition.DecidedBy is null)
            errors.Add(Diagnostic(
                ProcurementValidationRules.DecisionNeedsAuthority,
                $"{subject} records a procurement decision but names nobody who took it. TempestOS compares and recommends; "
                + "it never decides, and a decision it cannot attribute to a person must not stand in the record."));

        if (definition.DecisionState != SourcingDecisionState.NoneChosen
            && string.IsNullOrWhiteSpace(definition.ChosenCandidateCode))
            errors.Add(Diagnostic(
                ProcurementValidationRules.DecisionNeedsChosenCandidate,
                $"{subject} records a decision but names no candidate chosen."));

        if (definition.DepartsFromRecommendation && string.IsNullOrWhiteSpace(definition.DecisionRationale))
            warnings.Add(Diagnostic(
                ProcurementValidationRules.DepartureNeedsRationale,
                $"{subject} was decided against its own recommendation without saying why. Departing from it is entirely "
                + "legitimate; leaving no reason is what makes the decision unreviewable."));

        if (definition.Strength == RecommendationStrength.Insufficient)
            warnings.Add(Diagnostic(
                ProcurementValidationRules.DecisionOnInsufficientComparison,
                $"{subject} was decided on a comparison that established too little to rank the candidates."));
    }

    private async Task EvaluateAgainstRequirementAsync(
        SourcingComparison definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_requirements is null)
            return;

        var record = await _requirements
            .FindByReferenceAsync(definition.RequirementReference, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
            return;

        var requirement = record.Definition;

        foreach (var candidate in definition.Candidates)
        {
            foreach (var assessment in candidate.Assessments
                         .Where(a => requirement.FindCriterion(a.CriterionCode) is null))
                warnings.Add(Diagnostic(
                    ProcurementValidationRules.AssessmentCriterionUnresolved,
                    $"{subject} candidate '{candidate.Code}' is assessed on criterion '{assessment.CriterionCode}', "
                    + $"which requirement '{requirement.Reference}' does not state."));

            if (requirement.ComparisonCurrency is { } currency
                && candidate.Price is { } price
                && price.Currency != currency)
                warnings.Add(Diagnostic(
                    ProcurementValidationRules.CandidateCurrencyMismatch,
                    $"{subject} candidate '{candidate.Code}' is priced in {price.Currency} but the requirement compares in "
                    + $"{currency}. The two cannot be ranked against each other."));
        }
    }

    private async Task EvaluateSuppliersAsync(
        SourcingComparison definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_suppliers is null)
            return;

        foreach (var candidate in definition.Candidates)
        {
            var supplier = await _suppliers.FindAsync(candidate.SupplierRecordId, cancellationToken).ConfigureAwait(false);

            if (supplier is null)
                warnings.Add(Diagnostic(
                    ProcurementValidationRules.CandidateSupplierMustResolve,
                    $"{subject} candidate '{candidate.Code}' names supplier '{candidate.SupplierRecordId}', which the "
                    + "supplier database does not hold."));
        }
    }
}
