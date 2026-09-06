using Tempest.Core.EngineeringIntelligence.Subjects;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.MaterialSelection;

/// <summary>The concrete <see cref="IMaterialSelectionService"/> implementation.</summary>
/// <remarks>
/// <para>
/// Every criterion, every family restriction and every applicable released
/// rule goes through the same <see cref="RuleEngine"/>, so a project
/// criterion and a library rule cannot drift apart in how they treat a
/// missing value. The engine is pure; this class supplies it with
/// catalogue reads, resolved constants, a clock and a principal, and does
/// nothing else.
/// </para>
/// <para>
/// <b>Every candidate is assessed and reported, including the eliminated
/// ones.</b> Quietly omitting a candidate that failed a criterion would
/// leave an engineer unable to tell "it was not considered" from "it was
/// considered and ruled out for this reason" — and the second is often
/// the more useful answer.
/// </para>
/// </remarks>
public sealed class MaterialSelectionService : IMaterialSelectionService
{
    /// <summary>The library name every material pin carries.</summary>
    public const string MaterialLibrary = "Materials";

    /// <summary>
    /// Recorded as the assessor where no principal is established — the
    /// same honest placeholder <see cref="Calculations.CalculationEngine"/>
    /// and <see cref="EngineeringData.EngineeringDocumentStore"/> already
    /// use, rather than an empty string that reads as an omission.
    /// </summary>
    public const string UnknownAssessorPrincipalId = "unknown";

    private readonly IMaterialCatalog _materials;
    private readonly IRuleCatalog? _rules;
    private readonly IReleasedConstantSource? _constants;
    private readonly ICurrentPrincipalAccessor _principals;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="MaterialSelectionService"/> class.</summary>
    /// <param name="materials">The `A1` catalogue candidates are read from.</param>
    /// <param name="principals">The platform's own identity boundary, for attributing an assessment.</param>
    /// <param name="rules">The rule library, for the released rules that apply to a candidate. Optional: selection against project criteria alone is legitimate, and a rule library may be empty.</param>
    /// <param name="constants">The released-constant seam, for rules whose thresholds are constants. Optional.</param>
    /// <param name="timeProvider">The clock an assessment is stamped with. Defaults to the system clock; injected so a test can pin it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="materials"/> or <paramref name="principals"/> is <see langword="null"/>.</exception>
    public MaterialSelectionService(
        IMaterialCatalog materials,
        ICurrentPrincipalAccessor principals,
        IRuleCatalog? rules = null,
        IReleasedConstantSource? constants = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(principals);

        _materials = materials;
        _principals = principals;
        _rules = rules;
        _constants = constants;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<MaterialSelectionResult> AssessAsync(
        MaterialRequirementSet requirements,
        IReadOnlyList<IReferenceRecord<MaterialDefinition>> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(candidates);

        var assessments = new List<MaterialCandidateAssessment>(candidates.Count);

        foreach (var candidate in candidates.OrderBy(c => c.Id, StringComparer.Ordinal))
            assessments.Add(await AssessCandidateAsync(requirements, candidate, cancellationToken).ConfigureAwait(false));

        return new MaterialSelectionResult(
            requirements.ApplicationDescription,
            assessments,
            _time.GetUtcNow(),
            _principals.Current?.Identity.Id ?? UnknownAssessorPrincipalId);
    }

    /// <inheritdoc />
    public async Task<MaterialSelectionResult> AssessCatalogueAsync(
        MaterialRequirementSet requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var query = new MaterialQuery
        {
            Families = requirements.AcceptableFamilies,
            ValidationStates = requirements.RequireReleasedMaterials
                ? [ReferenceValidationState.Released]
                : [],
        };

        var candidates = (await _materials.SearchAsync(query, cancellationToken).ConfigureAwait(false))
            .Where(record => !requirements.ExcludedFamilies.Contains(record.Definition.Family))
            .ToList();

        return await AssessAsync(requirements, candidates, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MaterialSelectionResult> ReproduceAsync(
        MaterialRequirementSet requirements,
        IReadOnlyList<ReferencePin> pins,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(pins);

        var candidates = new List<IReferenceRecord<MaterialDefinition>>(pins.Count);

        foreach (var pin in pins)
        {
            if (!string.Equals(pin.Library, MaterialLibrary, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Pin {pin} names library '{pin.Library}', and this service can only reproduce material pins.",
                    nameof(pins));

            // The whole point: read the record as it stood, not as it is.
            candidates.Add(await _materials
                .GetRevisionAsync(pin.RecordId, pin.RevisionNumber, cancellationToken)
                .ConfigureAwait(false));
        }

        return await AssessAsync(requirements, candidates, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MaterialCandidateAssessment> AssessCandidateAsync(
        MaterialRequirementSet requirements,
        IReferenceRecord<MaterialDefinition> candidate,
        CancellationToken cancellationToken)
    {
        var subject = new MaterialSubject(candidate);
        var criterionAssessments = new List<CriterionAssessment>();

        // The candidate's own governance is a criterion in its own right:
        // selecting against an unverified record is a real risk, and
        // reporting it as a criterion keeps it visible rather than buried
        // in a filter.
        if (requirements.RequireReleasedMaterials && candidate.ValidationState != ReferenceValidationState.Released)
            criterionAssessments.Add(new CriterionAssessment(
                "The material record must be Released",
                MaterialCriterionRole.Constraint,
                AssessmentOutcome.Fail,
                $"{subject.DisplayName} is {candidate.ValidationState}, not Released. "
                + "Its recorded values have not been verified to the standard a released record requires."));

        foreach (var criterion in requirements.Criteria)
            criterionAssessments.Add(AssessCriterion(criterion, subject));

        foreach (var criterion in requirements.EvidenceCriteria)
            criterionAssessments.Add(new CriterionAssessment(
                criterion.Description,
                criterion.Role,
                AssessmentOutcome.EvidenceRequired,
                $"This cannot be concluded from recorded material properties and needs evidence a person supplies: {criterion.Description}"));

        var familyAssessment = AssessFamily(requirements, candidate, subject);
        var ruleEvaluations = await EvaluateRulesAsync(subject, cancellationToken).ConfigureAwait(false);

        return new MaterialCandidateAssessment(
            candidate.Id,
            subject.DisplayName,
            subject.Pin,
            candidate.ValidationState,
            criterionAssessments,
            ruleEvaluations,
            familyAssessment);
    }

    private static CriterionAssessment AssessCriterion(MaterialCriterion criterion, MaterialSubject subject)
    {
        // Project criteria run through the same engine as library rules,
        // so a missing property is treated identically by both.
        var probe = new RuleDefinition
        {
            Code = "CRITERION",
            Name = criterion.Describe(),
            Statement = criterion.Describe(),
            Severity = criterion.Role == MaterialCriterionRole.Constraint ? RuleSeverity.Requirement : RuleSeverity.Recommendation,
            Condition = criterion.Expression,
        };

        var evaluation = RuleEngine.Evaluate(
            probe,
            new ReferencePin("Criteria", "criterion", 1),
            subject,
            ConstantResolutionSet.Empty);

        var outcome = criterion.Role == MaterialCriterionRole.Informational && AssessmentOutcomes.IsAdverse(evaluation.Outcome)
            ? AssessmentOutcome.Concern
            : evaluation.Outcome;

        return new CriterionAssessment(
            criterion.Describe(),
            criterion.Role,
            outcome,
            evaluation.ConditionResult?.Reason ?? evaluation.Reason);
    }

    private static CriterionAssessment? AssessFamily(
        MaterialRequirementSet requirements,
        IReferenceRecord<MaterialDefinition> candidate,
        MaterialSubject subject)
    {
        var family = candidate.Definition.Family;

        if (requirements.ExcludedFamilies.Contains(family))
            return new CriterionAssessment(
                $"The material family must not be one of [{string.Join(", ", requirements.ExcludedFamilies)}]",
                MaterialCriterionRole.Constraint,
                AssessmentOutcome.Fail,
                $"{subject.DisplayName} is a {family}, which this application rules out.");

        if (requirements.AcceptableFamilies.Count == 0)
            return null;

        // An unstated family cannot be checked against a family
        // restriction, and assuming it passes would let an unclassified
        // material through a restriction it may well violate.
        if (family == MaterialFamily.Unspecified)
            return new CriterionAssessment(
                $"The material family must be one of [{string.Join(", ", requirements.AcceptableFamilies)}]",
                MaterialCriterionRole.Constraint,
                AssessmentOutcome.NotRecorded,
                $"{subject.DisplayName} records no material family, so whether it meets the family restriction cannot be determined.");

        return new CriterionAssessment(
            $"The material family must be one of [{string.Join(", ", requirements.AcceptableFamilies)}]",
            MaterialCriterionRole.Constraint,
            requirements.AcceptableFamilies.Contains(family) ? AssessmentOutcome.Pass : AssessmentOutcome.Fail,
            $"{subject.DisplayName} is a {family}. "
            + (requirements.AcceptableFamilies.Contains(family) ? "That is acceptable." : "That is not among the acceptable families."));
    }

    private async Task<IReadOnlyList<RuleEvaluation>> EvaluateRulesAsync(
        MaterialSubject subject,
        CancellationToken cancellationToken)
    {
        if (_rules is null)
            return [];

        var applicable = await _rules.FindReleasedApplicableAsync(subject, cancellationToken).ConfigureAwait(false);

        if (applicable.Count == 0)
            return [];

        var constants = _constants is null
            ? ConstantResolutionSet.Empty
            : await ConstantResolutionSet
                .ResolveForAsync(applicable.Select(r => r.Definition), _constants, cancellationToken)
                .ConfigureAwait(false);

        return applicable
            .Select(rule => RuleEngine.Evaluate(
                rule.Definition,
                ReferencePin.For(_rules.LibraryName, rule),
                subject,
                constants))
            .ToList();
    }
}
