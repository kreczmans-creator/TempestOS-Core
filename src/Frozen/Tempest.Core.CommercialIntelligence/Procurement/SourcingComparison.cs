using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Procurement;

/// <summary>How much weight a recommendation can carry.</summary>
/// <remarks>
/// Not a confidence percentage. The strength of a recommendation is
/// decided by how much of the comparison was actually established, and
/// saying "62% confident" would dress that up as a measurement.
/// </remarks>
public enum RecommendationStrength
{
    /// <summary>Too little is established for the comparison to say anything.</summary>
    Insufficient,

    /// <summary>A candidate leads, but on information with material gaps in it.</summary>
    Provisional,

    /// <summary>A candidate leads on a complete comparison, but not by much.</summary>
    Marginal,

    /// <summary>A candidate leads clearly on a complete comparison.</summary>
    Clear
}

/// <summary>Where a person has taken a comparison.</summary>
/// <remarks>
/// A second axis from <see cref="ReferenceValidationState"/> and from
/// <see cref="RecommendationStrength"/>. The comparison's own quality is
/// one thing; whether anybody has acted on it is another, and TempestOS
/// only ever records the second, never performs it.
/// </remarks>
public enum SourcingDecisionState
{
    /// <summary>Nobody has looked at it yet.</summary>
    AwaitingHumanDecision,

    /// <summary>Somebody is looking, and has asked for more information first.</summary>
    MoreInformationRequested,

    /// <summary>A person accepted the recommendation. What they then did about it is outside TempestOS.</summary>
    RecommendationAccepted,

    /// <summary>A person chose a different candidate, and said why.</summary>
    AlternativeChosen,

    /// <summary>A person decided to source none of the candidates.</summary>
    NoneChosen,

    /// <summary>Overtaken by events; nobody will decide it now.</summary>
    Abandoned
}

/// <summary>How a candidate came out of the comparison.</summary>
/// <param name="CandidateCode">The candidate ranked.</param>
/// <param name="Score">The weighted score, from 0 to 1. <see langword="null"/> where too little was established to score it.</param>
/// <param name="EstablishedWeight">The share of the total weight actually established for this candidate, from 0 to 1.</param>
/// <param name="MissingCriterionCodes">Criteria nobody established for this candidate. Never <see langword="null"/>.</param>
/// <param name="Commentary">What the ranking turns on, in plain words. <see langword="null"/> where nothing was written.</param>
public sealed record CandidateRanking(
    string CandidateCode,
    decimal? Score,
    decimal EstablishedWeight,
    IReadOnlyList<string> MissingCriterionCodes,
    string? Commentary = null)
{
    /// <summary>Criteria nobody established for this candidate.</summary>
    public IReadOnlyList<string> MissingCriterionCodes { get; } = MissingCriterionCodes ?? [];

    /// <summary>Whether the candidate could be scored at all.</summary>
    public bool IsScored => Score is not null;

    /// <summary>Whether every scoring criterion was established for this candidate.</summary>
    public bool IsComplete => MissingCriterionCodes.Count == 0;
}

/// <summary>
/// What the comparison found, and what a person did about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>TempestOS recommends; it does not procure.</b> A comparison ranks
/// candidates, states what it could not establish, and says which
/// candidate leads. It does not place an order, award business, approve a
/// supplier, qualify a supplier or commit a penny. Those are acts of
/// procurement authority the platform does not hold, and there is
/// deliberately no method on this type or any other in `D5` that performs
/// one (`ADR-0135`).
/// </para>
/// <para>
/// <see cref="DecidedBy"/> records that a person acted, who they are, and
/// on what authority. It is a record of a human decision, never a
/// substitute for one — and <see cref="SourcingDecisionState.AlternativeChosen"/>
/// exists precisely so that disagreeing with the recommendation is a
/// first-class outcome rather than an anomaly.
/// </para>
/// </remarks>
public sealed record SourcingComparison
{
    /// <summary>The reference the comparison is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>The requirement being sourced against. Required.</summary>
    public required string RequirementReference { get; init; }

    /// <summary>The requirement at the revision it was read. <see langword="null"/> where unpinned.</summary>
    public ReferencePin? RequirementPin { get; init; }

    /// <summary>The candidates considered, in contention or not. Never <see langword="null"/>.</summary>
    public IReadOnlyList<SourcingCandidate> Candidates { get; init; } = [];

    /// <summary>How each candidate in contention came out, best first. Never <see langword="null"/>.</summary>
    public IReadOnlyList<CandidateRanking> Rankings { get; init; } = [];

    /// <summary>The candidate the comparison recommends. <see langword="null"/> where it recommends none.</summary>
    public string? RecommendedCandidateCode { get; init; }

    /// <summary>Why, in plain words. <see langword="null"/> where nothing was written.</summary>
    public string? RecommendationRationale { get; init; }

    /// <summary>How much weight the recommendation can carry.</summary>
    public RecommendationStrength Strength { get; init; } = RecommendationStrength.Insufficient;

    /// <summary>What the comparison could not establish, across all candidates. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> OutstandingQuestions { get; init; } = [];

    /// <summary>Where a person has taken it.</summary>
    public SourcingDecisionState DecisionState { get; init; } = SourcingDecisionState.AwaitingHumanDecision;

    /// <summary>The candidate a person actually chose. <see langword="null"/> until one does.</summary>
    public string? ChosenCandidateCode { get; init; }

    /// <summary>Why they chose it, particularly where it differs from the recommendation. <see langword="null"/> where nothing was written.</summary>
    public string? DecisionRationale { get; init; }

    /// <summary>The person who decided, and the authority they held. <see langword="null"/> until somebody does.</summary>
    public BusinessAuthorisation? DecidedBy { get; init; }

    /// <summary>When the comparison was prepared. <see langword="null"/> where unrecorded.</summary>
    public DateOnly? PreparedOn { get; init; }

    /// <summary>Who prepared it. <see langword="null"/> where unrecorded.</summary>
    public string? PreparedByPrincipalId { get; init; }

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Always <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// A property rather than a constant so it reads at every call site,
    /// and unconditional because there is no comparison `D5` can produce
    /// that does not need a person to act on it. Mirrors `P02`'s
    /// <c>MaterialAssessment.RequiresHumanDecision</c>, for the same
    /// reason.
    /// </remarks>
    public bool RequiresHumanDecision => true;

    /// <summary>Whether a person has recorded a decision.</summary>
    public bool HasBeenDecided => DecisionState
        is SourcingDecisionState.RecommendationAccepted
        or SourcingDecisionState.AlternativeChosen
        or SourcingDecisionState.NoneChosen;

    /// <summary>Whether the person who decided went against the recommendation.</summary>
    public bool DepartsFromRecommendation =>
        DecisionState == SourcingDecisionState.AlternativeChosen
        || (ChosenCandidateCode is not null
            && RecommendedCandidateCode is not null
            && !string.Equals(ChosenCandidateCode, RecommendedCandidateCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>The candidates still in the running.</summary>
    public IEnumerable<SourcingCandidate> CandidatesInContention => Candidates.Where(c => c.IsInContention);

    /// <summary>The candidates taken out of the running, each with its reason.</summary>
    public IEnumerable<SourcingCandidate> ExcludedCandidates => Candidates.Where(c => !c.IsInContention);

    /// <summary>The candidate carrying <paramref name="code"/>, or <see langword="null"/> where none does.</summary>
    public SourcingCandidate? FindCandidate(string code) =>
        Candidates.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every record the comparison rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Candidates.SelectMany(c => c.AllPins)
            .Concat(RequirementPin is { } requirement ? [requirement] : Array.Empty<ReferencePin>())
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

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
