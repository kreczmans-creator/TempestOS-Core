using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringAssets;

namespace Tempest.Core.Knowledge.Lessons;

/// <summary>What kind of thing went wrong.</summary>
public enum FailureCategory
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>The part or structure failed physically.</summary>
    Structural,

    /// <summary>The material behaved otherwise than expected.</summary>
    Material,

    /// <summary>It could not be made as drawn, or was made wrongly.</summary>
    Manufacturing,

    /// <summary>Parts did not go together, or did not stay together.</summary>
    Assembly,

    /// <summary>The design was wrong before anything was made.</summary>
    Design,

    /// <summary>The requirement was wrong, missing or misunderstood.</summary>
    Requirements,

    /// <summary>A calculation was wrong.</summary>
    Analysis,

    /// <summary>It was verified inadequately, or not at all.</summary>
    Verification,

    /// <summary>Somebody was not told something they needed.</summary>
    Communication,

    /// <summary>A supplier did not deliver what was needed.</summary>
    Supply,

    /// <summary>It failed in service, for reasons of use rather than design.</summary>
    InService,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>How badly it went.</summary>
public enum FailureSeverity
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Caught before it cost anything.</summary>
    NearMiss,

    /// <summary>Cost time or money and nothing else.</summary>
    Minor,

    /// <summary>Cost a great deal of time or money, or a customer's confidence.</summary>
    Significant,

    /// <summary>Caused or could have caused injury, or a substantial loss.</summary>
    Serious
}

/// <summary>
/// How confidently a cause was established.
/// </summary>
/// <remarks>
/// The distinction that keeps a lessons database honest. Most failures
/// are never fully investigated, and a database recording every plausible
/// story as a root cause teaches the wrong lessons with great confidence.
/// </remarks>
public enum CauseConfidence
{
    /// <summary>Nobody investigated.</summary>
    NotInvestigated,

    /// <summary>Somebody's plausible account, with nothing established.</summary>
    Suspected,

    /// <summary>Supported by evidence, and other explanations remain possible.</summary>
    Probable,

    /// <summary>Established by evidence, other explanations ruled out.</summary>
    Established
}

/// <summary>Something that caused, or helped cause, the failure.</summary>
/// <remarks>
/// A root cause and a contributing factor are both recorded here, with
/// <see cref="IsRootCause"/> separating them, because the distinction is
/// a judgement somebody made rather than a property of the world — and
/// because most real failures have several of each.
/// </remarks>
/// <param name="Reference">The cause's own identifier within the record. Required.</param>
/// <param name="Statement">What it was. Required.</param>
/// <param name="IsRootCause">Whether somebody judged this a root cause rather than a contributing factor.</param>
/// <param name="Confidence">How confidently it was established.</param>
/// <param name="Evidence">What establishes it. Never <see langword="null"/>.</param>
public sealed record FailureCause(
    string Reference,
    string Statement,
    bool IsRootCause = false,
    CauseConfidence Confidence = CauseConfidence.NotInvestigated,
    IReadOnlyList<EngineeringEvidence>? Evidence = null)
{
    /// <summary>The cause's own identifier within the record.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A failure cause must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What it was.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A failure cause must say what it was.", nameof(Statement))
        : Statement.Trim();

    /// <summary>What establishes it.</summary>
    public IReadOnlyList<EngineeringEvidence> Evidence { get; init; } = Evidence ?? [];

    /// <summary>Whether anything anybody can check supports it.</summary>
    public bool IsEvidenced => Evidence.Any(e => e.IsLocatable);

    /// <summary>Whether the cause was actually established rather than supposed.</summary>
    public bool IsEstablished => Confidence == CauseConfidence.Established;
}

/// <summary>Where a corrective action has got to.</summary>
public enum CorrectiveActionState
{
    /// <summary>Proposed and not started.</summary>
    Proposed,

    /// <summary>Being done.</summary>
    InProgress,

    /// <summary>Done.</summary>
    Implemented,

    /// <summary>Done, and somebody has confirmed it worked.</summary>
    VerifiedEffective,

    /// <summary>Deliberately not being done, with a reason.</summary>
    Declined
}

/// <summary>Something done so it does not happen again.</summary>
/// <param name="Reference">The action's own identifier within the record. Required.</param>
/// <param name="Description">What was, or is to be, done. Required.</param>
/// <param name="State">Where it has got to.</param>
/// <param name="OwnerPrincipalId">Who is responsible. <see langword="null"/> where nobody is.</param>
/// <param name="AddressesCauseReferences">The causes it addresses. Never <see langword="null"/>.</param>
/// <param name="EffectivenessEvidence">What shows it worked. Never <see langword="null"/>.</param>
/// <param name="DeclineReason">Why it is not being done, where it is not. <see langword="null"/> otherwise.</param>
public sealed record CorrectiveAction(
    string Reference,
    string Description,
    CorrectiveActionState State = CorrectiveActionState.Proposed,
    string? OwnerPrincipalId = null,
    IReadOnlyList<string>? AddressesCauseReferences = null,
    IReadOnlyList<EngineeringEvidence>? EffectivenessEvidence = null,
    string? DeclineReason = null)
{
    /// <summary>The action's own identifier within the record.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A corrective action must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What was, or is to be, done.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A corrective action must say what is to be done.", nameof(Description))
        : Description.Trim();

    /// <summary>The causes it addresses.</summary>
    public IReadOnlyList<string> AddressesCauseReferences { get; init; } = AddressesCauseReferences ?? [];

    /// <summary>What shows it worked.</summary>
    public IReadOnlyList<EngineeringEvidence> EffectivenessEvidence { get; init; } = EffectivenessEvidence ?? [];

    /// <summary>Whether somebody has confirmed the action actually worked.</summary>
    /// <remarks>
    /// Implemented and effective are different things, and the gap
    /// between them is where organisations learn nothing twice.
    /// </remarks>
    public bool IsVerifiedEffective =>
        State == CorrectiveActionState.VerifiedEffective && EffectivenessEvidence.Any(e => e.IsLocatable);

    /// <summary>Whether it still needs somebody's attention.</summary>
    public bool IsOutstanding => State is CorrectiveActionState.Proposed or CorrectiveActionState.InProgress;

    /// <summary>Whether it was declined without a stated reason.</summary>
    public bool IsUnexplainedDecline => State == CorrectiveActionState.Declined && string.IsNullOrWhiteSpace(DeclineReason);
}

/// <summary>
/// What went wrong, why, and what the organisation learned.
/// </summary>
/// <remarks>
/// <para>
/// <b>The lesson is the point.</b> An incident record without a
/// transferable lesson is an archive entry; the whole purpose of `F4` is
/// that the next project does not repeat this one. Applicability matters
/// more here than anywhere else in `P06`, because a lesson filed under
/// nothing is a lesson nobody finds.
/// </para>
/// <para>
/// <b>Confidentiality is first-class.</b> Failures involve named people,
/// named customers and named suppliers, and a lessons database is
/// exactly the sort of thing that gets shared more widely than intended.
/// `Classification` reuses `P07`'s
/// <see cref="ConfidentialityClassification"/>, and the model separates
/// what happened from who it happened to, so the lesson can be shared
/// when the incident cannot.
/// </para>
/// <para>
/// <b>No real failures ship.</b> Fabricating an incident would produce a
/// plausible engineering story with no basis, and populating this from
/// real history is the organisation's own decision to make with its own
/// records (`ADR-0141`).
/// </para>
/// </remarks>
public sealed record LessonRecord
{
    /// <summary>The reference the record is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What happened, in a line. Required.</summary>
    public required string Title { get; init; }

    /// <summary>The situation it happened in. Required.</summary>
    public required string Context { get; init; }

    /// <summary>What was observed to be wrong. Required.</summary>
    public required string ObservedProblem { get; init; }

    /// <summary>What it cost — time, money, confidence, safety. <see langword="null"/> where unrecorded.</summary>
    public string? Consequence { get; init; }

    /// <summary>What kind of thing went wrong.</summary>
    public FailureCategory Category { get; init; } = FailureCategory.Unspecified;

    /// <summary>How badly.</summary>
    public FailureSeverity Severity { get; init; } = FailureSeverity.Unspecified;

    /// <summary>When it happened. <see langword="null"/> where unrecorded.</summary>
    public DateOnly? OccurredOn { get; init; }

    /// <summary>What caused it, and what helped. Never <see langword="null"/>.</summary>
    public IReadOnlyList<FailureCause> Causes { get; init; } = [];

    /// <summary>What was done about it. Never <see langword="null"/>.</summary>
    public IReadOnlyList<CorrectiveAction> CorrectiveActions { get; init; } = [];

    /// <summary>
    /// The transferable lesson — what somebody on a different project
    /// should do differently. Required in substance.
    /// </summary>
    public string? Lesson { get; init; }

    /// <summary>Where the lesson applies, in the organisation's own words. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> AppliesWhen { get; init; } = [];

    /// <summary>What supports the account. Never <see langword="null"/>.</summary>
    public IReadOnlyList<EngineeringEvidence> Evidence { get; init; } = [];

    /// <summary>How sensitive the record is.</summary>
    public ConfidentialityClassification Classification { get; init; } = ConfidentialityClassification.Confidential;

    /// <summary>
    /// Whether the lesson may be shared without the incident it came
    /// from.
    /// </summary>
    /// <remarks>
    /// The mechanism that lets a confidential incident produce a
    /// shareable lesson. Set deliberately; nothing infers it.
    /// </remarks>
    public bool LessonIsShareable { get; init; }

    /// <summary>Who investigated. <see langword="null"/> where nobody did.</summary>
    public string? InvestigatedByPrincipalId { get; init; }

    /// <summary>Where it applies and who should read it.</summary>
    public KnowledgeApplicability Applicability { get; init; } = KnowledgeApplicability.Unrestricted;

    /// <summary>Where it came from and who has checked it.</summary>
    public KnowledgeProvenance Provenance { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The causes somebody judged root causes.</summary>
    public IReadOnlyList<FailureCause> RootCauses => Causes.Where(c => c.IsRootCause).ToList();

    /// <summary>The causes somebody judged contributing factors.</summary>
    public IReadOnlyList<FailureCause> ContributingFactors => Causes.Where(c => !c.IsRootCause).ToList();

    /// <summary>Actions still needing somebody's attention.</summary>
    public IReadOnlyList<CorrectiveAction> OutstandingActions => CorrectiveActions.Where(a => a.IsOutstanding).ToList();

    /// <summary>Whether anybody investigated at all.</summary>
    public bool WasInvestigated =>
        !string.IsNullOrWhiteSpace(InvestigatedByPrincipalId)
        || Causes.Any(c => c.Confidence != CauseConfidence.NotInvestigated);

    /// <summary>Whether the record carries a transferable lesson.</summary>
    public bool HasLesson => !string.IsNullOrWhiteSpace(Lesson);

    /// <summary>Root causes nothing addresses.</summary>
    /// <remarks>
    /// The gap that lets the same failure happen twice.
    /// </remarks>
    public IReadOnlyList<FailureCause> UnaddressedRootCauses =>
        RootCauses
            .Where(c => !CorrectiveActions.Any(a =>
                a.AddressesCauseReferences.Contains(c.Reference, StringComparer.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>
    /// Whether the record has done its job: investigated, with a lesson,
    /// and every root cause addressed by something confirmed effective.
    /// </summary>
    public bool IsClosedOut =>
        WasInvestigated
        && HasLesson
        && RootCauses.Count > 0
        && UnaddressedRootCauses.Count == 0
        && CorrectiveActions.Where(a => a.AddressesCauseReferences.Count > 0).All(a => a.IsVerifiedEffective);

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
