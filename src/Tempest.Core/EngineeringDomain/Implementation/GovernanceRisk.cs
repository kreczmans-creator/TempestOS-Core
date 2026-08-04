using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Issue : EngineeringObjectBase, IIssue
{
    public Issue(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }
}

public class Risk : EngineeringObjectBase, IRisk
{
    public string? Likelihood { get; }
    public string? Severity { get; }

    public Risk(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? likelihood = null, string? severity = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        Likelihood = likelihood;
        Severity = severity;
    }
}

public sealed class Hazard : Risk, IHazard
{
    public Hazard(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? likelihood = null, string? severity = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, likelihood, severity)
    {
    }
}

public sealed class Decision : EngineeringObjectBase, IDecision
{
    public string Rationale { get; }

    public Decision(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string rationale)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        Rationale = rationale;
    }
}

public sealed class Assumption : EngineeringObjectBase, IAssumption
{
    public Assumption(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }
}
