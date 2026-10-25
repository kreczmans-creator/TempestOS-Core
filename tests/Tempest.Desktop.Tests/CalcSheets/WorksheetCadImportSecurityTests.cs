using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Events;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// Security review of W1 in the Desktop: H4 (a picker's stream completes off the UI thread), M6 (the number format in force), L3 (profile
/// loading is bounded), L9 (no silent profile upgrade), L10 (apply what was previewed) and the Cancel path.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetCadImportSecurityTests
{
    private const string StackFile = "// Expression file created by NX 2306 (invented)\n// Part: STACK-1\n[mm]plate_len=30\n[mm]spacer_len=10\n[mm]pin_len=5\n[mm]gap_min=24.5\n[mm]gap_max=25.5\n";

    private static readonly CalcProvenance Drawing = CalcProvenance.Drawing("DRG-STK-1", "A");

    private static CadMappingProfile Profile(int version = 1, NumberProfile? number = null) => new(
        "nx-stack", version, "NX stack lengths to M1", [
            new("plate_len", "contributors[0].nominal"),
            new("spacer_len", "contributors[1].nominal"),
            new("pin_len", "contributors[2].nominal"),
            new("gap_min", "minimum"),
            new("gap_max", "maximum"),
        ],
        CadSystem.Nx, "mech.tolerance-stack", false, number);

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

    private static async Task<(WorkspaceHost Host, CalcInstanceService Service, CalcInstance Stack)> StartAsync(string code)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var project = await host.ProjectDirectory!.CreateAsync(code, "CAD import security project");
        await host.ProjectContext!.OpenAsync(project.Id);
        var service = host.CalcInstances!;
        var stack = await service.CreateAsync("mech.tolerance-stack", project.Id, "Pin stack", StackInputs());
        return (host, service, stack);
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }
    }

    private static void Click(CalcSheetView view, string name)
    {
        var button = view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);
        Assert.True(button.IsEnabled && button.IsEffectivelyVisible, $"'{name}' is not available.");
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
    }

    private static string WriteTemp(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), "w1-picked-" + Guid.NewGuid().ToString("N") + ".exp");
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    [AvaloniaFact]
    public async Task H4_A_Real_File_Stream_That_Completes_Off_The_Ui_Thread_Drives_The_Panel_Without_Touching_It_From_Another_Thread()
    {
        var (host, service, stack) = await StartAsync("P-H4");
        var path = WriteTemp(StackFile);

        try
        {
            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext!, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();
            await coordinator.OpenAsync(stack.Id);
            await Settle();
            await coordinator.ImportCadAsync();
            var model = coordinator.CadImport;
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(Profile())));

            // Every Changed the panel renders from arrives on the UI thread, whichever thread the work finished on.
            var threads = new List<bool>();
            model.Changed += () => threads.Add(Dispatcher.UIThread.CheckAccess());

            // A FileStream opened for asynchronous reads: its ReadAsync completes on a pool thread, as a desktop file picker's does.
            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
                await model.LoadFileAsync("picked.exp", stream);

            await Settle();
            Assert.True(model.CanApply, model.Status);
            Assert.Equal(5, model.Lines.Count(l => l.Status == CadRowStatus.Mapped));
            Assert.NotEmpty(threads);
            Assert.All(threads, onUi => Assert.True(onUi));

            // And a call that starts on a pool thread is marshalled too.
            threads.Clear();
            await Task.Run(() => model.RefreshAsync());
            await Settle();
            Assert.NotEmpty(threads);
            Assert.All(threads, onUi => Assert.True(onUi));
            Assert.True(view.CadPanel!.IsVisible);
        }
        finally
        {
            File.Delete(path);
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task H4_A_Slow_Read_Can_Be_Cancelled_And_The_Panel_Offers_Cancel_While_It_Runs()
    {
        var (host, service, stack) = await StartAsync("P-H4B");

        try
        {
            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext!, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();
            await coordinator.OpenAsync(stack.Id);
            await Settle();
            await coordinator.ImportCadAsync();
            var model = coordinator.CadImport;

            var stalled = new StalledStream();
            var load = model.LoadFileAsync("slow.exp", stalled);
            await Settle();
            Assert.True(model.IsBusy);
            var cancel = view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == CadImportPanel.CancelCaption);
            Assert.True(cancel.IsEffectivelyVisible);

            model.Cancel();
            await load;
            await Settle();
            Assert.False(model.IsBusy);
            Assert.Equal("Cancelled.", model.Status);
            Assert.False(model.CanApply);
            Assert.False(cancel.IsEffectivelyVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task M6_A_Profile_That_Fixes_The_Number_Format_Is_Shown_And_The_Choice_Is_Not_Offered_And_Grouping_Is_Reported()
    {
        var (host, service, stack) = await StartAsync("P-M6");

        try
        {
            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext!, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();
            await coordinator.OpenAsync(stack.Id);
            await Settle();
            await coordinator.ImportCadAsync();
            var model = coordinator.CadImport;
            var checkBox = view.GetLogicalDescendants().OfType<CheckBox>().Single(c => AutomationProperties.GetName(c) == "Numbers use a decimal comma");

            // No profile states a format: the person's choice is in force and says so.
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(Profile())));
            await Settle();
            Assert.False(model.NumberFormatFixedByProfile);
            Assert.Contains("your choice", model.NumberFormatText, StringComparison.Ordinal);
            Assert.True(checkBox.IsEnabled);

            // A profile that fixes decimal comma with '.' as thousands separator wins over the checkbox, which is disabled and shows what is in force.
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(Profile(2, new NumberProfile(DecimalMark.Comma, '.')))));
            await Settle();
            Assert.True(model.NumberFormatFixedByProfile);
            Assert.Contains("set by the profile", model.NumberFormatText, StringComparison.Ordinal);
            Assert.Contains("decimal comma", model.NumberFormatText, StringComparison.Ordinal);
            Assert.False(checkBox.IsEnabled);
            Assert.True(checkBox.IsChecked);
            await model.SetSettingsAsync(false, null, null, null, null, null);
            Assert.Equal(DecimalMark.Comma, model.EffectiveNumber.Decimal);

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("[mm]plate_len=1.500\n[mm]gap_min=24,5\n"));
            await model.LoadFileAsync("grouped.exp", stream);
            Assert.Contains(model.NoticeLines, l => l.Contains("plate_len", StringComparison.Ordinal) && l.Contains("thousands separator", StringComparison.Ordinal));
            Assert.Equal(1500, model.Preview!.Mapping!.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("nominal")!.Number);
            Assert.Equal(24.5, model.Preview.Mapping.Inputs.Single(i => i.Symbol == "minimum").Number);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task L3_A_Profile_Is_Read_Through_The_Bounded_Reader_A_Byte_Order_Mark_Is_Fine_And_An_Oversized_Or_Endless_One_Is_Refused()
    {
        var (host, service, stack) = await StartAsync("P-L3");

        try
        {
            var model = new CadImportViewModel(service);
            await model.BeginAsync(stack);

            var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(CadMappingProfileJson.Write(Profile()))).ToArray();
            Assert.Null(await model.LoadProfileStreamAsync(new MemoryStream(withBom)));
            Assert.Equal("nx-stack@1", model.Profile!.Label);

            var tooBig = await model.LoadProfileStreamAsync(new MemoryStream(new byte[CadMappingProfileJson.MaxBytes + 1]));
            Assert.Contains("larger than", tooBig, StringComparison.Ordinal);

            var endless = await model.LoadProfileStreamAsync(new EndlessStream());
            Assert.Contains("larger than", endless, StringComparison.Ordinal);

            var bad = await model.LoadProfileStreamAsync(new MemoryStream([0xFF, 0xFE, 0x7B, 0x00]));
            Assert.Contains("UTF-8", bad, StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task L9_A_Newer_Profile_Version_Is_Offered_Never_Switched_To()
    {
        var (host, service, stack) = await StartAsync("P-L9");

        try
        {
            var model = new CadImportViewModel(service);
            await model.BeginAsync(stack);
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(Profile())));
            Assert.Equal(1, model.Profile!.Version);

            // Another user publishes version 2 to the shared store.
            Assert.Null((await service.SaveImportProfileAsync(Profile(2))).Refusal);
            await model.LoadProfilesAsync();

            Assert.Equal(1, model.Profile!.Version);
            Assert.Contains(model.Profiles, p => p.Version == 2);
            Assert.Contains(model.Profiles, p => p.Version == 1);

            await model.SelectProfileAsync(model.Profiles.Single(p => p.Version == 2));
            Assert.Equal(2, model.Profile!.Version);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task L10_Apply_Applies_What_Was_Previewed_And_Is_Refused_When_The_Draft_Changed_Since()
    {
        var (host, service, stack) = await StartAsync("P-L10");

        try
        {
            var model = new CadImportViewModel(service);
            await model.BeginAsync(stack);
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(Profile())));
            await model.LoadFileAsync("stack.exp", new MemoryStream(Encoding.UTF8.GetBytes(StackFile)));
            Assert.True(model.CanApply, model.Status);

            // The draft is edited elsewhere after the preview.
            Assert.True((await service.UpdateInputsAsync(stack.Id, [.. stack.Inputs.Select(i => i.Symbol == "k" ? i with { Number = 4 } : i)])).Succeeded);

            var refused = await model.ApplyAsync();
            Assert.False(refused!.Action.Succeeded);
            Assert.Contains("changed after this import was previewed", model.Status, StringComparison.Ordinal);
            Assert.Null((await service.Store.FindInstanceAsync(stack.Id))!.CadImports);

            await model.RefreshAsync();
            var applied = await model.ApplyAsync();
            Assert.True(applied!.Action.Succeeded, applied.Action.Reason);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task M2_Editing_An_Imported_Value_In_The_Form_And_Saving_Tells_The_Person_The_Claim_Was_Removed()
    {
        var (host, service, stack) = await StartAsync("P-M2");

        try
        {
            var view = new CalcSheetView();
            var window = new Window { Width = 1600, Height = 1000, Content = view };
            window.Show();
            var coordinator = new CalcSheetCoordinator(service, view, host.ProjectContext!, () => "s.kreczman", (IEventBus)host.Services!.GetService(typeof(IEventBus)));
            await coordinator.RefreshAsync();
            await coordinator.OpenAsync(stack.Id);
            await Settle();
            await coordinator.ImportCadAsync();
            var model = coordinator.CadImport;
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(Profile())));
            await model.LoadFileAsync("stack.exp", new MemoryStream(Encoding.UTF8.GetBytes(StackFile)));
            Assert.True((await model.ApplyAsync())!.Action.Succeeded);
            await Settle();

            // "cad import" is not a kind a person can pick by hand; on an imported value it is shown, and an edit is caught at save.
            var provenance = view.GetLogicalDescendants().OfType<ComboBox>().Where(c => c.ItemsSource is IEnumerable<string> items && items.Contains("cad import")).ToList();
            Assert.NotEmpty(provenance);
            var plain = view.GetLogicalDescendants().OfType<ComboBox>().Where(c => c.ItemsSource is IEnumerable<string> items && items.Contains("drawing") && !items.Contains("cad import")).ToList();
            Assert.NotEmpty(plain);

            view.SetField("minimum", text: "24");
            await Settle();
            Click(view, CalcSheetView.SaveCaption);
            await Settle();
            Assert.Contains("CAD-import provenance was removed from 'minimum'", view.StatusText, StringComparison.Ordinal);
            Assert.Equal(CalcProvenanceKind.Manual, (await service.Store.FindInstanceAsync(stack.Id))!.Inputs.Single(i => i.Symbol == "minimum").Provenance!.Kind);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class EndlessStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(buffer.Length);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
