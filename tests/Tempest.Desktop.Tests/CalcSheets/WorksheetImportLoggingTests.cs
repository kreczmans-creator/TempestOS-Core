using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Logging;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// Grey GR-B-02: a CAD or scan import leaves one Information line per step (file name, size, hash prefix, counts, refusal code) for
/// support to go on, and never a value, a parameter name, a part name or a path from the file.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorksheetImportLoggingTests
{
    private const string StackFile = "// Expression file created by NX 2306 (invented)\n// Part: STACK-1\n[mm]plate_len=30\n[mm]spacer_len=10\n[mm]pin_len=5\n[mm]gap_min=24.5\n[mm]gap_max=25.5\n";

    private static readonly CalcProvenance Drawing = CalcProvenance.Drawing("DRG-LOG-1", "A");

    /// <summary>Records every line with its structured properties.</summary>
    private sealed class RecordingLogger : ILogger
    {
        private readonly ConcurrentQueue<(string Level, string Message, IReadOnlyDictionary<string, object?>? Properties)> _lines = new();

        public IReadOnlyList<(string Level, string Message, IReadOnlyDictionary<string, object?>? Properties)> Lines => [.. _lines];

        public IReadOnlyList<string> Messages => [.. _lines.Select(l => l.Message)];

        public void Trace(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) => _lines.Enqueue(("Trace", message, properties));

        public void Debug(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) => _lines.Enqueue(("Debug", message, properties));

        public void Information(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) => _lines.Enqueue(("Information", message, properties));

        public void Warning(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) => _lines.Enqueue(("Warning", message, properties));

        public void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) => _lines.Enqueue(("Error", message, properties));

        public void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) => _lines.Enqueue(("Critical", message, properties));
    }

    private static string Sha12(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes))[..12];

    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    private static async Task<(WorkspaceHost Host, Guid ProjectId)> StartAsync(string code)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var project = await host.ProjectDirectory!.CreateAsync(code, "Import logging project " + code);
        await host.ProjectContext!.OpenAsync(project.Id);
        return (host, project.Id);
    }

    private static IReadOnlyList<CalcInputValue> StackInputs(string contributor = "Plate") =>
    [
        CalcInputValue.Table("contributors",
        [
            CalcRow.Of(("name", new CalcCell(Text: contributor)), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(29, "mm")), ("upper", new CalcCell(0.1, "mm")), ("lower", new CalcCell(-0.1, "mm"))),
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

    private static CadMappingProfile CadProfile() => new(
        "nx-stack", 1, "NX stack lengths to M1", [
            new("plate_len", "contributors[0].nominal"),
            new("spacer_len", "contributors[1].nominal"),
            new("pin_len", "contributors[2].nominal"),
            new("gap_min", "minimum"),
            new("gap_max", "maximum"),
        ],
        CadSystem.Nx, "mech.tolerance-stack", false, null);

    [AvaloniaFact]
    public async Task GR_B_02_A_Cad_Import_Logs_Each_Step_Without_Any_Value_From_The_File()
    {
        var (host, projectId) = await StartAsync("P-LOG-CAD");

        try
        {
            var stack = await host.CalcInstances!.CreateAsync("mech.tolerance-stack", projectId, "Pin stack", StackInputs());
            var logger = new RecordingLogger();
            var model = new CadImportViewModel(host.CalcInstances, logger);
            await model.BeginAsync(stack);
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(CadProfile())));

            var bytes = Encoding.UTF8.GetBytes(StackFile);
            await model.LoadFileAsync(@"C:\Users\someone\Designs\stack.exp", new MemoryStream(bytes));
            Assert.True(model.CanApply, model.Status);
            Assert.NotNull(await model.ApplyAsync());
            await Settle();

            var messages = logger.Messages;
            var read = Assert.Single(messages, m => m.StartsWith("CAD import: file read", StringComparison.Ordinal));
            Assert.Contains("file=stack.exp", read, StringComparison.Ordinal);
            Assert.Contains($"bytes={bytes.Length}", read, StringComparison.Ordinal);
            Assert.Contains($"sha256={Sha12(bytes)}", read, StringComparison.Ordinal);

            var previewed = messages.First(m => m.StartsWith("CAD import: previewed, ready to apply", StringComparison.Ordinal) && m.Contains("mapped=5", StringComparison.Ordinal));
            Assert.Contains("refused=0", previewed, StringComparison.Ordinal);
            Assert.Contains("fileRefused=False", previewed, StringComparison.Ordinal);
            Assert.Contains(messages, m => m.StartsWith("CAD import: applied", StringComparison.Ordinal) && m.Contains("file=stack.exp", StringComparison.Ordinal));

            // The same pairs travel as structured properties.
            var props = logger.Lines.Single(l => l.Message.StartsWith("CAD import: file read", StringComparison.Ordinal)).Properties!;
            Assert.Equal("stack.exp", props["file"]);
            Assert.Equal(Sha12(bytes), props["sha256"]);

            // Nothing of the file's content, its parameter names, its part name or the path it came from.
            var everything = string.Join("\n", logger.Lines.Select(l => l.Message + " " + string.Join(" ", l.Properties?.Select(p => $"{p.Key}={p.Value}") ?? [])));
            Assert.All(logger.Lines, l => Assert.Equal("Information", l.Level));

            foreach (var secret in new[] { "STACK-1", "plate_len", "spacer_len", "pin_len", "gap_min", "24.5", "25.5", "NX 2306", "Plate", "Spacer", "Users", "someone", "Designs", @"C:\" })
                Assert.DoesNotContain(secret, everything, StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task GR_B_02_A_Refused_Cad_File_Logs_Its_Name_And_Refusal_Code_Only()
    {
        var (host, projectId) = await StartAsync("P-LOG-REF");

        try
        {
            var stack = await host.CalcInstances!.CreateAsync("mech.tolerance-stack", projectId, "Pin stack", StackInputs());
            var logger = new RecordingLogger();
            var model = new CadImportViewModel(host.CalcInstances, logger);
            await model.BeginAsync(stack);

            await model.LoadFileAsync("huge.exp", new MemoryStream(new byte[ImportLimits.DefaultMaxBytes + 1]));
            await Settle();

            var refused = Assert.Single(logger.Messages);
            Assert.StartsWith("CAD import: file refused", refused, StringComparison.Ordinal);
            Assert.Contains("file=huge.exp", refused, StringComparison.Ordinal);
            Assert.Contains($"code={ImportRefusalCode.TooLarge}", refused, StringComparison.Ordinal);
            Assert.Equal(ImportRefusalCode.TooLarge, logger.Lines.Single().Properties!["code"]);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task GR_B_02_A_Scan_Import_Logs_Each_Step_Without_Any_Value_From_The_Tables()
    {
        var (host, projectId) = await StartAsync("P-LOG-SCAN");

        try
        {
            var stack = await host.CalcInstances!.CreateAsync("mech.tolerance-stack", projectId, "Groove stack", StackInputs("Groove root"));
            var logger = new RecordingLogger();
            var model = new ScanImportViewModel(host.CalcInstances, logger);
            await model.BeginAsync(stack);

            var profile = new ScanProfile(
                "desk-log", 1, "Logging profile",
                new ScanColumns("Element", Nominal: "Nominal", Actual: "Actual", Deviation: "Deviation", UpperTolerance: "Upper tol.", LowerTolerance: "Lower tol.", Serial: "Serial"),
                [new ScanFeatureEntry("Groove root", "contributors[0]")], ScanSoftware.ZeissInspect, TemplateId: "mech.tolerance-stack");
            Assert.Null(await model.SaveProfileJsonAsync(ScanProfileJson.Write(profile)));

            var table = new StringBuilder("Element,Nominal,Actual,Deviation,Upper tol.,Lower tol.,Serial\r\n");

            for (var i = 0; i < 10; i++)
            {
                var v = 51.48 + (i % 5) * 0.004;
                table.Append(CultureInfo.InvariantCulture, $"Groove root,51.5,{v:0.000},{v - 51.5:0.000},0.05,-0.05,SN{i + 1:000}\r\n");
            }

            var bytes = Encoding.UTF8.GetBytes(table.ToString());
            await model.AddFileAsync("/mnt/share/line 4/scan-run.csv", new MemoryStream(bytes));
            Assert.True(model.CanApply, model.Status);
            Assert.NotNull(await model.ApplyAsync());
            await Settle();

            var messages = logger.Messages;
            var read = Assert.Single(messages, m => m.StartsWith("Scan import: file read", StringComparison.Ordinal));
            Assert.Contains("file=scan-run.csv", read, StringComparison.Ordinal);
            Assert.Contains($"bytes={bytes.Length}", read, StringComparison.Ordinal);
            Assert.Contains($"sha256={Sha12(bytes)}", read, StringComparison.Ordinal);
            var previewed = messages.First(m => m.StartsWith("Scan import: previewed, ready to apply", StringComparison.Ordinal) && m.Contains("usableRows=10", StringComparison.Ordinal));
            Assert.Contains("filesRefused=False", previewed, StringComparison.Ordinal);
            Assert.Contains(messages, m => m.StartsWith("Scan import: applied", StringComparison.Ordinal) && m.Contains(Sha12(bytes), StringComparison.Ordinal));

            var everything = string.Join("\n", logger.Lines.Select(l => l.Message + " " + string.Join(" ", l.Properties?.Select(p => $"{p.Key}={p.Value}") ?? [])));
            Assert.All(logger.Lines, l => Assert.Equal("Information", l.Level));

            foreach (var secret in new[] { "Groove root", "51.5", "51.48", "SN001", "mnt", "share", "line 4", "Nominal" })
                Assert.DoesNotContain(secret, everything, StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task GR_B_02_Without_A_Logger_The_Imports_Run_Unchanged()
    {
        var (host, projectId) = await StartAsync("P-LOG-NONE");

        try
        {
            var stack = await host.CalcInstances!.CreateAsync("mech.tolerance-stack", projectId, "Pin stack", StackInputs());
            var model = new CadImportViewModel(host.CalcInstances);
            await model.BeginAsync(stack);
            Assert.Null(await model.SaveProfileJsonAsync(CadMappingProfileJson.Write(CadProfile())));
            await model.LoadFileAsync("stack.exp", new MemoryStream(Encoding.UTF8.GetBytes(StackFile)));
            Assert.True(model.CanApply, model.Status);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
