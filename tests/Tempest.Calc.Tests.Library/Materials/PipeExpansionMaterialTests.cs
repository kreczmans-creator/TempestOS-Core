using Tempest.Calc.BuildingServices;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>B8 (<c>bs.pipe-expansion</c>) takes E and α from a W3 material: the mapping, the record kind, the unit conversion of α and the pin all work on the real template.</summary>
public sealed class PipeExpansionMaterialTests
{
    private static LibraryTestHost Host() => new(calculators: [new PipeExpansionCalculator()]);

    private static CalcDataRecord Steel(double alphaPerK = 11.5e-6) => new(
        Guid.Empty, MaterialLibrary.Kind, "P235 pipe (my mill certificate)", 0, "Mill certificate 4712", "rev A", "p. 2, Table 3", "Test Steelworks", "ignored", false, CalcDataOrigin.UserEntered,
        [new CalcDataValue("E", 205, "GPa"), new CalcDataValue("alpha", alphaPerK, "1/K")],
        default);

    [Fact]
    public void The_Template_Uses_The_Library_Material_Record_Kind()
    {
        Assert.Equal(MaterialLibrary.Kind, PipeExpansionCalculator.MaterialKind);
        var kind = new PipeExpansionCalculator().Template.RecordKinds!.Single(k => MaterialLibrary.IsMaterial(k.Kind));
        Assert.Equal(["E", "alpha"], kind.Symbols);
    }

    [Fact]
    public async Task A_Material_Fills_E_And_Alpha_In_The_Units_Of_The_Inputs_Pins_The_Revision_And_The_Sheet_Runs()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (steel, refusal) = await host.Service.Materials.SaveAsync(Steel());
        Assert.Null(refusal);

        var calc = await host.Service.CreateAsync(PipeExpansionCalculator.Id, project, "LTHW main",
            [CalcInputValue.Quantity("Sa", 100, "MPa", CalcProvenance.Specification("Design basis")), CalcInputValue.Quantity("E", 1, "GPa", CalcProvenance.Specification("typed"))]);
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, steel!.Id);
        Assert.True(applied.Succeeded, applied.Reason);

        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(205, inputs["E"].Number);
        Assert.Equal("GPa", inputs["E"].Unit);
        Assert.Equal(11.5, inputs["alpha"].Number!.Value, 9);
        Assert.Equal("um/(m*K)", inputs["alpha"].Unit);
        Assert.Equal(100, inputs["Sa"].Number);
        Assert.Equal((steel.Id, 1), CalcDataRecord.PinOf(inputs["alpha"].Provenance));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        var run = await host.Service.RunAsync(calc.Id);
        Assert.NotNull(run);
        Assert.Contains(run!.Instance.Outputs, o => o.Symbol == "dL");

        // A later revision of the material makes the calculation stale; the inputs stay editable.
        var (second, again) = await host.Service.Materials.SaveAsync(steel with { Values = [new CalcDataValue("E", 205, "GPa"), new CalcDataValue("alpha", 12e-6, "1/K")] });
        Assert.Null(again);
        Assert.Equal(2, second!.Revision);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }

    [Fact]
    public async Task A_Generic_Seed_Fills_E_And_Alpha()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        await host.Service.Materials.EnsureSeededAsync();
        var calc = await host.Service.CreateAsync(PipeExpansionCalculator.Id, project, "LTHW main 2", []);
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, MaterialSeeds.CarbonSteel);
        Assert.True(applied.Succeeded, applied.Reason);
        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(210, inputs["E"].Number);
        Assert.Equal(12, inputs["alpha"].Number!.Value, 9);
    }
}
