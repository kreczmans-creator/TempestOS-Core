namespace Tempest.Core.EngineeringData;

/// <summary>
/// A directed, typed relationship from one document to another (e.g.
/// <c>"verifies"</c>, <c>"derivedFrom"</c>).
/// </summary>
/// <param name="SourceDocumentId">The document the relationship is recorded against.</param>
/// <param name="TargetDocumentId">The document being referenced.</param>
/// <param name="RelationshipKind">
/// The caller-declared relationship kind — opaque to this namespace, no
/// fixed vocabulary is enforced.
/// </param>
public sealed record DocumentReference(Guid SourceDocumentId, Guid TargetDocumentId, string RelationshipKind);
