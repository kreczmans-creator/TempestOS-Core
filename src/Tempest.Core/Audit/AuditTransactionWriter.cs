using System.Globalization;
using System.Text.Json;
using Tempest.Core.Persistence;

namespace Tempest.Core.Audit;

/// <summary>
/// Writes one audit row into <see cref="AuditRecorder.AuditCollectionName"/>
/// through an in-flight <see cref="IPersistenceTransaction"/>, so a
/// committed mutation and the evidence that it happened land together
/// (`ADR-0145`).
/// </summary>
/// <remarks>
/// <para>
/// <b>The key carries the object id.</b> A row written for an engineering
/// object is keyed <c>{objectId:N}_{utcTicks:D19}_{guid:N}</c>, so
/// "what happened to this object" is one
/// <see cref="IQueryablePersistenceStore.ListKeysAsync"/> with the object
/// id as the prefix rather than a scan of every audit record ever
/// written (`TD-12`). The timestamp is the second segment so a prefix
/// listing comes back in chronological order without a sort, and the
/// GUID is the third so two rows written in the same tick are still
/// distinct keys.
/// </para>
/// <para>
/// <b>The record shape is unchanged.</b> The stored value is the same
/// <see cref="AuditRecordDto"/> <see cref="AuditRecorder"/> writes, so
/// <see cref="AuditQuery"/> reads both shapes with one deserialiser and
/// nothing outside this namespace can tell which writer produced a row.
/// The object id and Kind are carried in <see cref="IAuditRecord.Detail"/>
/// under <see cref="ObjectIdDetailKey"/> and <see cref="KindDetailKey"/>,
/// beside whatever short detail the mutation supplied.
/// </para>
/// </remarks>
internal static class AuditTransactionWriter
{
    /// <summary>The <see cref="IAuditRecord.Detail"/> key carrying the engineering object's Id.</summary>
    public const string ObjectIdDetailKey = "ObjectId";

    /// <summary>The <see cref="IAuditRecord.Detail"/> key carrying the engineering object's Kind.</summary>
    public const string KindDetailKey = "Kind";

    /// <summary>The <see cref="IAuditRecord.Detail"/> key carrying the mutation's own short description.</summary>
    public const string DetailKey = "Detail";

    /// <summary>
    /// Composes the key an object-scoped audit row is stored under.
    /// </summary>
    public static string KeyFor(Guid objectId, DateTimeOffset occurredAt) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{objectId:N}_{occurredAt.UtcTicks:D19}_{Guid.NewGuid():N}");

    /// <summary>
    /// Writes one audit row for <paramref name="objectId"/> inside
    /// <paramref name="transaction"/>.
    /// </summary>
    public static Task WriteAsync(
        IPersistenceTransaction transaction,
        Guid objectId,
        string kind,
        string action,
        string principalId,
        string? detail,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ObjectIdDetailKey] = objectId.ToString("N"),
            [KindDetailKey] = kind,
        };

        if (!string.IsNullOrWhiteSpace(detail))
            payload[DetailKey] = detail;

        var dto = new AuditRecordDto(principalId, action, occurredAt, payload);

        return transaction.WriteAsync(
            AuditRecorder.AuditCollectionName,
            KeyFor(objectId, occurredAt),
            JsonSerializer.Serialize(dto),
            cancellationToken);
    }
}

/// <summary>
/// The <see cref="IAuditRecord.Action"/> values the engineering domain
/// writes, one per committed mutation (`ADR-0145`).
/// </summary>
/// <remarks>
/// Named constants rather than literals at the call sites, so a query for
/// "every rename" is written against the same string the writer used.
/// </remarks>
public static class EngineeringAuditActions
{
    /// <summary>An object was created, with its document and first revision.</summary>
    public const string Created = "engineering.object.created";

    /// <summary>An object's lifecycle state moved.</summary>
    public const string Transitioned = "engineering.object.transitioned";

    /// <summary>An object's display name changed.</summary>
    public const string Renamed = "engineering.object.renamed";

    /// <summary>A new revision of an object was written.</summary>
    public const string Revised = "engineering.object.revised";

    /// <summary>A typed relationship was recorded from this object.</summary>
    public const string Linked = "engineering.object.linked";

    /// <summary>Attachment metadata was recorded against this object.</summary>
    public const string Attached = "engineering.object.attached";

    /// <summary>Attachment metadata and its bytes were recorded against this object.</summary>
    public const string ContentAttached = "engineering.object.content-attached";

    /// <summary>An object's structural parent changed.</summary>
    public const string Moved = "engineering.object.moved";

    /// <summary>An object was marked deleted.</summary>
    public const string Deleted = "engineering.object.deleted";

    /// <summary>An object's bill-of-materials line changed.</summary>
    public const string BomLineSet = "engineering.object.bom-line-set";

    /// <summary>A Kind-specific field of an object changed.</summary>
    public const string StateChanged = "engineering.object.state-changed";
}
