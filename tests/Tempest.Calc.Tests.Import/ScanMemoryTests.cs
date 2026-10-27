using System.Diagnostics;
using System.Text;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Xunit.Abstractions;

namespace Tempest.Calc.Tests.Import;

/// <summary>
/// The cost of a table at the row cap (W2 review, L5): rows are processed as they are read, so memory is the observations, not the rows twice over.
/// It asserts a wall time as well, so it runs with the timing tests: alone, and on CI without the coverage collector.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait("Category", TimingCollection.Category)]
public sealed class ScanMemoryTests(ITestOutputHelper output)
{
    [Fact]
    public void A_Table_At_The_Row_Cap_Is_Read_In_Bounded_Memory_And_Time()
    {
        // 999,999 data rows (plus the header: exactly 1,000,000 rows) in about 4 MB, the worst a 5 MB file can hold.
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []);
        var sb = new StringBuilder("Feature,Actual\n", 4_100_000);

        for (var i = 0; i < 999_999; i++)
            sb.Append("a,").Append(i % 10).Append('\n');

        var file = Samples.Text(sb.ToString());
        sb = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var baseline = GC.GetTotalMemory(forceFullCollection: true);
        var clock = Stopwatch.StartNew();
        long peak = 0;
        using var sampler = new Timer(_ => peak = Math.Max(peak, GC.GetTotalMemory(forceFullCollection: false)), null, 0, 25);

        var analysis = ScanImporter.Analyse(file, profile);

        clock.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before; // this thread only, the one that reads the table
        var retained = GC.GetTotalMemory(forceFullCollection: false) - baseline;
        output.WriteLine($"rows 999999, input {file.Length / 1e6:0.0} MB: {clock.Elapsed.TotalSeconds:0.00} s, allocated {allocated / 1e6:0} MB, managed heap peak (sampled) {(peak - baseline) / 1e6:0} MB above the baseline, retained at the end {retained / 1e6:0} MB, observations {analysis.Data.Observations.Count}");

        if (Environment.GetEnvironmentVariable("W2_MEMORY_REPORT") is { Length: > 0 } report)
            File.AppendAllText(report, $"rows 999999: {clock.Elapsed.TotalSeconds:0.00} s, allocated {allocated / 1e6:0} MB, managed peak above baseline {(peak - baseline) / 1e6:0} MB, retained {retained / 1e6:0} MB, process peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / 1e6:0} MB\n");

        Assert.Equal(999_999, analysis.Data.Observations.Count);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30));
        Assert.True(allocated < 800L * 1024 * 1024, $"allocated {allocated / 1e6:0} MB");
    }
}
