using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// A hand-written, in-memory <see cref="IPersistenceStore"/> test double —
/// mirrors the convention every other test area in this suite follows,
/// duplicated here rather than shared, per this codebase's own established
/// precedent of small, test-local fakes.
/// </summary>
/// <remarks>
/// `TD-158`: <see cref="ReferenceData.ReferenceDataCatalog{TDefinition}"/>
/// now refuses a store that is not also an <see cref="IQueryablePersistenceStore"/>
/// (a clear refusal, never a silent non-transactional fallback), so this
/// double must be one too. Rather than reimplementing real transaction
/// semantics a third time, it composes the suite's own hardened,
/// already-shared double (<see cref="InMemoryQueryablePersistenceStore"/>)
/// for storage and <see cref="CommitFailingPersistenceStore"/> for fault
/// injection, and exposes <see cref="FailNextCommit"/> straight through to
/// it — the same mechanism `TransactionalWriteFaultInjectionTests` already
/// uses for the engineering-object write path (`WP 17.1B`).
/// </remarks>
internal sealed class InMemoryPersistenceStore : IPersistenceStore, IQueryablePersistenceStore
{
    private readonly InMemoryQueryablePersistenceStore _inner = new();
    private readonly CommitFailingPersistenceStore _transactional;

    public InMemoryPersistenceStore()
    {
        _transactional = new CommitFailingPersistenceStore(_inner);
    }

    /// <summary>When set, the next transaction's commit fails after its body completes — `TD-158` fault injection.</summary>
    public bool FailNextCommit
    {
        get => _transactional.FailNextCommit;
        set => _transactional.FailNextCommit = value;
    }

    // ----------------------------------------------------------------
    // IPersistenceStore — straight through to the shared backing store.
    // ----------------------------------------------------------------

    public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(collection, key, cancellationToken);

    public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(collection, key, value, cancellationToken);

    public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(collection, key, cancellationToken);

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default) =>
        _inner.ListKeysAsync(collection, cancellationToken);

    // ----------------------------------------------------------------
    // IQueryablePersistenceStore — through the fault-injecting wrapper,
    // so a test can arm FailNextCommit and see the whole transaction body
    // (document write, index write, secondary index write) fail to land.
    // ----------------------------------------------------------------

    public long CurrentSequence => _inner.CurrentSequence;

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) =>
        _transactional.ListKeysAsync(collection, keyPrefix, cancellationToken);

    public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) =>
        _transactional.ReadAllAsync(collection, cancellationToken);

    public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(
        string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        _transactional.ReadManyAsync(collection, keys, cancellationToken);

    public Task ExecuteInTransactionAsync(
        Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        _transactional.ExecuteInTransactionAsync(work, cancellationToken);

    public Task<T> ExecuteInReadTransactionAsync<T>(
        Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default) =>
        _transactional.ExecuteInReadTransactionAsync(read, cancellationToken);

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
        _transactional.SearchAsync(query, limit, cancellationToken);

    public Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default) =>
        _transactional.IsSearchIndexEmptyAsync(cancellationToken);
}

/// <summary>
/// A counting <see cref="IEngineeringDocumentStore"/> decorator — every
/// other member forwards straight through to <paramref name="inner"/>
/// unchanged; only <see cref="GetRevisionHistoryAsync"/> and
/// <see cref="GetLatestRevisionAsync"/> are counted. Proves `TD-20`: a
/// latest-only catalogue lookup must call the single-revision fetch, never
/// the whole-history one.
/// </summary>
internal sealed class CountingDocumentStore(IEngineeringDocumentStore inner)
    : IEngineeringDocumentStore, Tempest.Core.EngineeringDomain.ITransactionalDocumentWriter
{
    public int GetRevisionHistoryAsyncCallCount { get; private set; }

    public int GetLatestRevisionAsyncCallCount { get; private set; }

    public Task<IEngineeringDocument> CreateAsync(string kind, string initialContent, CancellationToken cancellationToken = default) =>
        inner.CreateAsync(kind, initialContent, cancellationToken);

    public Task<IEngineeringDocument?> FindAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        inner.FindAsync(documentId, cancellationToken);

    public Task<IDocumentRevision> ReviseAsync(Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken = default) =>
        inner.ReviseAsync(documentId, newContent, changeSummary, cancellationToken);

    public Task<IReadOnlyList<IDocumentRevision>> GetRevisionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        GetRevisionHistoryAsyncCallCount++;
        return inner.GetRevisionHistoryAsync(documentId, cancellationToken);
    }

    public Task<IDocumentRevision> GetLatestRevisionAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        GetLatestRevisionAsyncCallCount++;
        return inner.GetLatestRevisionAsync(documentId, cancellationToken);
    }

    public Task LinkAsync(Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default) =>
        inner.LinkAsync(sourceDocumentId, targetDocumentId, relationshipKind, cancellationToken);

    public Task<IReadOnlyList<DocumentReference>> GetReferencesAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        inner.GetReferencesAsync(documentId, cancellationToken);

    // ----------------------------------------------------------------
    // ITransactionalDocumentWriter — ReferenceDataCatalog's constructor
    // requires this capability on whatever documentStore it is given
    // (`TD-158`); forwarded straight through to the real store `inner`
    // always is in this fixture.
    // ----------------------------------------------------------------

    private Tempest.Core.EngineeringDomain.ITransactionalDocumentWriter InnerWriter =>
        (Tempest.Core.EngineeringDomain.ITransactionalDocumentWriter)inner;

    Task<Tempest.Core.EngineeringDomain.DocumentCreation> Tempest.Core.EngineeringDomain.ITransactionalDocumentWriter.CreateAsync(
        IPersistenceTransaction transaction, Guid documentId, string kind, string initialContent, CancellationToken cancellationToken) =>
        InnerWriter.CreateAsync(transaction, documentId, kind, initialContent, cancellationToken);

    Task<IDocumentRevision> Tempest.Core.EngineeringDomain.ITransactionalDocumentWriter.ReviseAsync(
        IPersistenceTransaction transaction, Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken) =>
        InnerWriter.ReviseAsync(transaction, documentId, newContent, changeSummary, cancellationToken);

    Task Tempest.Core.EngineeringDomain.ITransactionalDocumentWriter.LinkAsync(
        IPersistenceTransaction transaction, Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken) =>
        InnerWriter.LinkAsync(transaction, sourceDocumentId, targetDocumentId, relationshipKind, cancellationToken);

    Task<bool> Tempest.Core.EngineeringDomain.ITransactionalDocumentWriter.ExistsAsync(
        IPersistenceTransaction transaction, Guid documentId, CancellationToken cancellationToken) =>
        InnerWriter.ExistsAsync(transaction, documentId, cancellationToken);
}

/// <summary>
/// A deliberately trivial domain, used to test the shared reference-data
/// machinery without dragging any real library's own engineering semantics
/// into the test.
/// </summary>
/// <remarks>
/// The point of a fake domain here is that a failure means the shared layer
/// is wrong, not that Bearings or Fasteners is. Every real library's own
/// tests then cover only what is genuinely theirs.
/// </remarks>
internal sealed record WidgetDefinition
{
    public required string Designation { get; init; }

    public string? Colour { get; init; }
}

internal sealed class WidgetCatalog : ReferenceDataCatalog<WidgetDefinition>
{
    public WidgetCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    public override string LibraryName => "Widgets";

    public override string DocumentKind => "WidgetReference";

    public override string IndexCollectionName => "Widgets.Index";

    protected override string? GetSecondaryKey(WidgetDefinition definition) => definition.Designation.Trim().ToUpperInvariant();

    protected override string DescribeSecondaryKey(WidgetDefinition definition) => $"Designation '{definition.Designation}'";

    public Task<IReferenceRecord<WidgetDefinition>?> FindByDesignationAsync(string designation, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(designation.Trim().ToUpperInvariant(), cancellationToken);

    public Task<IReadOnlyList<IReferenceRecord<WidgetDefinition>>> WithColourAsync(string colour, CancellationToken cancellationToken = default) =>
        FilterAsync(record => string.Equals(record.Definition.Colour, colour, StringComparison.OrdinalIgnoreCase), cancellationToken);
}

/// <summary>A catalogue with no secondary key at all, so the shared layer's own "no secondary key" path is exercised.</summary>
internal sealed class KeylessWidgetCatalog : ReferenceDataCatalog<WidgetDefinition>
{
    public KeylessWidgetCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore)
        : base(documentStore, persistenceStore)
    {
    }

    public override string LibraryName => "KeylessWidgets";

    public override string DocumentKind => "KeylessWidgetReference";

    public override string IndexCollectionName => "KeylessWidgets.Index";
}

internal static class ReferenceDataFixtures
{
    public static WidgetCatalog BuildCatalog() => BuildCatalog(out _, out _);

    public static WidgetCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new WidgetCatalog(documentStore, persistenceStore);
    }

    /// <summary>Builds a catalogue backed by <see cref="CountingDocumentStore"/>, so a test can assert how many times each read shape was called (`TD-20`).</summary>
    public static WidgetCatalog BuildCatalog(out CountingDocumentStore documentStore)
    {
        var persistenceStore = new InMemoryPersistenceStore();
        documentStore = new CountingDocumentStore(new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor()));
        return new WidgetCatalog(documentStore, persistenceStore);
    }

    public static WidgetDefinition Widget(string designation = "W-1", string? colour = null) =>
        new() { Designation = designation, Colour = colour };

    /// <summary>Provenance that identifies a source but has not been verified — what an honest import leaves behind.</summary>
    public static ReferenceProvenance Sourced() => new(
        SourceOrganisation: "TestFixture Publications",
        SourceDocument: "Fixture handbook (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Table 1",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.");

    /// <summary>Provenance a named reviewer has verified — the only kind that can reach Released.</summary>
    public static ReferenceProvenance Verified() => Sourced() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    public static async Task<IReferenceRecord<WidgetDefinition>> ReleaseAsync(WidgetCatalog catalog, string recordId)
    {
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Released, "Released.");
    }
}
