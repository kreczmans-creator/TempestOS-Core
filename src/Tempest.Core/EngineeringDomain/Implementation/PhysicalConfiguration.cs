using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public class Assembly : EngineeringObjectBase, IAssembly
{
    public IReadOnlyList<Guid> ChildIds { get; }

    public Assembly(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<Guid>? childIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ChildIds = childIds ?? Array.Empty<Guid>();
    }
}

public sealed class SubAssembly : Assembly, ISubAssembly
{
    public Guid ParentAssemblyId { get; }

    public SubAssembly(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid parentAssemblyId,
        IReadOnlyList<Guid>? childIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, childIds)
    {
        ParentAssemblyId = parentAssemblyId;
    }
}

public sealed class Part : EngineeringObjectBase, IPart
{
    public string? MaterialId { get; }

    public Part(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? materialId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        MaterialId = materialId;
    }
}

public sealed class Component : EngineeringObjectBase, IComponent
{
    public Component(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }
}

public class Configuration : EngineeringObjectBase, IConfiguration
{
    public IReadOnlyList<ConfigurationMember> MemberRevisions { get; }

    public Configuration(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        IReadOnlyList<ConfigurationMember>? memberRevisions = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        MemberRevisions = memberRevisions ?? Array.Empty<ConfigurationMember>();
    }
}
