using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringData;

/// <summary>
/// `TD-60` closure tests — corrupted stored content on the document
/// store's passive read paths must surface as controlled
/// <see cref="EngineeringDataException"/>s, never raw
/// <see cref="System.Text.Json.JsonException"/>s, and corruption must
/// never be misreported as absence.
/// </summary>
public class EngineeringDocumentStoreCorruptionTests
{
    private static (EngineeringDocumentStore Store, InMemoryPersistenceStore Persistence) Build()
    {
        var persistence = new InMemoryPersistenceStore();
        return (new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }

    [Theory]
    [InlineData("{{{not json")]
    [InlineData("null")]
    [InlineData("")]
    public async Task FindAsync_CorruptedDocumentRecord_ThrowsControlledEngineeringDataException(string corruptJson)
    {
        var (store, persistence) = Build();
        var document = await store.CreateAsync("Kind", "content");
        await persistence.WriteAsync(EngineeringDocumentStore.DocumentsCollectionName, document.Id.ToString("N"), corruptJson);

        await Assert.ThrowsAsync<EngineeringDataException>(() => store.FindAsync(document.Id));
    }

    [Fact]
    public async Task GetRevisionHistoryAsync_CorruptedRevisionRecord_ThrowsControlledEngineeringDataException()
    {
        var (store, persistence) = Build();
        var document = await store.CreateAsync("Kind", "content");
        var revisionKey = $"{document.Id:N}_{1:D10}";
        await persistence.WriteAsync(EngineeringDocumentStore.RevisionsCollectionName, revisionKey, "{{{not json");

        await Assert.ThrowsAsync<EngineeringDataException>(() => store.GetRevisionHistoryAsync(document.Id));
    }

    [Fact]
    public async Task GetReferencesAsync_CorruptedReferenceRecord_ThrowsControlledEngineeringDataException()
    {
        var (store, persistence) = Build();
        var source = await store.CreateAsync("Kind", "content");
        var target = await store.CreateAsync("Kind", "content");
        await store.LinkAsync(source.Id, target.Id, "relatesTo");

        var referencesCollection = EngineeringDocumentStore.GetReferencesCollectionName(source.Id);
        var keys = await persistence.ListKeysAsync(referencesCollection);
        await persistence.WriteAsync(referencesCollection, keys[0], "{{{not json");

        await Assert.ThrowsAsync<EngineeringDataException>(() => store.GetReferencesAsync(source.Id));
    }
}
