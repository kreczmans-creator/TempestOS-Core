using System.Diagnostics;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>One scenario: what it is called in the report and how it runs against a started host.</summary>
/// <param name="SummaryFacts">The keys of the facts the summary table shows for the scenario.</param>
/// <param name="TitleAt">The title with the numbers of the scale it runs at, when the title carries any (a run of a tenth of the numbers is not titled with the specification's); the plain <paramref name="Title"/> otherwise.</param>
internal sealed record SoakScenario(string Id, string Title, string Covers, IReadOnlyList<string> SummaryFacts, Func<SoakHost, SoakScale, SoakOutcome, CancellationToken, Task> Run, Func<SoakScale, string>? TitleAt = null)
{
    /// <summary>The scenario's title as it reads for a run at <paramref name="scale"/>: "500 calculation instances" in the full soak, "54 calculation instances" in the scaled-down one.</summary>
    public string TitleFor(SoakScale scale) => TitleAt?.Invoke(scale) ?? Title;
}

internal static partial class SoakScenarios
{
    /// <summary>The five scenarios of Addendum C, C-7, in the order the specification lists them, then the four that soak what the merged features (C-3 record import, C-6 demo project, C-4 dashboard, C-5 guided test) add.</summary>
    public static IReadOnlyList<SoakScenario> All { get; } =
    [
        new("1-instances", "500 calculation instances", "Create, run and save a calculation of every template, from its shipped worked example, in one project.", ["Calculations created, run and saved", "Templates covered", "Database growth per calculation (KB)"], InstancesAsync, s => $"{s.Instances:N0} calculation instances"),
        new("2-chains", "Chains of depth 20", "Link calculations 20 deep; change the head; every step goes stale; update in order; every step is current.", ["Calculations in chains", "Steps reported stale after the head changed", "Steps updated in order"], ChainsAsync, s => $"Chains of depth {s.ChainDepth}"),
        new("3-cycles", "100 revise and supersede cycles", "Revise, change, check, issue and supersede one calculation over and over; every issued revision stays byte-identical and refuses edits.", ["Revisions issued", "Edit attempts refused", "Database growth per issued revision (KB)"], CyclesAsync, s => $"{s.Cycles:N0} revise and supersede cycles"),
        new("4-monte-carlo", "1,000 Monte Carlo runs", "The Monte Carlo service (engine and through a calculator) at its default sample count, its caps, and a tornado sweep.", ["Engine runs (default samples each)", "Calculator evaluations", "Tornado sweeps"], MonteCarloAsync, s => $"{s.EngineMonteCarloRuns:N0} Monte Carlo runs"),
        new("5-imports", "Imports at their size caps", "Scan tables, CAD expression files and parameter CSVs, and reference records, each at its cap and one past it.", ["Largest scan table accepted (bytes)", "CAD parameters accepted", "Refusals one past a cap"], ImportsAsync, s => s.ImportFraction < 1 ? $"Imports at {s.ImportFraction * 100:0}% of their size caps (refusals at the caps)" : "Imports at their size caps"),
        new("6-record-import", "Record import at its caps", "Track P record templates imported at the 5 MB, 1,001-row, 64-column and 4,096-character caps, one past each cap refused, all or nothing, and the licence lock after many revisions.", ["Templates imported", "Records committed at the caps", "Refusals one past a cap"], RecordsAsync, s => s.ImportFraction < 1 ? $"Record import at {s.ImportFraction * 100:0}% of its row and byte caps (refusals at the caps)" : "Record import at its caps"),
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

    /// <summary>The scenario's title with the numbers of the scale it ran at.</summary>
    public string Title => Scenario.TitleFor(Scale);

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

    /// <summary>
    /// The growth of the settled managed heap that is always allowed within one host, between the halfway point of a scenario's items and its last item,
    /// whatever the number of items: the runtime's own caches and a pooled buffer or two settle by a few hundred kilobytes in a normal run.
    /// </summary>
    public const long InHostBaseBytes = 1L * 1024 * 1024;

    /// <summary>
    /// What each item of the second half may add to the settled heap within one host, on top of <see cref="InHostBaseBytes"/> (see <see cref="InHostBound"/>).
    /// The legitimate part of it is what the scenario keeps for each item to check it afterwards (an identifier, a hash, a few counts) and what the application
    /// keeps for each calculation it is asked about (an index entry, its Evidence object). Measured at a middle scale (about 300 calculations, 40 cycles, 400 engine
    /// runs, 40 licensed revisions, 8 demonstration cycles, 20 dashboard builds, 100 guided runs), the second half of the items added, per item: 3.7 KiB
    /// (calculations), 3.5 KiB (chain steps), 5.4 KiB (revise cycles, the largest: the fingerprint the scenario keeps of each issued revision), 2.4 KiB (demonstration
    /// calculations and packs, first pass only), and nothing measurable for the Monte Carlo runs, the licensed revisions, the dashboard builds and the guided runs.
    /// The factor is three times the largest of them. A leak the check exists for retains the thing the item made: a calculation record with its outputs and sheet
    /// text (19 KB stored, more in memory), a run with its results (22 KB), a connection's buffers: tens of kilobytes. So a retention of about 32 KiB an item (twice
    /// the factor, to clear the base allowance over a few hundred items) is always caught; one of 16 KiB or less an item is not, and the report says so.
    /// </summary>
    public const long InHostBytesPerItem = 16L * 1024;

    /// <summary>The fewest items in the second half for the in-host check to say anything: with fewer the base allowance is most of what could be seen.</summary>
    public const int InHostMinItems = 10;

    /// <summary>What the second half of a scenario's items may add to the settled heap within one host: <see cref="InHostBaseBytes"/> plus <see cref="InHostBytesPerItem"/> for each item.</summary>
    public static long InHostBound(int secondHalfItems) => InHostBaseBytes + InHostBytesPerItem * secondHalfItems;

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

        // Growth within one host: the check the passes cannot make, because a host that keeps everything it is given is disposed with the pass and leaves
        // nothing behind to compare. Raised only when every pass that could be measured grew by more than the bound in its second half: a leak does it each
        // time, a one-off step (a cache filled the first time a path is walked) does it once.
        var measured = passes.Where(p => p.InHost is { SecondHalfItems: >= InHostMinItems }).ToList();

        if (measured.Count > 0 && measured.All(p => p.InHost!.SecondHalfGrowth > InHostBound(p.InHost.SecondHalfItems)))
        {
            var last = measured[^1].InHost!;
            findings.Add($"LEAK: within one host the settled managed heap grew by {last.SecondHalfGrowth / (1024.0 * 1024):0.0} MiB over the second half of the work ({last.SecondHalfItems:N0} of {last.Items:N0} items) in {(measured.Count == 1 ? "the pass" : $"each of {measured.Count} passes")}; "
                + $"the most allowed is {InHostBound(last.SecondHalfItems) / (1024.0 * 1024):0.0} MiB ({InHostBaseBytes / (1024 * 1024)} MiB + {InHostBytesPerItem / 1024} KiB an item). Something the host or the scenario keeps for each item is not released.");
        }

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
                0, db, wal, dbShutdown, handlesBefore, handlesAfter, sqliteBefore, sqliteAfter, 0, 0, gen2, allocated, 0, databaseBefore, outcome.InHost, outcome.ItemsTotal);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }
}
