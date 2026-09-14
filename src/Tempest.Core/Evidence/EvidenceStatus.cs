namespace Tempest.Core.Evidence;

/// <summary>
/// A piece of <see cref="Evidence"/>'s own lifecycle position — the
/// canonical vocabulary of `ADR-0074` specialised for evidence (`ADR-0148`),
/// exactly as <c>Tempest.Core.Requirements.RequirementStatus</c> specialises
/// it for a requirement: a real, closed, four-value enum of its own, related
/// to the eight canonical states by name and by meaning rather than by
/// sharing their type. <see cref="Draft"/> is the canonical <c>Draft</c>;
/// <see cref="Checked"/> is the canonical <c>InReview</c> (checked, not yet
/// released to the client); <see cref="Issued"/> is the canonical
/// <c>Released</c> (issued to the client, in force); <see cref="Superseded"/>
/// is the canonical <c>Superseded</c> outright. <c>Approved</c>,
/// <c>Obsolete</c>, <c>Archived</c> and <c>Cancelled</c> are omitted —
/// evidence has no use for them, exactly as `ADR-0074` permits.
/// </summary>
public enum EvidenceStatus
{
    /// <summary>Recorded, but not yet checked.</summary>
    Draft,

    /// <summary>Checked — <see cref="Evidence.Check"/> is recorded.</summary>
    Checked,

    /// <summary>Issued to the client — <see cref="Evidence.Issue"/> is recorded.</summary>
    Issued,

    /// <summary>Superseded by a later revision.</summary>
    Superseded,
}

/// <summary>
/// The permitted <see cref="EvidenceStatus"/> transition table — the sole
/// enforcement point <see cref="IEvidenceService"/>'s own status-moving acts
/// check against, mirroring
/// <c>Tempest.Core.Requirements.RequirementStatusTransitions</c> exactly.
/// </summary>
internal static class EvidenceStatusTransitions
{
    private static readonly IReadOnlyDictionary<EvidenceStatus, IReadOnlySet<EvidenceStatus>> Permitted =
        new Dictionary<EvidenceStatus, IReadOnlySet<EvidenceStatus>>
        {
            [EvidenceStatus.Draft] = new HashSet<EvidenceStatus> { EvidenceStatus.Checked },
            [EvidenceStatus.Checked] = new HashSet<EvidenceStatus> { EvidenceStatus.Issued },
            // Issued -> Draft is ReviseAsync: a new round of work begins on
            // a fresh revision, and the issued content stays readable via
            // GetRevisionHistoryAsync. Issued -> Superseded is the terminal
            // move the canonical vocabulary itself names (`ADR-0074`); no
            // act in this Work Package's own scope drives it, but the
            // table declares it because the vocabulary claims it.
            [EvidenceStatus.Issued] = new HashSet<EvidenceStatus> { EvidenceStatus.Draft, EvidenceStatus.Superseded },
            [EvidenceStatus.Superseded] = new HashSet<EvidenceStatus>(),
        };

    /// <summary>Whether transitioning from <paramref name="from"/> to <paramref name="to"/> is permitted. A same-to-same request is not special-cased — permitted only where the table itself lists it.</summary>
    public static bool IsPermitted(EvidenceStatus from, EvidenceStatus to) =>
        Permitted[from].Contains(to);

    /// <summary>Every <see cref="EvidenceStatus"/> value, for a test to walk exhaustively.</summary>
    public static IReadOnlyList<EvidenceStatus> AllStatuses { get; } =
        [EvidenceStatus.Draft, EvidenceStatus.Checked, EvidenceStatus.Issued, EvidenceStatus.Superseded];
}
