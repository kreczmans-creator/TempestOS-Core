using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>Addendum B, M7 — the helical spring takes G, E and ρ from a W3 material, pinned by revision; R_m(d) stays the user's input.</summary>
public sealed class SpringMaterialTests
{
    private static LibraryTestHost Host() => new(calculators: [new SpringCalculator()]);

    private static CalcDataRecord SpringSteel() => new(
        Guid.Empty, MaterialLibrary.Kind, "Spring steel (test certificate)", 0, "Wire certificate WC-1", "rev A", "p. 2, Table 1", "Test Wire Co", "ignored", false, CalcDataOrigin.UserEntered,
        [new CalcDataValue("G", 79.3, "GPa"), new CalcDataValue("E", 206, "GPa"), new CalcDataValue("rho", 7850, "kg/m^3")],
        default);

    private static CalcDataRecord SpringWire() => new(
        Guid.Empty, MaterialLibrary.Kind, "Spring wire with an R_m(d) curve (test certificate)", 0, "Wire certificate WC-2", "rev A", "p. 3, Table 2", "Test Wire Co", "ignored", false, CalcDataOrigin.UserEntered,
        [new CalcDataValue("G", 79.3, "GPa"), new CalcDataValue("E", 206, "GPa"), new CalcDataValue("rho", 7850, "kg/m^3"), new CalcDataValue("Rm_A", 1800, "MPa"), new CalcDataValue("Rm_m", 0.1)],
        default);

    [Fact]
    public void The_Material_Record_Kind_Written_In_The_Template_Is_The_Library_Kind()
    {
        var kind = new SpringCalculator().Template.RecordKinds!.Single(k => MaterialLibrary.IsMaterial(k.Kind));
        var library = MaterialLibrary.RecordKind(["G", "E", "rho", "Rm_A", "Rm_m"]);
        Assert.Equal(library.Kind, kind.Kind);
        Assert.Equal(library.Symbols, kind.Symbols);
    }

    [Fact]
    public async Task Applying_A_Material_Fills_G_E_And_Rho_Pins_The_Revision_And_Leaves_The_Wire_Strength_Alone()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (steel, refusal) = await host.Service.Materials.SaveAsync(SpringSteel());
        Assert.Null(refusal);
        var template = new SpringCalculator().Template;
        var calc = await host.Service.CreateAsync(template.Id, project, "Return spring", [CalcInputValue.Quantity("Rm", 1650, "MPa", CalcProvenance.Specification("Wire certificate WC-1"))]);

        var applied = await host.Service.Materials.ApplyAsync(calc.Id, steel!.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(79.3, inputs["G"].Number);
        Assert.Equal("GPa", inputs["G"].Unit);
        Assert.Equal(206, inputs["E"].Number);
        Assert.Equal(7850, inputs["rho"].Number);
        Assert.Equal(1650, inputs["Rm"].Number);
        Assert.Equal((steel.Id, 1), CalcDataRecord.PinOf(inputs["G"].Provenance));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        // A later revision of the material makes the spring stale; the inputs stay editable (they are plain inputs).
        var (second, again) = await host.Service.Materials.SaveAsync(steel with { Values = [new CalcDataValue("G", 81.5, "GPa"), new CalcDataValue("E", 206, "GPa"), new CalcDataValue("rho", 7850, "kg/m^3")] });
        Assert.Null(again);
        Assert.Equal(2, second!.Revision);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }

    [Fact]
    public async Task Review_M6_The_Rm_Curve_Coefficients_Come_From_The_Material_Pinned_By_Revision_And_Drive_The_Sheet()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (wire, refusal) = await host.Service.Materials.SaveAsync(SpringWire());
        Assert.Null(refusal);
        var calc = await host.Service.CreateAsync(SpringCalculator.Id, project, "Return spring, curve",
            [CalcInputValue.Choice("rm_source", SpringCalculator.RmCurve), CalcInputValue.Quantity("S_e", 400, "MPa", CalcProvenance.Specification("Wire certificate WC-2")),
                CalcInputValue.Quantity("L0", 45, "mm", CalcProvenance.Specification("Drawing")), CalcInputValue.Quantity("F_1", 10, "N", CalcProvenance.Specification("Duty")), CalcInputValue.Quantity("F_2", 40, "N", CalcProvenance.Specification("Duty"))]);

        var applied = await host.Service.Materials.ApplyAsync(calc.Id, wire!.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(1800, inputs["Rm_A"].Number);
        Assert.Equal("MPa", inputs["Rm_A"].Unit);
        Assert.Equal(0.1, inputs["Rm_m"].Number);
        Assert.Equal((wire.Id, 1), CalcDataRecord.PinOf(inputs["Rm_A"].Provenance));
        Assert.Equal((wire.Id, 1), CalcDataRecord.PinOf(inputs["Rm_m"].Provenance));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        var run = await host.Service.RunAsync(calc.Id);
        Assert.NotNull(run);
        Assert.Empty(run!.Instance.Blocks.Select(b => b.Message));
        var rm = run.Instance.Outputs.Single(o => o.Symbol == "Rm");
        Assert.Equal(1800e6 / Math.Pow(2, 0.1), rm.Si!.Value, 1800e6 / Math.Pow(2, 0.1) * 1e-9);

        // A later revision of the wire's curve makes the sheet stale: the curve is revision-pinned.
        var (second, again) = await host.Service.Materials.SaveAsync(wire with { Values = [new CalcDataValue("G", 79.3, "GPa"), new CalcDataValue("E", 206, "GPa"), new CalcDataValue("rho", 7850, "kg/m^3"), new CalcDataValue("Rm_A", 1750, "MPa"), new CalcDataValue("Rm_m", 0.1)] });
        Assert.Null(again);
        Assert.Equal(2, second!.Revision);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }
}
