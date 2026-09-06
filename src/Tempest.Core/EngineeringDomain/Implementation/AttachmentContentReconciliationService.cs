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
    /// <b>Read order and the write-intent marker (`WP 16.4B-R2`), together
    /// — and why the marker must be sampled <em>between</em> the two scans,
    /// never after both.</b> <c>ADR-0114</c> Decision 4 writes an
    /// attachment's content before the object state that references it,
    /// so there is always a real window in which content exists and no
    /// state yet names it. This method reads in exactly this order —
    /// content keys, <em>then</em> markers, <em>then</em> object states —
    /// and that middle position is load-bearing, not cosmetic.
    /// <para>
    /// <b>Content before states narrows the window; it does not close it.</b>
    /// Reading states first and content second (this sweep's original
    /// order) widened the window: any content written between the two
    /// reads was guaranteed to look orphaned. Reading content first
    /// merely gives an in-flight write one more moment to land before the
    /// state read — a state write can always still land after that read,
    /// however late it runs, so the reorder alone was never going to be a
    /// closure.
    /// </para>
    /// <para>
    /// <b>Sampling the marker last reopens the identical race, one read
    /// later.</b> Label this method's own three reads T1 (content), T2,
    /// T3, and the attachment's own lifecycle <c>Mark(m) → content(c) →
    /// state(s) → Clear(x)</c>, so <c>m &lt; c &lt; s &lt; x</c>. If markers
    /// were sampled last (T2 = states, T3 = markers — the shape this type
    /// originally shipped with), the interleaving <c>c &lt; T1 &lt; T2 &lt;
    /// s &lt; x &lt; T3</c> is consistent with that lifecycle: the content
    /// write lands before T1, the state write lands strictly after the
    /// state read at T2 (so the object still looks unreferenced), <c>Clear</c>
    /// then runs, and only after that does the marker read at T3 find
    /// nothing marked. Every one of this method's three checks —
    /// present, unreferenced, unmarked — is satisfied, and a fully
    /// live, now-referenced attachment's bytes are deleted. This is the
    /// original bug, reopened through a narrower window by the marker
    /// read's own position rather than closed by its existence.
    /// </para>
    /// <para>
    /// <b>Sampling the marker between the two scans (T2 = markers, T3 =
    /// states — the order below) is airtight.</b> For collection, the
    /// attachment must be unmarked at T2. Since <c>m &lt; c &lt; T1 &lt;
    /// T2</c>, the mark was already in place before T2, so "unmarked at
    /// T2" can only mean <c>Clear</c> already ran: <c>x &lt; T2</c>. But
    /// <c>s &lt; x</c> always (the lifecycle above), so <c>s &lt; x &lt;
    /// T2 &lt; T3</c> — the state write necessarily completed strictly
    /// before the state read at T3, which therefore sees it: the
    /// attachment is referenced, and collection never happens. There is
    /// no interleaving of this sweep's three reads against
    /// <see cref="EngineeringObjectBase.AttachContentAsync"/>'s four
    /// writes that deletes live content. This is the actual closure;
    /// the content-before-states reorder is a real, independent
    /// narrowing on top of it for the (no-marker-store-configured) case
    /// below, never the thing that makes this safe by itself.
    /// </para>
    /// </remarks>
    private async Task<AttachmentContentReconciliationReport> RunAsync(bool collect, CancellationToken cancellationToken)
    {
        // 1: every stored content record.
        var contentKeys = await _persistenceStore.ListKeysAsync(AttachmentContentStore.ContentCollectionName, cancellationToken).ConfigureAwait(false);

        // 2: every write-intent marker — sampled here, between the content
        // scan and the state scan, not after both. See this method's own
        // remarks: sampled anywhere else, this read stops closing the
        // race and only narrows it, exactly as content-before-states
        // alone does.
        var markedAttachmentIds = _writeIntentStore is not null
            ? await _writeIntentStore.ListMarkedAsync(cancellationToken).ConfigureAwait(false)
            : (IReadOnlySet<Guid>)new HashSet<Guid>();

        // 3: every persisted object's attachment references — the
        // authoritative side, read last.
        var states = await _objectStateStore.ListAsync(cancellationToken).ConfigureAwait(false);

        var referencedAttachmentIds = new HashSet<Guid>();
        foreach (var state in states)
            foreach (var attachment in state.Attachments)
                referencedAttachmentIds.Add(attachment.Id);

        var orphans = new List<OrphanedAttachmentContent>();
        var skippedByMarker = new List<Guid>();

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
            {
                // `WP 16.4B-R6`. Unreferenced content a marker protects is
                // an in-flight write or a leak, and this sweep cannot tell
                // which — so it still declines to collect it, exactly as
                // before. What changed is that it says so. See
                // `AttachmentContentReconciliationReport.SkippedByMarker`
                // for why silence here was itself a finding.
                skippedByMarker.Add(attachmentId);
                _logger?.Information(
                    $"Skipped unreferenced attachment content '{attachmentId}': a write-intent marker still protects it. " +
                    "This is an in-flight attach, or a marker stranded by an interrupted one.");
                continue;
            }

            var collected = false;
            if (collect)
            {
                await _attachmentContentStore.DeleteAsync(attachmentId, cancellationToken).ConfigureAwait(false);
                collected = true;
                _logger?.Information($"Collected orphaned attachment content '{attachmentId}'.");
            }

            orphans.Add(new OrphanedAttachmentContent(attachmentId, collected));
        }

        return new AttachmentContentReconciliationReport(orphans, skippedByMarker);
    }
}
