using Tempest.Calc.FluidPower;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>Addendum B, F1 — the cylinder rod takes E and f_y from a W3 material, pinned by revision; the fluid density and the mounting factor stay the user's inputs.</summary>
public sealed class CylinderMaterialTests
{
    private static LibraryTestHost Host() => new(calculators: [new CylinderCalculator()]);

    private static CalcDataRecord RodSteel() => new(
        Guid.Empty, MaterialLibrary.Kind, "Chromed rod steel (test certificate)", 0, "Rod certificate RC-1", "rev A", "p. 2, Table 1", "Test Rod Co", "ignored", false, CalcDataOrigin.UserEntered,
        [new CalcDataValue("E", 205, "GPa"), new CalcDataValue("f_y", 460, "MPa"), new CalcDataValue("rho", 7850, "kg/m^3")],
        default);

    [Fact]
    public void The_Material_Record_Kind_Written_In_The_Template_Is_The_Library_Kind()
    {
        var kind = new CylinderCalculator().Template.RecordKinds!.Single(k => MaterialLibrary.IsMaterial(k.Kind));
        var library = MaterialLibrary.RecordKind(["E", "f_y"]);
        Assert.Equal(library.Kind, kind.Kind);
        Assert.Equal(library.Symbols, kind.Symbols);
        Assert.Equal(MaterialLibrary.Kind, CylinderCalculator.MaterialKind);
    }

    [Fact]
    public async Task Applying_A_Material_Fills_E_And_Fy_Pins_The_Revision_And_Leaves_The_Fluid_Density_Alone()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (steel, refusal) = await host.Service.Materials.SaveAsync(RodSteel());
        Assert.Null(refusal);
        var template = new CylinderCalculator().Template;
        var calc = await host.Service.CreateAsync(template.Id, project, "Ram 1", [CalcInputValue.Quantity("rho", 870, "kg/m^3", CalcProvenance.Specification("Oil data sheet OD-1"))]);

        var applied = await host.Service.Materials.ApplyAsync(calc.Id, steel!.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(205, inputs["E"].Number);
        Assert.Equal("GPa", inputs["E"].Unit);
        Assert.Equal(460, inputs["f_y"].Number);
        Assert.Equal("MPa", inputs["f_y"].Unit);
        Assert.Equal(870, inputs["rho"].Number);
        Assert.Equal((steel.Id, 1), CalcDataRecord.PinOf(inputs["E"].Provenance));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        // A later revision of the material makes the calculation stale; the inputs stay editable (they are plain inputs).
        var (second, again) = await host.Service.Materials.SaveAsync(steel with { Values = [new CalcDataValue("E", 205, "GPa"), new CalcDataValue("f_y", 500, "MPa"), new CalcDataValue("rho", 7850, "kg/m^3")] });
        Assert.Null(again);
        Assert.Equal(2, second!.Revision);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }
}
