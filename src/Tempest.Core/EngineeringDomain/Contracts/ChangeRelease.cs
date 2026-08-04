namespace Tempest.Core.EngineeringDomain;

public interface IChangeRequest : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
}

public interface IEngineeringChange : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    Guid ChangeRequestId { get; }
}

public interface IBaseline : IConfiguration
{
}

public interface IRelease : IBaseline
{
}
