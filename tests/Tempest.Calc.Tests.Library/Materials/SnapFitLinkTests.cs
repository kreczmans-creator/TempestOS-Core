using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>Addendum B, M8 — the snap-fit's ports in a chain: the separation force of a self-locking joint is not a number, so it cannot be linked.</summary>
public sealed class SnapFitLinkTests
{
    private static LibraryTestHost Host() => new(calculators: [new SnapFitCalculator(), new SpringCalculator()]);

    private static async Task<CalcInstance> SnapFit(LibraryTestHost host, Guid? project, string name, double returnAngle)
    {
        var instance = await host.Service.CreateAsync(SnapFitCalculator.Id, project, name,
        [
            CalcInputValue.Of("mu", 0.5, CalcProvenance.Specification("Test report TR-9")),
            CalcInputValue.Quantity("Es", 2400, "MPa", CalcProvenance.Specification("Maker data sheet DS-1")),
            CalcInputValue.Of("eps_perm_once", 0.04, CalcProvenance.Specification("Maker data sheet DS-1")),
            CalcInputValue.Quantity("alpha_r", returnAngle, "deg", CalcProvenance.Drawing("DRG-9", "A")),
        ]);

        var run = await host.Service.RunAsync(instance.Id);
        Assert.NotNull(run);
        Assert.Empty(run!.Instance.Blocks.Select(b => b.Message));
        return run.Instance;
    }

    [Fact]
    public async Task A_Link_To_The_Separation_Force_Is_Made_Below_The_Locking_Angle_And_Refused_At_Or_Above_It()
    {
        var host = Host();
        var project = await host.ProjectAsync();

        // μ = 0.5: the locking angle is arctan(2) = 63.43°.
        var free = await SnapFit(host, project, "Lid hook, serviceable", 60);
        var locked = await SnapFit(host, project, "Lid hook, permanent", 70);
        Assert.NotNull(free.Outputs.Single(o => o.Symbol == "retention_force").Si);
        Assert.Null(locked.Outputs.Single(o => o.Symbol == "retention_force").Si);

        var downstream = await host.Service.CreateAsync(SpringCalculator.Id, project, "Release spring", []);
        var refused = await host.Service.BindAsync(downstream.Id, "F_2", locked.Id, "retention_force");
        Assert.False(refused.Succeeded);
        Assert.Contains("no numeric output 'retention_force'", refused.Reason, StringComparison.Ordinal);

        // The same joint with a return angle below the limit offers the number, and the assembly force of the locked joint is still a number.
        var bound = await host.Service.BindAsync(downstream.Id, "F_2", free.Id, "retention_force");
        Assert.True(bound.Succeeded, bound.Reason);
        var inputs = bound.Instance!.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal("N", inputs["F_2"].Unit);
        Assert.Equal(free.Outputs.Single(o => o.Symbol == "retention_force").Si!.Value, inputs["F_2"].Number!.Value, 1e-9);

        var assembly = await host.Service.BindAsync(downstream.Id, "F_1", locked.Id, "assembly_force");
        Assert.True(assembly.Succeeded, assembly.Reason);
    }
}
