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
    public void RegisteringTwoMigrationsForTheIdenticalChainAndFromVersion_Throws()
    {
        var registry = new StateMigrationRegistry();
        registry.Register(new LoggingMigration(TestKind, 1, "First", []));

        var ex = Assert.Throws<DuplicateStateMigrationException>(
            () => registry.Register(new LoggingMigration(TestKind, 1, "Second", [])));

        Assert.Equal(TestKind, ex.Kind);
        Assert.Equal(1, ex.FromVersion);
    }

    [Fact]
    public void RegisteringTwoCommonMigrationsForTheIdenticalFromVersion_Throws()
    {
        var registry = new StateMigrationRegistry();
        registry.Register(new LoggingMigration(null, 1, "First", []));

        var ex = Assert.Throws<DuplicateStateMigrationException>(
            () => registry.Register(new LoggingMigration(null, 1, "Second", [])));

        Assert.Null(ex.Kind);
        Assert.Equal(1, ex.FromVersion);
    }

    [Fact]
    public void RegisteringACommonMigration_AfterAKindSpecificOneAtTheSameFromVersion_Throws()
    {
        // Registration order: Kind-specific first, common second — the
        // common migration would silently start winning that FromVersion
        // step for every Kind, including this one, from this point on.
        var registry = new StateMigrationRegistry();
        registry.Register(new LoggingMigration(TestKind, 1, "KindSpecific", []));

        var ex = Assert.Throws<ConflictingStateMigrationException>(
            () => registry.Register(new LoggingMigration(null, 1, "Common", [])));

        Assert.Equal(TestKind, ex.Kind);
        Assert.Equal(1, ex.FromVersion);
    }

    [Fact]
    public void RegisteringAKindSpecificMigration_AfterACommonOneAtTheSameFromVersion_Throws()
    {
        // The reverse registration order from the test above: common
        // first, Kind-specific second — a guard that only catches one of
        // these two orders would still let the other one through, and
        // would read as complete while doing so.
        var registry = new StateMigrationRegistry();
        registry.Register(new LoggingMigration(null, 1, "Common", []));

        var ex = Assert.Throws<ConflictingStateMigrationException>(
            () => registry.Register(new LoggingMigration(TestKind, 1, "KindSpecific", [])));

        Assert.Equal(TestKind, ex.Kind);
        Assert.Equal(1, ex.FromVersion);
    }

    [Fact]
    public async Task ANonCollidingCommonMigration_FollowedByThatKindsOwnLaterMigration_MigratesEndToEndInOrder()
    {
        // Common at FromVersion 1 (1 -> 2), that Kind's own migration at
        // FromVersion 2 (2 -> 3) — different versions, so no collision;
        // this is ADR-0120 Decision 2's real, intended ordering (common
        // chain first, then that Kind's own), still working correctly
        // after the collision guard above was added.
        var persistence = new InMemoryPersistenceStore();
        var id = await SeedAsync(persistence, TestKind, 1);

        var log = new List<string>();
        var registry = new StateMigrationRegistry();
        registry.Register(new LoggingMigration(null, 1, "Common", log));
        registry.Register(new LoggingMigration(TestKind, 2, "KindSpecific", log));

        var store = new EngineeringObjectStateStore(persistence, registry, null, 3);
        var state = await store.FindAsync(id);

        Assert.NotNull(state);
        Assert.Equal(3, state!.SchemaVersion);
        Assert.Equal(["Common", "KindSpecific"], log);
    }
}
