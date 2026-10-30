using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Desktop.GuidedTest;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.GuidedTest;

/// <summary>The fixes made after the review of the Guided test (ADR-0175, "Changes after review"): which step Compare reads, a comparison that goes stale, failures that must not end the app, and the smaller findings.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class GuidedTestReviewTests
{
    private const string BearingExample = "example:mech.bearing-life-basic/ball-bearing-combined-95";
    private const string BearingTemplate = "mech.bearing-life-basic";
    private const string FlangeExample = "example:mech.bolted-joint/m12-88-flange";
    private const string OverloadedExample = "example:mech.bolted-joint/m12-88-overloaded";

    private static async Task Settle()
    {
        for (var i = 0; i < 12; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    private static T Named<T>(Control root, string name)
        where T : Control => root.GetLogicalDescendants().OfType<T>().Single(c => AutomationProperties.GetName(c) == name);

    private static async Task Click(Control root, string caption)
    {
        var button = Named<Button>(root, caption);
        Assert.True(button.IsEnabled && button.IsEffectivelyVisible, $"'{caption}' is not available.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Settle();
    }

    /// <summary>A started run with the bearing example open in the fake form and compared (Pass ticked, values filled).</summary>
    private static async Task<GuidedKit> ComparedAsync()
    {
        var kit = GuidedKit.Create();
        var vm = kit.Model;
        await vm.StartNewAsync();
        vm.Select(BearingExample);
        kit.Worksheet.Shown[BearingTemplate] = FakeWorksheet.Engine(vm.Current!);
        vm.OpenWorksheet();
        vm.AutoCompare();
        Assert.Equal(ManualTestOutcome.Pass, vm.DraftOutcome);
        return kit;
    }

    // ---------------------------------------------------------------- M1: Compare reads only the step the form was opened for

    [Fact]
    public async Task Compare_Refuses_A_Step_Whose_Worksheet_Was_Not_Opened_Even_When_Another_Example_Of_The_Same_Template_Is_In_The_Form()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select(FlangeExample);
        kit.Worksheet.Shown["mech.bolted-joint"] = FakeWorksheet.Engine(model.Current!);
        model.OpenWorksheet();
        model.AutoCompare();
        Assert.Equal(ManualTestOutcome.Pass, model.DraftOutcome);

        // The overloaded example is selected without being opened: the form still holds the flange example's run.
        model.Select(OverloadedExample);
        Assert.Null(model.DraftOutcome);
        model.AutoCompare();

        Assert.Contains("Open the worksheet for this step first", model.Status, StringComparison.Ordinal);
        Assert.Contains("another example", model.Status, StringComparison.Ordinal);
        Assert.Null(model.DraftOutcome);
        Assert.Empty(model.DraftValues);
        Assert.All(model.ExpectedLines, l => Assert.Null(l.Shown));

        // Once its own worksheet is opened, it compares.
        model.OpenWorksheet();
        model.AutoCompare();
        Assert.Contains("lines", model.Status, StringComparison.Ordinal);
        Assert.NotNull(model.ExpectedLines[0].Shown);
    }

    [Fact]
    public async Task A_Form_That_Could_Not_Be_Prepared_Or_Is_Not_Holding_The_Example_Any_More_Is_Not_Compared()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select(BearingExample);
        kit.Worksheet.Shown[BearingTemplate] = FakeWorksheet.Engine(model.Current!);

        // The worksheet refuses the example's inputs: nothing is open for it.
        kit.Worksheet.Next = GuidedPrepareOutcome.Unavailable;
        model.OpenWorksheet();
        model.AutoCompare();
        Assert.Contains("Open the worksheet for this step first", model.Status, StringComparison.Ordinal);

        // Opened, then the form no longer gives the example's run (edited, or a saved calculation shown): the host says so by returning no run.
        kit.Worksheet.Next = GuidedPrepareOutcome.Ready;
        model.OpenWorksheet();
        kit.Worksheet.Shown.Clear();
        model.AutoCompare();
        Assert.Contains("no longer holds this example's inputs", model.Status, StringComparison.Ordinal);
        Assert.Null(model.DraftOutcome);
    }

    // ---------------------------------------------------------------- M2: a comparison goes stale with the form

    [Fact]
    public async Task A_Change_To_The_Form_After_Compare_Takes_Back_The_Comparison_The_Tick_And_The_Values()
    {
        await using var kit = await ComparedAsync();
        var model = kit.Model;
        model.SetDraftNote("My own note.");
        Assert.NotEmpty(model.DraftValues);

        kit.Worksheet.RaiseFormChanged();

        Assert.Null(model.DraftOutcome);
        Assert.Empty(model.DraftValues);
        Assert.Null(model.Suggested);
        Assert.Empty(model.ComparisonNotes);
        Assert.All(model.ExpectedLines, l => Assert.Null(l.Shown));
        Assert.Contains("form changed after the comparison", model.Status, StringComparison.Ordinal);
        Assert.Equal("My own note.", model.DraftNote);

        await model.RecordAsync();
        Assert.Equal("Tick Pass, Fail, Blocked or Skipped first.", model.Status);
        Assert.Null(model.Recorded);
    }

    [Fact]
    public async Task A_Result_The_Tester_Ticked_Or_Typed_Themselves_Survives_A_Change_To_The_Form()
    {
        await using var kit = await ComparedAsync();
        var model = kit.Model;
        model.SetDraftOutcome(ManualTestOutcome.Blocked);
        kit.Worksheet.RaiseFormChanged();

        Assert.Equal(ManualTestOutcome.Blocked, model.DraftOutcome);
        Assert.Empty(model.DraftValues); // the values were the comparison's, not the tester's

        model.SetDraftValues("F_dyn = 3.9 kN (read by eye)");
        kit.Worksheet.RaiseFormChanged();
        Assert.Equal("F_dyn = 3.9 kN (read by eye)", model.DraftValues);
        await model.RecordAsync();
        Assert.Equal(ManualTestOutcome.Blocked, model.Recorded!.Outcome);
    }

    [Fact]
    public async Task Recording_Reads_The_Form_Again_And_Refuses_A_Compare_Tick_Whose_Form_Has_Moved_On()
    {
        await using var kit = await ComparedAsync();
        var model = kit.Model;

        // The form now gives other numbers although no change event arrived.
        var other = FakeWorksheet.Engine(model.Current!);
        kit.Worksheet.Shown[BearingTemplate] = other with { Outputs = [.. other.Outputs.Select(o => o.Symbol == "F_dyn" ? o with { Value = new Tempest.Calc.Engine.Values.ScalarValue(4000, o.Value.Dimension, o.Value.DisplayUnit) } : o)] };
        await model.RecordAsync();

        Assert.Contains("no longer shows what the comparison read", model.Status, StringComparison.Ordinal);
        Assert.Contains("nothing was recorded", model.Status, StringComparison.Ordinal);
        Assert.Null(model.Recorded);
        Assert.Null(model.DraftOutcome);
        Assert.Empty(model.DraftValues);
        Assert.Equal($"0 pass, 0 fail, 0 blocked, 0 skipped, {GuidedKit.ScriptSteps} not run", model.TallyText);

        // Pressing Record again records nothing either: the stale tick was taken back, not just refused once.
        await model.RecordAsync();
        Assert.Equal("Tick Pass, Fail, Blocked or Skipped first.", model.Status);
    }

    [Fact]
    public async Task Recording_Refuses_A_Compare_Tick_When_The_Form_Gives_No_Run_At_All()
    {
        await using var kit = await ComparedAsync();
        var model = kit.Model;
        kit.Worksheet.Shown.Clear();

        await model.RecordAsync();

        Assert.Contains("no longer holds this example's inputs", model.Status, StringComparison.Ordinal);
        Assert.Contains("Nothing was recorded", model.Status, StringComparison.Ordinal);
        Assert.Null(model.Recorded);
    }

    [Fact]
    public async Task A_Compare_Tick_Over_An_Unchanged_Form_Is_Recorded_With_Its_Values()
    {
        await using var kit = await ComparedAsync();
        var model = kit.Model;

        await model.RecordAsync();

        Assert.Equal(ManualTestOutcome.Pass, model.Recorded!.Outcome);
        Assert.Contains("F_dyn = 3.930 kN", model.Recorded.ValuesShown, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- M3: a failure is a sentence, not a crash

    [Fact]
    public async Task A_Store_That_Fails_Is_Reported_In_The_Status_By_Every_Action_That_Uses_It()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();

        // A second panel over the same store sees the unfinished run to resume.
        var other = kit.Restart();
        await other.RefreshAsync();
        Assert.NotNull(other.Resumable);

        await kit.BreakStoreAsync();

        await model.RecordAsync(); // (nothing ticked yet)
        model.SetDraftOutcome(ManualTestOutcome.Pass);
        await model.RecordAsync();
        Assert.StartsWith("Could not record the result:", model.Status, StringComparison.Ordinal);

        await model.FinishAsync();
        Assert.StartsWith("Could not finish the test run:", model.Status, StringComparison.Ordinal);

        await other.ResumeAsync();
        Assert.StartsWith("Could not resume the test run:", other.Status, StringComparison.Ordinal);

        await model.StartNewAsync();
        Assert.StartsWith("Could not start a new test run:", model.Status, StringComparison.Ordinal);

        await model.DeleteOlderRunsAsync();
        Assert.StartsWith("Could not delete the older test runs:", model.Status, StringComparison.Ordinal);

        await model.RefreshAsync();
        Assert.StartsWith("The test runs on file could not be read:", model.Status, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_Start_Button_On_A_Failing_Store_Says_So_And_The_Application_Carries_On()
    {
        await using var kit = GuidedKit.Create();
        var view = new GuidedTestView(kit.Model);
        new Window { Width = 700, Height = 1000, Content = view }.Show();
        await kit.BreakStoreAsync();

        await Click(view, GuidedTestView.StartCaption);

        Assert.StartsWith("Could not start a new test run:", Named<TextBlock>(view, "Guided test status").Text, StringComparison.Ordinal);
        Assert.Null(kit.Model.Run);
    }

    [Fact]
    public async Task A_Results_Sheet_That_Cannot_Be_Saved_Is_A_Sentence_And_One_That_Can_Is_Written_Whole()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.SaveResultsAsync(_ => Task.CompletedTask, "x.md");
        Assert.Equal("Start or resume a run first.", model.Status);

        await model.StartNewAsync();
        var folder = Path.Combine(Path.GetTempPath(), "tempest-guided-save-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(folder);

        try
        {
            await model.SaveResultsAsync(sheet => ManualTestMarkdown.WriteFileAsync(Path.Combine(folder, "no-such-folder", "sheet.md"), sheet), "sheet.md");
            Assert.StartsWith("Could not save the results sheet:", model.Status, StringComparison.Ordinal);

            var path = Path.Combine(folder, "sheet.md");
            await model.SaveResultsAsync(sheet => ManualTestMarkdown.WriteFileAsync(path, sheet), "sheet.md");
            Assert.Equal("Saved the results sheet as 'sheet.md'.", model.Status);
            Assert.StartsWith("# TempestOS guided test results", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Any_Action_Run_Through_The_Guard_Turns_An_Exception_Into_A_Sentence()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        var changed = 0;
        model.Changed += () => changed++;

        await model.RunGuardedAsync(() => throw new IOException("The disk is full."), "copy the results sheet");

        Assert.Equal("Could not copy the results sheet: The disk is full.", model.Status);
        Assert.True(changed > 0);
        await Assert.ThrowsAsync<OperationCanceledException>(() => model.RunGuardedAsync(() => throw new OperationCanceledException(), "x"));
    }

    [Fact]
    public async Task A_Worksheet_That_Throws_When_Opened_Is_A_Sentence_And_Leaves_Nothing_To_Compare()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        model.Select(BearingExample);
        kit.Worksheet.PrepareFailure = new InvalidOperationException("The form is broken.");

        model.OpenWorksheet();

        Assert.Equal("Could not open the worksheet: The form is broken.", model.Status);
        model.AutoCompare();
        Assert.Contains("Open the worksheet for this step first", model.Status, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- L4: signed in

    [Fact]
    public async Task A_Run_Is_Not_Started_When_Nobody_Is_Signed_In_And_The_Panel_Says_To_Sign_In()
    {
        await using var kit = GuidedKit.Create();
        kit.TesterId = string.Empty;
        kit.TesterName = string.Empty;

        await kit.Model.StartNewAsync();

        Assert.Null(kit.Model.Run);
        Assert.Contains("sign in first", kit.Model.Status, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- L5: a long note is counted, not cut

    [Fact]
    public async Task A_Note_Over_The_Cap_Is_Kept_Whole_Counted_And_Refused_On_Recording_Until_It_Is_Shortened()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();
        Assert.Equal("0 of 2,000 characters", model.NoteCounterText);

        model.SetDraftNote(new string('n', 1_234));
        Assert.Equal("1,234 of 2,000 characters", model.NoteCounterText);
        Assert.False(model.NoteTooLong);

        var pasted = new string('p', 2_500);
        model.SetDraftNote(pasted);
        Assert.Equal(pasted, model.DraftNote); // nothing was cut
        Assert.True(model.NoteTooLong);
        Assert.Contains("500 over the 2,000", model.NoteCounterText, StringComparison.Ordinal);

        model.SetDraftOutcome(ManualTestOutcome.Pass);
        await model.RecordAsync();
        Assert.Contains("2500", model.Status.Replace(",", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("2000", model.Status.Replace(",", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Null(model.Recorded);
        Assert.Equal(pasted, model.DraftNote);

        model.SetDraftNote(pasted[..ManualTestRun.MaxNoteLength]);
        await model.RecordAsync();
        Assert.Equal(ManualTestRun.MaxNoteLength, model.Recorded!.Note.Length);
    }

    [Fact]
    public async Task The_Note_Box_Itself_Has_A_Cap_Far_Above_A_Note_And_Says_When_It_Leaves_Text_Out()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;

        model.SetDraftNote(new string('x', GuidedTestViewModel.MaxDraftNoteLength + 10));

        Assert.Equal(GuidedTestViewModel.MaxDraftNoteLength, model.DraftNote.Length);
        Assert.Contains("20,000", model.Status, StringComparison.Ordinal);
        Assert.True(GuidedTestViewModel.MaxDraftNoteLength >= 5 * ManualTestRun.MaxNoteLength);
    }

    [AvaloniaFact]
    public async Task A_Note_Pasted_Into_The_Box_Keeps_Every_Character_And_Shows_The_Counter()
    {
        await using var kit = GuidedKit.Create();
        var view = new GuidedTestView(kit.Model);
        new Window { Width = 700, Height = 1000, Content = view }.Show();
        await Click(view, GuidedTestView.StartCaption);

        Named<TextBox>(view, "Result note").Text = new string('q', 2_600);
        await Settle();

        Assert.Equal(2_600, kit.Model.DraftNote.Length);
        Assert.Equal(2_600, Named<TextBox>(view, "Result note").Text!.Length);
        Assert.Contains("600 over the 2,000", Named<TextBlock>(view, "Note length").Text, StringComparison.Ordinal);

        Named<RadioButton>(view, "Pass").IsChecked = true;
        await Settle();
        await Click(view, GuidedTestView.RecordCaption);
        Assert.Contains("a note can hold is 2000", Named<TextBlock>(view, "Guided test status").Text, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- L6: the allowance applied

    [Fact]
    public async Task After_A_Compare_Each_Number_Shows_The_Allowance_That_Was_Applied()
    {
        await using var kit = await ComparedAsync();
        var lines = kit.Model.ExpectedLines;

        Assert.Equal("±0.005", lines.Single(l => l.Symbol == "F_dyn").Applied);
        Assert.Equal("±0.5", lines.Single(l => l.Symbol == "L_10h").Applied);
        Assert.Null(lines.Single(l => l.Symbol == "governing check").Applied);

        // Before a compare nothing was applied yet.
        kit.Worksheet.RaiseFormChanged();
        Assert.All(kit.Model.ExpectedLines, l => Assert.Null(l.Applied));
    }

    [AvaloniaFact]
    public async Task The_Panel_Prints_The_Allowance_Applied_Beside_What_The_Form_Shows()
    {
        await using var kit = GuidedKit.Create();
        var view = new GuidedTestView(kit.Model);
        new Window { Width = 700, Height = 1000, Content = view }.Show();
        await Click(view, GuidedTestView.StartCaption);
        kit.Model.Select(BearingExample);
        kit.Worksheet.Shown[BearingTemplate] = FakeWorksheet.Engine(kit.Model.Current!);
        await Settle();

        await Click(view, GuidedTestView.OpenWorksheetCaption);
        await Click(view, GuidedTestView.CompareCaption);

        Assert.Contains(Named<StackPanel>(view, "Expected outputs").GetLogicalDescendants().OfType<TextBlock>(), t => t.Text is { } s && s.Contains("allowance applied ±0.005", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- L7: the draft survives leaving and coming back

    [Fact]
    public async Task Coming_Back_To_The_Screen_Keeps_The_Tick_Note_And_Values_Not_Yet_Recorded_For_The_Same_Step()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();
        model.Next();
        var step = model.Current!.Id;
        model.SetDraftOutcome(ManualTestOutcome.Fail);
        model.SetDraftNote("Half written.");
        model.SetDraftValues("F_dyn = 3.9");

        await model.RefreshAsync(); // what the Guided test node does each time it is shown

        Assert.Equal(step, model.Current!.Id);
        Assert.Equal(ManualTestOutcome.Fail, model.DraftOutcome);
        Assert.Equal("Half written.", model.DraftNote);
        Assert.Equal("F_dyn = 3.9", model.DraftValues);

        // A step with nothing recorded is not changed by looking at the same step again (Next on the last step stays on it).
        model.Select(step);
        Assert.Equal("Half written.", model.DraftNote);
    }

    [Fact]
    public async Task A_Draft_Is_Not_Carried_To_Another_Step_And_A_New_Run_Starts_Its_Step_Afresh()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();
        model.SetDraftNote("For step one.");

        model.Next();
        await model.RefreshAsync();
        Assert.Equal(string.Empty, model.DraftNote);

        model.Previous();
        Assert.Equal(string.Empty, model.DraftNote); // moving away dropped the unrecorded draft of step one, as before

        model.SetDraftNote("Unrecorded draft.");
        model.SetDraftOutcome(ManualTestOutcome.Skipped);
        await model.FinishAsync();
        await model.StartNewAsync();
        Assert.Equal(string.Empty, model.DraftNote);
        Assert.Null(model.DraftOutcome);
    }

    // ---------------------------------------------------------------- L11: deleting older finished runs

    private static async Task<List<Guid>> FinishedRunsAsync(GuidedKit kit, int count)
    {
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var run = await kit.Service.StartAsync();
            ids.Add((await kit.Service.FinishAsync(run.Id)).Id);
            await Task.Delay(5); // the runs are told apart by their times
        }

        return ids;
    }

    [Fact]
    public async Task Older_Finished_Runs_Are_Deleted_Only_After_The_Confirmation_And_The_Newest_Are_Kept()
    {
        await using var kit = GuidedKit.Create();
        var asked = new List<(string Title, string Message, string Confirm)>();
        var answer = false;
        var model = new GuidedTestViewModel(kit.Service, kit.Worksheet, confirm: (title, message, ok) =>
        {
            asked.Add((title, message, ok));
            return Task.FromResult(answer);
        }, keepFinishedRuns: 2);
        var ids = await FinishedRunsAsync(kit, 5);
        var open = await kit.Service.StartAsync();

        await model.RefreshAsync();
        Assert.Equal(5, model.FinishedRuns);
        Assert.True(model.CanDeleteOlderRuns);

        // Declined: nothing changes.
        await model.DeleteOlderRunsAsync();
        var question = Assert.Single(asked);
        Assert.Contains("Delete 3 finished test run(s) older than the newest 2", question.Message, StringComparison.Ordinal);
        Assert.Contains("cannot be undone", question.Message, StringComparison.Ordinal);
        Assert.Contains("not finished are never deleted", question.Message, StringComparison.Ordinal);
        Assert.Equal("Delete", question.Confirm);
        Assert.Equal("Nothing was deleted.", model.Status);
        Assert.Equal(6, (await kit.Service.ListAsync()).Count);

        // Confirmed.
        answer = true;
        await model.DeleteOlderRunsAsync();
        Assert.StartsWith("Deleted 3 older finished test run(s)", model.Status, StringComparison.Ordinal);
        Assert.Equal(new HashSet<Guid>([ids[3], ids[4], open.Id]), (await kit.Service.ListAsync()).Select(r => r.Id).ToHashSet());
        Assert.Equal(2, model.FinishedRuns);
        Assert.False(model.CanDeleteOlderRuns);

        await model.DeleteOlderRunsAsync();
        Assert.Contains("no older finished runs to delete", model.Status, StringComparison.Ordinal);
        Assert.Equal(2, asked.Count);
    }

    [Fact]
    public async Task Without_The_Apps_Confirmation_Dialog_Nothing_Is_Deleted()
    {
        await using var kit = GuidedKit.Create();
        var model = new GuidedTestViewModel(kit.Service, kit.Worksheet, keepFinishedRuns: 1);
        await FinishedRunsAsync(kit, 3);

        await model.DeleteOlderRunsAsync();

        Assert.Contains("confirmation dialog", model.Status, StringComparison.Ordinal);
        Assert.Equal(3, (await kit.Service.ListAsync()).Count);
    }

    [Fact]
    public async Task The_Open_Run_Is_Closed_On_The_Panel_When_It_Is_One_Of_The_Runs_Deleted()
    {
        await using var kit = GuidedKit.Create();
        var model = new GuidedTestViewModel(kit.Service, kit.Worksheet, confirm: (_, _, _) => Task.FromResult(true), keepFinishedRuns: 1);
        await model.StartNewAsync();
        await model.FinishAsync();
        await FinishedRunsAsync(kit, 2);
        Assert.Equal(model.Run!.Id, (await kit.Service.ListAsync()).Last().Id);

        await model.DeleteOlderRunsAsync();

        Assert.Null(model.Run);
        Assert.Equal("No run is open.", model.RunText);
        Assert.Single(await kit.Service.ListAsync());
    }

    [AvaloniaFact]
    public async Task The_Delete_Button_Appears_Only_With_More_Finished_Runs_Than_Are_Kept_And_Asks_Through_The_Apps_Dialog()
    {
        await using var kit = GuidedKit.Create();
        var dialog = new ConfirmationDialog();
        var model = new GuidedTestViewModel(kit.Service, kit.Worksheet, confirm: dialog.ConfirmAsync, keepFinishedRuns: 1);
        var view = new GuidedTestView(model);
        var host = new Grid();
        host.Children.Add(view);
        host.Children.Add(dialog);
        new Window { Width = 800, Height = 1000, Content = host }.Show();

        await FinishedRunsAsync(kit, 1);
        await model.RefreshAsync();
        await Settle();
        Assert.False(Named<Button>(view, GuidedTestView.DeleteOlderCaption).IsVisible);

        await FinishedRunsAsync(kit, 2);
        await model.RefreshAsync();
        await Settle();
        Assert.True(Named<Button>(view, GuidedTestView.DeleteOlderCaption).IsVisible);

        await Click(view, GuidedTestView.DeleteOlderCaption);
        Assert.True(dialog.IsVisible, "The app's confirmation dialog is shown before anything is deleted.");
        Assert.Equal(3, (await kit.Service.ListAsync()).Count);

        Named<Button>(dialog, "Delete").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Settle();

        Assert.False(dialog.IsVisible);
        Assert.Single(await kit.Service.ListAsync());
        Assert.False(Named<Button>(view, GuidedTestView.DeleteOlderCaption).IsVisible);
    }
}
