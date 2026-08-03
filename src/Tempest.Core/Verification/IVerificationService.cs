namespace Tempest.Core.Verification;

/// <summary>Records and retrieves verification outcomes against a document (typically, but not necessarily, a requirement).</summary>
public interface IVerificationService
{
    /// <summary>Records a verification outcome against <paramref name="subjectDocumentId"/>.</summary>
    /// <exception cref="EngineeringData.EngineeringDocumentNotFoundException"><paramref name="subjectDocumentId"/>, or any document Id linked through <paramref name="context"/>, does not exist.</exception>
    /// <exception cref="ArgumentException"><paramref name="method"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    Task<IVerificationRecord> RecordAsync(
        Guid subjectDocumentId,
        VerificationOutcome outcome,
        string method,
        VerificationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every verification recorded against <paramref name="subjectDocumentId"/>,
    /// oldest first. Never <see langword="null"/> — empty if
    /// <paramref name="subjectDocumentId"/> has no recorded verifications,
    /// or does not exist.
    /// </summary>
    /// <exception cref="Identity.PermissionDeniedException">The current principal does not hold the verification-read permission.</exception>
    Task<IReadOnlyList<IVerificationRecord>> GetVerificationHistoryAsync(Guid subjectDocumentId, CancellationToken cancellationToken = default);
}
