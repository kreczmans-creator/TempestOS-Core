using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Portfolio : EngineeringObjectBase, IPortfolio, IRehydratable<Portfolio>
{
    public IReadOnlyList<Guid> ProgrammeIds { get; }

    public Portfolio(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<Guid>? programmeIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ProgrammeIds = programmeIds ?? Array.Empty<Guid>();
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        WriteGuidList(state, nameof(ProgrammeIds), ProgrammeIds);

    static Portfolio IRehydratable<Portfolio>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuidList(nameof(ProgrammeIds)));
}

public sealed class Programme : EngineeringObjectBase, IProgramme, IRehydratable<Programme>
{
    public Guid? PortfolioId { get; }
    public IReadOnlyList<Guid> ProjectIds { get; }

    public Programme(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        Guid? portfolioId = null, IReadOnlyList<Guid>? projectIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        PortfolioId = portfolioId;
        ProjectIds = projectIds ?? Array.Empty<Guid>();
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(PortfolioId)] = PortfolioId?.ToString();
        WriteGuidList(state, nameof(ProjectIds), ProjectIds);
    }

    static Programme IRehydratable<Programme>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.TypeGuid(nameof(PortfolioId)), state.TypeGuidList(nameof(ProjectIds)));
}

public sealed class Project : EngineeringObjectBase, IProject, IRehydratable<Project>
{
    public Guid? ProgrammeId { get; }

    public Project(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid? programmeId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ProgrammeId = programmeId;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        state[nameof(ProgrammeId)] = ProgrammeId?.ToString();

    static Project IRehydratable<Project>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.TypeGuid(nameof(ProgrammeId)));
}
