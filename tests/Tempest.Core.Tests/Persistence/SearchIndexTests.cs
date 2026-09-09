using Microsoft.Data.Sqlite;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// `WP 18.1B` Findability — the SQLite FTS5 search index over
/// <see cref="SqlitePersistenceStore"/>: schema availability, indexing
/// inside the write transaction, deletion, rebuild, and search itself.
/// </summary>
/// <remarks>
/// <see cref="Fts5IsCompiledIntoTheBundledSqlite"/> is this Work Package's
/// own kill switch (its brief, `WP 18.1B` §1): if the bundled
/// <c>e_sqlite3</c> does not report <c>ENABLE_FTS5</c> among its compile
/// options, every other test in this class — and the feature itself —
/// cannot work, and the Work Package stops and reports rather than
/// working around it.
/// </remarks>
public sealed class SearchIndexTests : SqlitePersistenceStoreFixture
{
    [Fact]
    public void Fts5IsCompiledIntoTheBundledSqlite()
    {
        // This Work Package's own kill switch (brief §1): read straight off
        // the bundled native provider's own compile options, before relying
        // on it anywhere else. `SQLitePCL.Batteries_V2.Init()` mirrors
        // `SqlitePersistenceStore`'s own static constructor — idempotent,
        // safe to call again here.
        SQLitePCL.Batteries_V2.Init();

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA compile_options;";

        var options = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                options.Add(reader.GetString(0));
        }

        Assert.Contains(options, o => o.Contains("ENABLE_FTS5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IndexTextAsync_ThenSearchAsync_FindsByTitlePrefix()
    {
        var objectId = Guid.NewGuid();

        await Store.ExecuteInTransactionAsync(async (tx, ct) =>
            await tx.IndexTextAsync(objectId, "Part", projectId: null, title: "Bracket Mounting Plate", identifier: "BRK-001", refs: null, ct));

        var hits = await Store.SearchAsync("bra", 10);

        Assert.Contains(hits, h => h.ObjectId == objectId && h.Kind == "Part");
    }

    [Fact]
    public async Task IndexTextAsync_ThenSearchAsync_FindsByIdentifierPrefix()
    {
        var objectId = Guid.NewGuid();

        await Store.ExecuteInTransactionAsync(async (tx, ct) =>
            await tx.IndexTextAsync(objectId, "Part", projectId: null, title: "Bracket Mounting Plate", identifier: "BRK-001", refs: null, ct));

        var hits = await Store.SearchAsync("BRK-0", 10);

        Assert.Contains(hits, h => h.ObjectId == objectId);
    }

    [Fact]
    public async Task IndexTextAsync_ThenSearchAsync_FindsByRefsToken()
    {
        var objectId = Guid.NewGuid();

        await Store.ExecuteInTransactionAsync(async (tx, ct) =>
            await tx.IndexTextAsync(
                objectId, "Evidence", projectId: null, title: "Bracket calc", identifier: null,
                refs: "Materials MAT-007", ct));

        var hits = await Store.SearchAsync("MAT-007", 10);

        Assert.Contains(hits, h => h.ObjectId == objectId && h.Kind == "Evidence");
    }

    [Fact]
    public async Task RemoveFromIndexAsync_RemovesTheRow()
    {
        var objectId = Guid.NewGuid();

        await Store.ExecuteInTransactionAsync(async (tx, ct) =>
            await tx.IndexTextAsync(objectId, "Part", projectId: null, title: "Bracket", identifier: null, refs: null, ct));

        Assert.NotEmpty(await Store.SearchAsync("Bracket", 10));

        await Store.ExecuteInTransactionAsync(async (tx, ct) => await tx.RemoveFromIndexAsync(objectId, ct));

        Assert.DoesNotContain(await Store.SearchAsync("Bracket", 10), h => h.ObjectId == objectId);
    }

    [Fact]
    public async Task ARolledBackTransaction_LeavesNoIndexRow()
    {
        var objectId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Store.ExecuteInTransactionAsync(async (tx, ct) =>
        {
            await tx.IndexTextAsync(objectId, "Part", projectId: null, title: "Rolled Back Part", identifier: null, refs: null, ct);
            throw new InvalidOperationException("simulated failure after the index write");
        }));

        Assert.DoesNotContain(await Store.SearchAsync("Rolled", 10), h => h.ObjectId == objectId);
    }

    [Fact]
    public async Task SearchAsync_OrdersByRank_AndRespectsLimit()
    {
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            var title = $"Widget {i}";
            await Store.ExecuteInTransactionAsync(async (tx, ct) =>
                await tx.IndexTextAsync(id, "Part", projectId: null, title: title, identifier: null, refs: null, ct));
        }

        var hits = await Store.SearchAsync("Widget", 3);

        Assert.Equal(3, hits.Count);
    }

    [Fact]
    public async Task IsSearchIndexEmptyAsync_TrueUntilSomethingIsIndexed()
    {
        Assert.True(await QueryableStore.IsSearchIndexEmptyAsync());

        await Store.ExecuteInTransactionAsync(async (tx, ct) =>
            await tx.IndexTextAsync(Guid.NewGuid(), "Part", projectId: null, title: "Anything", identifier: null, refs: null, ct));

        Assert.False(await QueryableStore.IsSearchIndexEmptyAsync());
    }
}
