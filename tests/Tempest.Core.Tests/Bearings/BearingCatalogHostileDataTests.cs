using Tempest.Core.Bearings;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Bearings;

// Hostile-data tests: corrupted indexes, unreadable content and stale
// entries must surface as controlled BearingsExceptions naming what is
// wrong, never as raw FormatException/JsonException/NullReferenceException,
// and never as a silent "no such bearing".
public class BearingCatalogHostileDataTests
{
    private static BearingCatalog Build(out InMemoryPersistenceStore persistenceStore, out EngineeringDocumentStore documentStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new BearingCatalog(documentStore, persistenceStore);
    }

    [Fact]
    public async Task FindAsync_CorruptedIndexEntry_ThrowsAControlledExceptionNamingTheEntry()
    {
        var catalog = Build(out var persistenceStore, out _);
        await persistenceStore.WriteAsync(BearingCatalog.IndexCollectionName, "brg-0001", "not-a-guid");

        var exception = await Assert.ThrowsAsync<BearingsException>(() => catalog.FindAsync("brg-0001"));

        Assert.Contains("brg-0001", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not-a-guid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindAsync_IndexPointingAtAMissingDocument_ReadsAsNoSuchBearing()
    {
        var catalog = Build(out var persistenceStore, out _);
        await persistenceStore.WriteAsync(BearingCatalog.IndexCollectionName, "brg-0001", Guid.NewGuid().ToString("N"));

        Assert.Null(await catalog.FindAsync("brg-0001"));
    }

    [Fact]
    public async Task FindAsync_IndexPointingAtADocumentOfAnotherKind_ReadsAsNoSuchBearing()
    {
        var catalog = Build(out var persistenceStore, out var documentStore);
        var foreign = await documentStore.CreateAsync("MaterialSpecification", "{}");
        await persistenceStore.WriteAsync(BearingCatalog.IndexCollectionName, "brg-0001", foreign.Id.ToString("N"));

        Assert.Null(await catalog.FindAsync("brg-0001"));
    }

    [Fact]
    public async Task FindAsync_UnreadableContent_ThrowsAControlledException()
    {
        var catalog = Build(out _, out var documentStore);
        var bearing = await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        await documentStore.ReviseAsync(bearing.UnderlyingDocumentId, "{ not json", "Corrupted.");

        var exception = await Assert.ThrowsAsync<BearingsException>(() => catalog.FindAsync("brg-0001"));

        Assert.Contains("brg-0001", exception.Message, StringComparison.Ordinal);
        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task FindAsync_ContentMissingItsDefinition_ThrowsAControlledException()
    {
        var catalog = Build(out _, out var documentStore);
        var bearing = await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        await documentStore.ReviseAsync(
            bearing.UnderlyingDocumentId,
            "{\"BearingId\":\"brg-0001\",\"ValidationState\":\"Draft\"}",
            "Definition removed.");

        var exception = await Assert.ThrowsAsync<BearingsException>(() => catalog.FindAsync("brg-0001"));

        Assert.Contains("brg-0001", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindAsync_ContentNamingAnUnknownFamily_ThrowsAControlledException()
    {
        // Enums are stored as strings so a member added later cannot
        // silently reinterpret an existing record; a value this build does
        // not know is reported, never guessed at.
        var catalog = Build(out _, out var documentStore);
        var bearing = await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        var content = (await documentStore.GetRevisionHistoryAsync(bearing.UnderlyingDocumentId))[^1].Content
            .Replace("\"DeepGrooveBall\"", "\"MagneticLevitation\"", StringComparison.Ordinal);
        await documentStore.ReviseAsync(bearing.UnderlyingDocumentId, content, "Unknown family.");

        await Assert.ThrowsAsync<BearingsException>(() => catalog.FindAsync("brg-0001"));
    }

    [Fact]
    public async Task ListAsync_SkipsAStaleIndexEntryRatherThanAbortingTheWholeListing()
    {
        var catalog = Build(out var persistenceStore, out _);
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));
        await persistenceStore.WriteAsync(BearingCatalog.IndexCollectionName, "brg-stale", Guid.NewGuid().ToString("N"));

        var listed = await catalog.ListAsync();

        Assert.Equal(["brg-0001"], listed.Select(b => b.BearingId));
    }

    [Fact]
    public async Task FindByPartNumberAsync_PartNumberIndexPointingAtAMissingBearing_ReturnsNull()
    {
        var catalog = Build(out var persistenceStore, out _);
        await persistenceStore.WriteAsync(BearingCatalog.PartNumberIndexCollectionName, "TESTFIXTURE BEARINGS FX-6000", "brg-gone");

        Assert.Null(await catalog.FindByPartNumberAsync("TestFixture Bearings", "FX-6000"));
    }

    [Fact]
    public async Task Constructor_NullDocumentStore_Throws()
    {
        var persistenceStore = new InMemoryPersistenceStore();

        Assert.Throws<ArgumentNullException>(() => new BearingCatalog(null!, persistenceStore));
        await Task.CompletedTask;
    }

    [Fact]
    public void Constructor_NullPersistenceStore_Throws()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());

        Assert.Throws<ArgumentNullException>(() => new BearingCatalog(documentStore, null!));
    }
}
