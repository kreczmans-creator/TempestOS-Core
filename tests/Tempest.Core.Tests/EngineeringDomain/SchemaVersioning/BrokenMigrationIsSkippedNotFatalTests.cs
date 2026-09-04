using Tempest.Core.EngineeringDomain;
using Tempest.Core.Tests.Logging;

namespace Tempest.Core.Tests.EngineeringDomain.SchemaVersioning;

/// <summary>
/// `TD-87`/`ADR-0120` Decision 5 — a record whose version cannot be
/// bridged is logged and skipped, extending (not duplicating) the
/// discipline <see cref="EngineeringObjectStateStore"/> already applies to
/// malformed JSON: a migration that throws, and a record from a newer
/// build than this one knows about, both degrade to "unreadable, skipped,
/// logged" rather than aborting the whole read.
/// </summary>
public class BrokenMigrationIsSkippedNotFatalTests
{
    private const string ThrowingKind = "TD87TestThrowingKind";

    private sealed class ThrowingMigration(string? kind, int fromVersion) : IStateMigration
    {
        public string? Kind { get; } = kind;
        public int FromVersion { get; } = fromVersion;

        public EngineeringObjectState Migrate(EngineeringObjectState state) =>
            throw new InvalidOperationException("This migration is deliberately broken, for the test that proves it does not take down the whole read.");
    }

    private static EngineeringObjectState BuildState(Guid id, string kind, int schemaVersion, string identifier) => new(
        schemaVersion, id, kind, identifier, "Test object", EngineeringObjectMetadata.Empty,
        LifecycleState.Draft, null, false, EngineeringObjectBomLineState.Default, [], [],
        new Dictionary<string, string?>());

    [Fact]
    public async Task AThrowingMigration_IsCaught_LoggedAsWarning_AndTheRecordIsSkipped()
    {
        var persistence = new InMemoryPersistenceStore();
        var id = Guid.NewGuid();
        await new EngineeringObjectStateStore(persistence).SaveAsync(BuildState(id, ThrowingKind, 1, "X-1"));

        var registry = new StateMigrationRegistry();
        registry.Register(new ThrowingMigration(ThrowingKind, 1));

        var logger = new RecordingLogger();
        // CurrentSchemaVersion is a fixed const 1 this release, so an
        // explicit, higher target is what lets the store's own read path
        // attempt this FromVersion:1 migration at all.
        var store = new EngineeringObjectStateStore(persistence, registry, logger, targetSchemaVersion: 2);

        var state = await store.FindAsync(id);

        Assert.Null(state);
        var message = Assert.Single(logger.Messages);
        Assert.Contains(id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(ThrowingKind, message, StringComparison.Ordinal);
        Assert.Contains("1", message, StringComparison.Ordinal); // the stuck version
    }

    [Fact]
    public async Task AThrowingMigration_DoesNotCostTheNextRecordInAListAsyncBatch()
    {
        var persistence = new InMemoryPersistenceStore();
        var store = new EngineeringObjectStateStore(persistence);

        var brokenId = Guid.NewGuid();
        var goodId = Guid.NewGuid();
        await store.SaveAsync(BuildState(brokenId, ThrowingKind, 1, "X-BROKEN"));
        // Seeded already at the target version, so its own survival is not
        // itself dependent on a migration running for its Kind.
        await store.SaveAsync(BuildState(goodId, "TD87TestOtherKind", 2, "X-GOOD"));

        var registry = new StateMigrationRegistry();
        registry.Register(new ThrowingMigration(ThrowingKind, 1));

        var states = await new EngineeringObjectStateStore(persistence, registry, null, 2).ListAsync();

        var survivor = Assert.Single(states);
        Assert.Equal(goodId, survivor.Id);
    }

    [Fact]
    public async Task ARecordWithNoMigrationPathToItsTarget_IsSkipped_AndEveryOtherObjectStillComesBack()
    {
        // `ADR-0120` Decision 5's other named case: not a throwing
        // migration, not a record from the future — simply a v1 record at
        // a target (2) nothing bridges to, because no migration for
        // (kind, 1) was registered at all. The surviving record is seeded
        // directly at the target version, so its own success is not
        // itself dependent on any migration running.
        var persistence = new InMemoryPersistenceStore();
        var plainStore = new EngineeringObjectStateStore(persistence);

        var stuckId = Guid.NewGuid();
        var goodId = Guid.NewGuid();
        await plainStore.SaveAsync(BuildState(stuckId, "TD87TestNoPathKind", 1, "X-STUCK"));
        await plainStore.SaveAsync(BuildState(goodId, "Part", 2, "X-GOOD"));

        var logger = new RecordingLogger();
        var target2Store = new EngineeringObjectStateStore(persistence, null, logger, 2);

        var stuck = await target2Store.FindAsync(stuckId);
        Assert.Null(stuck);
        var message = Assert.Single(logger.Messages, m => m.Contains(stuckId.ToString(), StringComparison.Ordinal));
        Assert.Contains("TD87TestNoPathKind", message, StringComparison.Ordinal);
        Assert.Contains("1", message, StringComparison.Ordinal); // the stuck version
        Assert.Contains("2", message, StringComparison.Ordinal); // the target version

        var states = await target2Store.ListAsync();
        var survivor = Assert.Single(states);
        Assert.Equal(goodId, survivor.Id);
        Assert.Equal(2, survivor.SchemaVersion);
    }

    [Fact]
    public async Task ARecordFromANewerBuild_IsSkipped_NotThrown()
    {
        var persistence = new InMemoryPersistenceStore();
        var id = Guid.NewGuid();

        // No migration registered for version 99 at all — nor could there
        // ever be one this build knows about; SchemaVersion 99 is beyond
        // EngineeringObjectStateStore.CurrentSchemaVersion.
        await new EngineeringObjectStateStore(persistence).SaveAsync(BuildState(id, "Part", 99, "X-FUTURE"));

        var logger = new RecordingLogger();
        var state = await new EngineeringObjectStateStore(persistence, logger: logger).FindAsync(id);

        Assert.Null(state);
        var message = Assert.Single(logger.Messages);
        Assert.Contains(id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("99", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARecordFromANewerBuild_DoesNotCostTheNextRecordInAListAsyncBatch()
    {
        var persistence = new InMemoryPersistenceStore();
        var store = new EngineeringObjectStateStore(persistence);

        var futureId = Guid.NewGuid();
        var goodId = Guid.NewGuid();
        await store.SaveAsync(BuildState(futureId, "Part", 99, "X-FUTURE"));
        await store.SaveAsync(BuildState(goodId, "Part", 1, "X-GOOD"));

        var states = await new EngineeringObjectStateStore(persistence).ListAsync();

        var survivor = Assert.Single(states);
        Assert.Equal(goodId, survivor.Id);
    }
}
