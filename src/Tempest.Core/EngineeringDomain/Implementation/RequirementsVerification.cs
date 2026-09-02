using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Verification : EngineeringObjectBase, IVerification, IRehydratable<Verification>
{
    public Verification(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier: null, displayName: document.Kind, metadata)
    {
    }

    static Verification IRehydratable<Verification>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Metadata);
}

public class VerificationActivity : EngineeringObjectBase, IVerificationActivity, IRehydratable<VerificationActivity>
{
    public Guid SubjectId { get; }
    public string Method { get; }

    public VerificationActivity(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string displayName, EngineeringObjectMetadata metadata, Guid subjectId, string method)
        : base(document, currentRevision, context, identifier: null, displayName, metadata)
    {
        SubjectId = subjectId;
        Method = method;
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(SubjectId)] = SubjectId.ToString();
        state[nameof(Method)] = Method;
    }

    /// <summary>The subject this and every derived activity Kind persist identically.</summary>
    private protected static Guid ReadSubjectId(EngineeringObjectState state) => state.TypeGuidOrEmpty(nameof(SubjectId));

    /// <summary>The method this and every derived activity Kind persist identically.</summary>
    private protected static string ReadMethod(EngineeringObjectState state) => state.Type(nameof(Method)) ?? string.Empty;

    static VerificationActivity IRehydratable<VerificationActivity>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.DisplayName, state.Metadata, ReadSubjectId(state), ReadMethod(state));
}
