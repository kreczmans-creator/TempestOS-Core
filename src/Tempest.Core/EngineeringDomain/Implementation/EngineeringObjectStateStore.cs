using System.Text.Json;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Persists and reads <see cref="EngineeringObjectState"/> — the durable
/// half of engineering-object rehydration (`TD-85`).
/// </summary>
/// <remarks>
/// <para>
/// Writes through the platform's single <see cref="IPersistenceStore"/>,
/// one record per object, keyed by the object's own Id — the same
/// substrate and shape <c>EngineeringDocumentStore</c> already uses for
/// documents and revisions. This introduces <b>no new storage
/// mechanism</b> and no second authority: the document remains the
/// object's identity and revision history; this record carries the object
/// state the document was never designed to hold.
/// </para>
/// <para>
/// A corrupted state record is skipped with a warning rather than failing
/// the whole rehydration, mirroring `TD-60`'s established discipline for
/// passive read paths — one unreadable object must not cost the user
/// every other object they own.
/// </para>
/// </remarks>
public sealed class EngineeringObjectStateStore : IEngineeringObjectStateStore
{
    /// <summary>The <see cref="IPersistenceStore"/> collection engineering object state lives in.</summary>
    public const string StateCollectionName = "EngineeringDomain.ObjectState";

    private readonly IPersistenceStore _persistenceStore;
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="EngineeringObjectStateStore"/> class.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="persistenceStore"/> is <see langword="null"/>.</exception>
    public EngineeringObjectStateStore(IPersistenceStore persistenceStore, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);

        _persistenceStore = persistenceStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _persistenceStore.WriteAsync(
            StateCollectionName,
            state.Id.ToString("N"),
            JsonSerializer.Serialize(state),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EngineeringObjectState?> FindAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var json = await _persistenceStore.ReadAsync(StateCollectionName, objectId.ToString("N"), cancellationToken).ConfigureAwait(false);
        return json is null ? null : Deserialise(objectId, json);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default)
    {
        var keys = await _persistenceStore.ListKeysAsync(StateCollectionName, cancellationToken).ConfigureAwait(false);
        var states = new List<EngineeringObjectState>(keys.Count);

        foreach (var key in keys)
        {
            if (!Guid.TryParseExact(key, "N", out var objectId))
            {
                // A foreign file beside the store's own is not a record.
                _logger?.Warning($"Ignoring non-state key '{key}' in '{StateCollectionName}'.");
                continue;
            }

            var json = await _persistenceStore.ReadAsync(StateCollectionName, key, cancellationToken).ConfigureAwait(false);
            if (json is null)
                continue;

            if (Deserialise(objectId, json) is { } state)
                states.Add(state);
        }

        return states;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid objectId, CancellationToken cancellationToken = default) =>
        _persistenceStore.DeleteAsync(StateCollectionName, objectId.ToString("N"), cancellationToken);

    private EngineeringObjectState? Deserialise(Guid objectId, string json)
    {
        try
        {
            return JsonSerializer.Deserialize<EngineeringObjectState>(json);
        }
        catch (JsonException ex)
        {
            _logger?.Warning($"Engineering object state '{objectId}' is unreadable and was skipped during rehydration.", ex);
            return null;
        }
    }
}
