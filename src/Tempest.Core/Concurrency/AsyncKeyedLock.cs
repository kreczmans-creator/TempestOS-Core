using System.Collections.Concurrent;

namespace Tempest.Core.Concurrency;

/// <summary>
/// A minimal per-key asynchronous mutual-exclusion lock.
/// </summary>
/// <remarks>
/// Used internally by <see cref="Persistence.PersistenceStore"/> and
/// <see cref="Settings.SettingsProvider"/> to serialise concurrent
/// operations against the same key without serialising access to two
/// different keys against each other — both services' own Thread Safety
/// Expectations (<c>Platform Service Contracts.md</c>) require exactly
/// this granularity. Placed in its own small, neutral namespace rather
/// than inside either consumer's own folder, since it genuinely serves
/// two independent services, not one — <c>Reuse Before Invention</c>
/// applied once a second real consumer existed, not before.
/// </remarks>
internal sealed class AsyncKeyedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    /// <summary>
    /// Acquires the lock for <paramref name="key"/>, waiting if another
    /// caller currently holds it. Dispose the returned value to release.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        var semaphore = _semaphores.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose()
        {
            if (_released)
                return;

            _released = true;
            _semaphore.Release();
        }
    }
}
