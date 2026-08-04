namespace Tempest.Core.EngineeringDomain;

public interface ISupplier : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
}

public interface IPurchaseItem : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRelationships
{
    Guid SupplierId { get; }
    Guid? ReferencedObjectId { get; }
}
