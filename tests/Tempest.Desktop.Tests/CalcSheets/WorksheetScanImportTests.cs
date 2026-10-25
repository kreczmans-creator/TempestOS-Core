using System.Globalization;
using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Events;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Addendum B, W2 in the worksheet form: "Import scan deviations…" previews the statistics and the mapping, then applies them to the open draft; a re-import reports the diff and stales the linked fit.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetScanImportTests
{
    private static readonly double[] GrooveRoot = [51.480, 51.490, 51.485, 51.495, 51.480, 51.490, 51.485, 51.475, 51.490, 51.480];

    private static readonly CalcProvenance Drawing = CalcProvenance.Drawing("DRG-SCAN-1", "A");

    private static string Table(double move = 0)
    {
        var sb = new StringBuilder("Element,Nominal,Actual,Deviation,Upper tol.,Lower tol.,Serial\r\n");

        for (var i = 0; i < GrooveRoot.Length; i++)
            sb.Append(CultureInfo.InvariantCulture, $"Groove root,51.5,{GrooveRoot[i] + move:0.000},{GrooveRoot[i] + move - 51.5:0.000},0.05,-0.05,S{i + 1:000}\r\n");

        sb.Append("Bore,56.9,56.921,0.021,0.046,0,S001\r\n");
        return sb.ToString();
    }

    private static ScanProfile Profile() => new(
        "desk-scan", 1, "Groove root to M1",
        new ScanColumns("Element", Nominal: "Nominal", Actual: "Actual", Deviation: "Deviation", UpperTolerance: "Upper tol.", LowerTolerance: "Lower tol.", Serial: "Serial"),
        [new ScanFeatureEntry("Groove root", "contributors[0]")], ScanSoftware.ZeissInspect, TemplateId: "mech.tolerance-stack");

    private static IReadOnlyList<CalcInputValue> StackInputs() =>
    [
        CalcInputValue.Table("contributors",
        [
            CalcRow.Of(("name", new CalcCell(Text: "Groove root")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(51.5, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm"))),
            CalcRow.Of(("name", new CalcCell(Text: "Plating")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(0, "mm")), ("upper", new CalcCell(0.002, "mm")), ("lower", new CalcCell(-0.002, "mm"))),
        ], Drawing),
        CalcInputValue.Quantity("minimum", 51.4, "mm", Drawing),
        CalcInputValue.Quantity("maximum", 51.6, "mm", Drawing),
        CalcInputValue.Of("k", 3, Drawing),
        CalcInputValue.Of("f_rss", 1.5, Drawing),
        CalcInputValue.Of("cpk_target", 1.33, Drawing),
        CalcInputValue.Of("n_mc", 2000, Drawing),
        CalcInputValue.Of("seed", 1, Drawing),
        CalcInputValue.Choice("notation", "BS 8888", Drawing),
    ];

    private static Stream Utf8(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [AvaloniaFact]
    public async Task The_Action_Previews_Statistics_And_Mapping_Applies_To_The_Draft_And_A_Re_Import_Shows_The_Diff_And_Stales_The_Fit()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-SCAN", "Scan import project");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var stack = await service.CreateAsync("mech.tolerance-stack", project.Id, "Groove stack", StackInputs());
            var fit = await service.CreateAsync("mech.iso286-fit", project.Id, "Seat fit", [CalcInputValue.Quantity("D", 51.5, "mm", Drawing)]);
            Assert.True((await service.BindAsync(fit.Id, "D", stack.Id, "x_mean")).Succeeded);

            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();

            // No saved draft shown: the action explains and opens nothing.
            Assert.False(view.CanImportScan);
            await coordinator.ImportScanAsync();
            Assert.Contains("Save the calculation as a draft first", view.StatusText, StringComparison.Ordinal);
            Assert.False(coordinator.ScanImport.IsOpen);

            await coordinator.OpenAsync(stack.Id);
            await Settle();
            Assert.True(view.CanImportScan);
            Assert.NotNull(FindButton(view, CalcSheetView.ImportScanCaption));
            await coordinator.ImportScanAsync();
            await Settle();
            var model = coordinator.ScanImport;
            Assert.True(model.IsOpen);
            Assert.True(view.ScanPanel!.IsVisible);

            // The profile is loaded as JSON and stored as a versioned profile; then the table: statistics first, then what the mapping does.
            Assert.Null(await model.SaveProfileJsonAsync(ScanProfileJson.Write(Profile())));
            Assert.Equal("desk-scan@1", model.Profile!.Label);
            await model.SetSettingsAsync("2024", "RPS-1 best fit", 1, 30, false);
            await model.AddFileAsync("groove.csv", Utf8(Table()));
            Assert.True(model.CanApply, model.Status);
            Assert.Contains(model.StatLines, l => l.StartsWith("Groove root (no stage): n = 10, mean 51.485 mm", StringComparison.Ordinal));
            Assert.Contains(model.StatLines, l => l.Contains("fewer than 30 parts", StringComparison.Ordinal));
            Assert.Contains(model.Lines, l => l.Status == ScanRowStatus.Mapped && l.Text.Contains("→ contributors[0]", StringComparison.Ordinal));
            Assert.Contains(model.Lines, l => l.Status == ScanRowStatus.Unmapped && l.Text.Contains("Bore", StringComparison.Ordinal));
            Assert.Contains("1 mapped, 1 not in the profile", model.Summary, StringComparison.Ordinal);
            Assert.Contains("fixed by the profile", model.NumberFormatText, StringComparison.Ordinal);
            Assert.Single(model.Files);
            Assert.Equal(64, model.Files[0].Sha256.Length);

            // Nothing is applied by previewing.
            Assert.Null((await service.Store.FindInstanceAsync(stack.Id))!.ScanImports);

            var applied = await model.ApplyAsync();
            Assert.True(applied!.Action.Succeeded, applied.Action.Reason);
            await Settle();
            var imported = (await service.Store.FindInstanceAsync(stack.Id))!;
            var upper = imported.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("upper")!;
            Assert.Equal(3 * 0.006236095644623894, upper.Number!.Value, 12);
            Assert.Equal(CalcProvenanceKind.ScanImport, upper.Provenance!.Kind);
            Assert.Equal("ZEISS Inspect 2024", ScanImportProvenance.TryParse(upper.Provenance)!.SoftwareAndVersion);
            Assert.Single(imported.ScanImports!);

            // The form shows the imported draft and carries the table with its provenance when saved again.
            var carried = view.ReadForm().Find("contributors")!.Rows![0].Get("upper")!;
            Assert.Equal(upper.Provenance, carried.Provenance);

            // The re-scan: a re-import reports the change; the fit goes stale through the ordinary machinery.
            await service.UpdateLinksAsync(fit.Id);
            await model.BeginAsync((await service.Store.FindInstanceAsync(stack.Id))!);
            await model.AddFileAsync("groove.csv", Utf8(Table(0.01)));
            Assert.Contains(model.ChangeLines, l => l.Contains("changed Groove root", StringComparison.Ordinal) && l.Contains("mean 51.485 mm", StringComparison.Ordinal) && l.Contains("mean 51.495 mm", StringComparison.Ordinal));
            Assert.Contains("1 changed", model.Summary, StringComparison.Ordinal);

            var second = await model.ApplyAsync();
            Assert.True(second!.Action.Succeeded, second.Action.Reason);
            Assert.True((await service.StalenessAsync((await service.Store.FindInstanceAsync(fit.Id))!)).IsStale);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Real_Asynchronous_File_Stream_Keeps_Every_Notification_On_The_UI_Thread()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        var path = Path.Combine(Path.GetTempPath(), "tempest-scan-" + Guid.NewGuid().ToString("N") + ".csv");

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-SCAN2", "Scan import project 2");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var stack = await service.CreateAsync("mech.tolerance-stack", project.Id, "Groove stack", StackInputs());
            await File.WriteAllTextAsync(path, Table());

            var model = new ScanImportViewModel(service);
            var offThread = 0;
            var notified = 0;
            model.Changed += () =>
            {
                notified++;

                if (!Dispatcher.UIThread.CheckAccess())
                    offThread++;
            };
            model.Applied += _ =>
            {
                if (!Dispatcher.UIThread.CheckAccess())
                    offThread++;
            };

            await model.BeginAsync(stack);
            Assert.Null(await model.SaveProfileJsonAsync(ScanProfileJson.Write(Profile())));

            // A FileStream completes its reads on a pool thread; the view model must come back to the caller's context.
            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
            {
                await model.AddFileAsync("groove.csv", stream);
            }

            Assert.True(model.CanApply, model.Status);
            var outcome = await model.ApplyAsync();
            Assert.True(outcome!.Action.Succeeded, outcome.Action.Reason);
            Assert.True(notified > 3);
            Assert.Equal(0, offThread);
        }
        finally
        {
            File.Delete(path);
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Refused_Row_A_Mismatching_Table_Or_A_Hostile_File_Disables_Apply_And_Says_Why()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-SCAN3", "Scan import project 3");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var stack = await service.CreateAsync("mech.tolerance-stack", project.Id, "Groove stack", StackInputs());
            var model = new ScanImportViewModel(service);
            await model.BeginAsync(stack);
            Assert.Null(await model.SaveProfileJsonAsync(ScanProfileJson.Write(Profile())));

            // A row of the stack that does not exist: refused with the way out.
            Assert.Null(await model.SaveProfileJsonAsync(ScanProfileJson.Write(Profile() with { Version = 2, Features = [new ScanFeatureEntry("Groove root", "contributors[5]")] })));
            await model.AddFileAsync("groove.csv", Utf8(Table()));
            Assert.False(model.CanApply);
            Assert.Contains(model.Lines, l => l.Status == ScanRowStatus.Refused && l.Text.Contains("no row 5", StringComparison.Ordinal));
            Assert.Null(await model.ApplyAsync()); // nothing previewed that can apply, so nothing is sent
            Assert.Contains("Preview the import first", model.Status, StringComparison.Ordinal);
            Assert.Null((await service.Store.FindInstanceAsync(stack.Id))!.ScanImports);

            // The wrong columns: the profile mismatch is reported with the headers the file has.
            await model.RemoveFileAsync(0);
            await model.AddFileAsync("other.csv", Utf8("Name,Soll,Ist\nA,1,1\n"));
            Assert.False(model.CanApply);
            Assert.Contains("does not match the profile", model.Status, StringComparison.Ordinal);
            Assert.Empty(model.Lines);

            // A UTF-16 file is not read at all.
            await model.RemoveFileAsync(0);
            Assert.Empty(model.Files);
            await model.AddFileAsync("utf16.csv", new MemoryStream([0xFF, 0xFE, 0x41, 0x00]));
            Assert.Contains("UTF-8", model.Status, StringComparison.Ordinal);

            // A profile that does not fit is refused when loaded, with the reasons.
            Assert.NotNull(await model.SaveProfileJsonAsync("{\"id\":\"Bad Id\",\"version\":1,\"title\":\"t\",\"columns\":{\"feature\":\"F\",\"actual\":\"A\"},\"features\":[]}"));
            Assert.NotNull(await model.SaveProfileJsonAsync("not json"));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Form_With_Unsaved_Edits_Is_Asked_To_Save_First()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-SCAN4", "Scan import project 4");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var stack = await service.CreateAsync("mech.tolerance-stack", project.Id, "Groove stack", StackInputs());

            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();
            await coordinator.OpenAsync(stack.Id);
            await Settle();

            view.SetField("k", text: "4");
            await coordinator.ImportScanAsync();
            Assert.Contains("Save or discard your edits first", view.StatusText, StringComparison.Ordinal);
            Assert.False(coordinator.ScanImport.IsOpen);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static Button? FindButton(CalcSheetView view, string name) =>
        view.GetLogicalDescendants().OfType<Button>().SingleOrDefault(b => AutomationProperties.GetName(b) == name);

    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }
    }
}
