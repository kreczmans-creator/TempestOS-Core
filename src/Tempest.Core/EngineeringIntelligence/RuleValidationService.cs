using Tempest.Core.EngineeringDomain;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>The concrete <see cref="IRuleValidationService"/> implementation.</summary>
/// <remarks>
/// Provenance, verification attributability, supersession and
/// cited-standard resolution are checked by
/// <see cref="ReferenceValidationService{TDefinition}"/>, shared with every
/// `P01` library — a rule's provenance <em>is</em> its engineering
/// authority, so those shared rules are exactly the right ones here.
/// Everything below is about being a usable engineering rule.
/// </remarks>
public sealed class RuleValidationService : ReferenceValidationService<RuleDefinition>, IRuleValidationService
{
    private readonly IReleasedConstantSource? _constants;
    private readonly IReadOnlySet<string> _knownSubjectKinds;

    /// <summary>Initialises a new instance of the <see cref="RuleValidationService"/> class.</summary>
    /// <param name="catalog">The rule library whose records this service validates.</param>
    /// <param name="constants">The released-constant seam, for confirming a rule's constant-backed thresholds resolve. Optional: a rule must be authorable before the constant it cites has been released.</param>
    /// <param name="standardResolver">Resolves a cited standard against `A2`. Optional.</param>
    public RuleValidationService(
        IRuleCatalog catalog,
        IReleasedConstantSource? constants = null,
        IStandardResolver? standardResolver = null)
        : base(catalog, materialCatalog: null, standardResolver)
    {
        _constants = constants;
        _knownSubjectKinds = AssessmentSubjectKinds.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        RuleDefinition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        EvaluateStatement(definition, errors, warnings);
        EvaluateApplicability(definition, warnings);
        EvaluateAuthority(definition, errors, warnings);
        EvaluateCondition(definition, errors, warnings);

        await EvaluateStandardReferencesAsync(definition.Standards, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateConstantsAsync(definition, warnings, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task EvaluateRecordAsync(
        IReferenceRecord<RuleDefinition> record,
        IReadOnlyList<IReferenceRecord<RuleDefinition>>? library,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var key = record.Definition.CodeKey;

        // Defence in depth: the catalogue already prevents this at write
        // time. Confirming it on read catches an index written before that
        // guard existed, or corrupted since.
        var others = library ?? await Catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var collisions = others
            .Where(other => !string.Equals(other.Id, record.Id, StringComparison.Ordinal))
            .Where(other => string.Equals(other.Definition.CodeKey, key, StringComparison.Ordinal))
            .Select(other => other.Id)
            .ToList();

        if (collisions.Count > 0)
            errors.Add(Diagnostic(
                RuleValidationRules.DuplicateRuleCode,
                $"Rule code '{record.Definition.Code}' is also registered as: {string.Join(", ", collisions)}. "
                + "A rule code identifies a rule across its revisions, so two rules cannot share one."));
    }

    private static void EvaluateStatement(RuleDefinition rule, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (rule.Severity == RuleSeverity.Unspecified)
            errors.Add(Diagnostic(
                RuleValidationRules.SeverityMustBeStated,
                $"Rule '{rule.Code}' does not say how binding it is. "
                + "Until it does, failing it means neither a defect nor a preference, and the rule cannot be acted on."));

        if (rule.Domain == RuleDomain.Unspecified)
            warnings.Add(Diagnostic(
                RuleValidationRules.DomainShouldBeStated,
                $"Rule '{rule.Code}' records no domain, so an engineer working in a discipline will not find it."));

        if (rule.Domain == RuleDomain.Other && string.IsNullOrWhiteSpace(rule.SourceClassification))
            errors.Add(Diagnostic(
                RuleValidationRules.OtherDomainNeedsSourceClassification,
                $"Rule '{rule.Code}' is classified 'Other' but records none of the author's own classification wording."));

        if (string.IsNullOrWhiteSpace(rule.Rationale))
            warnings.Add(Diagnostic(
                RuleValidationRules.RationaleShouldBeRecorded,
                $"Rule '{rule.Code}' records no rationale. A rule nobody can justify is a rule nobody can safely revise, "
                + "and is the first thing to be ignored when it becomes inconvenient."));

        // A statement that is the condition's own syntax read aloud tells
        // an engineer nothing the condition does not already say.
        if (rule.Condition is { } condition
            && string.Equals(rule.Statement.Trim(), condition.Describe(), StringComparison.OrdinalIgnoreCase))
            warnings.Add(Diagnostic(
                RuleValidationRules.StatementShouldBeEngineeringLanguage,
                $"Rule '{rule.Code}' states its condition verbatim rather than saying what it means. "
                + "The statement is what an engineer reads; the condition is what the engine evaluates."));
    }

    private void EvaluateApplicability(RuleDefinition rule, List<IValidationDiagnostic> warnings)
    {
        foreach (var kind in rule.Applicability.SubjectKinds)
        {
            if (!_knownSubjectKinds.Contains(kind))
                warnings.Add(Diagnostic(
                    RuleValidationRules.UnknownSubjectKind,
                    $"Rule '{rule.Code}' applies to subject kind '{kind}', which no reference library produces. "
                    + $"The kinds in use are: {string.Join(", ", AssessmentSubjectKinds.All)}. The rule will never match anything."));
        }
    }

    private static void EvaluateAuthority(RuleDefinition rule, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (!rule.IsSafetyCritical)
            return;

        // A safety-critical rule is held to a higher bar than a general
        // one, because acting on it — or failing to — has consequences a
        // rationale field cannot carry.
        if (rule.Standards.Count == 0 && string.IsNullOrWhiteSpace(rule.Rationale))
            errors.Add(Diagnostic(
                RuleValidationRules.SafetyCriticalRuleNeedsAuthority,
                $"Rule '{rule.Code}' is declared safety-critical but names neither a standard it derives from nor a rationale. "
                + "A safety-critical rule asserted on nobody's authority must not be released."));

        if (string.IsNullOrWhiteSpace(rule.Consequence))
            warnings.Add(Diagnostic(
                RuleValidationRules.SafetyCriticalRuleNeedsConsequence,
                $"Rule '{rule.Code}' is declared safety-critical but does not say what goes wrong when it is not followed. "
                + "A reviewer weighing a deviation needs that, and cannot infer it."));
    }

    private static void EvaluateCondition(RuleDefinition rule, List<IValidationDiagnostic> errors, List<IValidationDiagnostic> warnings)
    {
        if (rule.Condition is not { } condition)
        {
            errors.Add(Diagnostic(
                RuleValidationRules.ConditionMustBeStated,
                $"Rule '{rule.Code}' states no condition, so it can never be evaluated against anything."));
            return;
        }

        foreach (var comparison in condition.Flatten().OfType<QuantityComparisonExpression>())
        {
            if (comparison.Threshold.Literal is not { } literal)
                continue;

            var expected = MaterialPropertyNames.ExpectedDimensionOf(comparison.PropertyName);

            if (expected is not null && !string.Equals(expected, literal.DimensionName, StringComparison.Ordinal))
                errors.Add(Diagnostic(
                    RuleValidationRules.ThresholdDimensionMismatch,
                    $"Rule '{rule.Code}' compares {comparison.PropertyName}, which is a {expected}, "
                    + $"against a {literal.DimensionName}. The comparison can never conclude anything."));

            if (literal.IsDerived)
                warnings.Add(Diagnostic(
                    RuleValidationRules.ThresholdIsDerived,
                    $"Rule '{rule.Code}' compares against a threshold TempestOS derived rather than one a source published. "
                    + "A derived threshold gives the rule no authority it did not already have."));
            else if (literal.Origin == ReferenceValueOrigin.Unknown)
                warnings.Add(Diagnostic(
                    RuleValidationRules.ThresholdOriginShouldBeRecorded,
                    $"Rule '{rule.Code}' compares against a threshold whose origin is not recorded, "
                    + "so where the number came from is unknown."));
        }

        // A condition that always holds, or never does, is not a check.
        var stated = condition.Flatten().OfType<StatedExpression>().ToList();

        if (stated.Count > 0 && stated.Count == condition.Flatten().Count)
            warnings.Add(Diagnostic(
                RuleValidationRules.ConditionIsVacuous,
                $"Rule '{rule.Code}''s condition tests nothing about the subject — it is stated as "
                + $"{(stated[0].Holds ? "always holding" : "never holding")}. "
                + "That is legitimate where the rule's whole content is its applicability, and a modelling error otherwise."));

        foreach (var name in condition.ReferencedProperties)
        {
            // A well-known name is dimension-checkable; anything else is
            // legitimate but worth noting, because a typo in a property
            // name produces a rule that is never satisfied and never fails
            // — it silently reports "not recorded" forever.
            if (MaterialPropertyNames.ExpectedDimensionOf(name) is null && !SubjectPropertyNames.IsKnown(name))
                warnings.Add(Diagnostic(
                    RuleValidationRules.UnknownPropertyName,
                    $"Rule '{rule.Code}' reads property '{name}', which is not a well-known name in any reference library. "
                    + "That is legitimate for a domain-specific property, but a misspelled name produces a rule that "
                    + "reports 'not recorded' forever rather than ever failing."));
        }
    }

    private async Task EvaluateConstantsAsync(RuleDefinition rule, List<IValidationDiagnostic> warnings, CancellationToken cancellationToken)
    {
        if (_constants is null || rule.Condition is not { } condition)
            return;

        foreach (var symbol in condition.RequiredConstantSymbols)
        {
            if (await _constants.FindReleasedAsync(symbol, cancellationToken).ConfigureAwait(false) is null)
                warnings.Add(Diagnostic(
                    RuleValidationRules.ConstantNotReleased,
                    $"Rule '{rule.Code}' compares against constant '{symbol}', which is not available as a released engineering constant. "
                    + "Evaluating the rule will report that its evidence is missing rather than reaching a conclusion."));
        }
    }
}
