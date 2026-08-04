namespace Tempest.Core.EngineeringDomain;

public interface ITest : IVerificationActivity
{
}

public interface IInspection : IVerificationActivity
{
}

public interface IManufacturingOperation : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    Guid PartId { get; }
}

public interface IWorkInstruction : IDocument
{
    Guid ManufacturingOperationId { get; }
}
