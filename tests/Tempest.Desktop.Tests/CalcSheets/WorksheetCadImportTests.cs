using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Events;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Addendum B, W1 in the worksheet form: "Import CAD parameters…" previews the mapping, then applies it to the open draft; a re-import reports the diff and stales the linked fit.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetCadImportTests
{
    private const string StackFile = "// Expression file created by NX 2306 (invented)\n// Part: STACK-1\n// Revision: A\n[mm]plate_len=30\n[mm]spacer_len=10\n[mm]pin_len=5\n[mm]gap_min=24.5\n[mm]gap_max=25.5\n[mm]derived=plate_len-spacer_len\n[mm]flange=40 // not in the profile\n";

    private static readonly CalcProvenance Drawing = CalcProvenance.Drawing("DRG-STK-1", "A");

    private static CadMappingProfile Profile() => new(
        "nx-stack", 1, "NX stack lengths to M1", [
            new("plate_len", "contributors[0].nominal"),
            new("spacer_len", "contributors[1].nominal"),
            new("pin_len", "contributors[2].nominal"),
            new("gap_min", "minimum"),
            new("gap_max", "maximum"),
        ],
        CadSystem.Nx, "mech.tolerance-stack");

    private static IReadOnlyList<CalcInputValue> StackInputs() =>
    [
        CalcInputValue.Table("contributors",
        [
            CalcRow.Of(("name", new CalcCell(Text: "Plate")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(29, "mm")), ("upper", new CalcCell(0.1, "mm")), ("lower", new CalcCell(-0.1, "mm"))),
            CalcRow.Of(("name", new CalcCell(Text: "Spacer")), ("direction", new CalcCell(Text: "subtracts")), ("nominal", new CalcCell(9, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm"))),
            CalcRow.Of(("name", new CalcCell(Text: "Pin")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(5, "mm")), ("upper", new CalcCell(0.02, "mm")), ("lower", new CalcCell(-0.02, "mm"))),
        ], Drawing),
        CalcInputValue.Quantity("minimum", 20, "mm", Drawing),
        CalcInputValue.Quantity("maximum", 30, "mm", Drawing),
        CalcInputValue.Of("k", 3, Drawing),
        CalcInputValue.Of("f_rss", 1.5, Drawing),
        CalcInputValue.Of("cpk_target", 1.33, Drawing),
        CalcInputValue.Of("n_mc", 2000, Drawing),
        CalcInputValue.Of("seed", 1, Drawing),
        CalcInputValue.Choice("notation", "BS 8888", Drawing),
    ];

    private static Stream Utf8(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [AvaloniaFact]
    public async Task The_Action_Previews_The_Mapping_Applies_It_To_The_Draft_And_A_Re_Import_Shows_The_Diff_And_Stales_The_Fit()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-CAD", "CAD import project");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var stack = await service.CreateAsync("mech.tolerance-stack", project.Id, "Pin stack", StackInputs());
            var fit = await service.CreateAsync("mech.iso286-fit", project.Id, "Seat fit", [CalcInputValue.Quantity("D", 25, "mm", Drawing)]);
            Assert.True((await service.BindAsync(fit.Id, "D", stack.Id, "x_nom")).Succeeded);

            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();

            // No saved draft shown: the action explains and opens nothing.
            Assert.False(view.CanImportCad);
            await coordinator.ImportCadAsync();
            Assert.Contains("Save the calculation as a draft first", view.StatusText, StringComparison.Ordinal);
            Assert.False(coordinator.CadImport.IsOpen);

            await coordinator.OpenAsync(stack.Id);
            await Settle();
            Assert.True(view.CanImportCad);
            Assert.NotNull(FindButton(view, CalcSheetView.ImportCadCaption));
            await coordinator.ImportCadAsync();
            await Settle();
            var model = coordinator.CadImport;
            Assert.True(model.IsOpen);
            Assert.True(view.CadPanel!.IsVisible);

            // Profile first (loaded as JSON, stored as a versioned profile), then the file: the preview sorts every name.
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(Profile())));
            Assert.Equal("nx-stack@1", model.Profile!.Label);
            await model.LoadFileAsync("stack-1.exp", Utf8(StackFile));
            Assert.Equal(CadSourceFormat.NxExpressions, model.Format);
            Assert.True(model.CanApply, model.Status);
            Assert.Equal(5, model.Lines.Count(l => l.Status == CadRowStatus.Mapped));
            Assert.Contains(model.Lines, l => l.Status == CadRowStatus.Expression && l.Text.Contains("derived", StringComparison.Ordinal) && l.Text.Contains("never evaluated", StringComparison.Ordinal));
            Assert.Contains(model.Lines, l => l.Status == CadRowStatus.Unmapped && l.Text.Contains("flange", StringComparison.Ordinal));
            Assert.Contains("5 mapped, 1 not in the profile, 1 read-only", model.Summary, StringComparison.Ordinal);
            Assert.Equal(64, model.FileHash!.Length);

            // Nothing is applied by previewing.
            Assert.Null((await service.Store.FindInstanceAsync(stack.Id))!.CadImports);

            var applied = await model.ApplyAsync();
            Assert.True(applied!.Action.Succeeded, applied.Action.Reason);
            await Settle();
            Assert.Equal(24.5, view.ReadForm().Find("minimum")!.Number);
            var imported = (await service.Store.FindInstanceAsync(stack.Id))!;
            Assert.Equal(24.5, imported.Inputs.Single(i => i.Symbol == "minimum").Number);
            Assert.Equal(CalcProvenanceKind.CadImport, imported.Inputs.Single(i => i.Symbol == "minimum").Provenance!.Kind);
            Assert.Single(imported.CadImports!);

            // The form shows the imported draft, and saving the form again keeps the import provenance (not turned into a manual entry).
            var read = view.ReadForm().Find("minimum")!;
            Assert.Equal(24.5, read.Number);
            Assert.Equal(imported.Inputs.Single(i => i.Symbol == "minimum").Provenance, read.Provenance);

            // The designer changes the plate: a re-import reports the change; the fit goes stale through the ordinary machinery.
            await service.UpdateLinksAsync(fit.Id);
            await model.BeginAsync((await service.Store.FindInstanceAsync(stack.Id))!);
            await model.LoadFileAsync("stack-1.exp", Utf8(StackFile.Replace("plate_len=30", "plate_len=31", StringComparison.Ordinal)));
            Assert.Contains(model.ChangeLines, l => l.Contains("plate_len: 30 mm → 31 mm", StringComparison.Ordinal));
            Assert.Contains("1 changed", model.Summary, StringComparison.Ordinal);

            var second = await model.ApplyAsync();
            Assert.True(second!.Action.Succeeded, second.Action.Reason);
            Assert.Contains("1 changed", second.Diff!.Summary, StringComparison.Ordinal);
            Assert.True((await service.StalenessAsync((await service.Store.FindInstanceAsync(fit.Id))!)).IsStale);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Refused_Row_Or_A_Refused_File_Disables_Apply_And_Says_Why()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-CAD2", "CAD import project 2");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var stack = await service.CreateAsync("mech.tolerance-stack", project.Id, "Pin stack", StackInputs());
            var model = new CadImportViewModel(service);
            await model.BeginAsync(stack);
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(Profile())));

            // gap_min in newtons: a different kind of quantity from the input's millimetres.
            await model.LoadFileAsync("stack-1.exp", Utf8(StackFile.Replace("[mm]gap_min", "[N]gap_min", StringComparison.Ordinal)));
            Assert.False(model.CanApply);
            Assert.Contains(model.Lines, l => l.Status == CadRowStatus.Refused && l.Text.Contains("gap_min", StringComparison.Ordinal));
            Assert.Null((await service.Store.FindInstanceAsync(stack.Id))!.CadImports);
            var refused = await model.ApplyAsync();
            Assert.False(refused!.Action.Succeeded);

            // A UTF-16 file is not read at all; a file with no CAD extension offers no format.
            await model.LoadFileAsync("utf16.exp", new MemoryStream([0xFF, 0xFE, 0x5B, 0x00]));
            Assert.Contains("UTF-8", model.Status, StringComparison.Ordinal);
            Assert.False(model.CanApply);
            Assert.Empty(model.Lines);

            await model.LoadFileAsync("notes.docx", Utf8("hello"));
            Assert.Empty(model.Formats);
            Assert.Contains("not one of the CAD parameter formats", model.Status, StringComparison.Ordinal);

            // A profile that does not fit is refused when loaded, with the reasons.
            var message = await model.SaveProfileJsonAsync("{\"id\":\"Bad Id\",\"version\":1,\"title\":\"t\",\"entries\":[]}");
            Assert.NotNull(message);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task A_Checked_Draft_Does_Not_Offer_The_Import_And_A_Form_With_Unsaved_Edits_Is_Asked_To_Save_First()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-CAD3", "CAD import project 3");
            await host.ProjectContext!.OpenAsync(project.Id);
            var service = host.CalcInstances!;
            var stack = await service.CreateAsync("mech.tolerance-stack", project.Id, "Pin stack", StackInputs());

            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();
            await coordinator.OpenAsync(stack.Id);
            await Settle();

            view.SetField("k", text: "4");
            await coordinator.ImportCadAsync();
            Assert.Contains("Save or discard your edits first", view.StatusText, StringComparison.Ordinal);
            Assert.False(coordinator.CadImport.IsOpen);
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
