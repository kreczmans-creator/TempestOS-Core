using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public class Assembly : EngineeringObjectBase, IAssembly, IRehydratable<Assembly>
{
    public IReadOnlyList<Guid> ChildIds { get; }

    public Assembly(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<Guid>? childIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ChildIds = childIds ?? Array.Empty<Guid>();
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        WriteGuidList(state, nameof(ChildIds), ChildIds);

    static Assembly IRehydratable<Assembly>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuidList(nameof(ChildIds)));
}

public sealed class SubAssembly : Assembly, ISubAssembly, IRehydratable<SubAssembly>
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

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        base.CaptureTypeState(state);
        state[nameof(ParentAssemblyId)] = ParentAssemblyId.ToString();
    }

    static SubAssembly IRehydratable<SubAssembly>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.TypeGuidOrEmpty(nameof(ParentAssemblyId)), state.TypeGuidList(nameof(ChildIds)));
}

public sealed class Part : EngineeringObjectBase, IPart, IRehydratable<Part>
{
    public string? MaterialId { get; }

    public Part(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? materialId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        MaterialId = materialId;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        state[nameof(MaterialId)] = MaterialId;

    static Part IRehydratable<Part>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.Type(nameof(MaterialId)));
}

public sealed class Component : EngineeringObjectBase, IComponent, IRehydratable<Component>
{
    public Component(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }

    static Component IRehydratable<Component>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
}

public class Configuration : EngineeringObjectBase, IConfiguration, IRehydratable<Configuration>
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

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        WriteJson(state, nameof(MemberRevisions), MemberRevisions.ToList());

    /// <summary>Reads back the member revisions this and every derived Kind persist identically — a frozen Baseline's own members are no different in shape from a working Configuration's.</summary>
    private protected static IReadOnlyList<ConfigurationMember> ReadMemberRevisions(EngineeringObjectState state) =>
        state.TypeJson<List<ConfigurationMember>>(nameof(MemberRevisions)) ?? [];

    static Configuration IRehydratable<Configuration>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, ReadMemberRevisions(state));
}
