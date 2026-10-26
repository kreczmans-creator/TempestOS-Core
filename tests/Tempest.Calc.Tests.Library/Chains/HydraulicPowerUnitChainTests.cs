using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B, §B7 chain 1: the hydraulic power unit F1 → F2, B1, E1 → E4 → E3, E5 → E9 from the built-in pack template. The cylinder and pump circuit gives the
/// accumulator its stroke volume and supply pressure, the pressure line (the line to the cylinder cap port) its flow, oil density and bore, and the pump motor its
/// load torque and speed; the candidate motor's rated shaft power sizes the supply cable, whose design current, length and conductor area give the voltage drop and (with the device) the earth-fault
/// check, whose transformer gives the supply fault current the motor start dips against, on that cable. The cable step's two tables are SYNTHETIC test values (never a regulation or manufacturer table); the shipped example omits them.
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
        var (e3, e5, e9) = (made.Instances["e3"], made.Instances["e5"], made.Instances["e9"]);
        Assert.Equal(8, made.Pack.InstanceIds.Count);

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
        Assert.Equal(stall * 3.5 / 1465707.6880828117, Si(f1, "u_buck"), 9);
        Assert.Equal(0.3000755028295391, Si(f1, "u_buck"), 9);
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

        // chain.hydraulic-power-unit-02: F1's p_push (the pressure the push load needs at the cylinder, gauge) is F2's p_load, so F2 checks that its lowest working pressure p_1 still carries the push.
        Assert.Equal(80e3 / (areaBore * 0.95), Si(f1, "p_push"), 6);
        Assert.Equal(Si(f1, "p_push"), InputSi(f2, "p_load"), 6);
        Assert.Equal(Si(f1, "p_push") / 120e5, Si(f2, "u_load"), 9);
        Assert.Contains(f2.Links, l => l.Input.Symbol == "p_load");
        Assert.DoesNotContain(f2.Warnings, w => w.RuleId == "load-pressure");

        // F2 (1.3.0): an emergency stroke is held at p2, charged slowly (isothermal) and discharged at n = 1.4: V0 = f_T·ΔV·(p2/p0)/[(p2/p1)^(1/n) − 1] on absolute pressures, a 10 to 50 °C range.
        // python3: ΔV 3.926990816987242 L, p0 106.01325, p1 121.01325, p2 161.01325 bar abs, f_T 323.15/283.15: 30.080873024534817 L (the one-exponent form gave 26.695181275084906 L, 12.7 % less).
        // chain.hydraulic-power-unit-01: the ideal gas is short for nitrogen at 161 bar abs; the pack enters the real-gas factor f_rg = 1.311 (reference equation of state for nitrogen, hold at 160 bar gauge at 10 °C then the isentropic stroke:
        // 39.432 L against the 30.081 L of the ideal gas), so V0 = 1.311 × 30.080873024534817 = 39.4360 L, which covers the 39.43 L the stroke needs.
        Assert.Equal(1.311, InputSi(f2, "f_rg"), 12);
        Assert.Equal(30.080873024534817e-3 * 1.311, Si(f2, "V0"), 12);
        Assert.Equal(26.695181275084906e-3 * 1.1268278238893188 * 1.311, Si(f2, "V0"), 12);
        Assert.InRange(Si(f2, "V0") * 1e3, 39.432, 39.44);
        Assert.Equal(0.9733851458414678, Si(f2, "u_r0"), 9);
        Assert.Equal(0.43334029084376174, Si(f2, "u_rp"), 9);
        Assert.Equal(3, f2.Checks.Count);
        Assert.All(f2.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.DoesNotContain(f2.Warnings, w => w.RuleId is "exponent" or "realgas" or "shock" or "process");

        // fp.accumulator-R2: n = 1.4 is the ideal-gas exponent; the sheet says its real-gas factor is applied on top and must be the discharge path's.
        Assert.Contains("applied on top", Assert.Single(f2.Warnings, w => w.RuleId == "exponent-real").Message, StringComparison.Ordinal);
        Assert.Contains("charged slowly to p_2", f2.Outputs.Single(o => o.Symbol == "process").Text, StringComparison.Ordinal);

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

        // E4 → E3: the design current I_b (A), the length L (m) and the conductor area CSA (mm²) are the cable the voltage drop is judged on (three-phase, 400 V; the power factor is the motor's, typed equal to E4's).
        Assert.Equal(["A_c", "I", "L"], e3.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(Si(e4, "I_b"), InputSi(e3, "I"), 9);
        Assert.Equal(Si(e4, "L"), InputSi(e3, "L"), 9);
        Assert.Equal(Si(e4, "CSA"), InputSi(e3, "A_c"), 15);
        Assert.Equal(InputSi(e4, "cos_phi"), InputSi(e3, "cos_phi"), 15);
        Assert.Equal(InputSi(e4, "U"), InputSi(e3, "V_n"), 12);

        // E3: ρ_70 = 0.01724 (1 + 0.00393 · 50) = 0.0206277 Ω·mm²/m; R = ρ_70 L/A = 0.061883 Ω; X = 0.08 Ω/km · 30 m = 0.0024 Ω; ΔV = √3 I (R cos φ + X sin φ) = 3.1562 V (0.789 % of 400 V).
        var rho70 = 0.01724 * (1 + 0.00393 * 50);
        Assert.Equal(rho70 * 30 / 10, Si(e3, "R"), 12);
        Assert.Equal(0.06188297999999999, Si(e3, "R"), 12);
        Assert.Equal(0.0024, Si(e3, "X"), 12);
        Assert.Equal(Math.Sqrt(3) * Si(e4, "I_b") * (Si(e3, "R") * 0.86 + Si(e3, "X") * Math.Sqrt(1 - 0.86 * 0.86)), Si(e3, "dV"), 9);
        Assert.Equal(3.1562182118909865, Si(e3, "dV"), 9);
        Assert.Equal(0.7890545529727465, Si(e3, "dV_pct"), 9);
        Assert.Equal(207.97164114612576, Si(e3, "P_loss"), 6);
        Assert.Single(e3.Checks);
        Assert.All(e3.Checks, c => Assert.True(c.Passed, c.Name));

        // E4 → E5: the conductor area (the line conductor), the length, the device rating and the device type code; the protective conductor is typed the cable's size (guarded), and the transformer is entered here, once.
        Assert.Equal(["I_n", "L", "S_line", "device_code"], e5.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(Si(e4, "CSA"), InputSi(e5, "S_line"), 15);
        Assert.Equal(Si(e4, "CSA"), InputSi(e5, "S_cpc"), 15);
        Assert.Equal(Si(e4, "L"), InputSi(e5, "L"), 9);
        Assert.Equal(Si(e4, "I_n"), InputSi(e5, "I_n"), 9);
        Assert.Equal(Si(e4, "device_code"), InputSi(e5, "device_code"), 9);

        // E5: R1 + R2 = ρ_70 L (1/10 + 1/10) with ρ_20 = 17.241·10⁻⁶ Ω·mm = 0.123773 Ω; Z_s = 0.05 + 0.123773 = 0.173773 Ω (the 0.05 Ω external loop of a close-coupled 400 kVA transformer, invented); I_f = 0.95 · 230/Z_s = 1257.39 A against I_a = 10 · 40 = 400 A; Z_T = 0.04 · (√3 · 230)² / 400 kVA = 0.01587 Ω;
        // I_k3 = 1.1 · √3 · 230 / (√3 · Z_T) = 15.942 kA, 63.8 % of the 25 kA breaking capacity.
        var r12 = 17.241e-3 * (1 + 0.00393 * 50) * 30 * (1.0 / 10 + 1.0 / 10);
        Assert.Equal(r12, Si(e5, "R_12"), 12);
        Assert.Equal(0.12377313899999998, Si(e5, "R_12"), 12);
        Assert.Equal(0.05 + r12, Si(e5, "Z_s"), 12);
        Assert.Equal(0.95 * 230 / (0.05 + r12), Si(e5, "I_f"), 9);
        Assert.Equal(1257.3865055174035, Si(e5, "I_f"), 9);
        Assert.Equal(400, Si(e5, "I_a"), 9);
        Assert.Equal(0.54625, Si(e5, "Z_s_max"), 12);
        Assert.Equal(0.3181201629290617, Si(e5, "u_Zs"), 9);
        Assert.Equal(114.83612782557213, Si(e5, "k"), 9);
        Assert.Equal(Math.Sqrt(8e4) / 114.83612782557213, Si(e5, "S_min") * 1e6, 9);
        Assert.Equal(0.04 * (Math.Sqrt(3) * 230) * (Math.Sqrt(3) * 230) / 400e3, Si(e5, "Z_T"), 12);
        Assert.Equal(0.01587, Si(e5, "Z_T"), 12);
        Assert.Equal(1.1 * Math.Sqrt(3) * 230 / (Math.Sqrt(3) * 0.01587), Si(e5, "I_k3"), 6);
        Assert.Equal(15942.028985507251, Si(e5, "I_k3"), 6);
        Assert.Equal(0.63768115942029, Si(e5, "u_Icn"), 9);
        Assert.Equal(1.1, Si(e5, "c_k3"), 12);
        Assert.Equal(5, e5.Checks.Count);
        Assert.All(e5.Checks, c => Assert.True(c.Passed, c.Name));

        // E1 → E9 (rated power, W into kW), E4 → E9 (conductor area and length) and E5 → E9 (the three-phase fault current, kA): the start is judged on the cable E4 sized and the supply E5 protects.
        Assert.Equal(["A_c", "I_k", "L_cab", "P", "c_src"], e9.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(Si(e5, "c_k3"), InputSi(e9, "c_src"), 12);
        Assert.Equal("c_k3", e9.Links.Single(l => l.Input.Symbol == "c_src").Link.OutputSymbol);
        Assert.Equal(Si(e1, "P_rated"), InputSi(e9, "P"), 6);
        Assert.Equal(Si(e4, "CSA"), InputSi(e9, "A_c"), 15);
        Assert.Equal(Si(e4, "L"), InputSi(e9, "L_cab"), 9);
        Assert.Equal(Si(e5, "I_k3"), InputSi(e9, "I_k"), 6);
        Assert.Equal("P_rated", e9.Links.Single(l => l.Input.Symbol == "P").Link.OutputSymbol);
        Assert.Equal("I_k3", e9.Links.Single(l => l.Input.Symbol == "I_k").Link.OutputSymbol);

        // The motor data E9 and E4 each carry are the same motor's, and E9's rated current is E4's design current (so neither step has drifted from the other).
        Assert.Equal(InputSi(e4, "eta"), InputSi(e9, "eta"), 15);
        Assert.Equal(InputSi(e4, "cos_phi"), InputSi(e9, "cos_phi"), 15);
        Assert.Equal(InputSi(e4, "U"), InputSi(e9, "U"), 12);
        Assert.Equal(Si(e4, "I_b"), Si(e9, "I_rated"), 9);

        // E9: star-delta start, I_start = 7 I_rated / 3 = 78.097 A, |Z_m| = (400/√3)/I_start = 2.9571 Ω at cos φ_st 0.3; |Z_s| = 1.1 · 400/(√3 · 15.942 kA) = 0.0159349 Ω at X/R 4.2;
        // the complex divider with the cable's 0.061883 + j0.0024 Ω gives |V_m| = 395.018 V (a 1.2455 % dip at the motor terminals) and |V_pcc| = 397.880 V (0.5300 %); Q_c to 0.95 = 5.2783 kvar; S_sc = U²/|Z_s| = 10.041 MVA.
        Assert.Equal("Star-delta", e9.Inputs.Single(i => i.Symbol == "starter").Text);
        Assert.Equal(7 * Si(e4, "I_b") / 3, Si(e9, "I_start"), 9);
        Assert.Equal(78.09668647823433, Si(e9, "I_start"), 9);
        Assert.Equal(0.01593486742963367, Si(e9, "Z_src"), 12);
        Assert.Equal(2.957105071803702, Si(e9, "Z_m"), 9);
        Assert.Equal(77.12399429122192, Si(e9, "I_dip"), 9);
        Assert.Equal(395.0180104643313, Si(e9, "V_m"), 8);
        Assert.Equal(397.8800479514327, Si(e9, "V_pcc"), 8);
        Assert.Equal(1.2454973839171712, Si(e9, "start_dip") * 100, 9);
        Assert.Equal(0.5299880121418354, Si(e9, "dip_pcc") * 100, 9);
        Assert.Equal(2.7857796356254783, Si(e9, "dV_cab"), 9);
        Assert.Equal(19942.28380104825, Si(e9, "P_in"), 6);
        Assert.Equal(5278.344602713781, Si(e9, "kvar"), 6);
        Assert.Equal(7.618634193131024, Si(e9, "I_cap"), 9);
        Assert.Equal(10040874.2467761, Si(e9, "S_sc"), 3);
        Assert.Equal(43.61510214012638, Si(e9, "h_res"), 9);
        Assert.Single(e9.Checks);
        Assert.All(e9.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.DoesNotContain(e9.Warnings, w => w.RuleId == "c_src");

        // Starting-current coordination (I_start is not a design current): the type C device's instantaneous band starts at 5 I_n (200 A for the 40 A device), so the star-delta start (78 A) stays clear of it;
        // the same motor started direct on line would draw 7 I_rated = 234 A, inside the 200 to 400 A band, where the device may trip on the start. (The band is a device characteristic, entered here, not shipped.)
        var lowestTrip = 5 * Si(e4, "I_n");
        Assert.True(Si(e9, "I_start") < lowestTrip);
        Assert.True(7 * Si(e9, "I_rated") is > 200 and < 400);
        Assert.Equal(234.290059434703, 7 * Si(e9, "I_rated"), 9);
        Assert.Equal(10 * Si(e4, "I_n"), Si(e5, "I_a"), 9);

        // Star-start torque: in star the motor gives about a third of its direct-on-line torque, and E1 judges its run-up at the direct-on-line peak torque T_peak. A pump held at relief pressure is at full load: E1's SF · T_pk
        // (107.16 N·m) exceeds T_peak/3 (91.67 N·m), so a LOADED star start fails E1's own criterion. The pack starts the pump UNLOADED (vent or unloading valve to tank), with an INVENTED unloaded torque of 20 N·m (typed here, not shipped):
        // SF · (T_acc + 20) = 34.97 N·m, inside T_peak/3.
        var starTorque = InputSi(e1, "T_peak") / 3;
        var safety = InputSi(e1, "SF");
        const double unloadedTorque = 20;
        Assert.True(starTorque < safety * Si(e1, "T_pk"), "a loaded star start is not covered");
        Assert.True(starTorque >= safety * (Si(e1, "T_acc") + unloadedTorque), "an unloaded star start is covered");
        Assert.Equal(34.96857363358299, safety * (Si(e1, "T_acc") + unloadedTorque), 8);

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

        Assert.Equal(["f1", "f2", "b1", "e1", "e4", "e3", "e5", "e9"], template.Steps.Select(s => s.Key));
        Assert.Equal(
            [
                ("f1", "dV", "f2", "dV"), ("f1", "p", "f2", "p_2"), ("f1", "p_push", "f2", "p_load"), ("f1", "Q_prs", "b1", "Q"), ("f1", "rho", "b1", "rho_custom"), ("f1", "d_prs", "b1", "segments"), ("f1", "T_shaft", "e1", "T_L"), ("f1", "n_shaft", "e1", "n_L"), ("e1", "P_rated", "e4", "P"),
                ("e4", "I_b", "e3", "I"), ("e4", "L", "e3", "L"), ("e4", "CSA", "e3", "A_c"),
                ("e4", "CSA", "e5", "S_line"), ("e4", "L", "e5", "L"), ("e4", "I_n", "e5", "I_n"), ("e4", "device_code", "e5", "device_code"),
                ("e1", "P_rated", "e9", "P"), ("e4", "CSA", "e9", "A_c"), ("e4", "L", "e9", "L_cab"), ("e5", "I_k3", "e9", "I_k"), ("e5", "c_k3", "e9", "c_src"),
            ],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.Equal(21, template.Bindings.Count);
        Assert.Equal((0, "D"), (template.Bindings.Single(b => b.Input == "segments").Row, template.Bindings.Single(b => b.Input == "segments").Column));

        foreach (var b in template.Bindings.Where(b => b.Row is null))
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the oil's density and the tube bore come from the cylinder step (the bore as one table cell), and every other input and table is entered. E9's rated current is deliberately NOT linked to E4
        // (E4 → E5 → E9 → E4 would be a cycle): E4 sizes on E1's rated power, and the two steps' copies of the motor's efficiency and power factor are guarded equal.
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

        // The revision reaches the last calculation: the motor start (E9), four links away from F1 through E1, E4 and E5.
        await ChainKit.ReviseAndUpdateAsync(host, made, "f1", Uprate, ["f2", "b1", "e1", "e4", "e3", "e5", "e9"]);

        var f1New = await Fresh(host, made, "f1");
        var (f2, b1, e1, e4) = (await Fresh(host, made, "f2"), await Fresh(host, made, "b1"), await Fresh(host, made, "e1"), await Fresh(host, made, "e4"));
        var (e3, e5, e9) = (await Fresh(host, made, "e3"), await Fresh(host, made, "e5"), await Fresh(host, made, "e9"));

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

        // ... so the whole electrical tail re-pins to the same cable and the same start: the pack is re-checked on current members, not on a different design.
        Assert.Equal(Si(e4, "I_b"), InputSi(e3, "I"), 9);
        Assert.Equal(3.1562182118909865, Si(e3, "dV"), 9);
        Assert.Equal(Si(e4, "CSA"), InputSi(e5, "S_line"), 15);
        Assert.Equal(15942.028985507251, Si(e5, "I_k3"), 6);
        Assert.Equal(Si(e5, "I_k3"), InputSi(e9, "I_k"), 6);
        Assert.Equal(Si(e1, "P_rated"), InputSi(e9, "P"), 6);
        Assert.Equal(1.2454973839171712, Si(e9, "start_dip") * 100, 9);
        Assert.All(new[] { f2, b1, e1, e4, e3, e5, e9 }, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task A_Heavier_Push_Load_Fails_The_Accumulator_Because_Its_Lowest_Pressure_No_Longer_Carries_It()
    {
        // chain.hydraulic-power-unit-02: F_push 100 kN needs 134.0 bar at the 100 mm bore (100 kN/(78.54 cm² x 0.95)), and the accumulator still ends its stroke at p_1 = 120 bar: before the link F1 passed (u_push 0.838),
        // F2 sized V0 as before and all 16 checks were green. Now F2 takes the pressure the push needs from F1 and fails.
        var host = Host();
        var inputs = ChainInputs.HydraulicPowerUnit(host);
        inputs["f1"] = [.. inputs["f1"].Select(v => v.Symbol == "F_push" ? CalcInputValue.Quantity("F_push", 100, "kN", v.Provenance) : v)];
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.HydraulicPowerUnit, await host.ProjectAsync(), "Press 7 hydraulic power unit", inputs);
        Assert.True(refusal is null, refusal);
        var (f1, f2) = (made!.Instances["f1"], made.Instances["f2"]);

        var needed = 100e3 / (Math.PI * 0.1 * 0.1 / 4 * 0.95);
        Assert.Equal(needed, Si(f1, "p_push"), 6);
        Assert.Equal(134.0, Si(f1, "p_push") / 1e5, 1);
        Assert.True(f1.Checks.Single(c => c.Name.StartsWith("Push load", StringComparison.Ordinal) || c.Name.StartsWith("Extend force", StringComparison.Ordinal)).Passed);
        Assert.Equal(needed / 120e5, Si(f2, "u_load"), 9);
        Assert.Contains(f2.Checks, c => c.Name.StartsWith("Lowest working pressure p_1 carries the load", StringComparison.Ordinal) && !c.Passed);
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

        foreach (var key in new[] { "f2", "b1", "e1", "e4", "e3", "e5", "e9" })
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

        // The electrical tail is stale only through the steps it is linked to: E3 and E5 through the cable, E9 through the motor, the cable and the fault protection.
        var e3Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e3"))).Reasons;
        Assert.Contains(e3Reasons, r => r.Contains("'Motor supply cable', which 'I' is linked to, is itself stale", StringComparison.Ordinal));
        var e5Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e5"))).Reasons;
        Assert.Contains(e5Reasons, r => r.Contains("'Motor supply cable', which 'S_line' is linked to, is itself stale", StringComparison.Ordinal));
        var e9Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e9"))).Reasons;
        Assert.Contains(e9Reasons, r => r.Contains("'Pump drive motor', which 'P' is linked to, is itself stale", StringComparison.Ordinal));
        Assert.Contains(e9Reasons, r => r.Contains("'Motor supply fault protection', which 'I_k' is linked to, is itself stale", StringComparison.Ordinal));

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
        Assert.Contains("link: Motor supply cable rev A · I_b", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Motor supply cable rev A · CSA", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Motor supply cable rev A · device_code", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Motor supply fault protection rev A · I_k3", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Motor supply fault protection rev A · c_k3", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_The_Owners_Tables_The_Pack_Is_Refused_At_The_Cable_Step_Naming_Them_And_Creates_Nothing()
    {
        var host = Host();
        var template = CalcPackTemplates.HydraulicPowerUnit;
        var project = await host.ProjectAsync();

        // The shipped example holds no table, and every step but the cable's is clean on its example.
        Assert.All(template.Steps, s => Assert.NotEmpty(s.Example ?? []));
        var cable = host.Service.ExampleInputs(template, template.Steps.Single(s => s.Key == "e4"))!;
        Assert.DoesNotContain(cable, v => v.Symbol is "ampacity" or "ratings");
        Assert.DoesNotContain(template.Steps.Single(s => s.Key == "e4").Example!, v => v.Symbol is "ampacity" or "ratings");

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, project, "Press 5 hydraulic power unit", null);

        // The voltage drop, the fault protection and the motor start take from the cable step, so the chain is refused there, as the motor circuit is, and nothing is left behind.
        Assert.Null(made);
        Assert.StartsWith("Step 'e4' is blocked", refusal, StringComparison.Ordinal);
        Assert.Contains("ampacity:", refusal, StringComparison.Ordinal);
        Assert.Contains("own licensed table", refusal, StringComparison.Ordinal);
        Assert.Contains("none ship with the program", refusal, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListInstancesAsync(project));
        Assert.Empty(await host.Store.ListPacksAsync(project));
    }

    [Fact]
    public async Task Without_The_Motors_Rated_Speed_The_Pack_Is_Refused_Naming_It_Because_The_Cable_Is_Sized_On_The_Rated_Power()
    {
        var host = Host();
        var inputs = ChainInputs.HydraulicPowerUnit(host);
        inputs["e1"] = [.. inputs["e1"].Where(v => v.Symbol != "n_rated")];
        Assert.DoesNotContain(inputs["e1"], v => v.Symbol == "n_rated");

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.HydraulicPowerUnit, await host.ProjectAsync(), "Press 5 hydraulic power unit", inputs);

        // E1 gives no P_rated without n_rated (it warns, and does not fall back to the running speed), so the cable step cannot take it: the refusal says which input to enter.
        Assert.Null(made);
        Assert.NotNull(refusal);
        Assert.Contains("P_rated", refusal, StringComparison.Ordinal);
        Assert.Contains("n_rated", refusal, StringComparison.Ordinal);
        Assert.Contains("e4", refusal, StringComparison.Ordinal);

        // With it back the pack is created.
        var (ok, none) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.HydraulicPowerUnit, await host.ProjectAsync(), "Press 6 hydraulic power unit", ChainInputs.HydraulicPowerUnit(host));
        Assert.True(none is null, none);
        Assert.NotNull(ok);
    }

    [Fact]
    public async Task The_Cable_Step_Given_Only_Its_Tables_Completes_The_Example_Chain_And_Every_Other_Input_Is_An_Example_To_Replace_Or_A_Link()
    {
        var host = Host();
        var template = CalcPackTemplates.HydraulicPowerUnit;
        var inputs = template.Steps.ToDictionary(st => st.Key, st => host.Service.ExampleInputs(template, st)!, StringComparer.Ordinal);
        inputs["e4"] = [.. inputs["e4"], ConductorSizingCalculator.SyntheticAmpacity(), ConductorSizingCalculator.TypicalRatings()];
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, await host.ProjectAsync(), "Press 5 hydraulic power unit", inputs);

        Assert.True(refusal is null, refusal);
        Assert.Equal(8, made!.Pack.InstanceIds.Count);
        Assert.All(made.Instances.Values, i => Assert.False(i.Blocks.Count > 0, i.Name));
        Assert.Equal(40, Si(made.Instances["e4"], "I_n"), 9);
        Assert.Equal(15942.028985507251, Si(made.Instances["e5"], "I_k3"), 6);

        // Every input is an example the user must replace, or a link, or one of the cable step's own tables: nothing reads as a project figure.
        Assert.All(made.Instances.Values, i => Assert.All(i.Inputs.Where(input => input.Symbol is not ("ampacity" or "ratings")), input =>
            Assert.True(input.Provenance is { Kind: CalcProvenanceKind.Link } || (input.Provenance?.Kind == CalcProvenanceKind.Assumption && input.Provenance.Reference == CalcInstanceService.ExampleBasis), $"{i.Name}.{input.Symbol}: {input.Provenance}")));
    }

    [Fact]
    public async Task Revising_The_Supply_Transformer_Makes_Only_The_Motor_Start_Stale_And_The_Update_Carries_The_New_Fault_Current_Into_The_Dip()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var e5 = made.Instances["e5"];
        var e9Before = made.Instances["e9"];
        Assert.Equal(1.2454973839171712, Si(e9Before, "start_dip") * 100, 9);

        // A smaller transformer (250 kVA, same 4 %): the fault current falls by 5/8 to 9.964 kA, so the source impedance rises to 0.0254958 Ω and the dip at the motor goes from 1.2455 % to 1.5585 % (python3, complex divider).
        var revised = await host.Service.UpdateInputsAsync(e5.Id, [.. e5.Inputs.Select(i => i.Symbol == "S_r" ? i with { Number = 250 } : i)]);
        Assert.True(revised.Succeeded, revised.Reason);

        // Only the motor start takes from the fault-protection step, so only it is stale; the cable, the voltage drop and everything upstream are not.
        var staleness = await host.Service.StalenessAsync(await Fresh(host, made, "e9"));
        Assert.True(staleness.IsStale);
        Assert.Contains(staleness.Reasons, r => r.Contains("'I_k'", StringComparison.Ordinal) && r.Contains("different 'I_k3'", StringComparison.Ordinal));

        foreach (var key in new[] { "f1", "f2", "b1", "e1", "e4", "e3" })
            Assert.False((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, key);

        Assert.True((await host.Service.UpdateLinksAsync(e9Before.Id)).Succeeded);
        var e9 = await Fresh(host, made, "e9");
        Assert.False((await host.Service.StalenessAsync(e9)).IsStale);
        Assert.Equal(9963.76811594203, Si(await Fresh(host, made, "e5"), "I_k3"), 6);
        Assert.Equal(9963.76811594203, InputSi(e9, "I_k"), 6);
        Assert.Equal(0.025495787887413877, Si(e9, "Z_src"), 12);
        Assert.Equal(1.5585449743588775, Si(e9, "start_dip") * 100, 9);
        Assert.Equal(0.8453037369859029, Si(e9, "dip_pcc") * 100, 9);
        Assert.Equal(393.7658201025645, Si(e9, "V_m"), 8);
    }

    [Fact]
    public async Task A_Different_Voltage_Factor_On_The_Fault_Protection_Reaches_The_Motor_Start_Source_Impedance_Without_A_Spurious_Warning()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var e5 = made.Instances["e5"];
        var e9Before = made.Instances["e9"];

        // c_max 1.05 (instead of 1.1) lowers the fault current to 1.05/1.1 of its value. The source impedance Z = c U/(√3 I_k) with I_k proportional to c does not depend on c, so with c_src linked to E5's factor Z_src is unchanged
        // (E5's own Z_T); before the link c_src stayed 1.1 and Z_src would have been 4.8 % high, with no warning.
        var revised = await host.Service.UpdateInputsAsync(e5.Id, [.. e5.Inputs.Select(i => i.Symbol == "c_max" ? i with { Number = 1.05 } : i)]);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, "e9"))).IsStale);
        Assert.True((await host.Service.UpdateLinksAsync(e9Before.Id)).Succeeded);
        var e9 = await Fresh(host, made, "e9");

        Assert.Equal(1.05, InputSi(e9, "c_src"), 12);
        Assert.Equal(15942.028985507251 * 1.05 / 1.1, InputSi(e9, "I_k"), 6);
        Assert.Equal(Si(e9Before, "Z_src"), Si(e9, "Z_src"), 12);
        Assert.DoesNotContain(e9.Warnings, w => w.RuleId == "c_src");
    }

    [Fact]
    public async Task A_Pack_Checked_While_Current_Is_Refused_At_The_Issue_Staleness_Gate_When_The_Pressure_Fed_To_The_Cylinder_Moves_On_And_Every_Downstream_Step_Is_Stale()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // The cylinder step takes its supply pressure from a circuit outside the pack, which becomes a new revision after the check: the cylinder step is stale with no pack member touched, and so is every step that takes from it,
        // down to the motor start four links away; the pack is still Checked, and Issue is refused naming the cylinder step.
        await ChainProofs.AnOutsideRevisionAfterTheCheckMakesTheWholeChainStaleAndRefusesIssueAsync(
            host, made, "f1", "p", "fp.cylinder", ChainInputs.HydraulicPowerUnit(host)["f1"], "p", ["f2", "b1", "e1", "e4", "e3", "e5", "e9"]);
    }
}
