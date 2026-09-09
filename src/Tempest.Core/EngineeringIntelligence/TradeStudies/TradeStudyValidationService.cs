using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.TradeStudies;

/// <summary>The diagnostic codes <see cref="ITradeStudyValidationService"/> reports.</summary>
public static class TradeStudyValidationRules
{
    /// <summary>The study states nothing to compare options on.</summary>
    public const string StudyMustHaveConsiderations = "TEMPEST-EIT-001";

    /// <summary>Two considerations in one study share a code, so a judgement cannot be tied to one of them.</summary>
    public const string DuplicateConsiderationCode = "TEMPEST-EIT-002";

    /// <summary>A consideration does not say what kind of statement it is, so the framework cannot tell whether it eliminates.</summary>
    public const string ConsiderationKindMustBeStated = "TEMPEST-EIT-003";

    /// <summary>The study states no criterion, so admissible options cannot be told apart.</summary>
    public const string StudyShouldHaveDiscriminatingCriteria = "TEMPEST-EIT-004";

    /// <summary>A consideration no rule condition can settle does not say what evidence would settle it.</summary>
    public const string ManualConsiderationShouldStateEvidence = "TEMPEST-EIT-005";

    /// <summary>Two assumptions in one study share a code.</summary>
    public const string DuplicateAssumptionCode = "TEMPEST-EIT-006";

    /// <summary>Two risks in one study share a code.</summary>
    public const string DuplicateRiskCode = "TEMPEST-EIT-007";

    /// <summary>The study records no assumptions. Every trade study rests on some.</summary>
    public const string AssumptionsShouldBeRecorded = "TEMPEST-EIT-008";

    /// <summary>A load-bearing assumption names nobody to confirm it.</summary>
    public const string CriticalAssumptionShouldHaveOwner = "TEMPEST-EIT-009";

    /// <summary>An accepted risk names nobody who accepted it.</summary>
    public const string AcceptedRiskMustNameAcceptor = "TEMPEST-EIT-010";

    /// <summary>A mitigated risk says nothing about what is being done.</summary>
    public const string MitigatedRiskMustStateMitigation = "TEMPEST-EIT-011";

    /// <summary>The study records no rationale for how it is framed.</summary>
    public const string RationaleShouldBeRecorded = "TEMPEST-EIT-012";

    /// <summary>Two studies share one study code.</summary>
    public const string DuplicateStudyCode = "TEMPEST-EIT-013";

    /// <summary>The study names a subject kind no reference library produces.</summary>
    public const string UnknownSubjectKind = "TEMPEST-EIT-014";

    /// <summary>A consideration's condition reads a property no reference library records.</summary>
    public const string UnknownPropertyName = "TEMPEST-EIT-015";

    /// <summary>A consideration cites a standard the standards library does not hold.</summary>
    public const string StandardMustResolve = "TEMPEST-EIT-016";

    /// <summary>Every consideration eliminates, so the study can only reject and never compare.</summary>
    public const string StudyIsAllConstraints = "TEMPEST-EIT-017";
}

/// <summary>Governance of trade-study definitions themselves.</summary>
public interface ITradeStudyValidationService : IReferenceValidationService<TradeStudyDefinition>
{
}

/// <summary>The concrete <see cref="ITradeStudyValidationService"/> implementation.</summary>
/// <remarks>
/// The checks here are about whether a study is <i>askable</i>, not about
/// whether its engineering is right. No amount of validation can tell
/// whether the right criteria were chosen; what it can tell is whether a
/// judgement can be attached to each of them, whether an accepted risk has
/// somebody's name on it, and whether the study can discriminate at all.
/// </remarks>
public sealed class TradeStudyValidationService
    : ReferenceValidationService<TradeStudyDefinition>, ITradeStudyValidationService
{
    /// <summary>Initialises a new instance of the <see cref="TradeStudyValidationService"/> class.</summary>
    /// <param name="catalog">The trade-study library whose records this service validates.</param>
    /// <param name="standardResolver">Resolves a cited standard against `A2`. Optional.</param>
    public TradeStudyValidationService(
        ITradeStudyCatalog catalog,
        IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog: null, standardResolver)
    {
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        TradeStudyDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateConsiderations(definition, errors, warnings);
        EvaluateAssumptions(definition, errors, warnings);
        EvaluateRisks(definition, errors, warnings);

        if (string.IsNullOrWhiteSpace(definition.Rationale))
            warnings.Add(Diagnostic(
                TradeStudyValidationRules.RationaleShouldBeRecorded,
                $"Trade study '{definition.Code}' does not record why it is framed as it is, so a later reader cannot tell "
                + "what was ruled out of the question before the options were drawn up."));

        if (definition.SubjectKind is { } kind && !AssessmentSubjectKinds.All.Contains(kind, StringComparer.OrdinalIgnoreCase))
            warnings.Add(Diagnostic(
                TradeStudyValidationRules.UnknownSubjectKind,
                $"Trade study '{definition.Code}' compares subject kind '{kind}', which no reference library produces."));

        await EvaluateStandardsAsync(definition, warnings, cancellationToken).ConfigureAwait(false);
    }

    private void EvaluateConsiderations(
        TradeStudyDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (definition.Considerations.Count == 0)
        {
            errors.Add(Diagnostic(
                TradeStudyValidationRules.StudyMustHaveConsiderations,
                $"Trade study '{definition.Code}' states nothing to compare options on, so carrying it out would establish nothing."));

            return;
        }

        foreach (var duplicate in Duplicates(definition.Considerations.Select(c => c.Code)))
            errors.Add(Diagnostic(
                TradeStudyValidationRules.DuplicateConsiderationCode,
                $"Trade study '{definition.Code}' declares consideration '{duplicate}' more than once, "
                + "so a judgement cannot be tied to one of them."));

        foreach (var consideration in definition.Considerations)
        {
            if (consideration.Kind == ConsiderationKind.Unspecified)
                errors.Add(Diagnostic(
                    TradeStudyValidationRules.ConsiderationKindMustBeStated,
                    $"Consideration '{consideration.Code}' in trade study '{definition.Code}' does not say whether it eliminates "
                    + "an option or merely counts against it, and the framework will not guess."));

            if (!consideration.IsAssessable && string.IsNullOrWhiteSpace(consideration.EvidenceExpected))
                warnings.Add(Diagnostic(
                    TradeStudyValidationRules.ManualConsiderationShouldStateEvidence,
                    $"Consideration '{consideration.Code}' in trade study '{definition.Code}' must be answered by a person but does not "
                    + "say what evidence would settle it, so nobody can tell when it has been answered."));

            foreach (var property in consideration.Condition?.ReferencedProperties ?? [])
            {
                if (!SubjectPropertyNames.IsKnown(property))
                    warnings.Add(Diagnostic(
                        TradeStudyValidationRules.UnknownPropertyName,
                        $"Consideration '{consideration.Code}' in trade study '{definition.Code}' reads property '{property}', "
                        + "which no reference library records. Every option will report NotRecorded against it."));
            }
        }

        if (!definition.DiscriminatingConsiderations.Any())
        {
            var code = definition.Considerations.All(c => c.IsEliminating)
                ? TradeStudyValidationRules.StudyIsAllConstraints
                : TradeStudyValidationRules.StudyShouldHaveDiscriminatingCriteria;

            warnings.Add(Diagnostic(
                code,
                $"Trade study '{definition.Code}' states no criterion, so it can rule options out but cannot say how the survivors differ. "
                + "That is a screening exercise rather than a trade study."));
        }
    }

    private void EvaluateAssumptions(
        TradeStudyDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        foreach (var duplicate in Duplicates(definition.Assumptions.Select(a => a.Code)))
            errors.Add(Diagnostic(
                TradeStudyValidationRules.DuplicateAssumptionCode,
                $"Trade study '{definition.Code}' declares assumption '{duplicate}' more than once."));

        if (definition.Assumptions.Count == 0)
            warnings.Add(Diagnostic(
                TradeStudyValidationRules.AssumptionsShouldBeRecorded,
                $"Trade study '{definition.Code}' records no assumptions. Every trade study rests on some; a study with none recorded "
                + "has not identified them rather than not having any."));

        foreach (var assumption in definition.Assumptions.Where(a => a.IsLoadBearing && string.IsNullOrWhiteSpace(a.Owner)))
            warnings.Add(Diagnostic(
                TradeStudyValidationRules.CriticalAssumptionShouldHaveOwner,
                $"Assumption '{assumption.Code}' in trade study '{definition.Code}' is load-bearing and unverified, but names nobody "
                + "to confirm it."));
    }

    private void EvaluateRisks(
        TradeStudyDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        foreach (var duplicate in Duplicates(definition.Risks.Select(r => r.Code)))
            errors.Add(Diagnostic(
                TradeStudyValidationRules.DuplicateRiskCode,
                $"Trade study '{definition.Code}' declares risk '{duplicate}' more than once."));

        foreach (var risk in definition.Risks)
        {
            if (risk.Standing == RiskStanding.Accepted && string.IsNullOrWhiteSpace(risk.AcceptedByPrincipalId))
                errors.Add(Diagnostic(
                    TradeStudyValidationRules.AcceptedRiskMustNameAcceptor,
                    $"Risk '{risk.Code}' in trade study '{definition.Code}' is recorded as accepted but names nobody who accepted it. "
                    + "Accepting a risk is an act of engineering authority and must be attributable."));

            if (risk.Standing == RiskStanding.Mitigated && string.IsNullOrWhiteSpace(risk.Mitigation))
                errors.Add(Diagnostic(
                    TradeStudyValidationRules.MitigatedRiskMustStateMitigation,
                    $"Risk '{risk.Code}' in trade study '{definition.Code}' is recorded as mitigated but does not say what is being done "
                    + "about it."));
        }
    }

    private async Task EvaluateStandardsAsync(
        TradeStudyDefinition definition,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var cited = definition.Standards
            .Concat(definition.Considerations.Select(c => c.Standard).OfType<StandardReference>())
            .Distinct()
            .ToList();

        await EvaluateStandardReferencesAsync(cited, warnings, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> Duplicates(IEnumerable<string> codes) =>
        codes
            .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
}
