using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringAssets;

namespace Tempest.Core.Knowledge;

/// <summary>How hard a piece of knowledge is to take in.</summary>
/// <remarks>
/// Deliberately about the <em>reader</em> rather than the content. "Hard"
/// is not a property of a subject; needing prior grounding is.
/// </remarks>
public enum KnowledgeLevel
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Assumes nothing beyond general engineering literacy.</summary>
    Introductory,

    /// <summary>Assumes a first course in the subject.</summary>
    Intermediate,

    /// <summary>Assumes working familiarity and some practice.</summary>
    Advanced,

    /// <summary>Assumes the reader already works in this area.</summary>
    Specialist
}

/// <summary>Who a piece of knowledge is for.</summary>
public enum KnowledgeAudience
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Somebody learning the subject.</summary>
    Learner,

    /// <summary>An engineer doing the work.</summary>
    PractisingEngineer,

    /// <summary>Somebody checking the work.</summary>
    Reviewer,

    /// <summary>Somebody deciding, who is not doing the engineering.</summary>
    DecisionMaker,

    /// <summary>Somebody outside the organisation.</summary>
    External
}

/// <summary>
/// Where a piece of knowledge applies, and who it is for.
/// </summary>
/// <remarks>
/// Reuses `P05`'s <see cref="EngineeringDiscipline"/> rather than
/// declaring a second discipline vocabulary — the disciplines TempestOS
/// reasons about are the same whether it is holding a template or a
/// lesson (`ADR-0139`). Everything else here is `P06`'s own, because
/// audience and level are properties of knowledge and of nothing else.
/// </remarks>
public sealed record KnowledgeApplicability
{
    /// <summary>The disciplines it belongs to. Never <see langword="null"/>; empty means all.</summary>
    public IReadOnlyList<EngineeringDiscipline> Disciplines { get; init; } = [];

    /// <summary>Free-form topic tags, for searching. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Topics { get; init; } = [];

    /// <summary>How much grounding it assumes.</summary>
    public KnowledgeLevel Level { get; init; } = KnowledgeLevel.Unspecified;

    /// <summary>Who it is for. Never <see langword="null"/>; empty means anybody.</summary>
    public IReadOnlyList<KnowledgeAudience> Audiences { get; init; } = [];

    /// <summary>
    /// Where the knowledge is known not to apply.
    /// </summary>
    /// <remarks>
    /// Stated separately from the positive scope because it carries
    /// different information. "Applies to steel" and "does not apply to
    /// castings" are both worth recording and neither implies the other.
    /// </remarks>
    public IReadOnlyList<string> Exclusions { get; init; } = [];

    /// <summary>Over what period it holds. <see langword="null"/> where it always has.</summary>
    public EffectivePeriod? Validity { get; init; }

    /// <summary>An applicability that restricts nothing.</summary>
    public static KnowledgeApplicability Unrestricted { get; } = new();

    /// <summary>Whether it has run past its own validity as at <paramref name="asAt"/>.</summary>
    public bool IsExpiredAt(DateOnly asAt) => Validity?.HasExpiredBy(asAt) ?? false;

    /// <summary>Whether it covers <paramref name="discipline"/>.</summary>
    public bool CoversDiscipline(EngineeringDiscipline discipline) =>
        Disciplines.Count == 0 || Disciplines.Contains(discipline);

    /// <summary>Whether it is meant for <paramref name="audience"/>.</summary>
    public bool CoversAudience(KnowledgeAudience audience) =>
        Audiences.Count == 0 || Audiences.Contains(audience);

    /// <summary>Whether it matches <paramref name="enquiry"/> on every dimension the enquiry states.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    public bool AppliesTo(KnowledgeEnquiry enquiry)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        if (enquiry.Discipline is { } discipline && !CoversDiscipline(discipline))
            return false;

        if (enquiry.Audience is { } audience && !CoversAudience(audience))
            return false;

        if (enquiry.Level is { } level && Level != KnowledgeLevel.Unspecified && Level > level)
            return false;

        if (enquiry.Topic is { } topic
            && Topics.Count > 0
            && !Topics.Contains(topic, StringComparer.OrdinalIgnoreCase))
            return false;

        if (enquiry.AsAt is { } date && Validity is { } validity && !validity.Contains(date))
            return false;

        return true;
    }
}

/// <summary>What a caller is looking for.</summary>
public sealed record KnowledgeEnquiry
{
    /// <summary>The discipline. <see langword="null"/> to leave it open.</summary>
    public EngineeringDiscipline? Discipline { get; init; }

    /// <summary>The topic. <see langword="null"/> to leave it open.</summary>
    public string? Topic { get; init; }

    /// <summary>
    /// The most demanding level the reader is ready for.
    /// <see langword="null"/> to leave it open.
    /// </summary>
    /// <remarks>
    /// Matches content at or below this level, so asking as an
    /// intermediate reader also returns introductory material. Knowledge
    /// pitched above the reader is excluded; knowledge pitched below is
    /// not.
    /// </remarks>
    public KnowledgeLevel? Level { get; init; }

    /// <summary>Who is asking. <see langword="null"/> to leave it open.</summary>
    public KnowledgeAudience? Audience { get; init; }

    /// <summary>The date the knowledge must be current on. <see langword="null"/> to leave it open.</summary>
    public DateOnly? AsAt { get; init; }
}
