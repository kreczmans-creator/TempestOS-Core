using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

/// <summary>
/// S9 wall-clock budgets (Yellow Y-B-11). They failed in full parallel runs of the Calculators suite (1537 ms and 1972 ms against a
/// 1500 ms budget) and pass alone, so they run in the non-parallel <see cref="TimingCollection"/> and judge the fastest of three runs, which
/// is the cost of the calculation and not of whatever else the machine was doing. The budget itself is unchanged.
/// </summary>
[Collection(TimingCollection.Name)]
public sealed class PlateBendingTimingTests
{
    private static readonly PlateBendingCalculator Plate = new();

    /// <summary>The CI runners scale every deadline by <c>TEMPEST_TEST_TIMEOUT_FACTOR</c> (3 on the Windows matrix), as in Red_SolverAttacks.</summary>
    private static readonly double TimeoutFactor = double.TryParse(Environment.GetEnvironmentVariable("TEMPEST_TEST_TIMEOUT_FACTOR"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) && f >= 1 ? f : 1;

    private const int Repeats = 3;

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Plate.Template);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue[] Rect(bool clamped, double a = 500, double b = 500, string load = PlateBendingCalculator.Uniform) =>
        [CalcInputValue.Choice("shape", PlateBendingCalculator.Rectangular), CalcInputValue.Choice("support", clamped ? PlateBendingCalculator.Clamped : PlateBendingCalculator.Simple), CalcInputValue.Choice("load_type", load), Q("a", a, "mm"), Q("b", b, "mm"), Q("t_p", 8, "mm"), Q("q", 10, "kPa"), Q("E", 210, "GPa"), CalcInputValue.Of("nu", 0.3)];

    private static CalcInputSet Sheet(IEnumerable<CalcInputValue> values) => values.Aggregate(Defaults(), (set, v) => set.With(v));

    /// <summary>Runs once to warm up, then <see cref="Repeats"/> times, and returns the last result and the fastest time in milliseconds.</summary>
    private static (CalcRunResult Result, long FastestMs) Time(CalcInputSet inputs)
    {
        var result = CalcRunner.Run(Plate, inputs);
        var fastest = long.MaxValue;

        for (var i = 0; i < Repeats; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            result = CalcRunner.Run(Plate, inputs);
            sw.Stop();
            fastest = Math.Min(fastest, sw.ElapsedMilliseconds);
        }

        return (result, fastest);
    }

    [Fact]
    public void Y_B_11_The_Wall_Clock_Budgets_Of_The_Plate_Calculator_Run_In_The_Non_Parallel_Timing_Collection()
    {
        var collection = typeof(PlateBendingTimingTests).CustomAttributes.Single(a => a.AttributeType == typeof(CollectionAttribute));
        Assert.Equal(TimingCollection.Name, collection.ConstructorArguments[0].Value);

        // Nothing left in the parallel plate test class measures wall-clock time.
        Assert.DoesNotContain(typeof(PlateBendingTests).GetMethods(), m => m.Name.Contains("_Speed_", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(5000, 500)]
    [InlineData(10000, 1000)]
    public void Review_Ritz_Speed_A_Long_Clamped_Plate_Stops_On_The_Stall_Rule_Quickly(double a, double b)
    {
        var (r, fastest) = Time(Sheet(Rect(true, a, b)));
        Assert.False(r.IsBlocked);
        Assert.True(fastest < 1500 * TimeoutFactor, $"{fastest} ms, fastest of {Repeats} (budget {1500 * TimeoutFactor:0} ms)");
        Assert.True(r.OutputSi("terms_used") <= PlateBendingCalculator.RitzCap);
    }

    [Fact]
    public void Review_Ritz_Speed_The_Smallest_Clamped_Patch_And_The_Square_Are_Quick_And_The_Run_Honours_Cancellation()
    {
        var inputs = Sheet(Rect(true, 1000, 1000, PlateBendingCalculator.Patch).Concat([Q("u", 250, "mm"), Q("v", 250, "mm")]));
        var (r, fastest) = Time(inputs);
        Assert.False(r.IsBlocked);
        Assert.True(fastest < 1500 * TimeoutFactor, $"{fastest} ms, fastest of {Repeats} (budget {1500 * TimeoutFactor:0} ms)");

        // The square settles before the cap (the sparse schedule keeps the quality: w to 1e-9, stress to 1e-5).
        var square = CalcRunner.Run(Plate, Sheet(Rect(true)));
        Assert.True(square.OutputSi("terms_used") <= 20);
        Assert.DoesNotContain(square.Warnings, w => w.RuleId == "ritz-converge");

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = CalcRunner.Run(Plate, inputs, cancellationToken: cts.Token);
        Assert.True(cancelled.IsBlocked || cancelled.Outputs.Count == 0);
    }
}
