namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// One class realising <see cref="IRelationshipDiscovery"/>, <see cref="IDependencyTraversal"/>, and
/// <see cref="IImpactAnalysis"/> — <see cref="IImpactAnalysis"/> is exactly <see cref="IDependencyTraversal"/>
/// run incoming, over Dependency/Allocation/Verification categories only, per WP8.2B Digital Thread Contract
/// Specification.md — no separate primitive, as that document itself discloses.
/// </summary>
public sealed class RelationshipDiscoveryService : IRelationshipDiscovery, IDependencyTraversal, IImpactAnalysis
{
    private static readonly RelationshipCategory[] ImpactCategories =
    {
        RelationshipCategory.Dependency, RelationshipCategory.Allocation, RelationshipCategory.Verification,
    };

    private readonly IEngineeringRelationshipRepository _relationshipRepository;
    private readonly IEngineeringObjectRepository _objectRepository;

    public RelationshipDiscoveryService(IEngineeringRelationshipRepository relationshipRepository, IEngineeringObjectRepository objectRepository)
    {
        ArgumentNullException.ThrowIfNull(relationshipRepository);
        ArgumentNullException.ThrowIfNull(objectRepository);
        _relationshipRepository = relationshipRepository;
        _objectRepository = objectRepository;
    }

    public Task<IReadOnlyList<IEngineeringRelationship>> GetOutgoingAsync(Guid objectId, CancellationToken cancellationToken = default) =>
        _relationshipRepository.GetOutgoingAsync(objectId, cancellationToken);

    public Task<IReadOnlyList<IEngineeringRelationship>> GetIncomingAsync(Guid objectId, CancellationToken cancellationToken = default) =>
        _relationshipRepository.GetIncomingAsync(objectId, cancellationToken);

    public async Task<IReadOnlyList<IEngineeringRelationship>> GetByCategoryAsync(Guid objectId, RelationshipCategory category, CancellationToken cancellationToken = default)
    {
        var outgoing = await GetOutgoingAsync(objectId, cancellationToken).ConfigureAwait(false);
        return outgoing.Where(r => r.Category == category).ToList();
    }

    public Task<IReadOnlyList<IEngineeringObject>> TraverseAsync(
        Guid startObjectId, RelationshipCategory category, int maxDepth = 1, CancellationToken cancellationToken = default) =>
        TraverseAsync(startObjectId, new[] { category }, outgoing: true, maxDepth, cancellationToken);

    public Task<IReadOnlyList<IEngineeringObject>> GetImpactedObjectsAsync(
        Guid changedObjectId, int maxDepth = 1, CancellationToken cancellationToken = default) =>
        TraverseAsync(changedObjectId, ImpactCategories, outgoing: false, maxDepth, cancellationToken);

    private async Task<IReadOnlyList<IEngineeringObject>> TraverseAsync(
        Guid startObjectId, IReadOnlyList<RelationshipCategory> categories, bool outgoing, int maxDepth, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { startObjectId };
        var frontier = new List<Guid> { startObjectId };
        var results = new List<IEngineeringObject>();

        for (var depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            var nextFrontier = new List<Guid>();

            foreach (var currentId in frontier)
            {
                var relationships = outgoing
                    ? await _relationshipRepository.GetOutgoingAsync(currentId, cancellationToken).ConfigureAwait(false)
                    : await _relationshipRepository.GetIncomingAsync(currentId, cancellationToken).ConfigureAwait(false);

                foreach (var relationship in relationships.Where(r => categories.Contains(r.Category)))
                {
                    var neighbourId = outgoing ? relationship.TargetId : relationship.SourceId;

                    if (!visited.Add(neighbourId))
                        continue;

                    var neighbour = await _objectRepository.FindAsync(neighbourId, cancellationToken).ConfigureAwait(false);

                    if (neighbour is not null)
                        results.Add(neighbour);

                    nextFrontier.Add(neighbourId);
                }
            }

            frontier = nextFrontier;
        }

        return results;
    }
}
