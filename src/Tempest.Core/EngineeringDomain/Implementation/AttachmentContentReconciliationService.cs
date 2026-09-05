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
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="AttachmentContentReconciliationService"/> class.</summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="persistenceStore"/>, <paramref name="objectStateStore"/>, or <paramref name="attachmentContentStore"/> is <see langword="null"/>.
    /// </exception>
    public AttachmentContentReconciliationService(
        IPersistenceStore persistenceStore,
        IEngineeringObjectStateStore objectStateStore,
        IAttachmentContentStore attachmentContentStore,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);
        ArgumentNullException.ThrowIfNull(objectStateStore);
        ArgumentNullException.ThrowIfNull(attachmentContentStore);

        _persistenceStore = persistenceStore;
        _objectStateStore = objectStateStore;
        _attachmentContentStore = attachmentContentStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<AttachmentContentReconciliationReport> DetectAsync(CancellationToken cancellationToken = default) =>
        RunAsync(collect: false, cancellationToken);

    /// <inheritdoc />
    public Task<AttachmentContentReconciliationReport> SweepAsync(CancellationToken cancellationToken = default) =>
        RunAsync(collect: true, cancellationToken);

    private async Task<AttachmentContentReconciliationReport> RunAsync(bool collect, CancellationToken cancellationToken)
    {
        var states = await _objectStateStore.ListAsync(cancellationToken).ConfigureAwait(false);

        var referencedAttachmentIds = new HashSet<Guid>();
        foreach (var state in states)
            foreach (var attachment in state.Attachments)
                referencedAttachmentIds.Add(attachment.Id);

        var contentKeys = await _persistenceStore.ListKeysAsync(AttachmentContentStore.ContentCollectionName, cancellationToken).ConfigureAwait(false);

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
