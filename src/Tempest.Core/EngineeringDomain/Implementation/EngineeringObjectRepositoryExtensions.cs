namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// The caller-side half of "the index is the list" (`TD-88`, `WP 21.5B`):
/// a small, mechanical seam every caller that genuinely needs full object
/// state — not just what an <see cref="EngineeringObjectIndexEntry"/>
/// already carries — materialises through, instead of casting over an
/// already-fully-materialised list the way every caller could before this
/// Work Package.
/// </summary>
public static class EngineeringObjectRepositoryExtensions
{
    /// <summary>
    /// Materialises every one of <paramref name="entries"/> through
    /// <paramref name="repository"/>'s own loader (<see cref="IEngineeringObjectRepository.FindAsync"/>)
    /// and returns the ones that are actually a <typeparamref name="T"/>,
    /// in <paramref name="entries"/>' own order. The mechanical replacement
    /// for a pre-`WP 21.5B` <c>(await repo.ListAllAsync()).OfType&lt;T&gt;()</c>:
    /// the same result, materialisation cost paid only for the rows walked
    /// rather than for the whole estate up front. An id whose
    /// materialisation fails, or whose live object is not a
    /// <typeparamref name="T"/>, is simply omitted — one bad object never
    /// costs the caller every other one (`TD-60`), exactly as it did not
    /// before.
    /// </summary>
    public static async Task<IReadOnlyList<T>> MaterialiseAsync<T>(
        this IEngineeringObjectRepository repository,
        IReadOnlyList<EngineeringObjectIndexEntry> entries,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(entries);

        var result = new List<T>(entries.Count);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await repository.FindAsync(entry.Id, cancellationToken).ConfigureAwait(false) is T typed)
                result.Add(typed);
        }

        return result;
    }

    /// <summary>
    /// Materialises exactly the ids <paramref name="ids"/> names, in that
    /// order — the same seam as <see cref="MaterialiseAsync{T}(IEngineeringObjectRepository,IReadOnlyList{EngineeringObjectIndexEntry},CancellationToken)"/>,
    /// for a caller that already has ids of interest (a selection, a
    /// relationship target list) rather than a fresh index read.
    /// </summary>
    public static async Task<IReadOnlyList<T>> MaterialiseAsync<T>(
        this IEngineeringObjectRepository repository,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(ids);

        var result = new List<T>();
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await repository.FindAsync(id, cancellationToken).ConfigureAwait(false) is T typed)
                result.Add(typed);
        }

        return result;
    }
}
