namespace Tempest.Core.EngineeringIntelligence;

/// <summary>Whether a rule applies to a subject — and the third answer that matters.</summary>
public enum ApplicabilityDecision
{
    /// <summary>The rule applies and must be evaluated.</summary>
    Applies,

    /// <summary>The rule does not apply. Nothing is missing; there is nothing to assess.</summary>
    DoesNotApply,

    /// <summary>
    /// Whether the rule applies cannot be determined from what the subject
    /// records — typically because the rule is restricted by family and the
    /// subject's family is unrecorded or unclassified. Never silently read
    /// as either of the other two.
    /// </summary>
    Unknown
}

/// <summary>
/// When a rule applies.
/// </summary>
/// <remarks>
/// <para>
/// Applicability is part of a rule's meaning, not a filter bolted on
/// afterwards. A rule that applies to austenitic stainless steel says
/// nothing whatsoever about cast iron, and reporting it as "passed" for
/// cast iron would be as wrong as reporting it as "failed".
/// </para>
/// <para>
/// <b>An unknown answer is never resolved by guessing.</b> Where a rule is
/// restricted by family and the subject's family is unrecorded, or where
/// the subject's own library cannot say what applies to its family, the
/// decision is <see cref="ApplicabilityDecision.Unknown"/> and the rule
/// reports <see cref="AssessmentOutcome.Indeterminate"/>. Treating unknown
/// as "does not apply" would make a rule disappear from an assessment
/// precisely when the data is weakest.
/// </para>
/// <para>
/// <see cref="Conditions"/> holds applicability the structured fields
/// cannot express — a service environment, a loading regime, a size range
/// stated in prose. It is deliberately free text and is deliberately
/// <em>not</em> evaluated: a rule carrying one is reported as needing a
/// person to confirm the rule applies at all, which is honest, where
/// silently ignoring the text would not be.
/// </para>
/// </remarks>
/// <param name="SubjectKinds">The subject kinds this rule applies to, as the owning library names them. Never <see langword="null"/>; empty applies to any kind.</param>
/// <param name="Families">The families within those kinds this rule applies to. Never <see langword="null"/>; empty applies to any family.</param>
/// <param name="Conditions">Further applicability the structured fields cannot express, in the author's own words. <see langword="null"/> where the structured fields are the whole story.</param>
public sealed record RuleApplicability(
    IReadOnlyList<string>? SubjectKinds = null,
    IReadOnlyList<string>? Families = null,
    string? Conditions = null)
{
    /// <summary>A rule that applies to every subject, unconditionally.</summary>
    public static RuleApplicability Universal { get; } = new();

    /// <summary>The subject kinds this rule applies to. Empty applies to any kind.</summary>
    public IReadOnlyList<string> SubjectKinds { get; init; } = SubjectKinds ?? [];

    /// <summary>The families this rule applies to. Empty applies to any family.</summary>
    public IReadOnlyList<string> Families { get; init; } = Families ?? [];

    /// <summary>Whether this rule's applicability depends on something only a person can confirm.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NeedsHumanConfirmation => !string.IsNullOrWhiteSpace(Conditions);

    /// <summary>Whether this rule is restricted at all, or applies universally.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsUniversal => SubjectKinds.Count == 0 && Families.Count == 0 && !NeedsHumanConfirmation;

    /// <summary>Decides whether this rule applies to <paramref name="subject"/>.</summary>
    /// <remarks>
    /// A kind mismatch is a definite no: a rule written for fasteners does
    /// not apply to a material, and nothing about the subject could change
    /// that. A family mismatch is only a definite no when the subject
    /// actually records a family <em>and</em> its own library can classify
    /// that family; otherwise the answer is
    /// <see cref="ApplicabilityDecision.Unknown"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is <see langword="null"/>.</exception>
    public ApplicabilityDecision DecideFor(IAssessmentSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (SubjectKinds.Count > 0
            && !SubjectKinds.Any(kind => string.Equals(kind, subject.SubjectKind, StringComparison.OrdinalIgnoreCase)))
            return ApplicabilityDecision.DoesNotApply;

        if (Families.Count > 0)
        {
            if (subject.Family is not { } family || !subject.IsApplicabilityKnown)
                return ApplicabilityDecision.Unknown;

            if (!Families.Any(f => string.Equals(f, family, StringComparison.OrdinalIgnoreCase)))
                return ApplicabilityDecision.DoesNotApply;
        }

        return ApplicabilityDecision.Applies;
    }

    /// <summary>A short description, for explanations.</summary>
    public string Describe()
    {
        if (IsUniversal)
            return "applies to any subject";

        var parts = new List<string>();

        if (SubjectKinds.Count > 0)
            parts.Add($"subject kind in [{string.Join(", ", SubjectKinds)}]");

        if (Families.Count > 0)
            parts.Add($"family in [{string.Join(", ", Families)}]");

        if (NeedsHumanConfirmation)
            parts.Add($"and, to be confirmed by a person: {Conditions}");

        return string.Join(", ", parts);
    }
}
