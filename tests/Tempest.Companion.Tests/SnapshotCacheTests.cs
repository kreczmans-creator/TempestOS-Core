using Tempest.Companion.Contracts;
using Tempest.Companion.Offline;

namespace Tempest.Companion.Tests;

// Proves SnapshotCache (ADR-0115) - store/load round-trips through real
// files, corrupt content reads as "no snapshot" rather than throwing, and
// Clear removes everything (the device-hygiene path).
public class SnapshotCacheTests
{
    [Fact]
    public void StoreThenLoad_RoundTripsDataAndFetchTime()
    {
        using var temp = new TempDirectory();
        var cache = new SnapshotCache(temp.Path);
        var fetchedAt = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

        cache.Store("activity", new ActivityDto(fetchedAt, []), fetchedAt);
        var loaded = cache.Load<ActivityDto>("activity");

        Assert.NotNull(loaded);
        Assert.Equal(fetchedAt, loaded.Value.FetchedAtUtc);
        Assert.Empty(loaded.Value.Data.RecentActivity);
    }

    [Fact]
    public void Load_NothingStored_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var cache = new SnapshotCache(temp.Path);

        Assert.Null(cache.Load<ActivityDto>("activity"));
    }

    [Fact]
    public void Load_CorruptFile_ReturnsNullRatherThanThrowing()
    {
        using var temp = new TempDirectory();
        var cache = new SnapshotCache(temp.Path);
        cache.Store("activity", new ActivityDto(DateTimeOffset.UtcNow, []), DateTimeOffset.UtcNow);

        File.WriteAllText(Path.Combine(temp.Path, "cache", "activity.json"), "{ not json ]");

        Assert.Null(cache.Load<ActivityDto>("activity"));
    }

    [Fact]
    public void Clear_RemovesEveryStoredSnapshot()
    {
        using var temp = new TempDirectory();
        var cache = new SnapshotCache(temp.Path);
        cache.Store("activity", new ActivityDto(DateTimeOffset.UtcNow, []), DateTimeOffset.UtcNow);

        cache.Clear();

        Assert.Null(cache.Load<ActivityDto>("activity"));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "cache")));
    }

    [Fact]
    public void Store_OverwritesThePriorSnapshot()
    {
        using var temp = new TempDirectory();
        var cache = new SnapshotCache(temp.Path);
        var older = new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

        cache.Store("cockpit", FakeCompanionApiClient.CannedCockpit("Healthy"), older);
        cache.Store("cockpit", FakeCompanionApiClient.CannedCockpit("Blocked"), newer);

        var loaded = cache.Load<CockpitSummaryDto>("cockpit");
        Assert.NotNull(loaded);
        Assert.Equal("Blocked", loaded.Value.Data.Health);
        Assert.Equal(newer, loaded.Value.FetchedAtUtc);
    }
}
