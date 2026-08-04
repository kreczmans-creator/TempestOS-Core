namespace Tempest.Core.EngineeringDomain;

public interface IValidationResult
{
    bool IsValid { get; }
    IReadOnlyList<IValidationDiagnostic> Errors { get; }
    IReadOnlyList<IValidationDiagnostic> Warnings { get; }
}

public interface IValidationDiagnostic
{
    string Code { get; }
    string Message { get; }
    Guid? SubjectId { get; }
}

public interface IValidationRule
{
    string RuleCode { get; }
    Task<IValidationResult> EvaluateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default);
}

public interface IValidationRuleSet
{
    IReadOnlyList<IValidationRule> GetRulesFor(string kind);
    Task<IValidationResult> ValidateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default);
}

/// <summary>The only rule structurally enforced platform-wide today (WP8.2B Validation Contract Specification.md).</summary>
public static class StructuralValidationRules
{
    public const string NoSelfReference = "TEMPEST-VAL-001";
}

public interface IRecommendedValidationRule : IValidationRule
{
    string Rationale { get; }
}

public interface IReferenceIntegrityChecker
{
    Task<IValidationResult> CheckAsync(IEngineeringRelationship relationship, CancellationToken cancellationToken = default);
    Task<IValidationResult> CheckBaselineMembersAsync(IBaseline baseline, CancellationToken cancellationToken = default);
}
