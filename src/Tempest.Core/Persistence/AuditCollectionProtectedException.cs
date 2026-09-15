namespace Tempest.Core.Persistence;

/// <summary>
/// Thrown when a caller attempts to write, delete, or otherwise mutate
/// <see cref="Audit.AuditRecorder.AuditCollectionName"/> through the
/// ordinary, generic <see cref="IPersistenceStore"/>/<see cref="IPersistenceTransaction"/>
/// surface — the one collection this store refuses to touch that way
/// (`WP 21.6A`, OSA-13). See <see cref="IAuditCollectionWriter"/>/
/// <see cref="IAuditCollectionTransactionWriter"/> for the one legitimate
/// route in.
/// </summary>
public sealed class AuditCollectionProtectedException : PersistenceException
{
    /// <summary>The protected collection name (always <see cref="Audit.AuditRecorder.AuditCollectionName"/>).</summary>
    public string Collection { get; }

    /// <summary>Initialises a new instance of the <see cref="AuditCollectionProtectedException"/> class.</summary>
    public AuditCollectionProtectedException(string collection)
        : base(
            $"'{collection}' is a protected collection — only Audit's own writer classes " +
            $"({nameof(Audit.AuditRecorder)}, {nameof(Audit.AuditTransactionWriter)}) may write to it; every other caller, " +
            "including a direct IPersistenceStore/IPersistenceTransaction reference, is refused.")
    {
        Collection = collection;
    }
}
