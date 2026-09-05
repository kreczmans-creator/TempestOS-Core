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
    // WP16.4A-R1: every TaskCompletionSource-based gate below waits at
    // most this long — long enough that a correct, fast implementation
    // never comes close, short enough that a seam which stops being
    // reached (a regression in AcquireAsync, or a signal a future change
    // forgets to send) fails in seconds with a named TimeoutException
    // instead of hanging until CI's own 30-minute job timeout kills the
    // whole matrix leg without saying why.
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

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
            await releaseFirst.Task.WaitAsync(GateTimeout);
        });

        await firstAcquired.Task.WaitAsync(GateTimeout);

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
            await releaseFirst.Task.WaitAsync(GateTimeout);
        });

        await firstAcquired.Task.WaitAsync(GateTimeout);

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

    // WP16.4A-R1: a single Barrier(2) race, run once, essentially never
    // catches this bug. Reverting Releaser.Dispose()'s Interlocked.Exchange
    // guard to a plain, non-atomic `bool` and running the single-shot
    // version of this test 20 full times produced 0 detections; a
    // standalone 2,000-trial probe against the same reverted guard caught
    // it in 9 trials (0.45%). A single-shot test built on a race that thin
    // buys no real confidence - it passes against genuinely broken code on
    // all but a handful of runs in a thousand.
    //
    // The fix is not a better single race - no single Barrier(2) pairing
    // can be made more reliable than the scheduler's own willingness to
    // interleave two threads at the SignalAndWait release point, which is
    // exactly what the 0.45% reflects - but many independent trials of
    // that same race inside one test, bounded purely by iteration count
    // (never wall-clock, per WP 16.4A's own no-flake discipline): the same
    // looped-real-thread-race idiom NavigationServiceTrustTests already
    // uses for its own registration race
    // (Register_ConcurrentRegistrantsForSameId_HighestTierAlwaysEndsUpSoleOwner).
    //
    // Iteration count: treating the measured 9/2000 = 0.45% as this race's
    // per-trial detection probability p, N independent iterations catch
    // the bug at least once with probability 1 - (1 - p)^N. Solving
    // 1 - (1 - 0.0045)^N >= 0.99 gives N >= ln(0.01) / ln(0.9955) ~= 1021.
    // 2,000 iterations - the same order of magnitude as the reviewer's own
    // probe, comfortably above that threshold - gives
    // 1 - (0.9955)^2000 ~= 1 - e^-9 ~= 99.99% detection probability, while
    // still running in well under a second: each iteration is just two
    // Task.Run calls meeting at a barrier and disposing a Releaser, no I/O
    // and no allocation-heavy work.
    [Fact]
    public async Task Releaser_DoubleDisposedConcurrently_ReleasesExactlyOnce()
    {
        const int iterations = 2000;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var keyedLock = new AsyncKeyedLock();
            var releaser = await keyedLock.AcquireAsync("key");

            var exceptions = new ConcurrentBag<Exception>();
            using var barrier = new Barrier(2);

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

            // Both threads race to dispose the exact same Releaser at the
            // same instant, via the barrier - a plain bool double-dispose
            // guard has a window here where both observe "not yet
            // released" and both go on to call SemaphoreSlim.Release(),
            // which throws SemaphoreFullException on a
            // SemaphoreSlim(1, 1)'s second call.
            await Task.WhenAll(Task.Run(DisposeOnce), Task.Run(DisposeOnce));

            Assert.Empty(exceptions);
            Assert.Equal(0, keyedLock.TrackedKeyCount);

            // Released exactly once: a fresh acquire on the same key
            // succeeds immediately (the semaphore's count is a sane 1, not
            // corrupted by an over-release), proving the double dispose
            // above did not release twice.
            using (await keyedLock.AcquireAsync("key"))
            {
                Assert.Equal(1, keyedLock.TrackedKeyCount);
            }
        }
    }
}
