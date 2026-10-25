using CsCheck;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

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
            const double h = 1e-5;
            const double h2 = 2e-4;

            if (link.At(t, w) is not { Singular: false } s || link.At(t + h, w) is not { } sp || link.At(t - h, w) is not { } sm || link.At(t + h2, w) is not { } sp2 || link.At(t - h2, w) is not { } sm2 || Math.Abs(Math.Sin(s.Theta3 - s.Theta4)) < 0.3)
                return true;

            double Wrap(double x) => Math.IEEERemainder(x, 2 * Math.PI);
            var w4 = Wrap(sp.Theta4 - sm.Theta4) / (2 * h) * w;
            var w3 = Wrap(sp.Theta3 - sm.Theta3) / (2 * h) * w;
            var a4 = Wrap(sp2.Theta4 - 2 * s.Theta4 + sm2.Theta4) / (h2 * h2) * w * w;
            var a3 = Wrap(sp2.Theta3 - 2 * s.Theta3 + sm2.Theta3) / (h2 * h2) * w * w;
            return s.Residual < 1e-12
                && Math.Abs(w4 - s.Omega4) <= 1e-6 * Math.Max(1, Math.Abs(w4)) && Math.Abs(w3 - s.Omega3) <= 1e-6 * Math.Max(1, Math.Abs(w3))
                && Math.Abs(a4 - s.Alpha4) <= 1e-4 * Math.Max(1, Math.Abs(s.Alpha4)) && Math.Abs(a3 - s.Alpha3) <= 1e-4 * Math.Max(1, Math.Abs(s.Alpha3));
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
        Assert.Contains(r.Warnings, w => w.RuleId == "limit" && w.Message.StartsWith("theta_range:", StringComparison.Ordinal));
        Assert.Contains(r.Warnings, w => w.RuleId == "grashof");
        Assert.True(Vector(r, "theta2_sw").Count < 73 && Vector(r, "theta2_sw").Count > 3);
        Assert.Contains("Non-Grashof", Text(r, "grashof"), StringComparison.Ordinal);
        Assert.True(r.OutputSi("res_max") < 1e-12);

        // The sweep is cut at the toggle (cos θ₂ = −0.42 → 114.9°) and the unbounded peaks and the ports are withheld, never printed.
        var last = Vector(r, "theta2_sw")[^1] * 180 / Math.PI;
        var limitDeg = Math.Acos(-0.42) * 180 / Math.PI;
        Assert.InRange(last, limitDeg - 3, limitDeg); // the row exactly at the toggle has no defined velocity and is left out

        foreach (var symbol in new[] { "input_torque", "output_motion", "peak_accel", "follower_force", "F_pin_pk", "T_rms_in", "P_mean", "omega_out_pk", "alpha_out_pk" })
            Assert.Null(r.FindOutput(symbol));

        Assert.Equal(0, r.OutputSi("mu_min"), 12);
        Assert.Contains("114", Text(r, "toggle_angles"), StringComparison.Ordinal);
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
        string[] shared = ["input_torque", "output_motion", "peak_accel", "follower_force", "F_pin_pk", "T_rms_in", "n_in", "f_in", "P_mean"];

        foreach (var run in new[] { Run(), Run(Slider()), Cam() })
        {
            Assert.False(run.IsBlocked, string.Join("; ", run.Blocks.Select(b => b.Message)));

            foreach (var port in Mechanism.Template.OutputPorts!)
            {
                var output = run.FindOutput(port.Symbol);

                if (output is null)
                {
                    // lift and F_spring_max belong to the cam only.
                    Assert.DoesNotContain(port.Symbol, shared);
                    continue;
                }

                Assert.Equal(UnitCatalog.Parse(port.Unit).Dimension, output.Value.Dimension);
            }

            Assert.All(shared, symbol => Assert.NotNull(run.FindOutput(symbol)));
        }

        Assert.Equal([.. shared, "lift", "F_spring_max"], Mechanism.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.DoesNotContain("M5", Mechanism.Template.OutputPorts!.Single(p => p.Symbol == "input_torque").Description, StringComparison.Ordinal);
        Assert.Contains("kinematic proxy", Mechanism.Template.OutputPorts!.Single(p => p.Symbol == "output_motion").Description, StringComparison.Ordinal);
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
        Assert.Contains(r.Warnings, w => w.RuleId == "limit" && w.Message.StartsWith("theta_range:", StringComparison.Ordinal));
        Assert.Contains("Rocker-slider", Text(r, "slider_class"), StringComparison.Ordinal);
        Assert.True(Vector(r, "theta_sw").Count < 73);

        // Cut at the limit sin θ = l/r = 0.8 (53.13°): the unbounded peaks and every port are withheld.
        var lastRow = Vector(r, "theta_sw")[^1] * 180 / Math.PI;
        var limitAngle = Math.Asin(0.8) * 180 / Math.PI;
        Assert.InRange(lastRow, limitAngle - 2, limitAngle + 1e-9);

        foreach (var symbol in new[] { "input_torque", "output_motion", "peak_accel", "follower_force", "F_pin_pk", "F_guide_pk", "T_rms_in", "P_mean" })
            Assert.Null(r.FindOutput(symbol));

        Assert.Equal(0, r.OutputSi("mu_min"), 12);
        Assert.Contains("53.13", Text(r, "limit_angles"), StringComparison.Ordinal);

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

    [Fact]
    public void Extreme_Numbers_In_Any_Mode_Never_Throw_And_Never_Print_Nan_Or_Infinity()
    {
        // The common checks only perturb the default (four-bar) sheet: run every known-answer case, so the slider and the cam are covered too.
        var hostile = new[] { 0.0, -1.0, 1e-300, 1e300, -1e300, double.NaN, double.PositiveInfinity, double.NegativeInfinity };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var testCase in Mechanism.Template.TestCases)
        {
            var baseline = new CalcInputSet(testCase.Inputs);

            foreach (var spec in Mechanism.Template.Inputs.Where(i => i.Kind is CalcInputKind.Quantity or CalcInputKind.Number))
            {
                var values = hostile.Concat(new[] { spec.HardMin, spec.HardMax, spec.Min, spec.Max }.Where(v => v is not null).Select(v => v!.Value));

                foreach (var value in values)
                {
                    var inputs = baseline.With(new CalcInputValue(spec.Symbol, Number: value, Unit: spec.Kind == CalcInputKind.Quantity ? spec.DefaultUnit : null));
                    var result = CalcRunner.Run(Mechanism, inputs);
                    var sheet = CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(Mechanism.Template, inputs, result, GoldenFiles.FixedHeader("x", "x"), GoldenFiles.FixedFooter(Mechanism.Template, 0, 0)));
                    Assert.DoesNotContain("NaN", sheet, StringComparison.Ordinal);
                    Assert.DoesNotContain("Infinity", sheet, StringComparison.Ordinal);
                    Assert.DoesNotContain("Exception", sheet, StringComparison.Ordinal);
                }
            }
        }

        Assert.True(sw.Elapsed.TotalSeconds < 120, $"{sw.Elapsed.TotalSeconds:0} s");
    }

    [Fact]
    public void A_Plateau_Does_Not_Crowd_A_True_Peak_Out_Of_The_Candidates()
    {
        // Two plateaus of 5 with a bump between them whose best sample (about 4.88) is below the plateau: the peak 5.2 must still be found.
        var peak = MechanismPeaks.Find(x => x < 0.3 || x > 0.7 ? 5 : 4 + 1.2 * Math.Exp(-Math.Pow((x - 0.5025) / 0.01, 2)), 0, 1, 200)!.Value;
        Assert.Equal(5.2, peak.Value, 3);
        Assert.InRange(peak.At, 0.5, 0.505);
    }

    [Fact]
    public void New_Outputs_Have_Closed_Forms_On_The_Parallelogram_RMS_Torque_Mean_Power_And_Forces()
    {
        // 100/40/100/40 swept 5° to 175°: ω₄ = ω₂, α = 0, so T_in = T_L = 3 N·m throughout; RMS 3 N·m, mean power ω₂·T_L; the pin force T_L/c.
        var r = Run(CalcInputValue.Choice("load_dir", MechanismCalculator.Constant), Q("L1", 100, "mm"), Q("L2", 40, "mm"), Q("L3", 100, "mm"), Q("L4", 40, "mm"), Q("theta_start", 5, "deg"), Q("theta_range", 170, "deg"), Q("T_load", 3, "N*m"), Q("J_out", 0.01, "kg*m^2"), Q("mu_req", 4, "deg"));
        var w = 120 * 2 * Math.PI / 60;
        Assert.Equal(3, r.OutputSi("T_rms_in"), 9);
        Assert.Equal(3 * w, r.OutputSi("P_mean"), 8);
        Assert.Equal(120, ((ScalarValue)r.FindOutput("n_in")!.Value).Displayed, 9);
        Assert.Equal(75, r.OutputSi("follower_force"), 8);

        // sin μ_min = sin 5°: the coupler carries the tangential force over sin μ.
        Assert.Equal(75 / Math.Sin(5 * Math.PI / 180), r.OutputSi("F_pin_pk"), 6);
    }

    [Fact]
    public void A_Change_Point_Linkage_Is_Followed_By_Continuity_And_Warns_Without_Printing_Peaks()
    {
        // The parallelogram swept a full turn from 5°: θ₄ = θ₂ on every row (the old fixed-sign branch jumped to the anti-parallelogram past 180°).
        var r = Run(Q("L1", 100, "mm"), Q("L2", 40, "mm"), Q("L3", 100, "mm"), Q("L4", 40, "mm"), Q("theta_start", 5, "deg"), Q("theta_range", 360, "deg"), CalcInputValue.Of("steps", 72));
        Assert.False(r.IsBlocked);
        var t2 = Vector(r, "theta2_sw");
        var t4 = Vector(r, "theta4_sw");
        Assert.Contains(r.Warnings, w => w.RuleId == "limit" && w.Message.Contains("change point", StringComparison.Ordinal));
        Assert.True(t2.Count > 60);

        for (var i = 0; i < t2.Count; i++)
            Assert.Equal(0, Math.IEEERemainder(t4[i] - t2[i], 2 * Math.PI), 8);

        Assert.Equal(360, r.OutputSi("swing") * 180 / Math.PI, 5);
        Assert.Null(r.FindOutput("input_torque"));
        Assert.Null(r.FindOutput("output_motion"));
    }

    [Theory]
    [InlineData(185, 170)]
    [InlineData(5, 170)]
    [InlineData(200, 150)]
    [InlineData(-170, 160)]
    public void The_Open_Circuit_Of_A_Parallelogram_Is_The_Same_Motion_Whatever_The_Sweep_Starts_At(double start, double range)
    {
        // Past 180° the old reference (the first row of the sweep) took the anti-parallelogram branch. The reference is now fixed.
        var r = Run(CalcInputValue.Choice("load_dir", MechanismCalculator.Constant), Q("L1", 100, "mm"), Q("L2", 40, "mm"), Q("L3", 100, "mm"), Q("L4", 40, "mm"), Q("theta_start", start, "deg"), Q("theta_range", range, "deg"), Q("T_load", 3, "N*m"), Q("J_out", 0.01, "kg*m^2"), Q("mu_req", 4, "deg"));
        Assert.False(r.IsBlocked);
        var t2 = Vector(r, "theta2_sw");
        var t4 = Vector(r, "theta4_sw");

        for (var i = 0; i < t2.Count; i++)
            Assert.Equal(0, Math.IEEERemainder(t4[i] - t2[i], 2 * Math.PI), 8);

        Assert.Equal(0.04 * 120 * 2 * Math.PI / 60, r.OutputSi("output_motion"), 8);
        Assert.Equal(3, r.OutputSi("input_torque"), 8);
        Assert.Equal(75, r.OutputSi("follower_force"), 6);
    }

    [Theory]
    [InlineData(100, 90, 60, 50)]
    [InlineData(100, 90, 40, 50)]
    [InlineData(40, 40, 100, 100)]
    [InlineData(100, 40, 100, 40)]
    public void A_Change_Point_Linkage_Gives_The_Same_Motion_At_The_Same_Crank_Angle_On_Any_Turn(double d, double a, double b, double c)
    {
        // θ₂ = 20° … 40° reached as 20°, 380° and −340°: the label means the same motion on every turn, for linkages with an odd number
        // of change points per turn too (the kite, period 720°).
        CalcRunResult At(double start) => Run(Q("L1", d, "mm"), Q("L2", a, "mm"), Q("L3", b, "mm"), Q("L4", c, "mm"), Q("theta_start", start, "deg"), Q("theta_range", 20, "deg"), CalcInputValue.Of("steps", 20));
        var reference = At(20);
        Assert.False(reference.IsBlocked, string.Join("; ", reference.Blocks.Select(x => x.Message)));
        Assert.NotNull(reference.FindOutput("input_torque"));

        foreach (var start in new[] { 380.0, -340.0 })
        {
            var other = At(start);
            Assert.False(other.IsBlocked);
            Assert.Equal(Vector(reference, "theta4_sw").Select(x => Math.Round(x, 9)), Vector(other, "theta4_sw").Select(x => Math.Round(x, 9)));

            foreach (var symbol in new[] { "theta4_0", "omega4_0", "input_torque", "output_motion", "peak_accel", "follower_force", "F_pin_pk", "mu_min", "swing" })
                Assert.Equal(reference.OutputSi(symbol), other.OutputSi(symbol), 9);
        }
    }

    [Fact]
    public void The_Kite_Label_Is_Fixed_Per_Crank_Angle_And_A_Sweep_Of_More_Than_One_Turn_Warns_About_The_720_Degree_Period()
    {
        // 40/40/100/100 has one change point (θ₂ = 0) per turn. 185° … 355° and −175° … −5° are the same angles.
        CalcRunResult Kite(double start, double range) => Run(Q("L1", 40, "mm"), Q("L2", 40, "mm"), Q("L3", 100, "mm"), Q("L4", 100, "mm"), Q("theta_start", start, "deg"), Q("theta_range", range, "deg"));
        var first = Kite(185, 170);
        var second = Kite(-175, 170);
        Assert.Equal(Vector(first, "theta4_sw").Select(x => Math.Round(x, 9)), Vector(second, "theta4_sw").Select(x => Math.Round(x, 9)));
        Assert.Equal(first.OutputSi("input_torque"), second.OutputSi("input_torque"), 9);
        Assert.Equal(first.OutputSi("output_motion"), second.OutputSi("output_motion"), 9);
        Assert.DoesNotContain(first.Warnings, w => w.Message.Contains("720", StringComparison.Ordinal));

        var twoTurns = Kite(10, 400);
        Assert.Contains(twoTurns.Warnings, w => w.RuleId == "limit" && w.Message.Contains("720", StringComparison.Ordinal));
        Assert.Null(twoTurns.FindOutput("input_torque"));
    }

    [Fact]
    public void The_Collinear_Positions_Are_Unique_Wrapped_And_Tolerant_Of_Rounding()
    {
        foreach (var (d, a, b, c) in new[] { (0.1, 0.04, 0.1, 0.04), (0.04, 0.04, 0.1, 0.1), (0.12, 0.04, 0.11, 0.09) })
        {
            var positions = new FourBarLinkage(d, a, b, c, false).CollinearPositions();
            Assert.All(positions, p => Assert.InRange(p.Theta2, 0, 2 * Math.PI - 1e-9));
            Assert.Equal(positions.Count, positions.Select(p => (Math.Round(p.Theta2, 6), Math.Round(Math.IEEERemainder(p.Theta4, 2 * Math.PI), 6))).Distinct().Count());
        }
    }

    [Fact]
    public void The_Parallelogram_Lists_Both_Collinear_Positions_Including_The_Folded_One_At_180_Degrees()
    {
        var r = Run(Q("L1", 100, "mm"), Q("L2", 40, "mm"), Q("L3", 100, "mm"), Q("L4", 40, "mm"));
        var text = Text(r, "limit_positions");
        Assert.True(text.Contains("θ₂ = 0°", StringComparison.Ordinal), text);
        Assert.True(text.Contains("θ₂ = 180°", StringComparison.Ordinal), text);
    }

    [Fact]
    public void The_Input_Speed_Is_Always_Passed_Through_Even_When_The_Other_Ports_Are_Withheld()
    {
        var withheld = Run(Q("L1", 60, "mm"), Q("L2", 40, "mm"), Q("L3", 50, "mm"), Q("L4", 35, "mm"));
        Assert.Null(withheld.FindOutput("input_torque"));
        Assert.Equal(120, ((ScalarValue)withheld.FindOutput("n_in")!.Value).Displayed, 9);

        var slider = Run([.. Slider(100, 80, 0, 0)]);
        Assert.Null(slider.FindOutput("input_torque"));
        Assert.NotNull(slider.FindOutput("n_in"));
    }

    [Fact]
    public void Peaks_Are_Reported_For_A_Non_Grashof_Sweep_Inside_Its_Assemblable_Stretch_And_Do_Not_Depend_On_The_Start()
    {
        // 60/40/50/35 assembles for |θ₂| < 114.9°; a sweep of ±90° stays inside and has finite, step-count-independent peaks.
        var geometry = new[] { Q("L1", 60, "mm"), Q("L2", 40, "mm"), Q("L3", 50, "mm"), Q("L4", 35, "mm") };
        var inside = Run([.. geometry, Q("theta_start", -90, "deg"), Q("theta_range", 180, "deg")]);
        Assert.DoesNotContain(inside.Warnings, w => w.RuleId == "limit");
        Assert.True(inside.OutputSi("input_torque") > 0 && inside.OutputSi("input_torque") < 1e4);
        var finer = Run([.. geometry, Q("theta_start", -90, "deg"), Q("theta_range", 180, "deg"), CalcInputValue.Of("steps", 600)]);
        Assert.Equal(inside.OutputSi("input_torque"), finer.OutputSi("input_torque"), 9);

        // Driving to the limit instead withholds them, whatever the start.
        foreach (var start in new[] { 0.1, 0.37, 5 })
        {
            var driven = Run([.. geometry, Q("theta_start", start, "deg")]);
            Assert.Null(driven.FindOutput("input_torque"));
            Assert.Null(driven.FindOutput("follower_force"));
        }

        var doubleRocker = Run(Q("L1", 100, "mm"), Q("L2", 90, "mm"), Q("L3", 40, "mm"), Q("L4", 110, "mm"));
        Assert.Null(doubleRocker.FindOutput("input_torque"));
        Assert.Contains(doubleRocker.Warnings, w => w.RuleId == "limit");
    }

    [Fact]
    public void A_Rocker_Slider_Swept_Inside_Its_Limits_Reports_Finite_Peaks()
    {
        var r = Run([.. Slider(100, 80, 0, -30), Q("theta_range", 60, "deg")]);
        Assert.False(r.IsBlocked);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "limit");
        Assert.True(r.OutputSi("input_torque") > 0 && r.OutputSi("input_torque") < 1e4);
        Assert.True(r.OutputSi("F_pin_pk") >= r.OutputSi("follower_force") - 1e-9);
        Assert.True(r.OutputSi("F_guide_pk") > 0);
    }

    [Fact]
    public void A_Blank_Slider_Offset_Defaults_To_Zero_And_Gives_The_Inline_Result()
    {
        var blank = Run([.. Slider(), new CalcInputValue("e_off")]);
        Assert.False(blank.IsBlocked, string.Join("; ", blank.Blocks.Select(b => b.Message)));
        var inline = Run(Slider());
        Assert.Equal(inline.OutputSi("x_0"), blank.OutputSi("x_0"), 12);
        Assert.Equal(inline.OutputSi("stroke"), blank.OutputSi("stroke"), 12);
    }

    [Fact]
    public void A_Zero_Transmission_Requirement_Passes_Even_At_A_Toggle_And_A_Positive_One_Fails()
    {
        var geometry = new[] { Q("L1", 60, "mm"), Q("L2", 40, "mm"), Q("L3", 50, "mm"), Q("L4", 35, "mm") };
        var zero = Run([.. geometry, Q("mu_req", 0, "deg")]);
        Assert.Equal(0, zero.OutputSi("u_mu"));
        Assert.Contains(zero.Checks, c => c.Name.StartsWith("Transmission angle", StringComparison.Ordinal) && c.Passed);
        var forty = Run([.. geometry, Q("mu_req", 40, "deg")]);
        Assert.Contains(forty.Checks, c => c.Name.StartsWith("Transmission angle", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void The_Velocity_Loop_Closes_On_Both_Mechanisms_And_The_Rocker_Extremes_Are_Where_Crank_And_Coupler_Are_Collinear()
    {
        foreach (var run in new[] { Run(), Run(Slider(50, 250, 20, 0)) })
        {
            Assert.True(run.OutputSi("res_vel_max") < 1e-8);
            Assert.Contains(run.Checks, c => c.Name.StartsWith("Velocity-loop residual", StringComparison.Ordinal) && c.Passed);
        }

        var link = new FourBarLinkage(0.120, 0.040, 0.110, 0.090, false);
        var positions = link.CollinearPositions();
        Assert.Equal(2, positions.Count);
        Assert.Equal(Math.Abs(positions[0].Theta4 - positions[1].Theta4), Run().OutputSi("swing"), 9);
        Assert.Contains("θ₂ =", Text(Run(), "limit_positions"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_Peak_Of_A_Periodic_Sweep_Just_Before_Its_End_Is_Not_Lost_To_The_Tie_With_The_First_Sample()
    {
        // cos(x − (2π − 0.0003)) peaks inside the last grid interval; the first and last samples tie on the way round.
        var peak = MechanismPeaks.Find(x => Math.Cos(x - (2 * Math.PI - 0.0003)), 0, 2 * Math.PI, 1440)!.Value;
        Assert.Equal(1, peak.Value, 13);
        Assert.Equal(2 * Math.PI - 0.0003, peak.At, 6);
        var trough = MechanismPeaks.Find(x => -Math.Cos(x - (2 * Math.PI - 0.0003)), 0, 2 * Math.PI, 1440, max: false)!.Value;
        Assert.Equal(-1, trough.Value, 13);

        // The same on a slider with a tiny offset: its stroke is the closed form although the dead centre lies just before 360°.
        var r = Run(Slider(67, 167.5, -0.105, 0));
        var closed = Math.Sqrt(Math.Pow(167.5 + 67, 2) - 0.105 * 0.105) - Math.Sqrt(Math.Pow(167.5 - 67, 2) - 0.105 * 0.105);
        Assert.Equal(closed, r.OutputSi("stroke") * 1e3, 9);
    }

    // ------------------------------------------------------------------
    // Cam
    // ------------------------------------------------------------------

    private static CalcRow[] RiseDwellReturnDwell(string law, double betaRise = 90, double betaDwell = 90, double lift = 20) =>
    [
        MechanismCalculator.Row(MechanismCalculator.Rise, law, betaRise, lift), MechanismCalculator.Row(MechanismCalculator.Dwell, null, betaDwell, 0),
        MechanismCalculator.Row(MechanismCalculator.Return, law, betaRise, lift), MechanismCalculator.Row(MechanismCalculator.Dwell, null, betaDwell, 0),
    ];

    private static CalcRunResult Cam(IReadOnlyList<CalcRow>? segments = null, params CalcInputValue[] changes) => Run(
    [
        Mode(MechanismCalculator.CamMode), CalcInputValue.Table("segs", segments ?? RiseDwellReturnDwell(MechanismCalculator.Cycloidal)),
        Q("n_in", 600, "rpm"), Q("Rb", 40, "mm"), Q("R_r", 8, "mm"), Q("e_off", 0, "mm"), Q("m_f", 0.3, "kg"), Q("k_sp", 8, "N/mm"), Q("F_pre", 30, "N"), Q("F_ext", 10, "N"),
        .. changes,
    ]);

    [Fact]
    public void Every_Motion_Law_Starts_And_Ends_At_Rest_Is_Symmetric_And_Has_Consistent_Derivatives()
    {
        foreach (var law in Enum.GetValues<CamLaw>())
        {
            var start = CamLaws.Evaluate(law, 0);
            var end = CamLaws.Evaluate(law, 1);
            Assert.Equal(0, start.F, 12);
            Assert.Equal(1, end.F, 12);
            Assert.Equal(0, start.F1, 12);
            Assert.Equal(0, end.F1, 12);
            Assert.Equal(0, start.F2, 12);
            Assert.Equal(0, end.F2, 12);

            Gen.Double[0.0005, 0.9995].Sample(x =>
            {
                var v = CamLaws.Evaluate(law, x);
                const double h = 1e-6;
                var up = CamLaws.Evaluate(law, x + h);
                var dn = CamLaws.Evaluate(law, x - h);
                var mirror = CamLaws.Evaluate(law, 1 - x);
                var nearModTrapBreak = law == CamLaw.ModifiedTrapezoid && new[] { 0.125, 0.375, 0.625, 0.875 }.Any(b => Math.Abs(x - b) < 2e-6);
                return Math.Abs(v.F + mirror.F - 1) < 1e-12
                    && Math.Abs(v.F1 - mirror.F1) < 1e-12
                    && (nearModTrapBreak || (Math.Abs((up.F - dn.F) / (2 * h) - v.F1) < 1e-7 && Math.Abs((up.F1 - dn.F1) / (2 * h) - v.F2) < 1e-6 * Math.Max(1, Math.Abs(v.F2))));
            }, iter: 200);
        }
    }

    [Theory]
    [InlineData(CamLaw.Cycloidal, 2.0, 6.283185307179586, 39.47841760435743)]
    [InlineData(CamLaw.ModifiedTrapezoid, 2.0, 4.888123762813258, 61.425974812367315)]
    [InlineData(CamLaw.Poly345, 1.875, 5.773502691896258, 60.0)]
    [InlineData(CamLaw.Poly4567, 2.1875, 7.513188404399289, 52.5)]
    public void The_Peak_Coefficients_Are_The_Published_Cv_Ca_Cj(CamLaw law, double cv, double ca, double cj)
    {
        Assert.Equal(cv, MechanismPeaks.Find(x => CamLaws.Evaluate(law, x).F1, 0, 1, 200)!.Value.Value, 11);
        Assert.Equal(ca, MechanismPeaks.Find(x => Math.Abs(CamLaws.Evaluate(law, x).F2), 0, 1, 200)!.Value.Value, 11);
        Assert.Equal(cj, MechanismPeaks.Find(x => Math.Abs(CamLaws.Evaluate(law, x).F3), 0, 1, 200)!.Value.Value, 10);
    }

    [Fact]
    public void The_Cam_Mode_Reports_The_Segment_Table_And_Closes_The_Cycle()
    {
        var r = Cam();
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal([0, 90, 180, 270], Vector(r, "seg_start").Select(x => Math.Round(x * 180 / Math.PI, 9)));
        Assert.Equal([20, 20, 0, 0], Vector(r, "seg_s_end").Select(x => Math.Round(x * 1e3, 9)));
        Assert.Contains("1: rise (Cycloidal)", Text(r, "seg_law"), StringComparison.Ordinal);
        Assert.Contains("2: dwell", Text(r, "seg_law"), StringComparison.Ordinal);
        var s = Vector(r, "s_sw");
        Assert.Equal(s[0], s[^1], 12);
        Assert.Equal(0.02, s.Max(), 9);
        Assert.Equal(73, s.Count);
    }

    [Fact]
    public void Segments_That_Are_Not_A_Cam_Block_And_Name_The_Table()
    {
        var short360 = Cam([MechanismCalculator.Row(MechanismCalculator.Rise, MechanismCalculator.Cycloidal, 90, 20), MechanismCalculator.Row(MechanismCalculator.Return, MechanismCalculator.Cycloidal, 90, 20)]);
        Assert.Contains(short360.Blocks, b => b.RuleId == "segments" && b.Message.StartsWith("segs:", StringComparison.Ordinal) && b.Message.Contains("180", StringComparison.Ordinal));

        var open = Cam([MechanismCalculator.Row(MechanismCalculator.Rise, MechanismCalculator.Cycloidal, 180, 20), MechanismCalculator.Row(MechanismCalculator.Return, MechanismCalculator.Cycloidal, 180, 15)]);
        Assert.Contains(open.Blocks, b => b.RuleId == "segments" && b.Message.Contains("do not cancel", StringComparison.Ordinal));

        var below = Cam([MechanismCalculator.Row(MechanismCalculator.Return, MechanismCalculator.Cycloidal, 180, 10), MechanismCalculator.Row(MechanismCalculator.Rise, MechanismCalculator.Cycloidal, 180, 10)]);
        Assert.Contains(below.Blocks, b => b.RuleId == "segments" && b.Message.Contains("below the base circle", StringComparison.Ordinal));

        var noLaw = Cam([MechanismCalculator.Row(MechanismCalculator.Rise, null, 180, 20), MechanismCalculator.Row(MechanismCalculator.Return, MechanismCalculator.Cycloidal, 180, 20)]);
        Assert.Contains(noLaw.Blocks, b => b.RuleId == "segments" && b.Message.StartsWith("segs[1]:", StringComparison.Ordinal));

        var empty = Cam([]);
        Assert.Contains(empty.Blocks, b => b.Message.StartsWith("segs:", StringComparison.Ordinal) && b.Message.Contains("enter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_Pressure_Angle_Is_Arctan_Of_The_Slope_Over_The_Prime_Radius_Plus_The_Lift()
    {
        var profile = new CamProfile([new CamSegment(CamSegmentKind.Rise, CamLaw.Cycloidal, Math.PI / 2, 0.02), new CamSegment(CamSegmentKind.Dwell, CamLaw.Cycloidal, 3 * Math.PI / 2, 0)]);

        Gen.Double[0, Math.PI / 2].Sample(theta =>
        {
            var inline = new CamAnalysis(profile, true, 0.04, 0.008, 0, 10, 0, 0, 0, 0).At(theta);
            var m = profile.At(theta);
            var offset = new CamAnalysis(profile, true, 0.04, 0.008, 0.01, 10, 0, 0, 0, 0).At(theta);
            return Math.Abs(inline.PressureAngle - Math.Atan(m.S1 / (0.048 + m.S))) < 1e-12
                && Math.Abs(offset.PressureAngle - Math.Atan((m.S1 - 0.01) / (Math.Sqrt(0.048 * 0.048 - 0.01 * 0.01) + m.S))) < 1e-12;
        }, iter: 200);
    }

    [Fact]
    public void The_Pitch_Curve_Radius_Matches_The_Norton_Formula_And_A_Circle_Gives_Its_Own_Radius()
    {
        // Norton (e = 0): ρ = [(R_p + s)² + s′²]^{3/2} / [(R_p + s)² + 2 s′² − (R_p + s) s″].
        var profile = new CamProfile([new CamSegment(CamSegmentKind.Rise, CamLaw.Poly345, Math.PI, 0.03), new CamSegment(CamSegmentKind.Return, CamLaw.Poly345, Math.PI, 0.03)]);

        Gen.Double[0.01, 2 * Math.PI - 0.01].Sample(theta =>
        {
            var p = new CamAnalysis(profile, true, 0.05, 0.01, 0, 5, 0, 0, 0, 0).At(theta);
            var m = profile.At(theta);
            var y = 0.06 + m.S;
            var rho = Math.Pow(y * y + m.S1 * m.S1, 1.5) / (y * y + 2 * m.S1 * m.S1 - y * m.S2);
            return Math.Abs(p.Curvature - 1 / rho) <= 1e-9 * Math.Max(1, Math.Abs(1 / rho));
        }, iter: 200);

        var circle = Cam([MechanismCalculator.Row(MechanismCalculator.Dwell, null, 360, 0)]);
        Assert.Equal(0.048, circle.OutputSi("rho_min"), 12);
        Assert.Equal(0, circle.OutputSi("phi_max_pk"), 12);
    }

    [Fact]
    public void A_Roller_Larger_Than_The_Smallest_Radius_Of_Curvature_Undercuts_And_Fails()
    {
        var ok = Cam();
        Assert.Contains(ok.Checks, c => c.Name.StartsWith("No undercut", StringComparison.Ordinal) && c.Passed);

        var bad = Cam(RiseDwellReturnDwell(MechanismCalculator.Cycloidal, 45, 135, 25), Q("Rb", 8, "mm"), Q("R_r", 10, "mm"));
        Assert.False(bad.IsBlocked);
        Assert.Contains(bad.Checks, c => c.Name.StartsWith("No undercut", StringComparison.Ordinal) && !c.Passed);
        Assert.False(bad.Passed);
        Assert.True(bad.OutputSi("u_rho") > 1);
    }

    [Fact]
    public void The_Reported_Minimum_Base_Circle_Holds_The_Pressure_Angle_At_Its_Limit()
    {
        Gen.Select(Gen.Double[10, 40], Gen.Double[60, 150], Gen.Double[-12, 12], Gen.Double[20, 35]).Sample((lift, beta, offset, limit) =>
        {
            var rows = RiseDwellReturnDwell(MechanismCalculator.Poly345, beta, (360 - 2 * beta) / 2, lift);
            var first = Cam(rows, Q("e_off", offset, "mm"), Q("phi_max", limit, "deg"));

            if (first.IsBlocked)
                return false;

            var rbMin = first.OutputSi("Rb_min_phi") * 1e3;

            if (rbMin <= 0.2)
                return true;

            var second = Cam(rows, Q("e_off", offset, "mm"), Q("phi_max", limit, "deg"), Q("Rb", rbMin, "mm"));
            return !second.IsBlocked && Math.Abs(second.OutputSi("phi_max_pk") * 180 / Math.PI - limit) < 1e-6 * limit;
        }, iter: 40);
    }

    [Fact]
    public void A_Weak_Spring_Lets_The_Follower_Jump_And_The_Sheet_Says_So()
    {
        var jump = Cam(RiseDwellReturnDwell(MechanismCalculator.Cycloidal, 60, 120, 20), Q("k_sp", 0.5, "N/mm"), Q("F_pre", 1, "N"), Q("F_ext", 0, "N"), Q("n_in", 1500, "rpm"));
        Assert.True(jump.OutputSi("F_min") < 0);
        Assert.Contains(jump.Checks, c => c.Name.StartsWith("Follower stays on the cam", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(jump.Warnings, w => w.RuleId == "contact");

        var held = Cam();
        Assert.True(held.OutputSi("F_min") >= 0);
        Assert.Contains(held.Checks, c => c.Name.StartsWith("Follower stays on the cam", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void The_Force_Is_Inertia_Plus_Spring_Plus_External_And_The_Torque_Is_Force_Times_Velocity_Over_Speed()
    {
        var r = Cam();
        var w = 600 * 2 * Math.PI / 60;
        var a = Vector(r, "a_sw");
        var s = Vector(r, "s_sw");
        var f = Vector(r, "F_sw");
        var v = Vector(r, "v_sw");
        var t = Vector(r, "T_in_sw");

        for (var i = 0; i < f.Count; i++)
        {
            Assert.Equal(0.3 * a[i] + 8000 * s[i] + 30 + 10, f[i], 7);
            Assert.Equal(f[i] * v[i] / w, t[i], 9);
        }
    }

    [Fact]
    public void A_Flat_Faced_Follower_Has_No_Pressure_Angle_And_Reports_The_Face_It_Needs()
    {
        var r = Cam(null, CalcInputValue.Choice("follower", MechanismCalculator.FlatFaced));
        Assert.False(r.IsBlocked);
        Assert.Null(r.FindOutput("phi_sw"));
        Assert.Null(r.FindOutput("Rb_min_phi"));
        Assert.Null(r.FindOutput("u_rho"));
        Assert.Equal(2 * 0.02 / (Math.PI / 2), r.OutputSi("w_face"), 9);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("No cusp", StringComparison.Ordinal) && c.Passed);
        Assert.NotNull(r.FindOutput("rho_sw"));
        Assert.Null(r.FindOutput("kappa_sw"));
    }

    [Fact]
    public void A_Flat_Faced_Follower_On_A_Cusped_Profile_Fails_Where_The_Radius_Goes_Negative()
    {
        // R_b = 5 mm with a 20 mm cycloidal rise over 60°: R_b + s + s″ reaches about −91 mm. The old check read a clamped 1e-9 and passed.
        var r = Cam(RiseDwellReturnDwell(MechanismCalculator.Cycloidal, 60, 120, 20), CalcInputValue.Choice("follower", MechanismCalculator.FlatFaced), Q("Rb", 5, "mm"));
        Assert.False(r.IsBlocked);
        Assert.True(r.OutputSi("rho_min") < -0.05);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("No cusp", StringComparison.Ordinal) && !c.Passed);
        Assert.False(r.Passed);
        Assert.True(Vector(r, "rho_sw").Min() >= r.OutputSi("rho_min") - 1e-9);
    }

    [Fact]
    public void A_Positive_Offset_Lowers_The_Rise_Pressure_Angle_And_The_Sign_Is_As_Documented()
    {
        // The cam turns counter-clockwise, the follower axis is at +x: φ = arctan[(s′ − e)/(√(R_p² − e²) + s)].
        var rows = RiseDwellReturnDwell(MechanismCalculator.Cycloidal, 120, 60, 20);
        var inline = Cam(rows, Q("e_off", 0, "mm"));
        var plus = Cam(rows, Q("e_off", 10, "mm"));
        var minus = Cam(rows, Q("e_off", -10, "mm"));
        const int mid = 60 / 5; // the row of the mid-rise (θ = 60°) of the 5° table
        Assert.True(Math.Abs(Vector(plus, "phi_sw")[mid]) < Math.Abs(Vector(inline, "phi_sw")[mid]));
        Assert.True(Math.Abs(Vector(minus, "phi_sw")[mid]) > Math.Abs(Vector(inline, "phi_sw")[mid]));
        Assert.True(Vector(plus, "phi_sw")[0] < 0);
        Assert.Contains("positive e reduces the pressure angle", Mechanism.Template.FindInput("e_off")!.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Roller_Curvature_Table_Replaces_The_Radius_Placeholder_And_The_Contact_Normal_Exceeds_The_Axial_Force()
    {
        var r = Cam();
        Assert.Null(r.FindOutput("rho_sw"));
        var kappa = Vector(r, "kappa_sw");
        Assert.InRange(kappa.Max(), 0.9 / r.OutputSi("rho_min"), 1 / r.OutputSi("rho_min") + 1e-9);
        Assert.True(r.OutputSi("F_pin_pk") >= r.OutputSi("follower_force") - 1e-9);
        Assert.Equal(8000 * 0.02 + 30, r.OutputSi("F_spring_max"), 9);
        Assert.Equal(0.02, r.OutputSi("lift"), 12);
    }

    [Fact]
    public void A_Follower_Offset_At_Or_Beyond_The_Prime_Radius_Blocks()
    {
        var r = Cam(null, Q("e_off", 48, "mm"));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.RuleId == "cam-geometry" && b.Message.StartsWith("e_off:", StringComparison.Ordinal));
    }

    [Fact]
    public void Doubling_The_Cam_Speed_Quadruples_The_Acceleration_And_Doubles_The_Velocity_Of_The_Same_Cam()
    {
        var slow = Cam();
        var fast = Cam(null, Q("n_in", 1200, "rpm"));
        Assert.Equal(2 * slow.OutputSi("output_motion"), fast.OutputSi("output_motion"), 9);
        Assert.Equal(4 * slow.OutputSi("peak_accel"), fast.OutputSi("peak_accel"), 7);
        Assert.Equal(8 * slow.OutputSi("j_pk"), fast.OutputSi("j_pk"), 5);
        Assert.Equal(slow.OutputSi("phi_max_pk"), fast.OutputSi("phi_max_pk"), 12);
    }

    [Fact]
    public void The_Spring_Inputs_Take_The_Dimensions_Of_The_M7_Rate_And_Force_Ports()
    {
        var spring = new SpringCalculator().Template;
        var rate = Mechanism.Template.FindInput("k_sp")!;
        var preload = Mechanism.Template.FindInput("F_pre")!;
        Assert.Equal(UnitCatalog.Parse(spring.OutputPorts!.Single(p => p.Symbol == "k").Unit).Dimension, UnitCatalog.Parse(rate.DefaultUnit).Dimension);
        Assert.Equal(UnitCatalog.Parse(spring.OutputPorts!.Single(p => p.Symbol == "F_1").Unit).Dimension, UnitCatalog.Parse(preload.DefaultUnit).Dimension);
        Assert.Equal(CalcInputKind.Quantity, rate.Kind);
        Assert.Equal(CalcInputKind.Quantity, preload.Kind);
    }

    [Fact]
    public void The_Cam_Ports_Are_Outputs_Of_The_Declared_Dimension()
    {
        var run = Cam();
        Assert.False(run.IsBlocked);

        foreach (var port in Mechanism.Template.OutputPorts!)
            Assert.Equal(UnitCatalog.Parse(port.Unit).Dimension, run.FindOutput(port.Symbol)!.Value.Dimension);

        Assert.Equal(run.OutputSi("v_pk"), run.OutputSi("output_motion"), 12);
    }

    [Fact]
    public void The_Largest_Cam_Within_The_Caps_Runs_Within_The_Time_Budget()
    {
        var rows = Enumerable.Range(0, 12).SelectMany(_ => new[]
        {
            MechanismCalculator.Row(MechanismCalculator.Rise, MechanismCalculator.Poly4567, 20, 1),
            MechanismCalculator.Row(MechanismCalculator.Return, MechanismCalculator.ModifiedTrapezoid, 10, 1),
        }).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = Cam(rows, CalcInputValue.Of("steps", 3600), Q("Rb", 60, "mm"));
        sw.Stop();
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(3601, Vector(r, "theta_sw").Count);
        Assert.True(sw.Elapsed.TotalSeconds < 5, $"{sw.ElapsedMilliseconds} ms");

        var tooMany = Cam([.. rows, .. rows]);
        Assert.True(tooMany.IsBlocked);
    }

    [Fact]
    public void The_Input_Frequency_In_Hz_Is_The_Speed_Over_Sixty_For_Every_Mode()
    {
        // n_in is an angular velocity (rpm), not a frequency, so it cannot be linked to a frequency input: f_in is the port that can.
        var fourBar = Run(Q("n_in", 150, "rpm"));
        Assert.Equal(2.5, fourBar.OutputSi("f_in"), 12);
        var slider = Run(CalcInputValue.Choice("mode", MechanismCalculator.SliderCrank), Q("n_in", 30, "rpm"));
        Assert.Equal(0.5, slider.OutputSi("f_in"), 12);
        Assert.Contains(Mechanism.Template.OutputPorts!, p => p.Symbol == "f_in" && p.Unit == "Hz");
    }
}
