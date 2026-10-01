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
using static Tempest.Desktop.Tests.DesktopTestHelpers;

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
        MainWindow? window = null;

        try
        {
            await host.StartAsync();
            var filePicker = new StubFilePicker();
            window = new MainWindow(host, filePicker);
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

            var expected = Path.Combine(folderRoot, ProjectFolderService.NoCustomerFolderName, "P-PF-EXPORT", "Quotes");
            Assert.Equal(expected, filePicker.SaveRequests[^1].StartFolder);
            Assert.True(Directory.Exists(expected));
        }
        finally
        {
            await TearDownAsync(window, host, folderRoot);
        }
    }

    [AvaloniaFact]
    public async Task OpeningAProject_GeneratesItsFolder_AndReportsItWithoutAModal()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var folderRoot = Path.Combine(Path.GetTempPath(), $"tempest-project-folders-{Guid.NewGuid():N}");
        var host = new WorkspaceHost(root);
        MainWindow? window = null;

        try
        {
            await host.StartAsync();
            window = new MainWindow(host, new StubFilePicker());
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

            var expected = Path.Combine(folderRoot, ProjectFolderService.NoCustomerFolderName, "P-PF-OPEN");
            await RenderUntilAsync(window, () => reports.Count > 0);
            Assert.True(Directory.Exists(expected));
            Assert.Equal($"Project folder created: {expected}", reports.Single().Message);
            Assert.True(reports.Single().Outcome.Succeeded);
        }
        finally
        {
            await TearDownAsync(window, host, folderRoot);
        }
    }

    [AvaloniaFact]
    public async Task TheComposedShell_WithAFolderRootOnTheCommandLine_GeneratesTheFolderOnOpen_AndExportsThere()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var folderRoot = Path.Combine(Path.GetTempPath(), $"tempest-project-folders-{Guid.NewGuid():N}");
        var host = new WorkspaceHost(root, commandLineArgs: [$"--{ProjectFolderOptions.FolderRootKey}={folderRoot}"]);
        MainWindow? window = null;

        try
        {
            await host.StartAsync();
            var filePicker = new StubFilePicker();
            window = new MainWindow(host, filePicker);
            LayOut(window);
            var quotations = (IQuotationService)host.Services!.GetService(typeof(IQuotationService));

            var project = await host.ProjectDirectory!.CreateAsync("P-PF-HOST", "Composed Folder Project");
            var created = await quotations.CreateAsync(project.Id);
            Assert.True(created.Succeeded, created.Reason);

            // Opening the project is all it takes: the composer's own coordinator files it.
            await host.ShellNavigator!.OpenProjectAsync(project.Id, ProjectArea.Quote);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var expected = Path.Combine(folderRoot, ProjectFolderService.NoCustomerFolderName, "P-PF-HOST");
            await RenderUntilAsync(window, () => Directory.Exists(expected));
            Assert.True(Directory.Exists(expected), $"The composed shell never generated '{expected}'.");

            var quoteView = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace").QuoteView;
            Assert.Equal(folderRoot, quoteView.ProjectFolders!.Service.Options.Root);
            await quoteView.SelectQuoteAsync(created.Quotation!.Id);
            LayOut(window);

            await ExportAsync(window, quoteView, filePicker);
            Assert.Equal(expected, filePicker.SaveRequests[^1].StartFolder);
        }
        finally
        {
            await TearDownAsync(window, host, folderRoot);
        }
    }

    [AvaloniaFact]
    public async Task TheCoordinator_WithFoldersSwitchedOff_StartsNoBackgroundWork()
    {
        var directory = new GatedProjectDirectory();
        var coordinator = new Composition.ProjectFolderCoordinator(
            new ProjectFolderLocator(new ProjectFolderService(new ProjectFolderOptions(null, [])), directory, UnusedOrganisations()),
            (_, _) => throw new InvalidOperationException("Nothing should be reported with folders switched off."));

        await coordinator.HandleAsync(new ProjectContextChangedEvent(null, directory.Project), CancellationToken.None);

        Assert.True(coordinator.Pending.IsCompleted);
        Assert.Equal(0, directory.Finds);
    }

    [AvaloniaFact]
    public async Task TheCoordinator_StopAsync_CancelsFolderWorkInFlight_AndReportsNothing()
    {
        var folderRoot = Path.Combine(Path.GetTempPath(), $"tempest-project-folders-{Guid.NewGuid():N}");
        var directory = new GatedProjectDirectory();
        var reports = new List<string>();
        var coordinator = new Composition.ProjectFolderCoordinator(
            new ProjectFolderLocator(new ProjectFolderService(new ProjectFolderOptions(folderRoot, [])), directory, UnusedOrganisations()),
            (message, _) =>
            {
                reports.Add(message);
                return Task.CompletedTask;
            });

        await coordinator.HandleAsync(new ProjectContextChangedEvent(null, directory.Project), CancellationToken.None);
        var deadline = Deadline(15);
        while (directory.Finds == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.Equal(1, directory.Finds);
        Assert.False(coordinator.Pending.IsCompleted);

        var stop = coordinator.StopAsync();
        while (!stop.IsCompleted && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(stop.IsCompleted, "StopAsync never finished.");
        await stop;
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(reports);
        Assert.False(Directory.Exists(folderRoot));

        // A project opened after the stop starts nothing.
        await coordinator.HandleAsync(new ProjectContextChangedEvent(null, directory.Project with { Id = Guid.NewGuid() }), CancellationToken.None);
        Assert.True(coordinator.Pending.IsCompleted);
        Assert.Equal(1, directory.Finds);
    }

    /// <summary>
    /// Closes the window and drains its queued render work while the
    /// headless session is still whole — a layout pass left queued (the
    /// export's status/toast report) otherwise runs inside the session's
    /// own teardown, after its font collections are gone (CI, Windows:
    /// KeyNotFoundException 'fonts:SystemFonts') — then shuts the host down
    /// and removes the temp folder root, whether or not the test passed.
    /// </summary>
    private static async Task TearDownAsync(MainWindow? window, WorkspaceHost host, string folderRoot)
    {
        try
        {
            window?.Close();
            Dispatcher.UIThread.RunJobs();
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

    private static IOrganisationCatalog UnusedOrganisations() =>
        System.Reflection.DispatchProxy.Create<IOrganisationCatalog, UnusedOrganisationCatalog>();

    /// <summary>An Organisation catalogue these coordinator tests never reach (their project has no client).</summary>
    public class UnusedOrganisationCatalog : System.Reflection.DispatchProxy
    {
        /// <inheritdoc />
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException($"{targetMethod?.Name} was not expected.");
    }

    /// <summary>A project directory whose <see cref="FindAsync"/> counts its calls and then waits until cancelled — folder work that is still in flight.</summary>
    private sealed class GatedProjectDirectory : IProjectDirectory
    {
        private int _finds;

        public ProjectSummary Project { get; } = new(Guid.NewGuid(), "P-PF-GATE", "Gated project", default, null);

        public int Finds => Volatile.Read(ref _finds);

        public async Task<ProjectSummary?> FindAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _finds);
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return Project;
        }

        public Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ProjectSummary> CreateAsync(string identifier, string displayName, string? description = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListProjectContentsAsync(Guid projectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? what = null)
    {
        var deadline = Deadline(15);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }

        // v0.23.0 board B9: a wait that times out is a failure, never a
        // silent fall-through to whatever the test checks next.
        Assert.True(condition(), $"Timed out waiting for: {what}");
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
