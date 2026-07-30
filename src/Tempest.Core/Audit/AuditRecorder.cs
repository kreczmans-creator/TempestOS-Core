using System.Text.Json;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.Audit;

/// <summary>
/// The concrete <see cref="IAuditRecorder"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Every record is stored in one <see cref="Persistence.IPersistenceStore"/>
/// collection, <see cref="AuditCollectionName"/> — Audit owns this
/// collection exclusively, distinct from <see cref="Settings.SettingsProvider.SettingsCollectionName"/>,
/// proving Persistence's own collection-scoping isolation in practice
/// (`ADR-0041`). Each record is serialized to JSON
/// (<see cref="System.Text.Json"/>, already used elsewhere in this
/// codebase — <c>PluginManifestDiscoveryService</c> — introducing no new
/// dependency) under a key combining the record's own UTC ticks (for
/// rough chronological ordering when inspected directly) and a random
/// component (so two records occurring in the same tick never collide).
/// </para>
/// <para>
/// <b>The current principal is resolved automatically</b> via
/// <see cref="ICurrentPrincipalAccessor"/> — a caller never supplies its
/// own actor id. If no principal is currently established (a normal,
/// honestly-reported state per `ADR-0044`), <see cref="UnknownActorId"/>
/// is recorded instead of failing the write — an audit record with an
/// unknown actor is still more useful than no record at all.
/// </para>
/// <para>
/// <b>Failure propagation, and why `RecordAsync` is awaited, not
/// fire-and-forget</b> (`ADR-0045`): a storage failure propagates as
/// <see cref="PersistenceStoreUnavailableException"/>, unchanged — never
/// swallowed. This method is not literally "fire-and-forget" (the
/// returned <see cref="Task"/> must be awaited for a failure to be
/// observable at all); `Platform Service Contracts.md`'s own "should not
/// meaningfully slow down the action it is recording" performance goal
/// is satisfied instead by keeping the write itself minimal — a single,
/// append-only file write, no read-before-write, no cache to
/// invalidate — not by discarding the task and racing ahead, which would
/// make a genuine storage failure invisible to the very code auditing
/// against exactly that kind of gap.
/// </para>
/// </remarks>
public sealed class AuditRecorder : IAuditRecorder
{
    /// <summary>
    /// The <see cref="Persistence.IPersistenceStore"/> collection every
    /// audit record is stored under.
    /// </summary>
    public const string AuditCollectionName = "Audit";

    /// <summary>
    /// The <see cref="IAuditRecord.ActorId"/> recorded when no principal
    /// is currently established.
    /// </summary>
    public const string UnknownActorId = "unknown";

    /// <summary>
    /// The well-known <see cref="IAuditRecord.Detail"/> key a caller uses
    /// to tie several related audit records together with a shared
    /// correlation identifier. Not a dedicated property on
    /// <see cref="IAuditRecord"/> — <c>Detail</c>'s own content is
    /// explicitly documented as free to carry exactly this kind of
    /// per-action convention without changing the approved contract.
    /// </summary>
    public const string CorrelationIdDetailKey = "CorrelationId";

    private readonly IPersistenceStore _persistenceStore;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="AuditRecorder"/> class.
    /// </summary>
    /// <param name="persistenceStore">The store this recorder writes through.</param>
    /// <param name="currentPrincipalAccessor">The service this recorder resolves the acting principal from.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="persistenceStore"/> or <paramref name="currentPrincipalAccessor"/> is <see langword="null"/>.
    /// </exception>
    public AuditRecorder(IPersistenceStore persistenceStore, ICurrentPrincipalAccessor currentPrincipalAccessor, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);

        _persistenceStore = persistenceStore;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action must not be null, empty, or whitespace.", nameof(action));

        var actorId = _currentPrincipalAccessor.Current?.Identity.Id ?? UnknownActorId;
        var occurredAt = DateTimeOffset.UtcNow;
        var effectiveDetail = detail ?? new Dictionary<string, string>();

        var dto = new AuditRecordDto(actorId, action, occurredAt, new Dictionary<string, string>(effectiveDetail));
        var key = $"{occurredAt.UtcTicks:D19}_{Guid.NewGuid():N}";
        var json = JsonSerializer.Serialize(dto);

        try
        {
            await _persistenceStore.WriteAsync(AuditCollectionName, key, json, cancellationToken).ConfigureAwait(false);
        }
        catch (PersistenceStoreUnavailableException ex)
        {
            _logger?.Warning($"Audit record write failed for action '{action}'.", ex);
            throw;
        }

        _logger?.Information($"Audit record written: actor '{actorId}', action '{action}'.");
    }
}
