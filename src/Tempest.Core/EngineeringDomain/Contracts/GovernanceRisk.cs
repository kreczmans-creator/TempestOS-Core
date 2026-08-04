namespace Tempest.Core.EngineeringDomain;

public interface IIssue : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
}

public interface IRisk : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    string? Likelihood { get; }
    string? Severity { get; }
}

public interface IHazard : IRisk
{
}

public interface IDecision : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
    string Rationale { get; }
}

public interface IAssumption : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
}
