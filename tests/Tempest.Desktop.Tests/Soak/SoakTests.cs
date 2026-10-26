using System.Diagnostics;
using Tempest.Calc.Import;
using Tempest.Calc.Library;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>
/// Skipped unless <c>TEMPEST_SOAK=full</c> is set: the full soak (the specification's numbers, about 20 minutes) is not part of an ordinary test run.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SoakFullFactAttribute : FactAttribute
{
    public SoakFullFactAttribute()
    {
        if (!SoakScale.FullRequested)
            Skip = $"Set {SoakScale.EnvironmentVariable}=full to run the full performance and stability soak (about 20 minutes); it rewrites docs/verification/soak.md.";
    }
}

/// <summary>
/// Addendum C, C-7: the scaled-down soak, always run. Each test runs one scenario twice over the real composition and a real database, at about a
/// tenth of the specification's numbers (the caps' refusals at the real caps), and asserts the scenario's own expectations (correctness, no stale
/// flag wrong, no immutability breach, caps enforced) and the leak check, with loose time and memory bounds.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SoakCiTests
{
    /// <summary>One pass of any scenario at CI scale takes seconds to a minute on a quiet machine; this is for a loaded one.</summary>
    private const double PassSecondsBound = 300;

    private const long WorkingSetBoundBytes = 3L * 1024 * 1024 * 1024;

    private const long DatabaseBoundBytes = 512L * 1024 * 1024;

    private static async Task RunAsync(int index)
    {
        var run = await SoakRunner.RunAsync(SoakScenarios.All[index], SoakScale.Ci);

        // TEMPEST_SOAK_DUMP=<file> appends what each scaled-down scenario measured, for anyone who wants to look at it.
        if (Environment.GetEnvironmentVariable("TEMPEST_SOAK_DUMP") is { Length: > 0 } dump)
            File.AppendAllText(dump, SoakReport.RenderSection(run) + Environment.NewLine);

        Assert.True(run.Passed, $"{run.Scenario.Title}:{Environment.NewLine}{string.Join(Environment.NewLine, run.Failures.Take(15))}");
        Assert.All(run.Passes, p =>
        {
            Assert.True(p.Seconds < PassSecondsBound, $"{run.Scenario.Title}: a pass took {p.Seconds:0.0} s (bound {PassSecondsBound} s).");
            Assert.True(p.PeakWorkingSetBytes < WorkingSetBoundBytes, $"{run.Scenario.Title}: the working set reached {p.PeakWorkingSetBytes / (1024.0 * 1024):0} MiB.");
            Assert.True(p.DbBytes + p.WalBytes < DatabaseBoundBytes, $"{run.Scenario.Title}: the database grew to {(p.DbBytes + p.WalBytes) / (1024.0 * 1024):0} MiB.");
        });
    }

    [Fact]
    public Task Instances_Of_Every_Template_Are_Created_Run_And_Saved_Without_A_Leak() => RunAsync(0);

    [Fact]
    public Task Chains_Twenty_Deep_Go_Stale_From_The_Head_And_Update_In_Order() => RunAsync(1);

    [Fact]
    public Task Revise_And_Supersede_Cycles_Leave_Every_Issued_Revision_Byte_Identical() => RunAsync(2);

    [Fact]
    public Task Monte_Carlo_And_Sensitivity_Runs_Are_Correct_And_Capped() => RunAsync(3);

    [Fact]
    public Task Imports_At_Their_Caps_Are_Read_And_One_Past_Them_Are_Refused_Cleanly() => RunAsync(4);

    [Fact]
    public Task Record_Imports_At_Their_Caps_Commit_All_Or_Nothing_And_The_Licence_Lock_Holds_After_Many_Revisions() => RunAsync(5);

    [Fact]
    public Task The_Demo_Project_Installs_And_Removes_Over_And_Over_Leaving_A_Real_Project_Byte_Identical() => RunAsync(6);

    [Fact]
    public Task The_Dashboard_Built_Repeatedly_Over_Many_Records_And_Runs_Gives_The_Same_Rows_And_Writes_Nothing() => RunAsync(7);

    [Fact]
    public Task Guided_Runs_Are_Made_To_The_Run_Cap_And_The_Older_Ones_Deleted_Without_Changing_A_Calculation() => RunAsync(8);
}

/// <summary>Addendum C, C-7: the full soak. Runs only with <c>TEMPEST_SOAK=full</c>, and writes <c>docs/verification/soak.md</c>.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SoakFullTests
{
    [SoakFullFact]
    public async Task The_Full_Soak_Runs_The_Specifications_Numbers_And_Writes_The_Report()
    {
        var scale = SoakScale.Full;
        var total = Stopwatch.StartNew();
        var runs = new List<SoakRun>();

        // TEMPEST_SOAK_ONLY=6,8 runs only the scenarios whose number is listed, at full scale, and writes TEMPEST_SOAK_REPORT (never docs/verification/soak.md,
        // which is the report of the whole soak): for looking at one scenario's full-scale numbers without the 20 minutes of the others.
        var only = Environment.GetEnvironmentVariable("TEMPEST_SOAK_ONLY")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var scenario in SoakScenarios.All.Where(s => only is null || only.Contains(s.Id.Split('-')[0], StringComparer.Ordinal)))
            runs.Add(await SoakRunner.RunAsync(scenario, scale));

        total.Stop();
        var path = only is null
            ? Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "verification", "soak.md")
            : Environment.GetEnvironmentVariable("TEMPEST_SOAK_REPORT") ?? Path.Combine(Path.GetTempPath(), "soak-partial.md");
        var (commit, branch) = SoakReport.GitState(DesktopTestHelpers.RepositoryRoot);
        var context = new SoakReportContext(commit, branch, DateTimeOffset.UtcNow, SoakProbe.MachineDescription(), total.Elapsed.TotalMinutes);
        File.WriteAllText(path, SoakReport.Render(runs, scale, context, SoakReport.ExistingNotes(path)));

        var failures = runs.SelectMany(r => r.Failures.Select(f => $"{r.Scenario.Title}: {f}")).ToList();
        Assert.True(failures.Count == 0, $"The soak found {failures.Count} problem(s); see docs/verification/soak.md:{Environment.NewLine}{string.Join(Environment.NewLine, failures.Take(15))}");
    }
}

/// <summary>The soak's own machinery: its leak check can fail, its files sit exactly at the caps, and its report keeps what a person wrote in it.</summary>
public sealed class SoakHarnessTests
{
    private static SoakPass Pass(double seconds = 10, long heapAfterDispose = 100, long heapBefore = 100, long heapAfter = 200, int handlesBaseline = 20, int handlesAfterDispose = 20, int sqliteBefore = 2, int sqliteAfter = 2, int sqliteAfterDispose = 0) =>
        new(seconds, 5, heapBefore, heapAfter, heapAfterDispose, 300, 400, 500, 10, 1, 10, 25, 25, sqliteBefore, sqliteAfter, handlesAfterDispose, handlesBaseline, 3, 1000, sqliteAfterDispose);

    [Fact]
    public void A_Heap_That_Is_Flat_From_One_Pass_To_The_Next_Is_Clean()
    {
        var findings = SoakRunner.Evaluate([Pass(heapAfterDispose: 50_000_000), Pass(heapAfterDispose: 50_300_000)]);
        Assert.Empty(findings);
    }

    [Fact]
    public void A_Heap_That_Grows_More_Than_The_Bound_Between_Passes_Is_A_Leak()
    {
        var findings = SoakRunner.Evaluate([Pass(heapAfterDispose: 50_000_000), Pass(heapAfterDispose: 50_000_000 + SoakRunner.HeapGrowthBoundBytes + 1)]);
        Assert.Contains(findings, f => f.StartsWith("LEAK: the managed heap left after the host was disposed grew", StringComparison.Ordinal));
    }

    [Fact]
    public void A_One_Off_Step_Up_That_Does_Not_Keep_Growing_Is_Not_A_Leak_But_A_Heap_That_Keeps_Rising_Is()
    {
        const long mib = 1024 * 1024;
        Assert.True(SoakRunner.HeapSteppedUp([Pass(heapAfterDispose: 40 * mib), Pass(heapAfterDispose: 50 * mib)], SoakRunner.HeapGrowthBoundBytes));
        Assert.False(SoakRunner.HeapSteppedUp([Pass(heapAfterDispose: 40 * mib), Pass(heapAfterDispose: 41 * mib)], SoakRunner.HeapGrowthBoundBytes));

        // 40 → 50 → 50: a buffer kept once, then flat.
        Assert.Empty(SoakRunner.Evaluate([Pass(heapAfterDispose: 40 * mib), Pass(heapAfterDispose: 50 * mib), Pass(heapAfterDispose: 50 * mib)]));

        // 40 → 50 → 60: still rising.
        Assert.Contains(SoakRunner.Evaluate([Pass(heapAfterDispose: 40 * mib), Pass(heapAfterDispose: 50 * mib), Pass(heapAfterDispose: 60 * mib)]), f => f.Contains("still rising", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Pass_That_Holds_More_For_The_Same_Work_Is_A_Leak()
    {
        var findings = SoakRunner.Evaluate([Pass(heapBefore: 100_000_000, heapAfter: 150_000_000), Pass(heapBefore: 100_000_000, heapAfter: 150_000_000 + SoakRunner.HeapGrowthBoundBytes + 50_000_000)]);
        Assert.Contains(findings, f => f.Contains("held", StringComparison.Ordinal));
    }

    [Fact]
    public void Handles_That_Are_Not_Returned_Are_Reported_Whether_Process_Wide_Or_On_The_Database()
    {
        Assert.Contains(SoakRunner.Evaluate([Pass(), Pass(handlesAfterDispose: 20 + SoakRunner.HandleSlack + 1)]), f => f.Contains("open handles", StringComparison.Ordinal));
        Assert.Contains(SoakRunner.Evaluate([Pass(), Pass(sqliteAfter: 2 + SoakRunner.HandleSlack + 1)]), f => f.Contains("SQLite connections", StringComparison.Ordinal));
        Assert.Contains(SoakRunner.Evaluate([Pass(), Pass(sqliteAfterDispose: 1)]), f => f.Contains("after its host was disposed", StringComparison.Ordinal));
        Assert.Empty(SoakRunner.Evaluate([Pass(handlesAfterDispose: 20), Pass(handlesAfterDispose: 20 + SoakRunner.HandleSlack, sqliteAfter: 2 + SoakRunner.HandleSlack)]));

        // Warm-up: the first pass may end with many more handles than the process began with (assemblies, libraries) without that being a leak.
        Assert.Empty(SoakRunner.Evaluate([Pass(handlesBaseline: 20, handlesAfterDispose: 90), Pass(handlesBaseline: 90, handlesAfterDispose: 90)]));
    }

    [Fact]
    public void A_Second_Pass_That_Is_Much_Slower_Is_Reported()
    {
        Assert.Contains(SoakRunner.Evaluate([Pass(seconds: 10), Pass(seconds: 10 * SoakRunner.SlowdownFactor + 5.1)]), f => f.StartsWith("SLOWDOWN", StringComparison.Ordinal));
        Assert.Empty(SoakRunner.Evaluate([Pass(seconds: 10), Pass(seconds: 10 * SoakRunner.SlowdownFactor + 4.9)]));
    }

    [Fact]
    public void An_Outcome_Keeps_Every_Failure_Up_To_Its_Cap_And_Counts_The_Rest()
    {
        var outcome = new SoakOutcome();

        for (var i = 0; i < SoakOutcome.MaxKeptFailures + 10; i++)
            outcome.Expect(false, $"failure {i}");

        outcome.Expect(true, "holds");
        Assert.Equal(SoakOutcome.MaxKeptFailures + 10, outcome.FailureCount);
        Assert.Equal(SoakOutcome.MaxKeptFailures, outcome.Failures.Count);
        Assert.Equal(SoakOutcome.MaxKeptFailures + 11, outcome.Checks);
    }

    [Fact]
    public void The_Generated_Import_Files_Sit_Exactly_At_The_Caps_They_Are_Built_For()
    {
        var cap = ImportLimits.DefaultMaxBytes;
        var (table, rows) = SoakImportFiles.ScanTable("Housing bore depth", cap);
        Assert.Equal(cap, table.Length);
        Assert.InRange(rows, 100_000, 200_000);
        Assert.Equal(cap + 1, SoakImportFiles.ScanTable("Housing bore depth", cap + 1).Bytes.Length);

        var scanRows = SoakImportFiles.ScanRows(ImportLimits.Scan.MaxRows);
        Assert.Equal(4 + 4L * (ImportLimits.Scan.MaxRows - 1), scanRows.Length);
        Assert.True(scanRows.Length < cap, "A table at the row cap must also be under the byte cap, or the row cap could never be reached.");
        Assert.Equal(ImportLimits.Scan.MaxRows, scanRows.Count(b => b == (byte)'\n'));

        var nx = SoakImportFiles.NxExpressions(ImportLimits.Default.MaxParameters, cap);
        Assert.Equal(cap, nx.Length);
        Assert.Equal(ImportLimits.Default.MaxParameters, System.Text.Encoding.UTF8.GetString(nx).Split('\n').Count(l => l.StartsWith("[mm]", StringComparison.Ordinal)));
        Assert.Equal(cap + 1, SoakImportFiles.NxExpressions(10, cap + 1).Length);

        var csv = System.Text.Encoding.UTF8.GetString(SoakImportFiles.GenericCsv(ImportLimits.Default.MaxParameters)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(ImportLimits.Default.MaxParameters + 1, csv.Length);
    }

    [Fact]
    public void The_Generated_Record_Files_Sit_Exactly_At_The_Caps_They_Are_Built_For()
    {
        var sample = SoakRecordFiles.Samples.Single(s => s.Id == "24");
        var pad = SoakRecordFiles.ColumnCap - sample.Template.Columns.Count;
        var atCaps = SoakRecordFiles.Build(sample, SoakRecordFiles.RowCap - 1, pad, SoakRecordFiles.FieldCap, SoakRecordFiles.ByteCap);
        Assert.Equal(SoakRecordFiles.ByteCap, atCaps.Length);

        var text = System.Text.Encoding.UTF8.GetString(atCaps);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(SoakRecordFiles.RowCap, lines.Length);
        Assert.All(lines, l => Assert.Equal(SoakRecordFiles.ColumnCap, l.Split(',').Length));
        Assert.Equal(SoakRecordFiles.FieldCap, lines.SelectMany(l => l.Split(',')).Max(f => f.Length));

        // One past each cap: the builders move exactly one thing.
        Assert.Equal(SoakRecordFiles.ByteCap + 1, SoakRecordFiles.Build(sample, SoakRecordFiles.RowCap - 1, pad, SoakRecordFiles.FieldCap, SoakRecordFiles.ByteCap + 1).Length);
        Assert.Equal(SoakRecordFiles.RowCap + 1, System.Text.Encoding.UTF8.GetString(SoakRecordFiles.Build(sample, SoakRecordFiles.RowCap)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Throws<InvalidOperationException>(() => SoakRecordFiles.Build(sample, 10, pad, SoakRecordFiles.FieldCap, SoakRecordFiles.ByteCap));

        // Every sample's valid rows are valid for the template's own width, and the padding is named by the column map.
        Assert.All(SoakRecordFiles.Samples, s => Assert.Equal(s.Template.Columns.Count, s.Row(0).Length));
        Assert.Equal(pad, SoakRecordFiles.PadMap(pad).Count);
        Assert.All(SoakRecordFiles.PadMap(pad).Values, v => Assert.Null(v));
    }

    [Fact]
    public void The_Real_Project_Fingerprint_Sees_A_Changed_A_Missing_And_An_Added_Document()
    {
        var before = new SoakScenarios.RealFingerprint(new Dictionary<string, string?> { ["a"] = "1", ["b"] = "2" });
        Assert.Empty(before.Differences(new SoakScenarios.RealFingerprint(new Dictionary<string, string?> { ["a"] = "1", ["b"] = "2" })));
        Assert.Equal(["b"], before.Differences(new SoakScenarios.RealFingerprint(new Dictionary<string, string?> { ["a"] = "1", ["b"] = "3" })));
        Assert.Equal(["b"], before.Differences(new SoakScenarios.RealFingerprint(new Dictionary<string, string?> { ["a"] = "1" })));
        Assert.Equal(["c"], before.Differences(new SoakScenarios.RealFingerprint(new Dictionary<string, string?> { ["a"] = "1", ["b"] = "2", ["c"] = "3" })));
    }

    [Fact]
    public void The_Report_Names_Every_Scenario_Its_Numbers_And_Keeps_The_Notes_A_Person_Wrote()
    {
        var outcome = new SoakOutcome();
        outcome.Fact("Calculations created, run and saved", 500L);
        outcome.Expect(false, "a finding");
        var scenario = SoakScenarios.All[0];
        var run = new SoakRun { Scenario = scenario, Scale = SoakScale.Full, Passes = [Pass(), Pass()], Outcomes = [outcome, outcome], LeakFindings = [] };
        var context = new SoakReportContext("abc123", "branch", new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero), "a machine", 12.5);

        var text = SoakReport.Render([run], SoakScale.Full, context, "A note written by a person.");
        Assert.Contains("# Performance and stability soak", text, StringComparison.Ordinal);
        Assert.Contains(scenario.Title, text, StringComparison.Ordinal);
        Assert.Contains("Calculations created, run and saved | 500", text, StringComparison.Ordinal);
        Assert.Contains("`abc123` on `branch`", text, StringComparison.Ordinal);
        Assert.Contains("FAILED", text, StringComparison.Ordinal);
        Assert.Contains("a finding", text, StringComparison.Ordinal);
        Assert.Contains("PRAGMA synchronous = FULL", text, StringComparison.Ordinal);

        var path = Path.Combine(Path.GetTempPath(), $"soak-report-{Guid.NewGuid():N}.md");

        try
        {
            File.WriteAllText(path, text);
            Assert.Equal("A note written by a person.", SoakReport.ExistingNotes(path));
            Assert.Null(SoakReport.ExistingNotes(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Every_Scenario_Of_The_Specification_Is_Present_In_Order()
    {
        Assert.Equal(["1-instances", "2-chains", "3-cycles", "4-monte-carlo", "5-imports", "6-record-import", "7-demo-project", "8-dashboard", "9-guided-runs"], SoakScenarios.All.Select(s => s.Id));
        Assert.Equal(500, SoakScale.Full.Instances);
        Assert.Equal(20, SoakScale.Full.ChainDepth);
        Assert.Equal(100, SoakScale.Full.Cycles);
        Assert.Equal(1_000, SoakScale.Full.EngineMonteCarloRuns);
        Assert.Equal(1.0, SoakScale.Full.ImportFraction);

        // The merged features (C-3, C-6, C-4, C-5): the numbers the brief names.
        Assert.Equal(20, SoakScale.Full.DemoCycles);
        Assert.Equal(2, SoakScale.Ci.DemoCycles);
        Assert.Equal(Tempest.Calc.Library.ManualTesting.ManualTestRunService.DefaultMaxRuns, SoakScale.Full.GuidedRuns);
        Assert.True(SoakScale.Ci.GuidedRuns < SoakScale.Full.GuidedRuns);
        Assert.True(SoakScale.Ci.Instances >= 54, "The CI scale must still touch every template.");
        Assert.Equal(SoakScale.Full.ChainDepth, SoakScale.Ci.ChainDepth);
    }
}

/// <summary>The soak's host is the Desktop's own composition, at the durability each mode says it runs at.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SoakHostTests
{
    private static string SynchronousLevel(SoakHost host) =>
        (string)typeof(Tempest.Core.Persistence.SqlitePersistenceStore)
            .GetProperty("SynchronousLevel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(host.Store)!;

    [Theory]
    [InlineData(true, "FULL")]
    [InlineData(false, "NORMAL")]
    public async Task The_Database_Runs_At_The_Durability_The_Mode_Names(bool fullDurability, string expected)
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        await using var host = await SoakHost.StartAsync(root, fullDurability);

        Assert.Equal(expected, SynchronousLevel(host));
        Assert.IsType<Tempest.Core.Persistence.SqlitePersistenceStore>(host.Store);
        Assert.True(File.Exists(host.DatabasePath), "The soak runs over a real SQLite database file.");
        Assert.NotNull(host.Service.Catalogue.Find("struct.lifting"));
        Assert.Equal(54, host.Service.Catalogue.Current().Count);
    }

    [Fact]
    public async Task Issued_Revisions_Are_Byte_For_Byte_The_Same_After_The_Application_Is_Restarted()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var fingerprints = new List<IssuedFingerprint>();
        var before = new SoakOutcome();

        await using (var first = await SoakHost.StartAsync(root, fullDurability: true))
            await SoakScenarios.CyclesAsync(first, SoakScale.Ci with { Cycles = 4 }, before, fingerprints, CancellationToken.None);

        Assert.Empty(before.Failures);
        Assert.Equal(5, fingerprints.Count);

        // A new host over the same database: the records, outputs, sheet text, PDFs and Evidence are read back from the file, not from memory.
        await using var second = await SoakHost.StartAsync(root, fullDurability: true);
        var after = new SoakOutcome();

        for (var i = 0; i < fingerprints.Count; i++)
            await SoakScenarios.VerifyAsync(second, fingerprints[i], i == fingerprints.Count - 1 ? CalcInstanceStatus.Issued : CalcInstanceStatus.Superseded, after, "after a restart");

        Assert.Empty(after.Failures);
        Assert.True(after.Checks >= fingerprints.Count * 5, "Every revision is looked at five ways.");
    }

    [Fact]
    public async Task The_Database_Is_Counted_In_Its_Own_Pages_And_Gives_Pages_Back_When_Rows_Are_Deleted()
    {
        await using var host = await SoakHost.StartAsync(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath(), fullDurability: false);
        var (used, free) = SoakProbe.LogicalDatabase(host.DatabasePath);
        Assert.True(used > 1_000_000, "A seeded database holds more than a megabyte.");

        var project = await host.Host.ProjectDirectory!.CreateAsync("SOAK-T2", "Soak pages test");
        var made = new List<CalcInstance>();

        for (var i = 0; i < 10; i++)
            made.Add(await host.Service.CreateAsync("mech.iso286-fit", project.Id, $"Pages {i}", SoakExamples.For("mech.iso286-fit")[0].Inputs));

        var grown = SoakProbe.LogicalDatabase(host.DatabasePath);
        Assert.True(grown.UsedBytes > used, "Ten calculations take pages.");

        foreach (var instance in made)
            await host.Service.Store.DeleteDraftInstanceAsync(instance.Id);

        var shrunk = SoakProbe.LogicalDatabase(host.DatabasePath);
        Assert.True(shrunk.FreeBytes > free, "Deleting rows puts their pages on the free list.");
    }

    [Fact]
    public async Task The_Soak_Signs_In_Two_Different_People()
    {
        await using var host = await SoakHost.StartAsync(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath(), fullDurability: false);
        var project = await host.Host.ProjectDirectory!.CreateAsync("SOAK-T", "Soak host test");
        host.SignInAs(SoakHost.Author);
        var created = await host.Service.CreateAsync("mech.iso286-fit", project.Id, "Who", SoakExamples.For("mech.iso286-fit")[0].Inputs);
        Assert.Equal(SoakHost.Author, created.CreatedBy);

        host.SignInAs(SoakHost.Checker);
        var second = await host.Service.CreateAsync("mech.iso286-fit", project.Id, "Who else", SoakExamples.For("mech.iso286-fit")[0].Inputs);
        Assert.Equal(SoakHost.Checker, second.CreatedBy);
    }
}

/// <summary>The leak check run for real: a scenario that keeps what it allocates is caught by the runner, over a real host and two passes.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SoakLeakDetectorTests
{
    private static readonly List<byte[]> Kept = [];

    [Fact]
    public async Task A_Scenario_That_Leaks_Is_Caught_By_The_Runner_And_One_That_Does_Not_Is_Not()
    {
        var leaking = new SoakScenario("x-leak", "A leaking scenario", "Keeps 12 MB per pass in a static list.", [], (_, _, outcome, _) =>
        {
            Kept.Add(new byte[12 * 1024 * 1024]);
            outcome.Fact("Kept", Kept.Count);
            return Task.CompletedTask;
        });

        var clean = new SoakScenario("x-clean", "A clean scenario", "Allocates and drops 12 MB per pass.", [], (_, _, outcome, _) =>
        {
            var dropped = new byte[12 * 1024 * 1024];
            outcome.Fact("Dropped", dropped.Length);
            return Task.CompletedTask;
        });

        try
        {
            var caught = await SoakRunner.RunAsync(leaking, SoakScale.Ci);
            Assert.Contains(caught.LeakFindings, f => f.StartsWith("LEAK", StringComparison.Ordinal));
            Assert.False(caught.Passed);

            var fine = await SoakRunner.RunAsync(clean, SoakScale.Ci);
            Assert.True(fine.Passed, string.Join(Environment.NewLine, fine.Failures));
        }
        finally
        {
            Kept.Clear();
        }
    }
}
