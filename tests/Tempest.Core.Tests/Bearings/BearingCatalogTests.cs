using Tempest.Core.Bearings;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Bearings;

// Catalogue tests: registration, identity, lookup, enumeration and
// revision — the reference-data write and read path.
public class BearingCatalogTests
{
    // ----------------------------------------------------------------
    // RegisterAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_ReturnsARecordCarryingTheDefinitionAsGiven()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var definition = BearingFixtures.DeepGrooveBall();

        var bearing = await catalog.RegisterAsync("brg-0001", definition);

        Assert.Equal("brg-0001", bearing.BearingId);
        Assert.Equal(BearingFamily.DeepGrooveBall, bearing.Definition.Family);
        Assert.Equal(BearingFixtures.Millimetres(10), bearing.Definition.Geometry.Bore);
        Assert.Equal(1, bearing.RevisionNumber);
        Assert.NotEqual(Guid.Empty, bearing.UnderlyingDocumentId);
    }

    [Fact]
    public async Task RegisterAsync_StartsEveryRecordInDraft()
    {
        var catalog = BearingFixtures.BuildCatalog();

        var bearing = await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        Assert.Equal(BearingValidationState.Draft, bearing.ValidationState);
        Assert.Null(bearing.SupersededByBearingId);
    }

    [Fact]
    public async Task RegisterAsync_BacksTheRecordWithAnEngineeringDocumentOfTheBearingKind()
    {
        var catalog = BearingFixtures.BuildCatalog(out var documentStore);

        var bearing = await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());
        var document = await documentStore.FindAsync(bearing.UnderlyingDocumentId);

        Assert.NotNull(document);
        Assert.Equal(BearingCatalog.BearingDocumentKind, document!.Kind);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateBearingId_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));

        var exception = await Assert.ThrowsAsync<DuplicateBearingException>(
            () => catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6001")));

        Assert.Equal("brg-0001", exception.BearingId);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateManufacturerAndPartNumber_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));

        var exception = await Assert.ThrowsAsync<DuplicateBearingPartNumberException>(
            () => catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6000")));

        Assert.Equal("brg-0001", exception.ExistingBearingId);
        Assert.Equal("FX-6000", exception.PartNumber);
    }

    [Fact]
    public async Task RegisterAsync_SamePartNumberFromADifferentManufacturer_IsPermitted()
    {
        // Two manufacturers legitimately use the same designation for
        // bearings that are not the same bearing.
        var catalog = BearingFixtures.BuildCatalog();
        var first = BearingFixtures.DeepGrooveBall("6000");
        var second = first with { Identity = new BearingIdentity("Other Fixture Bearings", "6000") };

        await catalog.RegisterAsync("brg-0001", first);
        var registered = await catalog.RegisterAsync("brg-0002", second);

        Assert.Equal("brg-0002", registered.BearingId);
    }

    [Fact]
    public async Task RegisterAsync_PartNumberUniqueness_IgnoresCaseAndWhitespace()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));

        var collidingIdentity = new BearingIdentity(" testfixture bearings ", " fx-6000 ");
        var colliding = BearingFixtures.DeepGrooveBall() with { Identity = collidingIdentity };

        await Assert.ThrowsAsync<DuplicateBearingPartNumberException>(
            () => catalog.RegisterAsync("brg-0002", colliding));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_BlankBearingId_Throws(string bearingId)
    {
        var catalog = BearingFixtures.BuildCatalog();

        await Assert.ThrowsAsync<ArgumentException>(() => catalog.RegisterAsync(bearingId, BearingFixtures.DeepGrooveBall()));
    }

    [Fact]
    public async Task RegisterAsync_NullDefinition_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();

        await Assert.ThrowsAsync<ArgumentNullException>(() => catalog.RegisterAsync("brg-0001", null!));
    }

    [Fact]
    public async Task RegisterAsync_ConcurrentRegistrationsOfTheSameId_OnlyOneSucceeds()
    {
        var catalog = BearingFixtures.BuildCatalog();

        var attempts = Enumerable.Range(0, 8)
            .Select(i => Task.Run(async () =>
            {
                try
                {
                    await catalog.RegisterAsync("brg-race", BearingFixtures.DeepGrooveBall($"FX-{i:0000}"));
                    return true;
                }
                catch (BearingsException)
                {
                    return false;
                }
            }))
            .ToList();

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(succeeded => succeeded));
    }

    [Fact]
    public async Task RegisterAsync_ConcurrentRegistrationsSharingAPartNumber_OnlyOneSucceeds()
    {
        var catalog = BearingFixtures.BuildCatalog();

        var attempts = Enumerable.Range(0, 8)
            .Select(i => Task.Run(async () =>
            {
                try
                {
                    await catalog.RegisterAsync($"brg-{i:0000}", BearingFixtures.DeepGrooveBall("FX-SHARED"));
                    return true;
                }
                catch (BearingsException)
                {
                    return false;
                }
            }))
            .ToList();

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(succeeded => succeeded));
    }

    // ----------------------------------------------------------------
    // FindAsync / FindByPartNumberAsync / ListAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNullRatherThanThrowing()
    {
        var catalog = BearingFixtures.BuildCatalog();

        Assert.Null(await catalog.FindAsync("brg-missing"));
    }

    [Fact]
    public async Task FindAsync_RoundTripsEveryRecordedField()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Construction = new BearingConstruction("steel-100cr6", "steel-100cr6", "polyamide-66", Class: BearingConstructionClass.Standard),
            Lubrication = new BearingLubrication(BearingLubricationType.Grease, "FX-GXN", "30% free space"),
            ApplicationClassification = "Fixture general-purpose",
            Notes = "Fictional fixture record.",
            EffectiveDate = new DateOnly(2026, 3, 1),
            ManufacturerAttributes = new Dictionary<string, string> { ["Fixture note"] = "verbatim" },
        };

        await catalog.RegisterAsync("brg-0001", definition);
        var found = await catalog.FindAsync("brg-0001");

        Assert.NotNull(found);
        var read = found!.Definition;

        Assert.Equal("TestFixture Bearings", read.Identity.Manufacturer);
        Assert.Equal(BearingFamily.DeepGrooveBall, read.Family);
        Assert.Equal(BearingFixtures.Millimetres(26), read.Geometry.OutsideDiameter);
        Assert.Equal(BearingFixtures.Millimetres(0.3), read.Geometry.ChamferMinimum);
        Assert.Equal(4.6, read.LoadRatings!.BasicDynamicRadial!.Value.Value);
        Assert.Equal("kN", read.LoadRatings.BasicDynamicRadial.Value.Unit.Symbol);
        Assert.Equal(BearingValueOrigin.ManufacturerCatalogue, read.LoadRatings.BasicDynamicRadial.Origin);
        Assert.Equal(2, read.SpeedRatings.Count);
        Assert.Equal("Oil lubrication", read.SpeedRatings[0].Rating.Conditions);
        Assert.Equal(BearingSealingType.Open, read.Configuration!.Sealing!.Type);
        Assert.Equal("CN", read.Configuration.InternalClearanceClass);
        Assert.Equal(BearingRowConfiguration.SingleRow, read.Configuration.Rows);
        Assert.Equal("steel-100cr6", read.Construction!.RingMaterialId);
        Assert.Equal(BearingLubricationType.Grease, read.Lubrication!.Type);
        Assert.Equal("FX-GXN", read.Lubrication.LubricantDesignation);
        Assert.Equal(BearingFixtures.Kilograms(0.019), read.Mass);
        Assert.Single(read.Standards);
        Assert.Equal("Fixture general-purpose", read.ApplicationClassification);
        Assert.Equal(new DateOnly(2026, 3, 1), read.EffectiveDate);
        Assert.Equal("verbatim", read.ManufacturerAttributes["Fixture note"]);
        Assert.Equal(BearingExtractionMethod.ManualTranscription, read.Provenance.ExtractionMethod);
        Assert.Equal("Table 1", read.Provenance.SourceLocation);
    }

    [Fact]
    public async Task FindAsync_RoundTripsAnAbsentValueAsAbsentNotAsZero()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var definition = BearingFixtures.DeepGrooveBall() with { Mass = null, LoadRatings = null };

        await catalog.RegisterAsync("brg-0001", definition);
        var found = await catalog.FindAsync("brg-0001");

        Assert.Null(found!.Definition.Mass);
        Assert.Null(found.Definition.LoadRatings);
    }

    [Fact]
    public async Task FindAsync_RoundTripsAQuantityRecordedInANonMetricUnit()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var definition = BearingFixtures.DeepGrooveBall() with
        {
            Geometry = new BearingGeometry(
                Bore: new Quantity<Length>(0.5, LengthUnits.Inch),
                OutsideDiameter: new Quantity<Length>(1.125, LengthUnits.Inch)),
        };

        await catalog.RegisterAsync("brg-imperial", definition);
        var found = await catalog.FindAsync("brg-imperial");

        Assert.Equal("in", found!.Definition.Geometry.Bore!.Value.Unit.Symbol);
        Assert.Equal(0.5, found.Definition.Geometry.Bore!.Value.Value);
    }

    [Fact]
    public async Task FindByPartNumberAsync_ResolvesTheRecordIgnoringCaseAndWhitespace()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));

        var found = await catalog.FindByPartNumberAsync(" testfixture bearings ", " fx-6000 ");

        Assert.Equal("brg-0001", found!.BearingId);
    }

    [Fact]
    public async Task FindByPartNumberAsync_UnknownPartNumber_ReturnsNull()
    {
        var catalog = BearingFixtures.BuildCatalog();

        Assert.Null(await catalog.FindByPartNumberAsync("TestFixture Bearings", "FX-NOPE"));
    }

    [Fact]
    public async Task ListAsync_EmptyCatalogue_ReturnsEmptyNeverNull()
    {
        var catalog = BearingFixtures.BuildCatalog();

        Assert.Empty(await catalog.ListAsync());
    }

    [Fact]
    public async Task ListAsync_ReturnsEveryRecordInDeterministicOrder()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0003", BearingFixtures.DeepGrooveBall("FX-6003"));
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6001"));
        await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6002"));

        var listed = await catalog.ListAsync();

        Assert.Equal(["brg-0001", "brg-0002", "brg-0003"], listed.Select(b => b.BearingId));
    }

    // ----------------------------------------------------------------
    // ReviseAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task ReviseAsync_AdvancesTheRevisionNumberAndReplacesTheDefinition()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall());

        var revised = await catalog.ReviseAsync(
            "brg-0001",
            BearingFixtures.DeepGrooveBall() with
            {
                LoadRatings = new BearingLoadRatings(
                    BasicDynamicRadial: new BearingRatedValue<Tempest.Core.UnitsAndQuantities.Force>(
                        BearingFixtures.Kilonewtons(4.75), BearingValueOrigin.ManufacturerCatalogue)),
            },
            "Catalogue revision 2 restated C.");

        Assert.Equal(2, revised.RevisionNumber);
        Assert.Equal(4.75, revised.Definition.LoadRatings!.BasicDynamicRadial!.Value.Value);
    }

    [Fact]
    public async Task ReviseAsync_LeavesTheValidationStateAlone()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall(provenance: BearingFixtures.SourcedProvenance()));
        await catalog.SetValidationStateAsync("brg-0001", BearingValidationState.Checked, "Checked.");

        var revised = await catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall(), "Correction.");

        Assert.Equal(BearingValidationState.Checked, revised.ValidationState);
    }

    [Fact]
    public async Task ReviseAsync_UnknownBearing_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();

        var exception = await Assert.ThrowsAsync<BearingNotFoundException>(
            () => catalog.ReviseAsync("brg-missing", BearingFixtures.DeepGrooveBall(), null));

        Assert.Equal("brg-missing", exception.BearingId);
    }

    [Fact]
    public async Task ReviseAsync_ChangingThePartNumber_MovesTheIndexEntry()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));

        await catalog.ReviseAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000-B"), "Part number corrected.");

        Assert.Null(await catalog.FindByPartNumberAsync("TestFixture Bearings", "FX-6000"));
        Assert.Equal("brg-0001", (await catalog.FindByPartNumberAsync("TestFixture Bearings", "FX-6000-B"))!.BearingId);
    }

    [Fact]
    public async Task ReviseAsync_ChangingThePartNumberOntoAnotherRecords_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));
        await catalog.RegisterAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6001"));

        await Assert.ThrowsAsync<DuplicateBearingPartNumberException>(
            () => catalog.ReviseAsync("brg-0002", BearingFixtures.DeepGrooveBall("FX-6000"), "Collide."));
    }

    [Fact]
    public async Task ReviseAsync_KeepingTheSamePartNumber_IsNotTreatedAsADuplicate()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000"));

        var revised = await catalog.ReviseAsync(
            "brg-0001",
            BearingFixtures.DeepGrooveBall("FX-6000", widthMillimetres: 9.0),
            "Width corrected.");

        Assert.Equal(BearingFixtures.Millimetres(9.0), revised.Definition.Geometry.Width);
    }
}
