using CsCheck;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class MechanismTests
{
    private static readonly MechanismCalculator Mechanism = new();

    /// <summary>The first known-answer case (crank-rocker 120/40/110/90 mm, open circuit) with the template defaults under it.</summary>
    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Mechanism.Template);

    private static CalcRunResult Run(params CalcInputValue[] changes) => CalcRunner.Run(Mechanism, changes.Aggregate(Defaults(), (set, v) => set.With(v)));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static string Text(CalcRunResult r, string symbol) => ((TextValue)r.FindOutput(symbol)!.Value).Value;

    private static CalcInputValue Mode(string mode) => CalcInputValue.Choice("mode", mode);

    private static CalcInputValue[] Slider(double r = 50, double l = 250, double e = 0, double start = 90) =>
        [Mode(MechanismCalculator.SliderCrank), Q("L2", r, "mm"), Q("L3", l, "mm"), Q("e_off", e, "mm"), Q("theta_start", start, "deg")];

    private static IReadOnlyList<double> Vector(CalcRunResult r, string symbol) => ((VectorValue)r.FindOutput(symbol)!.Value).Si;

    // ------------------------------------------------------------------
    // Four-bar
    // ------------------------------------------------------------------

    [Fact]
    public void The_Loop_Closes_And_Velocity_And_Acceleration_Match_Finite_Differences_On_Both_Circuits()
    {
        // Property: random Grashof crank-rockers; position residual ~1e-12 m, ω and α against central differences of θ₄ and θ₃.
        Gen.Select(Gen.Double[0.02, 0.05], Gen.Double[0.09, 0.15], Gen.Double[0.08, 0.14], Gen.Double[0.07, 0.12], Gen.Double[0, 2 * Math.PI], Gen.Bool).Sample((a, d, b, c, t, crossed) =>
        {
            var link = new FourBarLinkage(d, a, b, c, crossed);
            const double w = 7.5;
            const double h = 1e-4;

            if (link.At(t, w) is not { Singular: false } s || link.At(t + h, w) is not { } sp || link.At(t - h, w) is not { } sm || Math.Abs(Math.Sin(s.Theta3 - s.Theta4)) < 1e-2)
                return true;

            double Wrap(double x) => Math.IEEERemainder(x, 2 * Math.PI);
            var w4 = Wrap(sp.Theta4 - sm.Theta4) / (2 * h) * w;
            var w3 = Wrap(sp.Theta3 - sm.Theta3) / (2 * h) * w;
            var a4 = Wrap(sp.Theta4 - 2 * s.Theta4 + sm.Theta4) / (h * h) * w * w;
            var scale = Math.Max(1, Math.Abs(s.Alpha4));
            return s.Residual < 1e-12 && Math.Abs(w4 - s.Omega4) <= 1e-6 * Math.Max(1, Math.Abs(w4)) && Math.Abs(w3 - s.Omega3) <= 1e-6 * Math.Max(1, Math.Abs(w3)) && Math.Abs(a4 - s.Alpha4) <= 1e-4 * scale;
        }, iter: 300);
    }

    [Fact]
    public void The_Two_Circuits_Are_Mirror_Images_About_The_Line_From_The_Rocker_Pivot_To_The_Crank_Pin()
    {
        var open = new FourBarLinkage(0.12, 0.04, 0.11, 0.09, false).At(1.1, 1)!.Value;
        var crossed = new FourBarLinkage(0.12, 0.04, 0.11, 0.09, true).At(1.1, 1)!.Value;
        var phi = Math.Atan2(0.04 * Math.Sin(1.1), 0.04 * Math.Cos(1.1) - 0.12);
        Assert.Equal(1, Math.Cos(open.Theta4 + crossed.Theta4 - 2 * phi), 12);
        Assert.Equal(open.Mu, crossed.Mu, 12);
        Assert.NotEqual(open.Theta4, crossed.Theta4);
        var sheet = Run(Q("theta_start", 63.025, "deg"));
        var openAt = new FourBarLinkage(0.12, 0.04, 0.11, 0.09, false).At(63.025 * Math.PI / 180, 1)!.Value;
        var crossedAt = new FourBarLinkage(0.12, 0.04, 0.11, 0.09, true).At(63.025 * Math.PI / 180, 1)!.Value;
        Assert.Equal(openAt.Theta4, sheet.OutputSi("theta4_0"), 12);
        Assert.Equal(crossedAt.Theta4, sheet.OutputSi("theta4_alt"), 12);
    }

    [Theory]
    [InlineData(100, 40, 100, 40, "change-point", true)]
    [InlineData(120, 40, 110, 90, "crank-rocker", true)]
    [InlineData(40, 90, 110, 100, "double-crank", true)]
    [InlineData(100, 90, 40, 110, "double-rocker", false)]
    [InlineData(110, 90, 40, 100, "double-rocker", false)]
    [InlineData(60, 40, 50, 35, "Non-Grashof", false)]
    [InlineData(100, 110, 90, 40, "rocker-crank", false)]
    public void The_Grashof_Class_Follows_The_Shortest_Link_And_The_Sum_Of_Lengths(double d, double a, double b, double c, string expected, bool fullRotation)
    {
        var text = FourBarLinkage.Classify(d, a, b, c, out var sl, out var pq, out var full);
        Assert.Contains(expected, text, StringComparison.Ordinal);
        Assert.Equal(fullRotation, full);

        var sorted = new[] { d, a, b, c }.Order().ToArray();
        Assert.Equal(sorted[0] + sorted[3], sl, 12);
        Assert.Equal(sorted[1] + sorted[2], pq, 12);
    }

    [Fact]
    public void A_Non_Grashof_Linkage_Is_Swept_Only_Where_It_Assembles_And_Says_So()
    {
        // 60/40/50/35 is non-Grashof: the crank cannot turn through 360°. The rows that cannot be assembled are left out and counted.
        var r = Run(Q("L1", 60, "mm"), Q("L2", 40, "mm"), Q("L3", 50, "mm"), Q("L4", 35, "mm"));
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "reach" && w.Message.StartsWith("L2:", StringComparison.Ordinal));
        Assert.Contains(r.Warnings, w => w.RuleId == "grashof");
        Assert.True(Vector(r, "theta2_sw").Count < 73 && Vector(r, "theta2_sw").Count > 3);
        Assert.Contains("Non-Grashof", Text(r, "grashof"), StringComparison.Ordinal);
        Assert.True(r.OutputSi("res_max") < 1e-12);
    }

    [Fact]
    public void Links_That_Never_Assemble_Block_With_A_Message_That_Names_The_Input()
    {
        var r = Run(Q("L1", 100, "mm"), Q("L2", 10, "mm"), Q("L3", 20, "mm"), Q("L4", 20, "mm"));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.RuleId == "geometry" && b.Message.StartsWith("L1:", StringComparison.Ordinal));
        Assert.DoesNotContain(r.Outputs, o => o.Symbol == "input_torque");
    }

    [Fact]
    public void A_Transmission_Angle_Below_The_Requirement_Fails_And_The_Requirement_Is_Editable()
    {
        var fail = Run(Q("mu_req", 50, "deg"));
        Assert.Contains(fail.Checks, c => c.Name.StartsWith("Transmission angle", StringComparison.Ordinal) && !c.Passed);
        Assert.False(fail.Passed);
        Assert.Equal(50 / 45.815614846695546, fail.OutputSi("u_mu"), 6);

        var pass = Run(Q("mu_req", 40, "deg"));
        Assert.Contains(pass.Checks, c => c.Name.StartsWith("Transmission angle", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void Peaks_Do_Not_Depend_On_The_Table_Step_Count()
    {
        var coarse = Run(CalcInputValue.Of("steps", 8));
        var fine = Run(CalcInputValue.Of("steps", 720));

        foreach (var symbol in new[] { "input_torque", "output_motion", "peak_accel", "follower_force", "mu_min", "omega_out_pk", "alpha_out_pk", "swing" })
            Assert.Equal(fine.OutputSi(symbol), coarse.OutputSi(symbol), 9);

        Assert.Equal(9, Vector(coarse, "theta2_sw").Count);
        Assert.Equal(721, Vector(fine, "theta2_sw").Count);
    }

    [Fact]
    public void The_Table_Is_Capped_And_The_Cap_Runs_Within_The_Time_Budget()
    {
        var over = Run(CalcInputValue.Of("steps", 3601));
        Assert.True(over.IsBlocked);
        Assert.Contains(over.Blocks, b => b.Message.StartsWith("steps", StringComparison.Ordinal));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var capped = Run(CalcInputValue.Of("steps", 3600), Q("theta_range", 3600, "deg"));
        sw.Stop();
        Assert.False(capped.IsBlocked);
        Assert.Equal(3601, Vector(capped, "theta2_sw").Count);
        Assert.True(sw.Elapsed.TotalSeconds < 5, $"{sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Input_Torque_Is_The_Output_Power_Over_The_Input_Speed_At_Every_Row()
    {
        // Virtual work: T_in·ω₂ = (T_L·sign(ω₄) + J·α₄)·ω₄, row by row from the printed tables.
        var r = Run();
        var w2 = 120 * 2 * Math.PI / 60;
        var w4 = Vector(r, "omega4_sw");
        var a4 = Vector(r, "alpha4_sw");
        var t = Vector(r, "T_in_sw");

        for (var i = 0; i < t.Count; i++)
            Assert.Equal((2.5 * Math.Sign(w4[i]) + 0.004 * a4[i]) * w4[i] / w2, t[i], 9);

        // The pin force and the pin speed are the same peaks seen from the rocker.
        Assert.Equal(r.OutputSi("omega_out_pk") * 0.09, r.OutputSi("output_motion"), 12);
    }

    [Fact]
    public void An_Assisting_Load_Reverses_The_Sign_Of_The_Input_Torque_Where_It_Does_Work()
    {
        var resist = Run(CalcInputValue.Choice("load_dir", MechanismCalculator.Constant), CalcInputValue.Of("steps", 36), Q("J_out", 0, "kg*m^2"));
        var assist = Run(CalcInputValue.Choice("load_dir", MechanismCalculator.Constant), CalcInputValue.Of("steps", 36), Q("J_out", 0, "kg*m^2"), Q("T_load", -2.5, "N*m"));
        var a = Vector(resist, "T_in_sw");
        var b = Vector(assist, "T_in_sw");

        for (var i = 0; i < a.Count; i++)
            Assert.Equal(-a[i], b[i], 12);
    }

    [Fact]
    public void The_Probe_Outputs_Are_The_First_Row_Of_The_Table()
    {
        var r = Run();
        Assert.Equal(Vector(r, "theta4_sw")[0], r.OutputSi("theta4_0"), 12);
        Assert.Equal(Vector(r, "omega4_sw")[0], r.OutputSi("omega4_0"), 12);
        Assert.Equal(Vector(r, "T_in_sw")[0], r.OutputSi("T_in_0"), 12);
        Assert.Equal(Vector(r, "mu_sw")[0], r.OutputSi("mu_0"), 12);
    }

    [Fact]
    public void Toggle_Positions_Are_Where_Coupler_And_Rocker_Are_Collinear()
    {
        // 120/40/110/90: ℓ = b + c = 200 > d + a (no toggle); ℓ = |b − c| = 20 < d − a = 80 (no toggle): a crank-rocker with no toggle.
        Assert.Contains("none", Text(Run(), "toggle_angles"), StringComparison.Ordinal);

        // d = 100, a = 40, b = 80, c = 30: ℓ = b + c = 110 → cos θ₂ = (100² + 40² − 110²)/(2·100·40) = −0.6125; |b − c| = 50 → cos θ₂ = 0.9...
        var link = new FourBarLinkage(0.100, 0.040, 0.080, 0.030, false);
        var toggles = link.ToggleCrankAngles();
        Assert.NotEmpty(toggles);

        foreach (var t in toggles)
        {
            var s = link.At(t, 1)!.Value;
            Assert.True(s.Mu < 1e-6, $"μ = {s.Mu} at {t}");
        }
    }

    [Fact]
    public void A_Cleared_Link_Blocks_And_Says_What_To_Enter()
    {
        var r = CalcRunner.Run(Mechanism, Defaults().With(new CalcInputValue("L4")));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.RuleId == "missing" && b.Message.StartsWith("L4:", StringComparison.Ordinal) && b.Message.Contains("enter", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_Port_Is_An_Output_Of_The_Declared_Dimension_In_Every_Mode()
    {
        foreach (var run in new[] { Run(), Run(Slider()) })
        {
            Assert.False(run.IsBlocked, string.Join("; ", run.Blocks.Select(b => b.Message)));

            foreach (var port in Mechanism.Template.OutputPorts!)
            {
                var output = run.FindOutput(port.Symbol);
                Assert.NotNull(output);
                Assert.Equal(UnitCatalog.Parse(port.Unit).Dimension, output.Value.Dimension);
            }
        }

        Assert.Equal(["input_torque", "output_motion", "peak_accel", "follower_force"], Mechanism.Template.OutputPorts!.Select(p => p.Symbol));
    }

    // ------------------------------------------------------------------
    // Slider-crank
    // ------------------------------------------------------------------

    [Fact]
    public void The_Slider_Position_Is_The_Published_Formula_And_Its_Derivatives_Match_Finite_Differences()
    {
        Gen.Select(Gen.Double[0.03, 0.08], Gen.Double[0.15, 0.35], Gen.Double[-0.03, 0.03], Gen.Double[0, 2 * Math.PI], Gen.Bool).Sample((r, l, e, t, crossed) =>
        {
            var link = new SliderCrankLinkage(r, l, e, crossed);
            const double h = 1e-4;

            if (link.At(t) is not { } s || link.At(t + h) is not { } sp || link.At(t - h) is not { } sm || Math.Abs(l * l - Math.Pow(r * Math.Sin(t) - e, 2)) < 1e-4 * l * l)
                return true;

            var x = r * Math.Cos(t) + (crossed ? -1 : 1) * Math.Sqrt(l * l - Math.Pow(r * Math.Sin(t) - e, 2));
            var v = (sp.X - sm.X) / (2 * h);
            var a = (sp.X - 2 * s.X + sm.X) / (h * h);
            var w3 = Math.IEEERemainder(sp.Theta3 - sm.Theta3, 2 * Math.PI) / (2 * h);
            return Math.Abs(s.X - x) < 1e-15 && s.Residual < 1e-12 && Math.Abs(v - s.DxDtheta) < 1e-7 && Math.Abs(a - s.D2xDtheta2) < 1e-5 * Math.Max(1, Math.Abs(a)) && Math.Abs(w3 - s.Dtheta3) < 1e-6 * Math.Max(1, Math.Abs(w3));
        }, iter: 300);
    }

    [Fact]
    public void The_Inline_Stroke_Is_Twice_The_Crank_And_The_Offset_Stroke_Is_The_Closed_Form()
    {
        var inline = Run(Slider());
        Assert.Equal(0.1, inline.OutputSi("stroke"), 12);
        Assert.Equal(0.1, inline.OutputSi("stroke_closed"), 12);

        Gen.Select(Gen.Double[20, 80], Gen.Double[0, 1], Gen.Double[-0.4, 0.4]).Sample((r, lr, eFraction) =>
        {
            var l = r * (2.5 + 3 * lr);
            var e = eFraction * (l - r);
            var run = Run(Slider(r, l, e, 0));
            var closed = Math.Sqrt((l + r) * (l + r) - e * e) - Math.Sqrt((l - r) * (l - r) - e * e);
            return !run.IsBlocked && Math.Abs(run.OutputSi("stroke") * 1e3 - closed) < 1e-9 && Math.Abs(run.OutputSi("stroke_closed") * 1e3 - closed) < 1e-9;
        }, iter: 60);
    }

    [Fact]
    public void Dead_Centres_Are_Where_The_Slider_Velocity_Is_Zero()
    {
        var link = new SliderCrankLinkage(0.05, 0.25, 0.02, false);
        var centres = link.DeadCentres();
        Assert.Equal(2, centres.Count);

        foreach (var (_, theta) in centres)
            Assert.Equal(0, link.At(theta)!.Value.DxDtheta, 9);

        var crossed = new SliderCrankLinkage(0.05, 0.25, 0.02, true);

        foreach (var (_, theta) in crossed.DeadCentres())
            Assert.Equal(0, crossed.At(theta)!.Value.DxDtheta, 9);

        Assert.Contains("outer", Text(Run(Slider(50, 250, 20, 0)), "dead_centres"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_Crank_Longer_Than_The_Rod_Cannot_Turn_Fully_And_Is_Swept_Where_It_Assembles()
    {
        var r = Run(Slider(100, 80, 0, 0));
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "reach" && w.Message.StartsWith("L3:", StringComparison.Ordinal));
        Assert.Contains("Rocker-slider", Text(r, "slider_class"), StringComparison.Ordinal);
        Assert.True(Vector(r, "theta_sw").Count < 73);

        var never = Run(Slider(100, 80, 300, 0));
        Assert.True(never.IsBlocked);
        Assert.Contains(never.Blocks, b => b.RuleId == "geometry" && b.Message.StartsWith("L3:", StringComparison.Ordinal));
    }

    [Fact]
    public void Slider_Input_Torque_Is_Force_Times_Velocity_Over_The_Input_Speed()
    {
        var r = Run([.. Slider(50, 250, 15, 0), CalcInputValue.Choice("load_dir", MechanismCalculator.Constant), Q("m_slider", 2, "kg"), Q("F_load", 80, "N"), Q("n_in", 300, "rpm"), CalcInputValue.Of("steps", 90)]);
        var w = 300 * 2 * Math.PI / 60;
        var v = Vector(r, "v_sw");
        var a = Vector(r, "a_sw");
        var t = Vector(r, "T_in_sw");

        for (var i = 0; i < t.Count; i++)
            Assert.Equal((2 * a[i] + 80) * v[i] / w, t[i], 9);
    }

    [Fact]
    public void A_Rod_Too_Steep_For_The_Required_Transmission_Angle_Fails_The_Check()
    {
        // r/l = 0.9 → rod angle up to 64°, μ_min = 25.8° < 40°.
        var r = Run(Slider(90, 100, 0, 0));
        Assert.Equal(Math.Acos(0.9) * 180 / Math.PI, r.OutputSi("mu_min") * 180 / Math.PI, 7);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Transmission angle", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void The_Loop_Residual_Is_Asserted_And_Reported_Below_1e_9_mm_For_Both_Mechanisms()
    {
        foreach (var run in new[] { Run(), Run(Slider(50, 250, 20, 0)) })
        {
            Assert.True(run.OutputSi("res_max") * 1e3 < MechanismCalculator.ClosureLimitMm);
            Assert.Contains(run.Checks, c => c.Name.StartsWith("Loop-closure residual", StringComparison.Ordinal) && c.Passed);
        }
    }

    [Fact]
    public void The_Peak_Search_Finds_A_Peak_Between_The_Samples_And_Skips_Undefined_Angles()
    {
        var sine = MechanismPeaks.Find(Math.Sin, 0, Math.PI, 9)!.Value;
        Assert.Equal(1, sine.Value, 12);
        Assert.Equal(Math.PI / 2, sine.At, 7);
        var min = MechanismPeaks.Find(x => x < 1 ? double.NaN : Math.Cos(x), 0, 3.0, 40, max: false)!.Value;
        Assert.Equal(Math.Cos(3.0), min.Value, 12);
        Assert.Null(MechanismPeaks.Find(_ => double.NaN, 0, 1, 10));
    }
}
