using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>Where and when a soak ran, for the head of its report.</summary>
internal sealed record SoakReportContext(string Commit, string Branch, DateTimeOffset At, string Machine, double TotalMinutes);

/// <summary>
/// Writes the soak's measurements as the markdown report <c>docs/verification/soak.md</c>: a summary, then for each scenario what ran, what it
/// cost over two passes, and what the leak and stability checks found. The one hand-written part, the notes between the two markers, is
/// kept when the report is regenerated.
/// </summary>
internal static class SoakReport
{
    public const string NotesBegin = "<!-- soak:notes:begin -->";
    public const string NotesEnd = "<!-- soak:notes:end -->";

    private static string Mib(long bytes) => (bytes / (1024.0 * 1024)).ToString("0.0", CultureInfo.InvariantCulture);

    private static string Num(double value, string format = "0.0") => value.ToString(format, CultureInfo.InvariantCulture);

    public static string Render(IReadOnlyList<SoakRun> runs, SoakScale scale, SoakReportContext context, string? notes)
    {
        var text = new StringBuilder();
        text.AppendLine("# Performance and stability soak");
        text.AppendLine();
        text.AppendLine("Addendum C, Track S, item C-7. Scripted runs of the real services over a real SQLite database, with time, memory and database size measured, and checks for leaks, stale-flag errors and immutability breaches. This page is written by the soak itself; see \"Running it again\" at the end.");
        text.AppendLine();
        text.AppendLine("| | |");
        text.AppendLine("|---|---|");
        text.AppendLine(CultureInfo.InvariantCulture, $"| Result | **{(runs.All(r => r.Passed) ? "PASS" : "FAIL")}**: {runs.Count(r => r.Passed)} of {runs.Count} scenarios clean |");
        text.AppendLine(CultureInfo.InvariantCulture, $"| Mode | {scale.Name} (`{SoakScale.EnvironmentVariable}=full` runs the specification's numbers) |");
        text.AppendLine(CultureInfo.InvariantCulture, $"| Date | {context.At:yyyy-MM-dd HH:mm} UTC |");
        text.AppendLine(CultureInfo.InvariantCulture, $"| Commit | `{context.Commit}` on `{context.Branch}` |");
        text.AppendLine(CultureInfo.InvariantCulture, $"| Machine | {context.Machine} |");
        text.AppendLine(CultureInfo.InvariantCulture, $"| Database | SQLite in WAL mode in a temporary folder, `PRAGMA synchronous = {(scale.FullDurability ? "FULL (the product's setting)" : "NORMAL (the test suite's setting)")}`, a fresh database for every pass |");
        text.AppendLine(CultureInfo.InvariantCulture, $"| Composition | `WorkspaceHost` (the Desktop's own composition, headless): Evidence service, PDF sheet renderer, all 54 calculators, `CalcInstanceService` |");
        text.AppendLine(CultureInfo.InvariantCulture, $"| Duration | {Num(context.TotalMinutes)} minutes for all five scenarios, each run twice |");
        text.AppendLine();

        text.AppendLine("## Summary");
        text.AppendLine();
        text.AppendLine("Pass 1 / pass 2 are the same scenario run twice in one process, each over a fresh host and database (a third pass is run only to tell a one-off step in the heap from a leak).");
        text.AppendLine();
        text.AppendLine("| Scenario | What ran | Time, s (pass 1 / 2) | Peak working set, MiB | Peak managed, MiB | DB + WAL, MiB | Heap left after disposal, last pass vs pass 1, MiB | Verdict |");
        text.AppendLine("|---|---|---|---|---|---|---|---|");

        foreach (var run in runs)
        {
            var a = run.Passes[0];
            var b = run.Passes[1];
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {run.Scenario.Title} | {Short(run)} | {Num(a.Seconds)} / {Num(b.Seconds)} | {Mib(run.Passes.Max(p => p.PeakWorkingSetBytes))} | {Mib(run.Passes.Max(p => p.PeakManagedBytes))} | {Mib(run.Passes.Max(p => p.DbBytes + p.WalBytes))} | {Mib(run.HeapGrowthBytes)} | {(run.Passed ? "clean" : "**FAILED**")} |");
        }

        text.AppendLine();

        foreach (var run in runs)
            RenderScenario(text, run);

        text.AppendLine("## Method");
        text.AppendLine();
        text.AppendLine("- **Time** is the wall-clock time of the scenario itself on a started host; the host's start-up is measured separately and shown per scenario.");
        text.AppendLine("- **Managed heap** is `GC.GetTotalMemory` after three forced, compacting collections and a wait for finalisers: once before the scenario, once after it with the host still running (what the application holds for that data), and once after the host is disposed (what is left that nothing owns).");
        text.AppendLine("- **Peak working set and peak managed heap** are the highest values a sampler thread saw (every 100 ms) while the scenario ran, not the values at its end. The process's own peak working set is also shown (it includes every pass before it).");
        text.AppendLine("- **Database size** is `tempest.db` and its write-ahead log at the end of the scenario with the host still running, and `tempest.db` again once the host is shut down and the log has been folded in.");
        text.AppendLine("- **Handles** are the process's open file descriptors (`/proc/self/fd`), and, separately, those open on the database's own files, which is the footprint of the pooled SQLite connections. The process's handles after the second pass's host is disposed are compared with those after the first's (the first pass also opens what loading the application's assemblies opens, once); the database's handles must not grow during the scenario and must be none once the host is disposed.");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Leak check.** Each scenario runs twice in the same process. The managed heap left after the second pass's host is disposed may not exceed the first pass's by more than {SoakRunner.HeapGrowthBoundBytes / (1024 * 1024)} MiB (a leak worth the name is tens of kilobytes per item over hundreds of items, many times that, and keeps growing). If it does, a third pass is run: the heap is a leak only if it is still rising by more than half the bound; a one-off step (buffers the runtime keeps for a while after a large file is read) that then stays flat is reported as such and is not. The process may end the second pass with no more than {SoakRunner.HandleSlack} more handles than the first, the database's handles may not grow by more than {SoakRunner.HandleSlack} during a scenario and must be none after the host is disposed, and the second pass may not take more than {SoakRunner.SlowdownFactor:0.#} times the first plus five seconds.");
        text.AppendLine("- **Stale-flag errors** are both kinds: a calculation reported stale when nothing it depends on changed, and one not reported stale when something did.");
        text.AppendLine("- **Immutability breaches** are any change to an issued revision after its issue: its stored record (with only its status and time set aside, the two fields supersession changes), its outputs, its sheet text, its PDF sheet in the content store, or its Evidence; and any attempt to edit one that is not refused.");
        text.AppendLine();
        text.AppendLine("### How the specification's numbers were read");
        text.AppendLine();
        text.AppendLine("- **500 calc instances**: 500 calculations created round the 54 templates from their shipped worked examples, each run again and saved again, in one project.");
        text.AppendLine("- **Chains of depth 20**: chains of 20 links (21 calculations) of the ISO 286 fit calculator, each the nominal size of the next; a link past the depth cap is refused. Two chains share a project so that one chain's change can be seen to leave the other fresh.");
        text.AppendLine("- **100 revise/supersede cycles**: 100 times over one calculation: revise (the issued revision becomes Superseded), change an input, check by someone else, issue with its PDF sheet; 101 revisions in all.");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **1,000 Monte Carlo runs**: 1,000 runs of the Monte Carlo service, each at its default sample count ({Tempest.Calc.Engine.Analysis.MonteCarlo.DefaultSamples:N0}), which is inside the service's own cap ({Tempest.Calc.Engine.Analysis.MonteCarlo.MaxSamples:N0} for the engine). The service over a whole calculator re-runs the calculator once per sample and has a lower cap ({Tempest.Calc.Sheets.CalcRunner.MaxCalculatorSamples:N0}); it is run {scale.CalculatorMonteCarloRuns:N0} times at {scale.CalculatorMonteCarloSamples:N0} samples each (where that is fewer than the default, the reason is time, and is stated in the notes). 1,000 tornado sweeps are run as well.");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Imports of max-size files**: scan deviation tables at the 5 MB byte cap and at the one-million-row cap, a CAD expression file at the 5 MB cap with {Tempest.Calc.Import.ImportLimits.Default.MaxParameters:N0} parameters (the parameter cap), a CAD parameter CSV at the parameter cap, and a reference record at every one of its caps; then one past each cap, which must be refused with a plain sentence and leave the calculation byte for byte as it was. Record import from CSV (Addendum C, C-3) is not in this build, so it is not soaked.");
        text.AppendLine();

        text.AppendLine("## Notes");
        text.AppendLine();
        text.AppendLine(NotesBegin);
        text.AppendLine(notes is { Length: > 0 } ? notes.Trim() : "_No notes were written for this run._");
        text.AppendLine(NotesEnd);
        text.AppendLine();
        text.AppendLine("## Running it again");
        text.AppendLine();
        text.AppendLine("```");
        text.AppendLine("# the scaled-down soak (about a tenth), part of the ordinary Desktop test run:");
        text.AppendLine("dotnet test tests/Tempest.Desktop.Tests --filter \"FullyQualifiedName~Tempest.Desktop.Tests.Soak\"");
        text.AppendLine();
        text.AppendLine("# the full soak, which rewrites this page (about half an hour):");
        text.AppendLine("TEMPEST_SOAK=full dotnet test tests/Tempest.Desktop.Tests --filter \"FullyQualifiedName~Tempest.Desktop.Tests.Soak.SoakFull\"");
        text.AppendLine("```");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"`{SoakScale.ProgressVariable}=<file>` appends one line per step to a file, to watch a long run.");
        return text.ToString();
    }

    private static string Short(SoakRun run) =>
        string.Join("; ", run.Outcomes[0].Facts.Take(2).Select(f => $"{f.Key}: {f.Value}"));

    /// <summary>One scenario's section on its own (for the scaled-down run, which does not write the report but can show what it measured).</summary>
    public static string RenderSection(SoakRun run)
    {
        var text = new StringBuilder();
        RenderScenario(text, run);
        return text.ToString();
    }

    private static void RenderScenario(StringBuilder text, SoakRun run)
    {
        var passes = run.Passes;
        text.AppendLine(CultureInfo.InvariantCulture, $"## {run.Scenario.Title}");
        text.AppendLine();
        text.AppendLine(run.Scenario.Covers);
        text.AppendLine();
        text.AppendLine("What ran and what it found (pass 1):");
        text.AppendLine();
        text.AppendLine("| | |");
        text.AppendLine("|---|---|");

        foreach (var (key, value) in run.Outcomes[0].Facts)
            text.AppendLine(CultureInfo.InvariantCulture, $"| {Escape(key)} | {Escape(value)} |");

        text.AppendLine(CultureInfo.InvariantCulture, $"| Expectations checked | {string.Join(", ", run.Outcomes.Select((o, i) => $"{o.Checks:N0} (pass {i + 1})"))} |");
        text.AppendLine();
        text.AppendLine("Measurements:");
        text.AppendLine();
        text.AppendLine($"| | {string.Join(" | ", passes.Select((_, i) => $"Pass {i + 1}"))} |");
        text.AppendLine($"|---|{string.Concat(passes.Select(_ => "---|"))}");

        void Row(string label, Func<SoakPass, string> value) =>
            text.AppendLine(CultureInfo.InvariantCulture, $"| {label} | {string.Join(" | ", passes.Select(value))} |");

        Row("Scenario time, s", p => Num(p.Seconds));
        Row("Host start-up, s", p => Num(p.HostStartSeconds));
        Row("Managed heap before, MiB (host running)", p => Mib(p.HeapBeforeBytes));
        Row("Managed heap after, MiB (host running)", p => Mib(p.HeapAfterBytes));
        Row("Held for the work, MiB", p => Mib(p.HeapAfterBytes - p.HeapBeforeBytes));
        Row("Managed heap after the host is disposed, MiB", p => Mib(p.HeapAfterDisposeBytes));
        Row("Peak managed heap while running, MiB", p => Mib(p.PeakManagedBytes));
        Row("Peak working set while running, MiB", p => Mib(p.PeakWorkingSetBytes));
        Row("Process peak working set so far, MiB", p => Mib(p.ProcessPeakWorkingSetBytes));
        Row("tempest.db, MiB", p => Mib(p.DbBytes));
        Row("tempest.db-wal, MiB", p => Mib(p.WalBytes));
        Row("tempest.db after shutdown, MiB", p => Mib(p.DbBytesAfterShutdown));
        Row("Open handles: before the host / before the scenario / after / after dispose", p => $"{p.HandlesBaseline} / {p.HandlesBefore} / {p.HandlesAfter} / {p.HandlesAfterDispose}");
        Row("Handles on the database files: before / after / after dispose", p => $"{p.SqliteHandlesBefore} / {p.SqliteHandlesAfter} / {p.SqliteHandlesAfterDispose}");
        Row("Generation 2 collections", p => p.Gen2Collections.ToString(CultureInfo.InvariantCulture));
        Row("Allocated, GiB", p => Num(p.AllocatedBytes / (1024.0 * 1024 * 1024), "0.00"));
        text.AppendLine();

        var growth = Mib(run.HeapGrowthBytes);
        var verdict = run.LeakFindings.Count == 0 ? "no leak, slowdown or handle growth found" : $"{run.LeakFindings.Count} finding(s)";
        text.AppendLine(CultureInfo.InvariantCulture, $"Leak check: the heap left after disposal was **{growth} MiB** higher after pass {passes.Count} than after pass 1 (bound {Mib(SoakRunner.HeapGrowthBoundBytes)} MiB); {verdict}.");

        if (passes.Count > 2)
        {
            var step = Mib(passes[1].HeapAfterDisposeBytes - passes[0].HeapAfterDisposeBytes);
            var rise = Mib(passes[^1].HeapAfterDisposeBytes - passes[^2].HeapAfterDisposeBytes);
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"A third pass was run because pass 2 left the heap {step} MiB above pass 1. Pass 3 left it {rise} MiB above pass 2, so the heap {(run.LeakFindings.Any(f => f.Contains("managed heap", StringComparison.Ordinal)) ? "**kept growing**" : "did not keep growing: a one-off step (buffers the runtime keeps for a while), not a leak")}.");
        }

        text.AppendLine();

        if (run.Failures.Any())
        {
            text.AppendLine("Findings:");
            text.AppendLine();

            foreach (var failure in run.Failures.Take(20))
                text.AppendLine(CultureInfo.InvariantCulture, $"- {Escape(failure.Split('\n')[0])}");

            text.AppendLine();
        }
        else
        {
            text.AppendLine("No failed expectation, no stale-flag error, no immutability breach, no leak.");
            text.AppendLine();
        }
    }

    private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    /// <summary>The hand-written notes of an existing report, or <see langword="null"/>.</summary>
    public static string? ExistingNotes(string path)
    {
        if (!File.Exists(path))
            return null;

        var text = File.ReadAllText(path);
        var begin = text.IndexOf(NotesBegin, StringComparison.Ordinal);
        var end = text.IndexOf(NotesEnd, StringComparison.Ordinal);

        if (begin < 0 || end < begin)
            return null;

        var notes = text[(begin + NotesBegin.Length)..end].Trim();
        return notes.StartsWith("_No notes were written", StringComparison.Ordinal) ? null : notes;
    }

    /// <summary>The commit and branch the soak ran on, from git; "unknown" where git is not available.</summary>
    public static (string Commit, string Branch) GitState(string repositoryRoot)
    {
        static string? Run(string root, string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
                var output = process?.StandardOutput.ReadToEnd().Trim();
                process?.WaitForExit(10_000);
                return process?.ExitCode == 0 ? output : null;
            }
            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return null;
            }
        }

        var commit = Run(repositoryRoot, "rev-parse --short=10 HEAD") ?? "unknown";
        var branch = Run(repositoryRoot, "rev-parse --abbrev-ref HEAD") ?? "unknown";
        var dirty = Run(repositoryRoot, "status --porcelain --untracked-files=no") is { Length: > 0 };
        return (dirty ? commit + " (with uncommitted changes)" : commit, branch);
    }
}
