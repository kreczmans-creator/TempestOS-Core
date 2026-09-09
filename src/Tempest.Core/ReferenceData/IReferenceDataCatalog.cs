using Tempest.Core.EngineeringData;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// The operations every Group A reference-data catalogue offers, whatever
/// it holds.
/// </summary>
/// <remarks>
/// <para>
/// Register, retrieve, revise, govern, and read history — the parts that
/// are genuinely the same in every domain. Domain-specific querying,
/// comparison and validation are <em>not</em> here: a bearing query and a
/// standards query have nothing in common but the word, and forcing them
/// through one interface would produce a shape that fits neither. Each
/// library adds its own.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No selection, no
/// calculation, no suitability judgement, and no commercial data. P01
/// supplies the reference evidence those capabilities will consume; the
/// boundaries are documented per library under
/// <c>docs/architecture/</c>.
/// </para>
/// </remarks>
/// <typeparam name="TDefinition">The domain's own engineering description type.</typeparam>
public interface IReferenceDataCatalog<TDefinition>
    where TDefinition : class
{
    /// <summary>The library's own name, as it appears in diagnostics and exceptions (e.g. <c>"Bearings"</c>).</summary>
    string LibraryName { get; }

    /// <summary>
    /// Registers a new record, in <see cref="ReferenceValidationState.Draft"/>.
    /// </summary>
    /// <param name="recordId">The caller-assigned TempestOS identity to register under.</param>
    /// <param name="definition">The domain engineering description.</param>
    /// <param name="provenance">Where the data came from. Required — never optional, never fabricated.</param>
    /// <param name="cancellationToken">Cancels the registration.</param>
    /// <exception cref="ArgumentException"><paramref name="recordId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="provenance"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateReferenceRecordException"><paramref name="recordId"/> is already registered.</exception>
    /// <exception cref="DuplicateReferenceKeyException">Another record already holds this library's own secondary uniqueness key for <paramref name="definition"/>.</exception>
    Task<IReferenceRecord<TDefinition>> RegisterAsync(
        string recordId,
        TDefinition definition,
        ReferenceProvenance provenance,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the record, or <see langword="null"/> if none is registered under <paramref name="recordId"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="recordId"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<TDefinition>?> FindAsync(string recordId, CancellationToken cancellationToken = default);

    /// <summary>Every registered record, ordered by <see cref="IReferenceRecord{TDefinition}.Id"/> (ordinal). Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IReferenceRecord<TDefinition>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new revision of a record's own engineering description
    /// and provenance. Identity and validation state are unaffected.
    /// </summary>
    /// <param name="recordId">The record to revise.</param>
    /// <param name="definition">The revised engineering description.</param>
    /// <param name="provenance">The revised provenance — a corrected value usually comes from a different place in the source, or a different revision of it, so this is revised alongside the content rather than left behind.</param>
    /// <param name="changeSummary">Why the values changed — the "why" the revision history would otherwise lack. Optional, but a reference-data change without one is a change nobody can later explain.</param>
    /// <param name="cancellationToken">Cancels the revision.</param>
    /// <exception cref="ReferenceRecordNotFoundException"><paramref name="recordId"/> does not exist.</exception>
    /// <exception cref="ReleasedReferenceImmutableException">The record is <see cref="ReferenceValidationState.Released"/> or <see cref="ReferenceValidationState.Superseded"/>. Supersede it instead.</exception>
    /// <exception cref="DuplicateReferenceKeyException">The revision would collide with another record's own secondary key.</exception>
    Task<IReferenceRecord<TDefinition>> ReviseAsync(
        string recordId,
        TDefinition definition,
        ReferenceProvenance provenance,
        string? changeSummary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a record to <paramref name="state"/>, enforcing both the
    /// permitted-transition table and the provenance each state requires.
    /// </summary>
    /// <param name="recordId">The record to transition.</param>
    /// <param name="state">The state requested.</param>
    /// <param name="changeSummary">Why the state changed.</param>
    /// <param name="cancellationToken">Cancels the transition.</param>
    /// <exception cref="ReferenceRecordNotFoundException"><paramref name="recordId"/> does not exist.</exception>
    /// <exception cref="InvalidReferenceStateTransitionException">The transition is not permitted from the record's own current state.</exception>
    /// <exception cref="ReferenceProvenanceIncompleteException">The record's own provenance does not support the requested state.</exception>
    /// <exception cref="ArgumentException"><paramref name="state"/> is <see cref="ReferenceValidationState.Superseded"/> — use <see cref="SupersedeAsync"/>, which also records the replacement.</exception>
    Task<IReferenceRecord<TDefinition>> SetValidationStateAsync(
        string recordId,
        ReferenceValidationState state,
        string? changeSummary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a released record superseded by another, and records a
    /// <c>supersedes</c> reference
    /// (<see cref="EngineeringDomain.GovernanceRelationshipKinds.Supersedes"/>)
    /// from the replacement's own document to this one — the direction
    /// this platform already uses for supersession. Neither record is
    /// deleted or edited: the superseded values remain readable, which is
    /// the whole point.
    /// </summary>
    /// <param name="recordId">The record being superseded.</param>
    /// <param name="replacementRecordId">The record that replaces it. Must already be registered, and must not be <paramref name="recordId"/> itself.</param>
    /// <param name="changeSummary">Why the record was superseded.</param>
    /// <param name="cancellationToken">Cancels the supersession.</param>
    /// <exception cref="ReferenceRecordNotFoundException">Either record does not exist.</exception>
    /// <exception cref="InvalidReferenceStateTransitionException">The record is not in a state from which supersession is permitted.</exception>
    /// <exception cref="ArgumentException"><paramref name="replacementRecordId"/> is <paramref name="recordId"/>.</exception>
    Task<IReferenceRecord<TDefinition>> SupersedeAsync(
        string recordId,
        string replacementRecordId,
        string? changeSummary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every revision of a record's own backing document, oldest first —
    /// what changed, when, by whom, and (where a change summary was given)
    /// why. Never <see langword="null"/>.
    /// </summary>
    /// <exception cref="ReferenceRecordNotFoundException"><paramref name="recordId"/> does not exist.</exception>
    Task<IReadOnlyList<IDocumentRevision>> GetHistoryAsync(string recordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The record as it stood at <paramref name="revisionNumber"/> — how an
    /// engineering value that has since changed can still be read back
    /// exactly as a past calculation saw it.
    /// </summary>
    /// <exception cref="ReferenceRecordNotFoundException"><paramref name="recordId"/> does not exist.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revisionNumber"/> is not a revision this record has.</exception>
    Task<IReferenceRecord<TDefinition>> GetRevisionAsync(string recordId, int revisionNumber, CancellationToken cancellationToken = default);
}
