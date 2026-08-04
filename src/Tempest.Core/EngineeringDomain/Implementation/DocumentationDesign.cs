using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public class Document : EngineeringObjectBase, IDocument
{
    public Document(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }
}

public sealed class Drawing : Document, IDrawing
{
    public string? DrawingNumber { get; }

    public Drawing(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? drawingNumber = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        DrawingNumber = drawingNumber;
    }
}

public sealed class CadModel : Document, ICadModel
{
    public string? ModelFormat { get; }

    public CadModel(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? modelFormat = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ModelFormat = modelFormat;
    }
}

public sealed class Simulation : EngineeringObjectBase, ISimulation
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
}
