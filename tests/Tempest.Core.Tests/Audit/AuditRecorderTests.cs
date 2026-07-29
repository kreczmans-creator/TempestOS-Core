using Tempest.Core.Audit;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Audit;

public class AuditRecorderTests
{
    private static IPrincipal BuildPrincipal(string id) =>
        new PlatformPrincipal(new PlatformIdentity(id, id), []);

    // ----------------------------------------------------------------
    // Actor resolution
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecordAsync_PrincipalEstablished_RecordsThatActorId()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(BuildPrincipal("actor-1"));
        var recorder = new AuditRecorder(store, accessor);
        var query = new AuditQuery(store, accessor, GrantingEvaluator());

        await recorder.RecordAsync("action.performed");
        var records = await query.QueryAsync(new AuditQueryCriteria());

        var record = Assert.Single(records);
        Assert.Equal("actor-1", record.ActorId);
        Assert.Equal("action.performed", record.Action);
    }

    [Fact]
    public async Task RecordAsync_NoPrincipalEstablished_RecordsUnknownActorId()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var recorder = new AuditRecorder(store, accessor);
        var query = new AuditQuery(store, accessor, GrantingEvaluator());

        await recorder.RecordAsync("action.performed");
        var records = await query.QueryAsync(new AuditQueryCriteria());

        var record = Assert.Single(records);
        Assert.Equal(AuditRecorder.UnknownActorId, record.ActorId);
    }

    // ----------------------------------------------------------------
    // Timestamp handling
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecordAsync_SetsOccurredAtToApproximatelyNow()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var recorder = new AuditRecorder(store, accessor);
        var query = new AuditQuery(store, accessor, GrantingEvaluator());

        var before = DateTimeOffset.UtcNow;
        await recorder.RecordAsync("action.performed");
        var after = DateTimeOffset.UtcNow;

        var record = Assert.Single(await query.QueryAsync(new AuditQueryCriteria()));
        Assert.InRange(record.OccurredAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    // ----------------------------------------------------------------
    // Detail, including the correlation-id convention
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecordAsync_NoDetailSupplied_RecordsEmptyDetail()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var recorder = new AuditRecorder(store, accessor);
        var query = new AuditQuery(store, accessor, GrantingEvaluator());

        await recorder.RecordAsync("action.performed");
        var record = Assert.Single(await query.QueryAsync(new AuditQueryCriteria()));

        Assert.Empty(record.Detail);
    }

    [Fact]
    public async Task RecordAsync_DetailWithCorrelationId_RoundTrips()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var recorder = new AuditRecorder(store, accessor);
        var query = new AuditQuery(store, accessor, GrantingEvaluator());
        var detail = new Dictionary<string, string> { [AuditRecorder.CorrelationIdDetailKey] = "corr-123" };

        await recorder.RecordAsync("action.performed", detail);
        var record = Assert.Single(await query.QueryAsync(new AuditQueryCriteria()));

        Assert.Equal("corr-123", record.Detail[AuditRecorder.CorrelationIdDetailKey]);
    }

    // ----------------------------------------------------------------
    // Failure propagation
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecordAsync_PersistenceThrows_PropagatesUnchanged()
    {
        var recorder = new AuditRecorder(new FailingPersistenceStore(), new CurrentPrincipalAccessor());

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => recorder.RecordAsync("action.performed"));
    }

    // ----------------------------------------------------------------
    // Concurrency: recording never loses a record
    // ----------------------------------------------------------------

    [Fact]
    public async Task ConcurrentRecordAsyncCalls_NeverLoseARecord()
    {
        var store = new InMemoryPersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var recorder = new AuditRecorder(store, accessor);
        var query = new AuditQuery(store, accessor, GrantingEvaluator());

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => recorder.RecordAsync($"action-{i}")));

        var records = await query.QueryAsync(new AuditQueryCriteria());
        Assert.Equal(50, records.Count);
        Assert.Equal(50, records.Select(r => r.Action).Distinct().Count());
    }

    // ----------------------------------------------------------------
    // Argument validation
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordAsync_NullEmptyOrWhitespaceAction_ThrowsArgumentException(string? action)
    {
        var recorder = new AuditRecorder(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());

        await Assert.ThrowsAsync<ArgumentException>(() => recorder.RecordAsync(action!));
    }

    [Fact]
    public void Constructor_NullPersistenceStore_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditRecorder(null!, new CurrentPrincipalAccessor()));
    }

    [Fact]
    public void Constructor_NullCurrentPrincipalAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditRecorder(new InMemoryPersistenceStore(), null!));
    }

    // ----------------------------------------------------------------
    // Test helper
    // ----------------------------------------------------------------

    private static IPermissionEvaluator GrantingEvaluator() => new AlwaysGrantingPermissionEvaluator();

    private sealed class AlwaysGrantingPermissionEvaluator : IPermissionEvaluator
    {
        public bool HasPermission(IPrincipal principal, Permission permission) => true;
        public void RequirePermission(IPrincipal principal, Permission permission) { }
    }
}
