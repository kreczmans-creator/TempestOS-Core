using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Tests.Quotations;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The whole "Export register" journey (`WP 21.2A`, scope item 3) through
/// the real, running window: open a project → Documents → **Export
/// register** → a real, A4 landscape PDF naming the project — the
/// identical "click the real button, read the real file back" shape
/// <c>QuotationJourneyTests</c>'s own Export step already establishes.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class DrawingRegisterExportJourneyTests
{
    [AvaloniaFact]
    public async Task ExportRegister_ThroughTheRealButton_WritesARealLandscapeRegisterPdf()
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

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-DRJ", "Drawing Register Journey Project");
            await navigator.OpenProjectAsync(project.Id, ProjectArea.Documents);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            // `GetPrivateField` on the one `ProjectWorkspaceView` instance
            // (via `window`'s own private field), not `GetLogicalDescendants().OfType<ProjectDocumentsView>().Single()`
            // — the Documents tab's own view is built once and kept alive
            // even when another tab is selected, so more than one
            // `ProjectDocumentsView`-shaped control can be logically
            // reachable at once; `QuotationJourneyTests` reaches
            // `ProjectWorkspaceView` the identical way for the same reason.
            var projectWorkspace = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace");
            var documentsView = GetPrivateField<ProjectDocumentsView>(projectWorkspace, "_documentsView");

            var exportPath = Path.Combine(Path.GetTempPath(), $"drawing-register-export-{Guid.NewGuid():N}.pdf");
            filePicker.SetNextSavePath(exportPath);
            try
            {
                var exportButton = documentsView.GetLogicalDescendants().OfType<Button>()
                    .Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Export register");
                Assert.True(exportButton.IsEnabled, "Export register must be enabled once the shell has wired a real exporter/renderer.");
                exportButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await RenderUntilAsync(window, () => ExportIsComplete(exportPath));

                var bytes = await File.ReadAllBytesAsync(exportPath);
                Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(8, bytes.Length)), StringComparison.Ordinal);
                Assert.Equal(1, PDFtoImage.Conversion.GetPageCount(bytes));

                var text = PdfTextExtractor.ExtractText(bytes);
                Assert.Contains("DRAWING REGISTER", text, StringComparison.Ordinal);
                Assert.Contains("P-DRJ", text, StringComparison.Ordinal);
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

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");
        return (T)field.GetValue(instance)!;
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
