using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.EngineeringDomain;

/// <summary>The concrete <see cref="IAttachmentContentReconciliationService"/> implementation (`TD-97`).</summary>
/// <remarks>
/// Lists content keys through <see cref="IPersistenceStore"/> rather than
/// <see cref="IAttachmentContentStore"/> itself: enumerating a
/// collection's own keys never depends on whether the records in it are
/// text or bytes (<see cref="IPersistenceStore.ListKeysAsync"/> and
/// <see cref="IBinaryPersistenceStore"/>'s own read/write pair share the
/// identical file-per-key store, differing only at the point content is
/// actually read or written) — the same direct
/// <see cref="IPersistenceStore"/> dependency <c>Materials.MaterialCatalog</c>
/// and <c>Requirements.RequirementsService</c> already have for their own
/// sibling indexes, applied here to list a collection neither of
/// <see cref="IAttachmentContentStore"/>'s own two members otherwise
/// exposes.
/// </remarks>
public sealed class AttachmentContentReconciliationService : IAttachmentContentReconciliationService
{
    private readonly IPersistenceStore _persistenceStore;
    private readonly IEngineeringObjectStateStore _objectStateStore;
    private readonly IAttachmentContentStore _attachmentContentStore;
    private readonly IAttachmentWriteIntentStore? _writeIntentStore;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="AttachmentContentReconciliationService"/> class.</summary>
    /// <param name="persistenceStore">Lists the raw content keys this service reconciles.</param>
    /// <param name="objectStateStore">The authoritative side: every persisted object's attachment references.</param>
    /// <param name="attachmentContentStore">Deletes a collected orphan's bytes.</param>
    /// <param name="writeIntentStore">
    /// The write-intent markers this sweep skips content for (`WP 16.4B-R2`)
    /// — <see langword="null"/> when this domain has none configured, in
    /// which case every content record nothing references is treated as a
    /// genuine orphan exactly as before that Work Package. Never required:
    /// the production Host always supplies one alongside
    /// <paramref name="attachmentContentStore"/>.
    /// </param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="persistenceStore"/>, <paramref name="objectStateStore"/>, or <paramref name="attachmentContentStore"/> is <see langword="null"/>.
    /// </exception>
    public AttachmentContentReconciliationService(
        IPersistenceStore persistenceStore,
        IEngineeringObjectStateStore objectStateStore,
        IAttachmentContentStore attachmentContentStore,
        IAttachmentWriteIntentStore? writeIntentStore = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);
        ArgumentNullException.ThrowIfNull(objectStateStore);
        ArgumentNullException.ThrowIfNull(attachmentContentStore);

        _persistenceStore = persistenceStore;
        _objectStateStore = objectStateStore;
        _attachmentContentStore = attachmentContentStore;
        _writeIntentStore = writeIntentStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<AttachmentContentReconciliationReport> DetectAsync(CancellationToken cancellationToken = default) =>
        RunAsync(collect: false, cancellationToken);

    /// <inheritdoc />
    public Task<AttachmentContentReconciliationReport> SweepAsync(CancellationToken cancellationToken = default) =>
        RunAsync(collect: true, cancellationToken);

    /// <remarks>
    /// <b>Read order and the write-intent marker (`WP 16.4B-R2`), together.</b>
    /// <c>ADR-0114</c> Decision 4 writes an attachment's content before the
    /// object state that references it, so there is always a real window
    /// in which content exists and no state yet names it. Two, independent
    /// changes address that window:
    /// <list type="number">
    /// <item>
    /// <b>Content keys are read before object states</b> — the reverse of
    /// this sweep's original order. Reading states first and content
    /// second (the original order) widened the window: any content
    /// written between the two reads is guaranteed to look orphaned to
    /// this pass. Reading content first and states second narrows it —
    /// content still in flight at the moment of the content-key read
    /// gets one more moment, right up to the state read, for its own
    /// state write to land — but does not close it: a state write can
    /// always land after this sweep's own state read, no matter how late
    /// that read runs.
    /// </item>
    /// <item>
    /// <b>The marker closes what the reorder only narrows.</b> Any
    /// content key still marked in-flight (<see cref="IAttachmentWriteIntentStore.ListMarkedAsync"/>)
    /// is skipped outright, regardless of what either scan saw. Marking
    /// happens before the content write and is cleared only after the
    /// state write succeeds, so the marker is guaranteed to be visible
    /// for the attachment's entire exposure window — the reorder above
    /// narrows how often the marker is the thing actually doing the
    /// work, not whether the combination is safe.
    /// </item>
    /// </list>
    /// </remarks>
    private async Task<AttachmentContentReconciliationReport> RunAsync(bool collect, CancellationToken cancellationToken)
    {
        // Derived side first: every stored content record.
        var contentKeys = await _persistenceStore.ListKeysAsync(AttachmentContentStore.ContentCollectionName, cancellationToken).ConfigureAwait(false);

        // Authoritative side second: every persisted object's attachment references.
        var states = await _objectStateStore.ListAsync(cancellationToken).ConfigureAwait(false);

        var referencedAttachmentIds = new HashSet<Guid>();
        foreach (var state in states)
            foreach (var attachment in state.Attachments)
                referencedAttachmentIds.Add(attachment.Id);

        // A content record still marked in-flight is never a candidate for
        // collection, no matter what either scan above saw — this is what
        // actually closes the race the reorder above only narrows.
        var markedAttachmentIds = _writeIntentStore is not null
            ? await _writeIntentStore.ListMarkedAsync(cancellationToken).ConfigureAwait(false)
            : (IReadOnlySet<Guid>)new HashSet<Guid>();

        var orphans = new List<OrphanedAttachmentContent>();

        foreach (var key in contentKeys)
        {
            if (!Guid.TryParseExact(key, "N", out var attachmentId))
            {
                _logger?.Warning($"Ignoring non-content key '{key}' in '{AttachmentContentStore.ContentCollectionName}'.");
                continue;
            }

            if (referencedAttachmentIds.Contains(attachmentId))
                continue;

            if (markedAttachmentIds.Contains(attachmentId))
                continue;

            var collected = false;
            if (collect)
            {
                await _attachmentContentStore.DeleteAsync(attachmentId, cancellationToken).ConfigureAwait(false);
                collected = true;
                _logger?.Information($"Collected orphaned attachment content '{attachmentId}'.");
            }

            orphans.Add(new OrphanedAttachmentContent(attachmentId, collected));
        }

        return new AttachmentContentReconciliationReport(orphans);
    }
}
