using Tempest.Calc.Library.ManualTesting;
using Tempest.Desktop.GuidedTest;

namespace Tempest.Desktop.Tests.GuidedTest;

/// <summary>The Guided test's state machine, over a real SQLite store and a stand-in worksheet form: runs, steps, filters, recording, the worksheet and the auto-compare.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class GuidedTestViewModelTests
{
    private const string BearingExample = "example:mech.bearing-life-basic/ball-bearing-combined-95";

    [Fact]
    public async Task Starting_A_Run_Records_The_Tester_And_Build_And_Lands_On_The_First_Step()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        Assert.Null(model.Run);
        Assert.Contains("No run is open", model.RunText, StringComparison.Ordinal);
        Assert.False(model.CanRecord);

        await model.StartNewAsync();

        Assert.NotNull(model.Run);
        Assert.Equal("Steven Kreczman", model.Run!.Tester);
        Assert.Equal("1a2b3c4", model.Run.BuildCommit);
        Assert.Contains("Steven Kreczman", model.RunText, StringComparison.Ordinal);
        Assert.Contains("0.26.0 (1a2b3c4)", model.RunText, StringComparison.Ordinal);
        Assert.Contains("not finished", model.RunText, StringComparison.Ordinal);
        Assert.Equal("0 pass, 0 fail, 0 blocked, 0 skipped, 109 not run", model.TallyText);
        Assert.True(model.CanRecord);
        Assert.Equal(GuidedTestScript.Current.Steps[0].Id, model.Current!.Id);
        Assert.Equal(0, model.CurrentIndex);
    }

    [Fact]
    public async Task A_Result_Needs_A_Run_And_A_Ticked_Outcome_And_Says_So()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;

        await model.RecordAsync();
        Assert.Equal("Start or resume a run before recording a result.", model.Status);

        await model.StartNewAsync();
        await model.RecordAsync();
        Assert.Equal("Tick Pass, Fail, Blocked or Skipped first.", model.Status);
        Assert.Null(model.Recorded);
    }

    [Fact]
    public async Task A_Recorded_Result_Is_Shown_Again_When_The_Step_Is_Revisited_And_Counted()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();

        model.SetDraftOutcome(ManualTestOutcome.Fail);
        model.SetDraftNote("The axial factor is 1.5 on the form, 1.4 here.");
        model.SetDraftValues("Y = 1.4");
        await model.RecordAsync();

        Assert.Contains("Recorded Fail", model.Status, StringComparison.Ordinal);
        Assert.Equal("0 pass, 1 fail, 0 blocked, 0 skipped, 108 not run", model.TallyText);
        var first = model.Current!.Id;

        model.Next();
        Assert.NotEqual(first, model.Current!.Id);
        Assert.Null(model.DraftOutcome);
        Assert.Equal(string.Empty, model.DraftNote);

        model.Previous();
        Assert.Equal(first, model.Current!.Id);
        Assert.Equal(ManualTestOutcome.Fail, model.DraftOutcome);
        Assert.Equal("The axial factor is 1.5 on the form, 1.4 here.", model.DraftNote);
        Assert.Equal("Y = 1.4", model.DraftValues);
        Assert.Equal("Fail", model.Rows[0].Result);
        Assert.Equal("—", model.Rows[1].Result);
    }

    [Fact]
    public async Task A_Note_Over_The_Cap_Is_Refused_With_A_Plain_Sentence_And_Nothing_Is_Recorded()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();
        model.SetDraftOutcome(ManualTestOutcome.Pass);

        model.SetDraftNote(new string('x', ManualTestRun.MaxNoteLength + 1));
        await model.RecordAsync();
        Assert.Contains("2000", model.Status, StringComparison.Ordinal);
        Assert.Contains(model.Current!.Title, model.Status, StringComparison.Ordinal);
        Assert.Null(model.Recorded);

        model.SetDraftNote(new string('x', ManualTestRun.MaxNoteLength));
        await model.RecordAsync();
        Assert.Equal(ManualTestRun.MaxNoteLength, model.Recorded!.Note.Length);
    }

    [Fact]
    public async Task An_Unfinished_Run_Is_Offered_On_Restart_And_Resumed_At_Its_First_Step_Without_A_Result()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();

        foreach (var outcome in new[] { ManualTestOutcome.Pass, ManualTestOutcome.Blocked })
        {
            model.SetDraftOutcome(outcome);
            await model.RecordAsync();
            model.Next();
        }

        var run = model.Run!.Id;
        var restarted = kit.Restart();
        Assert.Null(restarted.Run);
        await restarted.RefreshAsync();

        Assert.Equal(run, restarted.Resumable!.Id);
        Assert.Contains("An unfinished run by Steven Kreczman", restarted.ResumeText, StringComparison.Ordinal);
        Assert.Contains("1 pass, 0 fail, 1 blocked", restarted.ResumeText, StringComparison.Ordinal);

        await restarted.ResumeAsync();
        Assert.Equal(run, restarted.Run!.Id);
        Assert.Null(restarted.Resumable);
        Assert.Equal(GuidedTestScript.Current.Steps[2].Id, restarted.Current!.Id);
        Assert.Equal("1 pass, 0 fail, 1 blocked, 0 skipped, 107 not run", restarted.TallyText);
        Assert.True(restarted.CanRecord);

        restarted.SetDraftOutcome(ManualTestOutcome.Pass);
        await restarted.RecordAsync();
        Assert.Equal("2 pass, 0 fail, 1 blocked, 0 skipped, 106 not run", restarted.TallyText);
    }

    [Fact]
    public async Task Resuming_With_Nothing_To_Resume_Says_So()
    {
        await using var kit = GuidedKit.Create();
        await kit.Model.ResumeAsync();
        Assert.Equal("There is no unfinished run to resume.", kit.Model.Status);
    }

    [Fact]
    public async Task A_Finished_Run_Is_Dated_Read_Only_And_Not_Offered_Again()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();
        model.SetDraftOutcome(ManualTestOutcome.Pass);
        await model.RecordAsync();

        await model.FinishAsync();

        Assert.True(model.Run!.IsFinished);
        Assert.False(model.CanFinish);
        Assert.False(model.CanRecord);
        Assert.Contains("finished 20", model.RunText, StringComparison.Ordinal);
        Assert.Contains("Run finished", model.Status, StringComparison.Ordinal);

        model.SetDraftOutcome(ManualTestOutcome.Fail);
        await model.RecordAsync();
        Assert.Contains("was finished on", model.Status, StringComparison.Ordinal);
        Assert.Equal(ManualTestOutcome.Pass, model.Recorded!.Outcome);

        var restarted = kit.Restart();
        await restarted.RefreshAsync();
        Assert.Null(restarted.Resumable);

        await model.FinishAsync();
        Assert.Equal("There is no unfinished run open to finish.", model.Status);
    }

    [Fact]
    public async Task Another_Person_Cannot_Add_To_A_Run_And_The_Refusal_Names_Who_Started_It()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();

        kit.TesterId = "a.n.other";
        kit.TesterName = "A N Other";
        model.SetDraftOutcome(ManualTestOutcome.Pass);
        await model.RecordAsync();

        Assert.Contains("started by Steven Kreczman", model.Status, StringComparison.Ordinal);
        Assert.Null(model.Recorded);
    }

    [Fact]
    public async Task A_Run_On_Another_Script_Version_Is_Resumed_Read_Only_And_Can_Be_Finished()
    {
        await using var kit = GuidedKit.Create();
        var older = GuidedTestScript.Parse("""{"format":1,"scriptVersion":"0.25.0+00000000","release":"0.25.0","steps":[{"id":"smoke-1","kind":"Smoke","section":"4","title":"Old step"}]}""");
        var started = await kit.MakeWith(older).StartAsync();

        var model = kit.Model;
        await model.RefreshAsync();
        Assert.Equal(started.Id, model.Resumable!.Id);

        await model.ResumeAsync();
        Assert.Equal(started.Id, model.Run!.Id);
        Assert.False(model.CanRecord);
        Assert.Contains("0.25.0+00000000", model.Status, StringComparison.Ordinal);
        Assert.Contains("Finish it, then start a new run", model.Status, StringComparison.Ordinal);

        model.SetDraftOutcome(ManualTestOutcome.Pass);
        await model.RecordAsync();
        Assert.Contains("finish the run and start a new one", model.Status, StringComparison.Ordinal);

        await model.FinishAsync();
        Assert.True(model.Run!.IsFinished);
    }

    [Fact]
    public async Task The_Filters_Cover_Every_Section_Of_The_Runbook_And_Narrow_The_List()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.RefreshAsync();
        var script = GuidedTestScript.Current;

        Assert.Equal(["All steps", "Not yet run", "4 · Smoke test", "5 · Worked examples", "6 · Linked chains", "7 · Imports and records", "8 · Lifting", "9 · Legacy records"], model.Filters);
        Assert.Equal(script.Steps.Count, model.Rows.Count);

        model.SetFilter("5 · Worked examples");
        Assert.Equal(87, model.Rows.Count);
        Assert.All(model.Rows, r => Assert.StartsWith("example:", r.Id, StringComparison.Ordinal));
        Assert.Equal(GuidedStepKind.Example, model.Current!.Kind);

        model.SetFilter("8 · Lifting");
        Assert.Equal(6, model.Rows.Count);

        model.SetFilter("No such filter");
        Assert.Equal("8 · Lifting", model.Filter);
    }

    [Fact]
    public async Task The_Search_Narrows_By_Title_Template_Example_Or_Discipline_And_Is_Capped()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.RefreshAsync();

        model.SetSearch("bearing-life-basic");
        Assert.Equal(2, model.Rows.Count);

        model.SetSearch("FLUID-POWER");
        Assert.All(model.Rows, r => Assert.StartsWith("example:fp.", r.Id, StringComparison.Ordinal));

        model.SetSearch("zzz no such step");
        Assert.Empty(model.Rows);
        Assert.Null(model.Current);
        model.Next();
        model.Previous();

        model.SetSearch(new string('a', GuidedTestViewModel.MaxSearchLength + 50));
        Assert.Equal(GuidedTestViewModel.MaxSearchLength, model.Search.Length);

        model.SetSearch(null);
        Assert.Equal(GuidedTestScript.Current.Steps.Count, model.Rows.Count);
    }

    [Fact]
    public async Task Not_Yet_Run_Lists_Only_Steps_Without_A_Result_And_Next_Not_Run_Skips_Done_Ones()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();

        model.SetDraftOutcome(ManualTestOutcome.Pass);
        await model.RecordAsync();
        model.SetFilter(GuidedTestViewModel.NotYetRun);
        Assert.Equal(GuidedTestScript.Current.Steps.Count - 1, model.Rows.Count);

        model.SetFilter(GuidedTestViewModel.AllSteps);
        model.Select(GuidedTestScript.Current.Steps[0].Id);
        model.NextNotRun();
        Assert.Equal(GuidedTestScript.Current.Steps[1].Id, model.Current!.Id);
    }

    [Fact]
    public async Task Selecting_A_Step_Outside_The_Filter_Widens_It_So_The_Step_Is_Shown()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.SetFilter("8 · Lifting");
        model.Select(BearingExample);

        Assert.Equal(GuidedTestViewModel.AllSteps, model.Filter);
        Assert.Equal(BearingExample, model.Current!.Id);
        Assert.Equal(BearingExample, model.Rows[model.CurrentIndex].Id);

        model.Select("example:not/there");
        Assert.Equal(BearingExample, model.Current!.Id);
    }

    [Fact]
    public async Task Opening_The_Worksheet_Hands_It_The_Examples_Template_And_Inputs_And_Says_What_To_Do_Next()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select(BearingExample);
        kit.Worksheet.Message = "Bearing life is open.";

        model.OpenWorksheet();

        var (template, inputs, discard) = Assert.Single(kit.Worksheet.Prepared);
        Assert.Equal("mech.bearing-life-basic", template);
        Assert.Equal(model.Current!.InputsJson, inputs);
        Assert.False(discard);
        Assert.StartsWith("Bearing life is open.", model.Status, StringComparison.Ordinal);
        Assert.Contains("compare its outputs", model.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Form_With_Unsaved_Edits_Is_Replaced_Only_On_The_Second_Press_For_The_Same_Step()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select(BearingExample);
        kit.Worksheet.Next = GuidedPrepareOutcome.NeedsConfirmation;
        kit.Worksheet.Message = "Unsaved changes. Press again.";

        model.OpenWorksheet();
        Assert.False(kit.Worksheet.Prepared[0].Discard);
        Assert.Equal("Unsaved changes. Press again.", model.Status);

        kit.Worksheet.Next = GuidedPrepareOutcome.Ready;
        model.OpenWorksheet();
        Assert.True(kit.Worksheet.Prepared[1].Discard);

        // Having been replaced, the next open asks again.
        model.OpenWorksheet();
        Assert.False(kit.Worksheet.Prepared[2].Discard);

        // Another step does not inherit the first press.
        kit.Worksheet.Next = GuidedPrepareOutcome.NeedsConfirmation;
        model.OpenWorksheet();
        model.Select("example:mech.bearing-life-basic/ball-bearing-radial");
        model.OpenWorksheet();
        Assert.False(kit.Worksheet.Prepared[^1].Discard);
    }

    [Fact]
    public async Task A_Worksheet_That_Cannot_Open_Gives_Its_Reason_And_A_Manual_Step_Has_Nothing_To_Open()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select("example:struct.lifting/lifting-beam-lug-pair");
        kit.Worksheet.Next = GuidedPrepareOutcome.Unavailable;
        kit.Worksheet.Message = "Lifting is switched off by the administrator.";
        model.OpenWorksheet();
        Assert.Equal("Lifting is switched off by the administrator.", model.Status);

        model.Select("lifting-1");
        model.OpenWorksheet();
        Assert.Equal("This step has no worksheet to open; follow its instructions.", model.Status);
        Assert.Single(kit.Worksheet.Prepared);
    }

    [Fact]
    public async Task The_Smoke_Test_Step_That_Pastes_An_Example_Opens_That_Example_And_Shows_Its_Expected_Values()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select("smoke-3");

        Assert.Equal("example:fp.accumulator/clamp-circuit-bladder-accumulator", model.CurrentExample!.Id);
        Assert.Contains(model.ExpectedLines, l => l.Symbol == "V0" && l.Expected.StartsWith("31.6562", StringComparison.Ordinal));

        model.OpenWorksheet();
        Assert.Equal("fp.accumulator", kit.Worksheet.Prepared.Single().Template);
    }

    [Fact]
    public async Task The_Expected_Lines_State_Each_Allowance_And_The_Rule_Is_On_The_Panel()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select(BearingExample);

        var lines = model.ExpectedLines;
        Assert.Contains(lines, l => l.Symbol == "P" && l.Expected == "3.93 kN" && l.Allowance == "±0.005");
        Assert.Contains(lines, l => l.Symbol == "L_10h" && l.Expected == "4290.3 h" && l.Allowance == "±0.05");
        Assert.Contains(lines, l => l.Symbol == "governing check" && l.Expected.StartsWith("PASS", StringComparison.Ordinal) && l.Allowance == "exact text");
        Assert.Contains(lines, l => l.Symbol == "governing utilisation" && l.Expected == "0.728");
        Assert.All(lines, l => Assert.Null(l.Shown));
        Assert.Contains("half a unit in the last digit", model.Rule, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compare_Reads_The_Forms_Outputs_Pre_Ticks_Pass_Fills_The_Values_And_Records_Nothing()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();
        model.Select(BearingExample);
        kit.Worksheet.Shown["mech.bearing-life-basic"] = FakeWorksheet.Engine(model.Current!);

        model.AutoCompare();

        Assert.Equal(ManualTestOutcome.Pass, model.DraftOutcome);
        Assert.Equal(ManualTestOutcome.Pass, model.Suggested);
        Assert.Contains("Pass is ticked; record it to confirm", model.Status, StringComparison.Ordinal);
        Assert.Contains("P = 3.93 kN", model.DraftValues, StringComparison.Ordinal);
        Assert.Contains("L_10h = 4290.3 h", model.DraftValues, StringComparison.Ordinal);
        Assert.All(model.ExpectedLines, l => Assert.Equal(GuidedCompareOutcome.Match, l.Outcome));
        Assert.Empty(model.ComparisonNotes);

        // Nothing is recorded until the tester confirms; recording keeps the shown values and the tester's note.
        Assert.Null(model.Recorded);
        Assert.Equal("0 pass, 0 fail, 0 blocked, 0 skipped, 109 not run", model.TallyText);
        model.SetDraftNote("Agrees with the sheet.");
        await model.RecordAsync();
        Assert.Equal(ManualTestOutcome.Pass, model.Recorded!.Outcome);
        Assert.Contains("P = 3.93 kN", model.Recorded.ValuesShown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Tester_Can_Overrule_The_Pre_Ticked_Result_Before_Recording()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();
        model.Select(BearingExample);
        kit.Worksheet.Shown["mech.bearing-life-basic"] = FakeWorksheet.Engine(model.Current!);
        model.AutoCompare();

        model.SetDraftOutcome(ManualTestOutcome.Fail);
        model.SetDraftNote("The PDF is wrong.");
        await model.RecordAsync();

        Assert.Equal(ManualTestOutcome.Fail, model.Recorded!.Outcome);
    }

    [Fact]
    public async Task Compare_With_A_Wrong_Output_Pre_Ticks_Fail_And_Lists_The_Lines_That_Differ()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select(BearingExample);
        var wrong = FakeWorksheet.Engine(model.Current!);
        kit.Worksheet.Shown["mech.bearing-life-basic"] = wrong with
        {
            Outputs = [.. wrong.Outputs.Select(o => o.Symbol == "P" ? o with { Value = new Tempest.Calc.Engine.Values.ScalarValue(4000, o.Value.Dimension, o.Value.DisplayUnit) } : o)],
        };

        model.AutoCompare();

        Assert.Equal(ManualTestOutcome.Fail, model.DraftOutcome);
        Assert.Contains("differ", model.Status, StringComparison.Ordinal);
        var note = Assert.Single(model.ComparisonNotes);
        Assert.StartsWith("P:", note, StringComparison.Ordinal);
        Assert.Contains(model.ExpectedLines, l => l.Symbol == "P" && l.Outcome == GuidedCompareOutcome.Mismatch);
    }

    [Fact]
    public async Task Compare_Leaves_A_Line_The_Rule_Cannot_Settle_To_The_Tester_And_Ticks_Nothing()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select("example:mech.press-fit/hollow-shaft");
        kit.Worksheet.Shown["mech.press-fit"] = FakeWorksheet.Engine(model.Current!);

        model.AutoCompare();

        Assert.Null(model.DraftOutcome);
        Assert.Null(model.Suggested);
        Assert.Contains("need your judgement", model.Status, StringComparison.Ordinal);
        Assert.Contains(model.ComparisonNotes, n => n.Contains("decide by hand", StringComparison.Ordinal));
        Assert.Contains(model.ExpectedLines, l => l.Outcome == GuidedCompareOutcome.NotComparable);
    }

    [Fact]
    public async Task Compare_Needs_The_Form_To_Be_Open_On_The_Example_And_A_Step_That_Has_Outputs()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select(BearingExample);

        model.AutoCompare();
        Assert.Contains("Open the worksheet for this step first", model.Status, StringComparison.Ordinal);
        Assert.Null(model.DraftOutcome);

        model.Select("legacy-1");
        model.AutoCompare();
        Assert.Equal("This step has no expected outputs to compare.", model.Status);
    }

    [Fact]
    public async Task A_Worksheet_Version_Other_Than_The_One_The_Example_Was_Written_For_Is_Pointed_Out()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select(BearingExample);
        Assert.Equal(string.Empty, model.VersionNote);

        var written = model.Current!.TemplateVersion!;
        kit.Worksheet.Versions["mech.bearing-life-basic"] = written;
        Assert.Equal(string.Empty, model.VersionNote);

        kit.Worksheet.Versions["mech.bearing-life-basic"] = "9.9.9";
        Assert.Contains($"written for version {written}", model.VersionNote, StringComparison.Ordinal);
        Assert.Contains("this build has 9.9.9", model.VersionNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Results_Sheet_Carries_The_Commit_And_The_Result_And_Names_Its_File_For_The_Commit_And_Date()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        Assert.Null(model.ExportMarkdown());
        Assert.Equal("tempestos-guided-test.md", model.SuggestedFileName);

        await model.StartNewAsync();
        model.SetDraftOutcome(ManualTestOutcome.Blocked);
        model.SetDraftNote("No network.");
        await model.RecordAsync();

        var sheet = model.ExportMarkdown()!;
        Assert.Contains("- Commit: 1a2b3c4", sheet, StringComparison.Ordinal);
        Assert.Contains("| Blocked |", sheet, StringComparison.Ordinal);
        Assert.Contains("No network.", sheet, StringComparison.Ordinal);
        Assert.Matches(@"^tempestos-guided-test-1a2b3c4-\d{8}\.md$", model.SuggestedFileName);
    }
}
