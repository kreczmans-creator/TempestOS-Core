namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// <see cref="StructuralValidationRules.NoSelfReference"/> is the only rule structurally enforced platform-wide
/// today (WP8.2B Validation Contract Specification.md) — and it is enforced directly, in
/// <see cref="EngineeringObjectBase.LinkAsync"/>, not through this registry. No Kind-specific
/// <see cref="IValidationRule"/> is registered by this reference implementation, so
/// <see cref="GetRulesFor"/> returns an empty list and <see cref="ValidateAsync"/> always reports valid —
/// a future discipline module registers its own rules here without this type changing shape.
/// </summary>
public sealed class ValidationRuleSet : IValidationRuleSet
{
    private readonly List<IValidationRule> _rules = new();

    public void Register(IValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
    }

    public IReadOnlyList<IValidationRule> GetRulesFor(string kind) => _rules;

    public async Task<IValidationResult> ValidateAsync(IEngineeringObject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (_rules.Count == 0)
            return ValidationResult.Valid;

        var errors = new List<IValidationDiagnostic>();
        var warnings = new List<IValidationDiagnostic>();

        foreach (var rule in _rules)
        {
            var result = await rule.EvaluateAsync(subject, cancellationToken).ConfigureAwait(false);
            errors.AddRange(result.Errors);
            warnings.AddRange(result.Warnings);
        }

        return new ValidationResult(errors, warnings);
    }
}
