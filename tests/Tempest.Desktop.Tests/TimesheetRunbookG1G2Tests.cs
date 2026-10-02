using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Desktop.Documents.Timesheets;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Product Owner runbook feedback on Business → Timesheets:
/// G1 — the Record dialog's task is a drop-down of the chosen project's
/// own deliverables, refreshed when the project changes;
/// G2 — Settings → Timesheets → Timesheet export folder is where Export
/// week's save dialog starts, created if missing, and persists across a
/// restart.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class TimesheetRunbookG1G2Tests
{
    [AvaloniaFact]
    public async Task RecordDialog_TaskDropDown_ListsTheProjectsDeliverables_AndFollowsTheProject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var rateCards = (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));

            var withDeliverables = await host.ProjectDirectory!.CreateAsync("P-G1-A", "Alpha G1 Project");
            var withoutDeliverables = await host.ProjectDirectory!.CreateAsync("P-G1-B", "Bravo G1 Project");
            var first = await TimesheetTaskTestSupport.AddDeliverableAsync(host, withDeliverables.Id, "Concept design");
            var second = await TimesheetTaskTestSupport.AddDeliverableAsync(host, withDeliverables.Id, "Detailed design");
            await PinReleasedRateCardAsync(host, "RC-G1", withDeliverables.Id, withoutDeliverables.Id);

            var prompt = new TimesheetEntryPrompt(domain, rateCards);
            var window = new Window { Content = prompt };
            LayOut(window);

            var pending = prompt.PromptAsync();
            await RenderUntilAsync(window, () => prompt.IsVisible);

            var projectCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().First();
            await RenderUntilAsync(window, () => projectCombo.ItemsSource is not null && projectCombo.ItemsSource.Cast<ComboBoxItem>().Count() >= 2);

            // Alpha: both of its deliverables, by identifier and title.
            Select(projectCombo, withDeliverables.Id);
            await RenderUntilAsync(window, () => TimesheetTaskTestSupport.ListedDeliverables(prompt).Count == 2);
            Assert.Equal([first.Id, second.Id], TimesheetTaskTestSupport.ListedDeliverables(prompt).OrderBy(id => id == first.Id ? 0 : 1));
            var labels = TimesheetTaskTestSupport.TaskCombo(prompt).ItemsSource!.Cast<ComboBoxItem>().Select(i => (string)i.Content!).ToList();
            Assert.Contains(first.Label, labels);
            Assert.Contains(second.Label, labels);
            Assert.False(NoDeliverablesHint(prompt).IsVisible);

            // Bravo: none — an empty drop-down and the hint.
            Select(projectCombo, withoutDeliverables.Id);
            await RenderUntilAsync(window, () => TimesheetTaskTestSupport.ListedDeliverables(prompt).Count == 0 && NoDeliverablesHint(prompt).IsVisible);
            Assert.Empty(TimesheetTaskTestSupport.ListedDeliverables(prompt));
            Assert.True(NoDeliverablesHint(prompt).IsVisible);

            // Recording with no task chosen keeps the existing refusal.
            prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Record")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            LayOut(window);
            Assert.True(prompt.IsVisible);
            Assert.Contains(
                prompt.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.IsVisible && (t.Text ?? string.Empty).StartsWith("A task is required.", StringComparison.Ordinal));

            // Back to Alpha: the list follows the project again.
            Select(projectCombo, withDeliverables.Id);
            await RenderUntilAsync(window, () => TimesheetTaskTestSupport.ListedDeliverables(prompt).Count == 2);
            Assert.False(NoDeliverablesHint(prompt).IsVisible);

            prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Null(await pending);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ExportFolderSetting_IsWhereExportWeekStarts_AndPersistsAcrossRestart()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var exportFolder = Path.Combine(Path.GetTempPath(), $"tempest-timesheet-export-{Guid.NewGuid():N}", "02 Timesheets");

        try
        {
            var host = new WorkspaceHost(root);
            try
            {
                await host.StartAsync();
                var filePicker = new StubFilePicker();
                var window = new MainWindow(host, filePicker);
                LayOut(window);
                await RenderUntilAsync(window, () => window.Ready.IsCompleted);

                // Settings → Timesheets → Timesheet export folder, via Browse….
                var settingsView = await OpenSettingsAsync(host, window);
                var folderBox = ExportFolderBox(settingsView);
                Assert.Equal(string.Empty, folderBox.Text); // the test run switches the default off (TestTimesheetExportFolder)

                filePicker.SetNextFolder(exportFolder);
                settingsView.GetLogicalDescendants().OfType<Button>()
                    .Single(b => AutomationProperties.GetName(b) == "Browse for timesheet export folder")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await RenderUntilAsync(window, () => folderBox.Text == exportFolder);
                Assert.Equal(exportFolder, folderBox.Text);

                settingsView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await RenderUntilAsync(window, () => settingsView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("Saved at", StringComparison.Ordinal)));

                // Business → Timesheets → Export week starts in that folder, creating it.
                Assert.False(Directory.Exists(exportFolder));
                await host.ShellNavigator!.GoToModuleAsync(ShellArea.Business);
                await window.RenderCurrentModuleAsync();
                LayOut(window);
                window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Timesheets");
                await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<TimesheetWeekView>().Any());
                LayOut(window);
                var week = window.GetLogicalDescendants().OfType<TimesheetWeekView>().Single();

                filePicker.SetNextSavePath(null); // cancel — only the start folder matters here
                week.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Export week")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await RenderUntilAsync(window, () => filePicker.SaveRequests.Count > 0);

                Assert.Equal(exportFolder, filePicker.SaveRequests.Last().StartFolder);
                Assert.True(Directory.Exists(exportFolder));

                await host.ShutdownAsync();
            }
            finally
            {
                await host.DisposeAsync();
            }

            // Restart: the setting is still there.
            var second = new WorkspaceHost(root);
            try
            {
                await second.StartAsync();
                var window = new MainWindow(second, new StubFilePicker());
                LayOut(window);
                await RenderUntilAsync(window, () => window.Ready.IsCompleted);

                var settingsView = await OpenSettingsAsync(second, window);
                await RenderUntilAsync(window, () => ExportFolderBox(settingsView).Text == exportFolder);
                Assert.Equal(exportFolder, ExportFolderBox(settingsView).Text);

                await second.ShutdownAsync();
            }
            finally
            {
                await second.DisposeAsync();
            }
        }
        finally
        {
            var parent = Path.GetDirectoryName(exportFolder)!;
            if (Directory.Exists(parent))
                Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void ExportFolder_Resolution_PrefersTheUserChoice_ThenConfiguration_ThenTheWindowsDefault()
    {
        Assert.Equal(@"D:\11 Business Admin\02 Timesheets", TimesheetExportFolder.Fallback(null, isWindows: true));
        Assert.Null(TimesheetExportFolder.Fallback(null, isWindows: false));
        Assert.Equal("/chosen", TimesheetExportFolder.Resolve("/chosen", null, isWindows: true));
        Assert.Equal(@"D:\11 Business Admin\02 Timesheets", TimesheetExportFolder.Resolve("  ", null, isWindows: true));

        // A drive that does not exist (or a relative path) falls back to the picker's own start, saying why.
        var relative = TimesheetExportFolder.Prepare("relative/folder");
        Assert.Null(relative.Folder);
        Assert.NotNull(relative.Note);
        Assert.Equal(new TimesheetExportStart(null, null), TimesheetExportFolder.Prepare(null));
    }

    private static async Task PinReleasedRateCardAsync(WorkspaceHost host, string rateCardId, params Guid[] projectIds)
    {
        var rateCards = (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));
        var card = new RateCard
        {
            Code = rateCardId,
            Name = "Runbook G1 Rate Card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
            Currency = CurrencyCode.Gbp,
            Governance = new BusinessGovernanceFacts { Ownership = new BusinessOwnership("owner-1", "Owner") },
            Entries = [new RateCardEntry("SVC-1", "Senior Engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: "Senior")],
        };
        await rateCards.RegisterAsync(rateCardId, card, new ReferenceProvenance(SourceOrganisation: "Test Org", SourceDocument: "Test Doc"));
        await host.ReferenceReview!.VerifyAsync(rateCards, rateCardId, new ReferenceReviewStatement("Consulted for the runbook G1 test."));
        await host.ReferenceReview!.ReleaseAsync(rateCards, rateCardId, "Released for the runbook G1 test.");

        var commercial = (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));
        foreach (var projectId in projectIds)
            Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
    }

    private static async Task<SettingsView> OpenSettingsAsync(WorkspaceHost host, MainWindow window)
    {
        await host.ShellNavigator!.GoToModuleAsync(ShellArea.Settings);
        await window.RenderCurrentModuleAsync();
        LayOut(window);
        var settingsView = GetPrivateField<SettingsView>(window, "_settingsView");
        await RenderUntilAsync(window, () => settingsView.GetLogicalDescendants().OfType<TextBox>().Any(t => AutomationProperties.GetName(t) == "Timesheet export folder"));
        return settingsView;
    }

    private static TextBox ExportFolderBox(SettingsView settingsView) =>
        settingsView.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Timesheet export folder");

    private static TextBlock NoDeliverablesHint(TimesheetEntryPrompt prompt) =>
        prompt.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Text == TimesheetEntryPrompt.NoDeliverablesHint);

    private static void Select(ComboBox projectCombo, Guid projectId) =>
        projectCombo.SelectedItem = projectCombo.ItemsSource!.Cast<ComboBoxItem>().First(i => (Guid)i.Tag! == projectId);

    private static Task RenderUntilAsync(Window window, Func<bool> condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? what = null) =>
        DesktopTestHelpers.WaitUntilAsync(condition, 15, () => LayOut(window), null, what);

    private static void LayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1400, 900));
            window.Arrange(new Rect(0, 0, 1400, 900));
        }
    }
}
