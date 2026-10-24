using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>Addendum A, §A7 drive train: E1 → M4 → M5 → M2 instantiated from the built-in pack template, values flowing down the links.</summary>
public sealed class DriveTrainChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. Tempest.Calc.Electrical.ElectricalCalculators.All, .. Tempest.Calc.Mechanical.MechanicalCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static double Si(CalcInstance instance, string symbol) => instance.Outputs.Single(o => o.Symbol == symbol).Si!.Value;

    private static double InputSi(CalcInstance instance, string symbol)
    {
        var input = instance.Inputs.Single(i => i.Symbol == symbol);
        return Tempest.Calc.Engine.Units.UnitCatalog.Parse(input.Unit ?? string.Empty).ToSi(input.Number!.Value);
    }

    [Fact]
    public async Task Motor_Torque_And_Speed_Reach_The_Shaft_Bearing_And_Seat()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.DriveTrain));
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.DriveTrain, await host.ProjectAsync(), "Conveyor drive");
        Assert.Null(refusal);
        var e1 = made!.Instances["e1"];
        var m4 = made.Instances["m4"];
        var m5 = made.Instances["m5"];
        var m2 = made.Instances["m2"];

        Assert.Equal(Si(e1, "T_rms"), InputSi(m4, "T"), 9);
        Assert.Equal(Si(e1, "n_pk"), InputSi(m4, "n"), 9);
        Assert.Equal(Si(m4, "R_A"), InputSi(m5, "F_r"), 9);
        Assert.Equal(Si(e1, "n_pk"), InputSi(m5, "n"), 9);
        Assert.Equal(Si(m4, "d_A"), InputSi(m2, "D"), 9);
        Assert.Equal(4, made.Pack.InstanceIds.Count);
        Assert.All(made.Instances.Values, i => Assert.False(i.Blocks.Count > 0, $"{i.Name}: {string.Join("; ", i.Blocks.Select(b => b.Message))}"));
    }

    [Fact]
    public async Task Changing_The_Motor_Makes_Every_Step_Stale_And_Updating_Carries_It_Through()
    {
        var host = Host();
        var (made, _) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.DriveTrain, await host.ProjectAsync(), "Conveyor drive");
        var e1 = made!.Instances["e1"];

        var changed = await host.Service.UpdateInputsAsync(e1.Id, [.. e1.Inputs.Select(i => i.Number is { } n && i.Symbol == "m" ? i with { Number = n * 2 } : i)]);
        Assert.True(changed.Succeeded, changed.Reason);
        Assert.NotEqual(e1.OutputsHash, changed.Instance!.OutputsHash);

        foreach (var key in new[] { "m4", "m5", "m2" })
            Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(made.Instances[key].Id))!)).IsStale, $"{key} should be stale");

        // Updating each step in chain order clears the staleness downstream (a step whose linked values did not change
        // is current again once its upstream is).
        foreach (var key in new[] { "m4", "m5", "m2" })
        {
            var current = (await host.Store.FindInstanceAsync(made.Instances[key].Id))!;

            if ((await host.Service.StalenessAsync(current)).IsStale)
                Assert.True((await host.Service.UpdateLinksAsync(current.Id)).Succeeded, key);
        }

        var m4 = (await host.Store.FindInstanceAsync(made.Instances["m4"].Id))!;
        var m5 = (await host.Store.FindInstanceAsync(made.Instances["m5"].Id))!;
        Assert.Equal(Si(changed.Instance, "T_rms"), InputSi(m4, "T"), 9);
        Assert.Equal(Si(m4, "R_A"), InputSi(m5, "F_r"), 9);
        Assert.False((await host.Service.StalenessAsync(m5)).IsStale);
        Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(made.Instances["m2"].Id))!)).IsStale);
    }
}
