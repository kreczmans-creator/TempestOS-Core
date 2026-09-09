namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// What kind of engineering statement a rule makes — and therefore what
/// failing it means.
/// </summary>
/// <remarks>
/// <b>"Must not" and "prefer" are not the same rule with a different
/// flag.</b> Flattening them into a boolean is the single most common way
/// an engineering rule system becomes untrustworthy: either every
/// preference starts blocking work, or every prohibition starts being
/// ignored alongside the preferences. Severity is therefore a required
/// part of a rule's meaning, not decoration on its result.
/// </remarks>
public enum RuleSeverity
{
    /// <summary>Not recorded. The honest default — never read as advisory, and never as blocking.</summary>
    Unspecified,

    /// <summary>The rule states something that must never be done. Failing it is a defect.</summary>
    Prohibition,

    /// <summary>The rule states something that must be achieved. Failing it is a defect.</summary>
    Requirement,

    /// <summary>The rule states a limit that must not be exceeded. Failing it is a defect.</summary>
    Constraint,

    /// <summary>The rule flags a condition that needs a person's attention. Failing it is not a defect.</summary>
    Warning,

    /// <summary>The rule states preferred practice. Failing it is a deliberate choice to be recorded, not a defect.</summary>
    Recommendation,

    /// <summary>The rule offers guidance for consideration. Failing it carries no obligation at all.</summary>
    Advisory
}

/// <summary>Questions about a <see cref="RuleSeverity"/>, answered in one place.</summary>
public static class RuleSeverities
{
    /// <summary>Every severity, from most binding to least.</summary>
    public static IReadOnlyList<RuleSeverity> All { get; } =
    [
        RuleSeverity.Prohibition,
        RuleSeverity.Requirement,
        RuleSeverity.Constraint,
        RuleSeverity.Warning,
        RuleSeverity.Recommendation,
        RuleSeverity.Advisory,
        RuleSeverity.Unspecified,
    ];

    /// <summary>
    /// Whether failing a rule of this severity is a defect — something
    /// that must be resolved rather than merely noted.
    /// </summary>
    /// <remarks>
    /// <see cref="RuleSeverity.Unspecified"/> is deliberately <b>not</b>
    /// blocking: a rule whose author never said how binding it is has not
    /// earned the authority to block work, and validation reports the
    /// omission rather than guessing.
    /// </remarks>
    public static bool IsBinding(RuleSeverity severity) =>
        severity is RuleSeverity.Prohibition or RuleSeverity.Requirement or RuleSeverity.Constraint;

    /// <summary>
    /// The outcome a rule of this severity produces when its own condition
    /// does not hold.
    /// </summary>
    /// <remarks>
    /// The one place severity turns into an outcome, so a binding rule
    /// cannot be quietly downgraded and an advisory one cannot quietly
    /// start failing a design. An unspecified severity yields
    /// <see cref="AssessmentOutcome.Concern"/>: something to look at,
    /// asserted as neither a defect nor a pass.
    /// </remarks>
    public static AssessmentOutcome OutcomeWhenNotSatisfied(RuleSeverity severity) => severity switch
    {
        RuleSeverity.Prohibition or RuleSeverity.Requirement or RuleSeverity.Constraint => AssessmentOutcome.Fail,
        RuleSeverity.Warning or RuleSeverity.Recommendation => AssessmentOutcome.Concern,
        RuleSeverity.Advisory => AssessmentOutcome.Concern,
        _ => AssessmentOutcome.Concern
    };
}
