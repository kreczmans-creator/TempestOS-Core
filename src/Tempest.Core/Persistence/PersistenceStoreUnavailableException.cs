namespace Tempest.Core.Persistence;

/// <summary>
/// Thrown when <see cref="IPersistenceStore"/> could not read, write,
/// delete, or list a value because the underlying storage backend is
/// unavailable (a disk I/O error, an access-denied failure, and so on).
/// </summary>
/// <remarks>
/// Never thrown for an ordinary "not found" case — <see cref="IPersistenceStore.ReadAsync"/>
/// returns <see langword="null"/> for that, and <see cref="IPersistenceStore.DeleteAsync"/>
/// is idempotent. This exception means the store itself could not be
/// reached or operated on, mirroring `ADR-0013`'s fail-loudly philosophy.
/// </remarks>
public sealed class PersistenceStoreUnavailableException : PersistenceException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PersistenceStoreUnavailableException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The underlying storage failure.</param>
    public PersistenceStoreUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
