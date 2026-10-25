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
/// accumulator its stroke volume and supply pressure, the pressure line its flow, and the pump motor its load torque and speed; the motor's continuous shaft power
/// sizes the supply cable. The cable step's two tables are SYNTHETIC test values (never a regulation or manufacturer table); the shipped example omits them.
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

        // F1 1.1.0 moved these (independent hand recompute): the rod is judged at the cylinder's thrust (119.38 kN, above the 80 kN push), and the 16 mm
        // cap-port line at the retract flow Q·A_b/A_a = 75.63 L/min, 6.27 m/s against the 5 m/s placeholder limit, so that one F1 check now fails.
        var thrust = 160e5 * areaBore * 0.95;
        Assert.Equal(thrust * 3.5 / 1787806.9776735522, Si(f1, "u_buck"), 9);
        var areaRod = Math.PI * 0.07 * 0.07 / 4;
        var capFlow = 28e-6 * 1450 / 60 * 0.95 * areaBore / (areaBore - areaRod);
        Assert.Equal(capFlow / (Math.PI * 0.016 * 0.016 / 4), Si(f1, "v_prs"), 9);
        Assert.Equal(6.269001383838239, Si(f1, "v_prs"), 9);
        Assert.Equal(28e-6 * 1450 / 60 / (Math.PI * 0.04 * 0.04 / 4), Si(f1, "v_suc"), 9);
        Assert.Equal(["Pressure line velocity within its limit, cap-port line at the larger stroke flow"], f1.Checks.Where(c => !c.Passed).Select(c => c.Name));

        // F1 → F2, swept volume ΔV (L): the emergency stroke is the whole larger stroke, 3.927 L.
        Assert.Equal(Si(f1, "dV"), InputSi(f2, "dV"), 15);
        Assert.Equal("Emergency stroke", f2.Inputs.Single(i => i.Symbol == "mode").Text);

        // F1 → F2, supply pressure p (bar) is the accumulator's highest working pressure p_2; p_1 stays above the pressure the 80 kN push needs (80 kN / (A_b·η_m)).
        Assert.Equal(Si(f1, "p"), InputSi(f2, "p_2"), 6);
        Assert.True(InputSi(f2, "p_1") > 80e3 / (areaBore * 0.95), "the lowest working pressure must still finish the stroke under load");
        Assert.Equal(107.2, 80e3 / (areaBore * 0.95) / 1e5, 1);

        // F2: V0 = f_T·ΔV/[(p0/p1)^(1/n) − (p0/p2)^(1/n)] on absolute pressures with n = 1.4 and a 10 to 50 °C range.
        Assert.Equal(26.69518127508491e-3, Si(f2, "V0"), 12);
        Assert.Equal(0.9733851458414678, Si(f2, "u_r0"), 9);
        Assert.Equal(0.43334029084376174, Si(f2, "u_rp"), 9);
        Assert.Equal(2, f2.Checks.Count);
        Assert.All(f2.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.DoesNotContain(f2.Warnings, w => w.RuleId is "exponent" or "realgas" or "shock");

        // F1 → B1, circuit flow Q (L/min into m³/h): 36 L/min is 2.16 m³/h, and it is the only value B1 takes by link.
        Assert.Equal(Si(f1, "Q"), InputSi(b1, "Q"), 15);
        Assert.Equal(2.16, Si(b1, "Q_design") * 3600, 12);
        Assert.Equal(["Q"], b1.Links.Select(l => l.Input.Symbol));

        // B1: the oil is a custom fluid whose density and the tube bore are entered equal to F1's rho and d_prs (F1 offers no output for either): this guards the match.
        Assert.Equal(InputSi(f1, "rho"), InputSi(b1, "rho_custom"), 12);
        Assert.Equal(870, InputSi(b1, "rho_custom"), 12);
        Assert.Equal(InputSi(f1, "d_prs"), CellSi(b1, "segments", 0, "D"), 12);
        Assert.Equal(16e-3, CellSi(b1, "segments", 0, "D"), 15);

        // B1: laminar (Re 1385), so f = 64/Re and Δp = 32 μ L v/D² + K ρ v²/2 = 134.287 + 23.243 = 157.530 kPa; the line takes about 1 % of the supply pressure.
        var velocity = 36e-3 / 60 / (Math.PI * 0.016 * 0.016 / 4);
        var reynolds = 870 * velocity * 0.016 / 0.03;
        Assert.Equal(velocity, Si(b1, "v_s1"), 9);
        Assert.Equal(1384.6480048994895, reynolds, 6);
        Assert.Equal(reynolds, Si(b1, "Re_s1"), 6);
        Assert.Equal(64 / reynolds, Si(b1, "f_s1"), 9);
        var laminar = 32 * 0.03 * 12 * velocity / (0.016 * 0.016);
        var fittings = 6 * 870 * velocity * velocity / 2;
        Assert.Equal(157529.5086611159, laminar + fittings, 3);
        Assert.Equal(laminar + fittings, Si(b1, "dp_total"), 3);
        Assert.Equal(Si(b1, "dp_total"), Si(b1, "dp_sys"), 6);
        Assert.True(Si(b1, "dp_total") < 0.02 * Si(f1, "p"));
        Assert.Contains(b1.Warnings, w => w.RuleId == "laminar");
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

        // E1 → E4, continuous shaft power P_cont (W into kW): E4 takes a shaft power directly, so there is no load-budget step and no design current is linked.
        Assert.Equal(Si(e1, "P_cont"), InputSi(e4, "P"), 6);
        Assert.Equal(11.805938169574532, InputSi(e4, "P") / 1e3, 9);
        Assert.DoesNotContain(e4.Inputs, i => i.Symbol == "I_b_in");
        Assert.Equal(["P"], e4.Links.Select(l => l.Input.Symbol));

        // E4: I_b = P/(√3 U cos φ η) = 21.537 A; the type C device is the 25 A rating and the synthetic capacities give 6 mm² (31.9 A).
        Assert.Equal(11805.938169574532 / (Math.Sqrt(3) * 400 * 0.86 * 0.92), Si(e4, "I_b"), 9);
        Assert.Equal(21.537416519969458, Si(e4, "I_b"), 9);
        Assert.Equal(25, Si(e4, "I_n"), 9);
        Assert.Equal(6e-6, Si(e4, "CSA"), 15);
        Assert.Equal(31.9, Si(e4, "I_z"), 9);
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
            [("f1", "dV", "f2", "dV"), ("f1", "p", "f2", "p_2"), ("f1", "Q", "b1", "Q"), ("f1", "T_shaft", "e1", "T_L"), ("f1", "n_shaft", "e1", "n_L"), ("e1", "P_cont", "e4", "P")],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));

        foreach (var b in template.Bindings)
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the oil's density and the tube bore are entered, and so is every table.
        Assert.Equal(template.Bindings.Count, made.Instances.Values.Sum(i => i.Links.Count));
        Assert.All(made.Instances.Values, i => Assert.Empty(i.CellLinks));
    }

    [Fact]
    public async Task A_Bigger_Cylinder_And_Pump_Make_Every_Downstream_Step_Stale_Refuse_The_Check_And_The_Issue_And_Updating_In_Order_Carries_The_Values_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var f1 = made.Instances["f1"];
        var e4Before = made.Instances["e4"];
        Assert.Equal(6e-6, Si(e4Before, "CSA"), 15);

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
        Assert.Equal(Si(f1New, "Q"), InputSi(b1, "Q"), 15);
        Assert.Equal(Si(f1New, "T_shaft"), InputSi(e1, "T_L"), 12);
        Assert.Equal(Si(f1New, "n_shaft"), InputSi(e1, "n_L"), 9);
        Assert.Equal(Si(e1, "P_cont"), InputSi(e4, "P"), 6);

        // The bigger pump takes more power and the supply cable grows with it: 14.3 kW, 26.1 A, a 32 A device and 10 mm² (the 6 mm² capacity of 31.9 A is below 32 A).
        Assert.True(Si(e1, "P_cont") > 1.2 * 11805.938169574532);
        Assert.Equal(Si(e1, "P_cont") / (Math.Sqrt(3) * 400 * 0.86 * 0.92), Si(e4, "I_b"), 9);
        Assert.Equal(32, Si(e4, "I_n"), 9);
        Assert.Equal(10e-6, Si(e4, "CSA"), 15);
        Assert.Equal(44.6, Si(e4, "I_z"), 9);
        Assert.NotEqual(Si(e4Before, "I_b"), Si(e4, "I_b"));
        Assert.All(new[] { f2, b1, e1, e4 }, i => Assert.Empty(i.Blocks));
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
        Assert.Contains(b1Reasons, r => r.Contains("'Q'", StringComparison.Ordinal) && r.Contains("different 'Q'", StringComparison.Ordinal));
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
        Assert.Contains("link: Cylinder and pump circuit rev A · Q", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cylinder and pump circuit rev A · T_shaft", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cylinder and pump circuit rev A · n_shaft", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Pump drive motor rev A · P_cont", sheet, StringComparison.Ordinal);
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
        Assert.Equal(25, Si(made.Instances["e4"], "I_n"), 9);
    }
}
