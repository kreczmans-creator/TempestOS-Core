using CsCheck;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class PlateBendingTests
{
    private static readonly PlateBendingCalculator Plate = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Plate.Template);

    private static CalcRunResult Run(params CalcInputValue[] changes) => CalcRunner.Run(Plate, changes.Aggregate(Defaults(), (set, v) => set.With(v)));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    private static CalcInputValue[] Circle(bool clamped, string load = PlateBendingCalculator.Uniform) =>
        [Choice("shape", PlateBendingCalculator.Circular), Choice("support", clamped ? PlateBendingCalculator.Clamped : PlateBendingCalculator.Simple), Choice("load_type", load), Q("a", 250, "mm"), Q("t", 5, "mm"), Q("q", 20, "kPa"), Q("E", 200, "GPa"), N("nu", 0.3)];

    private static CalcInputValue[] Rect(bool clamped, double a = 500, double b = 500, string load = PlateBendingCalculator.Uniform) =>
        [Choice("shape", PlateBendingCalculator.Rectangular), Choice("support", clamped ? PlateBendingCalculator.Clamped : PlateBendingCalculator.Simple), Choice("load_type", load), Q("a", a, "mm"), Q("b", b, "mm"), Q("t", 8, "mm"), Q("q", 10, "kPa"), Q("E", 210, "GPa"), N("nu", 0.3)];

    private static CalcRunResult With(CalcInputValue[] set, params CalcInputValue[] more) => Run([.. set, .. more]);

    private const double A = 0.25;
    private const double Nu = 0.3;
    private const double D = 200e9 * 1.25e-7 / (12 * (1 - Nu * Nu));

    [Fact]
    public void The_Uniform_Circular_Closed_Forms_Match_The_General_Solution()
    {
        const double q = 20e3;
        var clamped = PlateBendingMath.AxisymmetricPlate.Patch(A, A, Nu, D, q, true);
        Assert.Equal(q * Math.Pow(A, 4) / (64 * D), clamped.W(0), 12);
        Assert.Equal(-q * A * A / 8, clamped.Mr(A), 9);
        Assert.Equal(-Nu * q * A * A / 8, clamped.Mt(A), 9);
        Assert.Equal(0, clamped.W(A), 12);

        var simple = PlateBendingMath.AxisymmetricPlate.Patch(A, A, Nu, D, q, false);
        Assert.Equal((5 + Nu) * q * Math.Pow(A, 4) / (64 * (1 + Nu) * D), simple.W(0), 12);
        Assert.Equal((3 + Nu) * q * A * A / 16, simple.Mr(0), 9);
        Assert.Equal(0, simple.Mr(A), 9);
        Assert.Equal(0, simple.W(A), 12);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Patch_And_Ring_Closed_Forms_Equal_The_Solution_At_The_Centre(bool clamped)
    {
        const double q = 20e3;
        const double c = 0.1;
        var patch = PlateBendingMath.AxisymmetricPlate.Patch(A, c, Nu, D, q, clamped);
        var wPatch = clamped ? PlateBendingMath.PatchCentreDeflectionClamped(A, c, D, q) : PlateBendingMath.PatchCentreDeflectionSimple(A, c, Nu, D, q);
        Assert.Equal(wPatch, patch.W(0), 12);

        var ring = PlateBendingMath.AxisymmetricPlate.Ring(A, c, Nu, D, 4e3, clamped);
        var wRing = clamped ? PlateBendingMath.RingCentreDeflectionClamped(A, c, D, 4e3) : PlateBendingMath.RingCentreDeflectionSimple(A, c, Nu, D, 4e3);
        Assert.Equal(wRing, ring.W(0), 12);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_Patch_Is_The_Sum_Of_The_Rings_It_Is_Made_Of(bool clamped)
    {
        // Independent route: integrate the ring solution (force 2πρq dρ) over the patch radius by the midpoint rule and compare with the patch solution.
        const double q = 20e3;
        const double c = 0.12;
        var patch = PlateBendingMath.AxisymmetricPlate.Patch(A, c, Nu, D, q, clamped);
        const int n = 4000;

        foreach (var r in new[] { 0.01, 0.08, 0.15, 0.22 })
        {
            double w = 0, mr = 0, mt = 0;

            for (var i = 0; i < n; i++)
            {
                var rho = c * (i + 0.5) / n;
                var ring = PlateBendingMath.AxisymmetricPlate.Ring(A, rho, Nu, D, q * 2 * Math.PI * rho * c / n, clamped);
                w += ring.W(r);
                mr += ring.Mr(r);
                mt += ring.Mt(r);
            }

            Assert.Equal(patch.W(r), w, Math.Abs(patch.W(r)) * 2e-4);
            Assert.Equal(patch.Mr(r), mr, Math.Abs(patch.Mr(r)) * 2e-3 + 1e-3);
            Assert.Equal(patch.Mt(r), mt, Math.Abs(patch.Mt(r)) * 2e-3 + 1e-3);
        }
    }

    [Fact]
    public void A_Ring_Satisfies_Continuity_And_The_Edge_Conditions()
    {
        foreach (var clamped in new[] { true, false })
        {
            var p = PlateBendingMath.AxisymmetricPlate.Ring(A, 0.1, Nu, D, 3e3, clamped);
            const double e = 1e-9;
            Assert.Equal(p.W(0.1 - e), p.W(0.1 + e), 8);
            Assert.Equal(p.Mr(0.1 - e), p.Mr(0.1 + e), 4);
            Assert.Equal(p.Mt(0.1 - e), p.Mt(0.1 + e), 4);
            Assert.Equal(0, p.W(A), 12);

            if (clamped)
                Assert.Equal(0, (p.W(A) - p.W(A - 1e-6)) / 1e-6, 4);
            else
                Assert.Equal(0, p.Mr(A), 8);
        }
    }

    [Fact]
    public void The_Greatest_Stress_Is_At_The_Edge_For_A_Clamped_And_At_The_Centre_For_A_Simply_Supported_Uniform_Plate()
    {
        var clamped = Run(Circle(true));
        Assert.Equal("edge", ((TextValue)clamped.FindOutput("location")!.Value).Value);
        Assert.Equal(37.5e6, clamped.OutputSi("sigma_principal"), 1);
        Assert.Equal(37.5e6 * Math.Sqrt(1 - Nu + Nu * Nu), clamped.OutputSi("sigma_max"), 1);

        var simple = Run(Circle(false));
        Assert.Equal("centre", ((TextValue)simple.FindOutput("location")!.Value).Value);
        Assert.Equal(simple.OutputSi("sigma_principal"), simple.OutputSi("sigma_max"), 1);
    }

    [Fact]
    public void The_Navier_Series_Gives_The_Classical_Square_Plate_Coefficient_And_Reports_Its_Terms()
    {
        const double a = 0.5;
        var d = PlateBendingMath.Rigidity(210e9, 0.008, Nu);
        var r = PlateBendingMath.NavierSimplySupported(a, a, Nu, d, 10e3, null, null);
        Assert.Equal(0.00406, r.Deflection * d / (10e3 * Math.Pow(a, 4)), 5);
        Assert.Equal(0.00406235266, r.Deflection * d / (10e3 * Math.Pow(a, 4)), 10);
        Assert.Equal(0.0478864, r.Mx / (10e3 * a * a), 6);
        Assert.Equal(0.0324823, r.MxyCorner / (10e3 * a * a), 5);
        Assert.True(r.Converged);
        Assert.True(r.LastChange < PlateBendingCalculator.NavierTolerance);
        Assert.InRange(r.TermsPerDirection, 8, PlateBendingCalculator.NavierTermCap);

        var sheet = Run(Rect(false));
        Assert.Equal(r.TermsPerDirection, (int)sheet.OutputSi("terms_used"));
        Assert.DoesNotContain(sheet.Warnings, w => w.RuleId == "navier-converge");
    }

    [Fact]
    public void Navier_Point_Evaluation_Agrees_With_The_Centre_Sums_And_The_Edge_Is_Simply_Supported()
    {
        const double a = 0.6;
        const double b = 0.4;
        var d = PlateBendingMath.Rigidity(210e9, 0.006, Nu);
        var series = PlateBendingMath.NavierSimplySupported(a, b, Nu, d, 5e3, null, null, 256);
        var at = PlateBendingMath.NavierAt(a, b, Nu, d, 5e3, null, null, a / 2, b / 2, series.TermsPerDirection);
        Assert.Equal(series.Deflection, at.W, Math.Abs(at.W) * 1e-11);
        Assert.Equal(series.Mx, at.Mx, Math.Abs(at.Mx) * 1e-9);
        var edge = PlateBendingMath.NavierAt(a, b, Nu, d, 5e3, null, null, 0, b / 2, 64);
        Assert.Equal(0, edge.W, 12);
        Assert.Equal(0, edge.Mx, 6);
    }

    [Theory]
    [InlineData(500, 500, 0.3)]
    [InlineData(600, 400, 0.3)]
    [InlineData(900, 300, 0.2)]
    public void The_Greatest_Von_Mises_Stress_Of_A_Simply_Supported_Rectangle_Is_At_The_Centre_Or_A_Corner(double a, double b, double nu)
    {
        var d = PlateBendingMath.Rigidity(210e9, 0.008, nu);
        var r = PlateBendingMath.NavierSimplySupported(a / 1e3, b / 1e3, nu, d, 1e3, null, null);
        var reported = Math.Max(r.CentreVonMises, r.CornerVonMises);
        var worst = 0.0;

        for (var i = 0; i <= 8; i++)
        {
            for (var j = 0; j <= 8; j++)
            {
                var p = PlateBendingMath.NavierAt(a / 1e3, b / 1e3, nu, d, 1e3, null, null, a / 1e3 * i / 16.0, b / 1e3 * j / 16.0, 120);
                worst = Math.Max(worst, PlateBendingMath.VonMises(p.Mx, p.My, p.Mxy));
            }
        }

        Assert.True(worst <= reported * (1 + 2e-3), $"{worst} > {reported}");
    }

    [Theory]
    [InlineData(600, 400, 0.3, 150, 100)]
    [InlineData(500, 500, 0.33, 250, 250)]
    [InlineData(900, 300, 0.2, 90, 60)]
    [InlineData(600, 400, 0.3, 540, 360)]
    public void The_Greatest_Von_Mises_Stress_Of_A_Patch_Loaded_Rectangle_Is_At_The_Centre_Or_A_Corner(double a, double b, double nu, double u, double v)
    {
        var d = PlateBendingMath.Rigidity(210e9, 0.008, nu);
        var r = PlateBendingMath.NavierSimplySupported(a / 1e3, b / 1e3, nu, d, 1e3, u / 1e3, v / 1e3);
        var reported = Math.Max(r.CentreVonMises, r.CornerVonMises);
        var worst = 0.0;

        for (var i = 0; i <= 16; i++)
        {
            for (var j = 0; j <= 16; j++)
            {
                var p = PlateBendingMath.NavierAt(a / 1e3, b / 1e3, nu, d, 1e3, u / 1e3, v / 1e3, a / 1e3 * i / 32.0, b / 1e3 * j / 32.0, 160);
                worst = Math.Max(worst, PlateBendingMath.VonMises(p.Mx, p.My, p.Mxy));
            }
        }

        Assert.True(worst <= reported * (1 + 2e-3), $"{worst} > {reported}");
    }

    [Theory]
    [InlineData(1.0, 0.25, 2.9679217308584787)]
    [InlineData(0.6, 0.12, 0.6838674139402859)]
    public void Review_M1_The_Corner_Twisting_Moment_Of_A_Long_Uniform_Plate_Is_Extrapolated_And_The_Series_Counts_As_Converged(double a, double b, double mxy)
    {
        // Python limit: (1 − ν)·16q/π⁴·Σ 1/(ab·den²) at 4096 terms with the 1/N² tail removed, q = 1 kN/m², ν = 0.3.
        var d = PlateBendingMath.Rigidity(210e9, 0.008, Nu);
        var r = PlateBendingMath.NavierSimplySupported(a, b, Nu, d, 1e3, null, null);
        Assert.True(r.Converged, $"last change {r.LastChange}");
        Assert.Equal(mxy, r.MxyCorner, mxy * 2e-8);

        var sheet = With(Rect(false, a * 1e3, b * 1e3));
        Assert.DoesNotContain(sheet.Warnings, w => w.RuleId == "navier-converge");
    }

    [Theory]
    [InlineData(2000, 500)]
    [InlineData(5000, 500)]
    [InlineData(500, 5000)]
    public void Review_M1_Long_Plates_Do_Not_Warn_That_The_Series_Failed_To_Converge(double a, double b) =>
        Assert.DoesNotContain(With(Rect(false, a, b)).Warnings, w => w.RuleId == "navier-converge");

    [Fact]
    public void Review_M2_The_Ritz_Order_Cap_Is_Thirty_The_Clamped_Patch_Minimum_Is_A_Quarter_And_The_Warning_Makes_No_Accuracy_Claim()
    {
        Assert.Equal(30, PlateBendingCalculator.RitzCap);
        Assert.Equal([8, 12, 16, 20, 25, 30], PlateBendingCalculator.RitzOrders);
        Assert.Contains(With(Rect(true, 1000, 1000, PlateBendingCalculator.Patch), Q("u", 240, "mm"), Q("v", 240, "mm")).Blocks, b => b.RuleId == "patch-min");
        var ok = With(Rect(true, 1000, 1000, PlateBendingCalculator.Patch), Q("u", 250, "mm"), Q("v", 250, "mm"));
        Assert.False(ok.IsBlocked, string.Join("; ", ok.Blocks.Select(b => b.Message)));
        Assert.True(ok.OutputSi("terms_used") <= 30);

        // A long clamped plate does not reach 1e-5: the warning quotes the change and about three times it, and no longer says "accurate to".
        var long5 = With(Rect(true, 2500, 500));
        foreach (var w in long5.Warnings.Where(w => w.RuleId is "ritz-converge" or "ritz-approx"))
            Assert.DoesNotContain("accurate to about that change", w.Message, StringComparison.Ordinal);

        // F2: a Ritz solution that did not converge warned and passed; its convergence check now fails (the stress is still shown).
        Assert.False(long5.IsBlocked);
        var convergence = Assert.Single(long5.Checks, k => k.Name == "Ritz change against the tolerance");
        Assert.False(convergence.Passed);
        Assert.False(long5.Passed);
        Assert.Contains(ok.Checks, k => k.Name == "Ritz change against the tolerance" && !k.Passed);

        // Order 30 settles the square (w to ~1e-10 relative).
        const double a = 0.5;
        var d = PlateBendingMath.Rigidity(210e9, 0.008, Nu);
        var k30 = PlateBendingMath.ClampedRitz.Solve(a, a, Nu, d, 10e3, null, null, 30);
        var k20 = PlateBendingMath.ClampedRitz.Solve(a, a, Nu, d, 10e3, null, null, 20);
        Assert.Equal(k20.At(a / 2, a / 2).W, k30.At(a / 2, a / 2).W, k20.At(a / 2, a / 2).W * 1e-8);
        Assert.Equal(k20.At(0, a / 2).Mx, k30.At(0, a / 2).Mx, Math.Abs(k20.At(0, a / 2).Mx) * 1e-4);
    }

    [Fact]
    public void Review_The_Frequency_Iteration_Reports_Whether_It_Settled()
    {
        const double a = 0.5;
        var d = PlateBendingMath.Rigidity(210e9, 0.008, Nu);
        var ritz = PlateBendingMath.ClampedRitz.Solve(a, a, Nu, d, 1.0, null, null, 8);
        var full = ritz.FundamentalAngularFrequency(7850 * 0.008, out var settled, out var change);
        Assert.True(settled);
        Assert.True(change <= 1e-11);
        Assert.Equal(ritz.FundamentalAngularFrequency(7850 * 0.008), full);

        // Two iterations cannot settle: the flag is false, the last change is reported and the value is still close.
        var cut = ritz.FundamentalAngularFrequency(7850 * 0.008, out var cutSettled, out var cutChange, maxIterations: 2);
        Assert.False(cutSettled);
        Assert.True(cutChange > 1e-11 && double.IsFinite(cutChange));
        Assert.Equal(full, cut, full * 1e-2);

        // The sheet does not raise the warning for ordinary clamped plates, and the rule is declared.
        Assert.DoesNotContain(With(Rect(true)).Warnings, w => w.RuleId == "ritz-frequency");
        Assert.Contains(Plate.Template.ApplicabilityRules, r => r.Id == "ritz-frequency");
    }

    [Fact]
    public void Review_L5_L9_The_Stress_Check_Cites_The_Yield_Criterion_And_A_Simply_Supported_Edge_Moment_Is_Exactly_Zero()
    {
        var r = Run(Circle(false));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Stress", StringComparison.Ordinal) && c.Clause == "6.2.1(5)");
        Assert.Equal(0, r.OutputSi("M_edge"));
        Assert.Contains(Plate.Template.ClauseRefs, c => c.Clause == "6.2.1(5)");
        Assert.DoesNotContain(Plate.Template.ApplicabilityRules, x => x.Id == "ring-basis");
        Assert.Equal("1.3.0", Plate.Template.Version);
    }

    [Fact]
    public void A_Patch_The_Size_Of_The_Plate_Is_The_Uniform_Load_In_Both_Solutions()
    {
        const double a = 0.6;
        const double b = 0.4;
        var d = PlateBendingMath.Rigidity(210e9, 0.006, Nu);
        var uniform = PlateBendingMath.NavierSimplySupported(a, b, Nu, d, 5e3, null, null);
        var patch = PlateBendingMath.NavierSimplySupported(a, b, Nu, d, 5e3, a, b);

        // A patch that covers the plate is the uniform load and takes the same path: identical terms, extrapolation and results.
        Assert.Equal(uniform.TermsPerDirection, patch.TermsPerDirection);
        Assert.Equal(uniform.Deflection, patch.Deflection, uniform.Deflection * 1e-12);
        Assert.Equal(uniform.Mx, patch.Mx, Math.Abs(uniform.Mx) * 1e-12);
        Assert.Equal(uniform.MxyCorner, patch.MxyCorner, uniform.MxyCorner * 1e-12);

        // A patch just under the plate size is not the uniform path but is within the convergence tolerance of it.
        var nearly = PlateBendingMath.NavierSimplySupported(a, b, Nu, d, 5e3, a * 0.999, b * 0.999);
        Assert.True(nearly.Converged);
        Assert.Equal(uniform.Deflection, nearly.Deflection, uniform.Deflection * 5e-3);

        var ru = PlateBendingMath.ClampedRitz.Solve(a, b, Nu, d, 5e3, null, null, 8);
        var rp = PlateBendingMath.ClampedRitz.Solve(a, b, Nu, d, 5e3, a, b, 8);
        Assert.Equal(ru.At(a / 2, b / 2).W, rp.At(a / 2, b / 2).W, ru.At(a / 2, b / 2).W * 1e-9);
    }

    [Fact]
    public void The_Clamped_Square_Ritz_Solution_Matches_The_Classical_Coefficients_And_Converges()
    {
        const double a = 0.5;
        var d = PlateBendingMath.Rigidity(210e9, 0.008, Nu);
        var last = 0.0;

        foreach (var k in new[] { 4, 8, 12, 16, 20 })
        {
            var r = PlateBendingMath.ClampedRitz.Solve(a, a, Nu, d, 10e3, null, null, k);
            var w = r.At(a / 2, a / 2).W * d / (10e3 * Math.Pow(a, 4));

            if (k == 20)
            {
                Assert.Equal(0.0012653191, w, 8);
                Assert.Equal(0.0229051, r.At(a / 2, a / 2).Mx / (10e3 * a * a), 5);
                Assert.Equal(-0.05133, r.At(0, a / 2).Mx / (10e3 * a * a), 4);
                Assert.Equal(0, r.At(0, a / 2).W, 12);
            }

            last = w;
        }

        Assert.InRange(last, 0.00126, 0.00127);
    }

    [Fact]
    public void A_Long_Clamped_Plate_Approaches_The_Strip_Solution()
    {
        // Infinite strip clamped on its long edges: w = q·b⁴/(384D), edge moment q·b²/12.
        const double b = 0.1;
        const double a = 1.0;
        var d = PlateBendingMath.Rigidity(210e9, 0.004, Nu);
        var r = PlateBendingMath.ClampedRitz.Solve(a, b, Nu, d, 1e3, null, null, 20);
        Assert.Equal(1e3 * Math.Pow(b, 4) / (384 * d), r.At(a / 2, b / 2).W, 1e3 * Math.Pow(b, 4) / (384 * d) * 1e-3);
        Assert.Equal(-1e3 * b * b / 12, r.At(a / 2, 0).My, 1e3 * b * b / 12 * 5e-3);
    }

    [Fact]
    public void Natural_Frequencies_Match_The_Published_Parameters()
    {
        Assert.Equal(10.2158, PlateBendingMath.CircularFrequencyParameter(true, 0.3), 3);
        Assert.Equal(4.935, PlateBendingMath.CircularFrequencyParameter(false, 0.3), 3);

        const double a = 0.5;
        var d = PlateBendingMath.Rigidity(210e9, 0.008, Nu);
        var ritz = PlateBendingMath.ClampedRitz.Solve(a, a, Nu, d, 1.0, null, null, 8);
        var omega = ritz.FundamentalAngularFrequency(7850 * 0.008);
        Assert.Equal(35.985, omega * a * a * Math.Sqrt(7850 * 0.008 / d), 0.01);

        var sheet = Run(Rect(false, 600, 400));
        var omegaSs = Math.PI * Math.PI * (1 / 0.36 + 1 / 0.16) * Math.Sqrt(PlateBendingMath.Rigidity(210e9, 0.008, Nu) / (7850 * 0.008));
        Assert.Equal(omegaSs / (2 * Math.PI), sheet.OutputSi("f1"), 1e-6);
    }

    [Fact]
    public void Total_Force_On_The_Whole_Plate_Is_The_Same_As_The_Equivalent_Pressure()
    {
        var byPressure = With(Rect(false, 600, 400), Q("q", 10, "kPa"));
        var byForce = With(Rect(false, 600, 400), Choice("load_basis", PlateBendingCalculator.BasisForce), Q("P", 2.4, "kN"));
        Assert.Equal(byPressure.OutputSi("w_max"), byForce.OutputSi("w_max"), byPressure.OutputSi("w_max") * 1e-12);
        Assert.Equal(byPressure.OutputSi("sigma_max"), byForce.OutputSi("sigma_max"), byPressure.OutputSi("sigma_max") * 1e-9);
    }

    [Fact]
    public void Loads_That_Do_Not_Fit_The_Plate_Block_With_The_Input_Name()
    {
        Assert.Contains(With(Rect(false), Choice("load_type", PlateBendingCalculator.Ring)).Blocks, b => b.RuleId == "ring-rect" && b.Message.StartsWith("load_type:", StringComparison.Ordinal));
        Assert.Contains(With(Circle(true, PlateBendingCalculator.Patch), Q("r0", 300, "mm")).Blocks, b => b.RuleId == "patch-size" && b.Message.StartsWith("r0:", StringComparison.Ordinal));
        Assert.Contains(With(Circle(true, PlateBendingCalculator.Patch), Q("r0", 1, "mm")).Blocks, b => b.RuleId == "patch-min");
        Assert.Contains(With(Circle(true, PlateBendingCalculator.Ring), Q("r_ring", 249, "mm")).Blocks, b => b.RuleId == "ring-radius");
        Assert.Contains(With(Rect(false, 500, 500, PlateBendingCalculator.Patch), Q("u", 600, "mm")).Blocks, b => b.RuleId == "patch-size");
        Assert.Contains(With(Rect(true, 500, 500, PlateBendingCalculator.Patch), Q("u", 30, "mm"), Q("v", 30, "mm")).Blocks, b => b.RuleId == "patch-min");
        Assert.Contains(With(Rect(false, 3000, 200)).Blocks, b => b.RuleId == "aspect" && b.Message.StartsWith("b:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Deflection_Over_Half_The_Thickness_And_A_Thick_Plate_Warn()
    {
        var thin = With(Circle(false), Q("t", 1, "mm"), Q("q", 50, "kPa"));
        Assert.True(thin.OutputSi("w_over_t") > 0.5);
        Assert.Contains(thin.Warnings, w => w.RuleId == "large-deflection" && w.Message.StartsWith("t:", StringComparison.Ordinal));
        Assert.DoesNotContain(Run(Circle(true)).Warnings, w => w.RuleId == "large-deflection");

        // F2: below span/t = 10 the thin-plate theory is stiffer than the plate and the sheet warned and passed; it is refused (the default circle is 250 mm in radius: span 500 mm, so t = 60 mm is span/t 8.3).
        var thickPlate = With(Circle(true), Q("t", 60, "mm"));
        Assert.True(thickPlate.IsBlocked);
        var thickBlock = Assert.Single(thickPlate.Blocks, b => b.RuleId == "thin-plate");
        Assert.StartsWith("t:", thickBlock.Message, StringComparison.Ordinal);
        Assert.Empty(CalcMessageShape.Problems(thickBlock.Message));
        Assert.DoesNotContain(With(Circle(true), Q("t", 50, "mm")).Notices, n => n.RuleId == "thin-plate");
        Assert.All(new[] { "thin-plate", "navier-converge" }, id => Assert.Contains(Plate.Template.ApplicabilityRules, r => r.Id == id && r.Severity == CalcNoticeSeverity.Block && r.Effect == CalcRuleEffect.Unconservative));
        Assert.Contains(Plate.Template.ApplicabilityRules, r => r.Id == "ritz-converge" && r.Severity == CalcNoticeSeverity.Warning && r.Effect == CalcRuleEffect.Unconservative);
        Assert.DoesNotContain(With(Circle(true, PlateBendingCalculator.Ring), Q("P", 4, "kN"), Q("r_ring", 125, "mm")).Warnings, w => w.RuleId.Contains("ring", StringComparison.Ordinal));
        Assert.Contains(With(Rect(true)).Warnings, w => w.RuleId == "ritz-approx");
    }

    [Fact]
    public void Inputs_Outside_Their_Valid_Range_Are_Blocked()
    {
        foreach (var change in new[] { Q("t", 0, "mm"), Q("t", 400, "mm"), Q("a", 1, "mm"), Q("q", 0, "kPa"), Q("E", 0, "GPa"), N("nu", 0.5), N("nu", -0.1), Q("rho", 0, "kg/m^3"), N("gamma_M0", 0.9), N("defl_ratio", 1) })
        {
            var notices = CalcInputValidator.Validate(Plate.Template, Defaults().With(change));
            Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.RuleId.Contains(change.Symbol, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Ports_Records_And_Rule_Ids_Are_Declared()
    {
        var r = Run(Circle(true));
        Assert.All(Plate.Template.OutputPorts!, p => Assert.NotNull(r.FindOutput(p.Symbol)));
        Assert.Equal(["w_max", "sigma_max", "f1"], Plate.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.Contains(Plate.Template.OutputPorts!, p => p is { Symbol: "f1", Unit: "Hz" });

        foreach (var symbol in new[] { "E", "nu", "rho", "f_y" })
            Assert.Equal(symbol, Plate.Template.FindInput(symbol)!.MaterialProperty);

        Assert.Equal(["E", "nu", "rho", "f_y"], Plate.Template.RecordKinds!.Single(k => k.Kind == "material.properties").Symbols);
        Assert.Equal(Plate.Template.RecordKinds!.SelectMany(k => k.Symbols).Count(), Plate.Template.RecordKinds!.SelectMany(k => k.Symbols).Distinct().Count());

        var declared = Plate.Template.ApplicabilityRules.Select(x => x.Id).ToHashSet();
        var runs = new[]
        {
            Run(Circle(true)), With(Circle(true, PlateBendingCalculator.Patch)), With(Circle(false, PlateBendingCalculator.Ring), Q("P", 4, "kN"), Q("r_ring", 100, "mm")), With(Rect(false)), With(Rect(true)),
            With(Rect(false), Choice("load_type", PlateBendingCalculator.Ring)), With(Circle(true), Q("t", 60, "mm")), With(Circle(false), Q("t", 1, "mm"), Q("q", 50, "kPa")), With(Rect(false, 3000, 200)),
            With(Circle(true, PlateBendingCalculator.Patch), Q("r0", 300, "mm")), With(Circle(true, PlateBendingCalculator.Patch), Q("r0", 2, "mm"), Q("t", 5, "mm")), With(Rect(true, 900, 200)),
        };

        foreach (var notice in runs.SelectMany(x => x.Notices))
        {
            if (notice.RuleId.StartsWith("input.", StringComparison.Ordinal) || notice.RuleId.StartsWith("record.", StringComparison.Ordinal))
                continue;

            Assert.Contains(notice.RuleId, declared);
        }
    }

    [Fact]
    public void The_Sheet_Carries_A_Deflection_Profile_And_Both_Checks()
    {
        foreach (var set in new[] { Circle(true), Rect(false), Rect(true) })
        {
            var r = With(set);
            Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
            var series = Assert.Single(r.Series);
            Assert.True(series.Points.Count >= 21);
            Assert.Contains(r.Checks, c => c.Name.StartsWith("Stress", StringComparison.Ordinal));
            Assert.Contains(r.Checks, c => c.Name.StartsWith("Deflection", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Deflection_Scales_As_One_Over_Thickness_Cubed_And_Stress_As_One_Over_Thickness_Squared()
    {
        Gen.Double[1.0, 5.0].Select(Gen.Double[1.0, 2.5], Gen.Int[0, 3]).Sample((t, grow, which) =>
        {
            grow = Math.Min(grow, 40.0 / (t * 4)); // the thick plate stays at span/t of at least 10 on the shortest span of 400 mm (F2)
            CalcInputValue[] set = which switch
            {
                0 => Circle(true),
                1 => Circle(false, PlateBendingCalculator.Patch),
                2 => Rect(false, 600, 400),
                _ => Rect(true, 500, 400),
            };
            var thin = With(set, Q("t", t * 4, "mm"), Q("q", 1, "kPa"));
            var thick = With(set, Q("t", t * 4 * grow, "mm"), Q("q", 1, "kPa"));

            if (thin.IsBlocked || thick.IsBlocked)
                return false;

            var wRatio = thin.OutputSi("w_max") / thick.OutputSi("w_max");
            var sRatio = thin.OutputSi("sigma_max") / thick.OutputSi("sigma_max");
            return Math.Abs(wRatio / Math.Pow(grow, 3) - 1) < 1e-6 && Math.Abs(sRatio / (grow * grow) - 1) < 1e-4;
        }, iter: 60);
    }
}
