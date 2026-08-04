namespace Tempest.Core.EngineeringDomain;

/// <summary>The only relationship type in the Engineering Domain — one generic interface, never a closed set of per-category types (ADR-0073/ADR-0076).</summary>
public interface IEngineeringRelationship
{
    Guid SourceId { get; }
    Guid TargetId { get; }
    string RelationshipKind { get; }
    RelationshipCategory Category { get; }
    string CreatedByPrincipalId { get; }
    DateTimeOffset CreatedAt { get; }
}

/// <summary>Descriptive metadata only — never validated against <see cref="IEngineeringRelationship.RelationshipKind"/> at write time (ADR-0076).</summary>
public enum RelationshipCategory
{
    Parent,
    Child,
    Composition,
    Aggregation,
    Reference,
    Dependency,
    Verification,
    Evidence,
    Allocation,
    Derivation,
    Supersession,
    Manufacturing,
    Calculation,
    Documentation,
    Risk,
    Change,
    Decision,
}

public interface IRelationshipDescriptor
{
    RelationshipCategory Category { get; }
    RelationshipDirection Direction { get; }
    RelationshipMultiplicity Multiplicity { get; }
}

public enum RelationshipDirection
{
    SourceToTarget,
}

public enum RelationshipMultiplicity
{
    OneToOne,
    OneToMany,
    ManyToMany,
}

public interface IRelationshipValidator
{
    Task<IValidationResult> ValidateAsync(IEngineeringRelationship relationship, CancellationToken cancellationToken = default);
}
