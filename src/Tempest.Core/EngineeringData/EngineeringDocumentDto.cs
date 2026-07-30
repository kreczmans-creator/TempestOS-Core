namespace Tempest.Core.EngineeringData;

/// <summary>
/// The JSON-serializable shape of a document's own identity record,
/// stored in <see cref="EngineeringDocumentStore.DocumentsCollectionName"/>,
/// keyed by the document's own Id. Mirrors
/// <see cref="Audit.AuditRecordDto"/>'s own DTO-for-persistence
/// convention.
/// </summary>
internal sealed record EngineeringDocumentDto(string Kind, DateTimeOffset CreatedAt, int CurrentRevisionNumber);
