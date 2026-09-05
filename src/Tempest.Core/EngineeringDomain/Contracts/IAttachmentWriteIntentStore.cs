namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Durable write-intent markers for attachment content — the closure the
/// release plan asked for (`WP 16.4B-R2`) for the race
/// <see cref="AttachmentContentReconciliationService"/>'s sweep could
/// otherwise be caught in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The race this exists to close.</b> <c>ADR-0114</c> Decision 4 writes
/// attachment content before the object state that references it — on
/// purpose, so a crash between the two leaves unreferenced bytes rather
/// than an attachment promising content nobody stored. That deliberate
/// ordering means there is always a real window, between the content
/// write landing and the state write landing, in which the content exists
/// and nothing yet references it. A sweep that runs in that exact window
/// cannot tell "in flight" apart from "genuinely orphaned" by comparing
/// content keys to referenced attachment Ids alone — both look identical
/// from that vantage point.
/// </para>
/// <para>
/// <b>Why a marker, not a reordering, closes it.</b> Reordering the
/// sweep's own two reads (content keys before object states — see
/// <see cref="AttachmentContentReconciliationService"/>) narrows the
/// window but cannot close it: the sweep's state read still happens at
/// some fixed later instant, and a state write landing after that instant
/// is indistinguishable from one that will never land. A marker answers a
/// different, sufficient question directly — "is somebody in the middle of
/// writing this attachment right now" — that no comparison of two
/// snapshots can answer.
/// </para>
/// <para>
/// <b>Why a marker can only ever prevent a deletion, never cause one.</b>
/// The sweep's only use of this store is to skip a content key while it
/// carries a live marker. A marker that should not be there (mistakenly
/// set) only ever makes the sweep skip content it could have collected —
/// content already tolerated as leaked, disclosed as `TD-97`, before any
/// sweep existed. A marker that is missing when it should be present
/// simply leaves this store exactly as capable, or incapable, as it was
/// before this type existed. No code path reads a marker to decide
/// <em>to</em> delete something; only ever to decide not to.
/// </para>
/// <para>
/// <b>A stale marker is not an error state.</b> A crash between the
/// content write and the marker's own removal leaves a marker with
/// nothing left to protect against, forever (or until an operator clears
/// it by hand). Its only effect is that the content it names is never
/// collected — the same leaked-bytes outcome `TD-97` already tolerated,
/// now merely reached by a different path. Nothing reads a stale marker
/// as a fault.
/// </para>
/// </remarks>
public interface IAttachmentWriteIntentStore
{
    /// <summary>
    /// Records that content for <paramref name="attachmentId"/> is about
    /// to be written, before it is. Idempotent: marking an
    /// already-marked attachment simply refreshes the marker.
    /// </summary>
    Task MarkAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the marker for <paramref name="attachmentId"/>, once its
    /// content and the state that references it are both durably
    /// written. Idempotent: clearing a marker that is not there (or was
    /// already cleared) is not an error.
    /// </summary>
    Task ClearAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every attachment Id currently marked as being written. A sweep
    /// consults this before treating a content record as orphaned; no
    /// other caller has a reason to.
    /// </summary>
    Task<IReadOnlySet<Guid>> ListMarkedAsync(CancellationToken cancellationToken = default);
}
