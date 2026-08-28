using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public class Document : EngineeringObjectBase, IDocument, IRehydratable<Document>
{
    public Document(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }

    static Document IRehydratable<Document>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
}

public sealed class Drawing : Document, IDrawing, IRehydratable<Drawing>
{
    public string? DrawingNumber { get; }

    public Drawing(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? drawingNumber = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        DrawingNumber = drawingNumber;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        base.CaptureTypeState(state);
        state[nameof(DrawingNumber)] = DrawingNumber;
    }

    static Drawing IRehydratable<Drawing>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.Type(nameof(DrawingNumber)));
}

public sealed class CadModel : Document, ICadModel, IRehydratable<CadModel>
{
    public string? ModelFormat { get; }

    public CadModel(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? modelFormat = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ModelFormat = modelFormat;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        base.CaptureTypeState(state);
        state[nameof(ModelFormat)] = ModelFormat;
    }

    static CadModel IRehydratable<CadModel>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.Type(nameof(ModelFormat)));
}

public sealed class Simulation : EngineeringObjectBase, ISimulation, IRehydratable<Simulation>
{
    public Guid SubjectId { get; }
    public IReadOnlyList<string> ReferencedMaterialIds { get; }
    public string SimulationType { get; }

    public Simulation(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, Guid subjectId, string simulationType,
        IReadOnlyList<string>? referencedMaterialIds = null)
        : base(document, currentRevision, context, identifier: null, displayName, metadata)
    {
        SubjectId = subjectId;
        SimulationType = simulationType;
        ReferencedMaterialIds = referencedMaterialIds ?? Array.Empty<string>();
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(SubjectId)] = SubjectId.ToString();
        state[nameof(SimulationType)] = SimulationType;
        WriteList(state, nameof(ReferencedMaterialIds), ReferencedMaterialIds);
    }

    static Simulation IRehydratable<Simulation>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.DisplayName, state.Metadata,
            state.TypeGuidOrEmpty(nameof(SubjectId)), state.Type(nameof(SimulationType)) ?? string.Empty, state.TypeList(nameof(ReferencedMaterialIds)));
}
