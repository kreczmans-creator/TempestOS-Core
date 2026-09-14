using Tempest.Core.EngineeringData;

namespace Tempest.Core.Tests.Verification;

/// <summary>
/// A hand-written <see cref="IEngineeringDocumentStore"/> test double that
/// deliberately does <b>not</b> also implement
/// <c>Tempest.Core.EngineeringDomain.ITransactionalDocumentWriter</c>, used
/// to prove <see cref="Tempest.Core.Verification.VerificationService"/>'s
/// constructor refuses such a store (`TD-23`) rather than silently falling
/// back to a non-transactional sequence of writes.
/// </summary>
internal sealed class NonTransactionalDocumentStore : IEngineeringDocumentStore
{
    public Task<IEngineeringDocument> CreateAsync(string kind, string initialContent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used — this double exists only to fail the constructor's capability check.");

    public Task<IEngineeringDocument?> FindAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used — this double exists only to fail the constructor's capability check.");

    public Task<IDocumentRevision> ReviseAsync(Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used — this double exists only to fail the constructor's capability check.");

    public Task<IReadOnlyList<IDocumentRevision>> GetRevisionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used — this double exists only to fail the constructor's capability check.");

    public Task LinkAsync(Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used — this double exists only to fail the constructor's capability check.");

    public Task<IReadOnlyList<DocumentReference>> GetReferencesAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used — this double exists only to fail the constructor's capability check.");
}
