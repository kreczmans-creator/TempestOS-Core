namespace Tempest.Core.EngineeringDomain;

/// <summary>Responsible for exactly one <see cref="Kind"/> — mirrors ADR-0067's one-factory-per-Kind discipline (WP8.2B Dependency Rules.md §7).</summary>
public interface IEngineeringObjectFactory
{
    string Kind { get; }
    Task<IEngineeringObject> CreateAsync(string initialContent, CancellationToken cancellationToken = default);
}

public interface IEngineeringRelationshipFactory
{
    string RelationshipKind { get; }
    Task<IEngineeringRelationship> CreateAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken = default);
}
