namespace Tempest.Core.Audit;

/// <summary>Queries previously recorded audit entries. Read-only.</summary>
public interface IAuditQuery
{
    /// <summary>
    /// Queries recorded audit entries matching <paramref name="criteria"/>,
    /// ordered by <see cref="IAuditRecord.OccurredAt"/> ascending.
    /// </summary>
    /// <param name="criteria">The filter to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="criteria"/> is <see langword="null"/>.</exception>
    /// <exception cref="Identity.PermissionDeniedException">The current principal does not hold the audit-query permission.</exception>
    /// <exception cref="Persistence.PersistenceStoreUnavailableException">The underlying store could not be read.</exception>
    Task<IReadOnlyList<IAuditRecord>> QueryAsync(AuditQueryCriteria criteria, CancellationToken cancellationToken = default);
}
