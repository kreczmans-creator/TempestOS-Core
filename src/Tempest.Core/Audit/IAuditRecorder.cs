namespace Tempest.Core.Audit;

/// <summary>Records an audit entry. Never throws for the caller's own action failing — an audit record may describe a failed action.</summary>
public interface IAuditRecorder
{
    /// <summary>
    /// Records that <paramref name="action"/> occurred, attributed to
    /// the current principal (<see cref="Identity.ICurrentPrincipalAccessor"/>),
    /// resolved automatically.
    /// </summary>
    /// <param name="action">The action that occurred.</param>
    /// <param name="detail">Additional detail describing the action, if any.</param>
    /// <exception cref="ArgumentException"><paramref name="action"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="Persistence.PersistenceStoreUnavailableException">The underlying store could not be written.</exception>
    Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default);
}
