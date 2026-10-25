using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>Addendum B, M8 — the snap-fit takes the permissible strains, ν and the secant-modulus points Es(ε) from a W3 material, pinned by revision; μ and the annular factor stay the user's records.</summary>
public sealed class SnapFitMaterialTests
{
    private static LibraryTestHost Host() => new(calculators: [new SnapFitCalculator()]);

    private static CalcRow Row(double strain, double mpa) => CalcRow.Of(("eps", new CalcCell(strain)), ("Es", new CalcCell(mpa, "MPa")));

    private static CalcDataRecord Plastic(double permissible = 0.04) => new(
        Guid.Empty, MaterialLibrary.Kind, "Test plastic (maker's data sheet)", 0, "Maker data sheet DS-1", "rev A", "p. 3, Table 2", "Test Plastics Co", "ignored", false, CalcDataOrigin.UserEntered,
        [
            new CalcDataValue("nu", 0.38),
            new CalcDataValue("eps_perm_once", permissible),
            new CalcDataValue("eps_perm_repeated", 0.02),
            new CalcDataValue(MaterialProperties.SecantCurve, Rows: [Row(0.005, 2600), Row(0.01, 2400), Row(0.03, 1800)]),
        ],
        default);

    [Fact]
    public void The_Names_Written_In_The_Template_Are_The_Library_Names()
    {
        var template = new SnapFitCalculator().Template;
        var kind = template.RecordKinds!.Single(k => MaterialLibrary.IsMaterial(k.Kind));
        var library = MaterialLibrary.RecordKind(["Es_pts", "eps_perm_once", "eps_perm_repeated", "nu"]);
        Assert.Equal(library.Kind, kind.Kind);
        Assert.Equal(library.Symbols, kind.Symbols);
        Assert.Equal(MaterialProperties.SecantCurve, SnapFitCalculator.SecantCurveKey);
        Assert.Equal(MaterialProperties.SecantCurve, template.FindInput("Es_pts")!.MaterialProperty);
        Assert.Equal("eps_perm_once", template.FindInput("eps_perm_once")!.MaterialProperty);
        Assert.Equal("eps_perm_repeated", template.FindInput("eps_perm_repeated")!.MaterialProperty);
        Assert.Equal("nu", template.FindInput("nu")!.MaterialProperty);
        Assert.All(new[] { "eps_perm_once", "eps_perm_repeated", "nu" }, key => Assert.NotNull(MaterialProperties.Find(key, out _)));
    }

    [Fact]
    public async Task Applying_A_Plastic_Fills_The_Strain_Limits_Nu_And_The_Secant_Points_Pins_The_Revision_And_Drives_The_Sheet()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (plastic, refusal) = await host.Service.Materials.SaveAsync(Plastic());
        Assert.Null(refusal);
        var calc = await host.Service.CreateAsync(SnapFitCalculator.Id, project, "Lid hook",
            [CalcInputValue.Of("mu", 0.3, CalcProvenance.Specification("Test report TR-9")), CalcInputValue.Choice("Es_source", SnapFitCalculator.EsCurve), CalcInputValue.Quantity("y", 1.5, "mm", CalcProvenance.Specification("Drawing"))]);

        var applied = await host.Service.Materials.ApplyAsync(calc.Id, plastic!.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(0.04, inputs["eps_perm_once"].Number);
        Assert.Equal(0.02, inputs["eps_perm_repeated"].Number);
        Assert.Equal(0.38, inputs["nu"].Number);
        Assert.Equal(3, inputs["Es_pts"].Rows!.Count);
        Assert.Equal(0.3, inputs["mu"].Number);
        Assert.Equal((plastic.Id, 1), CalcDataRecord.PinOf(inputs["eps_perm_once"].Provenance));
        Assert.Equal((plastic.Id, 1), CalcDataRecord.PinOf(inputs["Es_pts"].Provenance));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        // l 15, h 2, y 1.5 mm: ε = 2 %; the points give Es = 2100 MPa; P = b·h²·Es·ε/(6·l) = 11.2 N; u = 0.02/0.04.
        var run = await host.Service.RunAsync(calc.Id);
        Assert.NotNull(run);
        Assert.Empty(run!.Instance.Blocks.Select(b => b.Message));
        Assert.Equal(2100e6, run.Instance.Outputs.Single(o => o.Symbol == "Es").Si!.Value, 1);
        Assert.Equal(11.2, run.Instance.Outputs.Single(o => o.Symbol == "P").Si!.Value, 1e-6);
        Assert.Equal(0.5, run.Instance.Outputs.Single(o => o.Symbol == "strain_util").Si!.Value, 1e-9);

        // A later revision of the plastic makes the sheet stale: the limits and the curve are revision-pinned.
        var (second, again) = await host.Service.Materials.SaveAsync(plastic with { Values = [.. plastic.Values.Where(v => v.Symbol != "eps_perm_once"), new CalcDataValue("eps_perm_once", 0.03)] });
        Assert.Null(again);
        Assert.Equal(2, second!.Revision);
        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);
    }
}
