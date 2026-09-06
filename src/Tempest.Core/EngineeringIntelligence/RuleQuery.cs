using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>A deterministic filter over the engineering rule library.</summary>
/// <remarks>
/// Every criterion is a predicate, every unset criterion matches
/// everything, criteria combine with AND, and results come back in
/// ascending record-Id order — the same contract every `P01` library
/// offers. Not a search engine: these are the dimensions an engineer
/// actually looks for a rule along.
/// </remarks>
public sealed record RuleQuery
{
    /// <summary>Matches any rule whose code contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CodeContains { get; init; }

    /// <summary>Matches any rule whose name or statement contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these domains. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<RuleDomain> Domains { get; init; } = [];

    /// <summary>Matches any of these severities. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<RuleSeverity> Severities { get; init; } = [];

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];

    /// <summary>Matches any rule that applies to this subject kind, including rules that apply to any kind. <see langword="null"/> to match any.</summary>
    public string? AppliesToSubjectKind { get; init; }

    /// <summary>Matches any rule that applies to this family, including rules that apply to any family. <see langword="null"/> to match any.</summary>
    public string? AppliesToFamily { get; init; }

    /// <summary>Matches any rule citing a standard whose designation contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CitesStandardContaining { get; init; }

    /// <summary>Matches any rule reading this property name in its own condition. <see langword="null"/> to match any.</summary>
    public string? ReadsProperty { get; init; }

    /// <summary>Matches any rule comparing against this `A6` constant symbol. <see langword="null"/> to match any.</summary>
    public string? UsesConstantSymbol { get; init; }

    /// <summary>Matches rules by whether their author declared them safety-critical. <see langword="null"/> to match any.</summary>
    public bool? IsSafetyCritical { get; init; }

    /// <summary>Matches rules by whether they carry an executable condition at all. <see langword="null"/> to match any.</summary>
    public bool? IsExecutable { get; init; }
}

/// <summary>Evaluates a <see cref="RuleQuery"/> against one rule record.</summary>
/// <remarks>A pure predicate kept out of <see cref="RuleCatalog"/> so query semantics can be tested without a store.</remarks>
internal static class RuleQueryEvaluator
{
    public static bool Matches(IReferenceRecord<RuleDefinition> record, RuleQuery query)
    {
        var rule = record.Definition;

        if (query.CodeContains is not null && !rule.Code.Contains(query.CodeContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.TextContains is { } text
            && !rule.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !rule.Statement.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Domains.Count > 0 && !query.Domains.Contains(rule.Domain))
            return false;

        if (query.Severities.Count > 0 && !query.Severities.Contains(rule.Severity))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        // A rule that names no subject kind applies to every kind, so it
        // matches a filter for any particular one.
        if (query.AppliesToSubjectKind is { } kind
            && rule.Applicability.SubjectKinds.Count > 0
            && !rule.Applicability.SubjectKinds.Any(k => string.Equals(k, kind, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.AppliesToFamily is { } family
            && rule.Applicability.Families.Count > 0
            && !rule.Applicability.Families.Any(f => string.Equals(f, family, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.CitesStandardContaining is { } standard
            && !rule.Standards.Any(s => s.Designation.Contains(standard, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.ReadsProperty is { } property
            && !(rule.Condition?.ReferencedProperties.Any(p => string.Equals(p, property, StringComparison.OrdinalIgnoreCase)) ?? false))
            return false;

        if (query.UsesConstantSymbol is { } symbol
            && !(rule.Condition?.RequiredConstantSymbols.Contains(symbol.Trim(), StringComparer.Ordinal) ?? false))
            return false;

        if (query.IsSafetyCritical is { } safety && rule.IsSafetyCritical != safety)
            return false;

        if (query.IsExecutable is { } executable && rule.IsExecutable != executable)
            return false;

        return true;
    }
}
