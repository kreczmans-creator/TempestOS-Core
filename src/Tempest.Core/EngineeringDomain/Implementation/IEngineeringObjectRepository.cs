namespace Tempest.Core.EngineeringDomain;

/// <summary>An in-memory, Kind-queryable index over constructed <see cref="IEngineeringObject"/> instances — the "In-memory repositories" deliverable named by WP 8.2C. Not proposed by WP8.2B (its own Dependency Rules §8 explicitly left registration/indexing to a future implementation Work Package) and never a replacement for <see cref="EngineeringData.IEngineeringDocumentStore"/>, which remains the sole source of durable document/revision/relationship truth per ADR-0072 — this repository only adds the by-Kind lookup that store cannot offer.</summary>
public interface IEngineeringObjectRepository
{
    void Register(IEngineeringObject engineeringObject);
    Task<IEngineeringObject?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IEngineeringObject>> ListByKindAsync(string kind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IEngineeringObject>> ListAllAsync(CancellationToken cancellationToken = default);
}
