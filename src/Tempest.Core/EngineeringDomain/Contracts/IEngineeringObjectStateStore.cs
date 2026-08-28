namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The durable store of <see cref="EngineeringObjectState"/> — what makes
/// an engineering object survive a process restart (`TD-85`).
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="EngineeringData.IEngineeringDocumentStore"/>:
/// the document owns identity, Kind and revision history; this store owns
/// the object state the document was never designed to carry. Together
/// they are one authority, split by concern — never two competing ones.
/// </remarks>
public interface IEngineeringObjectStateStore
{
    /// <summary>Writes <paramref name="state"/>, replacing any previous record for the same object.</summary>
    Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default);

    /// <summary>Reads one object's state, or <see langword="null"/> when none is persisted.</summary>
    Task<EngineeringObjectState?> FindAsync(Guid objectId, CancellationToken cancellationToken = default);

    /// <summary>Reads every persisted object state — the input to startup rehydration.</summary>
    Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes one object's persisted state.</summary>
    Task DeleteAsync(Guid objectId, CancellationToken cancellationToken = default);
}
