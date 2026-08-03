namespace Tempest.Core.Requirements;

/// <summary>
/// The plain, JSON-serializable shape a requirement collection is stored
/// as. Deliberately carries no membership list — membership is recorded
/// entirely through <see cref="EngineeringData.IEngineeringDocumentStore.LinkAsync"/>
/// (<see cref="RequirementRelationshipKinds.CollectedIn"/>), never
/// duplicated into this content, mirroring <c>WP7.2B Requirements Domain
/// Model.md</c> §2's own "a collection is a view over requirements, not a
/// container that owns them" finding.
/// </summary>
internal sealed record RequirementCollectionDto(string Name);
