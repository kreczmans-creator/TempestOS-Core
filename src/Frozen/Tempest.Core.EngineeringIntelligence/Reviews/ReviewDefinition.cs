using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence.Reviews;

/// <summary>What area of a design a review criterion is about.</summary>
/// <remarks>
/// Deliberately engineering-specific, and deliberately not a project
/// checklist. "Is the schedule on track" is a management question this
/// taxonomy has no home for, on purpose: a review that mixes engineering
/// criteria with project ones ends up satisfying neither audience.
/// </remarks>
public enum ReviewArea
{
    /// <summary>Not recorded.</summary>
    Unspecified,

    /// <summary>Whether the requirements are covered by the design.</summary>
    RequirementsCoverage,

    /// <summary>Whether the materials chosen suit the application.</summary>
    MaterialSuitability,

    /// <summary>Whether the design can be made by an available process.</summary>
    Manufacturability,

    /// <summary>Whether interfaces between parts and systems are defined and consistent.</summary>
    Interfaces,

    /// <summary>Whether fits, clearances and tolerances are specified and achievable.</summary>
    Tolerances,

    /// <summary>Whether the loads the design carries are established.</summary>
    Loads,

    /// <summary>Whether the service environment is established and accounted for.</summary>
    Environment,

    /// <summary>Whether safety-critical characteristics are identified and controlled.</summary>
    SafetyCritical,

    /// <summary>Whether the verification evidence exists.</summary>
    VerificationCoverage,

    /// <summary>Whether the documentation exists and is current.</summary>
    Documentation,

    /// <summary>Whether the known risks are recorded and dispositioned.</summary>
    Risk,

    /// <summary>Whether the decisions the design rests on are recorded.</summary>
    OutstandingDecisions,

    /// <summary>Whether the assumptions the design rests on are stated and resolved.</summary>
    Assumptions,

    /// <summary>An area this taxonomy does not classify.</summary>
    Other
}

/// <summary>
/// One thing a review checks.
/// </summary>
/// <remarks>
/// <para>
/// A criterion may be automated, manual, or both. Where it names a
/// <see cref="RuleCode"/>, running the review evaluates that rule and the
/// finding carries the evaluation. Where it does not, the criterion is
/// presented to a reviewer and the finding is
/// <see cref="AssessmentOutcome.EvidenceRequired"/> until a person records
/// one — which is honest, and is what stops an automated review quietly
/// reporting "all clear" on the questions no rule can answer.
/// </para>
/// <para>
/// <see cref="EvidenceExpected"/> says what would settle the criterion.
/// It is what turns a review from a list of questions into a list of
/// questions somebody can act on.
/// </para>
/// </remarks>
/// <param name="Code">The criterion's own identifier within its review. Required.</param>
/// <param name="Question">What is being checked, in plain engineering language. Required.</param>
/// <param name="Area">What area of the design it is about.</param>
/// <param name="Severity">How binding it is — what a failure means.</param>
/// <param name="RuleCode">The `P02` rule that answers this criterion automatically. <see langword="null"/> where a person must answer it.</param>
/// <param name="EvidenceExpected">What would settle the criterion. <see langword="null"/> if the question says it.</param>
/// <param name="Guidance">What a reviewer should look at. <see langword="null"/> if none.</param>
public sealed record ReviewCriterion(
    string Code,
    string Question,
    ReviewArea Area = ReviewArea.Unspecified,
    RuleSeverity Severity = RuleSeverity.Requirement,
    string? RuleCode = null,
    string? EvidenceExpected = null,
    string? Guidance = null)
{
    /// <summary>The criterion's own identifier within its review.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A review criterion must have a code, or its finding cannot be referred to.", nameof(Code))
        : Code.Trim();

    /// <summary>What is being checked.</summary>
    public string Question { get; } = string.IsNullOrWhiteSpace(Question)
        ? throw new ArgumentException("A review criterion must say what it checks.", nameof(Question))
        : Question.Trim();

    /// <summary>Whether a `P02` rule answers this criterion, or a person must.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsAutomated => !string.IsNullOrWhiteSpace(RuleCode);
}

/// <summary>
/// A structured engineering review: the criteria one kind of design review
/// checks, and on whose authority.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not the platform's own <c>IReview</c>.</b> That contract
/// (`Tempest.Core.EngineeringDomain`) is a lifecycle gate recording
/// <em>who</em> reviewed an object; this is <em>what was checked, against
/// what, with what evidence, and what was found</em>. They compose — a
/// completed engineering review is exactly the kind of thing an approval
/// gate should be satisfied by — and neither replaces the other.
/// </para>
/// <para>
/// A review definition is a governed record on `P01`'s shared layer, like
/// a rule and like a decision tree: authored, sourced, reviewed, released,
/// immutable once released, superseded rather than edited. A completed
/// review pins the definition's revision, so what a review checked in
/// March is still readable after the definition changed in June.
/// </para>
/// </remarks>
public sealed record ReviewDefinition
{
    /// <summary>The review's own engineering identifier, as an engineer would cite it. Required, and unique across the library.</summary>
    public required string Code { get; init; }

    /// <summary>A short name for the review. Required.</summary>
    public required string Name { get; init; }

    /// <summary>What this review is for, and when in a programme it is held. Required.</summary>
    public required string Purpose { get; init; }

    /// <summary>The criteria this review checks, in the order a reviewer works through them. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReviewCriterion> Criteria { get; init; } = [];

    /// <summary>The subject kind this review is written for. <see langword="null"/> where it is not tied to one.</summary>
    public string? SubjectKind { get; init; }

    /// <summary>The standards the review's authority derives from. Never <see langword="null"/>; empty if none.</summary>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>Why the review checks what it checks. <see langword="null"/> if not recorded.</summary>
    public string? Rationale { get; init; }

    /// <summary>The author's own classification wording, verbatim. <see langword="null"/> if none.</summary>
    public string? SourceClassification { get; init; }

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>The criteria a `P02` rule can answer.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReviewCriterion> AutomatedCriteria => Criteria.Where(c => c.IsAutomated).ToList();

    /// <summary>The criteria a person must answer.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<ReviewCriterion> ManualCriteria => Criteria.Where(c => !c.IsAutomated).ToList();

    /// <summary>The key review-code uniqueness is enforced on.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string CodeKey => CodeKeyFor(Code);

    /// <summary>Builds the uniqueness key from a code that is not (yet) a record — the lookup path.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    public static string CodeKeyFor(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return code.Trim().ToUpperInvariant();
    }
}
