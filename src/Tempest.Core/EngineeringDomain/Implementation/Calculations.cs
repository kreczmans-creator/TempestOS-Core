using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Calculation : EngineeringObjectBase, ICalculation, IRehydratable<Calculation>
{
    public Calculation(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }

    static Calculation IRehydratable<Calculation>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
}

public sealed class CalculationSet : EngineeringObjectBase, ICalculationSet, IRehydratable<CalculationSet>
{
    public IReadOnlyList<Guid> MemberCalculationIds { get; }

    public CalculationSet(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<Guid>? memberCalculationIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        MemberCalculationIds = memberCalculationIds ?? Array.Empty<Guid>();
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        WriteGuidList(state, nameof(MemberCalculationIds), MemberCalculationIds);

    static CalculationSet IRehydratable<CalculationSet>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuidList(nameof(MemberCalculationIds)));
}
