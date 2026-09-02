using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Test : VerificationActivity, ITest, IRehydratable<Test>
{
    public Test(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, Guid subjectId, string method)
        : base(document, currentRevision, context, displayName, metadata, subjectId, method)
    {
    }

    static Test IRehydratable<Test>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.DisplayName, state.Metadata, ReadSubjectId(state), ReadMethod(state));
}

public sealed class Inspection : VerificationActivity, IInspection, IRehydratable<Inspection>
{
    public Inspection(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, Guid subjectId, string method)
        : base(document, currentRevision, context, displayName, metadata, subjectId, method)
    {
    }

    static Inspection IRehydratable<Inspection>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.DisplayName, state.Metadata, ReadSubjectId(state), ReadMethod(state));
}

public sealed class ManufacturingOperation : EngineeringObjectBase, IManufacturingOperation, IRehydratable<ManufacturingOperation>
{
    public Guid PartId { get; }

    public ManufacturingOperation(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid partId)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        PartId = partId;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        state[nameof(PartId)] = PartId.ToString();

    static ManufacturingOperation IRehydratable<ManufacturingOperation>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuidOrEmpty(nameof(PartId)));
}

public sealed class WorkInstruction : Document, IWorkInstruction, IRehydratable<WorkInstruction>
{
    public Guid ManufacturingOperationId { get; }

    public WorkInstruction(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid manufacturingOperationId)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ManufacturingOperationId = manufacturingOperationId;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        base.CaptureTypeState(state);
        state[nameof(ManufacturingOperationId)] = ManufacturingOperationId.ToString();
    }

    static WorkInstruction IRehydratable<WorkInstruction>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuidOrEmpty(nameof(ManufacturingOperationId)));
}
