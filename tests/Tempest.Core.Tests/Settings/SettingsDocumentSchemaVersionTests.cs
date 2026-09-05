using Tempest.Core.Events;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Settings;

/// <summary>
/// `TD-87`/`ADR-0120` Decision 6 — the identical defaulting and
/// migration-chain behaviour <c>EngineeringObjectStateStore</c> gives
/// <c>EngineeringObjectState</c>, proven once here against a test-only DTO
/// rather than against all six real object-shaped
/// <see cref="SettingsDocument{TDocument}"/> consumers individually — the
/// six are proven by construction (they share this one generic type), not
/// by six near-duplicate test classes.
/// </summary>
public class SettingsDocumentSchemaVersionTests
{
    private const string Key = "test.versioned";

    private sealed record TestDto(int SchemaVersion, string Name);

    private sealed class RenameMigration : ISettingsMigration<TestDto>
    {
        public int FromVersion => 1;
        public TestDto Migrate(TestDto document) => document with { Name = document.Name + "-migrated" };
    }

    private sealed class AppendMigration(int fromVersion, string suffix) : ISettingsMigration<TestDto>
    {
        public int FromVersion { get; } = fromVersion;
        public TestDto Migrate(TestDto document) => document with { Name = document.Name + suffix };
    }

    private static SettingsProvider Provider() => new(new InMemoryPersistenceStore(), new EventBus());

    // ==================================================================
    // No migrations supplied — the zero-cost default (every real caller,
    // today) — behaves exactly as SettingsDocument<T> did before this WP.
    // ==================================================================

    [Fact]
    public async Task WithNoMigrationsSupplied_AnAbsentSchemaVersion_IsNeverNormalised()
    {
        var provider = Provider();
        var document = new SettingsDocument<TestDto>(provider, Key, "Test");
        await provider.SetValueAsync(Key, "{\"Name\":\"bracket\"}");

        var loaded = await document.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(0, loaded!.SchemaVersion); // untouched — the CLR default, not normalised, because no chain was supplied at all
        Assert.Equal("bracket", loaded.Name);
    }

    [Fact]
    public async Task ACorruptStoredValue_StillLoadsAsNull_EvenWithMigrationsSupplied()
    {
        var provider = Provider();
        var document = new SettingsDocument<TestDto>(provider, Key, "Test", migrations: [new RenameMigration()]);
        await provider.SetValueAsync(Key, "{ not json");

        // TD-60's recovery contract is unaffected by this WP.
        Assert.Null(await document.LoadAsync());
    }

    // ==================================================================
    // A chain is supplied — normalisation and migration both engage.
    // ==================================================================

    [Fact]
    public async Task WithAMigrationChainSupplied_AnAbsentSchemaVersion_NormalisesToOne()
    {
        var provider = Provider();
        var document = new SettingsDocument<TestDto>(provider, Key, "Test", migrations: []);
        await provider.SetValueAsync(Key, "{\"Name\":\"bracket\"}");

        var loaded = await document.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.SchemaVersion);
        Assert.Equal("bracket", loaded.Name);
    }

    [Fact]
    public async Task AMigrationRegisteredForVersion1_IsAppliedOnLoad_AndBumpsTheVersion()
    {
        var provider = Provider();
        var document = new SettingsDocument<TestDto>(provider, Key, "Test", migrations: [new RenameMigration()]);
        await provider.SetValueAsync(Key, "{\"Name\":\"bracket\"}"); // absent -> normalises to 1 first

        var loaded = await document.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.SchemaVersion);
        Assert.Equal("bracket-migrated", loaded.Name);
    }

    [Fact]
    public async Task AChainOfMigrations_AppliesInOrder_MultiStep()
    {
        var provider = Provider();
        var document = new SettingsDocument<TestDto>(
            provider, Key, "Test",
            migrations: [new AppendMigration(1, "-A"), new AppendMigration(2, "-B")]);
        await provider.SetValueAsync(Key, "{\"SchemaVersion\":1,\"Name\":\"bracket\"}");

        var loaded = await document.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.SchemaVersion); // multi-step: 1 -> 2 -> 3
        Assert.Equal("bracket-A-B", loaded.Name);
    }

    [Fact]
    public async Task ADocumentAlreadyPastEveryRegisteredMigration_IsLeftAlone()
    {
        var provider = Provider();
        var document = new SettingsDocument<TestDto>(provider, Key, "Test", migrations: [new RenameMigration()]); // FromVersion: 1, does not match 5
        await provider.SetValueAsync(Key, "{\"SchemaVersion\":5,\"Name\":\"bracket\"}");

        var loaded = await document.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(5, loaded!.SchemaVersion);
        Assert.Equal("bracket", loaded.Name);
    }

    [Fact]
    public async Task ASchemaVersionWrittenAsZero_NormalisesToOne_IdenticallyToAbsent()
    {
        var provider = Provider();
        var document = new SettingsDocument<TestDto>(provider, Key, "Test", migrations: []);
        await provider.SetValueAsync(Key, "{\"SchemaVersion\":0,\"Name\":\"bracket\"}");

        var loaded = await document.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.SchemaVersion);
    }

    // ==================================================================
    // A document the supplied chain cannot carry to its own highest
    // reachable version is discarded and logged, never handed back at
    // whatever version the walk stopped at (`v0.16.0` review board —
    // this seam lacked the check `EngineeringObjectStateStore` has had
    // since `WP 16.3B`, where Technical Review rejected an
    // implementation once for omitting exactly it).
    // ==================================================================

    [Fact]
    public async Task WhenTheChainHasAHole_TheDocumentIsDiscardedRatherThanReturnedAtTheWrongVersion()
    {
        var provider = Provider();

        // Migrations exist at 1 and at 3, but nothing bridges 2 -> 3, so a
        // v1 document walks to 2 and stops two versions short of the 4 this
        // chain can otherwise reach.
        var document = new SettingsDocument<TestDto>(
            provider,
            Key,
            "Test",
            migrations: [new AppendMigration(1, "-a"), new AppendMigration(3, "-c")]);

        await provider.SetValueAsync(Key, "{\"SchemaVersion\":1,\"Name\":\"stuck\"}");

        var loaded = await document.LoadAsync();

        Assert.Null(loaded);
    }

    [Fact]
    public async Task WhenTheChainCarriesTheDocumentAllTheWay_ItIsReturned()
    {
        var provider = Provider();

        var document = new SettingsDocument<TestDto>(
            provider,
            Key,
            "Test",
            migrations: [new AppendMigration(1, "-a"), new AppendMigration(2, "-b")]);

        await provider.SetValueAsync(Key, "{\"SchemaVersion\":1,\"Name\":\"ok\"}");

        var loaded = await document.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.SchemaVersion);
        Assert.Equal("ok-a-b", loaded.Name);
    }

    [Fact]
    public async Task ADocumentAlreadyAtTheHighestReachableVersion_IsNotTreatedAsStuck()
    {
        var provider = Provider();

        var document = new SettingsDocument<TestDto>(
            provider,
            Key,
            "Test",
            migrations: [new AppendMigration(1, "-a")]);

        await provider.SetValueAsync(Key, "{\"SchemaVersion\":2,\"Name\":\"current\"}");

        var loaded = await document.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.SchemaVersion);
        Assert.Equal("current", loaded.Name);
    }
}
