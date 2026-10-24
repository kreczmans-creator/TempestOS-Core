using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>Addendum A, §A7 drive train: E1 → M4 → M5 → M2 → M3 instantiated from the built-in pack template, values flowing down the links.</summary>
public sealed class DriveTrainChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. Tempest.Calc.Electrical.ElectricalCalculators.All, .. Tempest.Calc.Mechanical.MechanicalCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.DriveTrain, "Conveyor drive", ChainInputs.DriveTrain());

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

        // The chain's physical figures: a 12.5:1 gearbox at 120 rpm on the drum puts 1500 rpm on the motor and the shaft.
        Assert.Equal(1500, Si(e1, "n_pk") * 60 / (2 * Math.PI), 6);
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
}
