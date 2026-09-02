using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class ExternalSystemLink : EngineeringObjectBase, IExternalSystemLink, IRehydratable<ExternalSystemLink>
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

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(ExternalSystemName)] = ExternalSystemName;
        state[nameof(ExternalObjectIdentifier)] = ExternalObjectIdentifier;
    }

    static ExternalSystemLink IRehydratable<ExternalSystemLink>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.DisplayName, state.Metadata,
            state.Type(nameof(ExternalSystemName)) ?? string.Empty, state.Type(nameof(ExternalObjectIdentifier)) ?? string.Empty);
}
