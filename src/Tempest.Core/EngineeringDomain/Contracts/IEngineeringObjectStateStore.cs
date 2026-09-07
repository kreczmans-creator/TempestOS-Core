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
    /// <remarks>
    /// <b>Implementations must be all-or-nothing about failure reporting
    /// (`TD-143`, `WP 16.4B-R7`).</b> If this method throws, the stored
    /// record must be exactly what it was before the call; if it returns,
    /// <paramref name="state"/> is the stored record. An implementation
    /// that commits and <em>then</em> throws breaks its callers, because
    /// <c>EngineeringObjectBase</c>'s mutators undo their in-memory
    /// mutation when this throws — an undo that is correct only while
    /// "threw" means "did not land". The base class cannot verify this
    /// property of an arbitrary implementation and does not try: it states
    /// the requirement here, satisfies it in the one implementation this
    /// platform ships (<c>EngineeringObjectStateStore</c> over
    /// <c>PersistenceStore</c>, whose commit point is a rename with
    /// nothing fallible after it), and records the residual exposure in
    /// <c>EngineeringObjectBase</c>'s own remarks rather than claiming an
    /// atomicity it cannot enforce.
    /// </remarks>
    Task SaveAsync(EngineeringObjectState state, CancellationToken cancellationToken = default);

    /// <summary>Reads one object's state, or <see langword="null"/> when none is persisted.</summary>
    Task<EngineeringObjectState?> FindAsync(Guid objectId, CancellationToken cancellationToken = default);

    /// <summary>Reads every persisted object state — the input to startup rehydration.</summary>
    Task<IReadOnlyList<EngineeringObjectState>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes one object's persisted state.</summary>
    Task DeleteAsync(Guid objectId, CancellationToken cancellationToken = default);
}
