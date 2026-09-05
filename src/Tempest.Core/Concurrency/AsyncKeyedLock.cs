using System.Collections.Concurrent;

namespace Tempest.Core.Concurrency;

/// <summary>
/// A minimal per-key asynchronous mutual-exclusion lock.
/// </summary>
/// <remarks>
/// <para>
/// Used internally by <see cref="Persistence.PersistenceStore"/>,
/// <see cref="Settings.SettingsProvider"/>,
/// <see cref="EngineeringData.EngineeringDocumentStore"/>,
/// <see cref="Materials.MaterialCatalog"/>, and
/// <see cref="Requirements.RequirementsService"/> to serialise concurrent
/// operations against the same key without serialising access to two
/// different keys against each other — each service's own Thread Safety
/// Expectations (<c>Platform Service Contracts.md</c>) require exactly
/// this granularity. Placed in its own small, neutral namespace rather
/// than inside any one consumer's own folder, since it genuinely serves
/// several independent services, not one — <c>Reuse Before Invention</c>
/// applied once a second real consumer existed, not before.
/// </para>
/// <para>
/// <b>Lifetime (TD-68):</b> each key's <see cref="SemaphoreSlim"/> is held
/// behind a reference-counted <see cref="Entry"/> that is removed from the
/// backing dictionary, and disposed, the moment its last holder disposes
/// the <see cref="IDisposable"/> returned by <see cref="AcquireAsync"/> —
/// keys do not accumulate for process life. See <see cref="ReleaseRef"/>
/// for the exact race this closes and how.
/// </para>
/// </remarks>
internal sealed class AsyncKeyedLock
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    /// <summary>
    /// Gets the number of keys currently tracked (i.e. with at least one
    /// caller waiting on or holding the lock for that key). Exposed only
    /// for <c>Tempest.Core.Tests</c> to assert that keys are actually
    /// removed once released, not to support any production behaviour.
    /// </summary>
    internal int TrackedKeyCount => _entries.Count;

    /// <summary>
    /// Acquires the lock for <paramref name="key"/>, waiting if another
    /// caller currently holds it. Dispose the returned value to release.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var entry = _entries.GetOrAdd(key, static _ => new Entry());
            var acquired = false;

            lock (entry)
            {
                if (!entry.IsRemoved)
                {
                    entry.RefCount++;
                    acquired = true;
                }
            }

            if (!acquired)
            {
                // The entry we just fetched was already retired by a
                // concurrent release (see ReleaseRef) between our GetOrAdd
                // and our lock — its removal from the dictionary happened
                // before IsRemoved became visible to us (both under the
                // same per-entry lock), so a fresh GetOrAdd is guaranteed
                // to either find a genuinely new entry for this key or
                // create one. Retry rather than ever waiting on this one's
                // semaphore.
                continue;
            }

            try
            {
                await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // We reserved this entry above but never actually acquired
                // the semaphore (cancellation, or some other WaitAsync
                // failure) — release the reservation so the entry does not
                // leak forever waiting for a Dispose that will never come.
                ReleaseRef(key, entry);
                throw;
            }

            return new Releaser(this, key, entry);
        }
    }

    /// <summary>
    /// Releases one caller's reservation of <paramref name="entry"/>
    /// (registered under <paramref name="key"/>), removing and disposing
    /// the entry once nothing references it any more.
    /// </summary>
    /// <remarks>
    /// <b>The race this closes:</b> a naive "if (--RefCount == 0) then
    /// <see cref="ConcurrentDictionary{TKey,TValue}.TryRemove(TKey, out TValue)"/>"
    /// pair, performed as two separate steps against a
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/>, has a well-known
    /// TOCTOU window: between this thread deciding the count has reached
    /// zero and this thread actually removing the entry, a second caller's
    /// <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/>
    /// can observe the same (about-to-be-removed) entry, take a reference
    /// on it, and start waiting on a <see cref="SemaphoreSlim"/> that this
    /// thread is about to dispose out from under it.
    /// <para>
    /// This is closed with a lock on the entry object itself, held across
    /// both the refcount mutation <em>and</em> the dictionary removal:
    /// <see cref="AcquireAsync"/> only ever increments <see cref="Entry.RefCount"/>
    /// after confirming, under this same lock, that <see cref="Entry.IsRemoved"/>
    /// is not yet set — and because the removal from <see cref="_entries"/>
    /// happens here while still holding that lock, by the time
    /// <see cref="Entry.IsRemoved"/> becomes visible to another thread the
    /// entry is already gone from the dictionary. A concurrent acquirer can
    /// therefore never rediscover a retired entry and wait on its disposed
    /// semaphore; at worst it retries once with a fresh
    /// <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/>.
    /// </para>
    /// </remarks>
    private void ReleaseRef(string key, Entry entry)
    {
        var removed = false;

        lock (entry)
        {
            entry.RefCount--;

            if (entry.RefCount == 0)
            {
                entry.IsRemoved = true;
                _entries.TryRemove(key, out _);
                removed = true;
            }
        }

        if (removed)
            entry.Semaphore.Dispose();
    }

    private sealed class Entry
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);

        /// <summary>
        /// The number of callers that have reserved this entry (waiting on
        /// or holding <see cref="Semaphore"/>) and not yet released it.
        /// Guarded entirely by <c>lock (this)</c> — see
        /// <see cref="AsyncKeyedLock.ReleaseRef"/>.
        /// </summary>
        public int RefCount;

        /// <summary>
        /// Set once, under <c>lock (this)</c>, the moment <see cref="RefCount"/>
        /// reaches zero and this entry has been removed from the owning
        /// dictionary — a permanent tombstone; this <see cref="Entry"/>
        /// instance is never reused afterwards.
        /// </summary>
        public bool IsRemoved;
    }

    private sealed class Releaser : IDisposable
    {
        private readonly AsyncKeyedLock _owner;
        private readonly string _key;

        // Doubles as the double-dispose guard: Interlocked.Exchange hands
        // the entry to exactly one caller even when Dispose() is invoked
        // concurrently from two threads, so the semaphore is released and
        // the reservation dropped exactly once (TD-68) — a plain bool
        // guard is not safe against that race.
        private Entry? _entry;

        public Releaser(AsyncKeyedLock owner, string key, Entry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public void Dispose()
        {
            var entry = Interlocked.Exchange(ref _entry, null);
            if (entry is null)
                return;

            entry.Semaphore.Release();
            _owner.ReleaseRef(_key, entry);
        }
    }
}
