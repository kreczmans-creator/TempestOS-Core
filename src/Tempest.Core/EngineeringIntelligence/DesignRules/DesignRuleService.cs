using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.DesignRules;

/// <summary>
/// Runs the mechanical design rule library against an engineering subject
/// (`WP02.3`).
/// </summary>
/// <remarks>
/// <para>
/// The rule model, the catalogue and the evaluator are `P02`'s shared
/// core, used identically by material selection and by decision trees.
/// What this service adds is the part specific to running a <em>set</em>
/// of rules: choosing which apply, resolving the constants they need once
/// rather than per rule, stating the scope of what ran, and attributing
/// the assessment.
/// </para>
/// <para>
/// <b>Released rules only, always.</b> There is no option to include
/// Draft rules in an assessment. A rule nobody has finished reviewing must
/// not reach an engineering conclusion, and offering a flag to allow it
/// would guarantee that flag ends up set somewhere. Unreleased rules that
/// would have applied are counted and reported, so their absence is
/// visible rather than silent.
/// </para>
/// </remarks>
public interface IDesignRuleService
{
    /// <summary>Assesses <paramref name="subject"/> against every released rule that applies, within <paramref name="scope"/>.</summary>
    /// <param name="subject">The subject to assess.</param>
    /// <param name="scope">Which rules to run. <see langword="null"/> runs every released rule that applies.</param>
    /// <param name="cancellationToken">Cancels the assessment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is <see langword="null"/>.</exception>
    Task<DesignRuleAssessment> AssessAsync(
        IAssessmentSubject subject,
        DesignRuleScope? scope = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-runs an assessment against the exact rule revisions a previous
    /// result pinned, so a historical conclusion can be reproduced.
    /// </summary>
    /// <remarks>
    /// The subject must be supplied at the revision the original used —
    /// read it back through its own catalogue's
    /// <c>GetRevisionAsync</c> first. This service pins rules, not
    /// subjects: which library the subject came from is the caller's
    /// knowledge, and a resolver spanning all seven would make `P02`
    /// depend on every one of them.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A pin names a library other than the rule library.</exception>
    Task<DesignRuleAssessment> ReproduceAsync(
        IAssessmentSubject subject,
        IReadOnlyList<ReferencePin> rulePins,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IDesignRuleService"/> implementation.</summary>
public sealed class DesignRuleService : IDesignRuleService
{
    /// <summary>Recorded as the assessor where no principal is established.</summary>
    public const string UnknownAssessorPrincipalId = "unknown";

    private readonly IRuleCatalog _rules;
    private readonly ICurrentPrincipalAccessor _principals;
    private readonly IReleasedConstantSource? _constants;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="DesignRuleService"/> class.</summary>
    /// <param name="rules">The rule library.</param>
    /// <param name="principals">The platform's own identity boundary, for attributing an assessment.</param>
    /// <param name="constants">The released-constant seam, for rules whose thresholds are constants. Optional.</param>
    /// <param name="timeProvider">The clock an assessment is stamped with. Defaults to the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> or <paramref name="principals"/> is <see langword="null"/>.</exception>
    public DesignRuleService(
        IRuleCatalog rules,
        ICurrentPrincipalAccessor principals,
        IReleasedConstantSource? constants = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(principals);

        _rules = rules;
        _principals = principals;
        _constants = constants;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<DesignRuleAssessment> AssessAsync(
        IAssessmentSubject subject,
        DesignRuleScope? scope = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        scope ??= DesignRuleScope.All;

        var applicable = await _rules.FindReleasedApplicableAsync(subject, cancellationToken).ConfigureAwait(false);
        var inScope = applicable.Where(record => IsInScope(record.Definition, scope)).ToList();

        // Applicable rules the library holds but has not released. Counted
        // so their absence is visible: guidance that exists and did not run
        // is a different thing from guidance that does not exist.
        var unreleased = await CountApplicableUnreleasedAsync(subject, scope, cancellationToken).ConfigureAwait(false);

        var evaluations = await EvaluateAsync(subject, inScope, cancellationToken).ConfigureAwait(false);

        var record = new AssessmentRecord(
            subject.SubjectId,
            subject.DisplayName,
            subject.Pin,
            evaluations,
            _time.GetUtcNow(),
            _principals.Current?.Identity.Id ?? UnknownAssessorPrincipalId);

        return new DesignRuleAssessment(
            record,
            new AssessmentScopeStatement(scope, applicable.Count, inScope.Count, unreleased));
    }

    /// <inheritdoc />
    public async Task<DesignRuleAssessment> ReproduceAsync(
        IAssessmentSubject subject,
        IReadOnlyList<ReferencePin> rulePins,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(rulePins);

        var rules = new List<IReferenceRecord<RuleDefinition>>(rulePins.Count);

        foreach (var pin in rulePins)
        {
            if (!string.Equals(pin.Library, _rules.LibraryName, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Pin {pin} names library '{pin.Library}', and this service can only reproduce rule pins. "
                    + "Read the subject back at its own pinned revision through its own catalogue before calling this.",
                    nameof(rulePins));

            // The rule as it stood, not as it is.
            rules.Add(await _rules.GetRevisionAsync(pin.RecordId, pin.RevisionNumber, cancellationToken).ConfigureAwait(false));
        }

        var evaluations = await EvaluateAsync(subject, rules, cancellationToken).ConfigureAwait(false);

        var record = new AssessmentRecord(
            subject.SubjectId,
            subject.DisplayName,
            subject.Pin,
            evaluations,
            _time.GetUtcNow(),
            _principals.Current?.Identity.Id ?? UnknownAssessorPrincipalId);

        return new DesignRuleAssessment(
            record,
            new AssessmentScopeStatement(
                new DesignRuleScope { RuleCodes = rules.Select(r => r.Definition.Code).ToList() },
                rules.Count,
                rules.Count,
                UnreleasedRuleCount: 0));
    }

    private async Task<IReadOnlyList<RuleEvaluation>> EvaluateAsync(
        IAssessmentSubject subject,
        IReadOnlyList<IReferenceRecord<RuleDefinition>> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0)
            return [];

        // Resolved once for the whole set rather than per rule: the same
        // constant compared against by two rules must be the same value at
        // the same revision, or the assessment contradicts itself.
        var constants = _constants is null
            ? ConstantResolutionSet.Empty
            : await ConstantResolutionSet
                .ResolveForAsync(rules.Select(r => r.Definition), _constants, cancellationToken)
                .ConfigureAwait(false);

        return rules
            .Select(rule => RuleEngine.Evaluate(
                rule.Definition,
                ReferencePin.For(_rules.LibraryName, rule),
                subject,
                constants))
            .ToList();
    }

    private async Task<int> CountApplicableUnreleasedAsync(
        IAssessmentSubject subject,
        DesignRuleScope scope,
        CancellationToken cancellationToken)
    {
        var all = await _rules.ListAsync(cancellationToken).ConfigureAwait(false);

        return all.Count(record =>
            record.ValidationState != ReferenceValidationState.Released
            && record.Definition.Applicability.DecideFor(subject) != ApplicabilityDecision.DoesNotApply
            && IsInScope(record.Definition, scope));
    }

    private static bool IsInScope(RuleDefinition rule, DesignRuleScope scope)
    {
        if (scope.RuleCodes.Count > 0
            && !scope.RuleCodes.Any(code => string.Equals(code, rule.Code, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (scope.Domains.Count > 0 && !scope.Domains.Contains(rule.Domain))
            return false;

        if (scope.Severities.Count > 0 && !scope.Severities.Contains(rule.Severity))
            return false;

        if (scope.SafetyCriticalOnly is { } safetyCritical && rule.IsSafetyCritical != safetyCritical)
            return false;

        return true;
    }
}
