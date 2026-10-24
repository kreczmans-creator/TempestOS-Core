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
