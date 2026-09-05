using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;
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
    /// timing, no <see cref="Task.Delay(int)"/> —
    /// <see cref="OrderAgnosticGatedListKeysPersistenceStore"/> pauses the
    /// sweep's own document scan (the second, authoritative-side read
    /// under the fixed order) until the test releases it, and the
    /// registration runs to completion entirely inside that pause. The
    /// gate is deliberately order-agnostic — armed on the pair {index,
    /// documents} and pausing on whichever of the two is read <em>second</em>
    /// by arrival, not by name — so this test still exercises the real
    /// interleaving (and still fails) if the sweep's read order is ever
    /// reverted to documents-first; a gate keyed to the documents
    /// collection's name alone would instead pause on the very first call
    /// in that reverted order, let the whole registration run to
    /// completion before either scan had read anything, and pass against
    /// broken code (`WP16.4A-R1`).
    /// </summary>
    [Fact]
    public async Task SweepAsync_InterleavedWithAnInFlightRegistration_NeverMakesTheMaterialUnfindable()
    {
        var persistence = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor());
        var catalog = new MaterialCatalog(documentStore, persistence);

        var gated = new OrderAgnosticGatedListKeysPersistenceStore(
            persistence, MaterialCatalog.IndexCollectionName, EngineeringDocumentStore.DocumentsCollectionName);
        var reconciliation = new MaterialCatalogReconciliationService(documentStore, gated);

        var sweepTask = reconciliation.SweepAsync();

        // The sweep has already captured its (empty) index snapshot — the
        // derived side — and is paused about to scan documents — the
        // authoritative side.
        await gated.ReachedGate.WaitAsync(OrderAgnosticGatedListKeysPersistenceStore.GateTimeout);

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

    // The gate itself — order-agnostic, shared with the Requirements
    // sibling of this race test — lives in
    // Tempest.Core.Tests.Persistence.OrderAgnosticGatedListKeysPersistenceStore.

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
