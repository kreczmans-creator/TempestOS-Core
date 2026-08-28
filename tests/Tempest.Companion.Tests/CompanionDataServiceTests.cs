using Tempest.Companion.Client;
using Tempest.Companion.Contracts;
using Tempest.Companion.Offline;
using Tempest.Companion.Services;

namespace Tempest.Companion.Tests;

// Proves CompanionDataService (ADR-0115) - the whole offline state
// machine: live fetch stores the snapshot; an unreachable platform falls
// back to Cached, then Stale past the threshold, then Unavailable when
// nothing was ever stored; 401/403 fail closed (no cached fallback);
// reconnection returns to Live; connection-state transitions are raised
// exactly on change; and a mutation is never queued offline.
public class CompanionDataServiceTests
{
    private static readonly CompanionApiException Unreachable =
        new(CompanionApiFailureReason.Unreachable, "TempestOS could not be reached.");

    [Fact]
    public async Task Online_ReturnsLive_AndStoresTheSnapshot()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient();
        var cache = new SnapshotCache(temp.Path);
        var service = new CompanionDataService(client, cache);

        var result = await service.GetCockpitAsync();

        Assert.Equal(DataFreshness.Live, result.Freshness);
        Assert.NotNull(result.Data);
        Assert.Null(result.Error);
        Assert.NotNull(cache.Load<CockpitSummaryDto>("cockpit"));
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task Offline_WithRecentSnapshot_ReturnsCached()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient();
        var cache = new SnapshotCache(temp.Path);
        var now = DateTimeOffset.UtcNow;
        var service = new CompanionDataService(client, cache, () => now);

        await service.GetCockpitAsync();
        client.Failure = Unreachable;
        now = now.AddMinutes(5);

        var result = await service.GetCockpitAsync();

        Assert.Equal(DataFreshness.Cached, result.Freshness);
        Assert.NotNull(result.Data);
        Assert.Equal("TempestOS could not be reached.", result.Error);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task Offline_WithOldSnapshot_ReturnsStale()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient();
        var cache = new SnapshotCache(temp.Path);
        var now = DateTimeOffset.UtcNow;
        var service = new CompanionDataService(client, cache, () => now);

        await service.GetCockpitAsync();
        client.Failure = Unreachable;
        now = now + CompanionDataService.StaleAfter + TimeSpan.FromMinutes(1);

        var result = await service.GetCockpitAsync();

        Assert.Equal(DataFreshness.Stale, result.Freshness);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Offline_NothingEverStored_ReturnsUnavailable()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient { Failure = Unreachable };
        var service = new CompanionDataService(client, new SnapshotCache(temp.Path));

        var result = await service.GetCockpitAsync();

        Assert.Equal(DataFreshness.Unavailable, result.Freshness);
        Assert.Null(result.Data);
        Assert.Equal("TempestOS could not be reached.", result.Error);
    }

    [Theory]
    [InlineData(CompanionApiFailureReason.Unauthorized)]
    [InlineData(CompanionApiFailureReason.Forbidden)]
    public async Task DeniedCaller_NeverServedFromCache(CompanionApiFailureReason reason)
    {
        // Fail closed: a caller the platform refused must not keep
        // reading previously cached engineering data (WP 14.0A security
        // review).
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient();
        var cache = new SnapshotCache(temp.Path);
        var service = new CompanionDataService(client, cache);

        await service.GetCockpitAsync();
        client.Failure = new CompanionApiException(reason, "denied");

        var result = await service.GetCockpitAsync();

        Assert.Equal(DataFreshness.Unavailable, result.Freshness);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Reconnection_ReturnsToLive_AndRefreshesTheSnapshot()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient();
        var cache = new SnapshotCache(temp.Path);
        var service = new CompanionDataService(client, cache);

        await service.GetCockpitAsync();
        client.Failure = Unreachable;
        await service.GetCockpitAsync();
        client.Failure = null;
        client.Cockpit = FakeCompanionApiClient.CannedCockpit("Healthy");

        var result = await service.GetCockpitAsync();

        Assert.Equal(DataFreshness.Live, result.Freshness);
        Assert.Equal("Healthy", result.Data!.Health);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task ConnectionStateChanged_RaisedOnlyOnTransitions()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient();
        var service = new CompanionDataService(client, new SnapshotCache(temp.Path));
        var transitions = new List<bool>();
        service.ConnectionStateChanged += transitions.Add;

        await service.GetCockpitAsync();
        await service.GetActivityAsync();
        client.Failure = Unreachable;
        await service.GetCockpitAsync();
        await service.GetActivityAsync();
        client.Failure = null;
        await service.GetCockpitAsync();

        Assert.Equal([true, false, true], transitions);
    }

    [Fact]
    public async Task SetDocumentStatus_Unreachable_ThrowsRatherThanQueueing()
    {
        // ADR-0115 / AT-24: no offline write queue - the mutation either
        // reaches the authoritative platform now or fails visibly.
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient { Failure = Unreachable };
        var service = new CompanionDataService(client, new SnapshotCache(temp.Path));

        await Assert.ThrowsAsync<CompanionApiException>(() =>
            service.SetDocumentStatusAsync(new SetObjectStatusRequest(Guid.NewGuid(), "Document", "Approved")));

        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task SetDocumentStatus_Online_ReturnsTheCommandOutcome()
    {
        using var temp = new TempDirectory();
        var client = new FakeCompanionApiClient { ActionOutcome = new(false, "'X' cannot transition from Draft to Draft.") };
        var service = new CompanionDataService(client, new SnapshotCache(temp.Path));

        var outcome = await service.SetDocumentStatusAsync(new SetObjectStatusRequest(Guid.NewGuid(), "Document", "Draft"));

        Assert.False(outcome.Succeeded);
        Assert.Contains("cannot transition", outcome.Message);
    }
}
