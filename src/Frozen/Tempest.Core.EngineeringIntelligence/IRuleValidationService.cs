using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// Governance of engineering rules themselves — whether an authored rule
/// is fit to be released as guidance.
/// </summary>
/// <remarks>
/// <para>
/// The surface is <see cref="IReferenceValidationService{TDefinition}"/>,
/// shared with every `P01` library; what is rule-specific is the rule set
/// behind it (<see cref="RuleValidationRules"/>).
/// </para>
/// <para>
/// <b>This validates rules, not designs.</b> Nothing here asks whether any
/// subject satisfies any rule — that is <see cref="RuleEngine"/>. What it
/// asks is whether the rule says something definite, on some stated
/// authority, for a reason a later engineer can read. A rule that fails
/// these is not wrong about engineering; it is not yet a rule.
/// </para>
/// <para>
/// The consequence matters: a rule with errors cannot reach Validated, and
/// so can never reach Released, and so can never be returned by
/// <see cref="IRuleCatalog.FindReleasedApplicableAsync"/>. Unvalidated
/// guidance cannot reach an engineering conclusion.
/// </para>
/// </remarks>
public interface IRuleValidationService : IReferenceValidationService<RuleDefinition>
{
}
