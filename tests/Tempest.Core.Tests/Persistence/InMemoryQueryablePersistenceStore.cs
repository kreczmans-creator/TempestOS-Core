using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// The test suite's one persistence double: the whole shape of the
/// platform's durable store — text, bytes, queries and <b>real</b>
/// transactions — held in memory (`ADR-0145`, `WP 17.1B`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one double replaces a dozen.</b> Before `ADR-0145` an
/// engineering object's truth lived in four places, so a test that wanted
/// to fail one of them wrote a fake for that one: a hostile object-state
/// store, a link-failing document store, a byte store that throws on the
/// third write. Nine separate <c>InMemoryPersistenceStore</c> classes and
/// six <c>FailingPersistenceStore</c> classes grew up in this project that
/// way, each implementing <see cref="IPersistenceStore"/> and each
/// slightly different.
/// </para>
/// <para>
/// There is now one durable authority and one write path, so a test that
/// wants to fail a write fails <em>the</em> store, and a test that wants
/// to observe what landed reads <em>the</em> store. That is the point of
/// the ADR expressed as a test fixture: if a fault can only be injected
/// into one of four independent stores, the four stores are still there.
/// </para>
/// <para>
/// <b>Copy-on-write, so rollback is real.</b>
/// <see cref="ExecuteInTransactionAsync"/> copies the committed map,
/// hands the body a handle that reads and writes only the copy, and
/// publishes the copy as the new committed map when the body returns
/// normally. If the body throws, the copy is dropped and the committed
/// map is untouched — which is a genuine rollback rather than an undo
/// script, and is exactly what SQLite's <c>ROLLBACK</c> gives the
/// production path. A test can therefore assert "nothing landed" against
/// the same mechanism production relies on.
/// </para>
/// <para>
/// <b>One writer at a time</b>, like SQLite's <c>BEGIN IMMEDIATE</c>. The
/// semaphore is what makes the copy-and-publish safe: without it two
/// overlapping transactions would each copy the same base map and the
/// second to finish would silently discard the first's writes. The
/// concurrency facts (`TD-145`, `TD-146`) depend on this being modelled
/// honestly rather than on the double being conveniently permissive.
/// </para>
/// <para>
/// Values are immutable as stored — <see cref="string"/>, and byte arrays
/// that are copied in on write and out on read — so a shallow copy of the
/// map is a complete snapshot.
/// </para>
/// </remarks>
public sealed class InMemoryQueryablePersistenceStore
    : IPersistenceStore, IBinaryPersistenceStore, IQueryablePersistenceStore
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _publishLock = new();

    private Dictionary<(string Collection, string Key), Entry> _committed = new();
    private long _sequence;

    /// <summary>The number of transactions that committed.</summary>
    public int CommitCount { get; private set; }

    /// <summary>The number of transactions that rolled back.</summary>
    public int RollbackCount { get; private set; }

    /// <summary>Every key currently committed in <paramref name="collection"/>, in ascending ordinal order.</summary>
    /// <remarks>
    /// A synchronous window onto committed state for assertions, so a fact
    /// saying "nothing landed" does not have to go through the same
    /// asynchronous surface it is testing.
    /// </remarks>
    public IReadOnlyList<string> CommittedKeys(string collection)
    {
        lock (_publishLock)
        {
            return _committed.Keys
                .Where(k => string.Equals(k.Collection, collection, StringComparison.Ordinal))
                .Select(k => k.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>The committed text value under <paramref name="key"/>, or <see langword="null"/>.</summary>
    public string? CommittedText(string collection, string key)
    {
        lock (_publishLock)
            return _committed.TryGetValue((collection, key), out var entry) ? entry.Text : null;
    }

    /// <summary>The committed bytes under <paramref name="key"/>, or <see langword="null"/>.</summary>
    public byte[]? CommittedBytes(string collection, string key)
    {
        lock (_publishLock)
            return _committed.TryGetValue((collection, key), out var entry) ? entry.Bytes?.ToArray() : null;
    }

    // ================================================================
    // IPersistenceStore
    // ================================================================

    /// <inheritdoc />
    public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        Validate(collection, key);
        return Task.FromResult(CommittedText(collection, key));
    }

    /// <inheritdoc />
    public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
    {
        Validate(collection, key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_publishLock)
        {
            var next = new Dictionary<(string, string), Entry>(_committed) { [(collection, key)] = Entry.OfText(value) };
            _committed = next;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        Validate(collection, key);

        lock (_publishLock)
        {
            var next = new Dictionary<(string, string), Entry>(_committed);
            next.Remove((collection, key));
            _committed = next;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default)
    {
        ValidateCollection(collection);
        return Task.FromResult(CommittedKeys(collection));
    }

    // ================================================================
    // IBinaryPersistenceStore
    // ================================================================

    /// <inheritdoc />
    public Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        Validate(collection, key);
        return Task.FromResult(CommittedBytes(collection, key));
    }

    /// <inheritdoc />
    public Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        Validate(collection, key);

        lock (_publishLock)
        {
            var next = new Dictionary<(string, string), Entry>(_committed)
            {
                [(collection, key)] = Entry.OfBytes(value.ToArray()),
            };
            _committed = next;
        }

        return Task.CompletedTask;
    }

    // ================================================================
    // IQueryablePersistenceStore
    // ================================================================

    /// <inheritdoc />
    public long CurrentSequence
    {
        get { lock (_publishLock) return _sequence; }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default)
    {
        ValidateCollection(collection);
        ArgumentNullException.ThrowIfNull(keyPrefix);

        var matching = CommittedKeys(collection)
            .Where(k => k.StartsWith(keyPrefix, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(matching);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default)
    {
        ValidateCollection(collection);

        lock (_publishLock)
        {
            var all = _committed
                .Where(e => string.Equals(e.Key.Collection, collection, StringComparison.Ordinal) && e.Value.Text is not null)
                .OrderBy(e => e.Key.Key, StringComparer.Ordinal)
                .Select(e => new KeyValuePair<string, string>(e.Key.Key, e.Value.Text!))
                .ToList();

            return Task.FromResult<IReadOnlyList<KeyValuePair<string, string>>>(all);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(
        string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ValidateCollection(collection);
        ArgumentNullException.ThrowIfNull(keys);

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var key in keys)
            result[key] = CommittedText(collection, key);

        return Task.FromResult<IReadOnlyDictionary<string, string?>>(result);
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<(string, string), Entry> working;
            lock (_publishLock)
                working = new Dictionary<(string, string), Entry>(_committed);

            var transaction = new Transaction(working);

            try
            {
                await work(transaction, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // The working copy is simply dropped. Nothing to undo,
                // which is the property `ADR-0145` relies on.
                RollbackCount++;
                throw;
            }

            transaction.Close();

            lock (_publishLock)
            {
                _committed = working;
                _sequence++;
            }

            CommitCount++;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<T> ExecuteInReadTransactionAsync<T>(
        Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        // No lock needed beyond the one that captures the pair: `_committed`
        // is replaced wholesale on every commit (copy-on-write), never
        // mutated in place, so a captured reference is an immutable
        // snapshot for as long as this method holds it — true isolation
        // from every later writer, without contending with one.
        Dictionary<(string, string), Entry> committed;
        long sequence;
        lock (_publishLock)
        {
            committed = _committed;
            sequence = _sequence;
        }

        return read(new ReadTransaction(committed, sequence), cancellationToken);
    }

    private static void Validate(string collection, string key)
    {
        ValidateCollection(collection);

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("A key is required.", nameof(key));
    }

    private static void ValidateCollection(string collection)
    {
        if (string.IsNullOrWhiteSpace(collection))
            throw new ArgumentException("A collection is required.", nameof(collection));
    }

    private sealed record Entry(string? Text, byte[]? Bytes)
    {
        public static Entry OfText(string text) => new(text, null);

        public static Entry OfBytes(byte[] bytes) => new(null, bytes);
    }

    /// <summary>
    /// The handle handed to a transaction body. Reads and writes the
    /// working copy only; unusable once the transaction has closed.
    /// </summary>
    private sealed class Transaction(Dictionary<(string Collection, string Key), Entry> working) : IPersistenceTransaction
    {
        private bool _closed;

        public void Close() => _closed = true;

        public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            Validate(collection, key);
            return Task.FromResult(working.TryGetValue((collection, key), out var entry) ? entry.Text : null);
        }

        public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            Validate(collection, key);
            ArgumentNullException.ThrowIfNull(value);

            working[(collection, key)] = Entry.OfText(value);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            Validate(collection, key);

            working.Remove((collection, key));
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadBytesAsync(string collection, string key, CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            Validate(collection, key);
            return Task.FromResult(working.TryGetValue((collection, key), out var entry) ? entry.Bytes?.ToArray() : null);
        }

        public Task WriteBytesAsync(string collection, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            Validate(collection, key);

            working[(collection, key)] = Entry.OfBytes(value.ToArray());
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            ValidateCollection(collection);
            ArgumentNullException.ThrowIfNull(keyPrefix);

            var matching = working.Keys
                .Where(k => string.Equals(k.Collection, collection, StringComparison.Ordinal)
                            && k.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
                .Select(k => k.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(matching);
        }

        private void ThrowIfClosed()
        {
            if (_closed)
                throw new InvalidOperationException(
                    "This transaction handle was used after its body returned. A handle is valid only for the duration " +
                    "of the ExecuteInTransactionAsync call that produced it.");
        }
    }

    /// <summary>
    /// The handle handed to a read. Reads the captured, immutable snapshot
    /// only — never <c>_committed</c> itself, which may already have moved
    /// on by the time this runs.
    /// </summary>
    private sealed class ReadTransaction(Dictionary<(string Collection, string Key), Entry> snapshot, long sequence) : IPersistenceReadTransaction
    {
        public long Sequence => sequence;

        public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default)
        {
            Validate(collection, key);
            return Task.FromResult(snapshot.TryGetValue((collection, key), out var entry) ? entry.Text : null);
        }

        public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default)
        {
            ValidateCollection(collection);

            var all = snapshot
                .Where(e => string.Equals(e.Key.Collection, collection, StringComparison.Ordinal) && e.Value.Text is not null)
                .OrderBy(e => e.Key.Key, StringComparer.Ordinal)
                .Select(e => new KeyValuePair<string, string>(e.Key.Key, e.Value.Text!))
                .ToList();

            return Task.FromResult<IReadOnlyList<KeyValuePair<string, string>>>(all);
        }

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default)
        {
            ValidateCollection(collection);
            ArgumentNullException.ThrowIfNull(keyPrefix);

            var matching = snapshot.Keys
                .Where(k => string.Equals(k.Collection, collection, StringComparison.Ordinal)
                            && k.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
                .Select(k => k.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(matching);
        }
    }
}
