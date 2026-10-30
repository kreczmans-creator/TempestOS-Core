using System.Globalization;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>The W2 review (M4, M6, L8, L9) in the view model: an I/O error leaves it usable, Cancel stops work, the same file twice and a draft that changed since the preview are refused.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetScanImportReviewTests
{
    private static readonly CalcProvenance Drawing = CalcProvenance.Drawing("DRG-SCAN-2", "A");

    private static ScanProfile Profile(params ScanFeatureEntry[] features) => new(
        "desk-review", 1, "Review profile",
        new ScanColumns("Element", Nominal: "Nominal", Actual: "Actual", Deviation: "Deviation", UpperTolerance: "Upper tol.", LowerTolerance: "Lower tol.", Serial: "Serial"),
        features, ScanSoftware.ZeissInspect, TemplateId: "mech.tolerance-stack");

    private static string Table(string feature = "Groove root", int parts = 10, double move = 0)
    {
        var sb = new StringBuilder("Element,Nominal,Actual,Deviation,Upper tol.,Lower tol.,Serial\r\n");

        for (var i = 0; i < parts; i++)
        {
            var v = 51.48 + (i % 5) * 0.004 + move;
            sb.Append(CultureInfo.InvariantCulture, $"{feature},51.5,{v:0.000},{v - 51.5:0.000},0.05,-0.05,S{i + 1:000}\r\n");
        }

        return sb.ToString();
    }

    private static IReadOnlyList<CalcInputValue> StackInputs() =>
    [
        CalcInputValue.Table("contributors",
        [
            CalcRow.Of(("name", new CalcCell(Text: "Groove root")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(51.5, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm"))),
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

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    /// <summary>A stream whose read fails the way a locked file or a dropped share does.</summary>
    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("The network name is no longer available.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("The network name is no longer available.");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A stream that never delivers a byte until the read is cancelled.</summary>
    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static async Task<(WorkspaceHost Host, ScanImportViewModel Model, CalcInstance Stack)> OpenAsync(string code)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var project = await host.ProjectDirectory!.CreateAsync(code, "Scan review project " + code);
        await host.ProjectContext!.OpenAsync(project.Id);
        var stack = await host.CalcInstances!.CreateAsync("mech.tolerance-stack", project.Id, "Groove stack", StackInputs());
        var model = new ScanImportViewModel(host.CalcInstances);
        await model.BeginAsync(stack);
        Assert.Null(await model.SaveProfileJsonAsync(ScanProfileJson.Write(Profile(new ScanFeatureEntry("Groove root", "contributors[0]")))));
        return (host, model, stack);
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    [AvaloniaFact]
    public async Task An_Io_Error_While_Reading_Is_Reported_And_Leaves_The_Model_Idle_So_Cancel_Close_And_A_Second_File_Still_Work()
    {
        var (host, model, stack) = await OpenAsync("P-REV1");

        try
        {
            await model.AddFileAsync("locked.csv", new FailingStream());

            Assert.False(model.IsBusy);
            Assert.Contains("could not be read", model.Status, StringComparison.Ordinal);
            Assert.Contains("network name", model.Status, StringComparison.Ordinal);
            Assert.Empty(model.Files);

            // The model is usable: Cancel with nothing running, then a good file, then Close.
            model.Cancel();
            model.Cancel();
            await model.AddFileAsync("groove.csv", Utf8(Table()));
            Assert.True(model.CanApply, model.Status);
            Assert.Equal(stack.Id, model.InstanceId);
            model.Close();
            model.Cancel();
            Assert.False(model.IsOpen);
            Assert.False(model.IsBusy);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Cancel_Stops_A_Stalled_Read_At_Once_And_The_Model_Is_Idle_Afterwards()
    {
        var (host, model, _) = await OpenAsync("P-REV2");

        try
        {
            var read = model.AddFileAsync("stalled.csv", new StalledStream());
            await Settle();
            Assert.True(model.IsBusy);
            Assert.False(model.CanApply);

            model.Cancel();
            await read.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(model.IsBusy);
            Assert.Equal("Cancelled.", model.Status);
            Assert.Empty(model.Files);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Cancel_Stops_A_Long_Parse_And_Fit_Without_Waiting_For_It()
    {
        var (host, model, _) = await OpenAsync("P-REV3");

        try
        {
            var big = new StringBuilder("Element,Nominal,Actual,Deviation,Upper tol.,Lower tol.,Serial\r\n", 4_900_000);

            for (var i = 0; i < 90_000; i++)
                big.Append("Groove root,51.5,51.").Append(480 + (i % 40)).Append(",-0.0").Append(10 + (i % 40)).Append(",0.05,-0.05,S").Append(i).Append("\r\n");

            await model.AddFileAsync("big.csv", Utf8(big.ToString()), refresh: false);
            Assert.Single(model.Files);

            var started = System.Diagnostics.Stopwatch.StartNew();
            // Cancel as soon as the refresh has handed the parse to the pool. A delay first raced the parse: on a fast runner the 90,000 rows
            // were read and fitted before the delay ended, and the model then rightly said "Ready to apply.".
            var refresh = model.RefreshAsync();
            Assert.True(model.IsBusy);
            model.Cancel();
            await refresh.WaitAsync(TimeSpan.FromSeconds(10));

            // Cancellation is proved by what the model says and holds: the preview did not complete, so there is none and nothing can be applied.
            Assert.False(model.IsBusy);
            Assert.Equal("Cancelled.", model.Status);
            Assert.Null(model.Preview);
            Assert.False(model.CanApply);
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(8));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task The_Same_File_Twice_Is_Not_Added_And_Does_Not_Double_N()
    {
        var (host, model, _) = await OpenAsync("P-REV4");

        try
        {
            await model.AddFileAsync("a.csv", Utf8(Table(parts: 16)));
            Assert.Equal(16, model.Preview!.Stats[0].Count);

            await model.AddFileAsync("copy.csv", Utf8(Table(parts: 16)));

            Assert.Single(model.Files);
            Assert.Contains("is the same file as 'a.csv'", model.Status, StringComparison.Ordinal);
            Assert.Equal(16, model.Preview!.Stats[0].Count);
            Assert.False(model.IsBusy);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Apply_Is_Refused_When_The_Draft_Changed_Since_The_Preview()
    {
        var (host, model, stack) = await OpenAsync("P-REV5");

        try
        {
            await model.AddFileAsync("a.csv", Utf8(Table()));
            Assert.True(model.CanApply, model.Status);

            var current = (await host.CalcInstances!.Store.FindInstanceAsync(stack.Id))!;
            Assert.True((await host.CalcInstances.UpdateInputsAsync(stack.Id, current.Inputs.Select(i => i.Symbol == "minimum" ? i with { Number = 51.3 } : i).ToList())).Succeeded);

            var refused = await model.ApplyAsync();

            Assert.False(refused!.Action.Succeeded);
            Assert.Contains("changed after this import was previewed", refused.Action.Reason, StringComparison.Ordinal);
            Assert.Null((await host.CalcInstances.Store.FindInstanceAsync(stack.Id))!.ScanImports);

            // Previewed again, it applies.
            await model.RefreshAsync();
            Assert.True(model.CanApply, model.Status);
            Assert.True((await model.ApplyAsync())!.Action.Succeeded);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Settings_That_Did_Not_Change_Do_Not_Start_Another_Analysis_And_Long_Lists_Are_Capped()
    {
        var (host, model, stack) = await OpenAsync("P-REV6");

        try
        {
            await model.SetSettingsAsync("2024", "RPS", 1, 30, false);
            var changes = 0;
            model.Changed += () => changes++;
            await model.AddFileAsync("a.csv", Utf8(Table()));
            var afterAdd = changes;
            Assert.True(afterAdd > 0);

            await model.SetSettingsAsync("2024", "RPS", 1, 30, false);
            await model.SetSettingsAsync(" 2024 ", "RPS ", 1, 30, false);
            Assert.Equal(afterAdd, changes);

            await model.SetSettingsAsync("2024", "RPS", 1, 30, true);
            Assert.True(changes > afterAdd);

            // 600 features of three parts each: the lists shown are capped, with a line that says how many are not shown.
            var many = new StringBuilder("Element,Nominal,Actual,Deviation,Upper tol.,Lower tol.,Serial\r\n");

            for (var f = 0; f < 600; f++)
            {
                for (var p = 0; p < 3; p++)
                    many.Append(CultureInfo.InvariantCulture, $"F{f},51.5,{51.49 + p * 0.005:0.000},{-0.01 + p * 0.005:0.000},0.05,-0.05,S{p}\r\n");
            }

            model.Close();
            await model.BeginAsync((await host.CalcInstances!.Store.FindInstanceAsync(stack.Id))!);
            Assert.Null(await model.SaveProfileJsonAsync(ScanProfileJson.Write(Profile() with { Id = "desk-many" })));
            await model.AddFileAsync("many.csv", Utf8(many.ToString()));

            Assert.True(model.StatLines.Count <= ScanImportViewModel.MaxLines + 1);
            Assert.EndsWith("more line(s)", model.StatLines[^1], StringComparison.Ordinal);
            Assert.True(model.Lines.Count <= ScanImportViewModel.MaxLines + 1);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
