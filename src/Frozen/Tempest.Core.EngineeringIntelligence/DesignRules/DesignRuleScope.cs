namespace Tempest.Core.EngineeringIntelligence.DesignRules;

/// <summary>
/// Which rules an assessment runs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Narrowing the scope narrows the claim.</b> An assessment restricted
/// to one domain says nothing about the others, and
/// <see cref="AssessmentScopeStatement"/> carries that statement into the
/// result so nobody later reads a partial assessment as a complete one.
/// </para>
/// <para>
/// The default is every released rule that applies, which is the only
/// scope that supports an unqualified claim.
/// </para>
/// </remarks>
public sealed record DesignRuleScope
{
    /// <summary>Every released rule that applies to the subject — the only scope that supports an unqualified claim.</summary>
    public static DesignRuleScope All { get; } = new();

    /// <summary>Restricts the assessment to these domains. Never <see langword="null"/>; empty runs every domain.</summary>
    public IReadOnlyList<RuleDomain> Domains { get; init; } = [];

    /// <summary>Restricts the assessment to these severities. Never <see langword="null"/>; empty runs every severity.</summary>
    public IReadOnlyList<RuleSeverity> Severities { get; init; } = [];

    /// <summary>
    /// Restricts the assessment to rules their authors declared
    /// safety-critical. <see langword="null"/> runs both.
    /// </summary>
    public bool? SafetyCriticalOnly { get; init; }

    /// <summary>Restricts the assessment to these rule codes exactly. Never <see langword="null"/>; empty places no restriction.</summary>
    public IReadOnlyList<string> RuleCodes { get; init; } = [];

    /// <summary>Whether this scope is the unrestricted one.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsUnrestricted =>
        Domains.Count == 0 && Severities.Count == 0 && SafetyCriticalOnly is null && RuleCodes.Count == 0;

    /// <summary>A sentence stating what the assessment covered, and by implication what it did not.</summary>
    public string Describe()
    {
        if (IsUnrestricted)
            return "Every released rule that applies to this subject was run.";

        var parts = new List<string>();

        if (RuleCodes.Count > 0)
            parts.Add($"only rules [{string.Join(", ", RuleCodes)}]");

        if (Domains.Count > 0)
            parts.Add($"only the {string.Join(", ", Domains)} domain(s)");

        if (Severities.Count > 0)
            parts.Add($"only {string.Join(", ", Severities)} rules");

        if (SafetyCriticalOnly is true)
            parts.Add("only rules declared safety-critical");
        else if (SafetyCriticalOnly is false)
            parts.Add("only rules not declared safety-critical");

        return $"A restricted assessment: {string.Join("; ", parts)}. "
            + "It says nothing about rules outside that scope.";
    }
}

/// <summary>
/// What an assessment covered, carried into its result.
/// </summary>
/// <remarks>
/// Recorded rather than implied, because the most dangerous thing a rule
/// system can produce is a clean result whose scope nobody can see. A
/// reader six months later needs to know that only the fastener rules ran,
/// and that the rule library held eleven applicable rules of which three
/// were skipped for being unreleased.
/// </remarks>
/// <param name="Scope">What the caller asked for.</param>
/// <param name="ApplicableRuleCount">How many released rules applied to the subject before the scope narrowed them.</param>
/// <param name="RunRuleCount">How many actually ran.</param>
/// <param name="UnreleasedRuleCount">How many applicable rules were skipped for not being released — guidance that exists but is not yet trustworthy.</param>
public sealed record AssessmentScopeStatement(
    DesignRuleScope Scope,
    int ApplicableRuleCount,
    int RunRuleCount,
    int UnreleasedRuleCount)
{
    /// <summary>A sentence stating what was covered and what was not.</summary>
    public string Describe()
    {
        var text = $"{Scope.Describe()} {RunRuleCount} of {ApplicableRuleCount} applicable released rule(s) ran.";

        if (UnreleasedRuleCount > 0)
            text += $" A further {UnreleasedRuleCount} rule(s) apply to this subject but are not released, "
                + "so they were not run and this assessment says nothing about them.";

        if (ApplicableRuleCount == 0)
            text += " No released rule in the library applies to this subject, so this assessment establishes nothing about it.";

        return text;
    }
}
