namespace Tempest.Core.EngineeringData;

/// <summary>
/// The JSON-serializable shape of one outgoing reference, stored in a
/// per-source-document collection
/// (<see cref="EngineeringDocumentStore.GetReferencesCollectionName"/>) —
/// the source document's own Id is the collection itself, not part of
/// this DTO, so it is never duplicated on disk.
/// </summary>
internal sealed record DocumentReferenceDto(Guid TargetDocumentId, string RelationshipKind);
