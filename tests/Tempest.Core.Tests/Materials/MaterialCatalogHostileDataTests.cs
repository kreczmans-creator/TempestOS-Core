using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Materials;

/// <summary>
/// `TD-59`/`TD-60` closure tests over the REAL production stack —
/// <see cref="MaterialCatalog"/> over a real <see cref="EngineeringDocumentStore"/>
/// over a real, file-backed <see cref="PersistenceStore"/> (never the
/// in-memory fake, which cannot exhibit either defect): reserved
/// device-name identifiers must round-trip end to end, and corrupted
/// index/document content must surface as controlled
/// <see cref="MaterialsException"/>s, never raw BCL exceptions or
/// silently missing records.
/// </summary>
public class MaterialCatalogHostileDataTests
{
    private static IConfigurationProvider BuildConfiguration(string rootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
        ])).Build();

    private static (MaterialCatalog Catalog, EngineeringDocumentStore Documents, PersistenceStore Store) BuildRealStack(string rootPath)
    {
        var store = new PersistenceStore(BuildConfiguration(rootPath));
        var documentStore = new EngineeringDocumentStore(store, new CurrentPrincipalAccessor());
        return (new MaterialCatalog(documentStore, store), documentStore, store);
    }

    private static IReadOnlyDictionary<string, MaterialProperty> BuildProperties() =>
        new Dictionary<string, MaterialProperty>
        {
            ["YieldStrength"] = new MaterialProperty(
                new Quantity<Pressure>(100.0, PressureUnits.Megapascal),
                new MaterialPropertyProvenance(
                    SourceReference: "Test fixture — not a real material standard",
                    SourceRevision: 1,
                    ValidationStatus: MaterialPropertyValidationStatus.Validated,
                    ConfidenceLevel: MaterialPropertyConfidenceLevel.High,
                    ApplicableConditions: "Room temperature",
                    Notes: "Fictional test value.")),
        };

    // ----------------------------------------------------------------
    // TD-59 — reserved device-name identifiers, end to end
    // ----------------------------------------------------------------

    [Theory]
    [InlineData("NUL")]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    [InlineData("con")]
    [InlineData("CON.txt")]
    public async Task RegisterAsync_ReservedDeviceNameMaterialId_IsFindableAndListed(string materialId)
    {
        using var temp = new TempDirectory();
        var (catalog, _, _) = BuildRealStack(temp.Path);

        var registered = await catalog.RegisterAsync(materialId, "Test Material", BuildProperties());
        Assert.Equal(materialId, registered.MaterialId);

        // The original defect: on Windows the index write went to a
        // device, and the registered material silently vanished from
        // both lookups — a successful registration MUST be findable.
        var found = await catalog.FindAsync(materialId);
        Assert.NotNull(found);
        Assert.Equal(materialId, found!.MaterialId);

        var listed = await catalog.ListAsync();
        Assert.Contains(listed, m => m.MaterialId == materialId);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateReservedName_ThrowsDuplicateMaterialException()
    {
        using var temp = new TempDirectory();
        var (catalog, _, _) = BuildRealStack(temp.Path);

        await catalog.RegisterAsync("CON", "First", BuildProperties());

        await Assert.ThrowsAsync<DuplicateMaterialException>(
            () => catalog.RegisterAsync("CON", "Second", BuildProperties()));
    }

    [Fact]
    public async Task RegisterAsync_ReservedNameCaseVariants_AreDistinctMaterials()
    {
        using var temp = new TempDirectory();
        var (catalog, _, _) = BuildRealStack(temp.Path);

        await catalog.RegisterAsync("CON", "Upper", BuildProperties());
        await catalog.RegisterAsync("con", "Lower", BuildProperties());

        Assert.Equal("Upper", (await catalog.FindAsync("CON"))!.Name);
        Assert.Equal("Lower", (await catalog.FindAsync("con"))!.Name);
        Assert.Equal(2, (await catalog.ListAsync()).Count);
    }

    [Fact]
    public async Task RegisterAsync_IdentifiersAdjacentToReservedNames_AllCoexist()
    {
        using var temp = new TempDirectory();
        var (catalog, _, _) = BuildRealStack(temp.Path);

        foreach (var id in new[] { "CON", "CONX", "XCON", "COM1", "COM10" })
            await catalog.RegisterAsync(id, "M-" + id, BuildProperties());

        foreach (var id in new[] { "CON", "CONX", "XCON", "COM1", "COM10" })
            Assert.Equal("M-" + id, (await catalog.FindAsync(id))!.Name);

        Assert.Equal(5, (await catalog.ListAsync()).Count);
    }

    // ----------------------------------------------------------------
    // TD-60 — malformed index values on the passive read paths
    // ----------------------------------------------------------------

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{7d444840-9dc0-11d1-b245-5ffdce74fad2}")] // valid Guid text, but not the "N" format the index writes
    public async Task FindAsync_CorruptedIndexValue_ThrowsControlledMaterialsException(string corruptValue)
    {
        using var temp = new TempDirectory();
        var (catalog, _, store) = BuildRealStack(temp.Path);
        await store.WriteAsync(MaterialCatalog.IndexCollectionName, "AL-7075", corruptValue);

        var exception = await Assert.ThrowsAsync<MaterialsException>(() => catalog.FindAsync("AL-7075"));
        Assert.Contains("AL-7075", exception.Message);
    }

    [Fact]
    public async Task RegisterAsync_OverACorruptedIndexEntry_ThrowsControlledMaterialsException_NeverSucceeds()
    {
        using var temp = new TempDirectory();
        var (catalog, _, store) = BuildRealStack(temp.Path);
        await store.WriteAsync(MaterialCatalog.IndexCollectionName, "AL-7075", "garbage");

        // Neither a silent success (which would overwrite the corrupt
        // entry and orphan whatever it pointed at) nor a raw
        // FormatException — the corruption is reported as itself.
        await Assert.ThrowsAsync<MaterialsException>(
            () => catalog.RegisterAsync("AL-7075", "New", BuildProperties()));
    }

    [Fact]
    public async Task ListAsync_CorruptedIndexValue_ThrowsControlledMaterialsException()
    {
        using var temp = new TempDirectory();
        var (catalog, _, store) = BuildRealStack(temp.Path);
        await catalog.RegisterAsync("GOOD-1", "Good", BuildProperties());
        await store.WriteAsync(MaterialCatalog.IndexCollectionName, "BAD-1", "garbage");

        var exception = await Assert.ThrowsAsync<MaterialsException>(() => catalog.ListAsync());
        Assert.Contains("BAD-1", exception.Message);
    }

    [Fact]
    public async Task FindAsync_StaleIndexEntry_PointingAtNoDocument_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var (catalog, _, store) = BuildRealStack(temp.Path);
        await store.WriteAsync(MaterialCatalog.IndexCollectionName, "STALE-1", Guid.NewGuid().ToString("N"));

        Assert.Null(await catalog.FindAsync("STALE-1"));
    }

    [Fact]
    public async Task ListAsync_StaleIndexEntry_IsSkipped_OtherMaterialsStillListed()
    {
        using var temp = new TempDirectory();
        var (catalog, _, store) = BuildRealStack(temp.Path);
        await catalog.RegisterAsync("GOOD-1", "Good", BuildProperties());
        await store.WriteAsync(MaterialCatalog.IndexCollectionName, "STALE-1", Guid.NewGuid().ToString("N"));

        var listed = await catalog.ListAsync();

        Assert.Single(listed);
        Assert.Equal("GOOD-1", listed[0].MaterialId);
    }

    [Fact]
    public async Task FindAsync_IndexEntryPointingAtANonMaterialDocument_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var (catalog, documents, store) = BuildRealStack(temp.Path);
        var foreignDocument = await documents.CreateAsync("SomethingElse", "content");
        await store.WriteAsync(MaterialCatalog.IndexCollectionName, "WRONG-KIND", foreignDocument.Id.ToString("N"));

        Assert.Null(await catalog.FindAsync("WRONG-KIND"));
    }

    [Fact]
    public async Task FindAsync_CorruptedDocumentContent_ThrowsControlledMaterialsException()
    {
        using var temp = new TempDirectory();
        var (catalog, documents, _) = BuildRealStack(temp.Path);
        var material = await catalog.RegisterAsync("AL-7075", "Aluminium", BuildProperties());

        await documents.ReviseAsync(material.UnderlyingDocumentId, "{{{not json", "corrupting revision");

        await Assert.ThrowsAsync<MaterialsException>(() => catalog.FindAsync("AL-7075"));
    }

    [Fact]
    public async Task FindAsync_DocumentContentMissingProperties_ThrowsControlledMaterialsException()
    {
        using var temp = new TempDirectory();
        var (catalog, documents, _) = BuildRealStack(temp.Path);
        var material = await catalog.RegisterAsync("AL-7075", "Aluminium", BuildProperties());

        await documents.ReviseAsync(material.UnderlyingDocumentId, """{"MaterialId":"AL-7075","Name":"Aluminium"}""", "drops Properties");

        await Assert.ThrowsAsync<MaterialsException>(() => catalog.FindAsync("AL-7075"));
    }
}
