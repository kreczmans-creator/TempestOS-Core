using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Test : VerificationActivity, ITest
{
    public Test(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, Guid subjectId, string method)
        : base(document, currentRevision, context, displayName, metadata, subjectId, method)
    {
    }
}

public sealed class Inspection : VerificationActivity, IInspection
{
    public Inspection(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, Guid subjectId, string method)
        : base(document, currentRevision, context, displayName, metadata, subjectId, method)
    {
    }
}

public sealed class ManufacturingOperation : EngineeringObjectBase, IManufacturingOperation
{
    public Guid PartId { get; }

    public ManufacturingOperation(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid partId)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        PartId = partId;
    }
}

public sealed class WorkInstruction : Document, IWorkInstruction
{
    public Guid ManufacturingOperationId { get; }

    public WorkInstruction(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid manufacturingOperationId)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ManufacturingOperationId = manufacturingOperationId;
    }
}
