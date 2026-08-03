namespace Tempest.Core.Requirements;

/// <summary>
/// The plain, JSON-serializable shape a requirement group is stored as.
/// Deliberately carries no parent reference — the hierarchy is recorded
/// entirely through <see cref="EngineeringData.IEngineeringDocumentStore.LinkAsync"/>
/// (<see cref="RequirementRelationshipKinds.GroupedUnder"/>), never
/// duplicated into this content.
/// </summary>
internal sealed record RequirementGroupDto(string Name);
