using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Supplier : EngineeringObjectBase, ISupplier
{
    public Supplier(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }
}

public sealed class PurchaseItem : EngineeringObjectBase, IPurchaseItem
{
    public Guid SupplierId { get; }
    public Guid? ReferencedObjectId { get; }

    public PurchaseItem(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid supplierId, Guid? referencedObjectId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        SupplierId = supplierId;
        ReferencedObjectId = referencedObjectId;
    }
}
