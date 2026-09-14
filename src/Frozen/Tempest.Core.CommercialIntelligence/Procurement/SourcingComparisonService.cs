namespace Tempest.Core.CommercialIntelligence.Procurement;

/// <summary>Ranks assessed candidates against a requirement, and says what it could not establish.</summary>
public interface ISourcingComparisonService
{
    /// <summary>
    /// Applies <paramref name="requirement"/> to <paramref name="candidates"/>
    /// and produces the comparison.
    /// </summary>
    /// <remarks>
    /// Deterministic and pure: no I/O, no clock, no randomness. The same
    /// requirement and the same candidates produce the same comparison,
    /// including the same tie-breaking, every time — because a
    /// recommendation nobody can reproduce is one nobody can challenge.
    /// </remarks>
    /// <param name="reference">The reference the comparison will carry.</param>
    /// <param name="requirement">What is being sourced and how it is judged.</param>
    /// <param name="candidates">The candidates, already assessed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="requirement"/> or <paramref name="candidates"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    SourcingComparison Compare(string reference, SourcingRequirement requirement, IReadOnlyList<SourcingCandidate> candidates);
}

/// <summary>The concrete <see cref="ISourcingComparisonService"/> implementation.</summary>
/// <remarks>
/// <para>
/// The service compares and ranks. It never places an order, awards
/// business, approves or qualifies a supplier, or commits expenditure —
/// there is no method here that could, and the structural guard tests
/// assert as much (`ADR-0135`).
/// </para>
/// <para>
/// Two behaviours are worth stating because their opposites are the usual
/// failure. Absent information is never scored as zero; it reduces the
/// established weight and shows up as an outstanding question, so a
/// supplier nobody researched cannot be quietly ranked below one that was
/// researched and found wanting. And a candidate failing a mandatory
/// criterion is excluded <em>with the reason attached</em> rather than
/// dropped, so the reader can see it was considered.
/// </para>
/// </remarks>
public sealed class SourcingComparisonService : ISourcingComparisonService
{
    /// <summary>How much of the weight must be established before a recommendation is more than provisional.</summary>
    public const decimal CompleteEnoughThreshold = 0.999m;

    /// <summary>How far ahead the leader must be for the recommendation to read as clear rather than marginal.</summary>
    public const decimal ClearLeadMargin = 0.10m;

    /// <inheritdoc />
    public SourcingComparison Compare(string reference, SourcingRequirement requirement, IReadOnlyList<SourcingCandidate> candidates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(candidates);

        var considered = candidates.Select(c => ApplyMandatoryCriteria(c, requirement)).ToList();

        var rankings = considered
            .Where(c => c.IsInContention)
            .Select(c => Rank(c, requirement))
            .OrderByDescending(r => r.Score ?? -1m)
            .ThenByDescending(r => r.EstablishedWeight)
            .ThenBy(r => r.CandidateCode, StringComparer.Ordinal)
            .ToList();

        var leader = rankings.FirstOrDefault(r => r.IsScored);
        var strength = AssessStrength(rankings, leader);

        return new SourcingComparison
        {
            Reference = reference,
            RequirementReference = requirement.Reference,
            Candidates = considered,
            Rankings = rankings,
            RecommendedCandidateCode = strength == RecommendationStrength.Insufficient ? null : leader?.CandidateCode,
            RecommendationRationale = DescribeRecommendation(requirement, rankings, leader, strength),
            Strength = strength,
            OutstandingQuestions = OutstandingQuestions(considered, requirement),
            DecisionState = SourcingDecisionState.AwaitingHumanDecision
        };
    }

    private static SourcingCandidate ApplyMandatoryCriteria(SourcingCandidate candidate, SourcingRequirement requirement)
    {
        // An exclusion somebody has already recorded stands. The service
        // adds reasons; it never removes one a person put there.
        if (!candidate.IsInContention)
            return candidate;

        var failed = requirement.MandatoryCriteria
            .FirstOrDefault(c => candidate.FindAssessment(c.Code)?.Standing == CriterionStanding.Fails);

        if (failed is null)
            return candidate;

        return candidate with
        {
            Exclusion = new CandidateExclusion(
                $"Fails mandatory criterion '{failed.Code}': {failed.Statement}.",
                failed.Code)
        };
    }

    private static CandidateRanking Rank(SourcingCandidate candidate, SourcingRequirement requirement)
    {
        var scoring = requirement.WeightedCriteria.ToList();
        var totalWeight = scoring.Sum(c => c.Weight);

        decimal establishedWeight = 0m;
        decimal weightedScore = 0m;
        var missing = new List<string>();

        foreach (var criterion in scoring)
        {
            var standing = candidate.FindAssessment(criterion.Code)?.Standing ?? CriterionStanding.NotAssessed;

            if (standing == CriterionStanding.NotApplicable)
            {
                // Not applicable removes the criterion from this
                // candidate's denominator rather than scoring it, so a
                // criterion that cannot apply neither helps nor harms.
                continue;
            }

            var score = CriterionStandings.Score(standing);

            if (score is null)
            {
                missing.Add(criterion.Code);
                continue;
            }

            establishedWeight += criterion.Weight;
            weightedScore += criterion.Weight * score.Value;
        }

        // Also report mandatory criteria nobody established: a candidate
        // that has not been checked against a hard requirement is not a
        // candidate that passed it.
        missing.AddRange(
            requirement.MandatoryCriteria
                .Where(c => candidate.FindAssessment(c.Code) is not { IsEstablished: true })
                .Select(c => c.Code));

        return new CandidateRanking(
            candidate.Code,
            establishedWeight > 0m ? weightedScore / establishedWeight : null,
            totalWeight > 0m ? establishedWeight / totalWeight : 0m,
            missing.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.Ordinal).ToList());
    }

    private static RecommendationStrength AssessStrength(IReadOnlyList<CandidateRanking> rankings, CandidateRanking? leader)
    {
        if (leader is null)
            return RecommendationStrength.Insufficient;

        if (leader.EstablishedWeight < CompleteEnoughThreshold || !leader.IsComplete)
            return RecommendationStrength.Provisional;

        var runnerUp = rankings.FirstOrDefault(r => r != leader && r.IsScored);

        if (runnerUp is null)
            return RecommendationStrength.Clear;

        return leader.Score!.Value - runnerUp.Score!.Value >= ClearLeadMargin
            ? RecommendationStrength.Clear
            : RecommendationStrength.Marginal;
    }

    private static string DescribeRecommendation(
        SourcingRequirement requirement,
        IReadOnlyList<CandidateRanking> rankings,
        CandidateRanking? leader,
        RecommendationStrength strength)
    {
        if (rankings.Count == 0)
            return $"No candidate remains in contention for '{requirement.Subject}'.";

        if (leader is null || strength == RecommendationStrength.Insufficient)
            return $"Too little has been established about the candidates for '{requirement.Subject}' to rank them. "
                   + "The comparison recommends nobody.";

        var basis = strength switch
        {
            RecommendationStrength.Provisional =>
                $"on {leader.EstablishedWeight:P0} of the weighted criteria; the rest is not established",
            RecommendationStrength.Marginal =>
                "on a complete comparison, but by a narrow margin",
            _ => "on a complete comparison"
        };

        return $"Candidate '{leader.CandidateCode}' scores highest ({leader.Score:F2}) {basis}. "
               + "A person must decide whether to act on this; TempestOS does not.";
    }

    private static IReadOnlyList<string> OutstandingQuestions(
        IReadOnlyList<SourcingCandidate> candidates,
        SourcingRequirement requirement)
    {
        var questions = new List<string>();

        foreach (var candidate in candidates.Where(c => c.IsInContention))
        {
            var missing = candidate.MissingInformation(requirement);

            if (missing.Count > 0)
                questions.Add(
                    $"Candidate '{candidate.Code}': nothing established for {string.Join(", ", missing)}.");
        }

        return questions;
    }
}
