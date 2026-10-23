using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Bearings;
using Tempest.Core.EngineeringData;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Fasteners;
using Tempest.Core.Tests.Materials;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Calc.Tests.Library;

/// <summary>ADR-0167 §3: worksheet inputs filled from released reference records, with the record's pin as the source.</summary>
public sealed class ReferenceFillTests
{
    private sealed record Catalogues(MaterialCatalog Materials, FastenerCatalog Fasteners, BearingCatalog Bearings, CalcReferenceFill Fill);

    private static Catalogues New()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var documents = new EngineeringDocumentStore(store, new CurrentPrincipalAccessor());
        var materials = new MaterialCatalog(documents, store);
        var fasteners = new FastenerCatalog(documents, store);
        var bearings = new BearingCatalog(documents, store);
        return new Catalogues(materials, fasteners, bearings, new CalcReferenceFill(materials, fasteners, bearings));
    }

    private static async Task ReleasedSteelAsync(MaterialCatalog materials, string id = "mat-steel")
    {
        await materials.RegisterAsync(id, MaterialFixtures.Steel(), MaterialFixtures.Verified());
        await MaterialFixtures.ReleaseAsync(materials, id);
    }

    [Fact]
    public async Task A_Released_Material_Fills_Yield_And_Modulus_With_A_Pinned_Source()
    {
        var c = New();
        await ReleasedSteelAsync(c.Materials);
        var template = new MemberChecksCalculator().Template;

        Assert.Equal(["Materials"], CalcReferenceFill.LibrariesOf(template));
        var option = Assert.Single(await c.Fill.ListReleasedAsync("Materials"));
        Assert.Equal("FX-STEEL-1 — Fixture Structural Steel", option.Label);
        Assert.StartsWith("Materials/mat-steel@", option.Pin, StringComparison.Ordinal);

        var fill = await c.Fill.FillAsync(template, "Materials", "mat-steel");
        Assert.True(fill.Succeeded, string.Join(" ", fill.Problems.Select(p => p.Message)));

        var fy = fill.Values.Single(v => v.Symbol == "f_y");
        Assert.Equal(300, fy.Number!.Value, 9);
        Assert.Equal("MPa", fy.Unit);
        Assert.Equal(CalcProvenanceKind.Reference, fy.Provenance!.Kind);
        Assert.Equal(option.Pin, fy.Provenance.Reference);
        Assert.True(fy.Provenance.IsSufficientToIssue);

        var e = fill.Values.Single(v => v.Symbol == "E");
        Assert.Equal(200, e.Number!.Value, 9);
        Assert.Equal("GPa", e.Unit);
    }

    [Fact]
    public async Task An_Unreleased_Record_Is_Neither_Offered_Nor_Read()
    {
        var c = New();
        await c.Materials.RegisterAsync("mat-draft", MaterialFixtures.Steel("FX-DRAFT"), MaterialFixtures.Sourced());

        Assert.Empty(await c.Fill.ListReleasedAsync("Materials"));
        var fill = await c.Fill.FillAsync(new MemberChecksCalculator().Template, "Materials", "mat-draft");
        Assert.Empty(fill.Values);
        Assert.Contains(fill.Problems, p => p.Message.Contains("not Released", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Missing_Property_Is_Reported_And_The_Rest_Still_Fills()
    {
        var c = New();
        var steel = MaterialFixtures.Steel("FX-NO-E");
        var withoutModulus = steel with { Properties = steel.Properties.Where(p => p.Key != MaterialPropertyNames.YoungsModulus).ToDictionary(p => p.Key, p => p.Value) };
        await c.Materials.RegisterAsync("mat-no-e", withoutModulus, MaterialFixtures.Verified());
        await MaterialFixtures.ReleaseAsync(c.Materials, "mat-no-e");

        var fill = await c.Fill.FillAsync(new MemberChecksCalculator().Template, "Materials", "mat-no-e");
        Assert.False(fill.Succeeded);
        Assert.Single(fill.Values, v => v.Symbol == "f_y");
        Assert.Contains(fill.Problems, p => p.RuleId == "reference.E" && p.Message.Contains("records no YoungsModulus", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Slots_Fill_Hub_And_Shaft_From_Different_Records()
    {
        var c = New();
        await ReleasedSteelAsync(c.Materials, "mat-hub");
        var shaft = MaterialFixtures.Steel("FX-SHAFT");
        await c.Materials.RegisterAsync("mat-shaft", shaft with { Properties = new Dictionary<string, ReferenceQuantityValue>(shaft.Properties) { [MaterialPropertyNames.YieldStrength] = MaterialFixtures.Property(MaterialFixtures.Megapascals(450)) } }, MaterialFixtures.Verified());
        await MaterialFixtures.ReleaseAsync(c.Materials, "mat-shaft");
        var template = new PressFitCalculator().Template;

        Assert.Equal(["Materials:hub", "Materials:shaft"], CalcReferenceFill.LibrariesOf(template).Order(StringComparer.Ordinal));
        Assert.Equal("Hub material record", CalcReferenceFill.PickerLabel("Materials:hub"));

        var hub = await c.Fill.FillAsync(template, "Materials:hub", "mat-hub");
        var shaftFill = await c.Fill.FillAsync(template, "Materials:shaft", "mat-shaft");
        Assert.Equal(["E_h", "S_y_h"], hub.Values.Select(v => v.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(["E_s", "S_y_s"], shaftFill.Values.Select(v => v.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(300, hub.Values.Single(v => v.Symbol == "S_y_h").Number!.Value, 9);
        Assert.Equal(450, shaftFill.Values.Single(v => v.Symbol == "S_y_s").Number!.Value, 9);
    }

    [Fact]
    public async Task A_Released_Fastener_Fills_The_Bolt_Proof_Strength_And_The_Sheet_Shows_The_Pin()
    {
        var c = New();
        await c.Fasteners.RegisterAsync("fst-bolt", FastenerFixtures.HexBolt(), FastenerFixtures.VerifiedProvenance());
        await FastenerFixtures.ReleaseAsync(c.Fasteners, "fst-bolt");
        var calculator = new BoltedJointCalculator();

        var fill = await c.Fill.FillAsync(calculator.Template, "Fasteners", "fst-bolt");
        Assert.True(fill.Succeeded, string.Join(" ", fill.Problems.Select(p => p.Message)));
        var rp = Assert.Single(fill.Values);
        Assert.Equal("R_p02", rp.Symbol);
        Assert.Equal(600, rp.Number!.Value, 9);

        var inputs = CalcTemplateHasher.DefaultInputs(calculator.Template);
        inputs.Set(rp);
        var result = CalcRunner.Run(calculator, inputs);
        Assert.False(result.IsBlocked);
        var sheet = GoldenFilesText(calculator, inputs, result);
        Assert.Contains("R_p02 | Bolt 0.2 % proof (yield) strength | 600 | MPa | reference: Fasteners/fst-bolt@", sheet, StringComparison.Ordinal);
    }

    private static string GoldenFilesText(ICalcCalculator calculator, CalcInputSet inputs, CalcRunResult result)
    {
        var at = DateTimeOffset.UnixEpoch;
        var header = new CalcSheetHeader("P", "C", calculator.Template.Title, "A", CalcSheetStatus.Draft, "a", null, at, null, null);
        var footer = new CalcSheetFooter(calculator.Template.Id, "h", "e", 1, 1, at);
        return CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(calculator.Template, inputs, result, header, footer));
    }
}
