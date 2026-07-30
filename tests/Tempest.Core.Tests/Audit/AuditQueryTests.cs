using Tempest.Core.Audit;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Audit;

public class AuditQueryTests
{
    private static IPrincipal BuildPrincipal(string id) =>
        new PlatformPrincipal(new PlatformIdentity(id, id), []);

    private static async Task<InMemoryPersistenceStore> SeedAsync(params (string ActorId, string Action, DateTimeOffset OccurredAt)[] entries)
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var recorder = new AuditRecorder(store, accessor);

        foreach (var (actorId, action, _) in entries)
        {
            accessor.SetCurrent(BuildPrincipal(actorId));
            await recorder.RecordAsync(action);
        }

        return store;
    }

    // ----------------------------------------------------------------
    // Query filter correctness
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_NoCriteria_ReturnsEveryRecord()
    {
        var store = await SeedAsync(("actor-1", "action-a", default), ("actor-2", "action-b", default));
        var query = BuildGrantedQuery(store);

        var records = await query.QueryAsync(new AuditQueryCriteria());

        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task QueryAsync_FilterByActorId_ReturnsOnlyMatchingRecords()
    {
        var store = await SeedAsync(("actor-1", "action-a", default), ("actor-2", "action-b", default));
        var query = BuildGrantedQuery(store);

        var records = await query.QueryAsync(new AuditQueryCriteria(actorId: "actor-1"));

        var record = Assert.Single(records);
        Assert.Equal("actor-1", record.ActorId);
    }

    [Fact]
    public async Task QueryAsync_FilterByAction_ReturnsOnlyMatchingRecords()
    {
        var store = await SeedAsync(("actor-1", "action-a", default), ("actor-1", "action-b", default));
        var query = BuildGrantedQuery(store);

        var records = await query.QueryAsync(new AuditQueryCriteria(action: "action-a"));

        var record = Assert.Single(records);
        Assert.Equal("action-a", record.Action);
    }

    [Fact]
    public async Task QueryAsync_FilterByDateRange_ExcludesRecordsOutsideRange()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(BuildPrincipal("actor-1"));
        var recorder = new AuditRecorder(store, accessor);
        await recorder.RecordAsync("action-a");
        var afterFirst = DateTimeOffset.UtcNow;
        await Task.Delay(20);
        await recorder.RecordAsync("action-b");

        var query = BuildGrantedQuery(store);
        var records = await query.QueryAsync(new AuditQueryCriteria(from: afterFirst));

        var record = Assert.Single(records);
        Assert.Equal("action-b", record.Action);
    }

    [Fact]
    public async Task QueryAsync_NoMatchingRecords_ReturnsEmpty()
    {
        var store = await SeedAsync(("actor-1", "action-a", default));
        var query = BuildGrantedQuery(store);

        var records = await query.QueryAsync(new AuditQueryCriteria(actorId: "nonexistent"));

        Assert.Empty(records);
    }

    [Fact]
    public async Task QueryAsync_Results_AreOrderedByOccurredAtAscending()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(BuildPrincipal("actor-1"));
        var recorder = new AuditRecorder(store, accessor);
        await recorder.RecordAsync("first");
        await Task.Delay(20);
        await recorder.RecordAsync("second");
        await Task.Delay(20);
        await recorder.RecordAsync("third");

        var query = BuildGrantedQuery(store);
        var records = await query.QueryAsync(new AuditQueryCriteria());

        Assert.Equal(["first", "second", "third"], records.Select(r => r.Action));
    }

    // ----------------------------------------------------------------
    // Permission gating
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_PrincipalHoldsQueryPermission_Succeeds()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var principal = new PlatformPrincipal(new PlatformIdentity("auditor", "Auditor"), [AuditQuery.QueryPermission]);
        accessor.SetCurrent(principal);
        var query = new AuditQuery(store, accessor, new PermissionEvaluator());

        var exception = await Record.ExceptionAsync(() => query.QueryAsync(new AuditQueryCriteria()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task QueryAsync_PrincipalLacksQueryPermission_ThrowsPermissionDeniedException()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("someone", "Someone"), []));
        var query = new AuditQuery(store, accessor, new PermissionEvaluator());

        await Assert.ThrowsAsync<PermissionDeniedException>(() => query.QueryAsync(new AuditQueryCriteria()));
    }

    [Fact]
    public async Task QueryAsync_NoPrincipalEstablished_ThrowsPermissionDeniedException()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var query = new AuditQuery(store, accessor, new PermissionEvaluator());

        await Assert.ThrowsAsync<PermissionDeniedException>(() => query.QueryAsync(new AuditQueryCriteria()));
    }

    // ----------------------------------------------------------------
    // Failure propagation
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_PersistenceThrows_PropagatesUnchanged()
    {
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("auditor", "Auditor"), [AuditQuery.QueryPermission]));
        var query = new AuditQuery(new FailingPersistenceStore(), accessor, new PermissionEvaluator());

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => query.QueryAsync(new AuditQueryCriteria()));
    }

    // ----------------------------------------------------------------
    // Argument validation
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_NullCriteria_ThrowsArgumentNullException()
    {
        var query = BuildGrantedQuery(new InMemoryPersistenceStore());

        await Assert.ThrowsAsync<ArgumentNullException>(() => query.QueryAsync(null!));
    }

    [Fact]
    public void Constructor_NullPersistenceStore_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AuditQuery(null!, new CurrentPrincipalAccessor(), new PermissionEvaluator()));
    }

    [Fact]
    public void Constructor_NullCurrentPrincipalAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AuditQuery(new InMemoryPersistenceStore(), null!, new PermissionEvaluator()));
    }

    [Fact]
    public void Constructor_NullPermissionEvaluator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AuditQuery(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor(), null!));
    }

    // ----------------------------------------------------------------
    // Test helper
    // ----------------------------------------------------------------

    private static AuditQuery BuildGrantedQuery(IPersistenceStore store)
    {
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("auditor", "Auditor"), [AuditQuery.QueryPermission]));
        return new AuditQuery(store, accessor, new PermissionEvaluator());
    }
}
