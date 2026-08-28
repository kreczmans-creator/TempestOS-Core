using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class ChangeRequest : EngineeringObjectBase, IChangeRequest, IRehydratable<ChangeRequest>
{
    public ChangeRequest(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }

    static ChangeRequest IRehydratable<ChangeRequest>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
}

public sealed class EngineeringChange : EngineeringObjectBase, IEngineeringChange, IRehydratable<EngineeringChange>
{
    public Guid ChangeRequestId { get; }

    public EngineeringChange(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid changeRequestId)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ChangeRequestId = changeRequestId;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        state[nameof(ChangeRequestId)] = ChangeRequestId.ToString();

    static EngineeringChange IRehydratable<EngineeringChange>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuidOrEmpty(nameof(ChangeRequestId)));
}

public class Baseline : Configuration, IBaseline, IRehydratable<Baseline>
{
    public Baseline(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        IReadOnlyList<ConfigurationMember>? memberRevisions = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, memberRevisions)
    {
    }

    static Baseline IRehydratable<Baseline>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, ReadMemberRevisions(state));
}

public sealed class Release : Baseline, IRelease, IRehydratable<Release>
{
    public Release(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        IReadOnlyList<ConfigurationMember>? memberRevisions = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, memberRevisions)
    {
    }

    static Release IRehydratable<Release>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, ReadMemberRevisions(state));
}
