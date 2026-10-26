using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>S7 (<c>struct.ltb-interaction</c>) takes E, G and f_y from a W3 material: the mapping, the record kind and the pin all work on the real template.</summary>
public sealed class LtbInteractionMaterialTests
{
    private static LibraryTestHost Host() => new(calculators: [new LtbInteractionCalculator()]);

    private static CalcDataRecord Steel(double fy = 345) => new(
        Guid.Empty, MaterialLibrary.Kind, "S355 (my mill certificate)", 0, "Mill certificate 4711", "rev B", "p. 4, Table 2", "Test Steelworks", "ignored", false, CalcDataOrigin.UserEntered,
        [new CalcDataValue("E", 210, "GPa"), new CalcDataValue("G", 81, "GPa"), new CalcDataValue("f_y", fy, "MPa")],
        default);

    [Fact]
    public void The_Template_Uses_The_Library_Material_Record_Kind()
    {
        Assert.Equal(MaterialLibrary.Kind, LtbInteractionCalculator.MaterialKind);
        var kind = new LtbInteractionCalculator().Template.RecordKinds!.Single(k => MaterialLibrary.IsMaterial(k.Kind));
        Assert.Equal(["E", "G", "f_y"], kind.Symbols);
    }

    [Fact]
    public async Task A_Material_Fills_E_G_And_Fy_In_Their_Own_Units_Pins_The_Revision_And_The_Sheet_Runs()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (steel, refusal) = await host.Service.Materials.SaveAsync(Steel());
        Assert.Null(refusal);

        var calc = await host.Service.CreateAsync(LtbInteractionCalculator.Id, project, "Beam-column 1",
            [CalcInputValue.Quantity("E", 1, "GPa", CalcProvenance.Specification("typed")), CalcInputValue.Quantity("M_y_Ed", 100, "kN*m", CalcProvenance.Specification("typed"))]);
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, steel!.Id);
        Assert.True(applied.Succeeded, applied.Reason);

        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(210, inputs["E"].Number);
        Assert.Equal("GPa", inputs["E"].Unit);
        Assert.Equal(81, inputs["G"].Number);
        Assert.Equal(345, inputs["f_y"].Number);
        Assert.Equal("MPa", inputs["f_y"].Unit);
        Assert.Equal((steel.Id, 1), CalcDataRecord.PinOf(inputs["f_y"].Provenance));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        var run = await host.Service.RunAsync(calc.Id);
        Assert.NotNull(run);
        Assert.Contains(run!.Instance.Outputs, o => o.Symbol == "Mb_Rd");

        // A later revision of the material makes the calculation stale; the inputs stay editable.
        var (second, again) = await host.Service.Materials.SaveAsync(steel with { Values = [new CalcDataValue("E", 210, "GPa"), new CalcDataValue("G", 81, "GPa"), new CalcDataValue("f_y", 355, "MPa")] });
        Assert.Null(again);
        Assert.Equal(2, second!.Revision);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }

    [Fact]
    public async Task A_Generic_Seed_Fills_E_And_G_And_Leaves_Fy_To_The_Engineer()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        await host.Service.Materials.EnsureSeededAsync();
        var calc = await host.Service.CreateAsync(LtbInteractionCalculator.Id, project, "Beam-column 2", []);
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, MaterialSeeds.CarbonSteel);
        Assert.True(applied.Succeeded, applied.Reason);
        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(210, inputs["E"].Number);
        Assert.Equal(81, inputs["G"].Number);
        Assert.False(inputs.ContainsKey("f_y"));
    }
}
