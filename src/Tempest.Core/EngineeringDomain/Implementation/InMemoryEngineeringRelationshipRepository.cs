using System.Collections.Concurrent;

namespace Tempest.Core.EngineeringDomain;

public sealed class InMemoryEngineeringRelationshipRepository : IEngineeringRelationshipRepository
{
    private readonly ConcurrentBag<IEngineeringRelationship> _relationships = new();

    public void Record(IEngineeringRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        _relationships.Add(relationship);
    }

    public Task<IReadOnlyList<IEngineeringRelationship>> GetOutgoingAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IEngineeringRelationship> matches = _relationships
            .Where(r => r.SourceId == sourceId)
            .ToList();

        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<IEngineeringRelationship>> GetIncomingAsync(Guid targetId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IEngineeringRelationship> matches = _relationships
            .Where(r => r.TargetId == targetId)
            .ToList();

        return Task.FromResult(matches);
    }
}
