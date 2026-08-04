using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class ChangeRequest : EngineeringObjectBase, IChangeRequest
{
    public ChangeRequest(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }
}

public sealed class EngineeringChange : EngineeringObjectBase, IEngineeringChange
{
    public Guid ChangeRequestId { get; }

    public EngineeringChange(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid changeRequestId)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ChangeRequestId = changeRequestId;
    }
}

public class Baseline : Configuration, IBaseline
{
    public Baseline(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        IReadOnlyList<ConfigurationMember>? memberRevisions = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, memberRevisions)
    {
    }
}

public sealed class Release : Baseline, IRelease
{
    public Release(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        IReadOnlyList<ConfigurationMember>? memberRevisions = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, memberRevisions)
    {
    }
}
