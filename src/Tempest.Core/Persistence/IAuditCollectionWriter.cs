using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Persistence;

/// <summary>
/// The one legitimate route a non-transactional write reaches
/// <see cref="Audit.AuditRecorder.AuditCollectionName"/> through — held
/// only by <see cref="Audit.AuditRecorder"/> (`WP 21.6A`, OSA-13).
/// </summary>
/// <remarks>
/// <para>
/// <b>A capability, not a permission check.</b> <see cref="SqlitePersistenceStore"/>
/// implements this explicitly, alongside its own ordinary
/// <see cref="IPersistenceStore.WriteAsync"/>, which now unconditionally
/// refuses <see cref="Audit.AuditRecorder.AuditCollectionName"/>
/// (<see cref="AuditCollectionProtectedException"/>). <see cref="Audit.AuditRecorder"/>
/// checks its own injected <see cref="IPersistenceStore"/> for this
/// interface and writes through it when present, exactly as
/// <c>Tempest.Core.Requirements.RequirementsService</c>/<c>Tempest.Core.ReferenceData.ReferenceDataCatalog{TDefinition}</c>
/// already check their own store for <see cref="EngineeringDomain.ITransactionalDocumentWriter"/>/
/// <see cref="IQueryablePersistenceStore"/> — the same "widen the concrete
/// capability, refuse loudly if it is not there" shape this codebase
/// already established, applied here to narrow a capability instead of
/// widen one.
/// </para>
/// <para>
/// <b>Deliberately not exposed via <c>[InternalsVisibleTo]</c> to any
/// assembly outside <c>Tempest.Core</c>.</b> <c>Tempest.Core.Tests</c>
/// can see it structurally, the same way it can see every other internal
/// type in this assembly — not through any grant made for this interface
/// specifically. No production assembly (<c>Tempest.Desktop</c>,
/// <c>Tempest.Workspace</c>, <c>Tempest.Samples</c>, <c>Tempest.Harness</c>)
/// can reference it at all, so nothing outside <c>Tempest.Core</c> can
/// satisfy it even by declaring a type with a matching method signature —
/// C# interface implementation is nominal, not structural.
/// </para>
/// </remarks>
internal interface IAuditCollectionWriter
{
    /// <summary>Writes one audit row, bypassing the ordinary collection guard.</summary>
    Task WriteAuditRowAsync(string key, string value, CancellationToken cancellationToken);
}

/// <summary>
/// The one legitimate route a transactional write reaches
/// <see cref="Audit.AuditRecorder.AuditCollectionName"/> through — held
/// only by <see cref="Audit.AuditTransactionWriter"/> (`WP 21.6A`, OSA-13).
/// See <see cref="IAuditCollectionWriter"/>'s own remarks; this is its
/// exact counterpart for a write made through an open
/// <see cref="IPersistenceTransaction"/> rather than directly through the
/// store.
/// </summary>
internal interface IAuditCollectionTransactionWriter
{
    /// <summary>Writes one audit row inside this transaction, bypassing the ordinary collection guard.</summary>
    Task WriteAuditRowAsync(string key, string value, CancellationToken cancellationToken);
}
