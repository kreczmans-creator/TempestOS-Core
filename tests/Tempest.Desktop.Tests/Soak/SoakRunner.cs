using System.Diagnostics;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>One scenario: what it is called in the report and how it runs against a started host.</summary>
/// <param name="SummaryFacts">The keys of the facts the summary table shows for the scenario.</param>
internal sealed record SoakScenario(string Id, string Title, string Covers, IReadOnlyList<string> SummaryFacts, Func<SoakHost, SoakScale, SoakOutcome, CancellationToken, Task> Run);

internal static partial class SoakScenarios
{
    /// <summary>The five scenarios of Addendum C, C-7, in the order the specification lists them, then the four that soak what the merged features (C-3 record import, C-6 demo project, C-4 dashboard, C-5 guided test) add.</summary>
    public static IReadOnlyList<SoakScenario> All { get; } =
    [
        new("1-instances", "500 calculation instances", "Create, run and save a calculation of every template, from its shipped worked example, in one project.", ["Calculations created, run and saved", "Templates covered", "Database growth per calculation (KB)"], InstancesAsync),
        new("2-chains", "Chains of depth 20", "Link calculations 20 deep; change the head; every step goes stale; update in order; every step is current.", ["Calculations in chains", "Steps reported stale after the head changed", "Steps updated in order"], ChainsAsync),
        new("3-cycles", "100 revise and supersede cycles", "Revise, change, check, issue and supersede one calculation over and over; every issued revision stays byte-identical and refuses edits.", ["Revisions issued", "Edit attempts refused", "Database growth per issued revision (KB)"], CyclesAsync),
        new("4-monte-carlo", "1,000 Monte Carlo runs", "The Monte Carlo service (engine and through a calculator) at its default sample count, its caps, and a tornado sweep.", ["Engine runs (default samples each)", "Calculator evaluations", "Tornado sweeps"], MonteCarloAsync),
        new("5-imports", "Imports at their size caps", "Scan tables, CAD expression files and parameter CSVs, and reference records, each at its cap and one past it.", ["Largest scan table accepted (bytes)", "CAD parameters accepted", "Refusals one past a cap"], ImportsAsync),
        new("6-record-import", "Record import at its caps", "Track P record templates imported at the 5 MB, 1,001-row, 64-column and 4,096-character caps, one past each cap refused, all or nothing, and the licence lock after many revisions.", ["Templates imported", "Records committed at the caps", "Refusals one past a cap"], RecordsAsync),
        new("7-demo-project", "Demo project installed and removed", "The demonstration project installed and removed over and over, a real project beside it byte for byte unchanged, nothing leaked and the database growing only by what a removal keeps by design.", ["Install and remove cycles", "Install (mean ms)", "Database a removal leaves, first cycle (KiB)"], DemoAsync),
        new("8-dashboard", "Verification dashboard built repeatedly", "The verification dashboard built over and over on a store with thousands of reference records and hundreds of guided runs; the time per build.", ["Reference records in the store", "Finished guided runs in the store", "Build on the full store: mean (ms)"], DashboardAsync),
        new("9-guided-runs", "Guided test runs to the run cap", "Guided runs started, filled in (110 steps), finished and repeated to the 1,000-run cap, then the older ones deleted; the time to read the run store as it grows.", ["Runs made through the service (started, 110 steps recorded, finished)", "Record one step (mean ms)", "Delete older finished runs: deleted, kept (the newest 50 and those holding a template's latest result)"], GuidedAsync),
    ];
}

/// <summary>Everything one scenario produced over its two passes, and the verdict on leaks.</summary>
internal sealed class SoakRun
{
    public required SoakScenario Scenario { get; init; }

    public required SoakScale Scale { get; init; }

    public required IReadOnlyList<SoakPass> Passes { get; init; }

    public required IReadOnlyList<SoakOutcome> Outcomes { get; init; }

    public required IReadOnlyList<string> LeakFindings { get; init; }

    /// <summary>Second pass minus first pass of the managed heap left once the host is disposed.</summary>
    public long HeapGrowthBytes => Passes[^1].HeapAfterDisposeBytes - Passes[0].HeapAfterDisposeBytes;

    public IEnumerable<string> Failures => Outcomes.SelectMany((o, i) => o.Failures.Select(f => $"pass {i + 1}: {f}")).Concat(LeakFindings);

    public bool Passed => !Failures.Any() && Outcomes.All(o => o.FailureCount == 0);
}

/// <summary>
/// Runs a scenario twice in one process, each time over a fresh host and a fresh database, and measures both. The second pass is the leak check:
/// whatever a pass leaves behind after its host is disposed and the heap is collected, the second pass must leave no more of, to within
/// <see cref="HeapGrowthBoundBytes"/>.
/// </summary>
internal static class SoakRunner
{
    /// <summary>
    /// The most the settled managed heap may grow from the first pass's end to the second pass's end before a third pass is asked to say whether
    /// it keeps growing. A leak worth the name (a retained calculation, a sheet, a connection's buffers) is tens of kilobytes per item over hundreds
    /// of items, many times this bound, and keeps growing; what a settled heap legitimately varies by (the runtime's own caches, a pooled buffer
    /// kept for a while after a large file was read) steps once, by up to about ten megabytes, and then stays.
    /// </summary>
    public const long HeapGrowthBoundBytes = 4L * 1024 * 1024;

    /// <summary>How many more handles the process may hold once the host is disposed than before it was built (the runtime opens a few lazily: epoll, timers).</summary>
    public const int HandleSlack = 3;

    /// <summary>How much slower the second pass may be than the first (plus a few seconds of noise): no scenario may degrade with use.</summary>
    public const double SlowdownFactor = 2.0;

    public static async Task<SoakRun> RunAsync(SoakScenario scenario, SoakScale scale, CancellationToken cancellationToken = default)
    {
        var passes = new List<SoakPass>();
        var outcomes = new List<SoakOutcome>();

        // Two passes, always. A third is run only when the second left the settled heap higher than the first by more than the bound: a leak keeps
        // growing from pass to pass, while a one-off step (a pooled buffer kept for a while, a cache filled once) does not, and the third pass tells them apart.
        for (var pass = 1; pass <= 3; pass++)
        {
            SoakScale.Progress($"{scenario.Id}: pass {pass} starts");
            var (measured, outcome) = await RunPassAsync(scenario, scale, cancellationToken);
            passes.Add(measured);
            outcomes.Add(outcome);
            SoakScale.Progress($"{scenario.Id}: pass {pass} done in {measured.Seconds:0.0} s, {outcome.FailureCount} failure(s)");

            if (pass == 2 && !HeapSteppedUp(passes, HeapGrowthBoundBytes))
                break;
        }

        return new SoakRun { Scenario = scenario, Scale = scale, Passes = passes, Outcomes = outcomes, LeakFindings = Evaluate(passes) };
    }

    private static long Settled(SoakPass pass) => pass.HeapAfterDisposeBytes;

    private static long Held(SoakPass pass) => pass.HeapAfterBytes - pass.HeapBeforeBytes;

    /// <summary>Whether the last pass left more than <paramref name="bound"/> more than the first, in what is left after disposal or in what is held for the same work.</summary>
    public static bool HeapSteppedUp(IReadOnlyList<SoakPass> passes, long bound) =>
        Settled(passes[^1]) - Settled(passes[0]) > bound || Held(passes[^1]) - Held(passes[0]) > bound + Held(passes[0]) / 10;

    /// <summary>
    /// Whether a series of measurements keeps growing: the last is more than <paramref name="limit"/> above the first and, when there is a third
    /// pass to say, it is still rising (by more than half the bound) from the one before it.
    /// </summary>
    private static bool Grows(IReadOnlyList<long> series, long limit, long bound) =>
        series[^1] - series[0] > limit && (series.Count < 3 || series[^1] - series[^2] > bound / 2);

    /// <summary>The leak and stability findings for a scenario's passes; empty when it is clean.</summary>
    public static IReadOnlyList<string> Evaluate(IReadOnlyList<SoakPass> passes, long? heapBound = null)
    {
        var findings = new List<string>();
        var first = passes[0];
        var bound = heapBound ?? HeapGrowthBoundBytes;
        var settled = passes.Select(Settled).ToList();
        var held = passes.Select(Held).ToList();
        var which = passes.Count > 2 ? "from the first pass to the last, and was still rising" : "from the first pass to the second";

        if (Grows(settled, bound, bound))
            findings.Add($"LEAK: the managed heap left after the host was disposed grew by {(settled[^1] - settled[0]) / (1024.0 * 1024):0.0} MiB {which} (bound {bound / (1024.0 * 1024):0.0} MiB).");

        if (Grows(held, bound + held[0] / 10, bound))
            findings.Add($"LEAK: with the host running, the last pass held {(held[^1] - held[0]) / (1024.0 * 1024):0.0} MiB more than the first for the same work.");

        foreach (var (pass, number) in passes.Select((p, i) => (p, i + 1)))
        {
            // The first pass also opens what loading the application's assemblies and libraries opens, once; a leak shows as the next pass ending with more.
            if (number > 1 && pass.HandlesAfterDispose > passes[number - 2].HandlesAfterDispose + HandleSlack)
                findings.Add($"LEAK: pass {number} ended with {pass.HandlesAfterDispose} open handles; the pass before it ended with {passes[number - 2].HandlesAfterDispose}.");

            if (pass.SqliteHandlesAfterDispose > 0)
                findings.Add($"LEAK: pass {number} left {pass.SqliteHandlesAfterDispose} handle(s) open on the database's files after its host was disposed (an unreleased SQLite connection).");

            if (pass.SqliteHandlesAfter >= 0 && pass.SqliteHandlesAfter > pass.SqliteHandlesBefore + HandleSlack)
                findings.Add($"LEAK: pass {number} grew the database's open handles (SQLite connections) from {pass.SqliteHandlesBefore} to {pass.SqliteHandlesAfter}.");
        }

        if (passes[1].Seconds > first.Seconds * SlowdownFactor + 5)
            findings.Add($"SLOWDOWN: the second pass took {passes[1].Seconds:0.0} s against {first.Seconds:0.0} s for the first (limit {SlowdownFactor:0.#} × + 5 s).");

        return findings;
    }

    private static async Task<(SoakPass Pass, SoakOutcome Outcome)> RunPassAsync(SoakScenario scenario, SoakScale scale, CancellationToken cancellationToken)
    {
        var outcome = new SoakOutcome();
        var handlesBaseline = SoakProbe.OpenHandles();
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        try
        {
            // Everything that holds the host lives inside this call, so once it has returned nothing of the pass is reachable and the heap
            // that is measured next is what the pass left behind, not what it still holds.
            var measured = await RunHostedAsync(scenario, scale, outcome, root, cancellationToken);
            var heapDisposed = SoakProbe.SettledHeap();
            var handlesDisposed = SoakProbe.OpenHandles();
            var sqliteDisposed = SoakProbe.HandlesUnder(root);
            using var process = Process.GetCurrentProcess();

            return (measured with { HeapAfterDisposeBytes = heapDisposed, HandlesAfterDispose = handlesDisposed, HandlesBaseline = handlesBaseline, SqliteHandlesAfterDispose = Math.Max(0, sqliteDisposed), ProcessPeakWorkingSetBytes = process.PeakWorkingSet64 }, outcome);
        }
        finally
        {
            TestTempDirectoryCleanup.TryDeleteDirectoryRecursively(root);
        }
    }

    private static async Task<SoakPass> RunHostedAsync(SoakScenario scenario, SoakScale scale, SoakOutcome outcome, string root, CancellationToken cancellationToken)
    {
        var hostWatch = Stopwatch.StartNew();
        var host = await SoakHost.StartAsync(root, scale.FullDurability);
        hostWatch.Stop();

        try
        {
            var heapBefore = SoakProbe.SettledHeap();
            var handlesBefore = SoakProbe.OpenHandles();
            var sqliteBefore = SoakProbe.HandlesUnder(root);
            var databaseBefore = SoakProbe.FileBytes(host.DatabasePath) + SoakProbe.FileBytes(host.DatabasePath + "-wal");
            var gen2Before = GC.CollectionCount(2);
            var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            using var sampler = new SoakSampler();
            var watch = Stopwatch.StartNew();

            try
            {
                await scenario.Run(host, scale, outcome, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A crash is a finding like any other: it is reported with its stack, and the pass's measurements are still taken.
                outcome.Expect(false, () => $"CRASH: the scenario threw {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }

            watch.Stop();
            sampler.Stop();
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
            var gen2 = GC.CollectionCount(2) - gen2Before;
            var heapAfter = SoakProbe.SettledHeap();
            var handlesAfter = SoakProbe.OpenHandles();
            var sqliteAfter = SoakProbe.HandlesUnder(root);
            var db = SoakProbe.FileBytes(host.DatabasePath);
            var wal = SoakProbe.FileBytes(host.DatabasePath + "-wal");
            await host.DisposeAsync();
            var dbShutdown = SoakProbe.FileBytes(host.DatabasePath);

            return new SoakPass(
                watch.Elapsed.TotalSeconds, hostWatch.Elapsed.TotalSeconds, heapBefore, heapAfter, 0, sampler.PeakManagedBytes, sampler.PeakWorkingSetBytes,
                0, db, wal, dbShutdown, handlesBefore, handlesAfter, sqliteBefore, sqliteAfter, 0, 0, gen2, allocated, 0, databaseBefore);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }
}
