using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// A wrapper around a real <see cref="IPersistenceStore"/> that pauses
/// <see cref="ListKeysAsync"/> on whichever of a fixed, small set of
/// watched collections is read <em>second</em> — by order of arrival,
/// never by name. Shared by the Materials and Requirements reconciliation
/// race tests (`WP16.4A-R1`), whose sweeps each read a handful of
/// collections through <see cref="IPersistenceStore.ListKeysAsync"/> in a
/// specific derived-side-then-authoritative-side order.
/// </summary>
/// <remarks>
/// A gate keyed to one collection's name only pauses the interleaving
/// under the read order that name happens to hold <em>today</em>: it
/// waits for that specific collection's own <c>ListKeysAsync</c> call and
/// assumes — without checking — that the call arrives after the other
/// side's own read has already landed. Revert the sweep's read order (the
/// exact regression `WP16.4A-R1` exists to catch) and the watched
/// collection becomes the <em>first</em> call instead of the second; the
/// gate still fires on it, but by then nothing has interleaved yet — the
/// racing write either hasn't started or runs to completion entirely
/// inside the pause, and the broken interleaving this test claims to
/// exercise never occurs. All assertions still pass, against genuinely
/// broken code.
/// <para>
/// Watching arrival order instead — pausing whichever of the watched
/// names is the second <c>ListKeysAsync</c> call to arrive, regardless of
/// which one — has no such blind spot: it reproduces the derived-vs-
/// authoritative race under either read order, which is the entire point
/// of a test meant to catch someone reverting that order back.
/// </para>
/// <para>
/// The watched set is deliberately narrow (named collections only, not
/// "every <c>ListKeysAsync</c> call") because some sweeps — Requirements'
/// in particular — read more than two collections per run; a test racing
/// one specific index/registry against the documents collection must not
/// also trip on an unrelated registry's own read landing in between.
/// </para>
/// </remarks>
internal sealed class OrderAgnosticGatedListKeysPersistenceStore : IPersistenceStore
{
    /// <summary>
    /// How long a paused call, or a test awaiting <see cref="ReachedGate"/>,
    /// waits before giving up (`WP16.4A-R1`). Every gate in this suite is
    /// released deterministically by the same test that armed it, with no
    /// legitimate reason to wait anywhere near this long — this exists
    /// purely so a seam that stops being reached (a future change moves
    /// the watched collections out of the race, or removes the release
    /// call entirely) fails in seconds, naming itself with a
    /// <see cref="TimeoutException"/>, instead of hanging silently until
    /// the CI job's own 30-minute <c>timeout-minutes</c> backstop kills
    /// the whole matrix leg with no indication of which test caused it.
    /// </summary>
    public static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    private readonly IPersistenceStore _inner;
    private readonly HashSet<string> _watchedCollections;
    private readonly TaskCompletionSource _reachedGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _watchedCallCount;

    /// <param name="inner">The real store to delegate every call to.</param>
    /// <param name="watchedCollections">
    /// The collection names this gate cares about. The second
    /// <see cref="ListKeysAsync"/> call against any of these — whichever
    /// name it turns out to be — pauses until <see cref="Release"/> is
    /// called. Calls against any other collection, and any call beyond
    /// the second among these, pass straight through.
    /// </param>
    public OrderAgnosticGatedListKeysPersistenceStore(IPersistenceStore inner, params string[] watchedCollections)
    {
        _inner = inner;
        _watchedCollections = new HashSet<string>(watchedCollections, StringComparer.Ordinal);
    }

    /// <summary>Completes once the second watched <see cref="ListKeysAsync"/> call has arrived and is paused.</summary>
    public Task ReachedGate => _reachedGate.Task;

    /// <summary>Lets the paused call proceed to the real, underlying read.</summary>
    public void Release() => _releaseGate.TrySetResult();

    public async Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default)
    {
        if (_watchedCollections.Contains(collection) && Interlocked.Increment(ref _watchedCallCount) == 2)
        {
            _reachedGate.TrySetResult();
            await _releaseGate.Task.WaitAsync(GateTimeout, cancellationToken).ConfigureAwait(false);
        }

        return await _inner.ListKeysAsync(collection, cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(collection, key, cancellationToken);

    public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(collection, key, value, cancellationToken);

    public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(collection, key, cancellationToken);
}
