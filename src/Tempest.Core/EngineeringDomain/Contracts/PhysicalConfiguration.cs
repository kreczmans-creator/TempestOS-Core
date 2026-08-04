namespace Tempest.Core.EngineeringDomain;

public interface IAssembly : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, ITraceable, IValidatable
{
    IReadOnlyList<Guid> ChildIds { get; }
}

public interface ISubAssembly : IAssembly
{
    Guid ParentAssemblyId { get; }
}

public interface IPart : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, ITraceable, IValidatable
{
    string? MaterialId { get; }
}

public interface IComponent : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata
{
}

public interface IConfiguration : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle
{
    IReadOnlyList<ConfigurationMember> MemberRevisions { get; }
}

public readonly record struct ConfigurationMember(Guid ObjectId, int RevisionNumber);
