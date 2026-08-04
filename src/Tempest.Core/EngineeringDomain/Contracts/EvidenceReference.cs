namespace Tempest.Core.EngineeringDomain;

public interface IExternalSystemLink : IEngineeringObject, IHasMetadata
{
    string ExternalSystemName { get; }
    string ExternalObjectIdentifier { get; }
}
