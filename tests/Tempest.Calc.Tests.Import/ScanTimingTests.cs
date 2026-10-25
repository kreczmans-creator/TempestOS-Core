using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsCheck;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;

namespace Tempest.Calc.Tests.Import;

/// <summary>Runs alone: wall-clock assertions are not made while other test classes load the machine (the best of three runs per size is taken as well).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Timing";
}

/// <summary>The scan statistics time tests (W2 review H2, N3, N6).</summary>
[Collection(TimingCollection.Name)]
public sealed class ScanTimingTests
{
    private static TimeSpan TimeAnalysis(int rows)
    {
        var sb = new StringBuilder("F,N,A\n", rows * 14);

        for (var i = 0; i < rows; i++)
            sb.Append("a,").Append(i).Append(',').Append(i).Append('\n');

        var file = Samples.Text(sb.ToString());
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("F", Nominal: "N", Actual: "A"), [], Delimiter: ScanDelimiter.Comma, Number: NumberProfile.Point);
        var best = TimeSpan.MaxValue;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var clock = Stopwatch.StartNew();
            var a = ScanImporter.Analyse(file, profile);
            clock.Stop();
            Assert.Equal(rows, a.Stats[0].Count);
            best = clock.Elapsed < best ? clock.Elapsed : best;
        }

        return best;
    }

    [Fact]
    public void Statistics_Time_Grows_Linearly_With_The_Rows_Whatever_Their_Nominals_Are()
    {
        // A ratio, so a slow or loaded machine does not fail it: ten times the rows costs about ten times the time. The quadratic version cost about a hundred
        // times (6.9 s at 40,000 distinct nominals).
        _ = TimeAnalysis(2_000); // warm-up: JIT and first-use costs
        var small = TimeAnalysis(6_000);
        var large = TimeAnalysis(60_000);
        var ratio = large.TotalSeconds / Math.Max(small.TotalSeconds, 0.02);

        Assert.True(ratio < 30, $"60,000 rows took {large.TotalSeconds:0.00} s against {small.TotalSeconds:0.000} s for 6,000: a ratio of {ratio:0.0} (linear is about 10, quadratic about 100)");
    }

    [Fact]
    public void The_Time_Of_Any_Size_Stays_Within_A_Bound_Scaled_By_The_Test_Timeout_Factor()
    {
        var factor = double.TryParse(Environment.GetEnvironmentVariable("TEMPEST_TEST_TIMEOUT_FACTOR"), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0 ? f : 1;

        Gen.Int[2_000, 60_000].Sample(rows =>
        {
            Assert.True(TimeAnalysis(rows) < TimeSpan.FromSeconds(3 * factor), $"{rows} rows");
        }, iter: 3);
    }
}
