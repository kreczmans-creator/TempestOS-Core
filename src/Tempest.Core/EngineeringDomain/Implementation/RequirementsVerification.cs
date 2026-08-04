using Tempest.Core.EngineeringData;

namespace Tempest.Core.EngineeringDomain;

public sealed class Verification : EngineeringObjectBase, IVerification
{
    public Verification(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        EngineeringObjectMetadata metadata)
        : base(document, currentRevision, context, identifier: null, displayName: document.Kind, metadata)
    {
    }
}

public class VerificationActivity : EngineeringObjectBase, IVerificationActivity
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
}
