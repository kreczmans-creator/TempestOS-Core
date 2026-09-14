using Tempest.Core.ReferenceData;

namespace Tempest.Core.Evidence;

/// <summary>
/// The governed acts a piece of <see cref="Evidence"/> supports — create,
/// revise, cite, declare a figure, check and issue (`ADR-0148`). Every act
/// is one transaction with an audit row; an act whose permission depends on
/// something outside the object itself (a cited record's own validation
/// state, a status move, the independent-check rule) is decided here,
/// before <see cref="Evidence"/>'s own mutator ever runs, and reported back
/// as a refusal result rather than an exception — mirroring
/// <c>Tempest.Core.Calculations.GovernedBracketCheckService.CheckAsync</c>.
/// </summary>
public interface IEvidenceService
{
    /// <summary>Creates a new, empty piece of evidence in <see cref="EvidenceStatus.Draft"/>, under <paramref name="parentId"/> if given.</summary>
    /// <param name="parentId">Where the evidence goes — the open project, or a container the shell picked via <c>CreationPlacement</c>. <see langword="null"/> for a standalone, top-level record.</param>
    /// <param name="title">The evidence's own display name.</param>
    /// <param name="classification">What kind of engineering record this is.</param>
    /// <param name="subjectId">The Part, Assembly, Requirement or Deliverable this evidence is about, by id — a tag, never validated as a structure. Optional.</param>
    /// <exception cref="ArgumentException"><paramref name="title"/> is null, empty, or whitespace, or <paramref name="parentId"/> does not identify a live object.</exception>
    Task<Evidence> CreateAsync(Guid? parentId, string title, EvidenceClassification classification, Guid? subjectId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cites <paramref name="recordId"/> from <paramref name="library"/> against <paramref name="evidenceId"/>'s own evidence, pinned to the revision actually read.
    /// Refused, as a result rather than an exception, when the record is not registered or has not reached <see cref="ReferenceValidationState.Released"/>.
    /// </summary>
    Task<EvidenceCitationResult> CiteAsync(Guid evidenceId, string library, string recordId, CancellationToken cancellationToken = default);

    /// <summary>Removes the citation pinning <paramref name="pin"/>'s own record from <paramref name="evidenceId"/>'s own evidence, if it has one.</summary>
    /// <exception cref="ArgumentException"><paramref name="evidenceId"/> does not identify a known piece of evidence.</exception>
    Task<Evidence> RemoveCitationAsync(Guid evidenceId, ReferencePin pin, CancellationToken cancellationToken = default);

    /// <summary>Declares a named, typed figure against <paramref name="evidenceId"/>'s own evidence.</summary>
    /// <param name="quantity">The value, as <c>"&lt;value&gt; &lt;unit symbol&gt;"</c> text (`ADR-0147`) — checked against <see cref="EvidenceUnitCatalog.KnownUnits"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="evidenceId"/> does not identify a known piece of evidence, <paramref name="name"/> is null/empty/whitespace, or <paramref name="quantity"/> names a unit this platform does not know.</exception>
    Task<Evidence> DeclareFigureAsync(Guid evidenceId, string name, DeclaredFigureRole role, string quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a check against <paramref name="evidenceId"/>'s own evidence and moves it to <see cref="EvidenceStatus.Checked"/>.
    /// Refused, as a result, when the evidence is not <see cref="EvidenceStatus.Draft"/>, or — when <c>Evidence:IndependentCheck</c> is on — when the acting principal is this evidence's own author, or nobody is signed in.
    /// </summary>
    Task<EvidenceActionResult> RecordCheckAsync(Guid evidenceId, string checkerName, string checkerOrganisation, string statement, CheckOutcome outcome, CancellationToken cancellationToken = default);

    /// <summary>Records an issue against <paramref name="evidenceId"/>'s own evidence and moves it to <see cref="EvidenceStatus.Issued"/>. Refused, as a result, unless the evidence is <see cref="EvidenceStatus.Checked"/>.</summary>
    Task<EvidenceActionResult> IssueAsync(Guid evidenceId, string issueReference, string revision, string client, CancellationToken cancellationToken = default);

    /// <summary>Revises <paramref name="evidenceId"/>'s own Issued evidence: a new revision is created, and moved to <see cref="EvidenceStatus.Draft"/>, while the issued revision stays readable via its own revision history. Refused, as a result, unless the evidence is <see cref="EvidenceStatus.Issued"/>.</summary>
    Task<EvidenceActionResult> ReviseAsync(Guid evidenceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retags <paramref name="evidenceId"/>'s own evidence to
    /// <paramref name="subjectId"/> — the physical review's own "tag a
    /// record to a Part after creating it" step (`WP 18.2B`, closing a gap
    /// `WP 18.2A` disclosed). <see langword="null"/> clears the tag.
    /// Refused, as a result, once the evidence is <see cref="EvidenceStatus.Issued"/> —
    /// an issued record's own subject is part of what was issued;
    /// <see cref="ReviseAsync"/> it first.
    /// </summary>
    Task<EvidenceActionResult> SetSubjectAsync(Guid evidenceId, Guid? subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Points <paramref name="evidenceId"/>'s own <see cref="IssueRecord.IssueSheetAttachmentId"/>
    /// at <paramref name="issueSheetAttachmentId"/> — an already-stored
    /// attachment (via <see cref="Tempest.Core.EngineeringDomain.IHasAttachments.AttachContentAsync"/>)
    /// carrying the rendered issue sheet's own bytes (`WP 18.2B`). Plumbing,
    /// not a governed act of its own: the act that matters, <em>issuing</em>,
    /// already happened through <see cref="IssueAsync"/>; this only records
    /// where the sheet <see cref="IssueAsync"/> could not yet have rendered
    /// ended up.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="evidenceId"/> does not identify a known piece of evidence.</exception>
    /// <exception cref="InvalidOperationException">The evidence has not been issued — there is no <see cref="Evidence.Issue"/> record to attach a sheet to.</exception>
    Task<Evidence> RecordIssueSheetAsync(Guid evidenceId, Guid issueSheetAttachmentId, CancellationToken cancellationToken = default);
}
