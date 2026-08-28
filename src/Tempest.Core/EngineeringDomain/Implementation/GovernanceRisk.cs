using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Issue : EngineeringObjectBase, IIssue, IRehydratable<Issue>
{
    public Issue(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }

    static Issue IRehydratable<Issue>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
}

public class Risk : EngineeringObjectBase, IRisk, IRehydratable<Risk>
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

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(Likelihood)] = Likelihood;
        state[nameof(Severity)] = Severity;
    }

    static Risk IRehydratable<Risk>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.Type(nameof(Likelihood)), state.Type(nameof(Severity)));
}

public sealed class Hazard : Risk, IHazard, IRehydratable<Hazard>
{
    public Hazard(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string? likelihood = null, string? severity = null)
        : base(document, currentRevision, context, identifier, displayName, metadata, likelihood, severity)
    {
    }

    static Hazard IRehydratable<Hazard>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.Type(nameof(Likelihood)), state.Type(nameof(Severity)));
}

public sealed class Decision : EngineeringObjectBase, IDecision, IRehydratable<Decision>
{
    public string Rationale { get; }

    public Decision(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata, string rationale)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        Rationale = rationale;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state) =>
        state[nameof(Rationale)] = Rationale;

    static Decision IRehydratable<Decision>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata, state.Type(nameof(Rationale)) ?? string.Empty);
}

public sealed class Assumption : EngineeringObjectBase, IAssumption, IRehydratable<Assumption>
{
    public Assumption(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
    }

    static Assumption IRehydratable<Assumption>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata);
}
