namespace Tempest.Core.EngineeringDomain;

public interface IPortfolio : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    IReadOnlyList<Guid> ProgrammeIds { get; }
}

public interface IProgramme : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    Guid? PortfolioId { get; }
    IReadOnlyList<Guid> ProjectIds { get; }
}

public interface IProject : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships, ITraceable
{
    Guid? ProgrammeId { get; }
}
