using Tempest.Calc.BuildingServices;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>B9 (<c>bs.insulation</c>) takes the insulation's conductivity λ from a W3 material: the mapping, the record kind and the pin all work on the real template.</summary>
public sealed class InsulationMaterialTests
{
    private static LibraryTestHost Host() => new(calculators: [new InsulationCalculator()]);

    private static CalcDataRecord Wool(double lambda = 0.038) => new(
        Guid.Empty, MaterialLibrary.Kind, "Mineral wool pipe section (maker's datasheet)", 0, "Maker datasheet 17-A", "rev 3", "p. 2, 10 °C mean", "Test Insulations", "ignored", false, CalcDataOrigin.UserEntered,
        [new CalcDataValue("lambda", lambda, "W/(m*K)")],
        default);

    [Fact]
    public void The_Template_Uses_The_Library_Material_Record_Kind_For_Lambda()
    {
        Assert.Equal(MaterialLibrary.Kind, InsulationCalculator.MaterialKind);
        var kind = new InsulationCalculator().Template.RecordKinds!.Single(k => MaterialLibrary.IsMaterial(k.Kind));
        Assert.Equal(["lambda"], kind.Symbols);
    }

    [Fact]
    public async Task A_Material_Fills_Lambda_Pins_The_Revision_The_Sheet_Runs_And_An_Edit_Makes_It_Stale()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (wool, refusal) = await host.Service.Materials.SaveAsync(Wool());
        Assert.Null(refusal);

        var calc = await host.Service.CreateAsync(InsulationCalculator.Id, project, "Steam line 1",
            [CalcInputValue.Quantity("lambda", 1, "W/(m*K)", CalcProvenance.Specification("typed")), CalcInputValue.Quantity("q_max_m", 25, "W/m", CalcProvenance.Specification("typed"))]);
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, wool!.Id);
        Assert.True(applied.Succeeded, applied.Reason);

        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(0.038, inputs["lambda"].Number);
        Assert.Equal("W/(m*K)", inputs["lambda"].Unit);
        Assert.Equal((wool.Id, 1), CalcDataRecord.PinOf(inputs["lambda"].Provenance));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        var run = await host.Service.RunAsync(calc.Id);
        Assert.NotNull(run);
        Assert.Contains(run!.Instance.Outputs, o => o.Symbol == "thickness");

        var (second, again) = await host.Service.Materials.SaveAsync(wool with { Values = [new CalcDataValue("lambda", 0.042, "W/(m*K)")] });
        Assert.Null(again);
        Assert.Equal(2, second!.Revision);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }
}
