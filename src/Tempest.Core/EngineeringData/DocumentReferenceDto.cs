namespace Tempest.Core.EngineeringData;

/// <summary>
/// The JSON-serializable shape of one outgoing reference, stored in a
/// per-source-document collection
/// (<see cref="EngineeringDocumentStore.GetReferencesCollectionName"/>) —
/// the source document's own Id is the collection itself, not part of
/// this DTO, so it is never duplicated on disk.
/// </summary>
/// <remarks>
/// <see cref="CreatedByPrincipalId"/>/<see cref="CreatedAt"/> are optional
/// with a <see langword="null"/> default so a record written before
/// `TD-85` still deserialises — it simply reads back with no provenance,
/// which is the truth about it.
/// </remarks>
internal sealed record DocumentReferenceDto(
    Guid TargetDocumentId,
    string RelationshipKind,
    string? CreatedByPrincipalId = null,
    DateTimeOffset? CreatedAt = null);
