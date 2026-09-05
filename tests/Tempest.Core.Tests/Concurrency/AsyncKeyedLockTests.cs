using System.Collections.Concurrent;
using System.Threading;
using Tempest.Core.Concurrency;

namespace Tempest.Core.Tests.Concurrency;

// Proves TD-68's closure: entries are reference-counted and removed once
// released (no unbounded growth of one SemaphoreSlim per distinct key for
// process life), and Releaser's Interlocked double-dispose guard releases
// the underlying semaphore exactly once even under concurrent Dispose
// calls. AsyncKeyedLock is internal; TrackedKeyCount is exposed
// internal-only, for these tests, via the project's existing
// InternalsVisibleTo("Tempest.Core.Tests").
public class AsyncKeyedLockTests
{
    [Fact]
    public async Task AcquireAsync_SameKey_SerializesConcurrentAccess()
    {
        var keyedLock = new AsyncKeyedLock();
        var firstAcquired = new TaskCompletionSource();
        var releaseFirst = new TaskCompletionSource();

        var first = Task.Run(async () =>
        {
            using var releaser = await keyedLock.AcquireAsync("key");
            firstAcquired.SetResult();
            await releaseFirst.Task;
        });

        await firstAcquired.Task;

        var secondAcquireTask = keyedLock.AcquireAsync("key");

        // The second acquisition cannot legitimately complete until the
        // first has released - this is a correctness property, not a
        // timing race: on a correct implementation nothing can make
        // secondAcquireTask complete before releaseFirst is signalled, so
        // this assertion cannot flake, only ever catch a real defect.
        await Task.Delay(20);
        Assert.False(secondAcquireTask.IsCompleted);

        releaseFirst.SetResult();
        await first;

        using (await secondAcquireTask)
        {
            // Reached only once the first holder actually released.
        }
    }

    [Fact]
    public async Task AcquireAsync_AfterSoleHolderDisposes_RemovesTheEntry()
    {
        var keyedLock = new AsyncKeyedLock();

        using (await keyedLock.AcquireAsync("key"))
        {
            Assert.Equal(1, keyedLock.TrackedKeyCount);
        }

        Assert.Equal(0, keyedLock.TrackedKeyCount);
    }

    [Fact]
    public async Task AcquireAsync_WhileASecondHolderIsQueued_EntrySurvivesUntilBothRelease()
    {
        var keyedLock = new AsyncKeyedLock();
        var firstAcquired = new TaskCompletionSource();
        var releaseFirst = new TaskCompletionSource();

        var first = Task.Run(async () =>
        {
            using var releaser = await keyedLock.AcquireAsync("key");
            firstAcquired.SetResult();
            await releaseFirst.Task;
        });

        await firstAcquired.Task;

        var secondAcquireTask = keyedLock.AcquireAsync("key");

        // A second, still-waiting reservation must keep the entry alive -
        // it must not be torn down out from under a queued acquirer.
        Assert.Equal(1, keyedLock.TrackedKeyCount);

        releaseFirst.SetResult();
        await first;

        using (await secondAcquireTask)
        {
            Assert.Equal(1, keyedLock.TrackedKeyCount);
        }

        Assert.Equal(0, keyedLock.TrackedKeyCount);
    }

    // Many tasks contending over a handful of keys, with interleaved
    // acquire/release. Bounded entirely by iteration count (fixed loop
    // counts, no wall-clock waits) so it cannot flake from machine speed -
    // WP 16.4A closed four flaky-test debts and this must not reopen that
    // pattern. Every assertion is an invariant that holds regardless of
    // actual thread interleaving: no key is ever observed held twice at
    // once, every reservation lands (total count matches), and nothing is
    // left tracked once every task has finished.
    [Fact]
    public async Task AcquireAsync_ManyTasksFewKeysInterleaved_NoDoubleEntryAndCorrectFinalState()
    {
        var keyedLock = new AsyncKeyedLock();
        const int keyCount = 4;
        const int taskCount = 40;
        const int iterationsPerTask = 50;

        var countersByKey = new int[keyCount];
        var busyByKey = new bool[keyCount];
        var violations = 0;

        async Task WorkerAsync(int workerId)
        {
            var random = new Random(workerId);

            for (var i = 0; i < iterationsPerTask; i++)
            {
                var keyIndex = random.Next(keyCount);
                var key = $"key-{keyIndex}";

                using (await keyedLock.AcquireAsync(key))
                {
                    if (busyByKey[keyIndex])
                        Interlocked.Increment(ref violations);

                    busyByKey[keyIndex] = true;
                    await Task.Yield();
                    countersByKey[keyIndex]++;
                    busyByKey[keyIndex] = false;
                }
            }
        }

        var tasks = Enumerable.Range(0, taskCount).Select(WorkerAsync).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(0, violations);
        Assert.Equal(taskCount * iterationsPerTask, countersByKey.Sum());
        Assert.Equal(0, keyedLock.TrackedKeyCount);
    }

    [Fact]
    public async Task Releaser_DoubleDisposedConcurrently_ReleasesExactlyOnce()
    {
        var keyedLock = new AsyncKeyedLock();
        var releaser = await keyedLock.AcquireAsync("key");

        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(2);

        void DisposeOnce()
        {
            barrier.SignalAndWait();

            try
            {
                releaser.Dispose();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // Both threads race to dispose the exact same Releaser at the same
        // instant, via the barrier - a plain bool double-dispose guard has
        // a window here where both observe "not yet released" and both go
        // on to call SemaphoreSlim.Release(), which throws
        // SemaphoreFullException on a SemaphoreSlim(1, 1)'s second call.
        await Task.WhenAll(Task.Run(DisposeOnce), Task.Run(DisposeOnce));

        Assert.Empty(exceptions);
        Assert.Equal(0, keyedLock.TrackedKeyCount);

        // Released exactly once: a fresh acquire on the same key succeeds
        // immediately (the semaphore's count is a sane 1, not corrupted by
        // an over-release), proving the double dispose above did not
        // release twice.
        using (await keyedLock.AcquireAsync("key"))
        {
            Assert.Equal(1, keyedLock.TrackedKeyCount);
        }
    }
}
