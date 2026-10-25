using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B, §B7 chain 1 (phase B-2): the hydraulic power unit F1 → F2, B1, E1 → E4 from the built-in pack template. The cylinder and pump circuit gives the
/// accumulator its stroke volume and supply pressure, the pressure line (the line to the cylinder cap port) its flow, oil density and bore, and the pump motor its
/// load torque and speed; the candidate motor's rated shaft power sizes the supply cable. The cable step's two tables are SYNTHETIC test values (never a regulation or manufacturer table); the shipped example omits them.
/// </summary>
public sealed class HydraulicPowerUnitChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. FluidPowerCalculators.All, .. BuildingServicesCalculators.All, .. ElectricalCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.HydraulicPowerUnit, "Press 5 hydraulic power unit", ChainInputs.HydraulicPowerUnit(host));

    /// <summary>The whole F1 revision of a bigger circuit: 10 % more bore, supply pressure, circuit flow and pump speed, so every downstream link carries a new value.</summary>
    private static IReadOnlyList<CalcInputValue> Uprate(IReadOnlyList<CalcInputValue> inputs) =>
        Scale("D", 1.1)(Scale("p", 1.1)(Scale("Q", 1.1)(Scale("n_p", 1.1)(inputs))));

    [Fact]
    public async Task Values_Flow_Down_Every_Link_Of_The_Chain_With_Their_Units()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.HydraulicPowerUnit));
        var made = await InstantiateAsync(host);
        var (f1, f2, b1, e1, e4) = (made.Instances["f1"], made.Instances["f2"], made.Instances["b1"], made.Instances["e1"], made.Instances["e4"]);
        Assert.Equal(5, made.Pack.InstanceIds.Count);

        // F1: 100/70 cylinder, 500 mm stroke, 160 bar, 36 L/min, a 28 cm³/rev pump at 1450 rpm (all closed-form, recomputed independently).
        var areaBore = Math.PI * 0.1 * 0.1 / 4;
        Assert.Equal(areaBore * 0.5, Si(f1, "dV"), 12);
        Assert.Equal(3.926990816987242e-3, Si(f1, "dV"), 15);
        Assert.Equal(160e5, Si(f1, "p"), 6);
        Assert.Equal(36e-3 / 60, Si(f1, "Q"), 15);
        var pumpPower = 160e5 * (28e-6 * 1450 / 60 * 0.95) / 0.88;
        Assert.Equal(11687.878787878786, pumpPower, 6);
        Assert.Equal(pumpPower, Si(f1, "P_pump"), 6);
        Assert.Equal(76.97311793171664, Si(f1, "T_shaft"), 9);
        Assert.Equal(160e5 * 28e-6 * 0.95 / (2 * Math.PI * 0.88), Si(f1, "T_shaft"), 9);
        Assert.Equal(1450, Si(f1, "n_shaft") * 60 / (2 * Math.PI), 9);

        // F1 1.2.0 moved these (independent hand recompute): the rod is judged at the stall thrust p·A_b (125.66 kN, above the 80 kN push), and the 20 mm
        // cap-port line at the retract flow Q·A_b/A_a = 75.63 L/min, 4.01 m/s against the 5 m/s placeholder limit (a 16 mm line read 6.27 m/s and failed, so the pack uses 20 mm).
        var stall = 160e5 * areaBore;
        Assert.Equal(stall * 3.5 / 1787806.9776735522, Si(f1, "u_buck"), 9);
        Assert.Equal(0.2460125600778818, Si(f1, "u_buck"), 9);
        var areaRod = Math.PI * 0.07 * 0.07 / 4;
        var capFlow = 28e-6 * 1450 / 60 * 0.95 * areaBore / (areaBore - areaRod);
        Assert.Equal(capFlow / (Math.PI * 0.02 * 0.02 / 4), Si(f1, "v_prs"), 9);
        Assert.Equal(4.012160885656473, Si(f1, "v_prs"), 9);
        Assert.Equal(0.8024321771312947, Si(f1, "u_prs"), 9);
        Assert.Equal(28e-6 * 1450 / 60 / (Math.PI * 0.04 * 0.04 / 4), Si(f1, "v_suc"), 9);
        Assert.Equal(7, f1.Checks.Count);
        Assert.All(f1.Checks, c => Assert.True(c.Passed, c.Name));

        // F1 → F2, swept volume ΔV (L): the emergency stroke is the whole larger stroke, 3.927 L.
        Assert.Equal(Si(f1, "dV"), InputSi(f2, "dV"), 15);
        Assert.Equal("Emergency stroke", f2.Inputs.Single(i => i.Symbol == "mode").Text);

        // F1 → F2, supply pressure p (bar) is the accumulator's highest working pressure p_2; p_1 stays above the pressure the 80 kN push needs (80 kN / (A_b·η_m)).
        Assert.Equal(Si(f1, "p"), InputSi(f2, "p_2"), 6);

        // L-g: the linked p is gauge and F2's p_basis is Gauge (its default), so the link is consistent and F2 does not warn; in Absolute mode it would (see the accumulator tests).
        Assert.Equal("Gauge", f2.Inputs.Single(i => i.Symbol == "p_basis").Text);
        Assert.DoesNotContain(f2.Warnings, w => w.RuleId == "link-basis");
        Assert.True(InputSi(f2, "p_1") > 80e3 / (areaBore * 0.95), "the lowest working pressure must still finish the stroke under load");
        Assert.Equal(107.2, 80e3 / (areaBore * 0.95) / 1e5, 1);

        // F2: V0 = f_T·ΔV/[(p0/p1)^(1/n) − (p0/p2)^(1/n)] on absolute pressures with n = 1.4 and a 10 to 50 °C range.
        Assert.Equal(26.69518127508491e-3, Si(f2, "V0"), 12);
        Assert.Equal(0.9733851458414678, Si(f2, "u_r0"), 9);
        Assert.Equal(0.43334029084376174, Si(f2, "u_rp"), 9);
        Assert.Equal(2, f2.Checks.Count);
        Assert.All(f2.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.DoesNotContain(f2.Warnings, w => w.RuleId is "exponent" or "realgas" or "shock");

        // F1 → B1: the pressure line B1 judges is the line F1 judges, the line to the cylinder cap port: its flow Q_prs (L/min into m³/h) is the larger of the two strokes,
        // the retract discharge Q·A_b/A_a = 38.57 × 1.9608 = 75.627 L/min = 4.5376 m³/h; the oil density and the line bore come by link too (the bore as a table-cell link).
        Assert.Equal(Si(f1, "Q_prs"), InputSi(b1, "Q"), 15);
        Assert.Equal(75.62745098039215e-3 / 60, Si(f1, "Q_prs"), 12);
        Assert.Equal(4.53764705882353, Si(b1, "Q_design") * 3600, 9);
        Assert.Equal(["Q", "rho_custom"], b1.Links.Select(l => l.Input.Symbol));
        var boreLink = Assert.Single(b1.CellLinks);
        Assert.Equal(("segments", 0, "D", "d_prs"), (boreLink.Symbol, boreLink.Row, boreLink.Column, boreLink.Link.OutputSymbol));
        Assert.Equal(f1.Id, boreLink.Link.UpstreamId);
        Assert.Equal(InputSi(f1, "rho"), InputSi(b1, "rho_custom"), 12);
        Assert.Equal(870, InputSi(b1, "rho_custom"), 12);
        Assert.Equal(Si(f1, "rho"), InputSi(b1, "rho_custom"), 12);
        Assert.Equal(InputSi(f1, "d_prs"), CellSi(b1, "segments", 0, "D"), 12);
        Assert.Equal(Si(f1, "d_prs"), CellSi(b1, "segments", 0, "D"), 12);
        Assert.Equal(20e-3, CellSi(b1, "segments", 0, "D"), 15);

        // B1 at 75.6 L/min in the 20 mm line: v = 4.012 m/s and Re = 2327, just above the 2300 where B1 switches from 64/Re to Colebrook–White, so the flow is
        // TRANSITIONAL (the sheet warns that the friction factor is uncertain; it is not flagged laminar). Colebrook f = 0.048912 (64/Re would read 0.027503, a 44 % step
        // across the threshold, so the Colebrook figure is the conservative one): Δp = f L/D ρ v²/2 + K ρ v²/2 = 205.499 + 42.014 = 247.513 kPa, 1.55 % of the supply pressure.
        var velocity = 75.62745098039215e-3 / 60 / (Math.PI * 0.02 * 0.02 / 4);
        var reynolds = 870 * velocity * 0.02 / 0.03;
        Assert.Equal(velocity, Si(b1, "v_s1"), 9);
        Assert.Equal(4.012160885656472, velocity, 9);
        Assert.Equal(2327.0533136807544, reynolds, 6);
        Assert.Equal(reynolds, Si(b1, "Re_s1"), 6);
        var friction = 0.02;

        for (var i = 0; i < 200; i++)
            friction = Math.Pow(-2 * Math.Log10(0.045e-3 / 0.02 / 3.7 + 2.51 / (reynolds * Math.Sqrt(friction))), -2);

        Assert.Equal(0.04891170887386416, friction, 12);
        Assert.Equal(friction, Si(b1, "f_s1"), 8);
        var frictionLoss = friction * 12 / 0.02 * 870 * velocity * velocity / 2;
        var fittings = 6 * 870 * velocity * velocity / 2;
        Assert.Equal(247513.4521071798, frictionLoss + fittings, 2);
        Assert.Equal(frictionLoss + fittings, Si(b1, "dp_total"), 1);
        Assert.Equal(Si(b1, "dp_total"), Si(b1, "dp_sys"), 6);
        Assert.True(Si(b1, "dp_total") < 0.02 * Si(f1, "p"));
        Assert.Contains(b1.Warnings, w => w.RuleId == "transition");
        Assert.DoesNotContain(b1.Warnings, w => w.RuleId == "laminar");

        // L-h: the six velocity heads of fittings are a turbulent-flow figure applied at Re 2327, where the real K is higher: the sheet says the fittings term is understated.
        var fittingsWarning = Assert.Single(b1.Warnings, w => w.RuleId == "fittings");
        Assert.Contains("understated", fittingsWarning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(b1.Outputs, o => o.Symbol == "T_shaft");
        Assert.Single(b1.Checks);
        Assert.True(b1.Checks[0].Passed);

        // F1 → E1, pump shaft torque T_shaft (N·m) and speed n_shaft (rpm) are the motor's continuous load torque and speed.
        Assert.Equal(Si(f1, "T_shaft"), InputSi(e1, "T_L"), 12);
        Assert.Equal(Si(f1, "n_shaft"), InputSi(e1, "n_L"), 9);
        Assert.Equal("continuous", e1.Inputs.Single(i => i.Symbol == "duty_mode").Text);

        // E1: the coupling (η 0.99) carries 77.751 N·m at 1450 rpm: 11.806 kW, with a 2 s run-up of 0.105 kg·m².
        var omega = 1450 * 2 * Math.PI / 60;
        Assert.Equal(76.97311793171664 / 0.99, Si(e1, "T_load"), 9);
        Assert.Equal(77.75062417345116, Si(e1, "T_load"), 9);
        Assert.Equal(Si(e1, "T_load"), Si(e1, "T_rms"), 12);
        Assert.Equal(0.10504040404040405, Si(e1, "J_total"), 12);
        Assert.Equal(7.974858906866394, Si(e1, "T_acc"), 9);
        Assert.Equal(85.72548308031755, Si(e1, "T_pk"), 9);
        Assert.Equal(Si(e1, "T_load") * omega, Si(e1, "P_cont"), 6);
        Assert.Equal(11805.938169574532, Si(e1, "P_cont"), 6);
        Assert.Equal(Si(e1, "P_cont"), Si(e1, "P_rms"), 6);
        Assert.Equal(3, e1.Checks.Count);
        Assert.All(e1.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.Equal(0.8099023351401162, Si(e1, "u_rms"), 9);

        // E1's candidate motor is rated 120 N·m at an invented nameplate 1460 rpm (not the 1450 rpm the pump runs at): 120 × 152.904 = 18.347 kW, above the pump's 11.806 kW.
        Assert.Equal(1460, InputSi(e1, "n_rated") * 60 / (2 * Math.PI), 9);
        Assert.Equal(120 * 1460 * 2 * Math.PI / 60, Si(e1, "P_rated"), 6);
        Assert.Equal(18346.90109696439, Si(e1, "P_rated"), 6);
        Assert.DoesNotContain(e1.Warnings, w => w.RuleId == "rated-speed");

        // E1 → E4, rated shaft power P_rated (W into kW): E4 is sized on the motor, not on the load; it takes a shaft power directly, so there is no load-budget step and no design current is linked.
        Assert.Equal(Si(e1, "P_rated"), InputSi(e4, "P"), 6);
        Assert.Equal(18.34690109696439, InputSi(e4, "P") / 1e3, 9);
        Assert.DoesNotContain(e4.Inputs, i => i.Symbol == "I_b_in");
        Assert.Equal(["P"], e4.Links.Select(l => l.Input.Symbol));
        Assert.Equal("P_rated", e4.Links.Single().Link.OutputSymbol);

        // E4: I_b = P/(√3 U cos φ η) = 33.470 A; the type C device is the 40 A rating and the synthetic capacities give 10 mm² (44.6 A; 6 mm² carries 31.9 A).
        Assert.Equal(18346.90109696439 / (Math.Sqrt(3) * 400 * 0.86 * 0.92), Si(e4, "I_b"), 9);
        Assert.Equal(33.47000849067186, Si(e4, "I_b"), 9);
        Assert.Equal(40, Si(e4, "I_n"), 9);
        Assert.Equal(10e-6, Si(e4, "CSA"), 15);
        Assert.Equal(44.6, Si(e4, "I_z"), 9);
        Assert.Equal(30, Si(e4, "L"), 9);
        Assert.Equal(2, Si(e4, "device_code"), 9);
        Assert.All(e4.Checks, c => Assert.True(c.Passed, c.Name));

        // The tables are labelled synthetic on the sheet's inputs and nowhere cite a regulation.
        Assert.Contains("SYNTHETIC", e4.Inputs.Single(i => i.Symbol == "ampacity").Provenance!.Reference, StringComparison.Ordinal);
        Assert.Contains("SYNTHETIC", e4.Inputs.Single(i => i.Symbol == "ratings").Provenance!.Reference, StringComparison.Ordinal);

        Assert.All(made.Instances.Values, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task Every_Binding_Of_The_Template_Is_A_Link_From_The_Output_It_Names()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var template = CalcPackTemplates.HydraulicPowerUnit;

        Assert.Equal(["f1", "f2", "b1", "e1", "e4"], template.Steps.Select(s => s.Key));
        Assert.Equal(
            [("f1", "dV", "f2", "dV"), ("f1", "p", "f2", "p_2"), ("f1", "Q_prs", "b1", "Q"), ("f1", "rho", "b1", "rho_custom"), ("f1", "d_prs", "b1", "segments"), ("f1", "T_shaft", "e1", "T_L"), ("f1", "n_shaft", "e1", "n_L"), ("e1", "P_rated", "e4", "P")],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.Equal((0, "D"), (template.Bindings.Single(b => b.Input == "segments").Row, template.Bindings.Single(b => b.Input == "segments").Column));

        foreach (var b in template.Bindings.Where(b => b.Row is null))
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the oil's density and the tube bore come from the cylinder step (the bore as one table cell), and every other input and table is entered.
        Assert.Equal(template.Bindings.Count(b => b.Row is null), made.Instances.Values.Sum(i => i.Links.Count));
        Assert.Equal(1, made.Instances.Values.Sum(i => i.CellLinks.Count));
        Assert.Equal("d_prs", Assert.Single(made.Instances["b1"].CellLinks).Link.OutputSymbol);
    }

    [Fact]
    public async Task A_Bigger_Cylinder_And_Pump_Make_Every_Downstream_Step_Stale_Refuse_The_Check_And_The_Issue_And_Updating_In_Order_Carries_The_Values_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var f1 = made.Instances["f1"];
        var e4Before = made.Instances["e4"];
        var e1Before = made.Instances["e1"];
        Assert.Equal(10e-6, Si(e4Before, "CSA"), 15);

        await ChainKit.ReviseAndUpdateAsync(host, made, "f1", Uprate, ["f2", "b1", "e1", "e4"]);

        var f1New = await Fresh(host, made, "f1");
        var (f2, b1, e1, e4) = (await Fresh(host, made, "f2"), await Fresh(host, made, "b1"), await Fresh(host, made, "e1"), await Fresh(host, made, "e4"));

        // The revision moved every value a link carries.
        Assert.NotEqual(Si(f1, "dV"), Si(f1New, "dV"));
        Assert.Equal(Math.PI * 0.11 * 0.11 / 4 * 0.5, Si(f1New, "dV"), 12);
        Assert.Equal(176e5, Si(f1New, "p"), 6);
        Assert.Equal(39.6e-3 / 60, Si(f1New, "Q"), 15);
        Assert.Equal(1595, Si(f1New, "n_shaft") * 60 / (2 * Math.PI), 9);
        Assert.Equal(1.1 * Si(f1, "T_shaft"), Si(f1New, "T_shaft"), 9);

        // ... and the update carried each one to its step.
        Assert.Equal(Si(f1New, "dV"), InputSi(f2, "dV"), 15);
        Assert.Equal(Si(f1New, "p"), InputSi(f2, "p_2"), 6);
        Assert.Equal(Si(f1New, "Q_prs"), InputSi(b1, "Q"), 15);
        Assert.Equal(71.30093055555555e-3 / 60, Si(f1New, "Q_prs"), 12);
        Assert.NotEqual(Si(f1, "Q_prs"), Si(f1New, "Q_prs"));
        Assert.Equal(Si(f1New, "T_shaft"), InputSi(e1, "T_L"), 12);
        Assert.Equal(Si(f1New, "n_shaft"), InputSi(e1, "n_L"), 9);
        Assert.Equal(Si(e1, "P_rated"), InputSi(e4, "P"), 6);

        // The faster pump (1595 rpm) changes the load speed only: the candidate motor's nameplate rated power is a property of the motor (120 N·m at 1460 rpm = 18.347 kW) and does not move,
        // so the cable stays 33.47 A, a 40 A device and 10 mm² (44.6 A).
        Assert.Equal(Si(e1Before, "P_rated"), Si(e1, "P_rated"), 9);
        Assert.Equal(18346.90109696439, Si(e1, "P_rated"), 6);
        Assert.Equal(Si(e1, "P_rated") / (Math.Sqrt(3) * 400 * 0.86 * 0.92), Si(e4, "I_b"), 9);
        Assert.Equal(33.47000849067186, Si(e4, "I_b"), 9);
        Assert.Equal(Si(e4Before, "I_b"), Si(e4, "I_b"), 9);
        Assert.Equal(40, Si(e4, "I_n"), 9);
        Assert.Equal(10e-6, Si(e4, "CSA"), 15);
        Assert.Equal(44.6, Si(e4, "I_z"), 9);
        Assert.All(new[] { f2, b1, e1, e4 }, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task Revising_The_Cylinder_Line_Bore_Makes_The_Pressure_Line_Stale_And_The_Update_Carries_The_Bore_Into_Its_Segment()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var f1 = made.Instances["f1"];
        var b1Before = made.Instances["b1"];
        Assert.Equal(20e-3, CellSi(b1Before, "segments", 0, "D"), 15);

        IReadOnlyList<CalcInputValue> Rebore(IReadOnlyList<CalcInputValue> inputs) => [.. inputs.Select(i => i.Symbol == "d_prs" ? i with { Number = 25 } : i)];
        var revised = await host.Service.UpdateInputsAsync(f1.Id, Rebore(f1.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);

        // Only the pressure line takes the bore, so only it goes stale, and the reason names the cell and the cylinder output it follows.
        var staleness = await host.Service.StalenessAsync(await Fresh(host, made, "b1"));
        Assert.True(staleness.IsStale);
        Assert.Contains(staleness.Reasons, r => r.Contains("segments", StringComparison.Ordinal) && r.Contains("d_prs", StringComparison.Ordinal));

        foreach (var key in new[] { "f2", "e1", "e4" })
            Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, key);

        Assert.True((await host.Service.UpdateLinksAsync(b1Before.Id)).Succeeded);
        var b1 = await Fresh(host, made, "b1");
        Assert.Equal(25e-3, CellSi(b1, "segments", 0, "D"), 15);
        Assert.False((await host.Service.StalenessAsync(b1)).IsStale);

        // The wider line slows the flow: 75.63 L/min in 25 mm is 2.568 m/s, Re 1861 (laminar again, f = 64/Re).
        Assert.Equal(75.62745098039215e-3 / 60 / (Math.PI * 0.025 * 0.025 / 4), Si(b1, "v_s1"), 9);
        Assert.Equal(1861.6426509446035, Si(b1, "Re_s1"), 6);
        Assert.Contains(b1.Warnings, w => w.RuleId == "laminar");
        Assert.DoesNotContain(b1.Warnings, w => w.RuleId == "transition");
    }

    [Fact]
    public async Task While_Stale_The_Pack_Check_And_Issue_Are_Refused_With_Reasons_That_Name_The_Stale_Step()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var f1 = made.Instances["f1"];

        var revised = await host.Service.UpdateInputsAsync(f1.Id, Uprate(f1.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);

        foreach (var key in new[] { "f2", "b1", "e1", "e4" })
        {
            var staleness = await host.Service.StalenessAsync(await Fresh(host, made, key));
            Assert.True(staleness.IsStale, $"{key} should be stale");
        }

        // The direct links say which value moved; E4 takes nothing from F1 and is stale only because E1, which it is linked to, is.
        var f2Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "f2"))).Reasons;
        Assert.Contains(f2Reasons, r => r.Contains("'dV'", StringComparison.Ordinal) && r.Contains("different 'dV'", StringComparison.Ordinal));
        Assert.Contains(f2Reasons, r => r.Contains("'p_2'", StringComparison.Ordinal) && r.Contains("different 'p'", StringComparison.Ordinal));
        var b1Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "b1"))).Reasons;
        Assert.Contains(b1Reasons, r => r.Contains("'Q'", StringComparison.Ordinal) && r.Contains("different 'Q_prs'", StringComparison.Ordinal));
        var e1Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e1"))).Reasons;
        Assert.Contains(e1Reasons, r => r.Contains("'T_L'", StringComparison.Ordinal) && r.Contains("different 'T_shaft'", StringComparison.Ordinal));
        Assert.Contains(e1Reasons, r => r.Contains("'n_L'", StringComparison.Ordinal) && r.Contains("different 'n_shaft'", StringComparison.Ordinal));
        var e4Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e4"))).Reasons;
        Assert.Contains(e4Reasons, r => r.Contains("'Pump drive motor', which 'P' is linked to, is itself stale", StringComparison.Ordinal));

        host.Principal = "j.reviewer";
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("Pack member 'Emergency stroke accumulator' is stale", check.Reason, StringComparison.Ordinal);
        Assert.Contains("Update it before checking the pack", check.Reason, StringComparison.Ordinal);

        var issue = await host.Service.IssuePackAsync(made.Pack.Id, "HPU-STALE", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("check it before issuing", issue.Reason, StringComparison.Ordinal);
        Assert.NotEqual(CalcInstanceStatus.Issued, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);
    }

    [Fact]
    public async Task The_Chain_Checks_And_Issues_As_One_Bundle_With_Every_Link_On_The_Sheet_Once_The_Cable_Step_Is_Independently_Checked()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // The cable step demands an independent check: the engineer who drew the chain up cannot check it.
        var self = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(self.Succeeded);
        Assert.Contains("needs an independent check", self.Reason, StringComparison.Ordinal);

        // Another engineer is recorded as the author of every step; full provenance, one check, one issue.
        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.HydraulicPowerUnit, "HPU-001", "Client Co", "Checked against SPEC-HPU-05.");

        Assert.Contains("link: Cylinder and pump circuit rev A · dV", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cylinder and pump circuit rev A · p", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cylinder and pump circuit rev A · Q_prs", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cylinder and pump circuit rev A · rho", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cylinder and pump circuit rev A · d_prs", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cylinder and pump circuit rev A · T_shaft", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cylinder and pump circuit rev A · n_shaft", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Pump drive motor rev A · P_rated", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_The_Owners_Tables_The_Cable_Step_Opens_Blocked_Naming_Them_And_The_Pack_Cannot_Be_Checked()
    {
        var host = Host();
        var template = CalcPackTemplates.HydraulicPowerUnit;

        // The shipped example holds no table, and every step but the cable's is clean on its example.
        Assert.All(template.Steps, s => Assert.NotEmpty(s.Example ?? []));
        var cable = host.Service.ExampleInputs(template, template.Steps.Single(s => s.Key == "e4"))!;
        Assert.DoesNotContain(cable, v => v.Symbol is "ampacity" or "ratings");
        Assert.DoesNotContain(template.Steps.Single(s => s.Key == "e4").Example!, v => v.Symbol is "ampacity" or "ratings");

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, await host.ProjectAsync(), "Press 5 hydraulic power unit", null);

        // Nothing takes from the cable step, so the chain is created and the cable step is the one that is blocked (a pack cannot be checked with a blocked member).
        Assert.True(refusal is null, refusal);
        Assert.Equal(5, made!.Pack.InstanceIds.Count);
        Assert.All(new[] { "f1", "f2", "b1", "e1" }, key => Assert.Empty(made.Instances[key].Blocks));
        var blocks = made.Instances["e4"].Blocks;
        var block = Assert.Single(blocks);
        Assert.StartsWith("ampacity:", block.Message, StringComparison.Ordinal);
        Assert.Contains("own licensed table", block.Message, StringComparison.Ordinal);

        // Every input is an example the user must replace, or a link: nothing reads as a project figure.
        Assert.All(made.Instances.Values, i => Assert.All(i.Inputs, input =>
            Assert.True(input.Provenance is { Kind: CalcProvenanceKind.Link } || (input.Provenance?.Kind == CalcProvenanceKind.Assumption && input.Provenance.Reference == CalcInstanceService.ExampleBasis), $"{i.Name}.{input.Symbol}: {input.Provenance}")));

        await AuthoredByAnotherAsync(host, made);
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
    }

    [Fact]
    public async Task The_Cable_Step_Given_Only_Its_Tables_Completes_The_Example_Chain()
    {
        var host = Host();
        var tables = ChainInputs.HydraulicPowerUnit(host).Where(kv => kv.Key == "e4").ToDictionary(kv => kv.Key, kv => kv.Value);
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.HydraulicPowerUnit, await host.ProjectAsync(), "Press 5 hydraulic power unit", tables);

        Assert.True(refusal is null, refusal);
        Assert.All(made!.Instances.Values, i => Assert.False(i.Blocks.Count > 0, i.Name));
        Assert.Equal(40, Si(made.Instances["e4"], "I_n"), 9);
    }
}
