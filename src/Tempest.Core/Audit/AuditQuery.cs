using System.Text.Json;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;

namespace Tempest.Core.Audit;

/// <summary>
/// The concrete <see cref="IAuditQuery"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Permission-gated, per `Platform Service Contracts.md`'s own
/// Security Considerations</b> (`ADR-0045`): every call requires
/// <see cref="QueryPermission"/>, checked against the current principal
/// via the existing, single authorization enforcement point
/// (<see cref="IPermissionEvaluator.RequirePermission"/>, `ADR-0044`).
/// If no principal is currently established, an anonymous, zero-
/// permission principal is checked instead of skipping the check — this
/// reuses <see cref="PermissionDeniedException"/>'s own existing failure
/// path rather than inventing a second "not authenticated" error
/// condition.
/// </para>
/// <para>
/// <b>Two queries, not one scan (`ADR-0145`).</b> This used to list every
/// key in the collection and then issue one read per key — the linear
/// scan `TD-12` recorded, at two round trips per record. It now reads
/// through <see cref="IQueryablePersistenceStore"/>:
/// </para>
/// <list type="bullet">
/// <item><description>An <see cref="AuditQueryCriteria.ObjectId"/> query is
/// one <see cref="IQueryablePersistenceStore.ListKeysAsync"/> on the
/// object id prefix followed by one
/// <see cref="IQueryablePersistenceStore.ReadManyAsync"/> — the reason
/// <c>AuditTransactionWriter</c> puts the object id first in the key.</description></item>
/// <item><description>Every other query is one
/// <see cref="IQueryablePersistenceStore.ReadAllAsync"/>. Date, actor and
/// action are still filtered in memory, because the key carries only the
/// object id and the timestamp; that is disclosed rather than hidden, and
/// it is now one round trip rather than <c>1 + n</c>.</description></item>
/// </list>
/// </remarks>
public sealed class AuditQuery : IAuditQuery
{
    /// <summary>
    /// The permission a principal must hold to call
    /// <see cref="QueryAsync"/>.
    /// </summary>
    public static readonly Permission QueryPermission = new("audit.query");

    private readonly IQueryablePersistenceStore _persistenceStore;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="AuditQuery"/> class.
    /// </summary>
    /// <param name="persistenceStore">The store this query reads through.</param>
    /// <param name="currentPrincipalAccessor">The service this query resolves the calling principal from.</param>
    /// <param name="permissionEvaluator">The service this query checks <see cref="QueryPermission"/> against.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Any parameter except <paramref name="logger"/> is <see langword="null"/>.</exception>
    public AuditQuery(
        IQueryablePersistenceStore persistenceStore,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(persistenceStore);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);

        _persistenceStore = persistenceStore;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IAuditRecord>> QueryAsync(AuditQueryCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var principal = _currentPrincipalAccessor.Current
            ?? new PlatformPrincipal(new PlatformIdentity(AuditRecorder.UnknownActorId, "Unauthenticated"), []);
        _permissionEvaluator.RequirePermission(principal, QueryPermission);

        var stored = criteria.ObjectId is { } objectId
            ? await ReadByObjectAsync(objectId, cancellationToken).ConfigureAwait(false)
            : await _persistenceStore.ReadAllAsync(AuditRecorder.AuditCollectionName, cancellationToken).ConfigureAwait(false);

        var results = new List<IAuditRecord>();

        foreach (var (key, json) in stored)
        {
            AuditRecordDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<AuditRecordDto>(json)
                    ?? throw new AuditException($"Audit record '{key}' could not be deserialised.");
            }
            catch (JsonException ex)
            {
                // Malformed stored content surfaces as this framework's
                // own controlled exception type, never a raw
                // JsonException from a passive query (`TD-60`).
                throw new AuditException($"Audit record '{key}' could not be deserialised.", ex);
            }

            var record = new AuditRecord(dto.ActorId, dto.Action, dto.OccurredAt, dto.Detail);

            if (criteria.ActorId is not null && !string.Equals(record.ActorId, criteria.ActorId, StringComparison.Ordinal))
                continue;

            if (criteria.Action is not null && !string.Equals(record.Action, criteria.Action, StringComparison.Ordinal))
                continue;

            if (criteria.From is not null && record.OccurredAt < criteria.From)
                continue;

            if (criteria.To is not null && record.OccurredAt > criteria.To)
                continue;

            results.Add(record);
        }

        _logger?.Information($"Audit query returned {results.Count} record(s).");

        return results.OrderBy(r => r.OccurredAt).ToList();
    }

    /// <summary>
    /// Reads every audit row whose key begins with
    /// <paramref name="objectId"/> — one prefix listing and one batched
    /// read, rather than a scan of the whole collection.
    /// </summary>
    private async Task<IReadOnlyList<KeyValuePair<string, string>>> ReadByObjectAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var keys = await _persistenceStore
            .ListKeysAsync(AuditRecorder.AuditCollectionName, objectId.ToString("N"), cancellationToken)
            .ConfigureAwait(false);

        if (keys.Count == 0)
            return [];

        var values = await _persistenceStore
            .ReadManyAsync(AuditRecorder.AuditCollectionName, keys, cancellationToken)
            .ConfigureAwait(false);

        var stored = new List<KeyValuePair<string, string>>(keys.Count);
        foreach (var key in keys)
        {
            // A key listed and then absent is a benign race with a
            // concurrent delete; skip it rather than fail the query, the
            // same allowance the per-key read path made.
            if (values.TryGetValue(key, out var json) && json is not null)
                stored.Add(new KeyValuePair<string, string>(key, json));
        }

        return stored;
    }
}
