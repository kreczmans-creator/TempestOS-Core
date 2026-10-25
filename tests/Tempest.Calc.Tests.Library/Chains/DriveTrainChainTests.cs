using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>Addendum A, §A7 drive train: E1 → M4 → M5 → M2 → M3 instantiated from the built-in pack template, values flowing down the links.</summary>
public sealed class DriveTrainChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. Tempest.Calc.Electrical.ElectricalCalculators.All, .. Tempest.Calc.Mechanical.MechanicalCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.DriveTrain, "Conveyor drive", ChainInputs.DriveTrain(host));

    [Fact]
    public async Task Motor_Torque_And_Speed_Reach_The_Shaft_Bearing_Seat_And_Coupling_Flange()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (e1, m4, m5, m2, m3) = (made.Instances["e1"], made.Instances["m4"], made.Instances["m5"], made.Instances["m2"], made.Instances["m3"]);

        Assert.Equal(Si(e1, "T_rms"), InputSi(m4, "T"), 9);
        Assert.Equal(Si(e1, "n_pk"), InputSi(m4, "n"), 9);
        Assert.Equal(Si(m4, "R_A"), InputSi(m5, "F_r"), 9);
        Assert.Equal(Si(e1, "n_pk"), InputSi(m5, "n"), 9);
        Assert.Equal(Si(m4, "d_A"), InputSi(m2, "D"), 9);

        // E1 → M3: the motor's peak torque is the torque about the coupling flange's bolt-circle centre, N·m into kN·m.
        Assert.Equal(Si(e1, "T_pk"), InputSi(m3, "T_z"), 9);
        Assert.Equal(Si(e1, "T_pk"), Si(m3, "T"), 9);
        Assert.Contains(m3.Inputs, i => i.Symbol == "T_z" && CalcLink.From(i.Provenance) is { OutputSymbol: "T_pk" });

        // Four bolts on a 40 mm radius share that torque equally in shear: v = T / (n r).
        Assert.Equal(Si(e1, "T_pk") / (4 * 0.040), Si(m3, "v_max"), 6);

        // The chain's physical figures: a 12.5:1 gearbox at 118 rpm on the drum puts 1475 rpm on the motor and the shaft, just below the motor's 1480 rpm nameplate
        // (the induction motor runs under its synchronous 1500 rpm), so E1 does not warn speed-above-rated.
        Assert.Equal(1475, Si(e1, "n_pk") * 60 / (2 * Math.PI), 6);
        Assert.Equal(1480, InputSi(e1, "n_rated") * 60 / (2 * Math.PI), 6);
        Assert.True(Si(e1, "n_pk") < InputSi(e1, "n_rated"));
        Assert.DoesNotContain(e1.Warnings, w => w.RuleId == "speed-above-rated");
        Assert.Equal(1800 / (12.5 * 0.95), Si(e1, "T_load"), 9);
        Assert.Equal(Si(e1, "T_load"), Si(e1, "T_rms"), 9);
        Assert.Equal(35, Si(m4, "d_A") * 1000, 9);
        Assert.Equal(5, made.Pack.InstanceIds.Count);
        Assert.All(made.Instances.Values, i => Assert.All(i.Checks, check => Assert.True(check.Passed, $"{i.Name}: {check.Name}")));
    }

    [Fact]
    public async Task Changing_The_Motor_Makes_Every_Step_Stale_Blocks_The_Pack_And_Updating_Carries_It_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var e1 = made.Instances["e1"];

        // A heavier drum: the load torque rises, so the shaft's torque, bearing load and coupling load all move.
        await ReviseAndUpdateAsync(host, made, "e1", "T_L", 1.2, ["m4", "m5", "m2", "m3"]);

        var (e1New, m4, m5, m3) = (await Fresh(host, made, "e1"), await Fresh(host, made, "m4"), await Fresh(host, made, "m5"), await Fresh(host, made, "m3"));
        Assert.True(Si(e1New, "T_rms") > Si(e1, "T_rms"));
        Assert.Equal(Si(e1New, "T_rms"), InputSi(m4, "T"), 9);
        Assert.Equal(Si(m4, "R_A"), InputSi(m5, "F_r"), 9);
        Assert.Equal(Si(e1New, "T_pk"), InputSi(m3, "T_z"), 9);
    }

    [Fact]
    public async Task The_Chain_Checks_And_Issues_As_One_Bundle_With_Every_Link_On_The_Sheet()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.DriveTrain, "CNV-DRV-001", "Client Co", "Checked against SPEC-CNV-07.");

        Assert.Contains("link: Motor sizing rev A · T_rms", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Motor sizing rev A · T_pk", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Shaft rev A · R_A", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Shaft rev A · d_A", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Member_Made_Stale_After_The_Pack_Was_Checked_By_A_Calculation_Outside_It_Cannot_Be_Issued()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // A motor sizing outside the pack feeds the shaft's torque instead of the pack's own; re-pinned while the shaft is still a draft.
        var external = await host.Service.CreateAsync("elec.motor-sizing", made.Pack.ProjectId, "Motor sizing from another job", ChainInputs.DriveTrain(host)["e1"]);
        var rebound = await host.Service.BindAsync(made.Instances["m4"].Id, "T", external.Id, "T_rms");
        Assert.True(rebound.Succeeded, rebound.Reason);

        await AuthoredByAnotherAsync(host, made);

        // While the outside calculation is a draft it can still change under its revision letter, so the pack is not checked on it (Red R-A-02).
        var early = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(early.Succeeded);
        Assert.Contains("'Motor sizing from another job' rev A, which is still a draft", early.Reason, StringComparison.Ordinal);

        // Checked and issued it is fixed, and the pack checks on it.
        host.Principal = "j.reviewer";
        Assert.True((await host.Service.CheckAsync(external.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(external.Id, "EXT-1", "Client Co")).Succeeded);
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);

        // The outside calculation moves on after the check, as a new revision; no pack member is touched, yet the shaft is now stale.
        var moved = await host.Service.ReviseAsync(external.Id);
        Assert.True(moved.Succeeded, moved.Reason);
        var shaft = await Fresh(host, made, "m4");
        Assert.True((await host.Service.StalenessAsync(shaft)).IsStale);

        // Issue is refused at the staleness gate: not for the pack's status, and not for a member edited after the check.
        var refused = await host.Service.IssuePackAsync(made.Pack.Id, "CNV-DRV-STALE", "Client Co");
        Assert.False(refused.Succeeded);
        Assert.Contains("Pack member 'Shaft' is stale", refused.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("changed after the pack was checked", refused.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);
    }
}
