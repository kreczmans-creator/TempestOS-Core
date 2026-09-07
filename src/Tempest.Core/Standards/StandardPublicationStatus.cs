namespace Tempest.Core.Standards;

/// <summary>
/// The publisher's own status for a standard — whether the issuing body
/// still holds it current.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not the same axis as
/// <see cref="ReferenceData.ReferenceValidationState"/>, and never
/// derivable from it.</b> This is a fact about the standard, stated by the
/// organisation that published it. The validation state is a fact about
/// <em>TempestOS's own record</em> of that standard — how far anyone here
/// has checked it. The two vary independently and both directions occur in
/// practice: a <see cref="Withdrawn"/> standard can be recorded in a
/// perfectly <see cref="ReferenceData.ReferenceValidationState.Released"/>
/// record (an accurate, verified record of a withdrawn standard is exactly
/// what a legacy design review needs), and a <see cref="Current"/> standard
/// can sit in a Draft record nobody has checked yet.
/// </para>
/// <para>
/// Collapsing them would be the single most damaging modelling error
/// available to A2: it would let TempestOS's own record-keeping confidence
/// be read as a statement about a standard's standing in the world, which
/// TempestOS has no authority to make.
/// </para>
/// </remarks>
public enum StandardPublicationStatus
{
    /// <summary>Not recorded. The honest default — never a claim the standard is current.</summary>
    Unknown,

    /// <summary>Issued for comment or ballot, not yet published as a standard.</summary>
    Draft,

    /// <summary>Published and held current by the issuing body.</summary>
    Current,

    /// <summary>Current, with one or more published amendments or corrigenda in force.</summary>
    Amended,

    /// <summary>Replaced by a later edition or a different standard, as stated by the issuing body.</summary>
    Superseded,

    /// <summary>Withdrawn by the issuing body without a stated replacement.</summary>
    Withdrawn,

    /// <summary>Withdrawn long enough ago that the issuing body no longer publishes it at all.</summary>
    Obsolete
}

/// <summary>Questions about a <see cref="StandardPublicationStatus"/>, answered in one place.</summary>
/// <remarks>
/// Every answer here is a restatement of what the publisher said. None is
/// an engineering recommendation: nothing in A2 says whether a standard
/// may be used for new design, which is a judgement resting on contract,
/// regulation and customer requirements that A2 knows nothing about.
/// </remarks>
public static class StandardPublicationStatuses
{
    /// <summary>Every status, in lifecycle order.</summary>
    public static IReadOnlyList<StandardPublicationStatus> All { get; } =
    [
        StandardPublicationStatus.Unknown,
        StandardPublicationStatus.Draft,
        StandardPublicationStatus.Current,
        StandardPublicationStatus.Amended,
        StandardPublicationStatus.Superseded,
        StandardPublicationStatus.Withdrawn,
        StandardPublicationStatus.Obsolete,
    ];

    /// <summary>Whether the publisher's own position is recorded at all.</summary>
    public static bool IsKnown(StandardPublicationStatus status) => status != StandardPublicationStatus.Unknown;

    /// <summary>Whether the issuing body still holds the standard current.</summary>
    public static bool IsCurrent(StandardPublicationStatus status) =>
        status is StandardPublicationStatus.Current or StandardPublicationStatus.Amended;

    /// <summary>Whether the issuing body has taken the standard out of force, however it described doing so.</summary>
    public static bool IsNoLongerInForce(StandardPublicationStatus status) =>
        status is StandardPublicationStatus.Superseded or StandardPublicationStatus.Withdrawn or StandardPublicationStatus.Obsolete;

    /// <summary>Whether a withdrawal date is a meaningful thing for a standard in this state to carry.</summary>
    public static bool ExpectsWithdrawalDate(StandardPublicationStatus status) => IsNoLongerInForce(status);
}
