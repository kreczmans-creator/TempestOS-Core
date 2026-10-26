using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.Calc.Library;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Core.Events;
using Tempest.Core.Persistence;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.GuidedTest;

namespace Tempest.Desktop.Tests.GuidedTest;

/// <summary>The Guided test against the real worksheet form: an example opens as a scratch sheet with its inputs, compares against the form's own run, and changes no calculation.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class GuidedTestWorksheetTests
{
    private const string BearingExample = "example:mech.bearing-life-basic/ball-bearing-combined-95";

    private sealed record Fixture(WorkspaceHost Host, CalcSheetView View, CalcSheetCoordinator Coordinator, GuidedTestViewModel Model, Guid ProjectId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Host.ShutdownAsync();
            await Host.DisposeAsync();
        }
    }

    private static async Task<Fixture> StartAsync(string code)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var project = await host.ProjectDirectory!.CreateAsync(code, code + " project");
        await host.ProjectContext!.OpenAsync(project.Id);
        var view = new CalcSheetView();
        var window = new Window { Width = 1600, Height = 1000, Content = view };
        window.Show();
        var coordinator = new CalcSheetCoordinator(host.CalcInstances!, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
        await coordinator.RefreshAsync();
        var model = new GuidedTestViewModel(host.ManualTests!, new CalcGuidedWorksheetHost(view, coordinator, host.CalcCatalogue!));
        return new Fixture(host, view, coordinator, model, project.Id);
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 12; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    [AvaloniaFact]
    public async Task The_Host_Composes_The_Run_Service_With_The_Build_And_The_Person_The_App_Signs_Off_As()
    {
        await using var fx = await StartAsync("P-GUIDED-HOST");
        Assert.NotNull(fx.Host.ManualTests);
        Assert.Same(GuidedTestScript.Current, fx.Host.ManualTests!.Script);

        await fx.Model.StartNewAsync();
        var run = fx.Model.Run!;
        var who = ((Tempest.Core.Identity.ICurrentPrincipalAccessor)fx.Host.Services!.GetService(typeof(Tempest.Core.Identity.ICurrentPrincipalAccessor))).Current!.Identity;
        Assert.Equal(who.DisplayName, run.Tester);
        Assert.Equal(who.Id, run.TesterId);
        Assert.Equal(MainWindow.DescribeBuild(fx.Host.Services!), run.Build);
        Assert.Equal(BuildDescription.Commit(fx.Host.Services!) is { Length: > 0 } commit ? commit : "unknown", run.BuildCommit);
        Assert.Equal(run.Id, (await fx.Host.ManualTests.FindUnfinishedAsync())!.Id);
    }

    [AvaloniaFact]
    public async Task An_Example_Opens_As_A_Scratch_Sheet_With_Its_Inputs_And_Compares_Against_The_Forms_Own_Outputs()
    {
        await using var fx = await StartAsync("P-GUIDED-OPEN");
        await fx.Model.StartNewAsync();
        fx.Model.Select(BearingExample);

        fx.Model.OpenWorksheet();
        await Settle();

        Assert.Equal("mech.bearing-life-basic", fx.View.SelectedTemplate!.Template.Id);
        Assert.Null(fx.View.CurrentInstance);
        Assert.Equal("25", ((TextBox)fx.View.FieldControl("C")!).Text);
        Assert.Equal("1.5", ((TextBox)fx.View.FieldControl("F_a")!).Text);
        Assert.NotNull(fx.View.LastRun);
        Assert.Contains("scratch sheet", fx.Model.Status, StringComparison.Ordinal);

        fx.Model.AutoCompare();
        Assert.Equal(ManualTestOutcome.Pass, fx.Model.DraftOutcome);
        Assert.All(fx.Model.ExpectedLines, l => Assert.Equal(GuidedCompareOutcome.Match, l.Outcome));

        // The tester changes an input; the form's outputs change; the compare now disagrees and says which line.
        fx.View.SetField("F_r", "4");
        await Settle();
        fx.Model.AutoCompare();
        Assert.Equal(ManualTestOutcome.Fail, fx.Model.DraftOutcome);
        Assert.Contains(fx.Model.ComparisonNotes, n => n.StartsWith("P:", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Opening_An_Example_Over_Unsaved_Edits_Asks_First_And_Then_Replaces_Them()
    {
        await using var fx = await StartAsync("P-GUIDED-EDITS");
        fx.Model.Select(BearingExample);
        fx.Model.OpenWorksheet();
        await Settle();

        fx.View.SetField("F_r", "7");
        await Settle();
        Assert.True(fx.View.HasUnsavedEdits());

        fx.Model.OpenWorksheet();
        await Settle();
        Assert.Contains("not saved", fx.Model.Status, StringComparison.Ordinal);
        Assert.Equal("7", ((TextBox)fx.View.FieldControl("F_r")!).Text);

        fx.Model.OpenWorksheet();
        await Settle();
        Assert.Equal("3", ((TextBox)fx.View.FieldControl("F_r")!).Text);

        // The form holds exactly the example again, so opening it once more is not a loss and asks nothing.
        fx.Model.OpenWorksheet();
        await Settle();
        Assert.Contains("scratch sheet", fx.Model.Status, StringComparison.Ordinal);
        Assert.DoesNotContain("not saved, ", fx.Model.Status, StringComparison.Ordinal);
        Assert.DoesNotContain("changes that are not saved", fx.Model.Status, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_Template_That_Is_Switched_Off_Says_Why_And_Nothing_Is_Opened()
    {
        await using var fx = await StartAsync("P-GUIDED-OFF");
        fx.Model.Select("example:struct.lifting/lifting-beam-lug-pair");

        fx.Model.OpenWorksheet();
        await Settle();

        Assert.Null(fx.View.SelectedTemplate);
        Assert.Contains("lifting", fx.Model.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("switched off", fx.Model.Status, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public async Task A_Whole_Guided_Run_On_The_Real_Form_Leaves_Every_Calculation_Table_As_It_Was()
    {
        await using var fx = await StartAsync("P-GUIDED-NOFX");
        var store = (IQueryablePersistenceStore)fx.Host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var tables = new[] { CalcInstanceStore.InstanceCollection, CalcInstanceStore.PackCollection, CalcInstanceStore.PackTemplateCollection, CalcInstanceStore.RecordCollection, CalcInstanceStore.ImportProfileCollection, CalcInstanceStore.ScanProfileCollection };

        // A saved draft the tester already had, so "unchanged" is more than "empty".
        var existing = await fx.Host.CalcInstances!.CreateAsync("elec.thermal-chain", fx.ProjectId, "CALC-E01 Heat sink");
        var before = await SnapshotAsync(store, tables);
        Assert.Contains(before, kv => kv.Key.StartsWith("Calc.Instances/", StringComparison.Ordinal));

        await fx.Model.StartNewAsync();

        foreach (var example in fx.Model.Script.Steps.Where(s => s.Kind == GuidedStepKind.Example && s.Discipline != "fluid-power").Take(6))
        {
            fx.Model.Select(example.Id);
            fx.Model.OpenWorksheet();
            fx.Model.AutoCompare();
            fx.Model.SetDraftOutcome(fx.Model.DraftOutcome ?? ManualTestOutcome.Pass);
            await fx.Model.RecordAsync();
            await Settle();
        }

        await fx.Model.FinishAsync();

        Assert.Equal(before, await SnapshotAsync(store, tables));
        Assert.Single(await fx.Host.CalcInstances.Store.ListInstancesAsync(fx.ProjectId));
        Assert.Equal(CalcInstanceStatus.Draft, (await fx.Host.CalcInstances.Store.FindInstanceAsync(existing.Id))!.Status);
        Assert.True(fx.Model.Run!.IsFinished);
        Assert.NotEmpty(fx.Model.Run.Results);
        Assert.Single(await store.ListKeysAsync(ManualTestRunService.Collection, string.Empty));
    }

    [AvaloniaFact]
    public async Task The_Dashboards_Read_Only_Source_Reports_What_The_Run_Recorded_Per_Template()
    {
        await using var fx = await StartAsync("P-GUIDED-STATUS");
        await fx.Model.StartNewAsync();
        fx.Model.Select(BearingExample);
        fx.Model.OpenWorksheet();
        await Settle();
        fx.Model.AutoCompare();
        await fx.Model.RecordAsync();

        IManualTestStatusSource source = fx.Host.ManualTests!;
        var status = await source.LatestByTemplateAsync();

        var bearing = Assert.Contains("mech.bearing-life-basic", status);
        Assert.Equal(ManualTestOutcome.Pass, bearing.Outcome);
        Assert.Equal(1, bearing.StepsRecorded);
        Assert.Equal(2, bearing.StepsInScript);
        Assert.Equal(fx.Model.Run!.Id, bearing.RunId);
        Assert.Single(status);
    }

    private static async Task<IReadOnlyDictionary<string, string>> SnapshotAsync(IQueryablePersistenceStore store, IEnumerable<string> collections)
    {
        var all = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var collection in collections)
        {
            foreach (var (key, value) in await store.ReadAllAsync(collection))
                all[$"{collection}/{key}"] = value;
        }

        return all;
    }
}
