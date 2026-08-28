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
/// <param name="CreatedByPrincipalId">
/// The principal that recorded the link, or <see langword="null"/> for a
/// link written before `TD-85` made relationship provenance durable —
/// honestly absent, never silently attributed to whoever happens to be
/// signed in when it is read back.
/// </param>
/// <param name="CreatedAt">When the link was recorded, or <see langword="null"/> for a pre-`TD-85` link.</param>
public sealed record DocumentReference(
    Guid SourceDocumentId,
    Guid TargetDocumentId,
    string RelationshipKind,
    string? CreatedByPrincipalId = null,
    DateTimeOffset? CreatedAt = null);
