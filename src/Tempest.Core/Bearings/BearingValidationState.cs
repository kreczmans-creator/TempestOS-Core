using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Bearings;

/// <summary>
/// A bearing reference record's own position in this library's own
/// validation lifecycle.
/// </summary>
/// <remarks>
/// A family-specific specialisation of the platform's own canonical
/// <see cref="LifecycleState"/> vocabulary, exactly as `ADR-0074`
/// prescribes and as <see cref="Requirements.RequirementStatus"/> already
/// does for requirements — not a competing, parallel state model.
/// <see cref="BearingValidationStates.CanonicalEquivalent"/> is the
/// mapping, so a future cross-discipline read can treat a released bearing
/// and a released requirement alike without this library pretending its
/// own reference-data lifecycle is the same set of words.
/// </remarks>
public enum BearingValidationState
{
    /// <summary>Recorded, not yet checked by anyone. Every new record starts here.</summary>
    Draft,

    /// <summary>A second person has checked the record's own values against the source it cites.</summary>
    Checked,

    /// <summary>The record has passed this library's own data-quality rules and is fit for engineering use.</summary>
    Validated,

    /// <summary>Released as authoritative reference data. Immutable: a released record is superseded, never edited.</summary>
    Released,

    /// <summary>Replaced by a later record. Retained, never deleted — the history of an engineering reference value is itself engineering data.</summary>
    Superseded
}

/// <summary>The permitted <see cref="BearingValidationState"/> transitions, and the canonical vocabulary each state maps onto.</summary>
/// <remarks>
/// A contractual state model, not workflow automation — nothing here
/// decides *when* a transition should happen, only whether a requested one
/// is permitted. Mirrors <c>RequirementStatusTransitions</c>'s own shape,
/// but is public rather than internal: unlike a requirement's own status,
/// a bearing's own validation state is reference-data governance a caller
/// legitimately needs to reason about before requesting a transition.
/// </remarks>
public static class BearingValidationStates
{
    private static readonly IReadOnlyDictionary<BearingValidationState, IReadOnlySet<BearingValidationState>> Permitted =
        new Dictionary<BearingValidationState, IReadOnlySet<BearingValidationState>>
        {
            // Down-transitions are permitted deliberately: a check or a
            // validation that finds a defect must be able to send a record
            // back to Draft, or the only way to correct it would be to
            // abandon the record and its history with it.
            [BearingValidationState.Draft] = new HashSet<BearingValidationState> { BearingValidationState.Checked },
            [BearingValidationState.Checked] = new HashSet<BearingValidationState> { BearingValidationState.Draft, BearingValidationState.Validated },
            [BearingValidationState.Validated] = new HashSet<BearingValidationState> { BearingValidationState.Checked, BearingValidationState.Released },

            // Released is terminal but for supersession: a released
            // reference value is never edited and never demoted, because
            // downstream engineering work has already consumed it.
            [BearingValidationState.Released] = new HashSet<BearingValidationState> { BearingValidationState.Superseded },
            [BearingValidationState.Superseded] = new HashSet<BearingValidationState>(),
        };

    /// <summary>
    /// Whether transitioning from <paramref name="from"/> to
    /// <paramref name="to"/> is permitted. A same-to-same request is
    /// deliberately not special-cased — it is permitted only where the
    /// table itself lists it (nowhere), mirroring
    /// <c>RequirementStatusTransitions.IsPermitted</c>'s own choice.
    /// </summary>
    public static bool IsPermitted(BearingValidationState from, BearingValidationState to) =>
        Permitted[from].Contains(to);

    /// <summary>Every state reachable in one transition from <paramref name="from"/>. Never <see langword="null"/>; empty for a terminal state.</summary>
    public static IReadOnlyList<BearingValidationState> GetPermittedTargets(BearingValidationState from) =>
        Permitted[from].Order().ToList();

    /// <summary>
    /// The platform-wide canonical <see cref="LifecycleState"/> this
    /// reference-data state corresponds to (`ADR-0074`).
    /// </summary>
    public static LifecycleState CanonicalEquivalent(BearingValidationState state) => state switch
    {
        BearingValidationState.Draft => LifecycleState.Draft,
        BearingValidationState.Checked => LifecycleState.InReview,
        BearingValidationState.Validated => LifecycleState.Approved,
        BearingValidationState.Released => LifecycleState.Released,
        BearingValidationState.Superseded => LifecycleState.Superseded,
        _ => LifecycleState.Draft
    };

    /// <summary>
    /// Whether a record in <paramref name="state"/> is authoritative,
    /// released engineering reference data — the distinction downstream
    /// engineering work must be able to make without interpreting the
    /// enum itself.
    /// </summary>
    public static bool IsReleased(BearingValidationState state) => state == BearingValidationState.Released;

    /// <summary>Whether a record in <paramref name="state"/> may still have its own engineering content revised.</summary>
    public static bool IsRevisable(BearingValidationState state) =>
        state is BearingValidationState.Draft or BearingValidationState.Checked or BearingValidationState.Validated;
}
