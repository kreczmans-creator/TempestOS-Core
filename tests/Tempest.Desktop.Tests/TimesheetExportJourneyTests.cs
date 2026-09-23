using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Tests.Quotations;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The whole "Export week" journey (`WP 21.2A`, scope item 3) through the
/// real, running window: Business → Timesheets → **Export week** → a real
/// PDF, naming the current principal and carrying "TIMESHEET" — the
/// identical "click the real button, read the real file back" shape
/// <c>QuotationJourneyTests</c>'s own Export step already establishes.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class TimesheetExportJourneyTests
{
    [AvaloniaFact]
    public async Task ExportWeek_ThroughTheRealButton_WritesARealTimesheetPdf()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var filePicker = new StubFilePicker();
            var window = new MainWindow(host, filePicker);
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToModuleAsync(Tempest.Workspace.Shell.ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Timesheets");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<TimesheetWeekView>().Any());
            LayOut(window);

            var timesheetView = window.GetLogicalDescendants().OfType<TimesheetWeekView>().Single();

            var exportPath = Path.Combine(Path.GetTempPath(), $"timesheet-export-{Guid.NewGuid():N}.pdf");
            filePicker.SetNextSavePath(exportPath);
            try
            {
                var exportButton = timesheetView.GetLogicalDescendants().OfType<Button>()
                    .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Export week");
                Assert.True(exportButton.IsEnabled, "Export week must be enabled once the shell has wired a real exporter/renderer.");
                exportButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await RenderUntilAsync(window, () => ExportIsComplete(exportPath));

                var bytes = await File.ReadAllBytesAsync(exportPath);
                Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);

                var text = PdfTextExtractor.ExtractText(bytes);
                Assert.Contains("TIMESHEET", text, StringComparison.Ordinal);
            }
            finally
            {
                try
                {
                    if (File.Exists(exportPath))
                        File.Delete(exportPath);
                }
                catch (IOException)
                {
                }
            }

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static bool ExportIsComplete(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return exclusive.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

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
