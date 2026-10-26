using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.GuidedTest;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests.GuidedTest;

/// <summary>The Guided test panel as a person uses it (buttons, list, result, note), and its place in the Engineering tree beside the one worksheet form.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class GuidedTestViewTests
{
    private const string BearingExample = "example:mech.bearing-life-basic/ball-bearing-combined-95";

    private static T Named<T>(Control root, string name)
        where T : Control => root.GetLogicalDescendants().OfType<T>().Single(c => AutomationProperties.GetName(c) == name);

    private static async Task Click(Control root, string caption)
    {
        var button = Named<Button>(root, caption);
        Assert.True(button.IsEnabled && button.IsEffectivelyVisible, $"'{caption}' is not available.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Settle();
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 12; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    private static (GuidedTestView View, Window Window) Show(GuidedTestViewModel model)
    {
        var view = new GuidedTestView(model);
        var window = new Window { Width = 700, Height = 1000, Content = view };
        window.Show();
        return (view, window);
    }

    [AvaloniaFact]
    public async Task Nothing_Can_Be_Recorded_Before_A_Run_Is_Started_And_The_Panel_Says_What_To_Do()
    {
        await using var kit = GuidedKit.Create();
        var (view, _) = Show(kit.Model);

        Assert.Equal("No run is open.", Named<TextBlock>(view, "Test run").Text);
        Assert.False(Named<Button>(view, GuidedTestView.RecordCaption).IsEnabled);
        Assert.False(Named<Button>(view, GuidedTestView.FinishCaption).IsEnabled);
        Assert.False(Named<Button>(view, GuidedTestView.CopyCaption).IsEnabled);
        Assert.False(Named<Button>(view, GuidedTestView.ResumeCaption).IsVisible);
        Assert.Contains("Start a run to begin", Named<TextBlock>(view, "Guided test status").Text, StringComparison.Ordinal);
        Assert.Contains("half a unit in the last digit", Named<TextBlock>(view, "Tolerance rule").Text, StringComparison.Ordinal);
        Assert.Equal(GuidedTestScript.Current.Steps.Count, Named<ListBox>(view, "Test steps").ItemCount);

        // The note box does not cut a pasted note at the 2,000 characters a recorded note can hold (it is counted, and refused when recorded).
        Assert.True(Named<TextBox>(view, "Result note").MaxLength > ManualTestRun.MaxNoteLength);
        Assert.Equal("0 of 2,000 characters", Named<TextBlock>(view, "Note length").Text);
    }

    [AvaloniaFact]
    public async Task A_Person_Starts_A_Run_Opens_A_Step_Ticks_A_Result_Writes_A_Note_And_Records_It()
    {
        await using var kit = GuidedKit.Create();
        var (view, _) = Show(kit.Model);

        await Click(view, GuidedTestView.StartCaption);
        Assert.Contains("Steven Kreczman", Named<TextBlock>(view, "Test run").Text, StringComparison.Ordinal);
        Assert.Equal($"0 pass, 0 fail, 0 blocked, 0 skipped, {GuidedKit.ScriptSteps} not run", Named<TextBlock>(view, "Run tally").Text);

        var steps = Named<ListBox>(view, "Test steps");
        kit.Model.Select(BearingExample);
        await Settle();
        Assert.Equal(kit.Model.CurrentIndex, steps.SelectedIndex);
        Assert.Contains("ball-bearing", Named<TextBlock>(view, "Step details").Text, StringComparison.Ordinal);
        Assert.Equal(kit.Model.ExpectedLines.Count, Named<StackPanel>(view, "Expected outputs").Children.Count);

        await Click(view, GuidedTestView.OpenWorksheetCaption);
        Assert.Equal("mech.bearing-life-basic", Assert.Single(kit.Worksheet.Prepared).Template);

        Named<RadioButton>(view, "Fail").IsChecked = true;
        Named<TextBox>(view, "Result note").Text = "Shows 3.94 kN.";
        await Settle();
        Assert.Equal(ManualTestOutcome.Fail, kit.Model.DraftOutcome);
        Assert.Equal("Shows 3.94 kN.", kit.Model.DraftNote);

        await Click(view, GuidedTestView.RecordCaption);
        Assert.Equal($"0 pass, 1 fail, 0 blocked, 0 skipped, {GuidedKit.ScriptSteps - 1} not run", Named<TextBlock>(view, "Run tally").Text);
        Assert.StartsWith("Recorded: Fail", Named<TextBlock>(view, "Recorded result").Text, StringComparison.Ordinal);
        Assert.Contains("Recorded Fail", Named<TextBlock>(view, "Guided test status").Text, StringComparison.Ordinal);

        // The step list now carries the result, and moving away and back shows the note again.
        Assert.StartsWith("Fail", (string)steps.Items[kit.Model.CurrentIndex]!, StringComparison.Ordinal);
        await Click(view, GuidedTestView.NextCaption);
        Assert.False(Named<RadioButton>(view, "Fail").IsChecked);
        await Click(view, GuidedTestView.PreviousCaption);
        Assert.True(Named<RadioButton>(view, "Fail").IsChecked);
        Assert.Equal("Shows 3.94 kN.", Named<TextBox>(view, "Result note").Text);
    }

    [AvaloniaFact]
    public async Task Compare_Pre_Ticks_The_Result_In_The_Panel_But_Only_Recording_Keeps_It()
    {
        await using var kit = GuidedKit.Create();
        var (view, _) = Show(kit.Model);
        await Click(view, GuidedTestView.StartCaption);
        kit.Model.Select(BearingExample);
        kit.Worksheet.Shown["mech.bearing-life-basic"] = FakeWorksheet.Engine(kit.Model.Current!);
        await Settle();

        await Click(view, GuidedTestView.OpenWorksheetCaption);
        await Click(view, GuidedTestView.CompareCaption);

        Assert.True(Named<RadioButton>(view, "Pass").IsChecked);
        Assert.Contains("F_dyn = 3.930 kN", Named<TextBox>(view, "Values shown").Text, StringComparison.Ordinal);
        Assert.Null(kit.Model.Recorded);
        Assert.Equal($"0 pass, 0 fail, 0 blocked, 0 skipped, {GuidedKit.ScriptSteps} not run", Named<TextBlock>(view, "Run tally").Text);
        Assert.Contains(Named<StackPanel>(view, "Expected outputs").GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "agrees");

        await Click(view, GuidedTestView.RecordCaption);
        Assert.Equal($"1 pass, 0 fail, 0 blocked, 0 skipped, {GuidedKit.ScriptSteps - 1} not run", Named<TextBlock>(view, "Run tally").Text);
    }

    [AvaloniaFact]
    public async Task Finishing_The_Run_Switches_Recording_Off_And_Resume_Is_Offered_For_An_Unfinished_One()
    {
        await using var kit = GuidedKit.Create();
        var (view, _) = Show(kit.Model);
        await Click(view, GuidedTestView.StartCaption);

        // A second panel over the same store, as after a restart, is offered the unfinished run.
        var restarted = kit.Restart();
        var (second, _) = Show(restarted);
        await restarted.RefreshAsync();
        await Settle();
        Assert.True(Named<Button>(second, GuidedTestView.ResumeCaption).IsVisible);
        Assert.Contains("unfinished run", Named<TextBlock>(second, "Unfinished run").Text, StringComparison.Ordinal);

        await Click(second, GuidedTestView.ResumeCaption);
        Assert.False(Named<Button>(second, GuidedTestView.ResumeCaption).IsVisible);
        Assert.True(Named<Button>(second, GuidedTestView.RecordCaption).IsEnabled);

        await Click(second, GuidedTestView.FinishCaption);
        Assert.False(Named<Button>(second, GuidedTestView.RecordCaption).IsEnabled);
        Assert.False(Named<Button>(second, GuidedTestView.FinishCaption).IsEnabled);
        Assert.True(Named<Button>(second, GuidedTestView.CopyCaption).IsEnabled);
        Assert.Contains("finished 20", Named<TextBlock>(second, "Test run").Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_Filter_And_Search_Narrow_The_Step_List()
    {
        await using var kit = GuidedKit.Create();
        var (view, _) = Show(kit.Model);
        var steps = Named<ListBox>(view, "Test steps");

        Named<ComboBox>(view, "Step filter").SelectedItem = "8 · Lifting";
        await Settle();
        Assert.Equal(6, steps.ItemCount);

        Named<ComboBox>(view, "Step filter").SelectedItem = GuidedTestViewModel.AllSteps;
        Named<TextBox>(view, "Search steps").Text = "thermal-chain";
        await Settle();
        Assert.Equal(kit.Model.Rows.Count, steps.ItemCount);
        Assert.All(kit.Model.Rows, r => Assert.Contains("thermal-chain", r.Id, StringComparison.Ordinal));

        // The model trims the search; the box must not, or a second word could never be typed.
        Named<TextBox>(view, "Search steps").Text = "thermal ";
        await Settle();
        Assert.Equal("thermal ", Named<TextBox>(view, "Search steps").Text);
        Assert.Equal("thermal", kit.Model.Search);
    }

    [AvaloniaFact]
    public async Task The_Guided_Test_Is_A_Node_Under_Calculations_And_Shares_The_One_Worksheet_Form_With_The_Worksheets_Node()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            window.Show();
            var project = await host.ProjectDirectory!.CreateAsync("P-GUIDED-NAV", "Guided navigation project");
            await host.ProjectContext!.OpenAsync(project.Id);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            await Settle();

            var area = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single();

            area.SelectNode("Guided test");
            await Settle();
            var guided = window.GetLogicalDescendants().OfType<GuidedTestView>().Single();
            var form = window.GetLogicalDescendants().OfType<CalcSheetView>().Single();
            Assert.True(guided.IsEffectivelyVisible && form.IsEffectivelyVisible, "The panel and the form are shown together.");
            Assert.Equal("Guided test", AutomationProperties.GetName(guided));

            // Deleting runs asks through the app's confirmation dialog, which the composer hands the panel.
            Assert.NotNull(typeof(GuidedTestViewModel).GetField("_confirm", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(guided.Model));

            // A note written and not yet recorded is still there when the node is left and shown again (the node refreshes the panel each time).
            guided.Model.SetDraftNote("Written before leaving the node.");

            // The one form moves back to the Worksheets node on its own; the panel is no longer shown.
            area.SelectNode("Worksheets");
            await Settle();
            Assert.Empty(window.GetLogicalDescendants().OfType<GuidedTestView>());
            Assert.Same(form, window.GetLogicalDescendants().OfType<CalcSheetView>().Single());

            area.SelectNode("Calculation register");
            await Settle();
            area.SelectNode("Guided test");
            await Settle();
            Assert.Same(guided, window.GetLogicalDescendants().OfType<GuidedTestView>().Single());
            Assert.Same(form, window.GetLogicalDescendants().OfType<CalcSheetView>().Single());
            Assert.Equal("Written before leaving the node.", guided.Model.DraftNote);
            Assert.Equal("Written before leaving the node.", Named<TextBox>(guided, "Result note").Text);

            // Driven from the panel, the form beside it opens on the example's template.
            var coordinator = (CalcSheetCoordinator)typeof(MainWindow).GetField("_calcSheetCoordinator", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            Assert.NotNull(coordinator);
            await Click(guided, GuidedTestView.StartCaption);
            guided.Model.Select(BearingExample);
            await Click(guided, GuidedTestView.OpenWorksheetCaption);
            Assert.Equal("mech.bearing-life-basic", form.SelectedTemplate!.Template.Id);
            Assert.Null(form.CurrentInstance);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
