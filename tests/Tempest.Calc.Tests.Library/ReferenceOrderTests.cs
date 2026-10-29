using Tempest.Calc.Library;
using Tempest.Core.Bearings;
using Tempest.Core.EngineeringData;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Tests.Fasteners;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// Owner, 2026-10-29: a record picker lists thread sizes numerically (M3 before M27, M30 after it), coarse pitch before
/// fine, then the property class; each fastener record carries its size and grade apart, and its label no longer
/// repeats the class ("M3 x 0.5 - 8.8 — 8.8").
/// </summary>
public sealed class ReferenceOrderTests
{
    private static string Join(IEnumerable<string?> items) => string.Join("|", items.Select(i => i ?? "(none)"));

    [Fact]
    public void Names_With_Numbers_Sort_Numerically_Not_As_Text()
    {
        string[] names = ["M30", "M27", "M3", "S1100", "S235", "6205-2RS", "6204"];
        Assert.Equal(["6204", "6205-2RS", "M3", "M27", "M30", "S235", "S1100"], names.Order(CalcReferenceOrder.Natural).ToArray());

        // String order, the old behaviour, put M27 above M3.
        Assert.Equal(["M27", "M3", "M30"], new[] { "M30", "M27", "M3" }.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Thread_Sizes_Sort_By_Diameter_Then_Coarse_Pitch_Before_Fine()
    {
        string[] sizes = ["M10 x 1.25", "M30 x 3.5", "M3 x 0.5", "M10 x 1.5", "M10", "M27 x 3", "M10 x 1"];
        Assert.Equal(["M3 x 0.5", "M10", "M10 x 1.5", "M10 x 1.25", "M10 x 1", "M27 x 3", "M30 x 3.5"], sizes.Order(CalcReferenceOrder.ThreadSize).ToArray());
    }

    [Fact]
    public void Property_Classes_Sort_Steel_Then_Stainless_With_No_Class_Last()
    {
        string?[] grades = ["A4-80", "10.9", null, "4.6", "A2-70", "12.9", "8.8", "A4-70", "5.6"];
        Assert.Equal("4.6|5.6|8.8|10.9|12.9|A2-70|A4-70|A4-80|(none)", Join(grades.Order(CalcReferenceOrder.Grade)));
    }

    [Theory]
    [InlineData("M3 x 0.5 - 8.8", "M3 x 0.5", "8.8", "M3 x 0.5", "8.8")]
    [InlineData("M12 x 1.75 - A4-80", null, null, "M12 x 1.75", "A4-80")]
    [InlineData("M12x1.25 – 10.9", null, null, "M12 x 1.25", "10.9")]
    [InlineData("M5 x 0.8", "M5 x 0.8", null, "M5 x 0.8", null)]
    [InlineData("M16", null, null, "M16", null)]
    [InlineData("FX-BOLT-1", "FX10", "FX-A", null, null)]
    [InlineData("1/4-20 UNC - Grade 5", null, null, null, null)]
    public void A_Fastener_Splits_Into_Its_Size_And_Grade(string designation, string? thread, string? propertyClass, string? size, string? grade)
    {
        Assert.Equal((size, grade), CalcReferenceOrder.SplitFastener(designation, thread, propertyClass));
    }

    [Fact]
    public void A_Name_Too_Long_For_A_Size_Is_Not_Read()
    {
        Assert.Equal((null, null), CalcReferenceOrder.SplitFastener("M12 x 1.75 - " + new string('8', 400), null, null));
        Assert.False(CalcReferenceOrder.TryParseThread("M" + new string('1', 400), out _, out _));
    }

    [Theory]
    [InlineData("M3 x 0.5 - 8.8", "8.8", "M3 x 0.5 - 8.8")]
    [InlineData("M3 x 0.5 - A4-80", "A4-80", "M3 x 0.5 - A4-80")]
    [InlineData("M5 x 0.8", "Bolt", "M5 x 0.8 — Bolt")]
    [InlineData("S355", "Structural steel", "S355 — Structural steel")]
    [InlineData("S355", "S355", "S355")]
    [InlineData("ABC10.9", "10.9", "ABC10.9 — 10.9")]
    public void A_Label_Does_Not_Repeat_A_Name_The_Designation_Ends_With(string designation, string name, string label)
    {
        Assert.Equal(label, CalcReferenceOrder.Label(designation, name));
    }

    [Fact]
    public async Task The_Fastener_Records_Are_Listed_By_Size_Then_Grade_With_Their_Size_And_Grade()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var documents = new EngineeringDocumentStore(store, new CurrentPrincipalAccessor());
        var fasteners = new FastenerCatalog(documents, store);
        var fill = new CalcReferenceFill(new MaterialCatalog(documents, store), fasteners, new BearingCatalog(documents, store));

        // The shipped records of M3, M5 (with its thread-only record), M27 and M30, registered in an order that is neither.
        var wanted = new[] { "fst-m30-", "fst-m3-", "fst-m27-", "fst-m5-" };
        var seeds = FastenerSeed.Instance.Records.Where(r => wanted.Any(w => r.RecordId.StartsWith(w, StringComparison.Ordinal))).Reverse().ToList();

        foreach (var seed in seeds)
        {
            await fasteners.RegisterAsync(seed.RecordId, seed.Definition, FastenerFixtures.VerifiedProvenance());
            await FastenerFixtures.ReleaseAsync(fasteners, seed.RecordId);
        }

        var options = await fill.ListReleasedAsync("Fasteners");
        Assert.Equal(seeds.Count, options.Count);
        Assert.Equal("M3 x 0.5|M5 x 0.8|M27 x 3|M30 x 3.5", Join(options.Select(o => o.Size).Distinct()));
        Assert.Equal("4.6|5.6|8.8|10.9|12.9|A2-70|A4-70|A4-80", Join(options.Where(o => o.Size == "M3 x 0.5").Select(o => o.Grade)));
        Assert.Equal("4.6|5.6|8.8|10.9|12.9|A2-70|A4-70|A4-80|(none)", Join(options.Where(o => o.Size == "M5 x 0.8").Select(o => o.Grade)));
        Assert.Equal("4.6|5.6|8.8|10.9|12.9", Join(options.Where(o => o.Size == "M30 x 3.5").Select(o => o.Grade)));

        var m3 = options.Single(o => o.RecordId == "fst-m3-8-8");
        Assert.Equal("M3 x 0.5 - 8.8", m3.Label);
        Assert.DoesNotContain(options, o => o.Label.EndsWith($"— {o.Grade}", StringComparison.Ordinal));
        Assert.Equal("M5 x 0.8 — Bolt", options.Single(o => o.RecordId == "fst-m5-coarse").Label);
    }
}
