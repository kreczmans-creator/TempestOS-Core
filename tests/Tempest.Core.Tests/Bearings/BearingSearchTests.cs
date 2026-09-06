using Tempest.Core.Bearings;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Bearings;

// Search and filter tests: exact match, partial match, dimensional ranges
// across units, combined criteria, and the rule that an unrecorded value
// never satisfies a range.
public class BearingSearchTests
{
    private static async Task<BearingCatalog> BuildPopulatedCatalogAsync()
    {
        var catalog = BearingFixtures.BuildCatalog();

        await catalog.RegisterAsync("brg-small", BearingFixtures.DeepGrooveBall("FX-6000", 10, 26, 8));
        await catalog.RegisterAsync("brg-medium", BearingFixtures.DeepGrooveBall("FX-6205", 25, 52, 15));
        await catalog.RegisterAsync("brg-large", BearingFixtures.DeepGrooveBall("FX-6310", 50, 110, 27));
        await catalog.RegisterAsync("brg-taper", BearingFixtures.TaperedRoller("FX-30200"));
        await catalog.RegisterAsync("brg-plain", BearingFixtures.PlainBush("FX-PB-1012"));

        return catalog;
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEverythingInListOrder()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery());

        Assert.Equal(["brg-large", "brg-medium", "brg-plain", "brg-small", "brg-taper"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_NullQuery_Throws()
    {
        var catalog = BearingFixtures.BuildCatalog();

        await Assert.ThrowsAsync<ArgumentNullException>(() => catalog.SearchAsync(null!));
    }

    [Fact]
    public async Task SearchAsync_ByManufacturer_MatchesExactlyIgnoringCase()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery { Manufacturer = "testfixture bearings" });

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task SearchAsync_ByManufacturer_ExcludesOtherManufacturers()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery { Manufacturer = "Other Fixture Bearings" });

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ByPartNumberFragment_MatchesPartially()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery { PartNumberContains = "62" });

        Assert.Equal(["brg-medium"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByDesignationFragment_MatchesPartially()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery { DesignationContains = "fx-63" });

        Assert.Equal(["brg-large"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_BySeries_MatchesExactly()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery { Series = "FX-302" });

        Assert.Equal(["brg-taper"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_BySeries_DoesNotMatchARecordWithNoSeries()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery { Series = "FX-60" });

        Assert.DoesNotContain("brg-plain", results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByFamily_FiltersToTheNamedFamilies()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            Families = [BearingFamily.TaperedRoller, BearingFamily.Plain],
        });

        Assert.Equal(["brg-plain", "brg-taper"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByBoreRange_FiltersInclusively()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            BoreMinimum = BearingFixtures.Millimetres(10),
            BoreMaximum = BearingFixtures.Millimetres(25),
        });

        Assert.Equal(["brg-medium", "brg-plain", "brg-small", "brg-taper"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByBoreRangeExpressedInInches_MatchesRecordsHeldInMillimetres()
    {
        // Range comparison converts to the dimension's own base unit, so
        // the unit a source quoted never changes what a query finds.
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            BoreMinimum = new Quantity<Length>(1.5, LengthUnits.Inch),
        });

        Assert.Equal(["brg-large"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByOutsideDiameterRange_Filters()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            OutsideDiameterMinimum = BearingFixtures.Millimetres(50),
        });

        Assert.Equal(["brg-large", "brg-medium"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByWidthRange_Filters()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            WidthMinimum = BearingFixtures.Millimetres(15),
        });

        Assert.Equal(["brg-large", "brg-medium"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByLoadRating_Filters()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            BasicDynamicRadialMinimum = BearingFixtures.Kilonewtons(10),
        });

        Assert.Equal(["brg-taper"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByStaticLoadRating_Filters()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            BasicStaticRadialMinimum = BearingFixtures.Kilonewtons(5),
        });

        Assert.Equal(["brg-taper"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByLoadRating_DoesNotMatchARecordThatHasNone()
    {
        // brg-plain records no load rating at all. An unrecorded rating is
        // never treated as satisfying a minimum, and never read as zero.
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            BasicDynamicRadialMinimum = BearingFixtures.Kilonewtons(0.001),
        });

        Assert.DoesNotContain("brg-plain", results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByMassMaximum_Filters()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            MassMaximum = BearingFixtures.Kilograms(0.005),
        });

        Assert.Equal(["brg-plain"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_BySpeed_ConsidersEveryKindWhenNoKindIsNamed()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            SpeedMinimum = BearingFixtures.RevolutionsPerMinute(25000),
        });

        Assert.Equal(["brg-large", "brg-medium", "brg-small"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_BySpeedOfANamedKind_IgnoresOtherKinds()
    {
        // The reference speed is 32000 and the limiting speed 20000: a
        // limiting-speed filter of 25000 must match neither, which is
        // exactly the distinction a single "max RPM" field would destroy.
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            SpeedMinimum = BearingFixtures.RevolutionsPerMinute(25000),
            SpeedRatingKind = BearingSpeedRatingKind.LimitingSpeed,
        });

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_BySealing_Filters()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-open", BearingFixtures.DeepGrooveBall("FX-6000"));
        await catalog.RegisterAsync("brg-sealed", BearingFixtures.DeepGrooveBall("FX-6000-2RS") with
        {
            Configuration = new BearingConfiguration(Sealing: new BearingSealingArrangement(BearingSealingType.ContactSeal, "FX-2RS")),
        });

        var results = await catalog.SearchAsync(new BearingQuery { Sealing = BearingSealingType.ContactSeal });

        Assert.Equal(["brg-sealed"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByClearanceClass_Filters()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery { InternalClearanceClass = "cn" });

        Assert.Equal(["brg-large", "brg-medium", "brg-small"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByPrecisionClass_Filters()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-standard", BearingFixtures.DeepGrooveBall("FX-6000"));
        await catalog.RegisterAsync("brg-precision", BearingFixtures.DeepGrooveBall("FX-6000-P5") with
        {
            Configuration = new BearingConfiguration(PrecisionClass: "P5", PrecisionStandard: "Fixture tolerance standard"),
        });

        var results = await catalog.SearchAsync(new BearingQuery { PrecisionClass = "P5" });

        Assert.Equal(["brg-precision"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByMaterialReference_Filters()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-steel", BearingFixtures.DeepGrooveBall("FX-6000") with
        {
            Construction = new BearingConstruction(RingMaterialId: "steel-100cr6"),
        });
        await catalog.RegisterAsync("brg-hybrid", BearingFixtures.DeepGrooveBall("FX-6000-HC") with
        {
            Construction = new BearingConstruction(
                RingMaterialId: "steel-100cr6",
                RollingElementMaterialId: "ceramic-si3n4",
                Class: BearingConstructionClass.Hybrid),
        });

        var byElement = await catalog.SearchAsync(new BearingQuery { ReferencesMaterialId = "ceramic-si3n4" });
        var byClass = await catalog.SearchAsync(new BearingQuery { ConstructionClass = BearingConstructionClass.Hybrid });

        Assert.Equal(["brg-hybrid"], byElement.Select(b => b.Id));
        Assert.Equal(["brg-hybrid"], byClass.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_ByValidationState_SeparatesReleasedDataFromDrafts()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-draft", BearingFixtures.DeepGrooveBall("FX-6000"));
        await catalog.RegisterAsync("brg-released", BearingFixtures.DeepGrooveBall("FX-6205"), BearingFixtures.VerifiedProvenance());
        await BearingFixtures.ReleaseAsync(catalog, "brg-released");

        var released = await catalog.SearchAsync(new BearingQuery { ValidationStates = [ReferenceValidationState.Released] });

        Assert.Equal(["brg-released"], released.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_CombinedCriteria_AreAnded()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            Families = [BearingFamily.DeepGrooveBall],
            BoreMinimum = BearingFixtures.Millimetres(20),
            OutsideDiameterMaximum = BearingFixtures.Millimetres(60),
            ValidationStates = [ReferenceValidationState.Draft],
        });

        Assert.Equal(["brg-medium"], results.Select(b => b.Id));
    }

    [Fact]
    public async Task SearchAsync_CombinedCriteriaThatNothingSatisfies_ReturnsEmpty()
    {
        var catalog = await BuildPopulatedCatalogAsync();

        var results = await catalog.SearchAsync(new BearingQuery
        {
            Families = [BearingFamily.Plain],
            BasicDynamicRadialMinimum = BearingFixtures.Kilonewtons(1),
        });

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_IsDeterministic()
    {
        var catalog = await BuildPopulatedCatalogAsync();
        var query = new BearingQuery { Families = [BearingFamily.DeepGrooveBall] };

        var first = await catalog.SearchAsync(query);
        var second = await catalog.SearchAsync(query);

        Assert.Equal(first.Select(b => b.Id), second.Select(b => b.Id));
    }
}
