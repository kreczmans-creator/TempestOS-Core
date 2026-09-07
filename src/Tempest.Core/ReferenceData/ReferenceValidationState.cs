using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// A reference-data record's own position in the Group A validation
/// lifecycle — one vocabulary, shared by every P01 library.
/// </summary>
/// <remarks>
/// A family-specific specialisation of the platform's own canonical
/// <see cref="LifecycleState"/> vocabulary, exactly as `ADR-0074`
/// prescribes and as <see cref="Requirements.RequirementStatus"/> already
/// does for requirements — not a competing, parallel state model. Shared
/// across P01 rather than restated per library, because the governance
/// question ("has a person checked this against its source, and may
/// engineering work rely on it?") is identical in every domain even where
/// the engineering content is not.
/// </remarks>
public enum ReferenceValidationState
{
    /// <summary>Recorded, not yet checked by anyone. Every new record starts here.</summary>
    Draft,

    /// <summary>A second person has checked the record's own values against the source it cites.</summary>
    Checked,

    /// <summary>The record has passed its own library's data-quality rules and is fit for engineering use.</summary>
    Validated,

    /// <summary>Released as authoritative reference data. Immutable: a released record is superseded, never edited.</summary>
    Released,

    /// <summary>Replaced by a later record. Retained, never deleted — the history of an engineering reference value is itself engineering data.</summary>
    Superseded
}

/// <summary>The permitted <see cref="ReferenceValidationState"/> transitions, the canonical vocabulary each state maps onto, and the provenance each state requires.</summary>
/// <remarks>
/// A contractual state model, not workflow automation — nothing here
/// decides *when* a transition should happen, only whether a requested one
/// is permitted and whether the record's own provenance supports it.
/// </remarks>
public static class ReferenceValidationStates
{
    private static readonly IReadOnlyDictionary<ReferenceValidationState, IReadOnlySet<ReferenceValidationState>> Permitted =
        new Dictionary<ReferenceValidationState, IReadOnlySet<ReferenceValidationState>>
        {
            // Down-transitions are permitted deliberately: a check or a
            // validation that finds a defect must be able to send a record
            // back to Draft, or the only way to correct it would be to
            // abandon the record and its history with it.
            [ReferenceValidationState.Draft] = new HashSet<ReferenceValidationState> { ReferenceValidationState.Checked },
            [ReferenceValidationState.Checked] = new HashSet<ReferenceValidationState> { ReferenceValidationState.Draft, ReferenceValidationState.Validated },
            [ReferenceValidationState.Validated] = new HashSet<ReferenceValidationState> { ReferenceValidationState.Checked, ReferenceValidationState.Released },

            // Released is terminal but for supersession: a released
            // reference value is never edited and never demoted, because
            // downstream engineering work has already consumed it.
            [ReferenceValidationState.Released] = new HashSet<ReferenceValidationState> { ReferenceValidationState.Superseded },
            [ReferenceValidationState.Superseded] = new HashSet<ReferenceValidationState>(),
        };

    /// <summary>
    /// Whether transitioning from <paramref name="from"/> to
    /// <paramref name="to"/> is permitted. A same-to-same request is
    /// deliberately not special-cased — it is permitted only where the
    /// table itself lists it (nowhere), mirroring
    /// <c>RequirementStatusTransitions.IsPermitted</c>'s own choice.
    /// </summary>
    public static bool IsPermitted(ReferenceValidationState from, ReferenceValidationState to) =>
        Permitted[from].Contains(to);

    /// <summary>Every state reachable in one transition from <paramref name="from"/>. Never <see langword="null"/>; empty for a terminal state.</summary>
    public static IReadOnlyList<ReferenceValidationState> GetPermittedTargets(ReferenceValidationState from) =>
        Permitted[from].Order().ToList();

    /// <summary>The platform-wide canonical <see cref="LifecycleState"/> this reference-data state corresponds to (`ADR-0074`).</summary>
    public static LifecycleState CanonicalEquivalent(ReferenceValidationState state) => state switch
    {
        ReferenceValidationState.Draft => LifecycleState.Draft,
        ReferenceValidationState.Checked => LifecycleState.InReview,
        ReferenceValidationState.Validated => LifecycleState.Approved,
        ReferenceValidationState.Released => LifecycleState.Released,
        ReferenceValidationState.Superseded => LifecycleState.Superseded,
        _ => LifecycleState.Draft
    };

    /// <summary>Whether a record in <paramref name="state"/> is authoritative, released engineering reference data.</summary>
    public static bool IsReleased(ReferenceValidationState state) => state == ReferenceValidationState.Released;

    /// <summary>Whether a record in <paramref name="state"/> may still have its own engineering content revised.</summary>
    public static bool IsRevisable(ReferenceValidationState state) =>
        state is ReferenceValidationState.Draft or ReferenceValidationState.Checked or ReferenceValidationState.Validated;

    /// <summary>
    /// Returns why <paramref name="provenance"/> cannot support
    /// <paramref name="state"/>, or <see langword="null"/> if it can — the
    /// single enforcement point for P01's own central rule: reference data
    /// earns its status from its provenance, never from a caller asserting
    /// one.
    /// </summary>
    public static string? DescribeProvenanceShortfall(ReferenceProvenance provenance, ReferenceValidationState state)
    {
        ArgumentNullException.ThrowIfNull(provenance);

        if (state == ReferenceValidationState.Draft)
            return null;

        if (!provenance.IdentifiesASource)
            return "its provenance names neither a source organisation nor a source document, so nothing about it can be checked.";

        if (state == ReferenceValidationState.Released && !provenance.IsVerified)
            return "release requires provenance verified against the source by a named reviewer on a recorded date; being imported is not being verified.";

        return null;
    }
}
