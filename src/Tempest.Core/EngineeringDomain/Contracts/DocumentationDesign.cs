namespace Tempest.Core.EngineeringDomain;

public interface IDocument : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRevisions, IHasRelationships, IHasAttachments
{
}

public interface IDrawing : IDocument
{
    string? DrawingNumber { get; }
}

public interface ICadModel : IDocument
{
    string? ModelFormat { get; }
}

public interface ISimulation : ICalculationResult, IHasAttachments
{
    string SimulationType { get; }
}
