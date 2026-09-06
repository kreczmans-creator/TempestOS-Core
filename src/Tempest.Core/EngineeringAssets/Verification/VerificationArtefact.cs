using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.Verification;

/// <summary>How a requirement is to be, or was, verified.</summary>
/// <remarks>
/// The four classical methods plus the two TempestOS can hold evidence
/// for directly. A closed vocabulary here, unlike
/// <c>Core.Verification.IVerificationRecord.Method</c>'s open string,
/// because `E3` plans verification as well as recording it and a plan
/// needs a countable method.
/// </remarks>
public enum VerificationMethod
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Examination against the drawing or specification.</summary>
    Inspection,

    /// <summary>Physical or functional test.</summary>
    Test,

    /// <summary>Calculation, simulation or other analysis.</summary>
    Analysis,

    /// <summary>Operation showing the requirement is met.</summary>
    Demonstration,

    /// <summary>Comparison with something already verified.</summary>
    Similarity,

    /// <summary>A supplier's or third party's certificate.</summary>
    Certification
}

/// <summary>
/// Where a verification activity has got to, and what it found.
/// </summary>
/// <remarks>
/// <para>
/// Six values because §11 requires six materially different answers, and
/// the three that are not <see cref="Passed"/> or <see cref="Failed"/>
/// are the ones that matter. <see cref="NotPerformed"/> is the state
/// almost every real verification is in for most of a project, and it is
/// not a failure. <see cref="Inconclusive"/> means somebody did the work
/// and it did not settle the question.
/// </para>
/// <para>
/// <b>Missing evidence is never a pass.</b> An artefact reaches
/// <see cref="Passed"/> only when somebody records that it did; there is
/// no path from "nothing recorded" to "passed", and
/// <see cref="VerificationStandings.IsDemonstrated"/> is true for exactly
/// one value.
/// </para>
/// </remarks>
public enum VerificationStanding
{
    /// <summary>Nobody has done it yet.</summary>
    NotPerformed,

    /// <summary>Under way.</summary>
    InProgress,

    /// <summary>Done, and it did not settle the question.</summary>
    Inconclusive,

    /// <summary>Done, and the requirement was not met.</summary>
    Failed,

    /// <summary>Done, and the requirement was met.</summary>
    Passed,

    /// <summary>The requirement does not apply to this subject.</summary>
    NotApplicable
}

/// <summary>What <see cref="VerificationStanding"/> means, and what it does not.</summary>
public static class VerificationStandings
{
    /// <summary>Whether the requirement has actually been shown to be met.</summary>
    /// <remarks>
    /// True for <see cref="VerificationStanding.Passed"/> alone. Not for
    /// <see cref="VerificationStanding.NotApplicable"/>, which means the
    /// question does not arise rather than that the answer is yes.
    /// </remarks>
    public static bool IsDemonstrated(VerificationStanding standing) => standing == VerificationStanding.Passed;

    /// <summary>Whether somebody has done the work, whatever it showed.</summary>
    public static bool IsPerformed(VerificationStanding standing) =>
        standing is VerificationStanding.Inconclusive or VerificationStanding.Failed or VerificationStanding.Passed;

    /// <summary>Whether the standing leaves work for somebody to do.</summary>
    public static bool IsOutstanding(VerificationStanding standing) =>
        standing is VerificationStanding.NotPerformed
            or VerificationStanding.InProgress
            or VerificationStanding.Inconclusive
            or VerificationStanding.Failed;

    /// <summary>
    /// The standing of a set of verifications taken together.
    /// </summary>
    /// <remarks>
    /// The weakest wins, and an empty set is
    /// <see cref="VerificationStanding.NotPerformed"/> rather than
    /// <see cref="VerificationStanding.Passed"/>. Verifying nothing is
    /// not verifying everything.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="standings"/> is <see langword="null"/>.</exception>
    public static VerificationStanding Weakest(IEnumerable<VerificationStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var considered = standings.Where(s => s != VerificationStanding.NotApplicable).ToList();

        return considered.Count == 0
            ? VerificationStanding.NotPerformed
            : considered.OrderBy(Rank).First();
    }

    /// <summary>How strong a standing is, for ordering. Higher is stronger.</summary>
    public static int Rank(VerificationStanding standing) => standing switch
    {
        VerificationStanding.Failed => 0,
        VerificationStanding.NotPerformed => 1,
        VerificationStanding.InProgress => 2,
        VerificationStanding.Inconclusive => 3,
        VerificationStanding.NotApplicable => 4,
        VerificationStanding.Passed => 5,
        _ => 0
    };
}

/// <summary>
/// What is being verified.
/// </summary>
/// <remarks>
/// A reference into the existing Requirements architecture, never a copy
/// of it. `E3` holds the evidence that a requirement was met; the
/// requirement itself belongs to <c>Tempest.Core.Requirements</c> and is
/// identified here by its own Id and its human identifier
/// (`ADR-0138`).
/// </remarks>
/// <param name="RequirementId">The requirement's own stable identity. Required.</param>
/// <param name="RequirementIdentifier">Its human-facing identifier, e.g. <c>"SYS-REQ-042"</c>. <see langword="null"/> where unrecorded.</param>
/// <param name="StatementAtVerification">The requirement's wording when the verification was planned, for the historical record. <see langword="null"/> where unrecorded.</param>
/// <param name="RevisionAtVerification">The requirement's revision when the verification was planned. <see langword="null"/> where unrecorded.</param>
public sealed record VerifiedRequirement(
    Guid RequirementId,
    string? RequirementIdentifier = null,
    string? StatementAtVerification = null,
    int? RevisionAtVerification = null)
{
    /// <summary>The requirement's own stable identity.</summary>
    public Guid RequirementId { get; } = RequirementId == Guid.Empty
        ? throw new ArgumentException("A verification artefact must name the requirement it verifies.", nameof(RequirementId))
        : RequirementId;

    /// <summary>
    /// Whether the artefact recorded what the requirement said at the
    /// time.
    /// </summary>
    /// <remarks>
    /// Without it, a requirement reworded after verification leaves
    /// evidence that appears to demonstrate something it never addressed.
    /// </remarks>
    public bool IsPinnedToRevision => RevisionAtVerification is not null;
}

/// <summary>
/// The result of one verification activity, as somebody recorded it.
/// </summary>
/// <remarks>
/// Distinct from the artefact that holds it, and distinct again from any
/// decision taken on the strength of it (`ADR-0138`). A result says what
/// was observed; a decision says what the organisation is going to do
/// about it, and `E3` records only the first.
/// </remarks>
/// <param name="Standing">What the activity showed.</param>
/// <param name="Summary">What was observed, in plain words. Required.</param>
/// <param name="PerformedByPrincipalId">Who did it. <see langword="null"/> where unrecorded.</param>
/// <param name="PerformedOn">When. <see langword="null"/> where unrecorded.</param>
/// <param name="VerificationRecordId">The platform's own <c>Core.Verification.IVerificationRecord</c>, where one was created. <see langword="null"/> otherwise.</param>
/// <param name="CalculationPackReference">The `E2` pack holding the analysis, where the method was analysis. <see langword="null"/> otherwise.</param>
public sealed record VerificationResult(
    VerificationStanding Standing,
    string Summary,
    string? PerformedByPrincipalId = null,
    DateOnly? PerformedOn = null,
    Guid? VerificationRecordId = null,
    string? CalculationPackReference = null)
{
    /// <summary>What was observed.</summary>
    public string Summary { get; } = string.IsNullOrWhiteSpace(Summary)
        ? throw new ArgumentException("A verification result must say what was observed.", nameof(Summary))
        : Summary.Trim();

    /// <summary>Whether the requirement was shown to be met.</summary>
    public bool IsDemonstrated => VerificationStandings.IsDemonstrated(Standing);

    /// <summary>Whether the result is attributable to a person on a date.</summary>
    /// <remarks>
    /// A pass nobody performed on no particular day is not evidence of
    /// anything, and the validation service treats it as an error rather
    /// than a warning.
    /// </remarks>
    public bool IsAttributable => !string.IsNullOrWhiteSpace(PerformedByPrincipalId) && PerformedOn is not null;
}

/// <summary>
/// The governed record that a requirement was verified — by what method,
/// on what evidence, with what result, checked by whom.
/// </summary>
/// <remarks>
/// <para>
/// Four things are kept apart here and are routinely conflated: the
/// <b>requirement</b> (owned by <c>Tempest.Core.Requirements</c>), the
/// <b>activity</b> that was performed, the <b>evidence</b> it produced,
/// and any <b>decision</b> taken on the strength of it. `E3` owns the
/// second and third, references the first, and records none of the
/// fourth (`ADR-0138`).
/// </para>
/// <para>
/// <b>Nothing here turns absent evidence into a pass.</b> An artefact
/// with no result stands at <see cref="VerificationStanding.NotPerformed"/>,
/// which is an honest description of most of a project.
/// </para>
/// </remarks>
public sealed record VerificationArtefact
{
    /// <summary>The reference the artefact is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What is being verified. Required.</summary>
    public required VerifiedRequirement Requirement { get; init; }

    /// <summary>What is being verified against it — the design, the article, the batch. Required.</summary>
    public required string Subject { get; init; }

    /// <summary>How verification is to be, or was, done.</summary>
    public VerificationMethod Method { get; init; } = VerificationMethod.Unspecified;

    /// <summary>What the method involves in this case. <see langword="null"/> where nothing was written.</summary>
    public string? MethodDescription { get; init; }

    /// <summary>What must be true for the requirement to count as met. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = [];

    /// <summary>What was found, once somebody has looked. <see langword="null"/> until then.</summary>
    public VerificationResult? Result { get; init; }

    /// <summary>What supports it. Never <see langword="null"/>.</summary>
    public IReadOnlyList<EngineeringEvidence> Evidence { get; init; } = [];

    /// <summary>Records this artefact depends on, at the revisions relied on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> SourcePins { get; init; } = [];

    /// <summary>Where and when it applies.</summary>
    public AssetApplicability Applicability { get; init; } = AssetApplicability.Unrestricted;

    /// <summary>Who owns it, who wrote it, who checked it.</summary>
    public AssetGovernanceFacts Governance { get; init; } = new();

    /// <summary>Why the requirement does not apply, where <see cref="Standing"/> is <see cref="VerificationStanding.NotApplicable"/>. <see langword="null"/> otherwise.</summary>
    /// <remarks>
    /// Blank is not a reason. A whitespace value leaves the artefact at
    /// <see cref="VerificationStanding.NotPerformed"/> rather than
    /// letting an empty string retire a requirement.
    /// </remarks>
    public string? NotApplicableReason { get; init; }

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Where the verification stands.
    /// </summary>
    /// <remarks>
    /// Derived from the result rather than settable, so no caller can
    /// record a pass without a result that says so.
    /// </remarks>
    public VerificationStanding Standing => Result?.Standing
        ?? (string.IsNullOrWhiteSpace(NotApplicableReason)
            ? VerificationStanding.NotPerformed
            : VerificationStanding.NotApplicable);

    /// <summary>Whether the requirement has actually been shown to be met.</summary>
    public bool IsDemonstrated => VerificationStandings.IsDemonstrated(Standing);

    /// <summary>Whether the artefact leaves work for somebody to do.</summary>
    public bool IsOutstanding => VerificationStandings.IsOutstanding(Standing);

    /// <summary>Whether anything anybody can go and look at supports the result.</summary>
    public bool IsEvidenced => Evidence.Any(e => e.IsLocatable);

    /// <summary>Whether anything independent of the asserting engineer supports it.</summary>
    public bool HasIndependentEvidence => Evidence.Any(e => e.IsIndependent);

    /// <summary>
    /// Whether the artefact claims a pass that nothing supports.
    /// </summary>
    /// <remarks>
    /// The single most important property on this type. A recorded pass
    /// with no locatable evidence behind it is the failure mode `E3`
    /// exists to make visible, and the validation service reports it as
    /// an error.
    /// </remarks>
    public bool IsUnsupportedPass => IsDemonstrated && !IsEvidenced;

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
