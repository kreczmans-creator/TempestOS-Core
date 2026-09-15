using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// `WP 21.6A`, OSA-13: proves the audit collection is write-protected at
/// the store — <see cref="SqlitePersistenceStore"/> and the shared
/// in-memory double both refuse a direct write/delete against
/// <see cref="AuditRecorder.AuditCollectionName"/> through the ordinary
/// <see cref="IPersistenceStore"/>/<see cref="IPersistenceTransaction"/>
/// surface, and accept one only through the internal capability
/// <see cref="Audit.AuditRecorder"/>/<see cref="Audit.AuditTransactionWriter"/>
/// hold.
/// </summary>
public class AuditCollectionProtectionTests
{
    // ------------------------------------------------------------
    // InMemoryQueryablePersistenceStore — the double every other test in
    // this suite already trusts to model the real store's transaction
    // semantics honestly (see that type's own remarks).
    // ------------------------------------------------------------

    [Fact]
    public async Task InMemoryStore_WriteAsync_ToTheAuditCollection_Throws()
    {
        var store = new InMemoryQueryablePersistenceStore();

        var exception = await Assert.ThrowsAsync<AuditCollectionProtectedException>(
            () => store.WriteAsync(AuditRecorder.AuditCollectionName, "some-key", "some-value"));

        Assert.Equal(AuditRecorder.AuditCollectionName, exception.Collection);
    }

    [Fact]
    public async Task InMemoryStore_DeleteAsync_FromTheAuditCollection_Throws()
    {
        var store = new InMemoryQueryablePersistenceStore();
        await ((IAuditCollectionWriter)store).WriteAuditRowAsync("some-key", "some-value", CancellationToken.None);

        await Assert.ThrowsAsync<AuditCollectionProtectedException>(
            () => store.DeleteAsync(AuditRecorder.AuditCollectionName, "some-key"));

        // Refused, not silently ignored — the row is still there.
        Assert.Equal("some-value", store.CommittedText(AuditRecorder.AuditCollectionName, "some-key"));
    }

    [Fact]
    public async Task InMemoryStore_TransactionalWriteAsync_ToTheAuditCollection_Throws()
    {
        var store = new InMemoryQueryablePersistenceStore();

        await Assert.ThrowsAsync<AuditCollectionProtectedException>(() =>
            store.ExecuteInTransactionAsync((transaction, ct) =>
                transaction.WriteAsync(AuditRecorder.AuditCollectionName, "some-key", "some-value", ct)));
    }

    [Fact]
    public async Task InMemoryStore_TransactionalDeleteAsync_FromTheAuditCollection_Throws()
    {
        var store = new InMemoryQueryablePersistenceStore();
        await ((IAuditCollectionWriter)store).WriteAuditRowAsync("some-key", "some-value", CancellationToken.None);

        await Assert.ThrowsAsync<AuditCollectionProtectedException>(() =>
            store.ExecuteInTransactionAsync((transaction, ct) =>
                transaction.DeleteAsync(AuditRecorder.AuditCollectionName, "some-key", ct)));
    }

    [Fact]
    public async Task InMemoryStore_TheCapabilityBypass_StillWrites()
    {
        // The one legitimate route — AuditRecorder.RecordAsync itself
        // exercises this end-to-end; this fact pins the capability
        // directly so a regression here is caught at the store layer,
        // not only through the recorder.
        var store = new InMemoryQueryablePersistenceStore();

        await ((IAuditCollectionWriter)store).WriteAuditRowAsync("some-key", "some-value", CancellationToken.None);

        Assert.Equal("some-value", store.CommittedText(AuditRecorder.AuditCollectionName, "some-key"));
    }

    [Fact]
    public async Task InMemoryStore_TheTransactionalCapabilityBypass_StillWrites()
    {
        var store = new InMemoryQueryablePersistenceStore();

        await store.ExecuteInTransactionAsync(async (transaction, ct) =>
        {
            var auditWriter = Assert.IsAssignableFrom<IAuditCollectionTransactionWriter>(transaction);
            await auditWriter.WriteAuditRowAsync("some-key", "some-value", ct);
        });

        Assert.Equal("some-value", store.CommittedText(AuditRecorder.AuditCollectionName, "some-key"));
    }

    // ------------------------------------------------------------
    // The real product store.
    // ------------------------------------------------------------

    private static IConfigurationProvider ConfigurationFor(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    [Fact]
    public async Task SqlitePersistenceStore_WriteAsync_ToTheAuditCollection_Throws()
    {
        using var temp = new TempDirectory();
        await using var store = new SqlitePersistenceStore(ConfigurationFor(temp.Path));

        var exception = await Assert.ThrowsAsync<AuditCollectionProtectedException>(
            () => store.WriteAsync(AuditRecorder.AuditCollectionName, "some-key", "some-value"));

        Assert.Equal(AuditRecorder.AuditCollectionName, exception.Collection);
    }

    [Fact]
    public async Task SqlitePersistenceStore_DeleteAsync_FromTheAuditCollection_Throws()
    {
        using var temp = new TempDirectory();
        await using var store = new SqlitePersistenceStore(ConfigurationFor(temp.Path));

        await Assert.ThrowsAsync<AuditCollectionProtectedException>(
            () => store.DeleteAsync(AuditRecorder.AuditCollectionName, "some-key"));
    }

    [Fact]
    public async Task SqlitePersistenceStore_TransactionalWriteAsync_ToTheAuditCollection_Throws()
    {
        using var temp = new TempDirectory();
        await using var store = new SqlitePersistenceStore(ConfigurationFor(temp.Path));

        await Assert.ThrowsAsync<AuditCollectionProtectedException>(() =>
            store.ExecuteInTransactionAsync((transaction, ct) =>
                transaction.WriteAsync(AuditRecorder.AuditCollectionName, "some-key", "some-value", ct)));
    }

    [Fact]
    public async Task SqlitePersistenceStore_RealAuditRecorder_RoundTripsThroughTheCapability()
    {
        // End to end, over the real store: AuditRecorder.RecordAsync
        // succeeds (it holds the capability), and AuditQuery reads back
        // exactly what it wrote.
        using var temp = new TempDirectory();
        await using var store = new SqlitePersistenceStore(ConfigurationFor(temp.Path));
        var principalAccessor = new CurrentPrincipalAccessor();
        principalAccessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("auditor", "Auditor"), [AuditQuery.QueryPermission]));
        var recorder = new AuditRecorder(store, principalAccessor);
        var query = new AuditQuery(store, principalAccessor, new PermissionEvaluator());

        await recorder.RecordAsync("real-store-action");

        var records = await query.QueryAsync(new AuditQueryCriteria(action: "real-store-action"));
        Assert.Single(records);
    }
}
