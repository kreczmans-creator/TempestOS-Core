using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Procurement;

/// <summary>How a candidate stands against one criterion.</summary>
/// <remarks>
/// <see cref="NotAssessed"/> and <see cref="Unknown"/> are deliberately
/// distinct. Nobody looked, and somebody looked and could not find out,
/// are different states of the world, and only the second is a fact about
/// the supplier.
/// </remarks>
public enum CriterionStanding
{
    /// <summary>Nobody has looked at this criterion for this candidate.</summary>
    NotAssessed,

    /// <summary>Somebody looked, and the information is not available.</summary>
    Unknown,

    /// <summary>The candidate does not meet it.</summary>
    Fails,

    /// <summary>The candidate meets it, but only just or with qualifications.</summary>
    Marginal,

    /// <summary>The candidate meets it.</summary>
    Meets,

    /// <summary>The candidate exceeds it.</summary>
    Exceeds,

    /// <summary>The criterion does not apply to this candidate.</summary>
    NotApplicable
}

/// <summary>What <see cref="CriterionStanding"/> means arithmetically, and what it does not.</summary>
public static class CriterionStandings
{
    /// <summary>
    /// The score a standing contributes, from 0 to 1, or
    /// <see langword="null"/> where it contributes nothing knowable.
    /// </summary>
    /// <remarks>
    /// <see cref="CriterionStanding.NotAssessed"/> and
    /// <see cref="CriterionStanding.Unknown"/> score <see langword="null"/>
    /// rather than zero. Scoring absence as zero is the single most common
    /// way a comparison lies: it silently ranks the supplier nobody
    /// researched below the supplier who was researched and found wanting.
    /// `D5` instead reports the comparison as incomplete.
    /// </remarks>
    public static decimal? Score(CriterionStanding standing) => standing switch
    {
        CriterionStanding.Fails => 0m,
        CriterionStanding.Marginal => 0.4m,
        CriterionStanding.Meets => 0.8m,
        CriterionStanding.Exceeds => 1.0m,
        _ => null
    };

    /// <summary>Whether the standing means somebody actually established something.</summary>
    public static bool IsEstablished(CriterionStanding standing) =>
        standing is not (CriterionStanding.NotAssessed or CriterionStanding.Unknown);
}

/// <summary>How one candidate stands against one criterion, and what says so.</summary>
/// <param name="CriterionCode">The criterion assessed. Required.</param>
/// <param name="Standing">How the candidate stands.</param>
/// <param name="Commentary">What the assessor found, in their own words. <see langword="null"/> where nothing was written.</param>
/// <param name="SourcePins">The records the assessment was drawn from. Never <see langword="null"/>.</param>
/// <param name="Evidence">Anything else supporting it. Never <see langword="null"/>.</param>
public sealed record CriterionAssessment(
    string CriterionCode,
    CriterionStanding Standing,
    string? Commentary = null,
    IReadOnlyList<ReferencePin>? SourcePins = null,
    IReadOnlyList<BusinessEvidence>? Evidence = null)
{
    /// <summary>The criterion assessed.</summary>
    public string CriterionCode { get; } = string.IsNullOrWhiteSpace(CriterionCode)
        ? throw new ArgumentException("A criterion assessment must say which criterion it assesses.", nameof(CriterionCode))
        : CriterionCode.Trim();

    /// <summary>The records the assessment was drawn from.</summary>
    public IReadOnlyList<ReferencePin> SourcePins { get; init; } = SourcePins ?? [];

    /// <summary>Anything else supporting it.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether anything at all supports the standing.</summary>
    public bool IsSupported => SourcePins.Count > 0 || Evidence.Any(e => e.IsLocatable);

    /// <summary>Whether somebody actually established something.</summary>
    public bool IsEstablished => CriterionStandings.IsEstablished(Standing);
}

/// <summary>Why a candidate was taken out of the running.</summary>
/// <remarks>
/// An exclusion must always carry a reason and the criterion it failed,
/// if any. A comparison that quietly drops a candidate is worse than one
/// that ranks it last: the reader cannot see that it was ever considered,
/// and cannot disagree with why it was not.
/// </remarks>
/// <param name="Reason">Why the candidate is out. Required.</param>
/// <param name="FailedCriterionCode">The mandatory criterion it failed. <see langword="null"/> where the exclusion is not criterion-based.</param>
/// <param name="ExcludedByPrincipalId">Who decided to exclude it, where a person did rather than a mandatory criterion. <see langword="null"/> otherwise.</param>
public sealed record CandidateExclusion(string Reason, string? FailedCriterionCode = null, string? ExcludedByPrincipalId = null)
{
    /// <summary>Why the candidate is out.</summary>
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException(
            "An excluded candidate must carry the reason it was excluded. A comparison that drops a candidate silently cannot be reviewed.",
            nameof(Reason))
        : Reason.Trim();

    /// <summary>Whether a mandatory criterion put it out, rather than a person's judgement.</summary>
    public bool IsAutomatic => FailedCriterionCode is not null;
}

/// <summary>One candidate, as assessed against the requirement.</summary>
/// <remarks>
/// A candidate is a supplier the organisation is considering. Being a
/// candidate is not a status a supplier holds; it exists only within one
/// sourcing assessment, which is why nothing here writes back to the
/// supplier database.
/// </remarks>
public sealed record SourcingCandidate
{
    /// <summary>The candidate's own identifier within the assessment. Required.</summary>
    public required string Code { get; init; }

    /// <summary>The supplier record considered. Required.</summary>
    public required string SupplierRecordId { get; init; }

    /// <summary>The supplier record at the revision it was read. <see langword="null"/> where unpinned.</summary>
    public ReferencePin? SupplierPin { get; init; }

    /// <summary>The supplier quote the assessment prices against. <see langword="null"/> where none was sought.</summary>
    public ReferencePin? QuotePin { get; init; }

    /// <summary>The price being compared. <see langword="null"/> where nobody has one.</summary>
    public Money? Price { get; init; }

    /// <summary>The lead time being compared. <see langword="null"/> where nobody has one.</summary>
    public LeadTimeDuration? LeadTime { get; init; }

    /// <summary>How the candidate stands against each criterion. Never <see langword="null"/>.</summary>
    public IReadOnlyList<CriterionAssessment> Assessments { get; init; } = [];

    /// <summary>Why the candidate is out of the running. <see langword="null"/> where it is still in it.</summary>
    public CandidateExclusion? Exclusion { get; init; }

    /// <summary>Anything else about the candidate. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the candidate is still in the running.</summary>
    public bool IsInContention => Exclusion is null;

    /// <summary>How the candidate stands against <paramref name="criterionCode"/>, or <see langword="null"/> where nobody recorded anything.</summary>
    public CriterionAssessment? FindAssessment(string criterionCode) =>
        Assessments.FirstOrDefault(a => string.Equals(a.CriterionCode, criterionCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every criterion in <paramref name="requirement"/> nobody has established for this candidate.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="requirement"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<string> MissingInformation(SourcingRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return requirement.Criteria
            .Where(c => c.Role != SourcingCriterionRole.Informational)
            .Where(c => FindAssessment(c.Code) is not { IsEstablished: true })
            .Select(c => c.Code)
            .ToList();
    }

    /// <summary>Every record the candidate's assessment rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Assessments.SelectMany(a => a.SourcePins)
            .Concat(SupplierPin is { } supplier ? [supplier] : Array.Empty<ReferencePin>())
            .Concat(QuotePin is { } quote ? [quote] : Array.Empty<ReferencePin>())
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();
}
