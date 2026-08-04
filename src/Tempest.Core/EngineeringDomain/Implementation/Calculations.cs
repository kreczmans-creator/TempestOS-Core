using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Calculation : EngineeringObjectBase, ICalculation
{
    public Calculation(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }
}

public sealed class CalculationSet : EngineeringObjectBase, ICalculationSet
{
    public IReadOnlyList<Guid> MemberCalculationIds { get; }

    public CalculationSet(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<Guid>? memberCalculationIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        MemberCalculationIds = memberCalculationIds ?? Array.Empty<Guid>();
    }
}
