using System.Text.Json.Nodes;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>The fixes made after the review of the Guided test's service layer (ADR-0175, "Changes after review"): identity, deletion, status, export.</summary>
public sealed class ManualTestRunServiceReviewTests
{
    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Tick() => Now = Now.AddMinutes(1);
    }

    private sealed class Tester(string id, string name)
    {
        public string Id { get; set; } = id;

        public string Name { get; set; } = name;
    }

    private static (ManualTestRunService Service, InMemoryQueryablePersistenceStore Store, Tester Tester) Make(int maxRuns = ManualTestRunService.DefaultMaxRuns, string scriptVersion = "9.9.9+aaaaaaaa", InMemoryQueryablePersistenceStore? store = null, Clock? clock = null)
    {
        store ??= new InMemoryQueryablePersistenceStore();
        clock ??= new Clock();
        var tester = new Tester("s.kreczman", "Steven Kreczman");
        return (new ManualTestRunService(store, GuidedTestHelpers.SmallScript(scriptVersion), () => "0.26.0 (1a2b3c4)", () => "1a2b3c4", () => (tester.Id, tester.Name), clock.Tick, maxRuns), store, tester);
    }

    // ---------------------------------------------------------------- L4: identity

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_Run_Cannot_Be_Started_With_Nobody_Signed_In_And_The_Refusal_Names_The_Sign_In(string id)
    {
        var (service, store, tester) = Make();
        tester.Id = id;
        tester.Name = string.Empty;

        var refusal = await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.StartAsync());

        Assert.Contains("signed in", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("sign in first", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(store.CommittedKeys(ManualTestRunService.Collection));
    }

    [Fact]
    public async Task A_Run_Left_With_No_Tester_Id_By_An_Earlier_Build_Is_Not_Continued_By_Anyone_And_Can_Only_Be_Finished()
    {
        var (service, store, tester) = Make();
        var run = await service.StartAsync();

        // What an earlier build wrote when nobody was signed in: the tester id is empty.
        var key = run.Id.ToString("N");
        var document = JsonNode.Parse(store.CommittedText(ManualTestRunService.Collection, key)!)!;
        document["TesterId"] = string.Empty;
        document["Tester"] = "unknown";
        await store.WriteAsync(ManualTestRunService.Collection, key, document.ToJsonString());

        var refusal = await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, "", null));
        Assert.Contains("nobody signed in", refusal.Message, StringComparison.Ordinal);

        tester.Id = string.Empty;
        refusal = await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, "", null));
        Assert.Contains("sign in first", refusal.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.FinishAsync(run.Id));

        // Someone signed in can close it (nothing is added), so that it can be cleared away.
        tester.Id = "a.n.other";
        Assert.True((await service.FinishAsync(run.Id)).IsFinished);
        Assert.Empty((await service.FindAsync(run.Id))!.Results);
    }

    [Fact]
    public async Task Nobody_Signed_In_Cannot_Record_Or_Finish_A_Run_That_Has_An_Id()
    {
        var (service, _, tester) = Make();
        var run = await service.StartAsync();
        tester.Id = string.Empty;

        await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, "", null));
        await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.FinishAsync(run.Id));
        await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.DeleteOlderFinishedAsync(1));
    }

    // ---------------------------------------------------------------- L11: deleting finished runs

    private static async Task<ManualTestRun> FinishedRunAsync(ManualTestRunService service, params (string Step, ManualTestOutcome Outcome)[] results)
    {
        var run = await service.StartAsync();

        foreach (var (step, outcome) in results)
            await service.RecordAsync(run.Id, step, outcome, "", null);

        return await service.FinishAsync(run.Id);
    }

    [Fact]
    public async Task Deleting_Older_Finished_Runs_Keeps_The_Newest_Ones_Every_Unfinished_Run_And_Every_Other_Collection()
    {
        var (service, store, _) = Make();
        var finished = new List<ManualTestRun>();

        for (var i = 0; i < 5; i++)
            finished.Add(await FinishedRunAsync(service, ("smoke-1", ManualTestOutcome.Pass)));

        var unfinished = await service.StartAsync();
        await store.WriteAsync("Calc.Instances", "a-calculation", "{\"keep\":true}");

        var plan = await service.PlanTrimAsync(keep: 2);
        Assert.Equal(5, plan.Finished);
        Assert.Equal(3, plan.Delete.Count);
        Assert.Equal(0, plan.KeptForStatus);

        var deleted = await service.DeleteOlderFinishedAsync(keep: 2);

        Assert.Equal(3, deleted);
        var left = (await service.ListAsync()).Select(r => r.Id).ToHashSet();
        Assert.Equal(new HashSet<Guid>([finished[3].Id, finished[4].Id, unfinished.Id]), left);
        Assert.Equal("{\"keep\":true}", store.CommittedText("Calc.Instances", "a-calculation"));
        Assert.Equal(3, store.CommittedKeys(ManualTestRunService.Collection).Count);

        // Nothing more to delete: the run count does not change.
        Assert.Equal(0, await service.DeleteOlderFinishedAsync(keep: 2));
        Assert.Equal(unfinished.Id, (await service.FindUnfinishedAsync())!.Id);
    }

    [Fact]
    public async Task A_Run_That_Still_Holds_A_Templates_Latest_Result_Is_Kept_Even_If_It_Is_Older_Than_The_Newest_N()
    {
        var (service, _, _) = Make();
        var oldest = await FinishedRunAsync(service, ("example:t.b/e2", ManualTestOutcome.Fail)); // the only run with a result for t.b
        await FinishedRunAsync(service, ("example:t.a/e0", ManualTestOutcome.Pass));
        await FinishedRunAsync(service, ("example:t.a/e0", ManualTestOutcome.Pass));
        await FinishedRunAsync(service, ("example:t.a/e0", ManualTestOutcome.Pass));

        var plan = await service.PlanTrimAsync(keep: 1);
        Assert.Equal(1, plan.KeptForStatus);
        Assert.DoesNotContain(oldest.Id, plan.Delete);

        Assert.Equal(2, await service.DeleteOlderFinishedAsync(keep: 1));
        var status = await service.LatestByTemplateAsync();
        Assert.Equal(ManualTestOutcome.Fail, status["t.b"].Outcome);
        Assert.Equal(oldest.Id, status["t.b"].RunId);
        Assert.NotNull(await service.FindAsync(oldest.Id));
    }

    [Fact]
    public async Task Nothing_Is_Deleted_When_There_Are_No_More_Finished_Runs_Than_Are_Kept_And_The_Keep_Count_Is_Checked()
    {
        var (service, _, _) = Make();
        await FinishedRunAsync(service);
        await FinishedRunAsync(service);

        Assert.Equal(0, await service.DeleteOlderFinishedAsync(keep: 2));
        Assert.Equal(2, (await service.ListAsync()).Count);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DeleteOlderFinishedAsync(keep: 0));
    }

    [Fact]
    public async Task A_Full_Store_Can_Be_Cleared_Enough_To_Start_Again_Without_Touching_Unfinished_Runs()
    {
        var (service, _, _) = Make(maxRuns: 4);

        for (var i = 0; i < 3; i++)
            await FinishedRunAsync(service);

        var open = await service.StartAsync();
        await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.StartAsync());

        Assert.Equal(2, await service.DeleteOlderFinishedAsync(keep: 1));

        var next = await service.StartAsync();
        Assert.NotEqual(open.Id, next.Id);
        Assert.NotNull(await service.FindAsync(open.Id));
    }

    // ---------------------------------------------------------------- the lighter listing

    [Fact]
    public async Task The_Overview_Counts_Finished_Runs_Without_Reading_Them_And_Finds_An_Unfinished_Run_Behind_Any_Number_Of_Newer_Finished_Ones()
    {
        var (service, _, _) = Make();
        var open = await service.StartAsync();
        await service.RecordAsync(open.Id, "smoke-1", ManualTestOutcome.Pass, "ok", null);

        // More finished runs than one listing returns, all newer than the unfinished one.
        for (var i = 0; i < ManualTestRunService.MaxListed + 5; i++)
            await FinishedRunAsync(service);

        var overview = await service.OverviewAsync();

        Assert.Equal(ManualTestRunService.MaxListed + 6, overview.Total);
        Assert.Equal(ManualTestRunService.MaxListed + 5, overview.Finished);
        Assert.Equal(open.Id, overview.NewestUnfinished!.Id);
        Assert.Equal(open.Id, (await service.FindUnfinishedAsync())!.Id);
    }

    [Fact]
    public async Task A_Note_That_Contains_The_Words_Finished_Utc_Does_Not_Make_An_Unfinished_Run_Look_Finished()
    {
        var (service, _, _) = Make();
        var run = await service.StartAsync();
        await service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, "\"FinishedUtc\":\"2026-10-26T10:00:00Z\"", "\"FinishedUtc\":");

        var overview = await service.OverviewAsync();

        Assert.Equal(0, overview.Finished);
        Assert.Equal(run.Id, overview.NewestUnfinished!.Id);
    }

    // ---------------------------------------------------------------- L12: what counts as the status

    [Fact]
    public async Task The_Latest_Status_Counts_Only_Finished_Runs_On_The_Current_Script()
    {
        var clock = new Clock();
        var (service, store, _) = Make(clock: clock);
        var open = await service.StartAsync();
        await service.RecordAsync(open.Id, "example:t.a/e0", ManualTestOutcome.Fail, "", null);

        Assert.Empty(await service.LatestByTemplateAsync()); // a run still being worked on is not a status

        await service.FinishAsync(open.Id);
        Assert.Equal(ManualTestOutcome.Fail, (await service.LatestByTemplateAsync())["t.a"].Outcome);

        // A finished run made against another script version is not counted either (its worked examples may have changed since).
        var (older, _, _) = Make(scriptVersion: "9.9.8+00000000", store: store, clock: clock);
        var olderRun = await older.StartAsync();
        await older.RecordAsync(olderRun.Id, "example:t.b/e2", ManualTestOutcome.Pass, "", null);
        await older.FinishAsync(olderRun.Id);

        var status = await service.LatestByTemplateAsync();
        Assert.Equal(["t.a"], status.Keys);
        Assert.Equal(open.Id, status["t.a"].RunId);
    }

    // ---------------------------------------------------------------- L8: export

    [Fact]
    public async Task The_Markdown_Sheet_Encodes_Angle_Brackets_And_Ampersands_In_Every_Text_The_Tester_Or_The_Build_Supplies()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var service = new ManualTestRunService(store, GuidedTestHelpers.SmallScript(), () => "0.26.0 <b>(1a2b3c4)</b>", () => "a&b", () => ("sid", "A <script>alert(1)</script> & Co"), new Clock().Tick);
        var run = await service.StartAsync();
        await service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Fail, "Shows <img src=x onerror=1> & more | here\nsecond", "P < 3 & Q > 2");

        var sheet = service.ExportMarkdown((await service.FindAsync(run.Id))!);

        Assert.DoesNotContain("<script>", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", sheet, StringComparison.Ordinal);
        Assert.Contains("- Tester: A &lt;script&gt;alert(1)&lt;/script&gt; &amp; Co", sheet, StringComparison.Ordinal);
        Assert.Contains("- Build: 0.26.0 &lt;b&gt;(1a2b3c4)&lt;/b&gt;", sheet, StringComparison.Ordinal);
        Assert.Contains("- Commit: a&amp;b", sheet, StringComparison.Ordinal);
        Assert.Contains("Shows &lt;img src=x onerror=1&gt; &amp; more \\| here<br>second", sheet, StringComparison.Ordinal);
        Assert.Contains("P &lt; 3 &amp; Q &gt; 2", sheet, StringComparison.Ordinal);

        // The one tag the sheet writes itself is still there.
        Assert.Contains("<br>second", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Sheet_Names_The_Known_Defects_List_Of_The_Script_Release_Not_A_Fixed_One()
    {
        var (service, _, _) = Make();
        var run = await service.StartAsync();
        var sheet = service.ExportMarkdown(run);
        Assert.Contains("`v9.9.9-known-defects.md`", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("v0.26.0-known-defects", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Sheet_Is_Written_Through_A_Temporary_File_And_Moved_So_A_Failure_Leaves_The_Earlier_File_Whole()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tempest-guided-sheet-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(folder);

        try
        {
            var path = Path.Combine(folder, "sheet.md");
            await ManualTestMarkdown.WriteFileAsync(path, "first sheet é");
            Assert.Equal("first sheet é", await File.ReadAllTextAsync(path));
            Assert.Equal([0x66, 0x69, 0x72], (await File.ReadAllBytesAsync(path)).Take(3)); // no byte-order mark

            await ManualTestMarkdown.WriteFileAsync(path, "second sheet");
            Assert.Equal("second sheet", await File.ReadAllTextAsync(path));
            Assert.Equal([path], Directory.GetFiles(folder)); // no temporary file left

            // A target that cannot be replaced (a folder of that name) fails, leaves the folder as it was and leaves no temporary file.
            var blocked = Path.Combine(folder, "blocked.md");
            Directory.CreateDirectory(blocked);
            await File.WriteAllTextAsync(Path.Combine(blocked, "inside.txt"), "x");
            await Assert.ThrowsAnyAsync<Exception>(() => ManualTestMarkdown.WriteFileAsync(blocked, "never"));
            Assert.Equal([path], Directory.GetFiles(folder));
            Assert.True(File.Exists(Path.Combine(blocked, "inside.txt")));

            // A folder that does not exist fails with an IO error.
            await Assert.ThrowsAnyAsync<IOException>(() => ManualTestMarkdown.WriteFileAsync(Path.Combine(folder, "missing", "sheet.md"), "never"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
