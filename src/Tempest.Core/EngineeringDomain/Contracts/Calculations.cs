namespace Tempest.Core.EngineeringDomain;

public interface ICalculation : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata
{
}

public interface ICalculationSet : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
    IReadOnlyList<Guid> MemberCalculationIds { get; }
}

/// <summary>Owned by <c>Tempest.Core.Calculations</c> (<c>CalculationRecord&lt;TResult&gt;</c>); not given a competing concrete realisation here (WP 8.2C).</summary>
public interface ICalculationResult : IEngineeringObject, IHasRevisions, IHasRelationships, ITraceable
{
    Guid SubjectId { get; }
    IReadOnlyList<string> ReferencedMaterialIds { get; }
}
