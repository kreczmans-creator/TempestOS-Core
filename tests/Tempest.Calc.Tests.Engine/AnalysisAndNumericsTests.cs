using CsCheck;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Analysis;
using Tempest.Calc.Engine.Numerics;

namespace Tempest.Calc.Tests.Engine;

public sealed class AnalysisAndNumericsTests
{
    [Fact]
    public void Sampler_Is_Deterministic_For_A_Seed()
    {
        var a = new Sampler(42);
        var b = new Sampler(42);
        Assert.Equal(Enumerable.Range(0, 100).Select(_ => a.NextDouble()), Enumerable.Range(0, 100).Select(_ => b.NextDouble()));
    }

    [Fact]
    public void Different_Seeds_Differ() => Assert.NotEqual(new Sampler(1).NextDouble(), new Sampler(2).NextDouble());

    [Fact]
    public void Uniform_Samples_Lie_In_Range()
    {
        var s = new Sampler(7);
        var d = Distribution.Uniform(2, 5);

        for (var i = 0; i < 10_000; i++)
        {
            var x = s.Next(d);
            Assert.InRange(x, 2, 5);
        }
    }

    [Fact]
    public void Normal_Samples_Have_The_Right_Mean_And_Sigma()
    {
        var s = new Sampler(11);
        var xs = Enumerable.Range(0, 50_000).Select(_ => s.Next(Distribution.Normal(10, 2))).ToList();
        Assert.Equal(10, Statistics.Mean(xs), 1);
        Assert.Equal(2, Statistics.StandardDeviation(xs), 1);
    }

    [Fact]
    public void Triangular_Samples_Have_The_Analytic_Mean()
    {
        var s = new Sampler(3);
        var xs = Enumerable.Range(0, 50_000).Select(_ => s.Next(Distribution.Triangular(0, 1, 5))).ToList();
        Assert.Equal(2.0, Statistics.Mean(xs), 1);
    }

    [Fact]
    public void Truncated_Normal_Respects_Bounds()
    {
        var s = new Sampler(5);
        var d = Distribution.TruncatedNormal(0, 1, -0.5, 0.5);

        for (var i = 0; i < 5_000; i++)
            Assert.InRange(s.Next(d), -0.5, 0.5);
    }

    [Theory]
    [InlineData(DistributionKind.Normal, 0, -1, 0, 0, 0)]
    [InlineData(DistributionKind.Uniform, 0, 0, 2, 1, 0)]
    [InlineData(DistributionKind.Triangular, 0, 0, 0, 2, 3)]
    [InlineData(DistributionKind.TruncatedNormal, 0, 1, 2, 1, 0)]
    [InlineData(DistributionKind.Normal, double.NaN, 1, 0, 0, 0)]
    public void Invalid_Distributions_Are_Refused(DistributionKind kind, double mean, double sigma, double lower, double upper, double mode) =>
        Assert.Throws<CalcEvaluationException>(() => new Distribution(kind, mean, sigma, lower, upper, mode).Validate("x"));

    [Fact]
    public void Monte_Carlo_Of_A_Sum_Of_Normals_Matches_RSS()
    {
        var nominal = new Dictionary<string, double> { ["a"] = 10, ["b"] = 20, ["c"] = 30 };
        var dists = new Dictionary<string, Distribution>
        {
            ["a"] = Distribution.Normal(10, 0.1),
            ["b"] = Distribution.Normal(20, 0.2),
            ["c"] = Distribution.Normal(30, 0.3),
        };

        var r = MonteCarlo.Run(x => x["a"] + x["b"] + x["c"], nominal, dists, samples: 40_000, seed: 99, lowerLimit: 59, upperLimit: 61);
        Assert.Equal(60, r.Mean, 1);
        Assert.Equal(Math.Sqrt(0.01 + 0.04 + 0.09), r.StandardDeviation, 2);
        Assert.NotNull(r.Yield);
        Assert.InRange(r.Yield!.Value, 0.98, 1.0);
        Assert.NotNull(r.Cpk);
        Assert.Equal(30, r.Histogram.Count);
        Assert.Equal(40_000, r.Histogram.Sum(b => b.Count));
    }

    [Fact]
    public void Monte_Carlo_Is_Reproducible_From_Its_Seed()
    {
        var nominal = new Dictionary<string, double> { ["a"] = 1 };
        var dists = new Dictionary<string, Distribution> { ["a"] = Distribution.Uniform(0, 2) };
        var r1 = MonteCarlo.Run(x => x["a"], nominal, dists, samples: 1000, seed: 5);
        var r2 = MonteCarlo.Run(x => x["a"], nominal, dists, samples: 1000, seed: 5);
        Assert.Equal(r1.Mean, r2.Mean);
        Assert.Equal(r1.Percentile95, r2.Percentile95);
    }

    [Fact]
    public void Monte_Carlo_Refuses_A_Distribution_Without_A_Nominal() =>
        Assert.Throws<CalcEvaluationException>(() => MonteCarlo.Run(_ => 1, new Dictionary<string, double>(), new Dictionary<string, Distribution> { ["q"] = Distribution.Uniform(0, 1) }));

    [Fact]
    public void Monte_Carlo_Refuses_A_Non_Finite_Model() =>
        Assert.Throws<CalcEvaluationException>(() => MonteCarlo.Run(_ => double.NaN, new Dictionary<string, double> { ["a"] = 1 }, new Dictionary<string, Distribution> { ["a"] = Distribution.Uniform(0, 1) }, samples: 10));

    [Fact]
    public void Monte_Carlo_Honours_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => MonteCarlo.Run(_ => 1, new Dictionary<string, double>(), new Dictionary<string, Distribution>(), samples: 10, cancellationToken: cts.Token));
    }

    [Fact]
    public void Monte_Carlo_Ten_Thousand_Samples_Is_Fast()
    {
        var nominal = new Dictionary<string, double> { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
        var dists = nominal.ToDictionary(kv => kv.Key, kv => Distribution.Normal(kv.Value, 0.1), StringComparer.Ordinal);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        MonteCarlo.Run(x => x["a"] * x["b"] + Math.Sqrt(x["c"]), nominal, dists);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Tornado_Orders_By_Swing()
    {
        var nominal = new Dictionary<string, double> { ["a"] = 1, ["b"] = 10 };
        var bars = Sensitivity.Tornado(x => x["a"] + x["b"], nominal, ["a", "b"]);
        Assert.Equal("b", bars[0].Input);
        Assert.Equal(2.0, bars[0].Swing, 9);
        Assert.Equal(0.2, bars[1].Swing, 9);
    }

    [Fact]
    public void Tornado_Takes_Absolute_Ranges()
    {
        var nominal = new Dictionary<string, double> { ["a"] = 1 };
        var bars = Sensitivity.Tornado(x => 2 * x["a"], nominal, ["a"], absoluteRanges: new Dictionary<string, (double, double)> { ["a"] = (0, 5) });
        Assert.Equal(0, bars[0].OutputAtLow);
        Assert.Equal(10, bars[0].OutputAtHigh);
    }

    [Fact]
    public void Tornado_Refuses_An_Unknown_Input() => Assert.Throws<CalcEvaluationException>(() => Sensitivity.Tornado(_ => 1, new Dictionary<string, double>(), ["zz"]));

    [Fact]
    public void Bisection_Finds_A_Root() => Assert.Equal(Math.Sqrt(2), RootFinding.Bisect(x => x * x - 2, 0, 2), 9);

    [Fact]
    public void Bisection_Refuses_A_Bracket_Without_Sign_Change() => Assert.Throws<CalcEvaluationException>(() => RootFinding.Bisect(x => x * x + 1, 0, 2));

    [Fact]
    public void Fixed_Point_Converges() => Assert.Equal(Math.Cos(0.7390851332151607), RootFinding.FixedPoint(Math.Cos, 1.0), 8);

    [Fact]
    public void Solve_Matches_A_Known_System()
    {
        var a = new double[,] { { 2, 1, -1 }, { -3, -1, 2 }, { -2, 1, 2 } };
        var x = LinearAlgebra.Solve(a, [8, -11, -3]);
        Assert.Equal(2, x[0], 9);
        Assert.Equal(3, x[1], 9);
        Assert.Equal(-1, x[2], 9);
    }

    [Fact]
    public void Determinant_Of_Identity_Is_One() => Assert.Equal(1.0, LinearAlgebra.Determinant(new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } }));

    [Fact]
    public void Inverse_Times_Matrix_Is_Identity()
    {
        var a = new double[,] { { 4, 7 }, { 2, 6 } };
        var p = LinearAlgebra.Multiply(a, LinearAlgebra.Inverse(a));
        Assert.Equal(1, p[0, 0], 9);
        Assert.Equal(0, p[0, 1], 9);
        Assert.Equal(1, p[1, 1], 9);
    }

    [Fact]
    public void Normal_Cdf_At_Zero_Is_Half() => Assert.Equal(0.5, Statistics.NormalCdf(0), 6);

    [Fact]
    public void Normal_Cdf_At_Three_Sigma_Matches_Tables() => Assert.InRange(Statistics.NormalCdf(3), 0.99864, 0.99866);

    [Fact]
    public void Quantile_Inverts_Cdf()
    {
        foreach (var p in new[] { 0.01, 0.1, 0.5, 0.9, 0.999 })
            Assert.Equal(p, Statistics.NormalCdf(Statistics.NormalQuantile(p)), 5);
    }

    [Fact]
    public void Ppm_Outside_Three_Sigma_Limits_Is_About_2700() => Assert.InRange(Statistics.PartsPerMillionOutside(0, 1, -3, 3), 2690, 2710);

    [Theory]
    [InlineData(7.0)]
    [InlineData(-7.0)]
    public void Deep_Tails_Keep_Their_Relative_Precision_On_Either_Side(double limit)
    {
        // Green G-07: 1 − Φ(7) cancelled to four digits; Φ(−7) = 1.279813e-12 to 1e-6 relative.
        var ppm = Statistics.PartsPerMillionOutside(0, 1, limit < 0 ? limit : null, limit > 0 ? limit : null);
        Assert.Equal(1.279812543885835e-6, ppm, 1e-12);
        Assert.True(Math.Abs(ppm / 1.279812543885835e-6 - 1) < 1e-6, $"ppm = {ppm}");
    }

    [Fact]
    public void Cpk_Is_The_Nearer_Limit() => Assert.Equal(1.0, Statistics.ProcessCapabilityIndex(5, 1, 2, 10)!.Value, 9);

    [Fact]
    public void Interpolation_Is_Exact_At_The_Nodes()
    {
        double[] xs = [0, 1, 2, 4];
        double[] ys = [1, 3, 2, 8];

        for (var i = 0; i < xs.Length; i++)
            Assert.Equal(ys[i], Interpolation.Linear(xs[i], xs, ys), 12);
    }

    [Fact]
    public void Interpolation_Holds_Ends_Unless_Extrapolating()
    {
        Assert.Equal(1, Interpolation.Linear(-5, [0, 1], [1, 3]));
        Assert.Equal(-9, Interpolation.Linear(-5, [0, 1], [1, 3], extrapolate: true));
    }

    [Fact]
    public void Property_Addition_Of_Lengths_Is_Commutative()
    {
        Gen.Select(Gen.Double[0.001, 1000], Gen.Double[0.001, 1000]).Sample((a, b) =>
        {
            var s = CalcEngine.Instance.CreateSession();
            s.Define("a", Tempest.Calc.Engine.Values.ScalarValue.Of(a, "mm"), record: false);
            s.Define("b", Tempest.Calc.Engine.Values.ScalarValue.Of(b, "m"), record: false);
            var ab = ((Tempest.Calc.Engine.Values.ScalarValue)s.Evaluate("a + b")).Si;
            var ba = ((Tempest.Calc.Engine.Values.ScalarValue)s.Evaluate("b + a")).Si;
            return Math.Abs(ab - ba) <= 1e-12 * Math.Max(1, Math.Abs(ab));
        });
    }

    [Fact]
    public void Property_Scaling_Inputs_Scales_Stress_Inversely_With_Area()
    {
        Gen.Select(Gen.Double[1, 1e6], Gen.Double[1, 1e6], Gen.Double[0.5, 4]).Sample((f, a, k) =>
        {
            var s = CalcEngine.Instance.CreateSession();
            s.Define("F", Tempest.Calc.Engine.Values.ScalarValue.Of(f, "N"), record: false);
            s.Define("A", Tempest.Calc.Engine.Values.ScalarValue.Of(a, "mm^2"), record: false);
            s.Define("k", new Tempest.Calc.Engine.Values.ScalarValue(k), record: false);
            var s1 = ((Tempest.Calc.Engine.Values.ScalarValue)s.Evaluate("F / A")).Si;
            var s2 = ((Tempest.Calc.Engine.Values.ScalarValue)s.Evaluate("(k * F) / (k * A)")).Si;
            return Math.Abs(s1 - s2) <= 1e-9 * Math.Abs(s1);
        });
    }

    [Fact]
    public void Property_Conversion_Round_Trips()
    {
        Gen.Select(Gen.Double[-1e6, 1e6], Gen.OneOfConst("mm", "in", "ft", "km", "m")).Sample((v, u) =>
        {
            var x = Tempest.Calc.Engine.Values.ScalarValue.Of(v, u);
            var back = ((Tempest.Calc.Engine.Values.ScalarValue)x.In(Tempest.Calc.Engine.Units.UnitCatalog.Parse(u))).Displayed;
            return Math.Abs(back - v) <= 1e-9 * Math.Max(1, Math.Abs(v));
        });
    }
}
