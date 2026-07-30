using System.Collections.Concurrent;
using Tempest.Core.Concurrency;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.Settings;

/// <summary>
/// The concrete <see cref="ISettingsProvider"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Every registered definition's current value is cached in memory,
/// invalidated only by this instance's own writes — <c>GetValueAsync</c>
/// is expected to be a hot-path call (`Platform Service Contracts.md`'s
/// own Performance Expectations), so it consults the underlying
/// <see cref="IPersistenceStore"/> only on a cache miss, never
/// unconditionally.
/// </para>
/// <para>
/// A per-key <see cref="AsyncKeyedLock"/> serialises the
/// "populate cache from persistence" sequence in <see cref="GetValueAsync"/>
/// against the "write persistence, then update cache" sequence in
/// <see cref="SetValueAsync"/>, for the same key — without this, a
/// concurrent, slow cache-miss read could overwrite a newer, just-written
/// cache entry with a stale value it read moments earlier. Reads that hit
/// the cache never acquire the lock at all — only the cache-miss and
/// write paths need it.
/// </para>
/// <para>
/// Every setting is stored in one <see cref="IPersistenceStore"/>
/// collection, <see cref="SettingsCollectionName"/> — Settings owns
/// this collection name exclusively; no other service reads or writes
/// it.
/// </para>
/// <para>
/// Per `Platform Service Contracts.md`'s own explicit default: a write
/// of the already-current value still publishes
/// <see cref="ISettingsChangedEvent"/> — this provider does not compare
/// old and new values to decide whether to publish, for simplicity and
/// predictability, exactly as that document's own Event Publication
/// Rules describe.
/// </para>
/// </remarks>
public sealed class SettingsProvider : ISettingsProvider
{
    /// <summary>
    /// The <see cref="IPersistenceStore"/> collection every setting
    /// value is stored under.
    /// </summary>
    public const string SettingsCollectionName = "Settings";

    private readonly IPersistenceStore _persistenceStore;
    private readonly IEventBus _eventBus;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, ISettingDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly AsyncKeyedLock _keyLock = new();

    /// <summary>
    /// Initialises a new instance of the <see cref="SettingsProvider"/> class.
    /// </summary>
    /// <param name="persistenceStore">The store this provider persists values through.</param>
    /// <param name="eventBus">The Event Bus this provider publishes changes through.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="persistenceStore"/> or <paramref name="eventBus"/> is <see langword="null"/>.
    /// </exception>
    public SettingsProvider(IPersistenceStore persistenceStore, IEventBus eventBus, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);
        ArgumentNullException.ThrowIfNull(eventBus);

        _persistenceStore = persistenceStore;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public void RegisterDefinition(ISettingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_definitions.TryAdd(definition.Key, definition))
            throw new DuplicateSettingDefinitionException(definition.Key);

        _logger?.Information($"Setting definition registered: '{definition.Key}' (default '{definition.DefaultValue}').");
    }

    /// <inheritdoc />
    public async Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var definition = GetRequiredDefinition(key);

        if (_cache.TryGetValue(key, out var cachedValue))
            return cachedValue;

        using (await _keyLock.AcquireAsync(key, cancellationToken).ConfigureAwait(false))
        {
            if (_cache.TryGetValue(key, out cachedValue))
                return cachedValue;

            var stored = await _persistenceStore.ReadAsync(SettingsCollectionName, key, cancellationToken).ConfigureAwait(false);
            var value = stored ?? definition.DefaultValue;
            _cache[key] = value;

            return value;
        }
    }

    /// <inheritdoc />
    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var definition = GetRequiredDefinition(key);
        string oldValue;

        using (await _keyLock.AcquireAsync(key, cancellationToken).ConfigureAwait(false))
        {
            oldValue = _cache.TryGetValue(key, out var cachedValue)
                ? cachedValue
                : (await _persistenceStore.ReadAsync(SettingsCollectionName, key, cancellationToken).ConfigureAwait(false))
                  ?? definition.DefaultValue;

            await _persistenceStore.WriteAsync(SettingsCollectionName, key, value, cancellationToken).ConfigureAwait(false);
            _cache[key] = value;
        }

        _logger?.Information($"Setting '{key}' changed.");

        await _eventBus.PublishAsync<ISettingsChangedEvent>(
            new SettingsChangedEvent(key, oldValue, value), cancellationToken).ConfigureAwait(false);
    }

    private ISettingDefinition GetRequiredDefinition(string key) =>
        _definitions.TryGetValue(key, out var definition) ? definition : throw new SettingNotFoundException(key);
}
