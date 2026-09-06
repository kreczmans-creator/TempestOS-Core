using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.TradeStudies;

/// <summary>
/// One stated consideration in a trade study, and what kind of statement
/// it is.
/// </summary>
/// <remarks>
/// <para>
/// A consideration is authored by an engineer, in the engineer's own
/// words. <see cref="Condition"/> is optional and offers a second thing:
/// where a consideration <i>can</i> be settled from recorded reference
/// data, stating it as a rule condition lets the framework assess it
/// mechanically and pin the revision it read. Where it cannot — most
/// producibility, supply, maintainability and programme considerations —
/// the consideration still exists, still counts, and is answered by a
/// person supplying evidence.
/// </para>
/// <para>
/// What the framework never does is convert <see cref="Kind"/> into a
/// number. A requirement is not "a criterion with weight 10".
/// </para>
/// </remarks>
/// <param name="Code">The study-local identifier a judgement refers to. Required.</param>
/// <param name="Kind">Whether this eliminates, discriminates, or merely inclines.</param>
/// <param name="Statement">What the consideration says, in the engineer's own words. Required.</param>
/// <param name="Condition">How to settle it from recorded reference data, where that is possible. <see langword="null"/> where only a person can settle it.</param>
/// <param name="PropertyName">The subject property this consideration is about, where <see cref="Condition"/> is <see langword="null"/> but the property is still worth naming.</param>
/// <param name="Source">Where the consideration came from — a specification clause, a customer statement, a meeting. <see langword="null"/> if not recorded.</param>
/// <param name="Standard">The standard the consideration derives from, resolved against `A2`. <see langword="null"/> if none.</param>
/// <param name="EvidenceExpected">What would settle this consideration for an option, where a person must settle it. <see langword="null"/> if not stated.</param>
public sealed record TradeStudyConsideration(
    string Code,
    ConsiderationKind Kind,
    string Statement,
    RuleExpression? Condition = null,
    string? PropertyName = null,
    string? Source = null,
    StandardReference? Standard = null,
    string? EvidenceExpected = null)
{
    /// <summary>The study-local identifier a judgement refers to.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A trade-study consideration must have a code a judgement can refer to.", nameof(Code))
        : Code.Trim();

    /// <summary>What the consideration says, in the engineer's own words.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A trade-study consideration must say what it requires or compares.", nameof(Statement))
        : Statement.Trim();

    /// <summary>
    /// Whether an option violating this consideration is out of the study
    /// altogether, rather than merely worse than another.
    /// </summary>
    /// <remarks>
    /// Requirements and constraints eliminate. Criteria and preferences
    /// never do, however badly an option scores against them.
    /// </remarks>
    public bool IsEliminating => Kind is ConsiderationKind.Requirement or ConsiderationKind.Constraint;

    /// <summary>Whether the framework can assess this consideration itself, rather than needing a person.</summary>
    public bool IsAssessable => Condition is not null;

    /// <summary>The rule severity a failure against this consideration carries.</summary>
    /// <remarks>
    /// Mapped, not chosen: it exists so an eliminated option's evidence
    /// reads the same as a design-rule defect's, and so
    /// <see cref="RuleSeverities.OutcomeWhenNotSatisfied"/> decides one
    /// way for both. A preference is advisory because missing one is not
    /// a finding against the design.
    /// </remarks>
    public RuleSeverity Severity => Kind switch
    {
        ConsiderationKind.Requirement => RuleSeverity.Requirement,
        ConsiderationKind.Constraint => RuleSeverity.Constraint,
        ConsiderationKind.Criterion => RuleSeverity.Recommendation,
        ConsiderationKind.Preference => RuleSeverity.Advisory,
        _ => RuleSeverity.Unspecified
    };
}

/// <summary>
/// Something the study takes to be true without having established it.
/// </summary>
/// <remarks>
/// Assumptions are recorded separately from evidence and separately from
/// criteria, because they behave differently: an assumption that turns out
/// to be false invalidates the decision that rested on it, whatever the
/// criteria said. A study that records none is not a study without
/// assumptions — it is a study that has not written them down, and
/// validation says so.
/// </remarks>
/// <param name="Code">The study-local identifier. Required.</param>
/// <param name="Statement">What is being assumed. Required.</param>
/// <param name="Confidence">How much the study would stake on it.</param>
/// <param name="WouldInvalidate">What would no longer hold if the assumption were false. <see langword="null"/> if not stated.</param>
/// <param name="Owner">Who is to confirm it. <see langword="null"/> if nobody has been named.</param>
public sealed record TradeStudyAssumption(
    string Code,
    string Statement,
    AssumptionConfidence Confidence = AssumptionConfidence.Unspecified,
    string? WouldInvalidate = null,
    string? Owner = null)
{
    /// <summary>The study-local identifier.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A trade-study assumption must have a code.", nameof(Code))
        : Code.Trim();

    /// <summary>What is being assumed.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A trade-study assumption must say what is being assumed.", nameof(Statement))
        : Statement.Trim();

    /// <summary>Whether the decision cannot be relied upon until this assumption is confirmed.</summary>
    public bool IsLoadBearing => Confidence is AssumptionConfidence.Critical;
}

/// <summary>
/// Something that could go wrong with an option, or with the study.
/// </summary>
/// <remarks>
/// Deliberately not scored. A 3×4 "risk number" multiplied into a decision
/// matrix is the same flattening this framework refuses everywhere else:
/// it converts a described engineering concern into a digit and then
/// argues with the digit. What is recorded instead is what could happen,
/// to which option, what is being done about it, and — where it is being
/// carried — who accepted it.
/// </remarks>
/// <param name="Code">The study-local identifier. Required.</param>
/// <param name="Statement">What could go wrong. Required.</param>
/// <param name="Consequence">What the effect would be if it did. <see langword="null"/> if not stated.</param>
/// <param name="Standing">How the risk stands at the time of recording.</param>
/// <param name="Mitigation">What is being done about it. <see langword="null"/> if nothing is.</param>
/// <param name="AppliesToOptionCodes">The options this risk is specific to. Empty means it applies to the study as a whole.</param>
/// <param name="AcceptedByPrincipalId">Who accepted the risk, where <see cref="Standing"/> is <see cref="RiskStanding.Accepted"/>.</param>
public sealed record TradeStudyRisk(
    string Code,
    string Statement,
    string? Consequence = null,
    RiskStanding Standing = RiskStanding.Unspecified,
    string? Mitigation = null,
    IReadOnlyList<string>? AppliesToOptionCodes = null,
    string? AcceptedByPrincipalId = null)
{
    /// <summary>The study-local identifier.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A trade-study risk must have a code.", nameof(Code))
        : Code.Trim();

    /// <summary>What could go wrong.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A trade-study risk must say what could go wrong.", nameof(Statement))
        : Statement.Trim();

    /// <summary>The options this risk is specific to. Empty means the study as a whole.</summary>
    public IReadOnlyList<string> AppliesToOptionCodes { get; init; } = AppliesToOptionCodes ?? [];

    /// <summary>Whether the risk is still live at the time of recording.</summary>
    public bool IsOutstanding => Standing is RiskStanding.Unspecified or RiskStanding.Open;

    /// <summary>Whether this risk applies to the option registered under <paramref name="optionCode"/>.</summary>
    public bool AppliesTo(string optionCode) =>
        AppliesToOptionCodes.Count == 0
        || AppliesToOptionCodes.Any(code => string.Equals(code, optionCode, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The question a trade study is answering, and everything that bears on
/// the answer.
/// </summary>
/// <remarks>
/// <para>
/// The definition is a governed reference-data record like any other in
/// TempestOS: authored, sourced, reviewed, released and revisioned through
/// the shared reference-data lifecycle. A study whose question changed
/// after the decision was taken must supersede rather than mutate, and the
/// shared lifecycle is what makes that so.
/// </para>
/// <para>
/// The definition holds the <i>question</i>. It does not hold the answer:
/// options, assessments and the decision are separate, because the same
/// question can legitimately be re-asked against a different option set or
/// at a different reference-data revision.
/// </para>
/// </remarks>
public sealed record TradeStudyDefinition
{
    /// <summary>The identifier this study is known by. Required.</summary>
    public required string Code { get; init; }

    /// <summary>What the study is called. Required.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The engineering question being answered, stated so that a reader
    /// two years later understands what was actually being decided.
    /// Required.
    /// </summary>
    public required string Problem { get; init; }

    /// <summary>What a good answer would achieve. <see langword="null"/> if not separately stated.</summary>
    public string? Objective { get; init; }

    /// <summary>The kind of subject the options are, where they are reference-data records. <see langword="null"/> where they are not.</summary>
    /// <remarks>
    /// One of <see cref="AssessmentSubjectKinds"/>. A study comparing
    /// materials names <c>Material</c> and its options are `A1` records; a
    /// study comparing architectures names nothing and its options are
    /// described rather than referenced. Both are legitimate.
    /// </remarks>
    public string? SubjectKind { get; init; }

    /// <summary>Everything that bears on the answer. Never <see langword="null"/>.</summary>
    public IReadOnlyList<TradeStudyConsideration> Considerations { get; init; } = [];

    /// <summary>What the study takes to be true without having established it. Never <see langword="null"/>.</summary>
    public IReadOnlyList<TradeStudyAssumption> Assumptions { get; init; } = [];

    /// <summary>What could go wrong. Never <see langword="null"/>.</summary>
    public IReadOnlyList<TradeStudyRisk> Risks { get; init; } = [];

    /// <summary>Standards the study as a whole works under. Never <see langword="null"/>.</summary>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>Why the study is framed as it is. <see langword="null"/> if not recorded.</summary>
    public string? Rationale { get; init; }

    /// <summary>Considerations an option must satisfy to remain admissible.</summary>
    public IEnumerable<TradeStudyConsideration> EliminatingConsiderations =>
        Considerations.Where(c => c.IsEliminating);

    /// <summary>Considerations on which admissible options are compared.</summary>
    public IEnumerable<TradeStudyConsideration> DiscriminatingConsiderations =>
        Considerations.Where(c => c.Kind is ConsiderationKind.Criterion);

    /// <summary>The case-insensitive key <see cref="Code"/> is indexed under.</summary>
    public string CodeKey => CodeKeyFor(Code);

    /// <summary>The case-insensitive key <paramref name="code"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    public static string CodeKeyFor(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return code.Trim().ToUpperInvariant();
    }

    /// <summary>Returns the consideration registered under <paramref name="code"/>, or <see langword="null"/> if none is.</summary>
    public TradeStudyConsideration? FindConsideration(string code) =>
        Considerations.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
}
