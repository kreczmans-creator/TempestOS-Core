using System.Text.Json.Nodes;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>Test runs: create, resume, record, finish, the caps, the tester and build they carry, the markdown sheet, and the dashboard's latest-per-template view.</summary>
public sealed class ManualTestRunServiceTests
{
    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Tick() => Now = Now.AddMinutes(1);
    }

    private static (ManualTestRunService Service, InMemoryQueryablePersistenceStore Store, Clock Clock, Tester Tester) Make(int maxRuns = ManualTestRunService.DefaultMaxRuns, GuidedTestScript? script = null)
    {
        var store = new InMemoryQueryablePersistenceStore();
        var clock = new Clock();
        var tester = new Tester("s.kreczman", "Steven Kreczman");
        var service = new ManualTestRunService(store, script ?? GuidedTestHelpers.SmallScript(), () => "0.26.0 (1a2b3c4)", () => "1a2b3c4", () => (tester.Id, tester.Name), clock.Tick, maxRuns);
        return (service, store, clock, tester);
    }

    private sealed class Tester(string id, string name)
    {
        public string Id { get; set; } = id;

        public string Name { get; set; } = name;
    }

    [Fact]
    public async Task A_New_Run_Carries_The_Date_Build_Commit_Tester_And_Script_Version()
    {
        var (service, store, clock, _) = Make();
        var run = await service.StartAsync();

        Assert.Equal(new DateTimeOffset(2026, 10, 26, 9, 1, 0, TimeSpan.Zero), run.StartedUtc);
        Assert.Null(run.FinishedUtc);
        Assert.Equal("0.26.0 (1a2b3c4)", run.Build);
        Assert.Equal("1a2b3c4", run.BuildCommit);
        Assert.Equal("Steven Kreczman", run.Tester);
        Assert.Equal("s.kreczman", run.TesterId);
        Assert.Equal("9.9.9+aaaaaaaa", run.ScriptVersion);
        Assert.Equal(ManualTestRun.CurrentSchema, run.Schema);
        Assert.Single(store.CommittedKeys(ManualTestRunService.Collection));
        Assert.Equal(clock.Now, run.StartedUtc);
        var stored = (await service.FindAsync(run.Id))!;
        Assert.Equal((run.Id, run.StartedUtc, run.Build, run.BuildCommit, run.Tester, run.TesterId, run.ScriptVersion), (stored.Id, stored.StartedUtc, stored.Build, stored.BuildCommit, stored.Tester, stored.TesterId, stored.ScriptVersion));
        Assert.Empty(stored.Results);
    }

    [Fact]
    public async Task A_Build_Without_A_Commit_And_A_Tester_Without_A_Name_Are_Recorded_Plainly()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var service = new ManualTestRunService(store, GuidedTestHelpers.SmallScript(), () => "0.26.0", () => "  ", () => ("sid-1", ""));
        var run = await service.StartAsync();
        Assert.Equal("unknown", run.BuildCommit);
        Assert.Equal("sid-1", run.Tester);
    }

    [Fact]
    public async Task Results_Are_Recorded_Replaced_And_Survive_A_Reload()
    {
        var (service, _, _, _) = Make();
        var run = await service.StartAsync();

        await service.RecordAsync(run.Id, "example:t.a/e0", ManualTestOutcome.Fail, "Off by one digit.", "P = 3.94 kN");
        var updated = await service.RecordAsync(run.Id, "example:t.a/e0", ManualTestOutcome.Pass, "On a second look it agrees.", "P = 3.93 kN");
        await service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Skipped, null, null);

        var stored = (await service.FindAsync(run.Id))!;
        Assert.Equal(2, stored.Results.Count);
        var result = stored.ResultFor("example:t.a/e0")!;
        Assert.Equal(ManualTestOutcome.Pass, result.Outcome);
        Assert.Equal("On a second look it agrees.", result.Note);
        Assert.Equal("P = 3.93 kN", result.ValuesShown);
        Assert.Equal(ManualTestOutcome.Skipped, stored.ResultFor("smoke-1")!.Outcome);
        Assert.Single(updated.Results); // re-recording a step replaced its result rather than adding one
        Assert.Equal(new ManualTestTally(1, 0, 0, 1, 2), ManualTestTally.Of(stored, service.Script));
    }

    [Fact]
    public async Task An_Unfinished_Run_Is_Found_To_Resume_And_The_Newest_One_Wins()
    {
        var (service, _, _, _) = Make();
        Assert.Null(await service.FindUnfinishedAsync());

        var first = await service.StartAsync();
        var second = await service.StartAsync();
        await service.RecordAsync(first.Id, "smoke-1", ManualTestOutcome.Pass, "ok", null);

        Assert.Equal(second.Id, (await service.FindUnfinishedAsync())!.Id);
        await service.FinishAsync(second.Id);
        var resumed = (await service.FindUnfinishedAsync())!;
        Assert.Equal(first.Id, resumed.Id);
        Assert.Equal("ok", resumed.ResultFor("smoke-1")!.Note);

        await service.RecordAsync(resumed.Id, "example:t.a/e1", ManualTestOutcome.Pass, "", "P = 3.93 kN");
        Assert.Equal(2, (await service.FindAsync(first.Id))!.Results.Count);
    }

    [Fact]
    public async Task A_Finished_Run_Is_Dated_And_Never_Changes_Again()
    {
        var (service, _, clock, _) = Make();
        var run = await service.StartAsync();
        await service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, "", null);
        var finished = await service.FinishAsync(run.Id);

        Assert.Equal(clock.Now, finished.FinishedUtc);
        Assert.True(finished.IsFinished);
        Assert.Null(await service.FindUnfinishedAsync());

        var refusal = await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Fail, "", null));
        Assert.Contains("was finished on 2026-10-26", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("start a new run", refusal.Message, StringComparison.Ordinal);

        var again = await service.FinishAsync(run.Id);
        Assert.Equal(finished.FinishedUtc, again.FinishedUtc);
        Assert.Equal(ManualTestOutcome.Pass, (await service.FindAsync(run.Id))!.ResultFor("smoke-1")!.Outcome);
    }

    [Fact]
    public async Task A_Note_Of_Two_Thousand_Characters_Is_Kept_And_One_More_Is_Refused_Not_Cut()
    {
        var (service, _, _, _) = Make();
        var run = await service.StartAsync();

        var max = new string('n', ManualTestRun.MaxNoteLength);
        await service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, max, null);
        Assert.Equal(max, (await service.FindAsync(run.Id))!.ResultFor("smoke-1")!.Note);

        var refusal = await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Fail, max + "x", null));
        Assert.Contains("2,000", refusal.Message.Replace("2000", "2,000", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("Smoke test 1", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(ManualTestOutcome.Pass, (await service.FindAsync(run.Id))!.ResultFor("smoke-1")!.Outcome);
    }

    [Fact]
    public async Task Shown_Values_Over_The_Cap_Are_Cut_With_A_Mark()
    {
        var (service, _, _, _) = Make();
        var run = await service.StartAsync();
        var saved = await service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, "", new string('v', ManualTestRun.MaxValuesShownLength + 500));
        var shown = saved.ResultFor("smoke-1")!.ValuesShown;
        Assert.Equal(ManualTestRun.MaxValuesShownLength, shown.Length);
        Assert.EndsWith("…", shown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Step_The_Script_Does_Not_Have_Or_An_Outcome_That_Is_Not_One_Is_Refused()
    {
        var (service, _, _, _) = Make();
        var run = await service.StartAsync();
        await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.RecordAsync(run.Id, "example:nope/none", ManualTestOutcome.Pass, "", null));
        await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.RecordAsync(run.Id, "smoke-1", (ManualTestOutcome)99, "", null));
        await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.RecordAsync(Guid.NewGuid(), "smoke-1", ManualTestOutcome.Pass, "", null));
        Assert.Empty((await service.FindAsync(run.Id))!.Results);
    }

    [Fact]
    public async Task Only_The_Person_Who_Started_A_Run_Can_Record_In_It_Or_Finish_It()
    {
        var (service, _, _, tester) = Make();
        var run = await service.StartAsync();

        tester.Id = "a.n.other";
        tester.Name = "A N Other";
        var refusal = await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, "", null));
        Assert.Contains("started by Steven Kreczman", refusal.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.FinishAsync(run.Id));

        tester.Id = "s.kreczman";
        await service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, "", null);
    }

    [Fact]
    public async Task A_Run_Started_On_Another_Script_Version_Can_Be_Finished_But_Not_Added_To()
    {
        var (service, store, clock, tester) = Make();
        var run = await service.StartAsync();

        var newer = new ManualTestRunService(store, GuidedTestHelpers.SmallScript("9.9.9+bbbbbbbb"), () => "b", () => "c", () => (tester.Id, tester.Name), clock.Tick);
        var refusal = await Assert.ThrowsAsync<ManualTestRefusedException>(() => newer.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Pass, "", null));
        Assert.Contains("9.9.9+aaaaaaaa", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("9.9.9+bbbbbbbb", refusal.Message, StringComparison.Ordinal);
        Assert.True((await newer.FinishAsync(run.Id)).IsFinished);
    }

    [Fact]
    public async Task The_Store_Keeps_No_More_Runs_Than_Its_Cap()
    {
        var (service, store, _, _) = Make(maxRuns: 3);

        for (var i = 0; i < 3; i++)
            await service.StartAsync();

        var refusal = await Assert.ThrowsAsync<ManualTestRefusedException>(() => service.StartAsync());
        Assert.Contains("already 3 test runs", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(3, store.CommittedKeys(ManualTestRunService.Collection).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(maxRuns: 0));
    }

    [Fact]
    public async Task A_Listing_Is_Newest_First_And_Capped()
    {
        var (service, _, _, _) = Make();
        var ids = new List<Guid>();

        for (var i = 0; i < 5; i++)
            ids.Add((await service.StartAsync()).Id);

        Assert.Equal(Enumerable.Reverse(ids), (await service.ListAsync()).Select(r => r.Id));
        Assert.Equal(2, (await service.ListAsync(2)).Count);
        Assert.Single(await service.ListAsync(0)); // a limit below one is one
    }

    [Fact]
    public async Task A_Record_That_Cannot_Be_Read_Or_Was_Written_By_A_Newer_Build_Is_Left_Out_Not_Fatal()
    {
        var (service, store, _, _) = Make();
        var good = await service.StartAsync();

        await store.WriteAsync(ManualTestRunService.Collection, "damaged", "{ not json");
        var newer = JsonNode.Parse(store.CommittedText(ManualTestRunService.Collection, good.Id.ToString("N"))!)!;
        newer["Schema"] = ManualTestRun.CurrentSchema + 1;
        newer["Id"] = Guid.NewGuid().ToString();
        await store.WriteAsync(ManualTestRunService.Collection, "from-the-future", newer.ToJsonString());

        Assert.Equal([good.Id], (await service.ListAsync()).Select(r => r.Id));
        Assert.Null(await service.FindAsync(Guid.NewGuid()));
        Assert.Empty(await service.LatestByTemplateAsync());
    }

    [Fact]
    public async Task The_Markdown_Sheet_Has_The_Runbook_Table_Columns_A_Row_Per_Step_And_The_Commit()
    {
        var (service, _, _, _) = Make();
        var run = await service.StartAsync();
        await service.RecordAsync(run.Id, "example:t.a/e0", ManualTestOutcome.Fail, "Shows 3.94 | not 3.93\nsecond line", "P = 3.94 kN");
        await service.RecordAsync(run.Id, "smoke-1", ManualTestOutcome.Blocked, "App would not start.", null);
        run = await service.FinishAsync(run.Id);

        var sheet = service.ExportMarkdown(run);
        var lines = sheet.Split('\n');

        Assert.Equal("# TempestOS guided test results", lines[0]);
        Assert.Contains("- Commit: 1a2b3c4", lines);
        Assert.Contains("- Build: 0.26.0 (1a2b3c4)", lines);
        Assert.Contains("- Tester: Steven Kreczman", lines);
        Assert.Contains("- Script: 9.9.9+aaaaaaaa", lines);
        Assert.Contains("- Started: 2026-10-26 09:01 UTC", lines);
        Assert.Contains("- Finished: 2026-10-26 09:04 UTC", lines);
        Assert.Contains("- Summary: 0 pass, 1 fail, 1 blocked, 0 skipped, 2 not run", lines);

        var header = lines.Single(l => l.StartsWith("| Step |", StringComparison.Ordinal));
        Assert.Equal("| Step | Item (template, example or script) | Result (Pass / Fail / Blocked) | Value shown | Expected | Note |", header);
        Assert.Equal("|---|---|---|---|---|---|", lines[Array.IndexOf(lines, header) + 1]);

        var rows = lines.Where(l => l.StartsWith("| ", StringComparison.Ordinal) && l != header).ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal("| 4 | Smoke test 1: start the app | Blocked |  | It starts. | App would not start. |", rows[0]);
        Assert.Equal("| 5 | t.a — e0 | Fail | P = 3.94 kN | P = 3.93 kN | Shows 3.94 \\| not 3.93<br>second line |", rows[1]);
        Assert.Contains("| Not run |", rows[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Unfinished_Run_Says_So_In_Its_Sheet()
    {
        var (service, _, _, _) = Make();
        var run = await service.StartAsync();
        Assert.Contains("- Finished: not finished", service.ExportMarkdown(run), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Latest_Result_Per_Template_Is_The_Worst_Of_The_Latest_Per_Example_With_Its_Date()
    {
        var (service, _, _, tester) = Make();
        var first = await service.StartAsync();
        await service.RecordAsync(first.Id, "example:t.a/e0", ManualTestOutcome.Pass, "", null);
        await service.RecordAsync(first.Id, "example:t.a/e1", ManualTestOutcome.Fail, "", null);
        await service.RecordAsync(first.Id, "example:t.b/e2", ManualTestOutcome.Blocked, "", null);
        await service.RecordAsync(first.Id, "smoke-1", ManualTestOutcome.Fail, "", null); // not a template result
        await service.FinishAsync(first.Id);

        var status = await service.LatestByTemplateAsync();
        Assert.Equal(["t.a", "t.b"], status.Keys.Order());
        Assert.Equal(ManualTestOutcome.Fail, status["t.a"].Outcome);
        Assert.Equal(2, status["t.a"].StepsRecorded);
        Assert.Equal(2, status["t.a"].StepsInScript);
        Assert.Equal(ManualTestOutcome.Blocked, status["t.b"].Outcome);
        Assert.Equal(first.Id, status["t.b"].RunId);
        Assert.Equal("1a2b3c4", status["t.b"].BuildCommit);
        Assert.Equal("Steven Kreczman", status["t.b"].Tester);

        // A later run supersedes the earlier result of the same example; a skipped step is not a result and supersedes nothing.
        tester.Name = "Steven K";
        var second = await service.StartAsync();
        await service.RecordAsync(second.Id, "example:t.a/e1", ManualTestOutcome.Pass, "", null);
        await service.RecordAsync(second.Id, "example:t.b/e2", ManualTestOutcome.Skipped, "", null);
        var after = await service.LatestByTemplateAsync();

        Assert.Equal(ManualTestOutcome.Pass, after["t.a"].Outcome);
        Assert.Equal(second.Id, after["t.a"].RunId);
        Assert.True(after["t.a"].LastRecordedUtc > status["t.a"].LastRecordedUtc);
        Assert.Equal(ManualTestOutcome.Blocked, after["t.b"].Outcome);
        Assert.Equal(first.Id, after["t.b"].RunId);
    }

    [Fact]
    public async Task The_Status_Source_Interface_Is_Read_Only()
    {
        var (service, _, _, _) = Make();
        IManualTestStatusSource source = service;
        Assert.Empty(await source.LatestByTemplateAsync());
        Assert.Equal(["LatestByTemplateAsync"], typeof(IManualTestStatusSource).GetMethods().Select(m => m.Name));
    }
}
