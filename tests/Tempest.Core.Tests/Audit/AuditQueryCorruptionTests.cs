using Tempest.Core.Audit;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

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
        var store = new InMemoryPersistenceStore();
        var recorderAccessor = new CurrentPrincipalAccessor();
        var recorder = new AuditRecorder(store, recorderAccessor);
        await recorder.RecordAsync("action-a");

        var keys = await store.ListKeysAsync(AuditRecorder.AuditCollectionName);
        await store.WriteAsync(AuditRecorder.AuditCollectionName, keys[0], "{{{not json");

        var query = BuildGrantedQuery(store);
        await Assert.ThrowsAsync<AuditException>(() => query.QueryAsync(new AuditQueryCriteria()));
    }

    private static AuditQuery BuildGrantedQuery(IPersistenceStore store)
    {
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("auditor", "Auditor"), [AuditQuery.QueryPermission]));
        return new AuditQuery(store, accessor, new PermissionEvaluator());
    }
}
