namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// The diagnostic codes <see cref="IRuleValidationService"/> reports.
/// </summary>
/// <remarks>
/// <b>Rules about rules.</b> These govern whether an authored engineering
/// rule is fit to be released as guidance — not whether any design
/// satisfies it. The rules about being a governed record at all live in
/// <see cref="ReferenceData.ReferenceValidationRules"/>'s
/// <c>TEMPEST-REF-</c> series, shared with every `P01` library, and are
/// not restated here.
/// </remarks>
public static class RuleValidationRules
{
    /// <summary>The rule states no condition, so it can never be evaluated.</summary>
    public const string ConditionMustBeStated = "TEMPEST-EIR-001";

    /// <summary>The rule does not say how binding it is, so failing it means nothing definite.</summary>
    public const string SeverityMustBeStated = "TEMPEST-EIR-002";

    /// <summary>The rule records no domain, so it cannot be found by the discipline it belongs to.</summary>
    public const string DomainShouldBeStated = "TEMPEST-EIR-003";

    /// <summary>A rule whose domain is <see cref="RuleDomain.Other"/> must record the author's own classification wording.</summary>
    public const string OtherDomainNeedsSourceClassification = "TEMPEST-EIR-004";

    /// <summary>The rule records no rationale, so a later engineer cannot tell why it exists or safely revise it.</summary>
    public const string RationaleShouldBeRecorded = "TEMPEST-EIR-005";

    /// <summary>A safety-critical rule must name the authority it derives from.</summary>
    public const string SafetyCriticalRuleNeedsAuthority = "TEMPEST-EIR-006";

    /// <summary>A safety-critical rule must state what goes wrong when it is not followed.</summary>
    public const string SafetyCriticalRuleNeedsConsequence = "TEMPEST-EIR-007";

    /// <summary>The rule's condition compares a property against a threshold of a different dimension, so it can never conclude anything.</summary>
    public const string ThresholdDimensionMismatch = "TEMPEST-EIR-008";

    /// <summary>The rule's condition compares against a value TempestOS derived rather than one a source published.</summary>
    public const string ThresholdIsDerived = "TEMPEST-EIR-009";

    /// <summary>The rule's condition compares against a transcribed value whose origin is not recorded, so the threshold has no stated authority.</summary>
    public const string ThresholdOriginShouldBeRecorded = "TEMPEST-EIR-010";

    /// <summary>The rule's condition cites an `A6` constant that is not available as a released constant.</summary>
    public const string ConstantNotReleased = "TEMPEST-EIR-011";

    /// <summary>The rule's condition reads a well-known property under a name no reference library recognises.</summary>
    public const string UnknownPropertyName = "TEMPEST-EIR-012";

    /// <summary>The rule's applicability names a subject kind no reference library uses.</summary>
    public const string UnknownSubjectKind = "TEMPEST-EIR-013";

    /// <summary>Two rules share one rule code.</summary>
    public const string DuplicateRuleCode = "TEMPEST-EIR-014";

    /// <summary>The rule's condition can never be satisfied, or can never fail, so evaluating it tells an engineer nothing.</summary>
    public const string ConditionIsVacuous = "TEMPEST-EIR-015";

    /// <summary>The rule's statement restates its own condition rather than saying what it means in engineering language.</summary>
    public const string StatementShouldBeEngineeringLanguage = "TEMPEST-EIR-016";
}
