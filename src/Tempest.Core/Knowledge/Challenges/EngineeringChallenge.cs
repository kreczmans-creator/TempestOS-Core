namespace Tempest.Core.Knowledge.Challenges;

/// <summary>What kind of thinking a challenge is meant to provoke.</summary>
public enum ChallengeKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>A problem with a determinable answer.</summary>
    Problem,

    /// <summary>"What if this changed?" — a perturbation of a known situation.</summary>
    WhatIf,

    /// <summary>Something has gone wrong; work out why.</summary>
    FailureInvestigation,

    /// <summary>An open design problem with no single right answer.</summary>
    DesignChallenge,

    /// <summary>A judgement call between defensible alternatives.</summary>
    TradeOff,

    /// <summary>Find what is wrong with this design, calculation or argument.</summary>
    Critique,

    /// <summary>An estimate from first principles with incomplete information.</summary>
    Estimation
}

/// <summary>How demanding a challenge is.</summary>
public enum ChallengeDifficulty
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>One step, one idea.</summary>
    Straightforward,

    /// <summary>Several steps, all of them standard.</summary>
    Moderate,

    /// <summary>Requires connecting ideas that are not obviously connected.</summary>
    Demanding,

    /// <summary>Requires judgement where the textbook runs out.</summary>
    Open
}

/// <summary>
/// What the challenge is testing the reader's ability to reason about.
/// </summary>
/// <remarks>
/// Deliberately <em>reasoning areas</em> rather than an answer key. An
/// open design challenge has no single right answer, and recording what
/// a good response would engage with is both more honest and more useful
/// than pretending otherwise (`ADR-0141`).
/// </remarks>
/// <param name="Reference">The area's own identifier within the challenge. Required.</param>
/// <param name="Description">What a good response engages with. Required.</param>
/// <param name="IsEssential">Whether a response ignoring this has missed the point.</param>
/// <param name="Hint">What to say to somebody who is stuck. <see langword="null"/> where nothing was written.</param>
public sealed record ReasoningArea(string Reference, string Description, bool IsEssential = false, string? Hint = null)
{
    /// <summary>The area's own identifier within the challenge.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A reasoning area must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What a good response engages with.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A reasoning area must say what a good response engages with.", nameof(Description))
        : Description.Trim();
}

/// <summary>
/// What a person marking the challenge should look for.
/// </summary>
/// <remarks>
/// <b>Guidance for a human marker, not a grading algorithm.</b> `F3`
/// holds no scoring, no rubric weights and no automated evaluation.
/// Judging an engineering response is engineering judgement, and a
/// numerical score attached to an open design problem would be a
/// judgement pretending to be a measurement (`ADR-0141`).
/// </remarks>
/// <param name="StrongResponseLooksLike">What a good answer does. Required.</param>
/// <param name="CommonMistakes">What people get wrong. Never <see langword="null"/>.</param>
/// <param name="AcceptableAlternatives">Different answers that are also right. Never <see langword="null"/>.</param>
/// <param name="ModelAnswer">A worked response, where one exists. <see langword="null"/> otherwise.</param>
/// <param name="WorkedExampleReference">The `F5` worked example holding the response. <see langword="null"/> where none does.</param>
public sealed record ChallengeGuidance(
    string StrongResponseLooksLike,
    IReadOnlyList<string>? CommonMistakes = null,
    IReadOnlyList<string>? AcceptableAlternatives = null,
    string? ModelAnswer = null,
    string? WorkedExampleReference = null)
{
    /// <summary>What a good answer does.</summary>
    public string StrongResponseLooksLike { get; } = string.IsNullOrWhiteSpace(StrongResponseLooksLike)
        ? throw new ArgumentException(
            "Challenge guidance must say what a strong response looks like. Guidance that cannot describe a good answer "
            + "cannot help anybody mark one.",
            nameof(StrongResponseLooksLike))
        : StrongResponseLooksLike.Trim();

    /// <summary>What people get wrong.</summary>
    public IReadOnlyList<string> CommonMistakes { get; init; } = CommonMistakes ?? [];

    /// <summary>Different answers that are also right.</summary>
    public IReadOnlyList<string> AcceptableAlternatives { get; init; } = AcceptableAlternatives ?? [];

    /// <summary>Whether the guidance admits that more than one answer is defensible.</summary>
    public bool AdmitsAlternatives => AcceptableAlternatives.Count > 0;
}

/// <summary>
/// An engineering question posed to make somebody think.
/// </summary>
/// <remarks>
/// <para>
/// The library holds the question, what a good answer engages with, and
/// guidance for whoever marks it. It holds no marker: `F3` has no
/// evaluator, no adaptive sequencing and no automated grading, and a
/// reflection test enforces it (`ADR-0141`).
/// </para>
/// <para>
/// A challenge is knowledge in its own right. "What happens to this
/// bracket if the load doubles?" is a question worth keeping, and worth
/// governing, whether or not anybody is currently being taught.
/// </para>
/// </remarks>
public sealed record EngineeringChallenge
{
    /// <summary>The reference the challenge is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What to call it. Required.</summary>
    public required string Title { get; init; }

    /// <summary>The situation the question is about. Required.</summary>
    public required string Scenario { get; init; }

    /// <summary>What is actually being asked. Required.</summary>
    public required string Question { get; init; }

    /// <summary>What kind of thinking it provokes.</summary>
    public ChallengeKind Kind { get; init; } = ChallengeKind.Unspecified;

    /// <summary>How demanding it is.</summary>
    public ChallengeDifficulty Difficulty { get; init; } = ChallengeDifficulty.Unspecified;

    /// <summary>What a good response engages with. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReasoningArea> ReasoningAreas { get; init; } = [];

    /// <summary>What the responder must work within. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Constraints { get; init; } = [];

    /// <summary>What the responder is told to assume. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> GivenAssumptions { get; init; } = [];

    /// <summary>
    /// What the responder is deliberately not told.
    /// </summary>
    /// <remarks>
    /// Recorded separately because it is often the whole point. A
    /// challenge that withholds the safety factor is testing whether the
    /// responder notices it is missing.
    /// </remarks>
    public IReadOnlyList<string> DeliberateOmissions { get; init; } = [];

    /// <summary>The Academy nodes a responder should have done first, by reference. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> PrerequisiteNodeReferences { get; init; } = [];

    /// <summary>What to read to prepare. Never <see langword="null"/>.</summary>
    public IReadOnlyList<KnowledgeCitation> ReferenceMaterial { get; init; } = [];

    /// <summary>What a person marking it should look for. <see langword="null"/> where nobody has written it.</summary>
    public ChallengeGuidance? Guidance { get; init; }

    /// <summary>Where it applies and who it is for.</summary>
    public KnowledgeApplicability Applicability { get; init; } = KnowledgeApplicability.Unrestricted;

    /// <summary>Where it came from and who has checked it.</summary>
    public KnowledgeProvenance Provenance { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The reasoning areas a response cannot ignore.</summary>
    public IReadOnlyList<ReasoningArea> EssentialReasoningAreas =>
        ReasoningAreas.Where(a => a.IsEssential).ToList();

    /// <summary>Whether anybody has written guidance for marking it.</summary>
    public bool IsMarkable => Guidance is not null;

    /// <summary>
    /// Whether the challenge admits more than one defensible answer.
    /// </summary>
    /// <remarks>
    /// True for the open kinds whatever the guidance says, because a
    /// design challenge with a single accepted answer is a problem
    /// mislabelled.
    /// </remarks>
    public bool IsOpenEnded =>
        Kind is ChallengeKind.DesignChallenge or ChallengeKind.TradeOff or ChallengeKind.Estimation
        || Difficulty == ChallengeDifficulty.Open
        || (Guidance?.AdmitsAlternatives ?? false);

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}
