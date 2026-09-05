using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Requirements;

/// <summary>
/// `TD-67` closure tests for the reconcile/repair path the register's own
/// entry names as missing: a Requirement/Requirement Collection/
/// Requirement Group document created without its own index/registry
/// entry (a crash or write failure between the two, exactly the window
/// <see cref="RequirementsService.CreateAsync"/>/<see cref="RequirementsService.CreateCollectionAsync"/>/
/// <see cref="RequirementsService.CreateGroupAsync"/> each leave open) is
/// found and, on request, repaired — and a live, correctly-indexed
/// document is never touched.
/// </summary>
public class RequirementsReconciliationServiceTests
{
    private static (RequirementsService Requirements, EngineeringDocumentStore Documents, IPersistenceStore Persistence, RequirementsReconciliationService Reconciliation) Build()
    {
        var store = new InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var permissionEvaluator = new PermissionEvaluator();
        var verificationService = new VerificationService(documentStore, principalAccessor, permissionEvaluator);
        var requirementsService = new RequirementsService(documentStore, store, principalAccessor, verificationService);
        var reconciliation = new RequirementsReconciliationService(documentStore, store);

        return (requirementsService, documentStore, store, reconciliation);
    }

    // ---- Requirement identifier index ----

    [Fact]
    public async Task DetectAsync_FindsARequirementWithNoIdentifierIndexEntry()
    {
        var (requirements, _, persistence, reconciliation) = Build();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        // Simulate the crash window: the document exists, but the write
        // that should have indexed it never happened (or was rolled back
        // by a partial failure).
        await persistence.DeleteAsync(RequirementsService.IdentifierIndexCollectionName, "REQ-1");

        var report = await reconciliation.DetectAsync();

        var finding = Assert.Single(report.Findings, f => f.Category == RequirementsReconciliationService.RequirementMissingIndexEntryCategory);
        Assert.Equal(requirement.Id, finding.DocumentId);
        Assert.Equal("REQ-1", finding.Key);
        Assert.False(finding.Repaired);
    }

    [Fact]
    public async Task DetectAsync_DoesNotChangeAnything()
    {
        var (requirements, _, persistence, reconciliation) = Build();
        await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await persistence.DeleteAsync(RequirementsService.IdentifierIndexCollectionName, "REQ-1");

        await reconciliation.DetectAsync();

        Assert.Null(await requirements.FindByIdentifierAsync("REQ-1"));
    }

    [Fact]
    public async Task SweepAsync_RepairsTheMissingIdentifierIndexEntry()
    {
        var (requirements, _, persistence, reconciliation) = Build();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await persistence.DeleteAsync(RequirementsService.IdentifierIndexCollectionName, "REQ-1");

        var report = await reconciliation.SweepAsync();

        Assert.True(Assert.Single(report.Findings).Repaired);
        var recovered = await requirements.FindByIdentifierAsync("REQ-1");
        Assert.NotNull(recovered);
        Assert.Equal(requirement.Id, recovered!.Id);
    }

    [Fact]
    public async Task SweepAsync_NeverTouchesALiveCorrectlyIndexedRequirement()
    {
        var (requirements, _, _, reconciliation) = Build();
        await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await requirements.CreateAsync("REQ-2", "The system shall do Y.");

        var report = await reconciliation.SweepAsync();

        Assert.Empty(report.Findings);
        Assert.NotNull(await requirements.FindByIdentifierAsync("REQ-1"));
        Assert.NotNull(await requirements.FindByIdentifierAsync("REQ-2"));
    }

    [Fact]
    public async Task SweepAsync_NeverOverwritesAGenuineIdentifierCollision()
    {
        var (requirements, documents, persistence, reconciliation) = Build();
        var first = await requirements.CreateAsync("REQ-1", "First.");
        var orphanDocument = await documents.CreateAsync(RequirementsService.RequirementDocumentKind,
            "{\"Identifier\":\"REQ-1\",\"Statement\":\"Orphaned duplicate.\",\"Category\":null,\"Status\":0,\"CreatedByPrincipalId\":\"unknown\",\"CreatedAt\":\"2026-01-01T00:00:00+00:00\"}");

        var report = await reconciliation.SweepAsync();

        var finding = Assert.Single(report.Findings, f => f.DocumentId == orphanDocument.Id);
        Assert.False(finding.Repaired);

        // The original registration is untouched.
        var stillIndexed = await requirements.FindByIdentifierAsync("REQ-1");
        Assert.Equal(first.Id, stillIndexed!.Id);
    }

    [Fact]
    public async Task DetectAsync_FindsAStaleIdentifierIndexEntry()
    {
        var (_, _, persistence, reconciliation) = Build();
        await persistence.WriteAsync(RequirementsService.IdentifierIndexCollectionName, "GHOST-1", Guid.NewGuid().ToString("N"));

        var report = await reconciliation.DetectAsync();

        var finding = Assert.Single(report.Findings);
        Assert.Equal(RequirementsReconciliationService.StaleIdentifierIndexEntryCategory, finding.Category);
        Assert.Equal("GHOST-1", finding.Key);
    }

    [Fact]
    public async Task SweepAsync_RemovesAStaleIdentifierIndexEntry()
    {
        var (_, _, persistence, reconciliation) = Build();
        await persistence.WriteAsync(RequirementsService.IdentifierIndexCollectionName, "GHOST-1", Guid.NewGuid().ToString("N"));

        await reconciliation.SweepAsync();

        Assert.Null(await persistence.ReadAsync(RequirementsService.IdentifierIndexCollectionName, "GHOST-1"));
    }

    // ---- Requirement Collection registry ----

    [Fact]
    public async Task DetectAsync_FindsACollectionWithNoRegistryEntry()
    {
        var (requirements, _, persistence, reconciliation) = Build();
        var collection = await requirements.CreateCollectionAsync("Set A");
        await persistence.DeleteAsync(RequirementsService.CollectionRegistryCollectionName, collection.Id.ToString("N"));

        var report = await reconciliation.DetectAsync();

        var finding = Assert.Single(report.Findings, f => f.Category == RequirementsReconciliationService.CollectionMissingRegistryEntryCategory);
        Assert.Equal(collection.Id, finding.DocumentId);
    }

    [Fact]
    public async Task SweepAsync_RepairsAMissingCollectionRegistryEntry_SoListCollectionsFindsItAgain()
    {
        var (requirements, _, persistence, reconciliation) = Build();
        var collection = await requirements.CreateCollectionAsync("Set A");
        await persistence.DeleteAsync(RequirementsService.CollectionRegistryCollectionName, collection.Id.ToString("N"));

        await reconciliation.SweepAsync();

        var listed = await requirements.ListCollectionsAsync();
        Assert.Contains(listed, c => c.Id == collection.Id);
    }

    // ---- Requirement Group registry ----

    [Fact]
    public async Task DetectAsync_FindsAGroupWithNoRegistryEntry()
    {
        var (requirements, _, persistence, reconciliation) = Build();
        var group = await requirements.CreateGroupAsync("Group A");
        await persistence.DeleteAsync(RequirementsService.GroupRegistryCollectionName, group.Id.ToString("N"));

        var report = await reconciliation.DetectAsync();

        var finding = Assert.Single(report.Findings, f => f.Category == RequirementsReconciliationService.GroupMissingRegistryEntryCategory);
        Assert.Equal(group.Id, finding.DocumentId);
    }

    [Fact]
    public async Task SweepAsync_RepairsAMissingGroupRegistryEntry_SoListGroupsFindsItAgain()
    {
        var (requirements, _, persistence, reconciliation) = Build();
        var group = await requirements.CreateGroupAsync("Group A");
        await persistence.DeleteAsync(RequirementsService.GroupRegistryCollectionName, group.Id.ToString("N"));

        await reconciliation.SweepAsync();

        var listed = await requirements.ListGroupsAsync();
        Assert.Contains(listed, g => g.Id == group.Id);
    }

    // ---- The race (`WP 16.4B-R2`) ----

    /// <summary>
    /// The Requirements sibling of <c>MaterialCatalogReconciliationServiceTests</c>'s
    /// own race test: a sweep interleaved with an in-flight
    /// <see cref="RequirementsService.CreateAsync"/> call must never make
    /// the fully-created Requirement unfindable by identifier. No timing —
    /// <see cref="GatedListKeysPersistenceStore"/> pauses the sweep's own
    /// document scan (the authoritative side, read second under the fixed
    /// order) until the registration inside that pause has completed.
    /// </summary>
    [Fact]
    public async Task SweepAsync_InterleavedWithAnInFlightCreate_NeverMakesTheRequirementUnfindable()
    {
        var store = new InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var permissionEvaluator = new PermissionEvaluator();
        var verificationService = new VerificationService(documentStore, principalAccessor, permissionEvaluator);
        var requirementsService = new RequirementsService(documentStore, store, principalAccessor, verificationService);

        var gated = new GatedListKeysPersistenceStore(store, EngineeringDocumentStore.DocumentsCollectionName);
        var reconciliation = new RequirementsReconciliationService(documentStore, gated);

        var sweepTask = reconciliation.SweepAsync();
        await gated.ReachedGate;

        var requirement = await requirementsService.CreateAsync("REQ-RACE-1", "The system shall survive a concurrent sweep.");

        gated.Release();
        var report = await sweepTask;

        var recovered = await requirementsService.FindByIdentifierAsync("REQ-RACE-1");
        Assert.NotNull(recovered);
        Assert.Equal(requirement.Id, recovered!.Id);

        Assert.DoesNotContain(report.Findings, f =>
            f.Category == RequirementsReconciliationService.StaleIdentifierIndexEntryCategory && f.DocumentId == requirement.Id);
    }

    /// <summary>
    /// The same race, for a <see cref="RequirementsService.CreateCollectionAsync"/>
    /// call and the Collection registry — the identical document-then-
    /// registry write order, reconciled by <c>ReconcileRegistryAsync</c>
    /// rather than <c>ReconcileIdentifierIndexAsync</c>.
    /// </summary>
    [Fact]
    public async Task SweepAsync_InterleavedWithAnInFlightCollectionCreate_NeverMakesTheCollectionUnlisted()
    {
        var store = new InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var permissionEvaluator = new PermissionEvaluator();
        var verificationService = new VerificationService(documentStore, principalAccessor, permissionEvaluator);
        var requirementsService = new RequirementsService(documentStore, store, principalAccessor, verificationService);

        var gated = new GatedListKeysPersistenceStore(store, EngineeringDocumentStore.DocumentsCollectionName);
        var reconciliation = new RequirementsReconciliationService(documentStore, gated);

        var sweepTask = reconciliation.SweepAsync();
        await gated.ReachedGate;

        var collection = await requirementsService.CreateCollectionAsync("Race Set");

        gated.Release();
        var report = await sweepTask;

        var listed = await requirementsService.ListCollectionsAsync();
        Assert.Contains(listed, c => c.Id == collection.Id);

        Assert.DoesNotContain(report.Findings, f =>
            f.Category == RequirementsReconciliationService.StaleCollectionRegistryEntryCategory && f.DocumentId == collection.Id);
    }

    /// <summary>
    /// A wrapper around a real <see cref="IPersistenceStore"/> that pauses
    /// one specific collection's <see cref="ListKeysAsync"/> call until
    /// released — mirrors <c>Materials.MaterialCatalogReconciliationServiceTests</c>'s
    /// own identical seam, duplicated per this codebase's own small-
    /// test-local-fake convention.
    /// </summary>
    private sealed class GatedListKeysPersistenceStore : IPersistenceStore
    {
        private readonly IPersistenceStore _inner;
        private readonly string _gatedCollection;
        private readonly TaskCompletionSource _reachedGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GatedListKeysPersistenceStore(IPersistenceStore inner, string gatedCollection)
        {
            _inner = inner;
            _gatedCollection = gatedCollection;
        }

        public Task ReachedGate => _reachedGate.Task;

        public void Release() => _releaseGate.TrySetResult();

        public async Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default)
        {
            if (string.Equals(collection, _gatedCollection, StringComparison.Ordinal))
            {
                _reachedGate.TrySetResult();
                await _releaseGate.Task.ConfigureAwait(false);
            }

            return await _inner.ListKeysAsync(collection, cancellationToken).ConfigureAwait(false);
        }

        public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(collection, key, cancellationToken);

        public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default) =>
            _inner.WriteAsync(collection, key, value, cancellationToken);

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(collection, key, cancellationToken);
    }

    // ---- Constructor validation ----

    [Fact]
    public void Constructor_NullDocumentStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RequirementsReconciliationService(null!, new InMemoryPersistenceStore()));
    }

    [Fact]
    public void Constructor_NullPersistenceStore_Throws()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());
        Assert.Throws<ArgumentNullException>(() => new RequirementsReconciliationService(documentStore, null!));
    }
}
