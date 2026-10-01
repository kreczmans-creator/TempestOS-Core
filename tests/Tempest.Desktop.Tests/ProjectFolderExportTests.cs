using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Desktop.Views;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The quote PDF export starts in the project's own folder (PO decision
/// 2026-10-01; earlier PO comment: "when we create a project, it generates
/// a standard file system in Windows Explorer and exports direct to the
/// quote section there") — and, with folder generation off (the whole
/// Desktop test run, `TestProjectFolders`), behaves exactly as before: no start folder at all.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectFolderExportTests
{
    [AvaloniaFact]
    public async Task QuoteExport_StartsInTheProjectsQuoteFolder_AndOffersNoneWhenGenerationIsOff()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var folderRoot = Path.Combine(Path.GetTempPath(), $"tempest-project-folders-{Guid.NewGuid():N}");
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var filePicker = new StubFilePicker();
            var window = new MainWindow(host, filePicker);
            LayOut(window);
            var navigator = host.ShellNavigator!;
            var quotations = (IQuotationService)host.Services!.GetService(typeof(IQuotationService));
            var organisations = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));

            var project = await host.ProjectDirectory!.CreateAsync("P-PF-EXPORT", "Folder Export Project");
            var created = await quotations.CreateAsync(project.Id);
            Assert.True(created.Succeeded, created.Reason);
            var quoteId = created.Quotation!.Id;

            await navigator.OpenProjectAsync(project.Id, ProjectArea.Quote);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var projectWorkspace = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace");
            var quoteView = projectWorkspace.QuoteView;
            await quoteView.SelectQuoteAsync(quoteId);
            LayOut(window);

            // The composer wires a locator; the test run switches generation off (`TestProjectFolders`).
            Assert.NotNull(quoteView.ProjectFolders);
            Assert.Null(quoteView.ProjectFolders!.Service.Options.Root);
            await ExportAsync(window, quoteView, filePicker);
            Assert.Null(filePicker.SaveRequests[^1].StartFolder);
            Assert.False(Directory.Exists(folderRoot));

            // With a root configured, the picker opens in the quote subfolder.
            quoteView.ProjectFolders = new ProjectFolderLocator(
                new ProjectFolderService(new ProjectFolderOptions(folderRoot, [], "Quotes")), host.ProjectDirectory!, organisations);
            await ExportAsync(window, quoteView, filePicker);

            var expected = Path.Combine(folderRoot, ProjectFolderService.NoCustomerFolderName, "P-PF-EXPORT Folder Export Project", "Quotes");
            Assert.Equal(expected, filePicker.SaveRequests[^1].StartFolder);
            Assert.True(Directory.Exists(expected));

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
            try
            {
                if (Directory.Exists(folderRoot))
                    Directory.Delete(folderRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only, as `QuotationJourneyTests` does for its export.
            }
        }
    }

    [AvaloniaFact]
    public async Task OpeningAProject_GeneratesItsFolder_AndReportsItWithoutAModal()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var folderRoot = Path.Combine(Path.GetTempPath(), $"tempest-project-folders-{Guid.NewGuid():N}");
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var organisations = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));
            var eventBus = (Tempest.Core.Events.IEventBus)host.Services!.GetService(typeof(Tempest.Core.Events.IEventBus));

            var reports = new List<(string Message, ActionOutcome Outcome)>();
            var locator = new ProjectFolderLocator(new ProjectFolderService(new ProjectFolderOptions(folderRoot, [])), host.ProjectDirectory!, organisations);
            eventBus.Subscribe(new Composition.ProjectFolderCoordinator(locator, (message, outcome) =>
            {
                reports.Add((message, outcome));
                return Task.CompletedTask;
            }));

            var project = await host.ProjectDirectory!.CreateAsync("P-PF-OPEN", "Folder Open Project");
            await host.ShellNavigator!.OpenProjectAsync(project.Id);

            var expected = Path.Combine(folderRoot, ProjectFolderService.NoCustomerFolderName, "P-PF-OPEN Folder Open Project");
            await RenderUntilAsync(window, () => reports.Count > 0);
            Assert.True(Directory.Exists(expected));
            Assert.Equal($"Project folder created: {expected}", reports.Single().Message);
            Assert.True(reports.Single().Outcome.Succeeded);

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
            try
            {
                if (Directory.Exists(folderRoot))
                    Directory.Delete(folderRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }
    }

    private static async Task ExportAsync(MainWindow window, ProjectQuoteView quoteView, StubFilePicker filePicker)
    {
        var before = filePicker.SaveRequests.Count;
        var exportPath = Path.Combine(Path.GetTempPath(), $"quote-folder-export-{Guid.NewGuid():N}.pdf");
        filePicker.SetNextSavePath(exportPath);
        try
        {
            var exportButton = quoteView.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "Export"));
            exportButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => filePicker.SaveRequests.Count > before && ExportIsComplete(exportPath));
            Assert.True(filePicker.SaveRequests.Count > before, "Export never reached the file picker.");
        }
        finally
        {
            await RenderUntilAsync(window, () => !File.Exists(exportPath) || ExportIsComplete(exportPath));
            try
            {
                if (File.Exists(exportPath))
                    File.Delete(exportPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
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

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");
        return (T)field.GetValue(instance)!;
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
