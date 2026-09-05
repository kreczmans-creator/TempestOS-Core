using Tempest.Core.EngineeringData;

namespace Tempest.Core.Bearings;

/// <summary>
/// The authoritative catalogue of bearing reference data — register,
/// retrieve, revise, govern, query and compare.
/// </summary>
/// <remarks>
/// <para>
/// Each bearing record is itself an <see cref="IEngineeringDocument"/> of
/// <c>Kind = "BearingReference"</c>; this catalogue is an indexed, typed
/// view over that shared store, never a second storage mechanism
/// (`ADR-0055`'s own precedent, continuing `ADR-0053`).
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No bearing
/// selection, no life or load calculation, no suitability judgement, and
/// no supplier or price data. A4 supplies the reference evidence those
/// capabilities will consume; the boundaries are documented in
/// `docs/architecture/A4 Bearing Library.md`.
/// </para>
/// </remarks>
public interface IBearingCatalog
{
    /// <summary>
    /// Registers a new bearing reference record, in
    /// <see cref="BearingValidationState.Draft"/>.
    /// </summary>
    /// <param name="bearingId">The caller-assigned TempestOS identity to register this bearing under.</param>
    /// <param name="definition">The bearing's own canonical engineering description.</param>
    /// <param name="cancellationToken">Cancels the registration.</param>
    /// <exception cref="ArgumentException"><paramref name="bearingId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateBearingException"><paramref name="bearingId"/> is already registered.</exception>
    /// <exception cref="DuplicateBearingPartNumberException">Another record already carries this manufacturer and manufacturer part number.</exception>
    Task<IBearing> RegisterAsync(string bearingId, BearingDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Returns the bearing, or <see langword="null"/> if none is registered under <paramref name="bearingId"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="bearingId"/> is null, empty, or whitespace.</exception>
    Task<IBearing?> FindAsync(string bearingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the bearing registered under <paramref name="manufacturer"/>
    /// and <paramref name="partNumber"/> (matched ignoring case and
    /// surrounding whitespace), or <see langword="null"/> if none is.
    /// </summary>
    /// <exception cref="ArgumentException">Either argument is null, empty, or whitespace.</exception>
    Task<IBearing?> FindByPartNumberAsync(string manufacturer, string partNumber, CancellationToken cancellationToken = default);

    /// <summary>Every registered bearing, ordered by <see cref="IBearing.BearingId"/> (ordinal). Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IBearing>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every registered bearing matching <paramref name="query"/>, in the
    /// same order <see cref="ListAsync"/> uses. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IBearing>> SearchAsync(BearingQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new revision of a bearing's own engineering description.
    /// The record's own identity and validation state are unaffected; only
    /// <paramref name="definition"/> changes.
    /// </summary>
    /// <param name="bearingId">The bearing to revise.</param>
    /// <param name="definition">The revised engineering description.</param>
    /// <param name="changeSummary">Why the values changed — the "why" the revision history would otherwise lack. Optional, but a reference-data change without one is a change nobody can later explain.</param>
    /// <param name="cancellationToken">Cancels the revision.</param>
    /// <exception cref="BearingNotFoundException"><paramref name="bearingId"/> does not exist.</exception>
    /// <exception cref="ReleasedBearingImmutableException">The record is <see cref="BearingValidationState.Released"/> or <see cref="BearingValidationState.Superseded"/>. Supersede it instead.</exception>
    /// <exception cref="DuplicateBearingPartNumberException">The revision would collide with another record's own manufacturer and part number.</exception>
    Task<IBearing> ReviseAsync(string bearingId, BearingDefinition definition, string? changeSummary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a record to <paramref name="state"/>, enforcing both the
    /// permitted-transition table and the provenance each state requires.
    /// </summary>
    /// <param name="bearingId">The bearing to transition.</param>
    /// <param name="state">The state requested.</param>
    /// <param name="changeSummary">Why the state changed.</param>
    /// <param name="cancellationToken">Cancels the transition.</param>
    /// <exception cref="BearingNotFoundException"><paramref name="bearingId"/> does not exist.</exception>
    /// <exception cref="InvalidBearingValidationStateTransitionException">The transition is not permitted from the record's own current state.</exception>
    /// <exception cref="BearingProvenanceIncompleteException">The record's own provenance does not support the requested state.</exception>
    /// <exception cref="ArgumentException"><paramref name="state"/> is <see cref="BearingValidationState.Superseded"/> — use <see cref="SupersedeAsync"/>, which also records the replacement.</exception>
    Task<IBearing> SetValidationStateAsync(string bearingId, BearingValidationState state, string? changeSummary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a released record superseded by another, and records a
    /// <c>supersedes</c> reference
    /// (<see cref="EngineeringDomain.GovernanceRelationshipKinds.Supersedes"/>)
    /// from the replacement's own document to this one — the direction
    /// this platform already uses for supersession, rather than a second
    /// bearing-specific relationship kind for the same concept
    /// (`ADR-0073`). Neither record is deleted or edited: the superseded
    /// values remain readable, which is the whole point.
    /// </summary>
    /// <param name="bearingId">The bearing being superseded.</param>
    /// <param name="replacementBearingId">The bearing that replaces it. Must already be registered, and must not be <paramref name="bearingId"/> itself.</param>
    /// <param name="changeSummary">Why the record was superseded.</param>
    /// <param name="cancellationToken">Cancels the supersession.</param>
    /// <exception cref="BearingNotFoundException"><paramref name="bearingId"/> or <paramref name="replacementBearingId"/> does not exist.</exception>
    /// <exception cref="InvalidBearingValidationStateTransitionException">The record is not in a state from which supersession is permitted.</exception>
    /// <exception cref="ArgumentException"><paramref name="replacementBearingId"/> is <paramref name="bearingId"/>.</exception>
    Task<IBearing> SupersedeAsync(string bearingId, string replacementBearingId, string? changeSummary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every revision of a bearing's own backing document, oldest first —
    /// what changed, when, by whom, and (where a change summary was given)
    /// why. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="BearingNotFoundException"><paramref name="bearingId"/> does not exist.</exception>
    Task<IReadOnlyList<IDocumentRevision>> GetHistoryAsync(string bearingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The bearing's own record as it stood at <paramref name="revisionNumber"/>
    /// — how an engineering value that has since changed can still be read
    /// back exactly as a past calculation saw it.
    /// </summary>
    /// <exception cref="BearingNotFoundException"><paramref name="bearingId"/> does not exist.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revisionNumber"/> is not a revision this record has.</exception>
    Task<IBearing> GetRevisionAsync(string bearingId, int revisionNumber, CancellationToken cancellationToken = default);
}
