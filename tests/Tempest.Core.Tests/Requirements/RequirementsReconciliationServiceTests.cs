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
