using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>S9 (<c>struct.plate-bending</c>) takes E, ν, ρ and f_y from a W3 material: the mapping, the record kind and the pin all work on the real template.</summary>
public sealed class PlateBendingMaterialTests
{
    private static LibraryTestHost Host() => new(calculators: [new PlateBendingCalculator()]);

    private static CalcDataRecord Steel() => new(
        Guid.Empty, MaterialLibrary.Kind, "S275 (my mill certificate)", 0, "Mill certificate 4711", "rev B", "p. 4, Table 2", "Test Steelworks", "ignored", false, CalcDataOrigin.UserEntered,
        [new CalcDataValue("E", 205, "GPa"), new CalcDataValue("nu", 0.29, string.Empty), new CalcDataValue("rho", 7800, "kg/m^3"), new CalcDataValue("f_y", 280, "MPa")],
        default);

    [Fact]
    public void The_Template_Uses_The_Library_Material_Record_Kind()
    {
        Assert.Equal(MaterialLibrary.Kind, PlateBendingCalculator.MaterialKind);
        var kind = new PlateBendingCalculator().Template.RecordKinds!.Single(k => MaterialLibrary.IsMaterial(k.Kind));
        Assert.Equal(["E", "nu", "rho", "f_y"], kind.Symbols);
    }

    [Fact]
    public async Task A_Material_Fills_The_Four_Properties_Pins_The_Revision_And_The_Sheet_Runs()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (steel, refusal) = await host.Service.Materials.SaveAsync(Steel());
        Assert.Null(refusal);

        var calc = await host.Service.CreateAsync(PlateBendingCalculator.Id, project, "Lid 1", [CalcInputValue.Quantity("E", 1, "GPa", CalcProvenance.Specification("typed"))]);
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, steel!.Id);
        Assert.True(applied.Succeeded, applied.Reason);

        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(205, inputs["E"].Number);
        Assert.Equal(0.29, inputs["nu"].Number);
        Assert.Equal(7800, inputs["rho"].Number);
        Assert.Equal(280, inputs["f_y"].Number);
        Assert.Equal((steel.Id, 1), CalcDataRecord.PinOf(inputs["nu"].Provenance));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        var run = await host.Service.RunAsync(calc.Id);
        Assert.NotNull(run);
        Assert.Contains(run!.Instance.Outputs, o => o.Symbol == "w_max");

        var (second, again) = await host.Service.Materials.SaveAsync(steel with { Values = [new CalcDataValue("E", 210, "GPa"), new CalcDataValue("nu", 0.3, string.Empty), new CalcDataValue("rho", 7850, "kg/m^3"), new CalcDataValue("f_y", 355, "MPa")] });
        Assert.Null(again);
        Assert.Equal(2, second!.Revision);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }
}
