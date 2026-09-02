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

    /// <summary>
    /// The SHA-256 of the bytes <see cref="IAttachmentContentStore"/> holds
    /// for this attachment, as lowercase hex — or <see langword="null"/>
    /// when this platform holds no content for it (`TD-31`).
    /// </summary>
    /// <remarks>
    /// Metadata <em>about</em> the content, deliberately not the content:
    /// it is what lets a read verify that the bytes that came back are the
    /// bytes that went in, and it keeps an object's state small enough to
    /// rehydrate a whole graph without loading a single file.
    /// <see langword="null"/> is a legitimate, permanent state — an
    /// attachment that describes a file this platform does not hold, which
    /// is every attachment created before `TD-31` and any created as
    /// metadata alone since.
    /// </remarks>
    string? ContentHash { get; }
}
