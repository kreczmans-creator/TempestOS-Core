using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>S8 (<c>struct.lifting</c>) takes f_y and f_u from a W3 material: the mapping, the record kind and the pin work on the real template (the feature flag is on in this test host only).</summary>
public sealed class LiftingLugMaterialTests
{
    private static LibraryTestHost Host() => new(calculators: [new LiftingLugCalculator()], features: new CalcFeatureFlags([CalcFeatures.Lifting]));

    private static CalcDataRecord Plate(double fy = 345, double fu = 470) => new(
        Guid.Empty, MaterialLibrary.Kind, "S355 plate 25 mm (my mill certificate)", 0, "Mill certificate 4711", "rev B", "p. 4, Table 2", "Test Steelworks", "ignored", false, CalcDataOrigin.UserEntered,
        [new CalcDataValue("f_y", fy, "MPa"), new CalcDataValue("f_u", fu, "MPa")],
        default);

    [Fact]
    public void The_Template_Uses_The_Library_Material_Record_Kind_For_Fy_And_Fu()
    {
        Assert.Equal(MaterialLibrary.Kind, LiftingLugCalculator.MaterialKind);
        var kind = new LiftingLugCalculator().Template.RecordKinds!.Single(k => MaterialLibrary.IsMaterial(k.Kind));
        Assert.Equal(["f_y", "f_u"], kind.Symbols);
    }

    [Fact]
    public async Task A_Material_Fills_Fy_And_Fu_Pins_The_Revision_And_A_Later_Revision_Makes_The_Calculation_Stale()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (plate, refusal) = await host.Service.Materials.SaveAsync(Plate());
        Assert.Null(refusal);

        var calc = await host.Service.CreateAsync(LiftingLugCalculator.Id, project, "Lug 1", new LiftingLugCalculator().Template.TestCases[0].Inputs.Select(v => v.Symbol is "f_y" or "f_u" ? v with { Number = 1 } : v).ToList());
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, plate!.Id);
        Assert.True(applied.Succeeded, applied.Reason);

        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(345, inputs["f_y"].Number);
        Assert.Equal(470, inputs["f_u"].Number);
        Assert.Equal("MPa", inputs["f_y"].Unit);
        Assert.Equal((plate.Id, 1), CalcDataRecord.PinOf(inputs["f_y"].Provenance));
        Assert.Equal((plate.Id, 1), CalcDataRecord.PinOf(inputs["f_u"].Provenance));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        var run = await host.Service.RunAsync(calc.Id);
        Assert.NotNull(run);
        Assert.Contains(run!.Instance.Outputs, o => o.Symbol == "capacity");

        var (second, again) = await host.Service.Materials.SaveAsync(plate with { Values = [new CalcDataValue("f_y", 355, "MPa"), new CalcDataValue("f_u", 490, "MPa")] });
        Assert.Null(again);
        Assert.Equal(2, second!.Revision);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }

    [Fact]
    public async Task The_Governance_Records_Are_Reference_Records_Of_Their_Own_Kinds_And_Are_Not_Defaulted()
    {
        var host = Host();
        // No design-factor, allowable-stress or limit record is shipped or seeded (only the indicative generic materials exist).
        var records = await host.Service.RecordsForAsync(LiftingLugCalculator.Id);
        Assert.DoesNotContain(records, r => r.Kind.StartsWith("lifting.", StringComparison.Ordinal));

        var template = new LiftingLugCalculator().Template;
        Assert.Contains(template.RecordKinds!, k => k.Kind == "lifting.design-factors" && k.Symbols.Contains("Nd"));
        Assert.Contains(template.RecordKinds!, k => k.Kind == "lifting.allowable-stress" && k.Symbols.Contains("n_y") && k.Symbols.Contains("n_u") && k.Symbols.Contains("k_b"));
        Assert.Contains(template.RecordKinds!, k => k.Kind == "lifting.limits" && k.Symbols.Contains("dish_max") && k.Symbols.Contains("k_proof"));

        // A new sheet opens with none of them filled: nothing from either standard is a default.
        var opened = CalcTemplateHasher.NewSheetInputs(template);
        foreach (var symbol in new[] { "Nd", "k_svc", "n_y", "n_u", "k_b", "dish_max", "k_proof", "design_category", "service_class" })
            Assert.Null(opened.Find(symbol));
    }
}
