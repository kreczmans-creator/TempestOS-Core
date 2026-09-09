using Tempest.Core.Bearings;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.Bearings;

// Comparison tests: common properties, missing properties, and the
// distinction between "not recorded" and "not applicable to this family"
// that a cross-family comparison depends on.
public class BearingComparisonTests
{
    private static async Task<IReadOnlyList<IReferenceRecord<BearingDefinition>>> RegisterAsync(BearingCatalog catalog, params (string Id, BearingDefinition Definition)[] entries)
    {
        var bearings = new List<IReferenceRecord<BearingDefinition>>();
        foreach (var (id, definition) in entries)
            bearings.Add(await catalog.RegisterAsync(id, definition));

        return bearings;
    }

    [Fact]
    public void Compare_NullBearings_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BearingComparer.Compare(null!));
    }

    [Fact]
    public void Compare_NoBearings_Throws()
    {
        Assert.Throws<ArgumentException>(() => BearingComparer.Compare([]));
    }

    [Fact]
    public async Task Compare_ReportsOneCellPerBearingPerProperty()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(
            catalog,
            ("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000")),
            ("brg-0002", BearingFixtures.DeepGrooveBall("FX-6205", 25, 52, 15)));

        var comparison = BearingComparer.Compare(bearings);

        Assert.Equal(["brg-0001", "brg-0002"], comparison.RecordIds);
        Assert.Equal(BearingComparisonProperties.All.Count, comparison.Rows.Count);
        Assert.All(comparison.Rows, row => Assert.Equal(2, row.Cells.Count));
    }

    [Fact]
    public async Task Compare_CommonDimensionalProperties_AreRecordedWithACanonicalValueForOrdering()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(
            catalog,
            ("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000", 10, 26, 8)),
            ("brg-0002", BearingFixtures.DeepGrooveBall("FX-6205", 25, 52, 15)));

        var bore = BearingComparer.Compare(bearings).Row(BearingComparisonProperties.Bore)!;

        Assert.All(bore.Cells, cell => Assert.Equal(ReferencePropertyAvailability.Recorded, cell.Availability));
        Assert.Equal(0.010, bore.Cells[0].CanonicalValue!.Value, 12);
        Assert.Equal(0.025, bore.Cells[1].CanonicalValue!.Value, 12);
    }

    [Fact]
    public async Task Compare_LoadRatings_CarryTheirUnitInTheDisplayValue()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(catalog, ("brg-0001", BearingFixtures.DeepGrooveBall()));

        var row = BearingComparer.Compare(bearings).Row(BearingComparisonProperties.BasicDynamicRadial)!;

        Assert.Equal("4.6 kN", row.Cells[0].Display);
        Assert.Equal(4600.0, row.Cells[0].CanonicalValue!.Value, 9);
    }

    [Fact]
    public async Task Compare_APropertyNoBearingRecords_ReportsNotRecordedNotZero()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(
            catalog,
            ("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000")),
            ("brg-0002", BearingFixtures.DeepGrooveBall("FX-6205", 25, 52, 15)));

        var row = BearingComparer.Compare(bearings).Row(BearingComparisonProperties.BasicDynamicAxial)!;

        Assert.All(row.Cells, cell => Assert.Equal(ReferencePropertyAvailability.NotRecorded, cell.Availability));
        Assert.All(row.Cells, cell => Assert.Null(cell.Display));
        Assert.All(row.Cells, cell => Assert.Null(cell.CanonicalValue));
        Assert.False(row.AnyRecorded);
    }

    [Fact]
    public async Task Compare_OneBearingMissingAPropertyAnotherHas_ReportsTheGapWithoutHidingTheValue()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var withMass = BearingFixtures.DeepGrooveBall("FX-6000");
        var withoutMass = BearingFixtures.DeepGrooveBall("FX-6205", 25, 52, 15) with { Mass = null };

        var bearings = await RegisterAsync(catalog, ("brg-0001", withMass), ("brg-0002", withoutMass));
        var row = BearingComparer.Compare(bearings).Row(BearingComparisonProperties.Mass)!;

        Assert.Equal(ReferencePropertyAvailability.Recorded, row.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotRecorded, row.Cells[1].Availability);
        Assert.True(row.AnyRecorded);
    }

    [Fact]
    public async Task Compare_AcrossFamilies_DistinguishesNotApplicableFromNotRecorded()
    {
        // The distinction this capability exists for: a deep-groove ball
        // bearing has no contact angle to record; a tapered roller bearing
        // does, and here records one.
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(
            catalog,
            ("brg-ball", BearingFixtures.DeepGrooveBall("FX-6000")),
            ("brg-taper", BearingFixtures.TaperedRoller("FX-30200")));

        var comparison = BearingComparer.Compare(bearings);
        var contactAngle = comparison.Row(BearingComparisonProperties.ContactAngle)!;

        Assert.Equal(ReferencePropertyAvailability.NotApplicable, contactAngle.Cells[0].Availability);
        Assert.Equal(ReferencePropertyAvailability.Recorded, contactAngle.Cells[1].Availability);
        Assert.False(comparison.IsSingleFamily);
    }

    [Fact]
    public async Task Compare_APlainBearing_ReportsRollingElementPropertiesAsNotApplicable()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(
            catalog,
            ("brg-ball", BearingFixtures.DeepGrooveBall("FX-6000")),
            ("brg-plain", BearingFixtures.PlainBush("FX-PB-1012")));

        var comparison = BearingComparer.Compare(bearings);

        Assert.Equal(ReferencePropertyAvailability.NotApplicable, comparison.Row(BearingComparisonProperties.Rows)!.Cells[1].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, comparison.Row(BearingComparisonProperties.InternalClearanceClass)!.Cells[1].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, comparison.Row(BearingComparisonProperties.RollingElementMaterial)!.Cells[1].Availability);
        Assert.Equal(ReferencePropertyAvailability.NotApplicable, comparison.Row(BearingComparisonProperties.CageMaterial)!.Cells[1].Availability);
    }

    [Fact]
    public async Task Compare_SingleFamilyCandidates_AreReportedAsSuch()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(
            catalog,
            ("brg-0001", BearingFixtures.DeepGrooveBall("FX-6000")),
            ("brg-0002", BearingFixtures.DeepGrooveBall("FX-6205", 25, 52, 15)));

        Assert.True(BearingComparer.Compare(bearings).IsSingleFamily);
    }

    [Fact]
    public async Task Compare_Sealing_ShowsTheManufacturersOwnDesignationAlongsideTheClassification()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var sealed2Rs = BearingFixtures.DeepGrooveBall("FX-6000-2RS") with
        {
            Configuration = new BearingConfiguration(Sealing: new BearingSealingArrangement(BearingSealingType.ContactSeal, "FX-2RS", 2)),
        };

        var bearings = await RegisterAsync(catalog, ("brg-0001", sealed2Rs));
        var row = BearingComparer.Compare(bearings).Row(BearingComparisonProperties.Sealing)!;

        Assert.Equal("ContactSeal (FX-2RS)", row.Cells[0].Display);
    }

    [Fact]
    public async Task Compare_SpeedRatings_AreComparedPerKindNotCollapsed()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(catalog, ("brg-0001", BearingFixtures.DeepGrooveBall()));

        var comparison = BearingComparer.Compare(bearings);

        Assert.Equal("32000 r/min", comparison.Row(BearingComparisonProperties.ReferenceSpeed)!.Cells[0].Display);
        Assert.Equal("20000 r/min", comparison.Row(BearingComparisonProperties.LimitingSpeed)!.Cells[0].Display);
    }

    [Fact]
    public async Task Compare_ValidationState_IsAvailableSoDraftsAreNeverMistakenForReleasedData()
    {
        var catalog = BearingFixtures.BuildCatalog();
        await catalog.RegisterAsync("brg-draft", BearingFixtures.DeepGrooveBall("FX-6000"));
        await catalog.RegisterAsync("brg-released", BearingFixtures.DeepGrooveBall("FX-6205", 25, 52, 15), BearingFixtures.VerifiedProvenance());
        await BearingFixtures.ReleaseAsync(catalog, "brg-released");

        var bearings = new[] { (await catalog.FindAsync("brg-draft"))!, (await catalog.FindAsync("brg-released"))! };
        var row = BearingComparer.Compare(bearings).Row(BearingComparisonProperties.ValidationState)!;

        Assert.Equal("Draft", row.Cells[0].Display);
        Assert.Equal("Released", row.Cells[1].Display);
    }

    [Fact]
    public async Task Compare_PopulatedRows_OmitsRowsNoCandidateRecords()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(catalog, ("brg-0001", BearingFixtures.DeepGrooveBall()));

        var comparison = BearingComparer.Compare(bearings);

        Assert.Contains(comparison.PopulatedRows, row => row.Property == BearingComparisonProperties.Bore);
        Assert.DoesNotContain(comparison.PopulatedRows, row => row.Property == BearingComparisonProperties.FatigueLoadLimit);
    }

    [Fact]
    public async Task Compare_UnknownProperty_HasNoRow()
    {
        var catalog = BearingFixtures.BuildCatalog();
        var bearings = await RegisterAsync(catalog, ("brg-0001", BearingFixtures.DeepGrooveBall()));

        Assert.Null(BearingComparer.Compare(bearings).Row("NoSuchProperty"));
    }
}
