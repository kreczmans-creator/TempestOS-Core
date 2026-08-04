using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Portfolio : EngineeringObjectBase, IPortfolio
{
    public IReadOnlyList<Guid> ProgrammeIds { get; }

    public Portfolio(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, IReadOnlyList<Guid>? programmeIds = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ProgrammeIds = programmeIds ?? Array.Empty<Guid>();
    }
}

public sealed class Programme : EngineeringObjectBase, IProgramme
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
}

public sealed class Project : EngineeringObjectBase, IProject
{
    public Guid? ProgrammeId { get; }

    public Project(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, Guid? programmeId = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ProgrammeId = programmeId;
    }
}
