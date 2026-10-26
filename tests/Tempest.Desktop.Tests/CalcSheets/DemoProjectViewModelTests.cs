using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Library;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Tests.Quotations;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// Addendum C, C-6: Settings → Demo project. The menu actions "Install demo project" and "Remove demo project" ask in the application's own confirmation
/// step before they do anything, never run by themselves, say in a sentence what they did, and leave every real project as it was. The host is the real one
/// over a fresh SQLite database.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class DemoProjectViewModelTests
{
    /// <summary>A confirmation that answers as told and remembers what it was asked.</summary>
    private sealed class Asked(bool answer)
    {
        public List<(string Title, string Message, string Button)> Calls { get; } = [];

        public Task<bool> Confirm(string title, string message, string button)
        {
            Calls.Add((title, message, button));
            return Task.FromResult(answer);
        }
    }

    private static async Task<WorkspaceHost> StartAsync()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        return host;
    }

    private static T Resolve<T>(WorkspaceHost host) where T : class => (T)host.Services!.GetService(typeof(T));

    /// <summary>Every calculation, pack and demo scan-profile row, and every demo project, in the database now (a fresh host already holds a few sample calculations).</summary>
    private static async Task<(int Instances, int Packs, int DemoProfiles, int DemoProjects)> RowsAsync(WorkspaceHost host)
    {
        var store = Resolve<IQueryablePersistenceStore>(host);
        var projects = await host.ProjectDirectory!.ListAsync();
        return (
            (await store.ListKeysAsync(CalcInstanceStore.InstanceCollection, string.Empty)).Count,
            (await store.ListKeysAsync(CalcInstanceStore.PackCollection, string.Empty)).Count,
            (await store.ListKeysAsync(CalcInstanceStore.ScanProfileCollection, CalcDemoProject.ScanProfilePrefix)).Count,
            projects.Count(p => string.Equals(p.Identifier, CalcDemoProject.Identifier, StringComparison.OrdinalIgnoreCase)));
    }

    private static Button? FindButton(Control root, string name) =>
        root.GetLogicalDescendants().OfType<Button>().SingleOrDefault(b => AutomationProperties.GetName(b) == name);

    private static async Task Settle()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    [AvaloniaFact]
    public async Task Starting_On_An_Empty_Database_Installs_Nothing_And_Offers_Only_The_Install()
    {
        var host = await StartAsync();

        try
        {
            Assert.NotNull(host.CalcDemo);
            var baseline = await RowsAsync(host);
            Assert.Equal((0, 0), (baseline.DemoProfiles, baseline.DemoProjects));
            Assert.DoesNotContain(await host.CalcInstances!.ListScanProfilesAsync(), p => p.Id.StartsWith(CalcDemoProject.ScanProfilePrefix, StringComparison.Ordinal));

            var status = await host.CalcDemo!.StatusAsync();
            Assert.False(status.Installed);
            Assert.Null(status.InstallRefusal);

            var model = new DemoProjectViewModel(host.CalcDemo, new Asked(true).Confirm);
            await model.RefreshAsync();

            Assert.False(model.IsInstalled);
            Assert.True(model.CanInstall);
            Assert.False(model.CanRemove);
            Assert.Equal("The demo project is not installed.", model.Status);

            // Looking at the section and refreshing it twice made nothing.
            await model.RefreshAsync();
            Assert.Equal(baseline, await RowsAsync(host));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task The_Install_Asks_First_Waits_For_The_Answer_And_A_No_Installs_Nothing()
    {
        var host = await StartAsync();

        try
        {
            var baseline = await RowsAsync(host);
            var gate = new TaskCompletionSource<bool>();
            var asked = new List<(string Title, string Message, string Button)>();
            var outcomes = new List<(string Message, ActionOutcome Outcome)>();
            var model = new DemoProjectViewModel(host.CalcDemo!, (title, message, button) =>
            {
                asked.Add((title, message, button));
                return gate.Task;
            });
            model.Completed += (message, outcome) => outcomes.Add((message, outcome));

            var running = model.InstallAsync();
            await Settle();

            // The person has been asked, in the application's own dialog, and nothing has happened while they decide.
            var question = Assert.Single(asked);
            Assert.Equal("Install demo project", question.Title);
            Assert.Equal("Install", question.Button);
            Assert.Contains("Demo — Hydraulic Press Cell", question.Message, StringComparison.Ordinal);
            Assert.Contains("invented", question.Message, StringComparison.Ordinal);
            Assert.False(running.IsCompleted);
            Assert.Equal(baseline, await RowsAsync(host));
            Assert.Empty(outcomes);

            // While the person decides the actions are off, so a second click cannot ask a second time.
            Assert.True(model.IsBusy);
            Assert.False(model.CanInstall);
            Assert.False(model.CanRemove);
            await model.InstallAsync();
            await model.RemoveAsync();
            Assert.Single(asked);

            gate.SetResult(false);
            await running;
            Assert.False(model.IsBusy);
            Assert.True(model.CanInstall);

            Assert.Equal(baseline, await RowsAsync(host));
            Assert.False(model.IsInstalled);
            Assert.Equal("Install demo project was cancelled.", model.Status);
            var done = Assert.Single(outcomes);
            Assert.Equal(ActionOutcome.NoChange, done.Outcome);

            // Remove with nothing installed does not even ask.
            asked.Clear();
            await model.RemoveAsync();
            Assert.Empty(asked);
            Assert.Equal("There is no demo project to remove.", model.Status);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task The_Menu_Actions_Install_The_Demo_Beside_A_Real_Project_And_Remove_Only_The_Demo()
    {
        var host = await StartAsync();

        try
        {
            // A real project with a real calculation, made the ordinary way.
            var real = await host.ProjectDirectory!.CreateAsync("REAL0-JOB001", "A customer's job");
            var service = host.CalcInstances!;
            var realCalc = await service.CreateAsync("mech.shaft", real.Id, "Real shaft", null);
            var realBefore = await service.Store.FindInstanceAsync(realCalc.Id);

            var yes = new Asked(true);
            var no = new Asked(false);
            var open = (Guid?)null;
            var outcomes = new List<(string Message, ActionOutcome Outcome)>();
            var model = new DemoProjectViewModel(host.CalcDemo!, yes.Confirm, id => open == id);
            model.Completed += (message, outcome) => outcomes.Add((message, outcome));

            // The menu action: asked, answered yes, installed on the pool, reported as a change of the workspace.
            await model.InstallAsync();
            var installed = Assert.Single(outcomes);
            Assert.True(installed.Outcome.Succeeded, installed.Message);
            Assert.True(installed.Outcome.WorkspaceChanged);
            Assert.Contains("Installed the demo project 'Demo — Hydraulic Press Cell'", installed.Message, StringComparison.Ordinal);
            Assert.Contains("60 calculations in 13 packs", installed.Message, StringComparison.Ordinal);
            Assert.Equal("Install demo project", Assert.Single(yes.Calls).Title);
            Assert.True(model.IsInstalled);
            Assert.False(model.CanInstall);
            Assert.True(model.CanRemove);

            // It is a project like any other: listed beside the real one, flagged, every calculation DEMO and current.
            var projects = await host.ProjectDirectory.ListAsync();
            Assert.Contains(projects, p => p.Identifier == CalcDemoProject.Identifier && p.DisplayName == CalcDemoProject.DisplayName);
            Assert.Contains(projects, p => p.Id == real.Id);
            var demoId = model.ProjectId!.Value;
            var demoCalcs = await service.Store.ListInstancesAsync(demoId);
            Assert.Equal(60, demoCalcs.Count);
            Assert.All(demoCalcs, c => Assert.StartsWith("DEMO — ", c.Name, StringComparison.Ordinal));
            Assert.All(demoCalcs, c => Assert.Empty(c.Blocks));
            Assert.DoesNotContain(true, (await service.StalenessForProjectAsync(demoId)).Values.Select(s => s.IsStale));
            Assert.Equal(CalcDemoProject.Watermark, (await service.SheetAsync(demoCalcs[0])).Header.Watermark);
            Assert.Null((await service.SheetAsync((await service.Store.FindInstanceAsync(realCalc.Id))!)).Header.Watermark);

            // An export of a demo calculation (the PDF the Desktop makes) says DEMO, in the header band, the notice and the page footer; a real one does not.
            var pdf = PdfTextExtractor.ExtractText(host.CalcSheetRenderer!.Render(await service.SheetAsync(demoCalcs[0])).ToArray());
            Assert.Contains("DEMO", pdf, StringComparison.Ordinal);
            Assert.Contains("CALCULATION SHEET — DRAFT — DEMO", pdf, StringComparison.Ordinal);
            Assert.Contains(CalcDemoProject.Watermark, pdf, StringComparison.Ordinal);
            var realPdf = PdfTextExtractor.ExtractText(host.CalcSheetRenderer.Render(await service.SheetAsync((await service.Store.FindInstanceAsync(realCalc.Id))!)).ToArray());
            Assert.DoesNotContain("DEMO", realPdf, StringComparison.Ordinal);

            // A second install is refused in a sentence, without asking.
            yes.Calls.Clear();
            await model.InstallAsync();
            Assert.Empty(yes.Calls);
            Assert.Contains("installed already", model.Status, StringComparison.Ordinal);
            Assert.False(outcomes[^1].Outcome.Succeeded);

            // Settings → Demo project: the two actions, enabled by state.
            var settingsProvider = Resolve<Tempest.Core.Settings.ISettingsProvider>(host);
            var configuration = Resolve<Tempest.Core.Configuration.IConfigurationProvider>(host);
            var view = new SettingsView(new ThemeService(settingsProvider), new UserSettings(settingsProvider), settingsProvider, configuration, "(test)", demoProject: model);
            view.ShowPage(SettingsPage.System);
            await view.RefreshAsync();
            await Settle();
            var install = FindButton(view, "Install demo project");
            var remove = FindButton(view, "Remove demo project");
            Assert.NotNull(install);
            Assert.NotNull(remove);
            Assert.False(install!.IsEnabled);
            Assert.True(remove!.IsEnabled);

            // Remove is refused while the demo is the open project, and asks nothing.
            open = demoId;
            yes.Calls.Clear();
            await model.RemoveAsync();
            Assert.Empty(yes.Calls);
            Assert.Contains("Close the demo project", model.Status, StringComparison.Ordinal);
            open = null;

            // Remove, answered no: nothing is removed.
            var cautious = new DemoProjectViewModel(host.CalcDemo!, no.Confirm);
            await cautious.RemoveAsync();
            var warning = Assert.Single(no.Calls);
            Assert.Equal("Remove demo project", warning.Title);
            Assert.Equal("Remove", warning.Button);
            Assert.Contains("No other project is touched", warning.Message, StringComparison.Ordinal);
            Assert.Equal("Remove demo project was cancelled.", cautious.Status);
            Assert.Equal(60, (await service.Store.ListInstancesAsync(demoId)).Count);

            // Remove, answered yes: the demo goes, the real project and its calculation do not move.
            yes.Calls.Clear();
            await model.RemoveAsync();
            Assert.Equal("Remove demo project", Assert.Single(yes.Calls).Title);
            Assert.False(model.IsInstalled);
            Assert.True(model.CanInstall);
            Assert.Contains("Removed the demo project", outcomes[^1].Message, StringComparison.Ordinal);
            Assert.True(outcomes[^1].Outcome.WorkspaceChanged);
            Assert.Empty(await service.Store.ListInstancesAsync(demoId));
            Assert.Empty(await service.Store.ListPacksAsync(demoId));
            Assert.Empty(await service.ListScanProfilesAsync());

            // The audit trail, which a removal never touches, says what was made and what was deleted.
            var installedRow = Assert.Single(await host.AuditQuery!.QueryAsync(new Tempest.Core.Audit.AuditQueryCriteria(action: "Demo.Installed")));
            var removedRow = Assert.Single(await host.AuditQuery.QueryAsync(new Tempest.Core.Audit.AuditQueryCriteria(action: "Demo.Removed")));
            Assert.Equal("60", installedRow.Detail["Calculations"]);
            Assert.Equal(CalcDemoProject.Identifier, removedRow.Detail["Project"]);
            Assert.Equal("60", removedRow.Detail["Calculations"]);
            Assert.Equal("13", removedRow.Detail["Packs"]);

            var realAfter = await service.Store.FindInstanceAsync(realCalc.Id);
            Assert.Equal(CalcInstanceStore.Serialise(realBefore!), CalcInstanceStore.Serialise(realAfter!));
            Assert.False(((IDeletable)(await Resolve<EngineeringDomainContext>(host).Repository.FindAsync(real.Id))!).IsDeleted);
            Assert.True(((IDeletable)(await Resolve<EngineeringDomainContext>(host).Repository.FindAsync(demoId))!).IsDeleted);

            await view.RefreshAsync();
            await Settle();
            Assert.True(FindButton(view, "Install demo project")!.IsEnabled);
            Assert.False(FindButton(view, "Remove demo project")!.IsEnabled);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Real_Project_That_Holds_The_Demo_Number_Is_Refused_In_A_Sentence_Without_Asking_And_Is_Never_Removed()
    {
        var host = await StartAsync();

        try
        {
            var real = await host.ProjectDirectory!.CreateAsync(CalcDemoProject.Identifier, "A customer's real job");
            var yes = new Asked(true);
            var model = new DemoProjectViewModel(host.CalcDemo!, yes.Confirm);
            var baseline = await RowsAsync(host);

            await model.InstallAsync();

            Assert.Empty(yes.Calls);
            Assert.Contains("not the demo project", model.Status, StringComparison.Ordinal);
            Assert.False(model.IsInstalled);

            await model.RemoveAsync();

            Assert.Empty(yes.Calls);
            Assert.Equal("There is no demo project to remove.", model.Status);
            Assert.Equal(baseline, await RowsAsync(host));
            Assert.False(((IDeletable)(await Resolve<EngineeringDomainContext>(host).Repository.FindAsync(real.Id))!).IsDeleted);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>A demo service that says it is installed and whose removal does whatever the test says: what a store failing under the real one looks like from the screen.</summary>
    private sealed class ScriptedDemo(WorkspaceHost host, Func<Task<CalcDemoOutcome>> remove)
        : CalcDemoProjectService(host.CalcInstances!, Resolve<EngineeringDomainContext>(host), [])
    {
        public override Task<CalcDemoStatus> StatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CalcDemoStatus(true, Guid.NewGuid(), "installed already"));

        public override Task<CalcDemoOutcome> RemoveAsync(CancellationToken cancellationToken = default) => remove();
    }

    [AvaloniaFact]
    public async Task A_Removal_That_Fails_In_The_Engineering_Domain_Is_Said_In_A_Sentence_And_The_Section_Refreshes_And_Reports()
    {
        var host = await StartAsync();

        try
        {
            var outcomes = new List<(string Message, ActionOutcome Outcome)>();
            var model = new DemoProjectViewModel(new ScriptedDemo(host, () => throw new EngineeringDomainException("The object could not be marked deleted")), new Asked(true).Confirm);
            model.Completed += (message, outcome) => outcomes.Add((message, outcome));

            // Nothing escapes: the exception is a sentence, the section is not left busy, and the shell is told (so it refreshes what the act may have changed).
            await model.RemoveAsync();

            var done = Assert.Single(outcomes);
            Assert.False(done.Outcome.Succeeded);
            Assert.True(done.Outcome.WorkspaceChanged);
            Assert.Contains("The object could not be marked deleted", done.Message, StringComparison.Ordinal);
            Assert.Equal(done.Message, model.Status);
            Assert.False(model.IsBusy);
            Assert.True(model.IsInstalled);
            Assert.True(model.CanRemove);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Removal_That_Stopped_Part_Way_Tells_The_Shell_The_Workspace_Changed_And_Says_What_To_Press()
    {
        var host = await StartAsync();

        try
        {
            var outcomes = new List<(string Message, ActionOutcome Outcome)>();
            var half = new CalcDemoOutcome(false, "The demo's calculations were removed; finishing the removal failed — press Remove demo project again.", 60, 13, PartlyDone: true);
            var model = new DemoProjectViewModel(new ScriptedDemo(host, () => Task.FromResult(half)), new Asked(true).Confirm);
            model.Completed += (message, outcome) => outcomes.Add((message, outcome));

            await model.RemoveAsync();

            var done = Assert.Single(outcomes);
            Assert.Equal(half.Message, done.Message);
            Assert.False(done.Outcome.Succeeded);
            Assert.True(done.Outcome.WorkspaceChanged);
            Assert.Equal(half.Message, model.Status);
            Assert.True(model.CanRemove);

            // A plain refusal changed nothing, so the shell is not told to rebuild.
            var refused = new CalcDemoOutcome(false, "The demo project was not removed: the evidence 'X' is issued, and issued evidence is never deleted.");
            var cautious = new DemoProjectViewModel(new ScriptedDemo(host, () => Task.FromResult(refused)), new Asked(true).Confirm);
            cautious.Completed += (message, outcome) => outcomes.Add((message, outcome));
            await cautious.RemoveAsync();
            Assert.Equal(ActionOutcome.Failed, outcomes[^1].Outcome);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task The_Settings_Section_Is_Absent_Unless_The_Host_Composed_The_Demo_Service()
    {
        var host = await StartAsync();

        try
        {
            var settingsProvider = Resolve<Tempest.Core.Settings.ISettingsProvider>(host);
            var configuration = Resolve<Tempest.Core.Configuration.IConfigurationProvider>(host);
            var view = new SettingsView(new ThemeService(settingsProvider), new UserSettings(settingsProvider), settingsProvider, configuration, "(test)");
            await view.RefreshAsync();

            Assert.Null(FindButton(view, "Install demo project"));
            Assert.Null(FindButton(view, "Remove demo project"));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
