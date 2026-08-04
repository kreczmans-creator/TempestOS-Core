namespace Tempest.Core.EngineeringDomain;

public interface IRelationshipDiscovery
{
    Task<IReadOnlyList<IEngineeringRelationship>> GetOutgoingAsync(Guid objectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IEngineeringRelationship>> GetIncomingAsync(Guid objectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IEngineeringRelationship>> GetByCategoryAsync(Guid objectId, RelationshipCategory category, CancellationToken cancellationToken = default);
}

/// <summary><paramref name="maxDepth"/> defaults to 1 deliberately — mirrors ADR-0065's disclosed Workspace Digital Thread panel limitation.</summary>
public interface IDependencyTraversal
{
    Task<IReadOnlyList<IEngineeringObject>> TraverseAsync(
        Guid startObjectId, RelationshipCategory category, int maxDepth = 1, CancellationToken cancellationToken = default);
}

public interface IEvidenceComposer
{
    Task<IEvidence> ComposeAsync(Guid subjectId, CancellationToken cancellationToken = default);
}

public interface IImpactAnalysis
{
    Task<IReadOnlyList<IEngineeringObject>> GetImpactedObjectsAsync(
        Guid changedObjectId, int maxDepth = 1, CancellationToken cancellationToken = default);
}
