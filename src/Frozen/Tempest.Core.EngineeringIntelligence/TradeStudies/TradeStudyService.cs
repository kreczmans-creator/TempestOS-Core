using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.TradeStudies;

/// <summary>Thrown when a trade study is run against a definition that has not been released.</summary>
public sealed class UnreleasedTradeStudyException : ReferenceDataException
{
    /// <summary>Initialises a new instance of the <see cref="UnreleasedTradeStudyException"/> class.</summary>
    /// <param name="studyCode">The study whose definition is not released.</param>
    /// <param name="state">The state the definition is actually in.</param>
    public UnreleasedTradeStudyException(string studyCode, ReferenceValidationState state)
        : base(
            "EngineeringTradeStudies",
            $"Trade study '{studyCode}' is {state}, not Released. A decision must not rest on a study nobody has finished "
            + "reviewing, so it is refused rather than run.")
    {
        StudyCode = studyCode;
        State = state;
    }

    /// <summary>The study whose definition is not released.</summary>
    public string StudyCode { get; }

    /// <summary>The state the definition is actually in.</summary>
    public ReferenceValidationState State { get; }
}

/// <summary>
/// Runs a trade study: assesses each option against each consideration
/// that can be assessed, and reports everything that cannot.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is deliberately no method here that chooses an option.</b> The
/// service narrows and evidences; <see cref="TradeStudyDecision"/> is
/// constructed by a caller acting for a named person, and
/// <see cref="RecordDecision"/> only attaches what that person decided.
/// No overload of anything in this interface returns a recommendation, a
/// ranking or a score, and none should be added.
/// </para>
/// </remarks>
public interface ITradeStudyService
{
    /// <summary>Runs the study registered under <paramref name="studyCode"/> against <paramref name="candidates"/>.</summary>
    /// <param name="studyCode">The released study to run.</param>
    /// <param name="candidates">The options being compared, each with its reference-data record where it has one.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <returns>The study as carried out, with no decision attached.</returns>
    /// <exception cref="ArgumentException"><paramref name="studyCode"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="candidates"/> is <see langword="null"/>.</exception>
    /// <exception cref="ReferenceRecordNotFoundException">No study is registered under <paramref name="studyCode"/>.</exception>
    /// <exception cref="UnreleasedTradeStudyException">The study is registered but not Released.</exception>
    Task<TradeStudyRecord> RunAsync(
        string studyCode,
        IReadOnlyList<TradeStudyCandidate> candidates,
        CancellationToken cancellationToken = default);

    /// <summary>Re-runs the study at the exact definition revision <paramref name="definitionPin"/> names.</summary>
    /// <param name="definitionPin">The study-definition revision to reproduce.</param>
    /// <param name="candidates">The options being compared.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <returns>The study as it would have been carried out at that revision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definitionPin"/> or <paramref name="candidates"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="definitionPin"/> does not name the trade-study library.</exception>
    Task<TradeStudyRecord> ReproduceAsync(
        ReferencePin definitionPin,
        IReadOnlyList<TradeStudyCandidate> candidates,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces one assessed judgement with an engineer's own.</summary>
    /// <param name="study">The study record to revise.</param>
    /// <param name="optionCode">The option the judgement is about.</param>
    /// <param name="considerationCode">The consideration the judgement is against.</param>
    /// <param name="outcome">What the engineer concluded.</param>
    /// <param name="reason">Why.</param>
    /// <param name="comparison">How this option stands against the others on this consideration, in the engineer's own words.</param>
    /// <param name="evidence">What supports the judgement.</param>
    /// <returns>A revised study record. The original is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="study"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The study has no such option or consideration, or <paramref name="reason"/> is blank.</exception>
    TradeStudyRecord RecordJudgement(
        TradeStudyRecord study,
        string optionCode,
        string considerationCode,
        AssessmentOutcome outcome,
        string reason,
        string? comparison = null,
        IReadOnlyList<EvidenceReference>? evidence = null);

    /// <summary>Attaches a person's decision to a study.</summary>
    /// <remarks>
    /// The decision is supplied, never computed. This method records it,
    /// checks that it refers to an option the study actually assessed, and
    /// changes nothing else.
    /// </remarks>
    /// <param name="study">The study the decision concludes.</param>
    /// <param name="decision">What the person decided, and why.</param>
    /// <returns>The study with the decision attached. The original is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="study"/> or <paramref name="decision"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The decision selects an option the study did not assess.</exception>
    /// <exception cref="InvalidOperationException">The study already carries a decision.</exception>
    TradeStudyRecord RecordDecision(TradeStudyRecord study, TradeStudyDecision decision);
}

/// <summary>The concrete <see cref="ITradeStudyService"/> implementation.</summary>
/// <remarks>
/// <para>
/// Considerations carrying a condition are evaluated through the same
/// <see cref="RuleEngine"/> as design rules, so a missing property means
/// the same thing in a trade study as it does in a rule assessment:
/// <see cref="AssessmentOutcome.NotRecorded"/>, never a pass.
/// Considerations without a condition — and options with no
/// reference-data record behind them — become
/// <see cref="AssessmentOutcome.EvidenceRequired"/> judgements naming what
/// would settle them.
/// </para>
/// <para>
/// Every option is assessed and reported, eliminated ones included, so an
/// engineer can tell "not considered" from "considered and ruled out for
/// this reason".
/// </para>
/// </remarks>
public sealed class TradeStudyService : ITradeStudyService
{
    /// <summary>The principal id recorded when no principal is available.</summary>
    public const string UnknownAssessorPrincipalId = "unknown";

    private readonly ITradeStudyCatalog _studies;
    private readonly IReleasedConstantSource? _constants;
    private readonly ICurrentPrincipalAccessor _principals;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="TradeStudyService"/> class.</summary>
    /// <param name="studies">The trade-study library definitions are read from.</param>
    /// <param name="principals">Supplies the principal recorded against an assessment.</param>
    /// <param name="constants">The `A6` source a consideration's symbolic threshold resolves against. Optional.</param>
    /// <param name="timeProvider">The clock the assessment time is read from. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public TradeStudyService(
        ITradeStudyCatalog studies,
        ICurrentPrincipalAccessor principals,
        IReleasedConstantSource? constants = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(studies);
        ArgumentNullException.ThrowIfNull(principals);

        _studies = studies;
        _principals = principals;
        _constants = constants;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<TradeStudyRecord> RunAsync(
        string studyCode,
        IReadOnlyList<TradeStudyCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studyCode);
        ArgumentNullException.ThrowIfNull(candidates);

        var record = await _studies.FindByCodeAsync(studyCode, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(_studies.LibraryName, studyCode);

        if (record.ValidationState != ReferenceValidationState.Released)
            throw new UnreleasedTradeStudyException(record.Definition.Code, record.ValidationState);

        return await RunRecordAsync(record, candidates, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TradeStudyRecord> ReproduceAsync(
        ReferencePin definitionPin,
        IReadOnlyList<TradeStudyCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitionPin);
        ArgumentNullException.ThrowIfNull(candidates);

        if (!string.Equals(definitionPin.Library, _studies.LibraryName, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Pin {definitionPin} names library '{definitionPin.Library}', and this service can only reproduce trade-study pins.",
                nameof(definitionPin));

        var record = await _studies
            .GetRevisionAsync(definitionPin.RecordId, definitionPin.RevisionNumber, cancellationToken)
            .ConfigureAwait(false);

        return await RunRecordAsync(record, candidates, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public TradeStudyRecord RecordJudgement(
        TradeStudyRecord study,
        string optionCode,
        string considerationCode,
        AssessmentOutcome outcome,
        string reason,
        string? comparison = null,
        IReadOnlyList<EvidenceReference>? evidence = null)
    {
        ArgumentNullException.ThrowIfNull(study);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(considerationCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var option = study.FindOption(optionCode)
            ?? throw new ArgumentException($"This study did not assess option '{optionCode}'.", nameof(optionCode));

        var existing = option.FindJudgement(considerationCode)
            ?? throw new ArgumentException(
                $"Option '{optionCode}' has no judgement against consideration '{considerationCode}'.",
                nameof(considerationCode));

        var supporting = new List<EvidenceReference>(evidence ?? []);

        // What the assessment said is kept as evidence rather than
        // discarded. An engineer overruling a rule is legitimate; doing so
        // without the record showing what the rule said is not.
        if (existing.Evaluation is { } evaluation)
            supporting.Add(new EvidenceReference(
                EvidenceKind.Other,
                $"Superseded by an engineer's own judgement. Rule {evaluation.RuleCode} ({evaluation.RulePin}) reported "
                + $"{evaluation.Outcome}: {evaluation.Reason}",
                evaluation.RulePin));

        var revised = existing with
        {
            Outcome = outcome,
            Source = JudgementSource.Judged,
            Reason = reason.Trim(),
            Comparison = comparison?.Trim() ?? existing.Comparison,
            Evaluation = null,
            Evidence = supporting,
            JudgedByPrincipalId = _principals.Current?.Identity.Id ?? UnknownAssessorPrincipalId,
        };

        var judgements = option.Judgements
            .Select(j => ReferenceEquals(j, existing) ? revised : j)
            .ToList();

        var options = study.Options
            .Select(o => ReferenceEquals(o, option) ? o with { Judgements = judgements } : o)
            .ToList();

        return study with { Options = options };
    }

    /// <inheritdoc />
    public TradeStudyRecord RecordDecision(TradeStudyRecord study, TradeStudyDecision decision)
    {
        ArgumentNullException.ThrowIfNull(study);
        ArgumentNullException.ThrowIfNull(decision);

        if (study.Decision is not null)
            throw new InvalidOperationException(
                $"Trade study '{study.StudyCode}' already records a decision by '{study.Decision.DecidedByPrincipalId}'. "
                + "A decision is not overwritten: re-run the study and decide again, so both decisions stay on the record.");

        if (study.FindOption(decision.SelectedOptionCode) is null)
            throw new ArgumentException(
                $"The decision selects option '{decision.SelectedOptionCode}', which this study did not assess.",
                nameof(decision));

        return study with { Decision = decision };
    }

    private async Task<TradeStudyRecord> RunRecordAsync(
        IReferenceRecord<TradeStudyDefinition> record,
        IReadOnlyList<TradeStudyCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var definition = record.Definition;

        var duplicates = candidates
            .GroupBy(c => c.Option.Code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new ArgumentException(
                $"Option code(s) {string.Join(", ", duplicates)} were offered more than once, so a judgement could not be tied to one of them.",
                nameof(candidates));

        var constants = _constants is null
            ? ConstantResolutionSet.Empty
            : await ResolveConstantsAsync(definition, cancellationToken).ConfigureAwait(false);

        var results = candidates
            .Select(candidate => AssessCandidate(definition, candidate, constants))
            .ToList();

        return new TradeStudyRecord(
            definition.Code,
            ReferencePin.For(_studies.LibraryName, record),
            results,
            _time.GetUtcNow(),
            _principals.Current?.Identity.Id ?? UnknownAssessorPrincipalId);
    }

    private Task<ConstantResolutionSet> ResolveConstantsAsync(
        TradeStudyDefinition definition,
        CancellationToken cancellationToken)
    {
        // Considerations reach the constant source through the same probe
        // rules they will be evaluated as, so a symbolic threshold means
        // exactly what it means in a released rule.
        var probes = definition.Considerations
            .Where(c => c.IsAssessable)
            .Select(ProbeFor)
            .ToList();

        return ConstantResolutionSet.ResolveForAsync(probes, _constants!, cancellationToken);
    }

    private static TradeStudyOptionResult AssessCandidate(
        TradeStudyDefinition definition,
        TradeStudyCandidate candidate,
        ConstantResolutionSet constants)
    {
        var judgements = definition.Considerations
            .Select(consideration => Judge(consideration, candidate, constants))
            .ToList();

        return new TradeStudyOptionResult(candidate.Option, judgements, candidate.Subject?.Pin);
    }

    private static TradeStudyJudgement Judge(
        TradeStudyConsideration consideration,
        TradeStudyCandidate candidate,
        ConstantResolutionSet constants)
    {
        if (!consideration.IsAssessable || candidate.Subject is null)
            return Outstanding(consideration, candidate);

        var evaluation = RuleEngine.Evaluate(
            ProbeFor(consideration),
            new ReferencePin("TradeStudyConsiderations", consideration.Code, 1),
            candidate.Subject,
            constants);

        return new TradeStudyJudgement(
            candidate.Option.Code,
            consideration.Code,
            consideration.Kind,
            evaluation.Outcome,
            JudgementSource.Assessed,
            evaluation.ConditionResult?.Reason ?? evaluation.Reason,
            Evaluation: evaluation,
            Evidence: evaluation.Evidence);
    }

    private static TradeStudyJudgement Outstanding(
        TradeStudyConsideration consideration,
        TradeStudyCandidate candidate)
    {
        var reason = consideration.IsAssessable
            ? $"Option '{candidate.Option.Code}' has no reference-data record behind it, so '{consideration.Statement}' cannot be "
              + "assessed from recorded data and needs an engineer's judgement."
            : consideration.EvidenceExpected is { } expected
                ? $"'{consideration.Statement}' cannot be settled from recorded data. What would settle it: {expected}"
                : $"'{consideration.Statement}' cannot be settled from recorded data and the study does not say what would settle it.";

        return new TradeStudyJudgement(
            candidate.Option.Code,
            consideration.Code,
            consideration.Kind,
            AssessmentOutcome.EvidenceRequired,
            JudgementSource.Outstanding,
            reason);
    }

    private static RuleDefinition ProbeFor(TradeStudyConsideration consideration) => new()
    {
        Code = consideration.Code,
        Name = consideration.Statement,
        Statement = consideration.Statement,
        Severity = consideration.Severity,
        Condition = consideration.Condition,
        Standards = consideration.Standard is null ? [] : [consideration.Standard],
    };
}
