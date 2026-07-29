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
/// <b>Filtering is client-side, over
/// <see cref="Persistence.IPersistenceStore.ListKeysAsync"/> plus a
/// per-key <see cref="Persistence.IPersistenceStore.ReadAsync"/></b> —
/// `IPersistenceStore` has no native query capability (confirmed,
/// `ADR-0041`/`ADR-0045`; `docs/releases/v0.6.0/Risk Register.md`'s own
/// `R8`). Every record in the collection is read and deserialised, then
/// filtered in memory against <see cref="AuditQueryCriteria"/> — correct,
/// but with a performance characteristic that scales linearly with the
/// total number of audit records, disclosed explicitly rather than
/// hidden; see this Work Package's own Platform Impact Assessment.
/// </para>
/// </remarks>
public sealed class AuditQuery : IAuditQuery
{
    /// <summary>
    /// The permission a principal must hold to call
    /// <see cref="QueryAsync"/>.
    /// </summary>
    public static readonly Permission QueryPermission = new("audit.query");

    private readonly IPersistenceStore _persistenceStore;
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
        IPersistenceStore persistenceStore,
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

        var keys = await _persistenceStore.ListKeysAsync(AuditRecorder.AuditCollectionName, cancellationToken).ConfigureAwait(false);

        var results = new List<IAuditRecord>();

        foreach (var key in keys)
        {
            var json = await _persistenceStore.ReadAsync(AuditRecorder.AuditCollectionName, key, cancellationToken).ConfigureAwait(false);

            // A benign race with a concurrent delete (not exposed by
            // IAuditRecorder, which never deletes - but ReadAsync's own
            // contract permits null for "no longer present") - skip
            // rather than fail the whole query.
            if (json is null)
                continue;

            var dto = JsonSerializer.Deserialize<AuditRecordDto>(json)
                ?? throw new AuditException($"Audit record '{key}' could not be deserialised.");
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
}
