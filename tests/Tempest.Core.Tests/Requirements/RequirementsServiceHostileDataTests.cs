using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;
using Tempest.Core.Configuration;

namespace Tempest.Core.Tests.Requirements;

/// <summary>
/// `TD-59`/`TD-60` closure tests for the Requirements identifier-index
/// paths, over the real durable store — the same defect family
/// <see cref="MaterialCatalog"/> carries, in the sibling that copied its
/// index pattern.
/// </summary>
/// <remarks>
/// Re-pointed from the deleted file-per-key store to
/// <see cref="SqlitePersistenceStore"/> (`WP 18.1A`, `ADR-0144`): every
/// case here writes a hostile-looking <em>key</em> (a reserved Win32
/// device stem as an identifier, a foreign-looking index key such as
/// <c>.DS_Store</c>) through the ordinary <see cref="IPersistenceStore.WriteAsync"/>
/// surface, never by touching a file directly, so the claim — that
/// <see cref="RequirementsService"/> itself tolerates an index entry it
/// did not expect — is exactly as meaningful against a database row as it
/// was against a directory entry.
/// </remarks>
public class RequirementsServiceHostileDataTests
{
    private static IConfigurationProvider BuildConfiguration(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    private static (RequirementsService Requirements, SqlitePersistenceStore Store) BuildRealStack(string rootPath)
    {
        var store = new SqlitePersistenceStore(BuildConfiguration(rootPath));
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var verificationService = new VerificationService(
            documentStore, principalAccessor, new PermissionEvaluator(), store, new InMemoryEngineeringRelationshipRepository());
        return (new RequirementsService(documentStore, store, principalAccessor, verificationService), store);
    }

    [Theory]
    [InlineData("NUL")]
    [InlineData("CON")]
    [InlineData("COM1")]
    [InlineData("con.json")]
    public async Task CreateAsync_ReservedDeviceNameIdentifier_IsFindableAndListed(string identifier)
    {
        using var temp = new TempDirectory();
        var (requirements, _) = BuildRealStack(temp.Path);

        await requirements.CreateAsync(identifier, "The system shall survive hostile identifiers.");

        var found = await requirements.FindByIdentifierAsync(identifier);
        Assert.NotNull(found);
        Assert.Equal(identifier, found!.Identifier);

        Assert.Contains(await requirements.ListAsync(), r => r.Identifier == identifier);
    }

    [Fact]
    public async Task FindByIdentifierAsync_CorruptedIndexValue_ThrowsControlledEngineeringDataException()
    {
        using var temp = new TempDirectory();
        var (requirements, store) = BuildRealStack(temp.Path);
        await store.WriteAsync(RequirementsService.IdentifierIndexCollectionName, "REQ-001", "garbage");

        var exception = await Assert.ThrowsAsync<EngineeringDataException>(
            () => requirements.FindByIdentifierAsync("REQ-001"));
        Assert.Contains("REQ-001", exception.Message);
    }

    [Fact]
    public async Task ListCollectionsAsync_ForeignFileInRegistryDirectory_IsIgnored()
    {
        using var temp = new TempDirectory();
        var (requirements, store) = BuildRealStack(temp.Path);
        await requirements.CreateCollectionAsync("Real Collection");

        // A foreign file dropped beside the store's own (a `.DS_Store`,
        // an editor dropping) previously threw FormatException from
        // Guid.ParseExact on the KEY itself, aborting the listing.
        await store.WriteAsync(RequirementsService.CollectionRegistryCollectionName, ".DS_Store", "junk");

        var collections = await requirements.ListCollectionsAsync();

        Assert.Single(collections);
        Assert.Equal("Real Collection", collections[0].Name);
    }

    [Fact]
    public async Task ListGroupsAsync_ForeignFileInRegistryDirectory_IsIgnored()
    {
        using var temp = new TempDirectory();
        var (requirements, store) = BuildRealStack(temp.Path);
        await requirements.CreateGroupAsync("Real Group");
        await store.WriteAsync(RequirementsService.GroupRegistryCollectionName, "Thumbs.db", "junk");

        var groups = await requirements.ListGroupsAsync();

        Assert.Single(groups);
        Assert.Equal("Real Group", groups[0].Name);
    }

    /// <summary>
    /// `WP 21.5F` Offensive Security Audit, item 8: <see cref="SqlitePersistenceStore"/>
    /// itself is already proven immune at the generic key/value layer
    /// (<c>PersistenceStoreHostileNameTests.AnArbitraryUnicodeOrPunctuatedKey_RoundTrips</c>)
    /// — every one of its statements binds <c>collection</c>/<c>key</c> as
    /// parameters, never string concatenation. This is the same claim one
    /// layer up, at the point a real business identifier (a drawing
    /// number, here a Requirement's own identifier) — not a raw store key —
    /// is what a user actually types, proving the SQL-injection-shaped
    /// value survives the whole real path: create, find by identifier,
    /// list, and (the part a generic KV-layer test cannot exercise) the
    /// FTS5 search index this domain service also writes through.
    /// </summary>
    [Theory]
    [InlineData("REQ-1'); DROP TABLE records; --")]
    [InlineData("REQ-1' OR '1'='1")]
    [InlineData("REQ-1\"; DELETE FROM records WHERE 1=1; --")]
    public async Task CreateAsync_SqlInjectionShapedIdentifier_RoundTripsSafely_AndNeverDamagesTheStore(string identifier)
    {
        using var temp = new TempDirectory();
        var (requirements, _) = BuildRealStack(temp.Path);

        await requirements.CreateAsync(identifier, "The system shall survive an identifier a naive query would treat as SQL.");

        var found = await requirements.FindByIdentifierAsync(identifier);
        Assert.NotNull(found);
        Assert.Equal(identifier, found!.Identifier);
        Assert.Contains(await requirements.ListAsync(), r => r.Identifier == identifier);

        // A second, ordinary requirement created afterwards proves the
        // store itself (specifically its "records" table) was never
        // damaged by the first identifier - the exact thing a real
        // injection would have broken.
        await requirements.CreateAsync("REQ-CANARY", "A second, ordinary requirement created after the hostile one.");
        Assert.NotNull(await requirements.FindByIdentifierAsync("REQ-CANARY"));
    }
}
