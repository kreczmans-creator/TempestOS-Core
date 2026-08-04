using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class ExternalSystemLink : EngineeringObjectBase, IExternalSystemLink
{
    public string ExternalSystemName { get; }
    public string ExternalObjectIdentifier { get; }

    public ExternalSystemLink(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, string externalSystemName, string externalObjectIdentifier)
        : base(document, currentRevision, context, identifier: null, displayName, metadata)
    {
        ExternalSystemName = externalSystemName;
        ExternalObjectIdentifier = externalObjectIdentifier;
    }
}
