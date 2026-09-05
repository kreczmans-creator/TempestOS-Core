using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Materials;

/// <summary>
/// `TD-67` closure tests for the reconcile/repair path the register's own
/// entry names as missing: <see cref="MaterialCatalog.RegisterAsync"/>
/// writes the backing document before its own <c>materialId</c> index
/// entry, so a crash or write failure between the two leaves a material
/// document nothing can find. Mirrors
/// <c>Requirements.RequirementsReconciliationServiceTests</c>'s own
/// identical shape for the sibling index.
/// </summary>
public class MaterialCatalogReconciliationServiceTests
{
    private static (MaterialCatalog Catalog, EngineeringDocumentStore Documents, IPersistenceStore Persistence, MaterialCatalogReconciliationService Reconciliation) Build()
    {
        var persistence = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor());
        var catalog = new MaterialCatalog(documentStore, persistence);
        var reconciliation = new MaterialCatalogReconciliationService(documentStore, persistence);

        return (catalog, documentStore, persistence, reconciliation);
    }

    private static IReadOnlyDictionary<string, MaterialProperty> BuildProperties(double yieldStrengthMPa = 100.0) =>
        new Dictionary<string, MaterialProperty>
        {
            ["YieldStrength"] = new MaterialProperty(
                new Quantity<Pressure>(yieldStrengthMPa, PressureUnits.Megapascal),
                new MaterialPropertyProvenance(
                    SourceReference: "Test fixture — not a real material standard",
                    SourceRevision: 3,
                    ValidationStatus: MaterialPropertyValidationStatus.Validated,
                    ConfidenceLevel: MaterialPropertyConfidenceLevel.High,
                    ApplicableConditions: "Room temperature",
                    Notes: "Fictional test value.")),
        };

    [Fact]
    public async Task DetectAsync_FindsAMaterialWithNoIndexEntry()
    {
        var (catalog, _, persistence, reconciliation) = Build();
        var material = await catalog.RegisterAsync("AL-7075", "Aluminium 7075", BuildProperties());
        await persistence.DeleteAsync(MaterialCatalog.IndexCollectionName, "AL-7075");

        var report = await reconciliation.DetectAsync();

        var finding = Assert.Single(report.Findings, f => f.Category == MaterialCatalogReconciliationService.MissingIndexEntryCategory);
        Assert.Equal(material.UnderlyingDocumentId, finding.DocumentId);
        Assert.Equal("AL-7075", finding.MaterialId);
        Assert.False(finding.Repaired);
    }

    [Fact]
    public async Task DetectAsync_DoesNotChangeAnything()
    {
        var (catalog, _, persistence, reconciliation) = Build();
        await catalog.RegisterAsync("AL-7075", "Aluminium 7075", BuildProperties());
        await persistence.DeleteAsync(MaterialCatalog.IndexCollectionName, "AL-7075");

        await reconciliation.DetectAsync();

        Assert.Null(await catalog.FindAsync("AL-7075"));
    }

    [Fact]
    public async Task SweepAsync_RepairsTheMissingIndexEntry()
    {
        var (catalog, _, persistence, reconciliation) = Build();
        var material = await catalog.RegisterAsync("AL-7075", "Aluminium 7075", BuildProperties());
        await persistence.DeleteAsync(MaterialCatalog.IndexCollectionName, "AL-7075");

        var report = await reconciliation.SweepAsync();

        Assert.True(Assert.Single(report.Findings).Repaired);
        var recovered = await catalog.FindAsync("AL-7075");
        Assert.NotNull(recovered);
        Assert.Equal(material.UnderlyingDocumentId, recovered!.UnderlyingDocumentId);
    }

    [Fact]
    public async Task SweepAsync_NeverTouchesALiveCorrectlyIndexedMaterial()
    {
        var (catalog, _, _, reconciliation) = Build();
        await catalog.RegisterAsync("AL-7075", "Aluminium 7075", BuildProperties());
        await catalog.RegisterAsync("TI-6AL-4V", "Titanium 6Al-4V", BuildProperties(880.0));

        var report = await reconciliation.SweepAsync();

        Assert.Empty(report.Findings);
        Assert.NotNull(await catalog.FindAsync("AL-7075"));
        Assert.NotNull(await catalog.FindAsync("TI-6AL-4V"));
    }

    [Fact]
    public async Task DetectAsync_FindsAStaleIndexEntry()
    {
        var (_, _, persistence, reconciliation) = Build();
        await persistence.WriteAsync(MaterialCatalog.IndexCollectionName, "GHOST-1", Guid.NewGuid().ToString("N"));

        var report = await reconciliation.DetectAsync();

        var finding = Assert.Single(report.Findings);
        Assert.Equal(MaterialCatalogReconciliationService.StaleIndexEntryCategory, finding.Category);
        Assert.Equal("GHOST-1", finding.MaterialId);
    }

    [Fact]
    public async Task SweepAsync_RemovesAStaleIndexEntry()
    {
        var (_, _, persistence, reconciliation) = Build();
        await persistence.WriteAsync(MaterialCatalog.IndexCollectionName, "GHOST-1", Guid.NewGuid().ToString("N"));

        await reconciliation.SweepAsync();

        Assert.Null(await persistence.ReadAsync(MaterialCatalog.IndexCollectionName, "GHOST-1"));
    }

    // ---- The race (`WP 16.4B-R2`) ----

    /// <summary>
    /// Deterministically reproduces the reviewer's exact scenario: a
    /// sweep interleaved with an in-flight <see cref="MaterialCatalog.RegisterAsync"/>
    /// call must never make the fully-registered material unfindable. No
    /// timing, no <see cref="Task.Delay(int)"/> — <see cref="GatedListKeysPersistenceStore"/>
    /// pauses the sweep's own document scan (the second, authoritative-side
    /// read under the fixed order) until the test releases it, and the
    /// registration runs to completion entirely inside that pause.
    /// </summary>
    [Fact]
    public async Task SweepAsync_InterleavedWithAnInFlightRegistration_NeverMakesTheMaterialUnfindable()
    {
        var persistence = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor());
        var catalog = new MaterialCatalog(documentStore, persistence);

        var gated = new GatedListKeysPersistenceStore(persistence, EngineeringDocumentStore.DocumentsCollectionName);
        var reconciliation = new MaterialCatalogReconciliationService(documentStore, gated);

        var sweepTask = reconciliation.SweepAsync();

        // The sweep has already captured its (empty) index snapshot — the
        // derived side — and is paused about to scan documents — the
        // authoritative side.
        await gated.ReachedGate;

        var material = await catalog.RegisterAsync("AL-RACE-1", "Aluminium Race", BuildProperties());

        gated.Release();
        var report = await sweepTask;

        // The one assertion that matters: still findable.
        var recovered = await catalog.FindAsync("AL-RACE-1");
        Assert.NotNull(recovered);
        Assert.Equal(material.UnderlyingDocumentId, recovered!.UnderlyingDocumentId);

        // No finding may have been a deletion of the live entry.
        Assert.DoesNotContain(report.Findings, f =>
            f.Category == MaterialCatalogReconciliationService.StaleIndexEntryCategory && f.DocumentId == material.UnderlyingDocumentId);
    }

    /// <summary>
    /// A wrapper around a real <see cref="IPersistenceStore"/> that pauses
    /// one specific collection's <see cref="ListKeysAsync"/> call until
    /// released — the deterministic interleaving seam these race tests
    /// use in place of any timing dependency.
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

    [Fact]
    public void Constructor_NullDocumentStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MaterialCatalogReconciliationService(null!, new InMemoryPersistenceStore()));
    }

    [Fact]
    public void Constructor_NullPersistenceStore_Throws()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());
        Assert.Throws<ArgumentNullException>(() => new MaterialCatalogReconciliationService(documentStore, null!));
    }
}
