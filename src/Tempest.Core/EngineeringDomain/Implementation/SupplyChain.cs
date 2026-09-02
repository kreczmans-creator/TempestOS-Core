using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Supplier : EngineeringObjectBase, ISupplier, IRehydratable<Supplier>
{
    public Supplier(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }

    static Supplier IRehydratable<Supplier>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
}

public sealed class PurchaseItem : EngineeringObjectBase, IPurchaseItem, IRehydratable<PurchaseItem>
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

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(SupplierId)] = SupplierId.ToString();
        state[nameof(ReferencedObjectId)] = ReferencedObjectId?.ToString();
    }

    static PurchaseItem IRehydratable<PurchaseItem>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.TypeGuidOrEmpty(nameof(SupplierId)), state.TypeGuid(nameof(ReferencedObjectId)));
}
