using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringData;

public class EngineeringDocumentStoreTests
{
    private static IPrincipal BuildPrincipal(string id) =>
        new PlatformPrincipal(new PlatformIdentity(id, id), []);

    private static EngineeringDocumentStore BuildStore(out CurrentPrincipalAccessor accessor)
    {
        accessor = new CurrentPrincipalAccessor();
        return new EngineeringDocumentStore(new InMemoryPersistenceStore(), accessor);
    }

    // ----------------------------------------------------------------
    // CreateAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ReturnsDocument_WithRevisionNumberOne()
    {
        var store = BuildStore(out _);

        var document = await store.CreateAsync("Requirement", "initial content");

        Assert.Equal("Requirement", document.Kind);
        Assert.Equal(1, document.CurrentRevisionNumber);
        Assert.NotEqual(Guid.Empty, document.Id);
    }

    [Fact]
    public async Task CreateAsync_NullKind_ThrowsArgumentNullException()
    {
        var store = BuildStore(out _);

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync(null!, "content"));
    }

    [Fact]
    public async Task CreateAsync_WhitespaceKind_ThrowsArgumentException()
    {
        var store = BuildStore(out _);

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync("   ", "content"));
    }

    [Fact]
    public async Task CreateAsync_NullContent_ThrowsArgumentNullException()
    {
        var store = BuildStore(out _);

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync("Requirement", null!));
    }

    [Fact]
    public async Task CreateAsync_RecordsInitialRevision_WithCallingPrincipalAsAuthor()
    {
        var store = BuildStore(out var accessor);
        accessor.SetCurrent(BuildPrincipal("author-1"));

        var document = await store.CreateAsync("Requirement", "initial content");
        var history = await store.GetRevisionHistoryAsync(document.Id);

        var revision = Assert.Single(history);
        Assert.Equal("author-1", revision.AuthorPrincipalId);
        Assert.Equal("initial content", revision.Content);
        Assert.Null(revision.ChangeSummary);
    }

    [Fact]
    public async Task CreateAsync_NoPrincipalEstablished_RecordsUnknownAuthor()
    {
        var store = BuildStore(out _);

        var document = await store.CreateAsync("Requirement", "initial content");
        var history = await store.GetRevisionHistoryAsync(document.Id);

        Assert.Equal(EngineeringDocumentStore.UnknownAuthorPrincipalId, Assert.Single(history).AuthorPrincipalId);
    }

    // ----------------------------------------------------------------
    // FindAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task FindAsync_ExistingDocument_ReturnsIt()
    {
        var store = BuildStore(out _);
        var created = await store.CreateAsync("Requirement", "content");

        var found = await store.FindAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal("Requirement", found.Kind);
    }

    [Fact]
    public async Task FindAsync_NonExistentDocument_ReturnsNull()
    {
        var store = BuildStore(out _);

        var found = await store.FindAsync(Guid.NewGuid());

        Assert.Null(found);
    }

    // ----------------------------------------------------------------
    // ReviseAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task ReviseAsync_ExistingDocument_IncrementsCurrentRevisionNumber()
    {
        var store = BuildStore(out _);
        var document = await store.CreateAsync("Requirement", "v1");

        var revision = await store.ReviseAsync(document.Id, "v2", "second draft");

        Assert.Equal(2, revision.RevisionNumber);
        Assert.Equal("v2", revision.Content);
        Assert.Equal("second draft", revision.ChangeSummary);

        var found = await store.FindAsync(document.Id);
        Assert.Equal(2, found!.CurrentRevisionNumber);
    }

    [Fact]
    public async Task ReviseAsync_NonExistentDocument_ThrowsEngineeringDocumentNotFoundException()
    {
        var store = BuildStore(out _);
        var missingId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(
            () => store.ReviseAsync(missingId, "content", null));

        Assert.Equal(missingId, exception.DocumentId);
    }

    [Fact]
    public async Task ReviseAsync_NullContent_ThrowsArgumentNullException()
    {
        var store = BuildStore(out _);
        var document = await store.CreateAsync("Requirement", "v1");

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.ReviseAsync(document.Id, null!, null));
    }

    [Fact]
    public async Task ReviseAsync_CalledConcurrently_NeverProducesTwoRevisionsWithTheSameNumber()
    {
        var store = BuildStore(out _);
        var document = await store.CreateAsync("Requirement", "v1");

        var tasks = Enumerable.Range(0, 20)
            .Select(i => store.ReviseAsync(document.Id, $"v{i + 2}", null))
            .ToArray();
        await Task.WhenAll(tasks);

        var history = await store.GetRevisionHistoryAsync(document.Id);
        var revisionNumbers = history.Select(r => r.RevisionNumber).ToList();

        Assert.Equal(21, history.Count);
        Assert.Equal(revisionNumbers.Distinct().Count(), revisionNumbers.Count);
        Assert.Equal(Enumerable.Range(1, 21), revisionNumbers.OrderBy(n => n));
    }

    // ----------------------------------------------------------------
    // GetRevisionHistoryAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetRevisionHistoryAsync_ReturnsEveryRevision_OldestFirst()
    {
        var store = BuildStore(out _);
        var document = await store.CreateAsync("Requirement", "v1");
        await store.ReviseAsync(document.Id, "v2", null);
        await store.ReviseAsync(document.Id, "v3", null);

        var history = await store.GetRevisionHistoryAsync(document.Id);

        Assert.Equal(3, history.Count);
        Assert.Equal(["v1", "v2", "v3"], history.Select(r => r.Content));
        Assert.Equal([1, 2, 3], history.Select(r => r.RevisionNumber));
    }

    [Fact]
    public async Task GetRevisionHistoryAsync_NonExistentDocument_ThrowsEngineeringDocumentNotFoundException()
    {
        var store = BuildStore(out _);

        await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(
            () => store.GetRevisionHistoryAsync(Guid.NewGuid()));
    }

    // ----------------------------------------------------------------
    // LinkAsync / GetReferencesAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task LinkAsync_ThenGetReferencesAsync_RoundTripsTheReference()
    {
        var store = BuildStore(out _);
        var source = await store.CreateAsync("Requirement", "source");
        var target = await store.CreateAsync("Requirement", "target");

        await store.LinkAsync(source.Id, target.Id, "verifies");
        var references = await store.GetReferencesAsync(source.Id);

        var reference = Assert.Single(references);
        Assert.Equal(source.Id, reference.SourceDocumentId);
        Assert.Equal(target.Id, reference.TargetDocumentId);
        Assert.Equal("verifies", reference.RelationshipKind);
    }

    [Fact]
    public async Task GetReferencesAsync_NoReferencesRecorded_ReturnsEmpty()
    {
        var store = BuildStore(out _);
        var document = await store.CreateAsync("Requirement", "content");

        var references = await store.GetReferencesAsync(document.Id);

        Assert.Empty(references);
    }

    [Fact]
    public async Task LinkAsync_NonExistentSource_ThrowsEngineeringDocumentNotFoundException()
    {
        var store = BuildStore(out _);
        var target = await store.CreateAsync("Requirement", "target");
        var missingSourceId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(
            () => store.LinkAsync(missingSourceId, target.Id, "verifies"));

        Assert.Equal(missingSourceId, exception.DocumentId);
    }

    [Fact]
    public async Task LinkAsync_NonExistentTarget_ThrowsEngineeringDocumentNotFoundException()
    {
        var store = BuildStore(out _);
        var source = await store.CreateAsync("Requirement", "source");
        var missingTargetId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<EngineeringDocumentNotFoundException>(
            () => store.LinkAsync(source.Id, missingTargetId, "verifies"));

        Assert.Equal(missingTargetId, exception.DocumentId);
    }

    [Fact]
    public async Task LinkAsync_NullRelationshipKind_ThrowsArgumentNullException()
    {
        var store = BuildStore(out _);
        var source = await store.CreateAsync("Requirement", "source");
        var target = await store.CreateAsync("Requirement", "target");

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.LinkAsync(source.Id, target.Id, null!));
    }

    [Fact]
    public async Task GetReferencesAsync_OnlyReturnsReferencesWhereDocumentIsTheSource()
    {
        var store = BuildStore(out _);
        var a = await store.CreateAsync("Requirement", "a");
        var b = await store.CreateAsync("Requirement", "b");
        await store.LinkAsync(a.Id, b.Id, "verifies");

        var referencesFromB = await store.GetReferencesAsync(b.Id);

        Assert.Empty(referencesFromB);
    }

    // ----------------------------------------------------------------
    // Constructor validation
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_NullPersistenceStore_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineeringDocumentStore(null!, new CurrentPrincipalAccessor()));
    }

    [Fact]
    public void Constructor_NullCurrentPrincipalAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineeringDocumentStore(new InMemoryPersistenceStore(), null!));
    }

    // ----------------------------------------------------------------
    // Failure injection
    // ----------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistenceUnavailable_PropagatesExceptionUnmodified()
    {
        var store = new EngineeringDocumentStore(new FailingPersistenceStore(), new CurrentPrincipalAccessor());

        await Assert.ThrowsAsync<Tempest.Core.Persistence.PersistenceStoreUnavailableException>(
            () => store.CreateAsync("Requirement", "content"));
    }
}
