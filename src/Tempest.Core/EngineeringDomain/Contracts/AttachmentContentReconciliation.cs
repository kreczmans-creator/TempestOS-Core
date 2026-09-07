namespace Tempest.Core.EngineeringDomain;

/// <summary>One content record <see cref="IAttachmentContentReconciliationService"/> found nothing referencing (`TD-97`).</summary>
/// <param name="AttachmentId">The attachment Id the orphaned content record is keyed by.</param>
/// <param name="Collected">Whether <see cref="IAttachmentContentReconciliationService.SweepAsync"/> deleted this record.</param>
public sealed record OrphanedAttachmentContent(Guid AttachmentId, bool Collected);

/// <summary>The complete result of one <see cref="IAttachmentContentReconciliationService"/> pass.</summary>
/// <param name="Orphans">Every orphaned content record this pass found. Empty if every stored record is referenced.</param>
/// <param name="SkippedByMarker">
/// Every unreferenced content record this pass declined to collect because
/// a write-intent marker still protects it (`WP 16.4B-R2`). Empty on a
/// healthy store.
/// </param>
/// <remarks>
/// <b>Why <paramref name="SkippedByMarker"/> exists (`WP 16.4B-R6`).</b>
/// A marker that outlives the attach it was protecting leaves bytes this
/// sweep must refuse to collect for ever. That outcome is deliberate and
/// bounded — `TD-97`'s disclosed leak, and always the safe end of the
/// trade against deleting content something still believes in — but until
/// this member existed it was also completely <em>unobservable</em>: the
/// sweep skipped the key with no report entry and no log line, while a
/// collected orphan got both, so the only way to find one was to read the
/// marker collection by hand. The fifth review board raised that as its
/// own finding, and a bounded leak nobody can see is indistinguishable
/// from a leak nobody has. The sweep already computes this set exactly;
/// it now reports it.
/// </remarks>
public sealed record AttachmentContentReconciliationReport(
    IReadOnlyList<OrphanedAttachmentContent> Orphans,
    IReadOnlyList<Guid> SkippedByMarker);

/// <summary>
/// The sweep `TD-97`'s own register entry names: "a sweep comparing
/// content keys against live attachment Ids closes it whenever disk cost
/// justifies one." Compares every content record
/// <see cref="IAttachmentContentStore"/> holds against every attachment
/// Id any currently-persisted <see cref="EngineeringObjectState"/>
/// references — live or soft-deleted alike, since a deleted object's own
/// attachment <em>metadata</em> is never erased (only its content is
/// released, by <see cref="EngineeringObjectBase.DeleteAsync"/>, at
/// delete time) — and reports, or collects, whatever nothing references.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this, and not a reference count on write.</b>
/// <c>ADR-0114</c> Decision 4 deliberately writes attachment content
/// before the metadata that names it, so a crash in between leaves
/// exactly this: bytes nothing references. `ADR-0114`'s own Consequences
/// section names the sweep as the closure, not a change to that ordering
/// — this type is that sweep, never a reversal of the decision it
/// implements.
/// </para>
/// <para>
/// <b>An explicit, operator-invoked repair, never an automatic one</b> —
/// mirroring <see cref="Requirements.IRequirementsReconciliationService"/>'s
/// own identical discipline for the sibling `TD-67` sweep: neither method
/// runs at startup or on any other schedule, and collecting content is
/// only ever a caller's own deliberate act.
/// </para>
/// </remarks>
public interface IAttachmentContentReconciliationService
{
    /// <summary>Scans every stored attachment content record without deleting anything.</summary>
    Task<AttachmentContentReconciliationReport> DetectAsync(CancellationToken cancellationToken = default);

    /// <summary>Repeats <see cref="DetectAsync"/>'s own scan and deletes every orphaned content record it finds.</summary>
    Task<AttachmentContentReconciliationReport> SweepAsync(CancellationToken cancellationToken = default);
}
