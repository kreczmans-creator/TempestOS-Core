using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// One engineering rule, as authored: what it states, when it applies, how
/// binding it is, and on whose authority.
/// </summary>
/// <remarks>
/// <para>
/// <b>A rule definition is a governed, authored record — so it is held the
/// way `P01` holds one.</b> A rule is written by somebody, from a source,
/// reviewed by somebody else, released, and eventually superseded by a
/// revised rule. That is precisely the shape
/// <see cref="ReferenceDataCatalog{TDefinition}"/> already provides, so
/// `P02` derives from it rather than growing a second lifecycle, a second
/// provenance model and a second revision mechanism (`ADR-0128`).
/// </para>
/// <para>
/// The consequences are the ones the programme needs and gets for free:
/// a rule cannot leave Draft without a named source, cannot be Released
/// without a named reviewer who verified it, is immutable once Released,
/// is superseded rather than edited, and every past revision stays
/// readable — which is what makes a historical assessment reconstructable.
/// </para>
/// <para>
/// <b>Definition, not execution.</b> Nothing here records a result. What
/// happened when this rule was run against a subject is a
/// <see cref="RuleEvaluation"/>, produced fresh each time and never stored
/// in this catalogue.
/// </para>
/// <para>
/// Carries no TempestOS identity, no provenance, no validation state and
/// no revision number: those belong to the registered record
/// (<see cref="IReferenceRecord{TDefinition}"/>), because they are
/// catalogue governance rather than engineering content.
/// </para>
/// </remarks>
public sealed record RuleDefinition
{
    /// <summary>
    /// The rule's own engineering identifier, as an engineer would cite it
    /// (e.g. <c>"FST-TORQUE-01"</c>). Required, and unique across the
    /// library.
    /// </summary>
    /// <remarks>
    /// Distinct from the TempestOS record Id, which is caller-assigned and
    /// never derived from this. A rule keeps its code across revisions;
    /// that is what makes "rule FST-TORQUE-01, revision 2" meaningful.
    /// </remarks>
    public required string Code { get; init; }

    /// <summary>A short name for the rule. Required.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// What the rule states, in plain engineering language — the sentence
    /// an engineer would read. Required, and never a restatement of the
    /// condition's syntax.
    /// </summary>
    public required string Statement { get; init; }

    /// <summary>How binding the rule is. This decides what failing it means.</summary>
    public RuleSeverity Severity { get; init; } = RuleSeverity.Unspecified;

    /// <summary>The area of engineering the rule belongs to, for retrieval.</summary>
    public RuleDomain Domain { get; init; } = RuleDomain.Unspecified;

    /// <summary>When the rule applies. Never <see langword="null"/>; defaults to universal.</summary>
    public RuleApplicability Applicability { get; init; } = RuleApplicability.Universal;

    /// <summary>
    /// The condition the rule tests. <see langword="null"/> where the rule
    /// has been authored but its condition has not yet been expressed —
    /// which validation reports, and which prevents the rule reaching
    /// Validated.
    /// </summary>
    public RuleExpression? Condition { get; init; }

    /// <summary>
    /// Why the rule exists — the engineering reasoning behind it, not a
    /// restatement of what it checks. <see langword="null"/> if not
    /// recorded, which validation reports: a rule nobody can justify is a
    /// rule nobody can later revise with confidence.
    /// </summary>
    public string? Rationale { get; init; }

    /// <summary>
    /// What consequence the rule guards against — what goes wrong if it is
    /// not followed. <see langword="null"/> if not recorded.
    /// </summary>
    public string? Consequence { get; init; }

    /// <summary>
    /// The standards this rule's authority derives from. Never
    /// <see langword="null"/>; empty if the rule rests on something other
    /// than a standard.
    /// </summary>
    /// <remarks>
    /// <b>Citing a standard is not claiming compliance with it.</b> This
    /// records where the rule's author says the rule came from. Nothing
    /// here, and nothing produced by evaluating this rule, asserts that
    /// any design conforms to any standard.
    /// </remarks>
    public IReadOnlyList<StandardReference> Standards { get; init; } = [];

    /// <summary>
    /// Whether this rule concerns a safety-critical characteristic —
    /// structural integrity, pressure, lifting, or a life-critical or
    /// regulated application.
    /// </summary>
    /// <remarks>
    /// A declaration by the rule's author, never inferred. Validation
    /// holds a safety-critical rule to a higher bar: it must name its
    /// authority and it must state a consequence. Passing such a rule
    /// still certifies nothing — see <see cref="RuleEvaluation"/>.
    /// </remarks>
    public bool IsSafetyCritical { get; init; }

    /// <summary>
    /// Whether a person must review this rule's result even when it
    /// passes. Set by the author for a rule whose conclusion is not safe
    /// to act on unattended.
    /// </summary>
    public bool RequiresHumanReview { get; init; }

    /// <summary>
    /// The author's own classification wording, verbatim — the honest home
    /// for a domain this taxonomy classifies as
    /// <see cref="RuleDomain.Other"/>. <see langword="null"/> if none was
    /// given.
    /// </summary>
    public string? SourceClassification { get; init; }

    /// <summary>Free-text notes not captured by any other field. <see langword="null"/> if none.</summary>
    public string? Notes { get; init; }

    /// <summary>The date from which this rule is effective, where one is stated. <see langword="null"/> otherwise.</summary>
    public DateOnly? EffectiveDate { get; init; }

    /// <summary>Whether the rule is complete enough to be evaluated at all.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsExecutable => Condition is not null;

    /// <summary>The key rule-code uniqueness is enforced on. Case-insensitive, so one code is one rule however it was typed.</summary>
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
