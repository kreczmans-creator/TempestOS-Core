using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.EngineeringDomain.SchemaVersioning;

/// <summary>
/// `TD-87`/`ADR-0120` Decision 2 — the migration chain
/// <see cref="EngineeringObjectStateStore"/>'s own read path walks: a
/// common (<c>Kind: null</c>) chain and one chain per Kind, applied
/// repeatedly — common first at any version both have a migration for,
/// then that Kind's own — until no further migration applies.
/// </summary>
public class StateMigrationChainTests
{
    private const string TestKind = "TD87TestChainKind";

    /// <summary>A migration that records its own name, in order, and otherwise leaves the record untouched — the store stamps the resulting version.</summary>
    private sealed class LoggingMigration(string? kind, int fromVersion, string name, List<string> log) : IStateMigration
    {
        public string? Kind { get; } = kind;
        public int FromVersion { get; } = fromVersion;

        public EngineeringObjectState Migrate(EngineeringObjectState state)
        {
            log.Add(name);
            return state;
        }
    }

    private static async Task<Guid> SeedAsync(InMemoryPersistenceStore persistence, string kind, int schemaVersion)
    {
        var id = Guid.NewGuid();
        var state = new EngineeringObjectState(
            schemaVersion, id, kind, "X-1", "Test object", EngineeringObjectMetadata.Empty,
            LifecycleState.Draft, null, false, EngineeringObjectBomLineState.Default, [], [],
            new Dictionary<string, string?>());

        // SaveAsync never migrates (migration is a read-path concern only,
        // ADR-0120 Decision 2) — the plain, unregistered store below writes
        // exactly the version handed to it.
        await new EngineeringObjectStateStore(persistence).SaveAsync(state);
        return id;
    }

    [Fact]
    public async Task AMigrationRegisteredForARecordsCurrentVersion_IsAppliedExactlyOnce()
    {
        var persistence = new InMemoryPersistenceStore();
        var id = await SeedAsync(persistence, TestKind, 1);

        var log = new List<string>();
        var registry = new StateMigrationRegistry();
        registry.Register(new LoggingMigration(TestKind, 1, "A", log));

        // CurrentSchemaVersion is a fixed const 1 this release (`ADR-0120`
        // ships the mechanism, not a version bump), so an explicit,
        // higher targetSchemaVersion is what lets this migration — and
        // every other one in this file — actually run at all: the read
        // path's loop stops the moment a record's own SchemaVersion
        // already equals its target.
        var store = new EngineeringObjectStateStore(persistence, registry, null, 2);
        var state = await store.FindAsync(id);

        Assert.NotNull(state);
        Assert.Equal(2, state!.SchemaVersion);
        Assert.Equal(["A"], log);
    }

    [Fact]
    public async Task ASecondMigrationChainedOneVersionLater_RunsAfterTheFirst_NotInsteadOfIt()
    {
        var persistence = new InMemoryPersistenceStore();
        var id = await SeedAsync(persistence, TestKind, 1);

        var log = new List<string>();
        var registry = new StateMigrationRegistry();
        registry.Register(new LoggingMigration(TestKind, 1, "A", log));
        registry.Register(new LoggingMigration(TestKind, 2, "B", log));

        var store = new EngineeringObjectStateStore(persistence, registry, null, 3);
        var state = await store.FindAsync(id);

        Assert.NotNull(state);
        Assert.Equal(3, state!.SchemaVersion); // multi-step: 1 -> 2 -> 3
        Assert.Equal(["A", "B"], log);
    }

    [Fact]
    public async Task ACommonMigration_RunsForEveryKind_NotOnlyTheOneItWasWrittenAgainst()
    {
        var persistence = new InMemoryPersistenceStore();
        var partId = await SeedAsync(persistence, "Part", 1);
        var assemblyId = await SeedAsync(persistence, "Assembly", 1);

        var log = new List<string>();
        var registry = new StateMigrationRegistry();
        registry.Register(new LoggingMigration(null, 1, "Common", log));

        var store = new EngineeringObjectStateStore(persistence, registry, null, 2);

        var part = await store.FindAsync(partId);
        var assembly = await store.FindAsync(assemblyId);

        Assert.Equal(2, part!.SchemaVersion);
        Assert.Equal(2, assembly!.SchemaVersion);
        Assert.Equal(["Common", "Common"], log);
    }

    [Fact]
    public async Task WhenBothACommonAndAKindSpecificMigrationTargetTheSameVersion_TheCommonOneRunsFirst()
    {
        var persistence = new InMemoryPersistenceStore();
        var id = await SeedAsync(persistence, TestKind, 1);

        var log = new List<string>();
        var registry = new StateMigrationRegistry();
        registry.Register(new LoggingMigration(TestKind, 1, "KindSpecific", log));
        registry.Register(new LoggingMigration(null, 1, "Common", log));

        var store = new EngineeringObjectStateStore(persistence, registry, null, 2);
        var state = await store.FindAsync(id);

        Assert.NotNull(state);
        // The common chain wins the FromVersion:1 step (ADR-0120 Decision 2:
        // "common chain first"), so the Kind-specific migration registered
        // for that same version never gets a turn — only one step happens,
        // taking the record from 1 to 2, not 1 to 3.
        Assert.Equal(2, state!.SchemaVersion);
        Assert.Equal(["Common"], log);
    }
}
