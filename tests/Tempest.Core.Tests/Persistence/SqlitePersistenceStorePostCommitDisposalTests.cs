using Tempest.Core.Configuration;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Logging;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// `TD-150`'s acceptance fact at the store itself: a transaction whose
/// <c>COMMIT;</c> returns and whose connection then fails to close reports
/// success, logs the close failure, and leaves the write durable — readable
/// by a fresh store instance opened over the same root afterwards
/// (`SqlitePersistenceStore.ExecuteInTransactionAsync`'s own remarks,
/// `ADR-0145` addendum, `WP 19.10J`).
/// </summary>
/// <remarks>
/// Drives <see cref="SqlitePersistenceStore.TestOnlyConnectionCloser"/>
/// directly — the internal seam that lets a test make the real store's own
/// post-commit close step throw without touching production code (see that
/// property's own remarks for why no decorator over
/// <see cref="IQueryablePersistenceStore"/> could do this instead). The
/// contrast fact below drives the same seam to fail a close whose
/// transaction never committed, and confirms that path is unchanged:
/// this Work Package narrows what propagates, it does not widen what is
/// swallowed.
/// </remarks>
public sealed class SqlitePersistenceStorePostCommitDisposalTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly List<SqlitePersistenceStore> _stores = [];

    public void Dispose()
    {
        for (var i = _stores.Count - 1; i >= 0; i--)
            _stores[i].Dispose();

        _root.Dispose();
    }

    private static IConfigurationProvider ConfigurationFor(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    private SqlitePersistenceStore NewStore(ILogger? logger = null)
    {
        var store = new SqlitePersistenceStore(ConfigurationFor(_root.Path), logger);
        _stores.Add(store);
        return store;
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CloseFailsAfterCommit_ReturnsSuccessfully_LogsIt_AndTheWriteSurvivesReopen()
    {
        var logger = new RecordingLogger();
        var store = NewStore(logger);

        store.TestOnlyConnectionCloser = async connection =>
        {
            await connection.DisposeAsync();
            throw new IOException("Injected post-commit close failure.");
        };

        // No exception reaches the caller — a throw here fails the test.
        await store.ExecuteInTransactionAsync(
            (transaction, token) => transaction.WriteAsync("collection", "key", "value", token));

        Assert.Contains(
            logger.Messages,
            m => m.Contains("could not close its connection", StringComparison.Ordinal));

        // Durable: a second, independent store instance over the same root
        // reads it back — not the same connection, not the same process
        // state, only what SQLite itself persisted.
        store.Dispose();
        _stores.Remove(store);

        var reopened = NewStore();
        Assert.Equal("value", await reopened.ReadAsync("collection", "key"));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CloseFailsBeforeCommit_StillPropagates()
    {
        var store = NewStore();

        store.TestOnlyConnectionCloser = async connection =>
        {
            await connection.DisposeAsync();
            throw new IOException("Injected close failure on an uncommitted transaction.");
        };

        // The transaction body itself throws, before COMMIT ever runs;
        // TD-150 narrows the *committed* path only, so the close failure
        // here is expected still to propagate rather than be swallowed —
        // exactly the pre-existing behaviour for a failed transaction's
        // cleanup, and the contrast that proves the fix is bounded by
        // `committed` rather than "never propagate a close failure".
        await Assert.ThrowsAsync<IOException>(() =>
            store.ExecuteInTransactionAsync((transaction, token) =>
                throw new InvalidOperationException("The transaction body refuses.")));
    }
}
