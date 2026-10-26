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

        // The shaft's steady torque is E1's load torque at the motor shaft, never its RMS torque (a thermal figure), and its alternating torque is E1's T_alt (0 in continuous duty).
        Assert.Equal(Si(e1, "T_load"), InputSi(m4, "T"), 9);
        Assert.Equal(Si(e1, "T_alt"), InputSi(m4, "T_a"), 9);
        Assert.Equal(0, InputSi(m4, "T_a"), 12);
        Assert.Equal(Si(e1, "F_ax_pk"), InputSi(m5, "F_a"), 9);
        Assert.Equal(Si(e1, "n_pk"), InputSi(m4, "n"), 9);
        Assert.Equal(Si(m4, "R_A"), InputSi(m5, "F_r"), 9);
        Assert.Equal(Si(e1, "n_pk"), InputSi(m5, "n"), 9);
        Assert.Equal(Si(m4, "d_A"), InputSi(m2, "D"), 9);

        // E1 → M3: E1's design torque is the torque about the coupling flange's bolt-circle centre, N·m into kN·m. On the default basis (the load peak) it is T_pk, as before (chain.drive-train-D1).
        Assert.Equal(Si(e1, "T_pk"), Si(e1, "T_design"), 12);
        Assert.Equal(Si(e1, "T_pk"), InputSi(m3, "T_z"), 9);
        Assert.Equal(Si(e1, "T_pk"), Si(m3, "T"), 9);
        Assert.Contains(m3.Inputs, i => i.Symbol == "T_z" && CalcLink.From(i.Provenance) is { OutputSymbol: "T_design" });

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

    private static IReadOnlyList<CalcInputValue> With(IReadOnlyList<CalcInputValue> inputs, params CalcInputValue[] changes) =>
        [.. inputs.Where(i => changes.All(c => c.Symbol != i.Symbol)), .. changes];

    /// <summary>The example's drum on a point-to-point cycle: a quarter turn of the drum in 0.4 s, then a 0.5 s dwell.</summary>
    private static Dictionary<string, IReadOnlyList<CalcInputValue>> PointToPointInputs(LibraryTestHost host)
    {
        var inputs = ChainInputs.DriveTrain(host);
        inputs["e1"] = With(inputs["e1"], CalcInputValue.Choice("duty_mode", "point-to-point"), CalcInputValue.Of("s", 0.25), CalcInputValue.Quantity("t_move", 0.4, "s"), CalcInputValue.Of("f_acc", 1.0 / 3), CalcInputValue.Quantity("t_dwell", 0.5, "s"));
        return inputs;
    }

    [Fact]
    public async Task On_A_Point_To_Point_Cycle_The_Shaft_Gets_The_Load_Torque_As_Mean_The_Cycles_Swing_As_Alternating_And_Fatigue_Sees_Both()
    {
        // chain.drive-train-01. python3 over E1 (12.5:1 at 0.95, J_motor 0.12, J_L 4, quarter turn in 0.4 s, 0.5 s dwell): T_load 151.579, T_acc 82.2537, T_pk 233.833, T_dec 69.325,
        // T_rms 110.527, T_alt = (T_pk - T_dec)/2 = 82.2537 N·m. M4 (35/45/35 mm, 2.692 kN mesh force at mid-span, ground, R 99 %, K_t 1.7, K_ts 1.5, q 0.85, q_s 0.9):
        // with T = T_load, T_a = T_alt the sections x = 50 and 150 mm give n_f 6.28494 and 6.60112 (lowest 6.28494) and n_y 9.16594 and 14.6181. The old binding (T_rms as a steady torque,
        // no alternating part) gave n_f 11.4119 and 7.33833: it overstated the lowest fatigue factor by 17 %.
        var host = Host();
        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.DriveTrain, "Conveyor drive, point to point", PointToPointInputs(host));
        var (e1, m4) = (made.Instances["e1"], made.Instances["m4"]);

        Assert.Equal(151.57894736842104, Si(e1, "T_load"), 6);
        Assert.Equal(82.25366456, Si(e1, "T_alt"), 5);
        Assert.Equal((Si(e1, "T_pk") - Si(e1, "T_dec")) / 2, Si(e1, "T_alt"), 9);
        Assert.NotEqual(Si(e1, "T_rms"), Si(e1, "T_load"), 3);

        Assert.Equal(Si(e1, "T_load"), InputSi(m4, "T"), 9);
        Assert.Equal(Si(e1, "T_alt"), InputSi(m4, "T_a"), 9);
        Assert.Equal(Si(e1, "T_pk"), InputSi(m4, "T_pk"), 9);
        Assert.Equal(6.284936527, Si(m4, "n_f_min"), 4);
        Assert.True(Si(m4, "n_f_min") < 11.4119 * 0.6, "the old steady-RMS binding overstated the fatigue factor");
        Assert.Equal(9.165935443, Si(m4, "n_y_min"), 4);
    }

    [Fact]
    public async Task On_A_Point_To_Point_Cycle_The_Bearing_Life_Is_Counted_At_The_Mean_Speed_Not_The_Peak_Speed()
    {
        // chain.drive-train-03: a quarter turn of the drum (12.5:1) is 0.25 x 2 pi x 12.5 = 19.635 rad of the motor per 0.9 s cycle (0.4 s move, 0.5 s dwell): n_mean = 19.635/0.9 = 21.817 rad/s = 208.33 rpm, where the peak speed
        // 19.635/(0.4 x 2/3) = 73.631 rad/s = 703.125 rpm was bound to the bearing before: the bearing turns at the mean speed (python3).
        var host = Host();
        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.DriveTrain, "Conveyor drive, point to point", PointToPointInputs(host));
        var (e1, m5) = (made.Instances["e1"], made.Instances["m5"]);

        Assert.Equal(703.125, Si(e1, "n_pk") * 60 / (2 * Math.PI), 3);
        Assert.Equal(208.3333, Si(e1, "n_mean") * 60 / (2 * Math.PI), 3);
        Assert.Equal(Si(e1, "n_mean"), InputSi(m5, "n"), 9);
        Assert.Contains(m5.Links, l => l.Input.Symbol == "n" && l.Link.OutputSymbol == "n_mean");
        Assert.DoesNotContain(CalcPackTemplates.DriveTrain.Bindings, b => b is { FromStep: "e1", Output: "n_pk", ToStep: "m5" });

        // The shaft's critical-speed ratio still uses the peak speed.
        Assert.Equal(Si(e1, "n_pk"), InputSi(made.Instances["m4"], "n"), 9);
    }

    [Fact]
    public async Task A_Ball_Screw_Motor_Puts_Its_Peak_Thrust_On_The_Bearing_As_The_Axial_Load()
    {
        // chain.drive-train-05: E1 as a 50 kg ball-screw slide (10 mm lead, 0.1 friction): F_load 49.033 N + m a_pk (50 x 9) = 499.033 N (python3) reaches M5 F_a (0.499033 kN). The bearing's axial
        // factors are typed for it (a single-row ball bearing's X 0.56 and Y 1.5 are invented here): left at Y = 0 the sheet refuses the axial load that then counts for nothing.
        var host = Host();
        var inputs = ChainInputs.DriveTrain(host);
        inputs["e1"] = [CalcInputValue.Choice("mechanism", "lead / ball screw"), CalcInputValue.Choice("duty_mode", "point-to-point"), CalcInputValue.Quantity("p_screw", 10, "mm"), CalcInputValue.Quantity("m", 50, "kg"), CalcInputValue.Of("mu", 0.1), CalcInputValue.Of("eta", 0.9), CalcInputValue.Of("i", 1),
            CalcInputValue.Of("s", 0.5), CalcInputValue.Quantity("t_move", 0.5, "s"), CalcInputValue.Of("f_acc", 1.0 / 3), CalcInputValue.Quantity("t_dwell", 0.5, "s"), CalcInputValue.Quantity("J_mech", 1e-4, "kg*m^2"),
            CalcInputValue.Quantity("J_motor", 2e-4, "kg*m^2"), CalcInputValue.Quantity("T_rated", 2, "N*m"), CalcInputValue.Quantity("T_peak", 6, "N*m"), CalcInputValue.Quantity("n_max", 10000, "rpm")];
        inputs["m5"] = With(inputs["m5"], CalcInputValue.Of("X", 0.56), CalcInputValue.Of("Y", 1.5));
        var made = await ChainKit.InstantiateAsync(host, CalcPackTemplates.DriveTrain, "Screw drive", inputs);
        var (e1, m5) = (made.Instances["e1"], made.Instances["m5"]);

        Assert.Equal(499.03325, Si(e1, "F_ax_pk"), 4);
        Assert.Equal(0.49903325, InputSi(m5, "F_a") / 1000, 8);
        Assert.Contains(m5.Inputs, i => i.Symbol == "F_a" && CalcLink.From(i.Provenance) is { OutputSymbol: "F_ax_pk" });
    }

    [Fact]
    public async Task The_Shaft_Is_The_Motor_Side_Shaft_And_Takes_The_Motor_Shaft_Torque_Without_The_Ratio_Which_The_Output_Side_Ports_Carry()
    {
        // chain.drive-train-02: the pack's shaft (35/45/35 mm, the pinion at mid-span, speed n_pk) is the gearbox INPUT shaft, so the torque on it is the motor-shaft torque: T_load = 1800/(12.5 x 0.95) = 151.579 N·m,
        // with no ratio or efficiency in the binding. A shaft behind the ratio carries T x i x eta: E1's output-side ports give 1800 N·m and 118 rpm, which a shaft after the gearbox is bound to instead.
        var host = Host();
        var made = await InstantiateAsync(host);
        var (e1, m4) = (made.Instances["e1"], made.Instances["m4"]);

        Assert.Equal(1800.0 / (12.5 * 0.95), InputSi(m4, "T"), 9);
        Assert.Equal(1800, Si(e1, "T_out_m"), 6);
        Assert.Equal(Si(e1, "T_load") * 12.5 * 0.95, Si(e1, "T_out_m"), 9);
        Assert.Equal(118, Si(e1, "n_out") * 60 / (2 * Math.PI), 6);
        Assert.Contains("motor-side", CalcPackTemplates.DriveTrain.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Coupling_Design_Torque_Follows_The_Basis_The_Owner_Chooses_On_The_Motor_Step()
    {
        // chain.drive-train-D1: the coupling is checked for the torque the drive can deliver when the owner says so. The shaft's own peak torque (M4 T_pk) stays the load peak.
        var host = Host();
        var inputs = ChainInputs.DriveTrain(host);
        inputs["e1"] = [.. inputs["e1"].Where(v => v.Symbol != "T_basis"), CalcInputValue.Choice("T_basis", Tempest.Calc.Electrical.MotorSizingCalculator.BasisMotorPeak)];
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.DriveTrain, await host.ProjectAsync(), "Conveyor drive", inputs);
        Assert.Null(refusal);
        var (e1, m4, m3) = (made!.Instances["e1"], made.Instances["m4"], made.Instances["m3"]);

        Assert.Equal(InputSi(e1, "T_peak"), Si(e1, "T_design"), 9);
        Assert.True(Si(e1, "T_design") > Si(e1, "T_pk"));
        Assert.Equal(Si(e1, "T_design"), InputSi(m3, "T_z"), 9);
        Assert.Equal(Si(e1, "T_pk"), InputSi(m4, "T_pk"), 9);
        Assert.Equal(Si(e1, "T_design") / (4 * 0.040), Si(m3, "v_max"), 6);
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
        Assert.True(Si(e1New, "T_load") > Si(e1, "T_load"));
        Assert.Equal(Si(e1New, "T_load"), InputSi(m4, "T"), 9);
        Assert.Equal(Si(m4, "R_A"), InputSi(m5, "F_r"), 9);
        Assert.Equal(Si(e1New, "T_design"), InputSi(m3, "T_z"), 9);
        Assert.Equal(Si(e1New, "T_pk"), InputSi(m3, "T_z"), 9);
    }

    [Fact]
    public async Task The_Chain_Checks_And_Issues_As_One_Bundle_With_Every_Link_On_The_Sheet()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.DriveTrain, "CNV-DRV-001", "Client Co", "Checked against SPEC-CNV-07.");

        Assert.Contains("link: Motor sizing rev A · T_load", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Motor sizing rev A · T_alt", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Motor sizing rev A · F_ax_pk", sheet, StringComparison.Ordinal);
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
        var rebound = await host.Service.BindAsync(made.Instances["m4"].Id, "T", external.Id, "T_load");
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
