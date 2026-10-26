using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B, §B7 chain 4: the mechanism review M9 → M7, M4 → M5, E1 → E4 from the built-in pack template. A cam-driven carton tamper: the cam step (M9) gives its spring force at full lift to the return spring (M7), its contact
/// force, RMS and peak torque and speed to the cam shaft (M4), the shaft's bearing reaction and the speed to the bearing (M5), and its RMS torque and speed to the geared motor (E1), whose nameplate rated power sizes the supply
/// cable (E4). The cable step's two tables are SYNTHETIC test values (never a regulation or manufacturer table); the shipped example omits them. Every number is recomputed in python3 (a 360 000-point scan of the cam for
/// the torques and the contact force; closed form for the rest).
/// </summary>
public sealed class MechanismReviewChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. ElectricalCalculators.All, .. MechanicalCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.MechanismReview, "Carton tamper cam", ChainInputs.MechanismReview(host));

    /// <summary>The cam revised: a 10 % stiffer return spring and a 20 % faster cam, so every value a link carries moves (the spring force, the contact force, both torques and the speed).</summary>
    private static IReadOnlyList<CalcInputValue> Revise(IReadOnlyList<CalcInputValue> inputs) => Scale("k_sp", 1.1)(Scale("n_in", 1.2)(inputs));

    [Fact]
    public async Task Values_Flow_Down_Every_Link_Of_The_Chain_With_Their_Units()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.MechanismReview));
        var made = await InstantiateAsync(host);
        var (m9, m7, m4, m5, e1, e4) = (made.Instances["m9"], made.Instances["m7"], made.Instances["m4"], made.Instances["m5"], made.Instances["e1"], made.Instances["e4"]);
        Assert.Equal(6, made.Pack.InstanceIds.Count);

        // M9: 40 mm lift (cycloidal rise, modified-trapezoid return, 120° each, 150 rpm): v_pk = C_v h ω/β = 2 · 0.04 · 15.708/2.0944 = 0.6 m/s (both laws have C_v = 2);
        // a_pk(rise) = 2π h ω²/β² = 14.137 m/s², a_pk(return) = 8π/(π + 2) h ω²/β² = 10.998 m/s²; F = m a + k s + F_pre peaks at full lift where a = 0 (a cycloidal rise ends at zero acceleration):
        // k h + F_pre = 9.874 N/mm · 40 mm + 50 N = 444.96 N, which is also the contact force there (the pressure angle is zero at the top). Torque by virtual work T = F ds/dθ.
        var omega = 150 * 2 * Math.PI / 60;
        Assert.Equal(0.040, Si(m9, "lift"), 12);
        Assert.Equal(2 * 0.040 * omega / (120 * Math.PI / 180), Si(m9, "v_pk"), 9);
        Assert.Equal(0.6, Si(m9, "output_motion"), 9);
        Assert.Equal(2 * Math.PI * 0.040 * omega * omega / Math.Pow(120 * Math.PI / 180, 2), Si(m9, "a_pk"), 9);
        Assert.Equal(14.137166941154073, Si(m9, "peak_accel"), 9);
        Assert.Equal(9.874 * 40 + 50, Si(m9, "F_spring_max"), 9);
        Assert.Equal(444.96, Si(m9, "F_spring_max"), 9);
        Assert.Equal(444.96, Si(m9, "follower_force"), 9);
        Assert.Equal(444.96, Si(m9, "F_pin_pk"), 9);
        Assert.Equal(50, Si(m9, "F_min"), 9);
        Assert.Equal(10.111347132901606, Si(m9, "input_torque"), 8);
        Assert.Equal(4.816164184183062, Si(m9, "T_rms_in"), 6);
        Assert.Equal(22.270179122602578, Si(m9, "phi_max_pk") * 180 / Math.PI, 6);
        Assert.Equal(150, Si(m9, "n_in") * 60 / (2 * Math.PI), 9);
        Assert.Equal(3, m9.Checks.Count);
        Assert.All(m9.Checks, c => Assert.True(c.Passed, c.Name));

        // M9 → M7: the spring force at full lift (N) is the spring's second working load; the first is the 50 N preload at the base circle, typed equal to the cam's (guarded).
        Assert.Equal(Si(m9, "F_spring_max"), InputSi(m7, "F_2"), 9);
        Assert.Equal("F_spring_max", m7.Links.Single(l => l.Input.Symbol == "F_2").Link.OutputSymbol);
        Assert.Equal(["F_2", "f_op", "s_work"], m7.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(InputSi(m9, "F_pre"), InputSi(m7, "F_1"), 12);

        // M7: k = G d⁴/(8 D³ n) = 80 000 · 6⁴/(8 · 50³ · 10.5) = 9.874286 N/mm; s₁ = 50/k = 5.0637 mm, s₂ = 444.96/k = 45.0625 mm; solid length 6 · (10.5 + 2) = 75 mm; k_c (Bergsträsser, C = 8.333) = 1.164835;
        // τ₂ = k_c 8 F₂ D/(π d³) = 305.52 MPa; f_n = (d/(2π n D²)) √(G/(2ρ)) = 82.118 Hz.
        var rate = 80e3 * Math.Pow(6, 4) / (8 * Math.Pow(50, 3) * 10.5);
        Assert.Equal(9.874285714285714, rate, 12);
        Assert.Equal(rate * 1e3, Si(m7, "k"), 6);
        Assert.Equal(444.96 / rate * 1e-3, Si(m7, "s_2"), 12);
        Assert.Equal(5.063657407407407e-3, Si(m7, "s_1"), 12);
        Assert.Equal(0.075, Si(m7, "L_s"), 12);
        Assert.Equal(1.164835164835165, Si(m7, "k_c"), 12);
        Assert.Equal(1.164835164835165 * 8 * 444.96 * 50 / (Math.PI * 216) * 1e6, Si(m7, "tau_2"), 3);
        Assert.Equal(305.5215241630442e6, Si(m7, "tau_2"), 2);
        Assert.Equal(82.11778786253618, Si(m7, "f_n"), 9);
        Assert.Equal(5, m7.Checks.Count);
        Assert.All(m7.Checks, c => Assert.True(c.Passed, c.Name));

        // M9 → M7 at run time: the cam's lift (mm) is the spring's expected working travel and the cam's frequency in Hz (f_in; n_in is an angular velocity and cannot be linked to a frequency) its operating frequency.
        Assert.Equal(Si(m9, "lift"), InputSi(m7, "s_work"), 12);
        Assert.Equal("lift", m7.Links.Single(l => l.Input.Symbol == "s_work").Link.OutputSymbol);
        Assert.Equal(2.5, Si(m9, "f_in"), 12);
        Assert.Equal(Si(m9, "f_in"), InputSi(m7, "f_op"), 12);
        Assert.Equal("f_in", m7.Links.Single(l => l.Input.Symbol == "f_op").Link.OutputSymbol);

        // The cam's typed rate and the spring's computed rate are the same spring (to 0.01 %), so the spring's working travel s₂ − s₁ is the cam's 40 mm lift (to 0.003 %) and M7 raises no travel warning.
        Assert.True(Math.Abs(InputSi(m9, "k_sp") - Si(m7, "k")) / Si(m7, "k") < 1e-4);
        Assert.True(Math.Abs(Si(m7, "s_2") - Si(m7, "s_1") - Si(m9, "lift")) / Si(m9, "lift") < 1e-4);
        Assert.Equal(Si(m7, "s_2") - Si(m7, "s_1"), Si(m7, "s_trav"), 12);
        Assert.DoesNotContain(m7.Warnings, w => w.RuleId == "travel-mismatch");
        Assert.True(Si(m7, "L_2") > Si(m7, "L_n"), "the spring must not be loaded past its minimum clearance length");

        // M9 → M4: the contact force (N into kN) is the load row at the cam, by a table-cell link; the peak input torque (N·m into kN·m) is the shaft's alternating torque amplitude and its peak torque, and the speed is the shaft's.
        var load = Assert.Single(m4.CellLinks);
        Assert.Equal(("loads", 0, "F_y", "F_pin_pk"), (load.Symbol, load.Row, load.Column, load.Link.OutputSymbol));
        Assert.Equal(m9.Id, load.Link.UpstreamId);
        Assert.Equal(Si(m9, "F_pin_pk"), CellSi(m4, "loads", 0, "F_y"), 9);
        Assert.Equal(["T_a", "T_pk", "n"], m4.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(Si(m9, "input_torque"), InputSi(m4, "T_a"), 9);
        Assert.Equal(Si(m9, "input_torque"), InputSi(m4, "T_pk"), 9);
        Assert.Equal(0, InputSi(m4, "T"), 12);
        Assert.Equal(Si(m9, "n_in"), InputSi(m4, "n"), 9);

        // M4: the cam sits at 150 mm, midway between bearings at 25 and 275 mm, so each reaction is half the contact force (222.48 N) and M_max = F L/4 = 444.96 · 0.25/4 = 27.81 N·m.
        Assert.Equal(222.48, Si(m4, "R_A"), 6);
        Assert.Equal(Si(m4, "R_A"), Si(m4, "R_B"), 6);
        Assert.Equal(Si(m9, "F_pin_pk") / 2, Si(m4, "R_A"), 6);
        Assert.Equal(444.96 * 0.250 / 4, Si(m4, "M_max"), 6);
        Assert.Equal(0.020, Si(m4, "d_A"), 12);
        Assert.Equal(5, m4.Checks.Count);
        Assert.All(m4.Checks, c => Assert.True(c.Passed, c.Name));

        // M4 → M5 (the bearing reaction, N into kN) and M9 → M5 (the speed).
        Assert.Equal(Si(m4, "R_A"), InputSi(m5, "F_r"), 6);
        Assert.Equal(0.22248, InputSi(m5, "F_r") / 1e3, 9);
        Assert.Equal(Si(m9, "n_in"), InputSi(m5, "n"), 9);
        Assert.Equal(["F_r", "n"], m5.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));

        // M5: P = F_r (a radial ball bearing), L₁₀ = (C/P)³ = (3.5/0.22248)³ = 3893.42 Mrev = 4.326·10⁵ h at 150 rpm.
        Assert.Equal(Math.Pow(3.5 / 0.22248, 3), Si(m5, "L_10"), 6);
        Assert.Equal(3893.419551926027, Si(m5, "L_10"), 6);
        Assert.Equal(432602.1724362252 * 3600, Si(m5, "L_10h"), 1);
        Assert.All(m5.Checks, c => Assert.True(c.Passed, c.Name));

        // M9 → E1: the RMS torque and the speed are the continuous load, torque at the cam shaft: through the 9.6:1, 0.9-efficient gearbox the motor carries 4.8162/8.64 = 0.557426 N·m at 1440 rpm (its nameplate speed) = 84.058 W.
        Assert.Equal(["T_L", "n_L"], e1.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(Si(m9, "T_rms_in"), InputSi(e1, "T_L"), 9);
        Assert.Equal(Si(m9, "n_in"), InputSi(e1, "n_L"), 9);
        Assert.Equal("continuous", e1.Inputs.Single(i => i.Symbol == "duty_mode").Text);
        Assert.Equal(Si(m9, "T_rms_in") / (9.6 * 0.9), Si(e1, "T_load"), 9);
        Assert.Equal(0.5574264103129274, Si(e1, "T_load"), 6);
        Assert.Equal(1440, Si(e1, "n_pk") * 60 / (2 * Math.PI), 9);
        Assert.Equal(1440, InputSi(e1, "n_rated") * 60 / (2 * Math.PI), 9);
        Assert.Equal(84.05792234668907, Si(e1, "P_cont"), 4);
        Assert.Equal(0.0013028163580246913, Si(e1, "J_total"), 12);
        Assert.Equal(0.6556564494972549, Si(e1, "T_pk"), 6);
        Assert.Equal(5, e1.Checks.Count);
        Assert.All(e1.Checks, c => Assert.True(c.Passed, c.Name));

        // E1 is judged on the cam's RMS torque; the cam's PEAK torque (10.111 N·m at the cam shaft, 1.1703 N·m at the motor) is inside the candidate motor's 3 N·m peak, which this step cannot see (guarded here).
        var motorPeak = Si(m9, "input_torque") / (9.6 * 0.9);
        Assert.Equal(1.1702948070522274, motorPeak, 8);
        Assert.True(motorPeak < InputSi(e1, "T_peak"));

        // E1 → E4: the candidate motor's nameplate rated shaft power T_rated · ω_rated = 1.0 · 1440 rpm = 150.796 W (W into kW) sizes the cable: I_b = P/(√3 U cos φ η) = 0.34009 A on a 6 A device and 1.5 mm².
        Assert.Equal(["P"], e4.Links.Select(l => l.Input.Symbol));
        Assert.Equal(Si(e1, "P_rated"), InputSi(e4, "P"), 6);
        Assert.Equal(1.0 * 1440 * 2 * Math.PI / 60, Si(e1, "P_rated"), 9);
        Assert.Equal(150.79644737231007, Si(e1, "P_rated"), 9);
        Assert.Equal(150.79644737231007 / (Math.Sqrt(3) * 400 * 0.8 * 0.8), Si(e4, "I_b"), 9);
        Assert.Equal(0.34008738079391576, Si(e4, "I_b"), 9);
        Assert.Equal(6, Si(e4, "I_n"), 9);
        Assert.Equal(1.5e-6, Si(e4, "CSA"), 15);
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
        var template = CalcPackTemplates.MechanismReview;

        Assert.Equal(["m9", "m7", "m4", "m5", "e1", "e4"], template.Steps.Select(s => s.Key));
        Assert.Equal(
            [
                ("m9", "F_spring_max", "m7", "F_2"), ("m9", "lift", "m7", "s_work"), ("m9", "f_in", "m7", "f_op"), ("m9", "F_pin_pk", "m4", "loads"), ("m9", "input_torque", "m4", "T_a"), ("m9", "input_torque", "m4", "T_pk"), ("m9", "n_in", "m4", "n"),
                ("m4", "R_A", "m5", "F_r"), ("m9", "n_in", "m5", "n"), ("m9", "T_rms_in", "e1", "T_L"), ("m9", "n_in", "e1", "n_L"), ("e1", "P_rated", "e4", "P"),
            ],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.Equal((0, "F_y"), (template.Bindings.Single(b => b.Input == "loads").Row, template.Bindings.Single(b => b.Input == "loads").Column));

        foreach (var b in template.Bindings.Where(b => b.Row is null))
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked: the spring's rate and preload are typed in the cam step (a link back from the spring would be a cycle) and guarded, and the first working load of the spring is typed equal to the preload;
        // every other input and table is entered.
        Assert.Equal(template.Bindings.Count(b => b.Row is null), made.Instances.Values.Sum(i => i.Links.Count));
        Assert.Equal(1, made.Instances.Values.Sum(i => i.CellLinks.Count));
        Assert.DoesNotContain(made.Instances["m9"].Inputs, i => CalcLink.From(i.Provenance) is not null);
    }

    [Fact]
    public async Task A_Stiffer_Spring_And_A_Faster_Cam_Make_Every_Downstream_Step_Stale_Refuse_The_Check_And_The_Issue_And_Updating_In_Order_Carries_The_Values_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var m9 = made.Instances["m9"];
        var e1Before = made.Instances["e1"];

        await ChainKit.ReviseAndUpdateAsync(host, made, "m9", Revise, ["m7", "m4", "m5", "e1", "e4"]);

        var m9New = await Fresh(host, made, "m9");
        var (m7, m4, m5, e1, e4) = (await Fresh(host, made, "m7"), await Fresh(host, made, "m4"), await Fresh(host, made, "m5"), await Fresh(host, made, "e1"), await Fresh(host, made, "e4"));

        // The revision moved every value a link carries (python3, same scan): k_sp 10.8614 N/mm and 180 rpm.
        Assert.Equal(9.874 * 1.1 * 40 + 50, Si(m9New, "F_spring_max"), 9);
        Assert.Equal(484.456, Si(m9New, "F_spring_max"), 9);
        Assert.Equal(484.456, Si(m9New, "F_pin_pk"), 9);
        Assert.Equal(10.64384852018375, Si(m9New, "input_torque"), 8);
        Assert.Equal(5.158731780010033, Si(m9New, "T_rms_in"), 6);
        Assert.Equal(180, Si(m9New, "n_in") * 60 / (2 * Math.PI), 9);
        Assert.NotEqual(Si(m9, "F_spring_max"), Si(m9New, "F_spring_max"));

        // ... and the update carried each one to its step.
        Assert.Equal(Si(m9New, "F_spring_max"), InputSi(m7, "F_2"), 9);
        Assert.Equal(Si(m9New, "F_pin_pk"), CellSi(m4, "loads", 0, "F_y"), 9);
        Assert.Equal(Si(m9New, "input_torque"), InputSi(m4, "T_a"), 9);
        Assert.Equal(Si(m9New, "input_torque"), InputSi(m4, "T_pk"), 9);
        Assert.Equal(Si(m9New, "n_in"), InputSi(m4, "n"), 9);
        Assert.Equal(242.228, Si(m4, "R_A"), 6);
        Assert.Equal(Si(m4, "R_A"), InputSi(m5, "F_r"), 6);
        Assert.Equal(Si(m9New, "n_in"), InputSi(m5, "n"), 9);
        Assert.Equal(Math.Pow(3.5 / 0.242228, 3), Si(m5, "L_10"), 6);
        Assert.Equal(Si(m9New, "T_rms_in"), InputSi(e1, "T_L"), 9);
        Assert.Equal(Si(m9New, "n_in"), InputSi(e1, "n_L"), 9);
        Assert.Equal(0.5970754375011612, Si(e1, "T_load"), 6);
        Assert.Equal(5.158731780010033 / (9.6 * 0.9), Si(e1, "T_load"), 6);
        Assert.Equal(1728, Si(e1, "n_pk") * 60 / (2 * Math.PI), 9);

        // The revised cam runs at 180 rpm, so the motor now runs at 1728 rpm against its 1440 rpm nameplate: E1 says so (speed-above-rated); the base case at 1440 rpm did not.
        Assert.Equal(1440, InputSi(e1, "n_rated") * 60 / (2 * Math.PI), 9);
        var fast = Assert.Single(e1.Warnings, w => w.RuleId == "speed-above-rated");
        Assert.Contains("1728", fast.Message, StringComparison.Ordinal);
        Assert.Contains("1440", fast.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(e1Before.Warnings, w => w.RuleId == "speed-above-rated");
        Assert.Equal(Si(m9New, "f_in"), InputSi(m7, "f_op"), 12);
        Assert.Equal(3, InputSi(m7, "f_op"), 12);

        // The motor's nameplate does not move with the load, so the cable stays 0.34009 A on 1.5 mm²; the spring's operating frequency followed the cam to 3 Hz. But the spring's own typed rate (9.874 N/mm) is now 9.1 % below the cam's
        // (10.861 N/mm) and its working travel 44.0 mm instead of the cam's 40 mm: the pack is current, and M7 now says so at run time (travel-mismatch) instead of silently still checking.
        Assert.Equal(Si(e1Before, "P_rated"), Si(e1, "P_rated"), 9);
        Assert.Equal(0.34008738079391576, Si(e4, "I_b"), 9);
        Assert.Equal(43.99872685185186e-3, Si(m7, "s_2") - Si(m7, "s_1"), 9);
        Assert.False(Math.Abs(InputSi(m9New, "k_sp") - Si(m7, "k")) / Si(m7, "k") < 1e-4);
        Assert.Equal(40e-3, InputSi(m7, "s_work"), 12);
        var mismatch = Assert.Single(m7.Warnings, w => w.RuleId == "travel-mismatch");
        Assert.StartsWith("s_work:", mismatch.Message, StringComparison.Ordinal);
        Assert.Contains(" % away from the expected 40", mismatch.Message, StringComparison.Ordinal);
        Assert.All(new[] { m7, m4, m5, e1, e4 }, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task While_Stale_The_Pack_Check_And_Issue_Are_Refused_With_Reasons_That_Name_The_Stale_Step()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var m9 = made.Instances["m9"];

        var revised = await host.Service.UpdateInputsAsync(m9.Id, Revise(m9.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);

        foreach (var key in new[] { "m7", "m4", "m5", "e1", "e4" })
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        // The direct links say which value moved; E4 takes nothing from the cam and is stale only because E1, which it is linked to, is.
        var m7Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "m7"))).Reasons;
        Assert.Contains(m7Reasons, r => r.Contains("'F_2'", StringComparison.Ordinal) && r.Contains("different 'F_spring_max'", StringComparison.Ordinal));
        var m4Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "m4"))).Reasons;
        Assert.Contains(m4Reasons, r => r.Contains("'T_a'", StringComparison.Ordinal) && r.Contains("different 'input_torque'", StringComparison.Ordinal));
        Assert.Contains(m4Reasons, r => r.Contains("'T_pk'", StringComparison.Ordinal) && r.Contains("different 'input_torque'", StringComparison.Ordinal));
        Assert.Contains(m4Reasons, r => r.Contains("'n'", StringComparison.Ordinal) && r.Contains("different 'n_in'", StringComparison.Ordinal));
        Assert.Contains(m4Reasons, r => r.Contains("loads", StringComparison.Ordinal) && r.Contains("F_pin_pk", StringComparison.Ordinal));
        var m5Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "m5"))).Reasons;
        Assert.Contains(m5Reasons, r => r.Contains("'F_r'", StringComparison.Ordinal) && r.Contains("is linked to, is itself stale", StringComparison.Ordinal));
        var e1Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e1"))).Reasons;
        Assert.Contains(e1Reasons, r => r.Contains("'T_L'", StringComparison.Ordinal) && r.Contains("different 'T_rms_in'", StringComparison.Ordinal));
        var e4Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "e4"))).Reasons;
        Assert.Contains(e4Reasons, r => r.Contains("'Cam drive motor', which 'P' is linked to, is itself stale", StringComparison.Ordinal));

        host.Principal = "j.reviewer";
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("Pack member 'Follower return spring' is stale", check.Reason, StringComparison.Ordinal);
        Assert.Contains("Update it before checking the pack", check.Reason, StringComparison.Ordinal);

        var issue = await host.Service.IssuePackAsync(made.Pack.Id, "TMP-STALE", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("check it before issuing", issue.Reason, StringComparison.Ordinal);
        Assert.NotEqual(CalcInstanceStatus.Issued, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);
    }

    [Fact]
    public async Task A_Pack_Checked_While_Current_Is_Refused_At_The_Issue_Staleness_Gate_When_The_Spring_Preload_Fed_To_The_Cam_Moves_On_And_Every_Downstream_Step_Is_Stale()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // The cam takes its spring preload from a spring outside the pack (its first working force, a port), which becomes a new revision after the check: the cam is stale with no pack member touched, and so is every
        // step that takes from it, down to the supply cable; the pack is still Checked, and Issue is refused naming the cam.
        var spring = ChainInputs.MechanismReview(host)["m7"].Concat([CalcInputValue.Quantity("F_2", 444.96, "N") with { Provenance = CalcProvenance.Assumed("Outside spring: second working load.") }]).ToList();
        await ChainProofs.AnOutsideRevisionAfterTheCheckMakesTheWholeChainStaleAndRefusesIssueAsync(
            host, made, "m9", "F_pre", "mech.spring", spring, "F_1", ["m7", "m4", "m5", "e1", "e4"]);
    }

    [Fact]
    public async Task The_Chain_Is_Checked_And_Issued_As_One_Bundle_With_An_Independent_Checker_And_Every_Link_On_The_Sheet()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // The cable step demands an independent check: the engineer who drew the chain up cannot check it.
        var self = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(self.Succeeded);
        Assert.Contains("needs an independent check", self.Reason, StringComparison.Ordinal);

        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.MechanismReview, "TMP-001", "Client Co", "Checked against SPEC-TMP-04.");

        Assert.Contains("link: Tamper cam rev A · F_spring_max", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Tamper cam rev A · F_pin_pk", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Tamper cam rev A · T_rms_in", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Tamper cam rev A · input_torque", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Tamper cam rev A · n_in", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cam shaft rev A · R_A", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Cam drive motor rev A · P_rated", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_The_Owners_Tables_The_Cable_Step_Opens_Blocked_Naming_Them_And_The_Pack_Cannot_Be_Checked()
    {
        var host = Host();
        var template = CalcPackTemplates.MechanismReview;

        Assert.All(template.Steps, s => Assert.NotEmpty(s.Example ?? []));
        Assert.DoesNotContain(template.Steps.Single(s => s.Key == "e4").Example!, v => v.Symbol is "ampacity" or "ratings");

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, await host.ProjectAsync(), "Carton tamper cam", null);

        // Nothing takes from the cable step, so the chain is created and the cable step is the one that is blocked (a pack cannot be checked with a blocked member).
        Assert.True(refusal is null, refusal);
        Assert.Equal(6, made!.Pack.InstanceIds.Count);
        Assert.All(new[] { "m9", "m7", "m4", "m5", "e1" }, key => Assert.Empty(made.Instances[key].Blocks));
        var block = Assert.Single(made.Instances["e4"].Blocks);
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
    public async Task Without_The_Motors_Rated_Speed_The_Pack_Is_Refused_Naming_It_Because_The_Cable_Is_Sized_On_The_Rated_Power()
    {
        var host = Host();
        var inputs = ChainInputs.MechanismReview(host);
        inputs["e1"] = [.. inputs["e1"].Where(v => v.Symbol != "n_rated")];

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MechanismReview, await host.ProjectAsync(), "Carton tamper cam", inputs);

        Assert.Null(made);
        Assert.NotNull(refusal);
        Assert.Contains("P_rated", refusal, StringComparison.Ordinal);
        Assert.Contains("n_rated", refusal, StringComparison.Ordinal);
        Assert.Contains("e4", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Cams_Torque_Reaches_The_Shaft_As_An_Alternating_Torque_About_A_Zero_Mean_Not_As_A_Steady_RMS_One()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var (m9, m4) = (made.Instances["m9"], made.Instances["m4"]);

        // chain.drive-train-01 (the same binding, copied into this pack). The cam's torque (a frictionless spring system) swings about a mean of zero (python3 over the cycle: −9.61 to +10.11 N·m, RMS 4.8162), so M4 takes the peak
        // magnitude as its alternating amplitude T_a and a steady torque of 0, not the RMS torque as a steady one. The guard reimplements M4's Marin factors for a ground finish (k_a = 1.58·S_ut^−0.085, k_b = 1.24·d^−0.107,
        // k_e = 1 − 0.08·2.326 at 99 %, S_e′ = 0.5·S_ut) and the notch factors of the example: 1/n_f = √(σ_a² + 3τ_a²)/S_e with τ_a from the peak torque, which M4 must reproduce; the old binding
        // (T_rms_in as a steady torque, 1/n_f = σ_a/S_e + √3τ_m/S_ut) read 8.9713 against the alternating reading 9.00 (bending dominates here, so the two are within 1 %).
        const double sut = 850;
        const double reaction = 222.48;
        var rms = Si(m9, "T_rms_in");
        var peak = Si(m9, "input_torque");
        var steady = double.PositiveInfinity;
        var alternating = double.PositiveInfinity;

        foreach (var (xMm, dMm) in new[] { (50.0, 20.0), (150.0, 25.0) })
        {
            var se = 1.58 * Math.Pow(sut, -0.085) * 1.24 * Math.Pow(dMm, -0.107) * (1 - 0.08 * 2.3263) * 0.5 * sut;
            var kf = 1 + 0.85 * (1.7 - 1);
            var kfs = 1 + 0.9 * (1.5 - 1);
            var moment = reaction * (xMm - 25) / 1000;
            var sigmaA = kf * 32 * moment * 1000 / (Math.PI * dMm * dMm * dMm);
            var tauM = kfs * 16 * rms * 1000 / (Math.PI * dMm * dMm * dMm);
            var tauA = kfs * 16 * peak * 1000 / (Math.PI * dMm * dMm * dMm);
            steady = Math.Min(steady, 1 / (sigmaA / se + Math.Sqrt(3) * tauM / sut));
            alternating = Math.Min(alternating, se / Math.Sqrt(sigmaA * sigmaA + 3 * tauA * tauA));
        }

        Assert.Equal(8.9713, steady, 3);
        Assert.Equal(alternating, Si(m4, "n_f_min"), 4);
        Assert.True(Math.Abs(Si(m4, "n_f_min") - steady) / steady < 0.01, "bending dominates at the governing section: the old steady-RMS stand-in is within 1 % of the alternating reading here (it would not be on a torque-dominated or keyed section)");
        var target = InputSi(m4, "n_target");
        Assert.Equal(1.5, target, 12);
        Assert.True(Si(m4, "n_f_min") >= target, "the shaft still clears the target safety factor on the alternating torque");

        // The zero-mean claim behind the unbound steady torque: the cam's mean input torque (mean power over the shaft speed) is a small fraction of the amplitude.
        var meanTorque = Si(m9, "P_mean") / Si(m9, "n_in");
        Assert.True(Math.Abs(meanTorque) < 0.05 * peak, $"mean input torque {meanTorque} N·m against an amplitude of {peak} N·m");
    }
}
