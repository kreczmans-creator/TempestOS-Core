using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Library;
using Tempest.Calc.Library.Dashboard;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Addendum C, C-4: the screen itself, headless, and its place in Calculations in the real window.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class VerificationDashboardViewTests
{
    private static VerificationDashboardViewModel Model(StubFilePicker? picker = null) =>
        new(new VerificationDashboardService(new CalcCatalogue(CalcCalculatorComposition.All()), CalcPackTemplateComposition.All()), picker ?? new StubFilePicker(), new VerificationPdfRenderer(), () => "TempestOS test");

    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }
    }

    [AvaloniaFact]
    public async Task Shows_One_Named_Row_Per_Calculator_And_Chain_And_Filters_From_The_Boxes()
    {
        var model = Model();
        var view = new VerificationDashboardView(model);
        await view.RefreshAsync();

        var list = view.GetLogicalDescendants().OfType<StackPanel>().Single(p => AutomationProperties.GetName(p) == VerificationDashboardView.ListName);
        Assert.Equal(model.Rows.Count, list.Children.Count);
        Assert.Contains(list.Children, c => AutomationProperties.GetName(c) == "mech.shaft");
        Assert.Contains(list.Children, c => AutomationProperties.GetName(c) == "pack.drive-train");

        var texts = view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains(model.Rows.Single(r => r.Id == "mech.shaft").Title, texts);
        Assert.Contains($"{model.Rows.Single(r => r.Id == "pack.drive-train").Title} (chain)", texts);
        Assert.Contains(texts, t => t is not null && t.StartsWith("Match, with findings:", StringComparison.Ordinal));
        Assert.Contains("Not tested", texts);

        var discipline = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == VerificationDashboardView.DisciplineFilterName);
        discipline.SelectedItem = "Fluid power";
        Assert.Equal(3, list.Children.Count);
        Assert.Equal("Fluid power", model.DisciplineFilter);

        var status = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == VerificationDashboardView.StatusFilterName);
        status.SelectedItem = status.Items.OfType<ComboBoxItem>().Single(i => i.Tag is VerificationStatusFilter.Failing);
        Assert.Empty(list.Children);
        Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text is { } x && x.StartsWith("0 of ", StringComparison.Ordinal));

        var buttons = view.GetLogicalDescendants().OfType<Button>().ToDictionary(b => AutomationProperties.GetName(b)!);
        Assert.False(buttons[VerificationDashboardView.ExportPdfCaption].IsEnabled);
        Assert.False(buttons[VerificationDashboardView.ExportMarkdownCaption].IsEnabled);
        Assert.True(buttons[VerificationDashboardView.RefreshCaption].IsEnabled);
    }

    /// <summary>SEC-C-06: two latest results, one recorded on the running build and one on an older build.</summary>
    private sealed class TwoBuildRuns : Tempest.Calc.Library.ManualTesting.IManualTestStatusSource
    {
        public Task<IReadOnlyDictionary<string, Tempest.Calc.Library.ManualTesting.ManualTestStatus>> LatestByTemplateAsync(CancellationToken cancellationToken = default)
        {
            var at = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
            return Task.FromResult<IReadOnlyDictionary<string, Tempest.Calc.Library.ManualTesting.ManualTestStatus>>(new Dictionary<string, Tempest.Calc.Library.ManualTesting.ManualTestStatus>(StringComparer.Ordinal)
            {
                ["mech.shaft"] = new("mech.shaft", Tempest.Calc.Library.ManualTesting.ManualTestOutcome.Pass, at, "abc1234", "Tester", Guid.NewGuid(), 2, 2, "0.26.0 (abc1234)"),
                ["mech.spring"] = new("mech.spring", Tempest.Calc.Library.ManualTesting.ManualTestOutcome.Pass, at, "0000000", "Tester", Guid.NewGuid(), 2, 2, "0.26.0 (0000000)"),
            });
        }
    }

    [AvaloniaFact]
    public async Task The_Manual_Test_Column_Shows_The_Build_And_Flags_A_Result_From_Another_Build()
    {
        var running = new RunningBuild("0.26.0 (abc1234)", "abc1234");
        var service = new VerificationDashboardService(
            new CalcCatalogue(CalcCalculatorComposition.All()), CalcPackTemplateComposition.All(), manual: new GuidedManualTestStatusSource(new TwoBuildRuns(), () => running));
        var model = new VerificationDashboardViewModel(service, new StubFilePicker(), new VerificationPdfRenderer(), () => "TempestOS test");
        var view = new VerificationDashboardView(model);
        await view.RefreshAsync();

        var texts = view.GetLogicalDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(texts, t => t.Text == "Passed — 0.26.0 (abc1234)");
        var flagged = texts.Single(t => t.Text == "Passed — 0.26.0 (0000000) — recorded on a different build");
        Assert.NotNull(ToolTip.GetTip(flagged));
        Assert.Contains("different build", ToolTip.GetTip(flagged)!.ToString(), StringComparison.Ordinal);
        Assert.Equal(Avalonia.Media.Brushes.DarkOrange, flagged.Foreground);
        Assert.NotEqual(Avalonia.Media.Brushes.DarkOrange, texts.Single(t => t.Text == "Passed — 0.26.0 (abc1234)").Foreground);
        Assert.True(model.Rows.Single(r => r.Id == "mech.spring").ManualTest.RecordedOnOtherBuild);

        // The same wording reaches the PDF and the Markdown the owner keeps.
        var markdown = VerificationMarkdown.Write(model.Report!, model.Rows, string.Empty, "0.26.0");
        Assert.Contains("Passed — 0.26.0 \\(0000000\\) — recorded on a different build", markdown, StringComparison.Ordinal);
        Assert.True(new VerificationPdfRenderer().Render(model.Report!, model.Rows, string.Empty, "TempestOS test").Length > 1000);
    }

    [AvaloniaFact]
    public async Task The_Export_Buttons_Save_Through_The_Picker_And_The_Status_Line_Says_Where()
    {
        var picker = new StubFilePicker();
        var folder = Path.Combine(Path.GetTempPath(), "tempest-verification-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var model = Model(picker);
            var view = new VerificationDashboardView(model);
            await view.RefreshAsync();
            picker.SetNextSavePath(Path.Combine(folder, "records.md"));

            view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == VerificationDashboardView.ExportMarkdownCaption).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Settle();

            Assert.True(File.Exists(Path.Combine(folder, "records.md")));
            Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == $"Saved {model.Rows.Count} row(s) of the verification dashboard to records.md.");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [AvaloniaFact]
    public async Task Calculations_Has_A_Verification_Node_That_Lists_The_Rows_And_Changes_Nothing_In_The_Store()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            window.Show();
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            await Settle();

            var area = window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single();
            var recordsBefore = (await host.CalcInstances!.Store.ListRecordsAsync()).Count;
            area.SelectNode("Verification dashboard");
            await Settle();

            var view = window.GetLogicalDescendants().OfType<VerificationDashboardView>().Single();
            Assert.Equal(host.CalcCatalogue!.Current().Count + host.CalcInstances.PackTemplates.Count, view.Model.Rows.Count);
            Assert.Contains(window.GetLogicalDescendants().OfType<TreeViewItem>(), n => AutomationProperties.GetName(n) == "Verification dashboard");

            // The records lookup finds the kinds with no record entered; opening the screen entered none, seeded none, changed none.
            Assert.Contains(view.Model.Rows, r => r.Id == "elec.conductor-sizing" && r.MissingRecordKinds.Contains("Cable current-carrying capacities"));
            Assert.Equal(recordsBefore, (await host.CalcInstances.Store.ListRecordsAsync()).Count);

            // The rest of Calculations still opens beside it.
            area.SelectNode("Worksheets");
            await Settle();
            Assert.Single(window.GetLogicalDescendants().OfType<CalcSheetView>());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
