using Tempest.Core.Audit;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.Audit;

/// <summary>
/// `TD-60` closure tests — a corrupted stored audit record must surface
/// from the passive <see cref="AuditQuery.QueryAsync"/> path as a
/// controlled <see cref="AuditException"/>, never a raw
/// <see cref="System.Text.Json.JsonException"/>.
/// </summary>
public class AuditQueryCorruptionTests
{
    [Fact]
    public async Task QueryAsync_CorruptedStoredRecord_ThrowsControlledAuditException()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var recorderAccessor = new CurrentPrincipalAccessor();
        var recorder = new AuditRecorder(store, recorderAccessor);
        await recorder.RecordAsync("action-a");

        var keys = await store.ListKeysAsync(AuditRecorder.AuditCollectionName);

        // `WP 21.6A`, OSA-13: the store's own ordinary WriteAsync now
        // refuses this collection unconditionally — this fact corrupts a
        // stored row on purpose (to prove QueryAsync's own controlled
        // failure path), the identical simulated-corruption route this
        // audit's own OSA-13 finding named, so it goes through the same
        // internal capability AuditRecorder itself uses rather than the
        // now-refused ordinary path.
        await ((IAuditCollectionWriter)store).WriteAuditRowAsync(keys[0], "{{{not json", CancellationToken.None);

        var query = BuildGrantedQuery(store);
        await Assert.ThrowsAsync<AuditException>(() => query.QueryAsync(new AuditQueryCriteria()));
    }

    private static AuditQuery BuildGrantedQuery(IQueryablePersistenceStore store)
    {
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("auditor", "Auditor"), [AuditQuery.QueryPermission]));
        return new AuditQuery(store, accessor, new PermissionEvaluator());
    }
}
