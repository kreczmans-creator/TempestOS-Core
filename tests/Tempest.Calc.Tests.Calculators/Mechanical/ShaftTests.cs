using CsCheck;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class ShaftTests
{
    private static readonly ShaftCalculator Shaft = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Shaft.Template);

    [Fact]
    public void Two_Planes_Combine_As_A_Vector()
    {
        var vertical = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((200, -3, 0, 0))));
        var both = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((200, -3, 4, 0))));
        Assert.Equal(5.0 / 3.0 * vertical.OutputSi("M_max"), both.OutputSi("M_max"), 6);
        Assert.Equal(5.0 / 3.0 * vertical.OutputSi("R_A"), both.OutputSi("R_A"), 6);
    }

    [Fact]
    public void Deflection_Falls_With_The_Fourth_Power_Of_Diameter()
    {
        var thin = CalcRunner.Run(Shaft, Defaults()).OutputSi("y_load");
        var thick = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((400, 60, 0)))).OutputSi("y_load");
        Assert.Equal(16, thin / thick, 6);
    }

    [Fact]
    public void Dunkerley_Never_Exceeds_Rayleigh()
    {
        Gen.Select(Gen.Double[5, 80], Gen.Double[50, 350], Gen.Double[1, 100]).Sample((d, x, m) =>
        {
            var r = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((400, d, 0))).With(ShaftCalculator.Loads((x, -1, 0, m), (400 - x / 2, 0, 0, m / 2))));
            return r.OutputSi("n_c_D") <= r.OutputSi("n_c") * (1 + 1e-9);
        }, iter: 40);
    }

    [Fact]
    public void A_Bore_Raises_The_Stress()
    {
        var solid = CalcRunner.Run(Shaft, Defaults()).OutputSi("n_f_min");
        var hollow = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((400, 30, 15)))).OutputSi("n_f_min");
        Assert.True(hollow < solid);
    }

    [Fact]
    public void Low_Critical_Speed_Warns()
    {
        var r = CalcRunner.Run(Shaft, Defaults().With(CalcInputValue.Quantity("n", 4000, "rpm")));
        Assert.Contains(r.Warnings, w => w.RuleId == "critical-speed");
    }

    [Fact]
    public void Bearing_Off_The_Shaft_Blocks()
    {
        var r = CalcRunner.Run(Shaft, Defaults().With(CalcInputValue.Quantity("x_B", 500, "mm")));
        Assert.True(r.IsBlocked);
        Assert.Equal("geometry", r.Blocks[0].RuleId);
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(0.9, 1.2815516)]
    [InlineData(0.99, 2.3263479)]
    [InlineData(0.999, 3.0902323)]
    public void Normal_Quantile_Matches_Tables(double p, double z) => Assert.Equal(z, ShaftCalculator.NormalQuantile(p), 6);

    [Fact]
    public void A_Shoulder_Is_Checked_On_Its_Smaller_Diameter()
    {
        var stepped = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((200, 30, 0), (400, 40, 0))));
        var uniform = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((400, 30, 0))));
        Assert.True(stepped.OutputSi("n_f_min") > 0.9 * uniform.OutputSi("n_f_min"));
        Assert.Equal(40, stepped.OutputSi("d_B") * 1e3, 9);
    }

    [Fact]
    public void A_Surface_Factor_Record_Overrides_The_Table_Equation()
    {
        // Opus verification of finding 2: k_a from a shaft.marin record replaces a·S_ut^b.
        var computed = CalcRunner.Run(Shaft, Defaults());
        var overridden = CalcRunner.Run(Shaft, Defaults().With(CalcInputValue.Of("k_a", 0.7)));
        Assert.Equal(0.7, overridden.Steps.Last(s => s.Symbol == "k_a").Result is Tempest.Calc.Engine.Values.ScalarValue v ? v.Si : double.NaN, 12);
        Assert.NotEqual(computed.OutputSi("n_f_min"), overridden.OutputSi("n_f_min"));
    }

    private const double RpmPerSi = 60 / (2 * Math.PI);

    [Fact]
    public void The_Critical_Speed_Ratio_And_Warning_Use_The_Dunkerley_Lower_Bound()
    {
        // Green G-A-07: Rayleigh is the upper bound; a ratio and a warning built on it can pass a shaft whose lower bound is within 2×.
        var loads = ShaftCalculator.Loads((100, -1, 0, 20), (300, -1, 0, 20));
        var probe = CalcRunner.Run(Shaft, Defaults().With(loads));
        var rayleigh = probe.OutputSi("n_c") * RpmPerSi;
        var dunkerley = probe.OutputSi("n_c_D") * RpmPerSi;
        Assert.True(dunkerley < rayleigh * (1 - 1e-6), "the two bounds must differ for the test to mean anything");

        var operating = (rayleigh + dunkerley) / 4;
        var r = CalcRunner.Run(Shaft, Defaults().With(loads).With(CalcInputValue.Quantity("n", operating, "rpm")));
        Assert.Equal(dunkerley / operating, r.OutputSi("r_c"), 6);
        Assert.True(r.OutputSi("r_c") < 2 && rayleigh / operating > 2);
        Assert.Contains(r.Warnings, w => w.RuleId == "critical-speed");
    }

    [Fact]
    public void Every_Mass_On_A_Bearing_With_No_Density_Skips_The_Critical_Speed_With_A_Warning()
    {
        // Black B-A-07(a): this used to block with "'n_c' is not a finite number".
        var r = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((0, -2, 0, 20))));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Contains(r.Warnings, w => w.RuleId == "critical-speed-skipped");
        Assert.DoesNotContain(r.Outputs, o => o.Symbol == "n_c");
    }

    [Fact]
    public void A_Bare_Shaft_With_No_Density_Says_Why_There_Is_No_Critical_Speed()
    {
        var r = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((200, -2, 0, 0))));
        Assert.Contains(r.Warnings, w => w.RuleId == "critical-speed-skipped");
        var withDensity = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((200, -2, 0, 0))).With(CalcInputValue.Quantity("rho", 7850, "kg/m^3")));
        Assert.DoesNotContain(withDensity.Warnings, w => w.RuleId == "critical-speed-skipped");
        Assert.Contains(withDensity.Outputs, o => o.Symbol == "n_c");
    }

    [Fact]
    public void A_Running_Shaft_With_No_Critical_Speed_Found_Fails_The_Critical_Speed_Check_And_A_Shaft_At_Rest_Does_Not()
    {
        // F2: with every mass on a bearing and no density no critical speed is found, the check that it clears the running speed was left out and the sheet passed a shaft that runs. It now fails,
        // while a shaft at rest (n = 0) has no critical speed to clear and is not failed on it.
        var running = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((0, -2, 0, 20))).With(CalcInputValue.Quantity("n", 1500, "rpm")));
        Assert.False(running.IsBlocked, string.Join("; ", running.Blocks.Select(b => b.Message)));
        Assert.Contains(running.Warnings, w => w.RuleId == "critical-speed-skipped");
        var check = Assert.Single(running.Checks, c => c.Name.StartsWith("Critical speed against the operating speed", StringComparison.Ordinal));
        Assert.False(check.Passed);
        Assert.False(running.Passed);

        var atRest = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((0, -2, 0, 20))).With(CalcInputValue.Quantity("n", 0, "rpm")));
        Assert.Contains(atRest.Warnings, w => w.RuleId == "critical-speed-skipped");
        Assert.DoesNotContain(atRest.Checks, c => c.Name.StartsWith("Critical speed against", StringComparison.Ordinal));

        // With a density the critical speed is found and the check is the ordinary one.
        var found = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((0, -2, 0, 20))).With(CalcInputValue.Quantity("n", 1500, "rpm")).With(CalcInputValue.Quantity("rho", 7850, "kg/m^3")));
        Assert.DoesNotContain(found.Warnings, w => w.RuleId == "critical-speed-skipped");
        Assert.NotNull(found.FindOutput("n_c"));
    }

    [Fact]
    public void Bearings_Closer_Than_A_Two_Hundredth_Of_The_Length_Block_In_The_Shafts_Own_Words()
    {
        // Black B-A-07(b): x_B = 1 mm on a 400 mm shaft used to surface "the structure is a mechanism (non-positive stiffness pivot…)".
        var r = CalcRunner.Run(Shaft, Defaults().With(CalcInputValue.Quantity("x_B", 1, "mm")));
        Assert.True(r.IsBlocked);
        Assert.Equal("geometry", r.Blocks[0].RuleId);
        Assert.Contains("1/200", r.Blocks[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("mechanism", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Peak_Torque_Governs_The_Yield_Check_Not_The_Fatigue_Check()
    {
        // Green G-A-06: d = 30 mm, M = 200 N·m, K = 1, S_y = 450 MPa. σ_a = τ(400 N·m) = 75.451 MPa: n_y = 450/√(σ² + 3τ²) = 2.98206 with T_pk = 0.4 kN·m,
        // 5.47305 on the steady 0.1 kN·m (python3).
        var basis = Defaults().With(ShaftCalculator.Segments((400, 30, 0))).With(ShaftCalculator.Loads((200, -2, 0, 0))).With(ShaftCalculator.Sections((200, 1, 1, 0, 0))).With(CalcInputValue.Quantity("T", 0.1, "kN*m"));
        var steady = CalcRunner.Run(Shaft, basis);
        var peak = CalcRunner.Run(Shaft, basis.With(CalcInputValue.Quantity("T_pk", 0.4, "kN*m")));
        Assert.Equal(5.473049392, steady.OutputSi("n_y_min"), 6);
        Assert.Equal(2.982058652, peak.OutputSi("n_y_min"), 6);
        Assert.Equal(steady.OutputSi("n_f_min"), peak.OutputSi("n_f_min"), 12);
    }

    private static CalcInputSet AlternatingBasis() => Defaults()
        .With(ShaftCalculator.Segments((400, 27.94, 0))).With(ShaftCalculator.Loads((200, -0.2848, 0, 0))).With(ShaftCalculator.Sections((200, 1.68, 1.42, 0.85, 0.92)))
        .With(CalcInputValue.Quantity("S_ut", 723.949485, "MPa")).With(CalcInputValue.Quantity("S_y", 565.370074, "MPa")).With(CalcInputValue.Choice("finish", ShaftCalculator.Machined)).With(CalcInputValue.Quantity("R", 99, "%"))
        .With(CalcInputValue.Quantity("x_T1", 0, "mm")).With(CalcInputValue.Quantity("x_T2", 400, "mm"));

    [Fact]
    public void An_Alternating_Torque_Enters_The_Fatigue_Check_As_An_Alternating_Stress()
    {
        // chain.drive-train-01. d = 27.94 mm, S_ut 723.95 and S_y 565.37 MPa, machined, R 99 %, K_t 1.68, K_ts 1.42, q 0.85, q_s 0.92 (Shigley Example 7-1 data); M = 0.2848 kN x 0.4 m / 4 = 28.48 N·m.
        // T_m = 50 N·m, T_a = 150 N·m. python3: σ_a 20.988 MPa, τ_m 16.186, τ_a 48.559, S_e 201.50 MPa; 1/n_f = √(σ_a² + 3τ_a²)/S_e + √3 τ_m/S_ut
        // gives n_f 2.13252 (the torque taken as steady, 50 N·m, gave 6.9987 and the whole 200 N·m taken as steady 3.8601); n_y = S_y/√(σ_a² + 3τ_pk²) with τ_pk (200 N·m) = 64.745 MPa gives 4.95549.
        var basis = AlternatingBasis().With(CalcInputValue.Quantity("T", 0.05, "kN*m"));
        var steady = CalcRunner.Run(Shaft, basis);
        var alternating = CalcRunner.Run(Shaft, basis.With(CalcInputValue.Quantity("T_a", 0.15, "kN*m")));
        Assert.False(alternating.IsBlocked, string.Join("; ", alternating.Blocks.Select(b => b.Message)));
        Assert.Equal(6.998678407, steady.OutputSi("n_f_min"), 5);
        Assert.Equal(2.132515973, alternating.OutputSi("n_f_min"), 5);
        Assert.Equal(4.955494791, alternating.OutputSi("n_y_min"), 5);
    }

    [Fact]
    public void A_Fully_Alternating_Torque_Has_No_Mean_Term()
    {
        // T_m = 0, T_a = 124.2833 N·m on the Shigley 7-1 shaft: 1/n_f = √(σ_a² + 3τ_a²)/S_e with σ_a 104.910 MPa, τ_a 40.234 MPa, S_e 201.500 MPa gives 1.599885 (python3);
        // the same torque as a steady one gave 1.620988.
        var basis = Defaults().With(ShaftCalculator.Segments((400, 27.94, 0))).With(ShaftCalculator.Loads((200, -1.4236088, 0, 0))).With(ShaftCalculator.Sections((200, 1.68, 1.42, 0.85, 0.92)))
            .With(CalcInputValue.Quantity("S_ut", 723.949485, "MPa")).With(CalcInputValue.Quantity("S_y", 565.370074, "MPa")).With(CalcInputValue.Choice("finish", ShaftCalculator.Machined)).With(CalcInputValue.Quantity("R", 99, "%"))
            .With(CalcInputValue.Quantity("x_T1", 0, "mm")).With(CalcInputValue.Quantity("x_T2", 400, "mm"));
        var r = CalcRunner.Run(Shaft, basis.With(CalcInputValue.Quantity("T_a", 0.1242833119, "kN*m")));
        Assert.Equal(1.599884694, r.OutputSi("n_f_min"), 5);
        Assert.Equal(1.620988247, CalcRunner.Run(Shaft, basis.With(CalcInputValue.Quantity("T", 0.1242833119, "kN*m"))).OutputSi("n_f_min"), 5);
    }

    [Fact]
    public void The_Key_Is_Checked_On_The_Peak_Torque_Not_The_Steady_One()
    {
        // chain.drive-train-01: the key sheet used T (the RMS torque in the drive-train pack) and ignored T_pk. d = 45 mm, w 14, h 9, L 40 mm, T_m 100 N·m, T_a 100 N·m, T_pk 400 N·m:
        // τ = 2 x 400 / (45 x 14 x 40) = 31.746 MPa and σ = 4 x 400 / (45 x 9 x 40) = 98.765 MPa (python3), at the peak 400 N·m; with T_pk blank the peak is T_m + T_a = 200 N·m, half of each.
        var basis = Defaults().With(ShaftCalculator.Segments((400, 45, 0))).With(CalcInputValue.Quantity("T", 0.1, "kN*m")).With(CalcInputValue.Quantity("T_a", 0.1, "kN*m"))
            .With(CalcInputValue.Quantity("x_key", 100, "mm")).With(CalcInputValue.Quantity("w", 14, "mm")).With(CalcInputValue.Quantity("h", 9, "mm")).With(CalcInputValue.Quantity("L_key", 40, "mm")).With(CalcInputValue.Quantity("S_y_key", 400, "MPa"));
        var blank = CalcRunner.Run(Shaft, basis);
        var peak = CalcRunner.Run(Shaft, basis.With(CalcInputValue.Quantity("T_pk", 0.4, "kN*m")));
        Assert.Equal(15.873016, blank.OutputSi("tau_key") / 1e6, 5);
        Assert.Equal(31.746032, peak.OutputSi("tau_key") / 1e6, 5);
        Assert.Equal(98.765432, peak.OutputSi("sigma_key") / 1e6, 5);
        Assert.Equal(2 * blank.OutputSi("L_key_req"), peak.OutputSi("L_key_req"), 12);
    }

    [Fact]
    public void A_Key_Missing_Some_Of_Its_Four_Dimensions_Says_So()
    {
        // Black B-A-16(a): a position and a width without a height and a length silently dropped the check.
        var r = CalcRunner.Run(Shaft, Defaults().With(CalcInputValue.Quantity("x_key", 100, "mm")).With(CalcInputValue.Quantity("w", 8, "mm")));
        Assert.Contains(r.Warnings, w => w.RuleId == "key");
        var whole = CalcRunner.Run(Shaft, Defaults().With(CalcInputValue.Quantity("x_key", 100, "mm")).With(CalcInputValue.Quantity("w", 8, "mm")).With(CalcInputValue.Quantity("h", 7, "mm")).With(CalcInputValue.Quantity("L_key", 30, "mm")));
        Assert.DoesNotContain(whole.Warnings, w => w.RuleId == "key");
    }

    [Fact]
    public void The_Key_Working_Names_Its_Symbols_And_Units()
    {
        // Black B-A-16(b): the safety factor was written as n (the speed), and T carried no unit.
        var inputs = Defaults().With(ShaftCalculator.Segments((400, 45, 0))).With(CalcInputValue.Quantity("T", 0.3, "kN*m")).With(CalcInputValue.Quantity("x_key", 100, "mm")).With(CalcInputValue.Quantity("w", 14, "mm")).With(CalcInputValue.Quantity("h", 9, "mm")).With(CalcInputValue.Quantity("L_key", 40, "mm")).With(CalcInputValue.Quantity("S_y_key", 400, "MPa"));
        var r = CalcRunner.Run(Shaft, inputs);
        var shear = r.Steps.Single(st => st.Symbol == "tau_key");
        Assert.Contains("N·m", shear.Substitution, StringComparison.Ordinal);
        var required = r.Steps.Single(st => st.Symbol == "L_key_req");
        Assert.Contains("n_target", required.Formula, StringComparison.Ordinal);
        Assert.Contains("n_target", required.Substitution, StringComparison.Ordinal);
    }
}
