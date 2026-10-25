using CsCheck;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.FluidPower;

/// <summary>F1 — cylinder and actuator circuit: behaviour beyond the known-answer cases.</summary>
public sealed class CylinderTests
{
    private static readonly CylinderCalculator Cylinder = new();

    private const double Bar = 1e5;
    private const double Litre = 1e-3;
    private const double Mm = 1e-3;

    /// <summary>The first known-answer case (pump, motor, orifice and a hose list included) with overrides applied in order.</summary>
    private static CalcInputSet With(params CalcInputValue[] overrides) => overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Cylinder.Template), (set, value) => set.With(value));

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Cylinder, With(overrides));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue Hoses(params double[] millimetres) => CalcInputValue.Table("hoses", millimetres.Select(v => CalcRow.Of(("ID", new CalcCell(v, "mm")))).ToList());

    private static double Area(double dMm) => Math.PI * dMm * Mm * dMm * Mm / 4;

    [Fact]
    public void Areas_And_Forces_Follow_Pressure_Times_Area_Times_Efficiency()
    {
        var r = Run();
        double ab = Area(100), ar = Area(70), aa = ab - ar;
        Assert.Equal(ab, r.OutputSi("A_b"), 12);
        Assert.Equal(ar, r.OutputSi("A_r"), 12);
        Assert.Equal(aa, r.OutputSi("A_a"), 12);
        Assert.Equal(160 * Bar * ab * 0.95, r.OutputSi("F_ext"), 6);
        Assert.Equal(160 * Bar * aa * 0.95, r.OutputSi("F_ret"), 6);
        Assert.Equal(ab / aa, r.OutputSi("phi"), 12);

        var lossless = Run(CalcInputValue.Of("eta_m", 1), CalcInputValue.Of("eta_vc", 1));
        Assert.Equal(160 * Bar * ab, lossless.OutputSi("F_ext"), 6);
    }

    [Fact]
    public void Speed_Time_And_Swept_Volume_Follow_The_Flow_And_The_Stroke()
    {
        var r = Run(Q("t_dwell", 2, "s"));
        double ab = Area(100), aa = ab - Area(70), q = 36 * Litre / 60;
        Assert.Equal(q * 0.98 / ab, r.OutputSi("v_ext"), 12);
        Assert.Equal(q * 0.98 / aa, r.OutputSi("v_ret"), 12);
        Assert.Equal(0.5 / (q * 0.98 / ab), r.OutputSi("t_ext"), 9);
        Assert.Equal(r.OutputSi("t_ext") + r.OutputSi("t_ret") + 2, r.OutputSi("t_cycle"), 9);
        Assert.Equal(ab * 0.5, r.OutputSi("dV_ext"), 12);
        Assert.Equal(aa * 0.5, r.OutputSi("dV_ret"), 12);
        Assert.Equal(Math.Max(ab, aa) * 0.5, r.OutputSi("dV"), 12);
    }

    [Fact]
    public void The_Output_Power_Of_The_Cylinder_Is_The_Fluid_Power_Times_Both_Efficiencies_In_Both_Connections()
    {
        // Property: F·v = p·Q·η_m·η_vc for the standard connection without back pressure and for the regenerative one, for any valid inputs.
        Gen.Select(Gen.Double[15, 700], Gen.Double[1, 2000], Gen.Double[0.5, 1], Gen.Double[0.5, 1], Gen.Double[20, 500], Gen.Double[0.2, 0.9], Gen.Bool).Sample((p, q, em, ev, bore, rodRatio, regen) =>
        {
            var r = Run(Q("p", p, "bar"), Q("Q", q, "L/min"), CalcInputValue.Of("eta_m", em), CalcInputValue.Of("eta_vc", ev), Q("D", bore, "mm"), Q("d_r", bore * rodRatio, "mm"),
                CalcInputValue.Choice("mode", regen ? CylinderCalculator.Regenerative : CylinderCalculator.Standard));
            var expected = p * Bar * q * Litre / 60 * em * ev;
            return Math.Abs(r.OutputSi("F_ext") * r.OutputSi("v_ext") - expected) <= 1e-9 * expected;
        }, iter: 120);
    }

    [Fact]
    public void Regenerative_Extend_Trades_Force_For_Speed_And_Loads_The_Cap_Line()
    {
        var standard = Run(Q("F_push", 40, "kN"));
        var regen = Run(Q("F_push", 40, "kN"), CalcInputValue.Choice("mode", CylinderCalculator.Regenerative), Q("d_prs", 25, "mm"));
        double ab = Area(100), ar = Area(70);
        Assert.Equal(160 * Bar * ar * 0.95, regen.OutputSi("F_ext"), 6);
        Assert.Equal(standard.OutputSi("F_ext"), regen.OutputSi("F_std"), 9);
        Assert.Equal(standard.OutputSi("v_ext") * ab / ar, regen.OutputSi("v_ext"), 9);
        Assert.Equal(standard.OutputSi("v_ext"), regen.OutputSi("v_std"), 9);
        Assert.Equal(regen.OutputSi("Q_line") * ab / ar, regen.OutputSi("Q_prs"), 9);
        Assert.Equal(ar * 0.5, regen.OutputSi("dV_ext"), 12);
        Assert.Equal(standard.OutputSi("F_ret"), regen.OutputSi("F_ret"), 9);
        Assert.Null(standard.FindOutput("F_std"));
    }

    [Fact]
    public void A_Back_Pressure_Takes_Force_Off_Both_Strokes_And_One_That_Takes_It_All_Blocks()
    {
        var free = Run();
        var loaded = Run(Q("p_back", 10, "bar"));
        double ab = Area(100), aa = ab - Area(70);
        Assert.Equal((160 * Bar * ab - 10 * Bar * aa) * 0.95, loaded.OutputSi("F_ext"), 6);
        Assert.Equal((160 * Bar * aa - 10 * Bar * ab) * 0.95, loaded.OutputSi("F_ret"), 6);
        Assert.True(loaded.OutputSi("F_ext") < free.OutputSi("F_ext"));

        var all = Run(Q("p", 5, "bar"), Q("p_back", 20, "bar"));
        Assert.True(all.IsBlocked);
        Assert.StartsWith("p_back:", all.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Rod_As_Large_As_The_Bore_Blocks_And_Names_The_Rod()
    {
        var r = Run(Q("d_r", 100, "mm"));
        Assert.True(r.IsBlocked);
        Assert.StartsWith("d_r:", r.Blocks[0].Message, StringComparison.Ordinal);
        Assert.True(Run(Q("D", 63, "mm"), Q("d_r", 45, "mm")).IsBlocked is false);
    }

    [Fact]
    public void The_Pump_Delivery_Power_Torque_And_Speed_Follow_The_Displacement()
    {
        var r = Run();
        double qp = 28e-6 * 1450 / 60 * 0.95, p = 160 * Bar, power = p * qp / 0.88;
        Assert.Equal(qp, r.OutputSi("Q_p"), 12);
        Assert.Equal(power, r.OutputSi("P_pump"), 6);
        Assert.Equal(power / (1450 * 2 * Math.PI / 60), r.OutputSi("T_shaft"), 6);
        Assert.Equal(1450 * 2 * Math.PI / 60, r.OutputSi("n_shaft"), 9);
        Assert.Equal(36 * Litre / 60 / qp, r.OutputSi("u_pump"), 9);
        // The displacement the circuit flow needs, per radian in SI: Q / (ω·η_vp).
        Assert.Equal(36 * Litre / 60 / (1450 * 2 * Math.PI / 60 * 0.95), r.OutputSi("Vg_req"), 15);

        // A pump that cannot deliver the circuit flow fails the delivery check; with no pump entered there is no pump torque, but the power still follows from the flow.
        Assert.Contains(Run(Q("V_g", 20, "cm^3/rev")).Checks, c => c.Name.StartsWith("Pump delivery", StringComparison.Ordinal) && !c.Passed);

        var none = CalcRunner.Run(Cylinder, CalcTemplateHasher.DefaultInputs(Cylinder.Template).With(new CalcInputValue("V_g")).With(new CalcInputValue("n_p")));
        Assert.False(none.IsBlocked);
        Assert.Null(none.FindOutput("T_shaft"));
        Assert.Null(none.FindOutput("n_shaft"));
        Assert.Equal(p * 36 * Litre / 60 / 0.88, none.OutputSi("P_pump"), 6);
    }

    [Fact]
    public void A_Pump_Entered_Half_Way_Or_With_A_Total_Efficiency_Above_Its_Volumetric_One_Blocks()
    {
        var noSpeed = CalcRunner.Run(Cylinder, With(new CalcInputValue("n_p")));
        Assert.True(noSpeed.IsBlocked);
        Assert.StartsWith("n_p:", noSpeed.Blocks[0].Message, StringComparison.Ordinal);

        var noDisplacement = CalcRunner.Run(Cylinder, With(new CalcInputValue("V_g")));
        Assert.StartsWith("V_g:", noDisplacement.Blocks[0].Message, StringComparison.Ordinal);

        var impossible = Run(CalcInputValue.Of("eta_vp", 0.9), CalcInputValue.Of("eta_tp", 0.95));
        Assert.True(impossible.IsBlocked);
        Assert.StartsWith("eta_tp:", impossible.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Motor_Torque_Is_Pressure_Drop_Times_Displacement_Over_Two_Pi_Times_Efficiency()
    {
        Assert.Equal(160 * Bar * 16e-6 / (2 * Math.PI) * 0.9, Run().OutputSi("T_motor"), 9);
        Assert.Equal(100 * Bar * 40e-6 / (2 * Math.PI) * 0.85, Run(Q("V_gm", 40, "cm^3/rev"), Q("dp_m", 100, "bar"), CalcInputValue.Of("eta_hm", 0.85)).OutputSi("T_motor"), 9);

        var half = CalcRunner.Run(Cylinder, With(new CalcInputValue("dp_m")));
        Assert.True(half.IsBlocked);
        Assert.StartsWith("dp_m:", half.Blocks[0].Message, StringComparison.Ordinal);
        Assert.Null(CalcRunner.Run(Cylinder, With(new CalcInputValue("V_gm"), new CalcInputValue("dp_m"))).FindOutput("T_motor"));
    }

    [Fact]
    public void Line_Velocities_Are_Checked_Against_The_Limit_Records_And_The_Bore_Comes_From_The_List()
    {
        var r = Run();
        double qLine = 28e-6 * 1450 / 60 * 0.95, qSuc = 28e-6 * 1450 / 60, phi = Area(100) / (Area(100) - Area(70));
        Assert.Equal(qSuc / Area(40), r.OutputSi("v_suc"), 9);
        Assert.Equal(qLine * phi / Area(16), r.OutputSi("v_prs"), 9);
        Assert.Equal(qLine * phi / Area(25), r.OutputSi("v_rtn"), 9);

        // The smallest listed size not below the bore the limit needs; the order of the list does not matter.
        Assert.Equal(32 * Mm, Run(Hoses(50, 8, 32, 12.5, 16, 40, 20, 25, 10)).OutputSi("d_rec_suc"), 12);
        Assert.Equal(Math.Sqrt(4 * qSuc / (Math.PI * 1.2)), r.OutputSi("d_min_suc"), 9);

        // A bore at exactly the minimum is accepted (a hair of rounding allowed).
        var exact = Run(Hoses(8, Math.Sqrt(4 * qSuc / (Math.PI * 1.2)) / Mm, 50));
        Assert.Equal(Math.Sqrt(4 * qSuc / (Math.PI * 1.2)), exact.OutputSi("d_rec_suc"), 9);

        // A limit that is too tight fails the check and names the line; no list gives no recommendation.
        var tight = Run(Q("v_prs_max", 2, "m/s"));
        Assert.Contains(Run(Q("v_prs_max", 8, "m/s"), Q("v_ret_max", 2, "m/s")).Checks, c => c.Name.StartsWith("Return line velocity", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(tight.Checks, c => c.Name.StartsWith("Pressure line velocity", StringComparison.Ordinal) && !c.Passed);
        Assert.False(tight.Passed);
        Assert.Null(CalcRunner.Run(Cylinder, With(CalcInputValue.Table("hoses", []))).FindOutput("d_rec_suc"));
    }

    [Fact]
    public void A_Size_List_That_Is_Too_Small_Warns_And_Recommends_Nothing_For_That_Line()
    {
        var r = Run(Hoses(8, 10, 12.5, 16, 20));
        Assert.Contains(r.Warnings, w => w.RuleId == "size" && w.Message.Contains("suction", StringComparison.Ordinal));
        Assert.Null(r.FindOutput("d_rec_suc"));
        Assert.Equal(20 * Mm, r.OutputSi("d_rec_prs"), 12);
    }

    [Fact]
    public void The_Regenerative_Pressure_Line_Is_Judged_At_The_Larger_Cap_Side_Flow()
    {
        var standard = Run();
        var regen = Run(CalcInputValue.Choice("mode", CylinderCalculator.Regenerative));
        double ab = Area(100), ar = Area(70), aa = ab - ar;
        Assert.Equal(standard.OutputSi("v_prs") * aa / ar, regen.OutputSi("v_prs"), 9);
        Assert.True(regen.OutputSi("u_prs") > standard.OutputSi("u_prs"));
    }

    [Fact]
    public void The_Cap_Port_Line_Is_Judged_At_The_Larger_Flow_Of_The_Two_Strokes()
    {
        // M1: on the retract stroke the line to the cap port carries the cap-side discharge Q·A_b/A_a, not the supply flow.
        double ab = Area(100), ar = Area(70), aa = ab - ar, qLine = 28e-6 * 1450 / 60 * 0.95;
        var standard = Run();
        Assert.Equal(qLine * ab / aa, standard.OutputSi("Q_prs"), 12);
        Assert.Equal(standard.OutputSi("Q_rtn"), standard.OutputSi("Q_prs"), 12);
        Assert.Equal(qLine * ab / aa / Area(16), standard.OutputSi("v_prs"), 9);
        Assert.Equal(qLine * ab / aa / Area(16) / 5, standard.OutputSi("u_prs"), 9);

        // The review's example: the 16 mm line that read 3.20 m/s and passed carries 6.27 m/s, over its 5 m/s limit.
        Assert.InRange(standard.OutputSi("v_prs"), 6.26, 6.28);
        Assert.Contains(standard.Checks, c => c.Name.StartsWith("Pressure line velocity", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(Run(Q("d_prs", 20, "mm")).Checks, c => c.Name.StartsWith("Pressure line velocity", StringComparison.Ordinal) && c.Passed);

        // A rod larger than the annulus: the retract discharge Q·A_b/A_a governs a regenerative cap-port line, not Q·A_b/A_r.
        var big = Run(CalcInputValue.Choice("mode", CylinderCalculator.Regenerative), Q("d_r", 90, "mm"), Q("d_prs", 25, "mm"));
        double arBig = Area(90), aaBig = ab - arBig;
        Assert.True(aaBig < arBig);
        Assert.Equal(qLine * ab / aaBig, big.OutputSi("Q_prs"), 12);
        Assert.Equal(qLine * ab / aaBig / Area(25), big.OutputSi("v_prs"), 9);

        // Property: the cap-port flow is Q_line times the larger of the two area ratios, in both connections.
        Gen.Select(Gen.Double[20, 500], Gen.Double[0.2, 0.9], Gen.Double[1, 2000], Gen.Bool).Sample((bore, rodRatio, q, regen) =>
        {
            var r = Run(Q("D", bore, "mm"), Q("d_r", bore * rodRatio, "mm"), Q("Q", q, "L/min"), CalcInputValue.Choice("mode", regen ? CylinderCalculator.Regenerative : CylinderCalculator.Standard));
            double a1 = Area(bore), a2 = Area(bore * rodRatio), a3 = a1 - a2, line = r.OutputSi("Q_line");
            var expected = line * a1 / (regen ? Math.Min(a2, a3) : a3);
            return !r.IsBlocked && Math.Abs(r.OutputSi("Q_prs") - expected) <= 1e-9 * expected;
        }, iter: 100);
    }

    [Fact]
    public void The_Suction_Line_Carries_The_Geometric_Pump_Delivery()
    {
        // L7: the suction line carries about V_g·n, not the delivered flow V_g·n·eta_vp.
        var r = Run();
        double geometric = 28e-6 * 1450 / 60, delivered = geometric * 0.95;
        Assert.Equal(geometric, r.OutputSi("Q_suc"), 12);
        Assert.Equal(delivered, r.OutputSi("Q_line"), 12);
        Assert.Equal(geometric / Area(40), r.OutputSi("v_suc"), 9);
        Assert.True(r.OutputSi("v_suc") > delivered / Area(40));

        // With no pump entered the suction line carries the circuit flow; a small pump never lowers it below the circuit flow.
        var none = CalcRunner.Run(Cylinder, With(new CalcInputValue("V_g"), new CalcInputValue("n_p")));
        Assert.Equal(36 * Litre / 60, none.OutputSi("Q_suc"), 12);
        Assert.Equal(36 * Litre / 60, Run(Q("V_g", 20, "cm^3/rev")).OutputSi("Q_suc"), 12);
    }

    [Fact]
    public void A_Stalled_Cylinder_Loads_The_Rod_With_Its_Thrust_So_A_Slender_Rod_Fails_Whatever_The_Push_Load_Entered()
    {
        // H1: 40 mm rod over 2100 mm, Euler F_cr = 59.06 kN. A stalled cylinder at 160 bar carries p·A_b = 80.42 kN (76.40 kN with the seal friction, which does not relieve a stalled rod), above F_cr even with a safety factor of 1.
        var slender = Run(Q("D", 80, "mm"), Q("d_r", 40, "mm"), Q("stroke", 2000, "mm"), Q("F_push", 15, "kN"));
        double thrust = 160 * Bar * Area(80) * 0.95, stall = 160 * Bar * Area(80), fcr = Math.PI * Math.PI * 210e9 * (Math.PI * Math.Pow(0.04, 4) / 64) / (2.1 * 2.1);
        Assert.Equal(thrust, slender.OutputSi("F_ext"), 6);
        Assert.Equal(stall, slender.OutputSi("F_stall"), 6);
        Assert.Equal(stall, slender.OutputSi("F_buck"), 6);
        Assert.True(slender.OutputSi("F_buck") > slender.OutputSi("F_ext"));
        Assert.Equal(stall * 3.5 / fcr, slender.OutputSi("u_buck"), 9);
        Assert.InRange(slender.OutputSi("u_buck"), 4.76, 4.77);
        Assert.Contains(slender.Checks, c => c.Name.StartsWith("Rod buckling", StringComparison.Ordinal) && !c.Passed);
        Assert.False(slender.Passed);

        // Even with a safety factor of 1 the rod buckles at the thrust.
        Assert.True(Run(Q("D", 80, "mm"), Q("d_r", 40, "mm"), Q("stroke", 2000, "mm"), Q("F_push", 15, "kN"), CalcInputValue.Of("S_f", 1)).OutputSi("u_buck") > 1);

        // Property: the load judged is the larger of the push load entered and the stall thrust p·A_b (no η_m, no back pressure), in both connections, and the utilisation follows from it.
        Gen.Select(Gen.Double[0, 400], Gen.Double[20, 200], Gen.Double[0.3, 0.8], Gen.Double[50, 3000], Gen.Bool).Sample((push, bore, rodRatio, stroke, regen) =>
        {
            var r = Run(Q("F_push", push, "kN"), Q("D", bore, "mm"), Q("d_r", bore * rodRatio, "mm"), Q("stroke", stroke, "mm"), CalcInputValue.Choice("mode", regen ? CylinderCalculator.Regenerative : CylinderCalculator.Standard));
            var expected = Math.Max(push * 1e3, 160 * Bar * Area(bore));
            return !r.IsBlocked && Math.Abs(r.OutputSi("F_buck") - expected) <= 1e-9 * expected && Math.Abs(r.OutputSi("u_buck") - expected * 3.5 / r.OutputSi("F_cr")) <= 1e-9 * Math.Max(1, r.OutputSi("u_buck"));
        }, iter: 100);

        // A push load above the stall thrust is the load judged.
        var heavy = Run(Q("F_push", 200, "kN"));
        Assert.Equal(200e3, heavy.OutputSi("F_buck"), 6);
    }

    [Fact]
    public void The_Mounting_Factor_And_The_Safety_Factor_Are_Records_And_The_Defaults_Are_Said_To_Be_Placeholders()
    {
        var kinds = Cylinder.Template.RecordKinds!;
        Assert.Contains(kinds, k => k.Kind == "cylinder.mounting" && k.Symbols.SequenceEqual(["K_m"]) && k.Guidance.Contains("fixed-free", StringComparison.Ordinal) && k.Guidance.Contains("K = 2", StringComparison.Ordinal));
        Assert.Contains(kinds, k => k.Kind == "cylinder.buckling-safety" && k.Symbols.SequenceEqual(["S_f"]));
        Assert.Contains(Cylinder.Template.Assumptions, a => a.Statement.Contains("K_m = 2", StringComparison.Ordinal) && a.Statement.Contains("pressure-limiting", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Stall_Thrust_Carries_No_Seal_Friction_And_The_Regenerative_Extend_Is_Judged_At_The_Standard_Connection_Thrust()
    {
        // L-a: a stalled rod is not relieved by the seal friction: F_stall = p·A_b, not p·A_b·eta_m.
        var standard = Run(Q("F_push", 10, "kN"));
        Assert.Equal(160 * Bar * Area(100), standard.OutputSi("F_stall"), 6);
        Assert.True(standard.OutputSi("F_stall") > standard.OutputSi("F_ext"));
        Assert.Equal(standard.OutputSi("F_ext") / 0.95, standard.OutputSi("F_buck"), 6);

        // L-b: the regenerative thrust p·A_r·eta_m is the lower one; the rod is judged at the standard connection's p·A_b, which the valve can always select.
        var regen = Run(Q("F_push", 10, "kN"), CalcInputValue.Choice("mode", CylinderCalculator.Regenerative));
        Assert.True(regen.OutputSi("F_ext") < standard.OutputSi("F_ext"));
        Assert.Equal(standard.OutputSi("F_buck"), regen.OutputSi("F_buck"), 6);
        Assert.Equal(standard.OutputSi("u_buck"), regen.OutputSi("u_buck"), 9);

        // The back pressure does not enter (no flow at stall), and a bigger push load than the stall thrust is the load.
        Assert.Equal(standard.OutputSi("F_buck"), Run(Q("F_push", 10, "kN"), Q("p_back", 10, "bar")).OutputSi("F_buck"), 6);
        Assert.Equal(300e3, Run(Q("F_push", 300, "kN")).OutputSi("F_buck"), 6);
    }

    [Fact]
    public void The_Template_Version_Is_Bumped_For_The_Changed_Model()
    {
        Assert.Equal("1.3.0", Cylinder.Template.Version);
    }

    [Fact]
    public void A_Required_Extend_Speed_Gives_The_Flow_And_The_Entered_Flow_Is_Blank_Or_Ignored_With_A_Notice()
    {
        // 150 mm/s on the 100 mm bore at η_vc 0.98: Q = v·A_b/η_vc = 0.15 · 7.854e-3 / 0.98 = 1.2021e-3 m³/s = 72.13 L/min.
        double ab = Area(100), expected = 0.15 * ab / 0.98;
        var blank = CalcRunner.Run(Cylinder, With(Q("v_req", 150, "mm/s"), new CalcInputValue("Q")));
        Assert.False(blank.IsBlocked);
        Assert.Equal(expected, blank.OutputSi("Q"), 12);
        Assert.Equal(0.15, blank.OutputSi("v_ext"), 12);
        Assert.Equal(0.5 / 0.15, blank.OutputSi("t_ext"), 9);
        Assert.DoesNotContain(blank.Warnings, w => w.RuleId == "v-req-q");

        // The retract speed and the line flows follow the derived flow, not a flow that is also entered.
        var both = Run(Q("v_req", 150, "mm/s"), Q("Q", 50, "L/min"));
        Assert.Equal(expected, both.OutputSi("Q"), 12);
        Assert.Contains(both.Warnings, w => w.RuleId == "v-req-q");

        // A flow still at the sheet's opening 36 L/min is not an entry to warn about (a new sheet given only the speed), and the speed still wins.
        var atDefault = Run(Q("v_req", 150, "mm/s"));
        Assert.Equal(expected, atDefault.OutputSi("Q"), 12);
        Assert.DoesNotContain(atDefault.Warnings, w => w.RuleId == "v-req-q");

        // The comparison is tied to the Q input's own default through the shared constant, so the two cannot drift apart; any other flow, however close, warns.
        Assert.Equal(CylinderCalculator.DefaultFlowLpm, Cylinder.Template.Inputs.Single(i => i.Symbol == "Q").Default);
        Assert.Contains(Run(Q("v_req", 150, "mm/s"), Q("Q", CylinderCalculator.DefaultFlowLpm + 0.5, "L/min")).Warnings, w => w.RuleId == "v-req-q");
        Assert.DoesNotContain(Run(Q("v_req", 150, "mm/s"), Q("Q", CylinderCalculator.DefaultFlowLpm, "L/min")).Warnings, w => w.RuleId == "v-req-q");
        Assert.Equal(blank.OutputSi("Q_prs"), both.OutputSi("Q_prs"), 12);

        // The regenerative extend runs on the rod area: Q = v·A_r/η_vc.
        var regen = CalcRunner.Run(Cylinder, With(Q("v_req", 150, "mm/s"), new CalcInputValue("Q"), CalcInputValue.Choice("mode", CylinderCalculator.Regenerative), Q("d_prs", 25, "mm")));
        Assert.Equal(0.15 * Area(70) / 0.98, regen.OutputSi("Q"), 12);
        Assert.Equal(0.15, regen.OutputSi("v_ext"), 12);
    }

    [Fact]
    public void Neither_A_Flow_Nor_A_Required_Speed_Blocks_And_A_Speed_Needing_A_Flow_Outside_The_Range_Blocks()
    {
        var none = CalcRunner.Run(Cylinder, With(new CalcInputValue("Q")));
        Assert.True(none.IsBlocked);
        Assert.StartsWith("Q:", none.Blocks[0].Message, StringComparison.Ordinal);

        // 5000 mm/s on the 100 mm bore needs about 2400 L/min, above the 2000 L/min this sheet covers; 0.1 mm/s needs 0.05 L/min, below 0.1.
        var fast = CalcRunner.Run(Cylinder, With(Q("v_req", 5000, "mm/s"), new CalcInputValue("Q")));
        Assert.True(fast.IsBlocked);
        Assert.StartsWith("v_req:", fast.Blocks[0].Message, StringComparison.Ordinal);
        var slow = CalcRunner.Run(Cylinder, With(Q("v_req", 0.1, "mm/s"), new CalcInputValue("Q")));
        Assert.True(slow.IsBlocked);
        Assert.StartsWith("v_req:", slow.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Line_Flows_Are_Said_To_Mix_The_Delivered_Flow_With_The_Circuit_Flow()
    {
        // L9: Q_line carries no volumetric efficiency while the speeds use Q·eta_vc; the assumption says so.
        Assert.Contains(Cylinder.Template.Assumptions, a => a.Statement.Contains("no volumetric efficiency", StringComparison.Ordinal) && a.Statement.Contains("conservative", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Orifice_Passes_Cd_Times_Area_Times_The_Jet_Velocity_And_The_Bore_For_The_Flow_Round_Trips()
    {
        var r = Run(Q("rho", 800, "kg/m^3"), CalcInputValue.Of("C_d", 0.6), Q("dp_o", 10, "bar"), Q("d_o", 4, "mm"));
        Assert.Equal(0.6 * Area(4) * 50, r.OutputSi("Q_o"), 12);

        // Property: an orifice of the diameter the sheet asks for passes the circuit flow.
        Gen.Select(Gen.Double[0.5, 600], Gen.Double[0.5, 1000], Gen.Double[0.3, 1], Gen.Double[700, 1300]).Sample((dp, q, cd, rho) =>
        {
            var need = Run(Q("dp_o", dp, "bar"), Q("Q", q, "L/min"), CalcInputValue.Of("C_d", cd), Q("rho", rho, "kg/m^3"), Q("p", 700, "bar"));
            var d = need.OutputSi("d_o_req");
            var check = Run(Q("dp_o", dp, "bar"), Q("Q", q, "L/min"), CalcInputValue.Of("C_d", cd), Q("rho", rho, "kg/m^3"), Q("p", 700, "bar"), Q("d_o", d / Mm, "mm"));
            return d / Mm is >= 0.1 and <= 100 && Math.Abs(check.OutputSi("Q_o") - q * Litre / 60) <= 1e-9 * q * Litre / 60;
        }, iter: 60);

        var half = CalcRunner.Run(Cylinder, With(Q("d_o", 4, "mm"), new CalcInputValue("dp_o")));
        Assert.True(half.IsBlocked);
        Assert.StartsWith("dp_o:", half.Blocks[0].Message, StringComparison.Ordinal);

        var above = Run(Q("p", 100, "bar"), Q("dp_o", 150, "bar"));
        Assert.True(above.IsBlocked);
        Assert.StartsWith("dp_o:", above.Blocks[0].Message, StringComparison.Ordinal);
        Assert.Null(CalcRunner.Run(Cylinder, With(new CalcInputValue("dp_o"), new CalcInputValue("d_o"))).FindOutput("d_o_req"));
    }

    [Fact]
    public void A_Slender_Rod_Buckles_As_Euler_And_A_Stocky_One_As_Johnson_And_The_Sheet_Says_Screening()
    {
        var slender = Run(Q("D", 80, "mm"), Q("d_r", 40, "mm"), Q("stroke", 2000, "mm"), Q("F_push", 15, "kN"));
        Assert.Equal(Math.PI * Math.PI * 210e9 * (Math.PI * Math.Pow(0.04, 4) / 64) / (2.1 * 2.1), slender.OutputSi("F_cr"), 3);
        Assert.Contains("slender", Assert.IsType<TextValue>(slender.FindOutput("buckling_note")!.Value).Value, StringComparison.Ordinal);

        var stocky = Run();
        Assert.Contains("Johnson", Assert.IsType<TextValue>(stocky.FindOutput("buckling_note")!.Value).Value, StringComparison.Ordinal);
        Assert.True(stocky.OutputSi("F_cr") < Math.PI * 0.035 * 0.035 * 500e6);

        foreach (var r in new[] { slender, stocky })
        {
            Assert.Contains(CylinderCalculator.ScreeningLabel, Assert.IsType<TextValue>(r.FindOutput("buckling_note")!.Value).Value, StringComparison.Ordinal);
            Assert.Contains(r.Checks, c => c.Name.Contains(CylinderCalculator.ScreeningLabel, StringComparison.Ordinal));
        }

        Assert.Contains(CylinderCalculator.ScreeningLabel, Cylinder.Template.Scope, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Critical_Load_Is_Johnson_Below_The_Transition_Euler_Above_It_And_Never_Exceeds_Either_Limit()
    {
        // Property: independent recomputation of the column relations, including the continuity at the transition slenderness.
        Gen.Select(Gen.Double[20, 300], Gen.Double[0.3, 0.8], Gen.Double[50, 3000], Gen.Double[0.5, 2.5], Gen.Double[100, 1500], Gen.Double[50, 250]).Sample((bore, rodRatio, stroke, k, fy, e) =>
        {
            var d = bore * rodRatio;
            var r = Run(Q("D", bore, "mm"), Q("d_r", d, "mm"), Q("stroke", stroke, "mm"), CalcInputValue.Of("K_m", k), Q("f_y", fy, "MPa"), Q("E", e, "GPa"));
            double leff = k * (stroke + 100), lam = leff / (d / 4), lamC = Math.PI * Math.Sqrt(2 * e * 1e3 / fy);
            double a = Math.PI * d * d / 4, i = Math.PI * Math.Pow(d, 4) / 64;
            var euler = Math.PI * Math.PI * e * 1e3 * i / (leff * leff);
            var johnson = a * fy * (1 - fy * lam * lam / (4 * Math.PI * Math.PI * e * 1e3));
            var expected = (lam > lamC ? euler : johnson) / 1e3;
            var got = r.OutputSi("F_cr") / 1e3;
            return !r.IsBlocked && got > 0 && Math.Abs(got - expected) <= 1e-9 * expected && got <= a * fy / 1e3 * (1 + 1e-12) && got <= euler / 1e3 * (1 + 1e-12);
        }, iter: 200);
    }

    [Fact]
    public void The_Mounting_Factor_Scales_The_Effective_Length_And_The_Push_Load_Sets_The_Utilisation()
    {
        var pinned = Run(Q("stroke", 1500, "mm"));
        var cantilever = Run(Q("stroke", 1500, "mm"), CalcInputValue.Of("K_m", 2));
        Assert.Equal(1.6, pinned.OutputSi("L_eff"), 9);
        Assert.Equal(3.2, cantilever.OutputSi("L_eff"), 9);
        Assert.True(cantilever.OutputSi("F_cr") < pinned.OutputSi("F_cr"));
        // The rod is judged at the larger of the push load and the stall thrust p·A_b (here the stall thrust, 125.7 kN).
        Assert.Equal(160 * Bar * Area(100) * 3.5 / pinned.OutputSi("F_cr"), pinned.OutputSi("u_buck"), 9);
        Assert.Equal(160 * Bar * Area(100), pinned.OutputSi("F_buck"), 6);
        Assert.Equal(pinned.OutputSi("u_buck"), Run(Q("F_push", 0, "kN"), Q("stroke", 1500, "mm")).OutputSi("u_buck"), 12);

        var over = Run(Q("stroke", 3500, "mm"), Q("F_push", 100, "kN"));
        Assert.Contains(over.Checks, c => c.Name.StartsWith("Rod buckling", StringComparison.Ordinal) && !c.Passed);
        Assert.False(over.Passed);
    }

    [Fact]
    public void A_Load_Above_The_Force_Of_Either_Stroke_Fails_The_Check()
    {
        Assert.True(Run(Q("d_prs", 20, "mm")).Passed);
        Assert.Contains(Run(Q("F_push", 130, "kN")).Checks, c => c.Name.StartsWith("Extend force covers", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(Run(Q("F_pull", 70, "kN")).Checks, c => c.Name.StartsWith("Retract force covers", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void The_Material_The_Mounting_The_Limits_And_The_Hose_List_Are_Records_And_Nothing_Is_Shipped_As_A_Table()
    {
        var t = Cylinder.Template;
        Assert.Equal("E", t.FindInput("E")!.MaterialProperty);
        Assert.Equal("f_y", t.FindInput("f_y")!.MaterialProperty);
        Assert.Null(t.FindInput("rho")!.MaterialProperty);
        Assert.Null(t.FindInput("K_m")!.MaterialProperty);
        Assert.Equal(["E", "f_y"], t.RecordKinds!.Single(k => k.Kind == CylinderCalculator.MaterialKind).Symbols);
        Assert.Contains(t.RecordKinds!, k => k.Kind == "cylinder.mounting" && k.Symbols.SequenceEqual(["K_m"]));
        Assert.Contains(t.RecordKinds!, k => k.Kind == "cylinder.buckling-safety" && k.Symbols.SequenceEqual(["S_f"]));
        Assert.Contains(t.RecordKinds!, k => k.Kind == "cylinder.line-limits" && k.Symbols.SequenceEqual(["v_suc_max", "v_prs_max", "v_ret_max"]));
        Assert.Contains(t.RecordKinds!, k => k.Kind == "cylinder.hose-sizes" && k.Symbols.SequenceEqual(["hoses"]));
        Assert.Equal(CalcInputKind.Table, t.FindInput("hoses")!.Kind);
        Assert.True(t.FindInput("hoses")!.IsOptional);
    }

    [Fact]
    public void The_Ports_Are_Outputs_Of_A_Default_Run_With_The_Declared_Units()
    {
        var r = Run();
        Assert.Equal(["Q", "p", "P_pump", "u_buck", "F_ext", "dV", "T_shaft", "n_shaft", "Q_prs", "rho", "d_prs"], Cylinder.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.Equal(870, r.OutputSi("rho"), 9);
        Assert.Equal(0.016, r.OutputSi("d_prs"), 12);
        Assert.Equal(r.OutputSi("Q_rtn"), r.OutputSi("Q_prs"), 12);

        foreach (var port in Cylinder.Template.OutputPorts!)
        {
            var scalar = Assert.IsType<ScalarValue>(r.FindOutput(port.Symbol)!.Value);
            Assert.Equal(UnitCatalog.Parse(port.Unit).Dimension, scalar.Dimension);
        }
    }

    [Fact]
    public void Every_Input_Outside_Its_Valid_Range_Is_A_Hard_Fail()
    {
        Assert.All(Cylinder.Template.Inputs.Where(i => i.Kind is CalcInputKind.Quantity or CalcInputKind.Number), spec =>
        {
            Assert.Equal(spec.Min, spec.HardMin);
            Assert.Equal(spec.Max, spec.HardMax);
        });

        foreach (var (symbol, value, unit) in new[]
        {
            ("D", 9.0, "mm"), ("D", 501.0, "mm"), ("stroke", 0.5, "mm"), ("stroke", 5001.0, "mm"), ("p", 0.5, "bar"), ("p", 701.0, "bar"),
            ("Q", 0.05, "L/min"), ("Q", 2001.0, "L/min"), ("eta_m", 0.4, ""), ("eta_vc", 1.1, ""), ("eta_vp", 0.49, ""), ("eta_tp", 1.01, ""), ("eta_hm", 0.3, ""),
            ("K_m", 0.4, ""), ("K_m", 2.6, ""), ("rho", 600.0, "kg/m^3"), ("E", 40.0, "GPa"), ("f_y", 50.0, "MPa"), ("S_f", 0.9, ""), ("d_suc", 2.0, "mm"),
        })
        {
            var value1 = unit.Length > 0 ? Q(symbol, value, unit) : CalcInputValue.Of(symbol, value);
            Assert.Contains(CalcInputValidator.Validate(Cylinder.Template, With(value1)), n => n.Severity == CalcNoticeSeverity.Block);
        }
    }

    [Fact]
    public void Yellow_Y_B_06_The_Supply_And_Back_Pressure_Print_As_Gauge_And_Pressure_Drops_Stay_Plain_Bar()
    {
        var inputs = With();
        var result = CalcRunner.Run(Cylinder, inputs);
        var sheet = CalcSheetBuilder.Build(Cylinder.Template, inputs, result, GoldenFiles.FixedHeader(Cylinder.Template.Id, "F1"), GoldenFiles.FixedFooter(Cylinder.Template, 0, 0));

        Assert.Equal("bar g", sheet.Inputs.Single(i => i.Symbol == "p").Unit);
        Assert.Equal("bar g", sheet.Inputs.Single(i => i.Symbol == "p_back").Unit);
        Assert.EndsWith("700 bar g", sheet.Inputs.Single(i => i.Symbol == "p").ValidRange, StringComparison.Ordinal);

        // A pressure drop is a difference between two pressures and has no basis.
        Assert.Equal("bar", sheet.Inputs.Single(i => i.Symbol == "dp_m").Unit);
        Assert.Equal("bar", sheet.Inputs.Single(i => i.Symbol == "dp_o").Unit);

        // The working shows the supply pressure with its basis; the numbers are the bar's.
        Assert.Contains("160 bar g", sheet.Working.Single(w => w.Symbol == "F_ext").Substitution, StringComparison.Ordinal);
        Assert.Equal(160 * Bar * Area(100) * 0.95, result.OutputSi("F_ext"), 6);
    }
}
