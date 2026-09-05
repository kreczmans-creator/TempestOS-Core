using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.EngineeringDomain;

/// <summary>The concrete <see cref="IAttachmentWriteIntentStore"/> implementation (`WP 16.4B-R2`).</summary>
/// <remarks>
/// Writes through the platform's single <see cref="IPersistenceStore"/>,
/// its own collection, one record per attachment, keyed by the
/// attachment's own Id — the same substrate and the same shape
/// <see cref="EngineeringObjectStateStore"/> and
/// <see cref="AttachmentContentStore"/> already use, each in their own
/// collection. A marker's value carries no meaning of its own (a
/// diagnostic timestamp only, never parsed back); its presence or absence
/// under a given key is the entire fact this store exists to hold, which
/// is exactly why the plain text shape of <see cref="IPersistenceStore"/>
/// — not the byte shape a content record needs — is all a marker ever
/// requires.
/// </remarks>
public sealed class AttachmentWriteIntentStore : IAttachmentWriteIntentStore
{
    /// <summary>The <see cref="IPersistenceStore"/> collection attachment write-intent markers live in.</summary>
    public const string CollectionName = "EngineeringDomain.AttachmentWriteIntent";

    private readonly IPersistenceStore _persistenceStore;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="AttachmentWriteIntentStore"/> class.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    public AttachmentWriteIntentStore(IPersistenceStore persistenceStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);

        _persistenceStore = persistenceStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task MarkAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
        _persistenceStore.WriteAsync(CollectionName, KeyOf(attachmentId), DateTimeOffset.UtcNow.ToString("O"), cancellationToken);

    /// <inheritdoc />
    public Task ClearAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
        _persistenceStore.DeleteAsync(CollectionName, KeyOf(attachmentId), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> ListMarkedAsync(CancellationToken cancellationToken = default)
    {
        var keys = await _persistenceStore.ListKeysAsync(CollectionName, cancellationToken).ConfigureAwait(false);

        var marked = new HashSet<Guid>();
        foreach (var key in keys)
        {
            if (Guid.TryParseExact(key, "N", out var attachmentId))
                marked.Add(attachmentId);
            else
                _logger?.Warning($"Ignoring non-marker key '{key}' in '{CollectionName}'.");
        }

        return marked;
    }

    private static string KeyOf(Guid attachmentId) => attachmentId.ToString("N");
}
