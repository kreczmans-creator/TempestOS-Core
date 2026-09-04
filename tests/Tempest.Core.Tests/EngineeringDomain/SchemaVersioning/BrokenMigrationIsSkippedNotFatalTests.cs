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
        var store = new EngineeringObjectStateStore(persistence, registry, logger);

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
        await store.SaveAsync(BuildState(goodId, "TD87TestOtherKind", 1, "X-GOOD"));

        var registry = new StateMigrationRegistry();
        registry.Register(new ThrowingMigration(ThrowingKind, 1));

        var states = await new EngineeringObjectStateStore(persistence, registry).ListAsync();

        var survivor = Assert.Single(states);
        Assert.Equal(goodId, survivor.Id);
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
