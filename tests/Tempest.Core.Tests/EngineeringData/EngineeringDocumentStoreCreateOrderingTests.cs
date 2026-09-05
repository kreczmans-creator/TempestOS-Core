using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.EngineeringData;

/// <summary>
/// `TD-67` closure tests: <see cref="EngineeringDocumentStore.CreateAsync"/>
/// now writes revision 1 before the document record — the exact inversion
/// of its own original ordering, mirroring
/// <see cref="EngineeringDocumentStore.ReviseAsync"/>'s own already-proven
/// ordering. A crash between the two writes must leave the store in the
/// crash-safe state (no document record — "not found," never "internally
/// inconsistent") in both directions: whichever write is made to fail,
/// nothing observable through the public API can ever be a document
/// record naming a revision that was never written.
/// </summary>
public class EngineeringDocumentStoreCreateOrderingTests
{
    /// <summary>
    /// A hand-written <see cref="IPersistenceStore"/> test double wrapping
    /// a real one, failing every write into one chosen collection —
    /// deterministic fault injection for exactly one write in a known
    /// sequence, mirroring this repository's own established "small,
    /// test-local fake" convention (see e.g. this folder's own
    /// <c>InMemoryPersistenceStore</c>).
    /// </summary>
    private sealed class WriteFailingPersistenceStore : IPersistenceStore
    {
        private readonly IPersistenceStore _inner;
        private readonly string _failingCollection;

        public WriteFailingPersistenceStore(IPersistenceStore inner, string failingCollection)
        {
            _inner = inner;
            _failingCollection = failingCollection;
        }

        public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(collection, key, cancellationToken);

        public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
        {
            if (string.Equals(collection, _failingCollection, StringComparison.Ordinal))
                throw new PersistenceStoreUnavailableException($"Simulated crash writing collection '{collection}'.");

            return _inner.WriteAsync(collection, key, value, cancellationToken);
        }

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(collection, key, cancellationToken);

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default) =>
            _inner.ListKeysAsync(collection, cancellationToken);
    }

    [Fact]
    public async Task CreateAsync_CrashOnTheDocumentWrite_LeavesNoDocumentRecord_NeverInternallyInconsistent()
    {
        var inner = new InMemoryPersistenceStore();
        var failing = new WriteFailingPersistenceStore(inner, EngineeringDocumentStore.DocumentsCollectionName);
        var store = new EngineeringDocumentStore(failing, new CurrentPrincipalAccessor());

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => store.CreateAsync("Kind", "content"));

        // The crash-safe state this ordering guarantees: no document
        // record exists at all. Every document record that *does* exist
        // therefore still has its own revision 1 (written first) — the
        // "internally inconsistent" GetRevisionHistoryAsync failure this
        // closure removes can only ever have come from a document record
        // naming a revision this store never wrote, and that can no
        // longer happen.
        var documentKeys = await inner.ListKeysAsync(EngineeringDocumentStore.DocumentsCollectionName);
        Assert.Empty(documentKeys);

        // The revision write (first, in the new ordering) did succeed —
        // confirming the ordering actually inverted, not merely that nothing
        // was written.
        var revisionKeys = await inner.ListKeysAsync(EngineeringDocumentStore.RevisionsCollectionName);
        Assert.Single(revisionKeys);
    }

    [Fact]
    public async Task CreateAsync_CrashOnTheRevisionWrite_WritesNeitherRecord()
    {
        var inner = new InMemoryPersistenceStore();
        var failing = new WriteFailingPersistenceStore(inner, EngineeringDocumentStore.RevisionsCollectionName);
        var store = new EngineeringDocumentStore(failing, new CurrentPrincipalAccessor());

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => store.CreateAsync("Kind", "content"));

        // Failing the now-first write means the second (the document
        // record) is never attempted at all — the cleanest possible
        // outcome: nothing written, not even a lone orphaned revision.
        Assert.Empty(await inner.ListKeysAsync(EngineeringDocumentStore.DocumentsCollectionName));
        Assert.Empty(await inner.ListKeysAsync(EngineeringDocumentStore.RevisionsCollectionName));
    }

    [Fact]
    public async Task CreateAsync_NoFailure_StillProducesAFullyConsistentDocument()
    {
        var store = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());

        var document = await store.CreateAsync("Kind", "content");

        var history = await store.GetRevisionHistoryAsync(document.Id);
        Assert.Single(history);
        Assert.Equal("content", history[0].Content);
    }
}
