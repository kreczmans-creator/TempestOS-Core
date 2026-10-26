using CsCheck;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class BoltTests
{
    private static readonly BoltedJointCalculator Joint = new();
    private static readonly BoltGroupCalculator Group = new();

    private static CalcInputSet JointInputs() => CalcTemplateHasher.DefaultInputs(Joint.Template);

    [Fact]
    public void Nut_Factor_And_Friction_Methods_Agree_For_A_Matching_K()
    {
        var friction = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Choice("method", "friction")));
        var fm = friction.OutputSi("F_M");
        var k = 93.0 / (fm * 0.012);
        var nut = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Choice("method", "nut factor")).With(CalcInputValue.Of("K", k)));
        Assert.Equal(fm, nut.OutputSi("F_M"), 3);
    }

    [Fact]
    public void Scatter_Reduces_Residual_Clamp_Not_Maximum_Bolt_Load()
    {
        var tight = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Of("alpha_A", 1.0)));
        var loose = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Of("alpha_A", 2.0)));
        Assert.True(loose.OutputSi("F_KR") < tight.OutputSi("F_KR"));
        Assert.Equal(tight.OutputSi("F_S_max"), loose.OutputSi("F_S_max"), 9);
    }

    [Fact]
    public void Separation_Is_Reported_Under_A_Large_Working_Load()
    {
        var r = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("F_A", 60, "kN")));
        Assert.Contains(r.Warnings, w => w.RuleId == "separation");
        Assert.Contains(r.Checks, c => c.Name == "No separation" && !c.Passed);
        Assert.False(r.Passed);
    }

    [Fact]
    public void Separation_Load_Factor_Is_Where_The_Residual_Clamp_Reaches_Zero()
    {
        // ADR-0167: the legacy joint-diagram load factor n_0 = F_A,sep / F_A, checked by re-running at F_A,sep.
        var r = CalcRunner.Run(Joint, JointInputs());
        var sep = r.OutputSi("F_A_sep");
        Assert.Equal(sep / 10_000, r.OutputSi("n_0"), 9);
        var atSep = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("F_A", sep / 1000, "kN")));
        Assert.True(Math.Abs(atSep.OutputSi("F_KR")) < 1e-6 * r.OutputSi("F_M"), $"F_KR at F_A,sep is {atSep.OutputSi("F_KR")} N");
    }

    [Fact]
    public void Yield_Load_Factor_Is_Where_The_Bolt_Load_Reaches_The_Proof_Load()
    {
        var r = CalcRunner.Run(Joint, JointInputs());
        var nL = r.OutputSi("n_L");
        Assert.True(nL > 1);
        var atYield = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("F_A", 10 * nL, "kN")));
        Assert.Equal(640e6 * r.OutputSi("A_s"), atYield.OutputSi("F_S_max"), 6);
    }

    [Fact]
    public void Load_Factors_Are_Omitted_Without_A_Working_Load()
    {
        var r = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("F_A", 0, "kN")));
        Assert.False(r.IsBlocked);
        Assert.Null(r.FindOutput("n_0"));
        Assert.Null(r.FindOutput("n_L"));
        Assert.NotNull(r.FindOutput("F_A_sep"));
    }

    [Fact]
    public void Load_Factor_Lies_Between_Zero_And_The_Introduction_Factor()
    {
        Gen.Select(Gen.Double[0, 1], Gen.Double[10, 200], Gen.Double[6, 30]).Sample((n, lk, d) =>
        {
            var inputs = JointInputs().With(CalcInputValue.Of("n", n)).With(CalcInputValue.Quantity("l_k", lk, "mm")).With(CalcInputValue.Quantity("d", d, "mm")).With(CalcInputValue.Quantity("P", d / 8, "mm")).With(CalcInputValue.Quantity("d_h", d + 1, "mm")).With(CalcInputValue.Quantity("d_w", 1.5 * d, "mm"));
            var r = CalcRunner.Run(Joint, inputs);
            return r.IsBlocked || (r.OutputSi("Phi") >= 0 && r.OutputSi("Phi") <= n + 1e-12);
        }, iter: 60);
    }

    [Fact]
    public void Once_The_Joint_Opens_The_Bolt_Carries_The_Whole_Load_And_The_Clamp_Is_Zero()
    {
        // Green G-05 (the legacy module's own branch): no negative clamp, no bolt load below the working load.
        var r = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("F_A", 80, "kN")));
        Assert.Equal(0, r.OutputSi("F_KR"), 9);
        Assert.Equal(80e3, r.OutputSi("F_S_max"), 3);
        Assert.Equal((80e3 - r.OutputSi("F_M")) / (2 * r.OutputSi("A_s")), r.OutputSi("sigma_a"), 0);
        Assert.Contains(r.Checks, c => c.Name == "Residual clamp against requirement" && !c.Passed);
    }

    [Fact]
    public void Given_Preload_Needs_F_V_And_Given_Properties_Replace_The_Derived_Ones()
    {
        var given = JointInputs().With(CalcInputValue.Choice("method", BoltedJointCalculator.GivenPreload));
        Assert.Contains(CalcRunner.Run(Joint, new CalcInputSet(given.Values.Where(v => v.Symbol != "F_V"))).Blocks, b => b.RuleId == "no-preload");

        var r = CalcRunner.Run(Joint, given.With(CalcInputValue.Quantity("F_V", 30, "kN")).With(CalcInputValue.Quantity("k_S_given", 300, "kN/mm")).With(CalcInputValue.Quantity("k_P_given", 900, "kN/mm")).With(CalcInputValue.Quantity("A_s_given", 84.3, "mm^2")));
        Assert.Equal(30e3, r.OutputSi("F_M"), 6);
        Assert.Equal(84.3e-6, r.OutputSi("A_s"), 12);
        Assert.Equal(0.5 * 300.0 / (300 + 900), r.OutputSi("Phi"), 9);
    }

    [Fact]
    public void Hole_Geometry_Blocks_Plainly()
    {
        var r = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Quantity("d_h", 11, "mm")));
        Assert.True(r.IsBlocked);
        Assert.StartsWith("d_h:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    private static CalcInputValue Bolts(params (double X, double Y)[] points) =>
        CalcInputValue.Table("bolts", points.Select(p => CalcRow.Of(("x", new CalcCell(p.X, "mm")), ("y", new CalcCell(p.Y, "mm")), ("A", new CalcCell(84.3, "mm^2")))).ToList());

    private static CalcInputSet GroupInputs(CalcInputValue bolts) => CalcTemplateHasher.DefaultInputs(Group.Template).With(bolts);

    [Fact]
    public void The_Linear_Interaction_Is_Evaluated_At_Every_Bolt_Not_Only_At_The_Bolt_That_Governs_The_Circular_One()
    {
        // mech.bolt-group-01: 3 x 2 grid (x 0, 80, 160; y 0, 100; A 84.3 mm2), F_x 25, F_y -75 kN at (300, 100), N 55, M_x -5 kN.m, V_R 60, T_R 90 kN. Bolt 6 at (160, 100) governs the circular form (0.9024) and
        // the sheet checked the linear form there too (0.9024, a pass), while bolt 3 at (160, 0) carries v 50.665 and t 25.833 kN: 50.665/60 + 25.833/(1.4 x 90) = 1.0494, a FAIL.
        // python3: centroid (80, 50), T = -75 x 220 - 25 x 50 = -17 750 kN.mm, J = 40 600 A, vx = Fx/6 - T ry/J, vy = Fy/6 + T rx/J, t = N/6 + M_x ry/sum(ry^2).
        var bolts = Bolts((0, 0), (80, 0), (160, 0), (0, 100), (80, 100), (160, 100));
        var r = CalcRunner.Run(Group, GroupInputs(bolts)
            .With(CalcInputValue.Quantity("F_x", 25, "kN")).With(CalcInputValue.Quantity("F_y", -75, "kN")).With(CalcInputValue.Quantity("e_x", 300, "mm")).With(CalcInputValue.Quantity("e_y", 100, "mm"))
            .With(CalcInputValue.Quantity("N", 55, "kN")).With(CalcInputValue.Quantity("M_x", -5, "kN*m")).With(CalcInputValue.Quantity("V_R", 60, "kN")).With(CalcInputValue.Quantity("T_R", 90, "kN")));

        Assert.False(r.IsBlocked);
        Assert.Equal("bolt 6", ((Tempest.Calc.Engine.Values.TextValue)r.FindOutput("governing")!.Value).Value);
        Assert.Equal(0.9024, r.OutputSi("u_max"), 3);
        Assert.Equal("bolt 3", ((Tempest.Calc.Engine.Values.TextValue)r.FindOutput("governing_lin")!.Value).Value);
        Assert.Equal(50.665 / 60 + 25.833 / (1.4 * 90), r.OutputSi("u_lin"), 3);
        Assert.Equal(1.0494, r.OutputSi("u_lin"), 3);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Linear shear", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Circular shear", StringComparison.Ordinal) && c.Passed);
        Assert.False(r.Passed);

        // Property: u_lin is never below the linear form at the circular-governing bolt (the old value), and for a single-row pattern with no torque the two bolts agree.
        var square = CalcRunner.Run(Group, GroupInputs(Bolts((0, 0), (100, 0), (100, 100), (0, 100))).With(CalcInputValue.Quantity("e_x", 50, "mm")).With(CalcInputValue.Quantity("e_y", 50, "mm")));
        Assert.Equal(square.OutputSi("v_max") / 40_000 + square.OutputSi("t_max") / (1.4 * 45_000), square.OutputSi("u_lin"), 9);
    }

    [Fact]
    public void The_Assembly_Stress_Is_A_Check_Not_Only_A_Warning()
    {
        // mech.bolted-joint-R1: M12 8.8, friction method, M_A 95 N.m: sigma_red,M 736.2 MPa against R_p0.2 640 MPa, nu_M 1.150, the bolt yields in tightening, and all five checks passed.
        // python3: d2 = 10.8634, d3 = 9.8529, A_s = 84.266 mm2, F_M = 95/(0.28 + 0.7561 + 0.93) mm = 48.319 kN, sigma_M 573.41, tau_M 266.56, sigma_red 736.18, nu_M = 1.15028.
        var over = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Choice("method", "friction")).With(CalcInputValue.Quantity("M_A", 95, "N*m")));
        Assert.Equal(1.15028, over.OutputSi("nu_M"), 5);
        Assert.Equal(1.15028, over.OutputSi("u_assembly"), 5);
        Assert.Contains(over.Checks, c => c.Name.StartsWith("Equivalent stress at assembly", StringComparison.Ordinal) && !c.Passed);
        Assert.False(over.Passed);
        Assert.Contains(over.Warnings, w => w.RuleId == "preload-yield" && w.Message.Contains("yields in tightening", StringComparison.Ordinal));

        // M_A 80 N.m (the worked example): nu_M 0.96866 passes against the default permitted 1.0; a permitted 0.9 (the usual VDI 2230 target) makes it a FAIL, 0.96866/0.9 = 1.0763.
        var ok = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Choice("method", "friction")).With(CalcInputValue.Quantity("M_A", 80, "N*m")));
        Assert.Equal(0.96866, ok.OutputSi("nu_M"), 5);
        Assert.Contains(ok.Checks, c => c.Name.StartsWith("Equivalent stress at assembly", StringComparison.Ordinal) && c.Passed);
        var strict = CalcRunner.Run(Joint, JointInputs().With(CalcInputValue.Choice("method", "friction")).With(CalcInputValue.Quantity("M_A", 80, "N*m")).With(CalcInputValue.Of("nu_perm", 0.9)));
        Assert.Equal(0.96866 / 0.9, strict.OutputSi("u_assembly"), 4);
        Assert.Contains(strict.Checks, c => c.Name.StartsWith("Equivalent stress at assembly", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void Symmetric_Pattern_Under_Central_Load_Shares_Shear_Equally()
    {
        var r = CalcRunner.Run(Group, GroupInputs(Bolts((0, 0), (100, 0), (100, 100), (0, 100))).With(CalcInputValue.Quantity("e_x", 50, "mm")).With(CalcInputValue.Quantity("e_y", 50, "mm")));
        Assert.Equal(0, r.OutputSi("T"), 9);
        Assert.Equal(12.5e3, r.OutputSi("v_max"), 6);
    }

    [Fact]
    public void Prying_Raises_Tension_By_The_Lever_Ratio()
    {
        var baseline = GroupInputs(Bolts((0, -100), (100, -100), (0, 100), (100, 100))).With(CalcInputValue.Quantity("M_x", 20, "kN*m")).With(CalcInputValue.Quantity("F_y", 0, "kN"));
        var without = CalcRunner.Run(Group, baseline);
        var with = CalcRunner.Run(Group, baseline.With(CalcInputValue.Quantity("a", 30, "mm")).With(CalcInputValue.Quantity("b", 15, "mm")));
        Assert.Equal(1.5 * without.OutputSi("t_max"), with.OutputSi("t_max"), 6);
    }

    [Fact]
    public void One_Bolt_Or_Coincident_Bolts_Block()
    {
        Assert.Contains(CalcRunner.Run(Group, GroupInputs(Bolts((0, 0)))).Blocks, b => b.RuleId == "few-bolts");
        Assert.Contains(CalcRunner.Run(Group, GroupInputs(Bolts((0, 0), (0, 0)))).Blocks, b => b.RuleId == "coincident");
    }

    [Fact]
    public void A_Single_Bolt_Takes_Direct_Shear_But_Not_A_Torque()
    {
        var through = GroupInputs(Bolts((0, 0))).With(CalcInputValue.Quantity("e_x", 0, "mm")).With(CalcInputValue.Quantity("e_y", 0, "mm"));
        var r = CalcRunner.Run(Group, through);
        Assert.False(r.IsBlocked);
        Assert.Equal(50e3, r.OutputSi("v_max"), 3);
        Assert.Contains(CalcRunner.Run(Group, through.With(CalcInputValue.Quantity("T_z", 1, "kN*m"))).Blocks, b => b.RuleId == "few-bolts");
    }

    [Fact]
    public void A_Moment_About_The_Line_Of_The_Bolts_Is_Blocked_Not_Dropped()
    {
        // Green G-06: bolts all on y = 0 give M_x no lever; it used to be silently ignored. The shared group-geometry kernel (Green G-A-01) blocks it
        // under the rule "collinear", which also covers a line at any angle; the v0.27.0 rule "unresisted-moment" was folded into it on integration.
        var inLine = GroupInputs(Bolts((0, 0), (100, 0), (200, 0))).With(CalcInputValue.Quantity("M_x", 5, "kN*m"));
        Assert.Contains(CalcRunner.Run(Group, inLine).Blocks, b => b.RuleId == "collinear");
    }

    [Fact]
    public void Property_Shear_Scales_Linearly_With_Load()
    {
        Gen.Select(Gen.Double[1, 500], Gen.Double[-300, 300]).Sample((f, e) =>
        {
            var bolts = Bolts((0, 0), (80, 0), (80, 120), (0, 120));
            var a = CalcRunner.Run(Group, GroupInputs(bolts).With(CalcInputValue.Quantity("F_y", -f, "kN")).With(CalcInputValue.Quantity("e_x", e, "mm")));
            var b = CalcRunner.Run(Group, GroupInputs(bolts).With(CalcInputValue.Quantity("F_y", -2 * f, "kN")).With(CalcInputValue.Quantity("e_x", e, "mm")));
            return Math.Abs(b.OutputSi("v_max") - 2 * a.OutputSi("v_max")) < 1e-6 * b.OutputSi("v_max");
        }, iter: 40);
    }

    private static CalcInputValue BoltTable(params (double X, double Y)[] points) =>
        CalcInputValue.Table("bolts", points.Select(p => CalcRow.Of(("x", new CalcCell(p.X, "mm")), ("y", new CalcCell(p.Y, "mm")), ("A", new CalcCell(84.3, "mm^2")))).ToList());

    private static CalcInputSet BendingOnly(params (double X, double Y)[] points) =>
        CalcTemplateHasher.DefaultInputs(Group.Template).With(BoltTable(points)).With(CalcInputValue.Quantity("F_y", 0, "kN")).With(CalcInputValue.Quantity("e_x", 0, "mm")).With(CalcInputValue.Quantity("e_y", 0, "mm"))
            .With(CalcInputValue.Quantity("V_R", 1000, "kN")).With(CalcInputValue.Quantity("T_R", 1000, "kN"));

    [Fact]
    public void An_Unsymmetric_Pattern_Uses_The_Product_Moment_In_Bending()
    {
        // Green G-A-01 (shared kernel): bolts at (0,0), (120,0), (60,80), (0,160), A = 84.3 mm², M_x = 12 kN·m. Moment equilibrium of
        // t = A(a·x' + b·y') solved in python3 gives 62.5 kN at the top bolt; the symmetric-axes form gave 68.18 kN.
        var r = CalcRunner.Run(Group, BendingOnly((0, 0), (120, 0), (60, 80), (0, 160)).With(CalcInputValue.Quantity("M_x", 12, "kN*m")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(62.5, r.OutputSi("t_max") / 1e3, 6);
    }

    [Fact]
    public void Bolts_On_One_Line_Cannot_Resist_A_Moment_About_That_Line()
    {
        // Bolts on the y axis: M_y (about y) has nothing to bear on. It used to be dropped silently.
        var about = CalcRunner.Run(Group, BendingOnly((0, 0), (0, 100), (0, 200)).With(CalcInputValue.Quantity("M_y", 5, "kN*m")));
        Assert.True(about.IsBlocked);
        Assert.Equal("collinear", about.Blocks[0].RuleId);

        // M_x bends the line: t = M y'/(A Σ y'²) × A, y' = 100 mm, Σ y'² = 20000: 10e6 × 100/20000 = 50 kN.
        var across = CalcRunner.Run(Group, BendingOnly((0, 0), (0, 100), (0, 200)).With(CalcInputValue.Quantity("M_x", 10, "kN*m")));
        Assert.False(across.IsBlocked, string.Join("; ", across.Blocks.Select(b => b.Message)));
        Assert.Equal(50, across.OutputSi("t_max") / 1e3, 6);
    }
}
