namespace Tempest.Core.EngineeringDomain;

public interface IAssembly : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, ITraceable, IValidatable, IRenamable, IHasParent, IDeletable, IHasBomLine
{
    /// <summary>Gets this Assembly's own children, fixed at construction time. Disclosed: not live — see <see cref="IHasParent.ParentId"/> for the authoritative, live membership source `WP 9.0A` introduces.</summary>
    IReadOnlyList<Guid> ChildIds { get; }
}

public interface ISubAssembly : IAssembly
{
    /// <summary>Gets the parent Assembly this Sub-Assembly was constructed under. Disclosed: fixed at construction time, exactly like <see cref="IAssembly.ChildIds"/> — see <see cref="IHasParent.ParentId"/> for the authoritative, live value after any <see cref="IHasParent.MoveAsync"/>.</summary>
    Guid ParentAssemblyId { get; }
}

public interface IPart : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, ITraceable, IValidatable, IRenamable, IHasParent, IDeletable, IHasBomLine
{
    string? MaterialId { get; }
}

public interface IComponent : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IRenamable, IHasParent, IDeletable, IHasBomLine
{
}

public interface IConfiguration : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle
{
    IReadOnlyList<ConfigurationMember> MemberRevisions { get; }
}

public readonly record struct ConfigurationMember(Guid ObjectId, int RevisionNumber);
