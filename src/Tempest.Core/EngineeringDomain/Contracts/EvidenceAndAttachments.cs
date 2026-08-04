namespace Tempest.Core.EngineeringDomain;

/// <summary>A composed, read-side traversal result — never a stored relationship (WP8.2A Relationship Catalogue §5).</summary>
public interface IEvidence
{
    Guid SubjectId { get; }
    IReadOnlyList<IEngineeringRelationship> SupportingRelationships { get; }
    IReadOnlyList<IVerificationResult> VerificationResults { get; }
    IReadOnlyList<ICalculationResult> CalculationResults { get; }
}

public interface IAttachment
{
    Guid Id { get; }
    string FileName { get; }
    string ContentType { get; }
    long SizeInBytes { get; }
}
