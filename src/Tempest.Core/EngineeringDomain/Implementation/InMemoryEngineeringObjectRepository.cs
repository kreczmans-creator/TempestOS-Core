using System.Collections.Concurrent;

namespace Tempest.Core.EngineeringDomain;

public sealed class InMemoryEngineeringObjectRepository : IEngineeringObjectRepository
{
    private readonly ConcurrentDictionary<Guid, IEngineeringObject> _objectsById = new();

    public void Register(IEngineeringObject engineeringObject)
    {
        ArgumentNullException.ThrowIfNull(engineeringObject);
        _objectsById[engineeringObject.Id] = engineeringObject;
    }

    public Task<IEngineeringObject?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _objectsById.TryGetValue(id, out var found);
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<IEngineeringObject>> ListByKindAsync(string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        IReadOnlyList<IEngineeringObject> matches = _objectsById.Values
            .Where(o => string.Equals(o.Kind, kind, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<IEngineeringObject>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IEngineeringObject> all = _objectsById.Values.ToList();
        return Task.FromResult(all);
    }
}
