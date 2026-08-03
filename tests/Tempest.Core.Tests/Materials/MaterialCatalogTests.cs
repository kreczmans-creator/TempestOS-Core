using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Materials;

public class MaterialCatalogTests
{
    private static MaterialCatalog BuildCatalog(out EngineeringDocumentStore documentStore)
    {
        var persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new MaterialCatalog(documentStore, persistenceStore);
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

    // ----------------------------------------------------------------
    // RegisterAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_ReturnsSpecification_WithGivenNameCategoryAndProperties()
    {
        var catalog = BuildCatalog(out _);
        var properties = BuildProperties();

        var material = await catalog.RegisterAsync("test-001", "Test Material", properties, category: "TestFixture");

        Assert.Equal("test-001", material.MaterialId);
        Assert.Equal("Test Material", material.Name);
        Assert.Equal("TestFixture", material.Category);
        Assert.Equal(1, material.RevisionNumber);
        Assert.NotEqual(Guid.Empty, material.UnderlyingDocumentId);
        Assert.Single(material.Properties);
    }

    [Fact]
    public async Task RegisterAsync_NoCategory_DefaultsToNull()
    {
        var catalog = BuildCatalog(out _);

        var material = await catalog.RegisterAsync("test-002", "Test Material", BuildProperties());

        Assert.Null(material.Category);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateMaterialId_ThrowsDuplicateMaterialException()
    {
        var catalog = BuildCatalog(out _);
        await catalog.RegisterAsync("test-003", "First", BuildProperties());

        var exception = await Assert.ThrowsAsync<DuplicateMaterialException>(
            () => catalog.RegisterAsync("test-003", "Second", BuildProperties()));

        Assert.Equal("test-003", exception.MaterialId);
    }

    [Fact]
    public async Task RegisterAsync_NullMaterialId_ThrowsArgumentNullException()
    {
        var catalog = BuildCatalog(out _);

        await Assert.ThrowsAsync<ArgumentNullException>(() => catalog.RegisterAsync(null!, "Name", BuildProperties()));
    }

    [Fact]
    public async Task RegisterAsync_WhitespaceMaterialId_ThrowsArgumentException()
    {
        var catalog = BuildCatalog(out _);

        await Assert.ThrowsAsync<ArgumentException>(() => catalog.RegisterAsync("   ", "Name", BuildProperties()));
    }

    [Fact]
    public async Task RegisterAsync_NullProperties_ThrowsArgumentNullException()
    {
        var catalog = BuildCatalog(out _);

        await Assert.ThrowsAsync<ArgumentNullException>(() => catalog.RegisterAsync("test-004", "Name", null!));
    }

    [Fact]
    public async Task RegisterAsync_ConcurrentDifferentMaterialIds_AllSucceed()
    {
        var catalog = BuildCatalog(out _);

        var tasks = Enumerable.Range(0, 20)
            .Select(i => catalog.RegisterAsync($"concurrent-{i}", $"Material {i}", BuildProperties()))
            .ToArray();
        await Task.WhenAll(tasks);

        var all = await catalog.ListAsync();
        Assert.Equal(20, all.Count);
    }

    [Fact]
    public async Task RegisterAsync_ConcurrentSameMaterialId_ExactlyOneSucceeds()
    {
        var catalog = BuildCatalog(out _);

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(async i =>
            {
                try
                {
                    await catalog.RegisterAsync("same-id", $"Attempt {i}", BuildProperties());
                    return true;
                }
                catch (DuplicateMaterialException)
                {
                    return false;
                }
            }));

        Assert.Single(results, succeeded => succeeded);
    }

    // ----------------------------------------------------------------
    // FindAsync / ListAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task FindAsync_ExistingMaterial_ReturnsIt()
    {
        var catalog = BuildCatalog(out _);
        await catalog.RegisterAsync("test-005", "Test Material", BuildProperties());

        var found = await catalog.FindAsync("test-005");

        Assert.NotNull(found);
        Assert.Equal("Test Material", found!.Name);
    }

    [Fact]
    public async Task FindAsync_NonExistentMaterial_ReturnsNull()
    {
        var catalog = BuildCatalog(out _);

        var found = await catalog.FindAsync("does-not-exist");

        Assert.Null(found);
    }

    [Fact]
    public async Task ListAsync_NoMaterialsRegistered_ReturnsEmpty()
    {
        var catalog = BuildCatalog(out _);

        var all = await catalog.ListAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryRegisteredMaterial()
    {
        var catalog = BuildCatalog(out _);
        await catalog.RegisterAsync("test-006", "First", BuildProperties());
        await catalog.RegisterAsync("test-007", "Second", BuildProperties());

        var all = await catalog.ListAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, m => m.MaterialId == "test-006");
        Assert.Contains(all, m => m.MaterialId == "test-007");
    }

    // ----------------------------------------------------------------
    // ReviseAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task ReviseAsync_ExistingMaterial_UpdatesPropertiesAndIncrementsRevisionNumber()
    {
        var catalog = BuildCatalog(out _);
        await catalog.RegisterAsync("test-008", "Test Material", BuildProperties(100.0), category: "TestFixture");

        var revised = await catalog.ReviseAsync("test-008", BuildProperties(105.0), "Updated fictional value.");

        Assert.Equal(2, revised.RevisionNumber);
        var property = (Quantity<Pressure>)revised.Properties["YieldStrength"].Value;
        Assert.Equal(105.0, property.Value);
    }

    [Fact]
    public async Task ReviseAsync_PreservesNameAndCategory()
    {
        var catalog = BuildCatalog(out _);
        await catalog.RegisterAsync("test-009", "Original Name", BuildProperties(), category: "OriginalCategory");

        var revised = await catalog.ReviseAsync("test-009", BuildProperties(200.0), null);

        Assert.Equal("Original Name", revised.Name);
        Assert.Equal("OriginalCategory", revised.Category);
    }

    [Fact]
    public async Task ReviseAsync_NonExistentMaterial_ThrowsMaterialNotFoundException()
    {
        var catalog = BuildCatalog(out _);

        var exception = await Assert.ThrowsAsync<MaterialNotFoundException>(
            () => catalog.ReviseAsync("does-not-exist", BuildProperties(), null));

        Assert.Equal("does-not-exist", exception.MaterialId);
    }

    [Fact]
    public async Task ReviseAsync_NullProperties_ThrowsArgumentNullException()
    {
        var catalog = BuildCatalog(out _);
        await catalog.RegisterAsync("test-010", "Test Material", BuildProperties());

        await Assert.ThrowsAsync<ArgumentNullException>(() => catalog.ReviseAsync("test-010", null!, null));
    }

    [Fact]
    public async Task ReviseAsync_ThenFindAsync_ReturnsLatestRevision()
    {
        var catalog = BuildCatalog(out _);
        await catalog.RegisterAsync("test-011", "Test Material", BuildProperties(100.0));
        await catalog.ReviseAsync("test-011", BuildProperties(150.0), null);

        var found = await catalog.FindAsync("test-011");

        Assert.Equal(2, found!.RevisionNumber);
        var property = (Quantity<Pressure>)found.Properties["YieldStrength"].Value;
        Assert.Equal(150.0, property.Value);
    }

    // ----------------------------------------------------------------
    // Provenance preservation
    // ----------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_ThenFindAsync_PreservesEveryProvenanceField()
    {
        var catalog = BuildCatalog(out _);
        var provenance = new MaterialPropertyProvenance(
            SourceReference: "Fictional Test Standard TFS-001",
            SourceRevision: 7,
            ValidationStatus: MaterialPropertyValidationStatus.Superseded,
            ConfidenceLevel: MaterialPropertyConfidenceLevel.Medium,
            ApplicableConditions: "Between -20C and 80C, fictional",
            Notes: "Round-trip provenance test.");
        var properties = new Dictionary<string, MaterialProperty>
        {
            ["YieldStrength"] = new MaterialProperty(new Quantity<Pressure>(123.0, PressureUnits.Megapascal), provenance),
        };
        await catalog.RegisterAsync("test-012", "Test Material", properties);

        var found = await catalog.FindAsync("test-012");

        Assert.Equal(provenance, found!.Properties["YieldStrength"].Provenance);
    }

    [Fact]
    public void MaterialProperty_NoProvenanceGiven_CannotBeConstructedWithNullProvenance()
    {
        Assert.Throws<ArgumentNullException>(() => new MaterialProperty(new Quantity<Length>(1.0, LengthUnits.Metre), null!));
    }

    [Fact]
    public void MaterialPropertyProvenance_Unknown_IsTheHonestDefault()
    {
        var unknown = MaterialPropertyProvenance.Unknown;

        Assert.Null(unknown.SourceReference);
        Assert.Null(unknown.SourceRevision);
        Assert.Equal(MaterialPropertyValidationStatus.Unvalidated, unknown.ValidationStatus);
        Assert.Equal(MaterialPropertyConfidenceLevel.Unknown, unknown.ConfidenceLevel);
    }

    // ----------------------------------------------------------------
    // Property value validation
    // ----------------------------------------------------------------

    [Fact]
    public void MaterialProperty_UnsupportedValueType_ThrowsMaterialsException()
    {
        Assert.Throws<MaterialsException>(() => new MaterialProperty(42.0, MaterialPropertyProvenance.Unknown));
    }

    [Fact]
    public void MaterialProperty_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new MaterialProperty(null!, MaterialPropertyProvenance.Unknown));
    }

    [Fact]
    public void MaterialProperty_EveryUnitsAndQuantitiesDimension_IsSupported()
    {
        var length = new MaterialProperty(new Quantity<Length>(1.0, LengthUnits.Metre), MaterialPropertyProvenance.Unknown);
        var mass = new MaterialProperty(new Quantity<Mass>(1.0, MassUnits.Kilogram), MaterialPropertyProvenance.Unknown);
        var duration = new MaterialProperty(new Quantity<Duration>(1.0, DurationUnits.Second), MaterialPropertyProvenance.Unknown);
        var force = new MaterialProperty(new Quantity<Force>(1.0, ForceUnits.Newton), MaterialPropertyProvenance.Unknown);
        var pressure = new MaterialProperty(new Quantity<Pressure>(1.0, PressureUnits.Pascal), MaterialPropertyProvenance.Unknown);
        var area = new MaterialProperty(new Quantity<Area>(1.0, AreaUnits.SquareMetre), MaterialPropertyProvenance.Unknown);
        var volume = new MaterialProperty(new Quantity<Volume>(1.0, VolumeUnits.CubicMetre), MaterialPropertyProvenance.Unknown);

        Assert.NotNull(length);
        Assert.NotNull(mass);
        Assert.NotNull(duration);
        Assert.NotNull(force);
        Assert.NotNull(pressure);
        Assert.NotNull(area);
        Assert.NotNull(volume);
    }

    // ----------------------------------------------------------------
    // Equality / immutability
    // ----------------------------------------------------------------

    [Fact]
    public void MaterialPropertyProvenance_SameValues_AreEqual()
    {
        var a = new MaterialPropertyProvenance("Source", 1, MaterialPropertyValidationStatus.Validated, MaterialPropertyConfidenceLevel.High, "Conditions", "Notes");
        var b = new MaterialPropertyProvenance("Source", 1, MaterialPropertyValidationStatus.Validated, MaterialPropertyConfidenceLevel.High, "Conditions", "Notes");

        Assert.Equal(a, b);
    }

    [Fact]
    public void MaterialPropertyProvenance_With_ProducesNewInstance_OriginalUnchanged()
    {
        var original = MaterialPropertyProvenance.Unknown;

        var modified = original with { SourceReference = "New Source" };

        Assert.Null(original.SourceReference);
        Assert.Equal("New Source", modified.SourceReference);
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void MaterialProperty_SameValueAndProvenance_AreEqual()
    {
        var provenance = MaterialPropertyProvenance.Unknown;
        var a = new MaterialProperty(new Quantity<Length>(5.0, LengthUnits.Metre), provenance);
        var b = new MaterialProperty(new Quantity<Length>(5.0, LengthUnits.Metre), provenance);

        Assert.Equal(a, b);
    }

    // ----------------------------------------------------------------
    // Traceability: the underlying document is directly usable through
    // IEngineeringDocumentStore for revision history and references this
    // catalogue does not itself duplicate.
    // ----------------------------------------------------------------

    [Fact]
    public async Task UnderlyingDocumentId_IsDirectlyRetrievableThroughEngineeringDocumentStore()
    {
        var catalog = BuildCatalog(out var documentStore);
        var material = await catalog.RegisterAsync("test-013", "Test Material", BuildProperties());

        var document = await documentStore.FindAsync(material.UnderlyingDocumentId);

        Assert.NotNull(document);
        Assert.Equal(MaterialCatalog.MaterialSpecificationDocumentKind, document!.Kind);
    }

    [Fact]
    public async Task UnderlyingDocument_RevisionHistory_MatchesMaterialRevisionCount()
    {
        var catalog = BuildCatalog(out var documentStore);
        var material = await catalog.RegisterAsync("test-014", "Test Material", BuildProperties(100.0));
        await catalog.ReviseAsync("test-014", BuildProperties(110.0), null);

        var history = await documentStore.GetRevisionHistoryAsync(material.UnderlyingDocumentId);

        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task UnderlyingDocument_CanBeLinkedToAnotherDocumentDirectlyThroughEngineeringDocumentStore()
    {
        var catalog = BuildCatalog(out var documentStore);
        var material = await catalog.RegisterAsync("test-015", "Test Material", BuildProperties());
        var sourceStandardDocument = await documentStore.CreateAsync("SourceStandard", "Fictional test standard content.");

        await documentStore.LinkAsync(material.UnderlyingDocumentId, sourceStandardDocument.Id, "derivedFrom");
        var references = await documentStore.GetReferencesAsync(material.UnderlyingDocumentId);

        var reference = Assert.Single(references);
        Assert.Equal("derivedFrom", reference.RelationshipKind);
        Assert.Equal(sourceStandardDocument.Id, reference.TargetDocumentId);
    }

    // ----------------------------------------------------------------
    // Constructor validation / failure injection
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_NullDocumentStore_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new MaterialCatalog(null!, new InMemoryPersistenceStore()));
    }

    [Fact]
    public void Constructor_NullPersistenceStore_ThrowsArgumentNullException()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());

        Assert.Throws<ArgumentNullException>(() => new MaterialCatalog(documentStore, null!));
    }

    [Fact]
    public async Task RegisterAsync_PersistenceUnavailable_PropagatesExceptionUnmodified()
    {
        var documentStore = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());
        var catalog = new MaterialCatalog(documentStore, new FailingPersistenceStore());

        await Assert.ThrowsAsync<Tempest.Core.Persistence.PersistenceStoreUnavailableException>(
            () => catalog.RegisterAsync("test-016", "Test Material", BuildProperties()));
    }
}
