using Avalonia.Controls;
using Tempest.Workspace;
using Tempest.Workspace.Editors;
using Tempest.Workspace.Files;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Shell;
using Tempest.Core.Commands;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Macros;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Files;
using Tempest.Desktop.History;
using Tempest.Desktop.Input;
using Tempest.Desktop.Tasks;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Every view, dialog and overlay <see cref="MainWindowComposer.BuildViews"/>
/// builds — nothing here needs any coordinator to already exist (`WP 19.2A`).
/// </summary>
internal sealed record ComposedViews(
    DesktopCompositionRoot Composition,
    IDiagnosticsProvider Diagnostics,
    DesktopSessionState Session,
    ThemeService Theme,
    SettingsDialog SettingsDialog,
    ToastHost ToastHost,
    BusyOverlay BusyOverlay,
    ConfirmationDialog ConfirmationDialog,
    InputDialog InputDialog,
    MessageDialog MessageDialog,
    MacroManagerDialog MacroManagerDialog,
    ProjectExplorerView ExplorerView,
    PropertyInspectorView InspectorView,
    StatusBarView StatusBar,
    CommandPaletteOverlay CommandPalette,
    DocumentAreaView DocumentArea,
    RibbonView Ribbon,
    DesktopCommandPrompt CommandPrompt,
    ActionOutcomeReporter ActionReporter,
    CitationPicker CitationPicker,
    SubjectPicker SubjectPicker,
    DeclaredFigureEntry DeclaredFigureEntry,
    CheckEntry CheckEntry,
    IssueEntry IssueEntry,
    ReviseReferenceRecordEntry ReviseReferenceRecordEntry,
    IFilePicker EvidenceFilePicker,
    EvidenceEditorSupport EvidenceSupport,
    KindEditorDeclarationRegistry KindEditorDeclarations,
    GlobalNavigationRail NavigationRail,
    ShellHeaderView Header,
    ContentControl ModuleHost,
    IProjectDirectory ProjectDirectory,
    ProjectBrowserView ProjectBrowser,
    ProjectWorkspaceView ProjectWorkspace,
    EngineeringCalculationView EngineeringCalculation,
    LibrariesView LibrariesView,
    Dictionary<Guid, IWorkspaceView> OpenGraphViewsByRootId,
    CommandHistoryLog CommandHistory,
    IBackgroundTaskRunner BackgroundTaskRunner,
    KeyboardCommandBindingProvider KeyboardBindingProvider,
    IWorkspace Workspace,
    WorkspaceManager Manager,
    Tempest.Core.Identity.IPrincipalDirectory Principals);

/// <summary>
/// <see cref="MainWindow"/>'s own methods, threaded into
/// <see cref="MainWindowComposer"/>'s later phases as plain delegates —
/// never a reference to <see cref="MainWindow"/> itself (`ADR-0103`: a
/// collaborator never references the composition root back). Each of
/// these stays a real method on <see cref="MainWindow"/> because it needs
/// several of that class's own fields together (an Explorer reveal, a
/// Navigation switch, a Document-Area open, all at once) and because it is
/// reached directly by acceptance tests, unchanged (`WP 19.2A`).
/// </summary>
internal sealed record MainWindowCallbacks(
    Action<string> RecordHistory,
    Action RefreshOutputPanelExtras,
    Action<WorkspaceManager> RefreshStatusBar,
    Action<string?> SetCurrentArea,
    Func<Guid, string, Task> OpenObjectAsync,
    Func<Guid, Guid, CancellationToken, Task> OpenProjectAttachmentAsync,
    Func<Guid, string, Task> OpenEvidenceRecordAsync,
    Func<string, string, Task<bool>> PromptForNewProjectAsync,
    Func<Task> RenderCurrentModuleAsync);

/// <summary>
/// Assembles <see cref="MainWindow"/>'s entire object graph — every view,
/// dialog, overlay and coordinator, every cross-view wiring, and the root
/// visual tree — in four explicit, ordered phases (`WP 19.2A`, `TD-109`):
/// <see cref="BuildViews"/> (every view/dialog/overlay that needs no
/// coordinator), <see cref="BuildCoordinators"/> (Undo/Redo, the Workspace
/// View coordinator, docking, layout presets, project delivery/governance,
/// the Evidence workspace — everything that needs a view from the first
/// phase), <see cref="Wire"/> (every cross-collaborator event, delegate,
/// palette binding and keyboard shortcut), <see cref="Layout"/> (the root
/// grid, dock, overlays and rail). Each phase returns a record the next
/// consumes; <see cref="MainWindow"/>'s own constructor is the short call
/// sequence over these four, plus its own <c>Opened</c>/<c>Closing</c>
/// handlers (see `docs/architecture/Desktop Composition Architecture.md`).
/// </summary>
/// <remarks>
/// A collaborator under `ADR-0103`: constructed once by
/// <see cref="MainWindow"/>, never DI-registered, never retaining a
/// reference to <see cref="MainWindow"/> itself — only to the already-built
/// collaborators each phase is handed, and to <see cref="MainWindowCallbacks"/>,
/// the plain delegates standing in for the few methods that must stay on
/// <see cref="MainWindow"/> (see that type's own remarks).
/// </remarks>
internal sealed partial class MainWindowComposer
{
    /// <summary>
    /// Builds every view, dialog and overlay that needs no coordinator to
    /// already exist — the great majority of <see cref="MainWindow"/>'s
    /// own collaborators. <see cref="DocumentAreaView"/> is built here,
    /// with its content builder left at the default
    /// (<see cref="DocumentAreaView.BuildDefaultBody"/>); <see cref="BuildCoordinators"/>
    /// sets <see cref="DocumentAreaView.ContentBuilder"/> once
    /// <c>WorkspaceViewCoordinator</c> exists, resolving that one
    /// construction-order cycle without either side reading a field behind
    /// a null-forgiving <c>!</c> (`WP 19.2A`).
    /// </summary>
    public ComposedViews BuildViews(WorkspaceHost host, Window window, IFilePicker? evidenceFilePickerOverride, MainWindowCallbacks callbacks)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(callbacks);

        var workspace = host.Workspace ?? throw new InvalidOperationException("WorkspaceHost must be started before constructing MainWindow.");
        var manager = host.Manager!;
        var services = host.Services!;

        var composition = new DesktopCompositionRoot(services);
        var diagnostics = composition.Diagnostics;

        var session = new DesktopSessionState(composition.SettingsProvider, composition.Logger);
        session.WindowUiState.ApplyTo(window);
        var toastHost = new ToastHost { DefaultDuration = TimeSpan.FromSeconds(session.UserSettings.ToastDurationSeconds) };

        var keyboardBindingProvider = new KeyboardCommandBindingProvider();
        composition.InputBindingRegistry.Register(keyboardBindingProvider);

        var commandHistory = new CommandHistoryLog();
        var backgroundTaskRunner = new BackgroundTaskRunner();

        var macroManagerDialog = new MacroManagerDialog(
            composition.MacroManager,
            composition.CommandRegistry,
            runMacro: async macroId =>
            {
                var descriptor = composition.CommandRegistry.Items.FirstOrDefault(d => d.Id == IMacroManager.CommandIdPrefix + macroId);
                var title = descriptor?.DisplayName ?? "Macro";

                var context = WorkspaceCommandContext.From(workspace.Selection);
                var result = await backgroundTaskRunner.RunAsync(
                    $"Running macro '{title}'…",
                    async ct =>
                    {
                        var invocation = await composition.CommandRegistry
                            .InvokeAsync(IMacroManager.CommandIdPrefix + macroId, context, prompt: null, ct)
                            .ConfigureAwait(false);

                        return invocation.Result
                            ?? CommandResult.Failure(invocation.Reason ?? "The macro could not be run.");
                    }).ConfigureAwait(true);

                commandHistory.Record($"Macro '{title}'", result.Succeeded);
                callbacks.RefreshOutputPanelExtras();

                // `WP 18.1A`: no explicit Explorer/Cockpit refresh here any
                // more — a macro is an arbitrary multi-command mutation,
                // and every one of its commands commits through the same
                // mutators as any other write, each raising its own
                // WorkspaceChanged. Explorer and Cockpit are both
                // subscribed and reload from that.
                return result;
            });

        var toastBridge = new PlatformNotificationToastBridge(toastHost);
        composition.EventBus.Subscribe(toastBridge);
        composition.NotificationDispatcher.Subscribe<Tempest.Core.Notifications.IPlatformNotification>(toastBridge);

        var theme = new ThemeService(composition.SettingsProvider);
        var settingsDialog = new SettingsDialog(theme, session.UserSettings, composition.SettingsProvider);

        var confirmationDialog = new ConfirmationDialog();
        var inputDialog = new InputDialog();
        var messageDialog = new MessageDialog();

        Task<bool> ConfirmDeleteAsync(string prompt) =>
            session.UserSettings.ConfirmBeforeDelete
                ? confirmationDialog.ConfirmAsync("Delete?", prompt, "Delete")
                : Task.FromResult(true);

        var explorerView = new ProjectExplorerView(workspace.ProjectExplorer, manager)
        {
            ConfirmDeleteAsync = ConfirmDeleteAsync,
            RecentSearchCapacity = session.UserSettings.RecentSearchCapacity,
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        var principals = (Tempest.Core.Identity.IPrincipalDirectory)services.GetService(typeof(Tempest.Core.Identity.IPrincipalDirectory));
        var inspectorView = new PropertyInspectorView(workspace.PropertyInspector, manager, composition.DomainContext, principals) { WorkspaceChanges = composition.WorkspaceChanges };
        var statusBar = new StatusBarView();
        var commandPalette = new CommandPaletteOverlay(composition.CommandRegistry);

        // The Object Editor Framework's own injectable content builder is
        // left at its default here — `BuildCoordinators` sets it once
        // `WorkspaceViewCoordinator.BuildDocumentContent` exists.
        var documentArea = new DocumentAreaView();

        var ribbon = new RibbonView(
            composition.CommandRegistry,
            manager,
            workspace,
            setHint: hint => statusBar.SetHint(hint),
            openDocument: view => documentArea.ShowTab(view))
        {
            ConfirmDeleteAsync = ConfirmDeleteAsync,
        };

        var commandPrompt = new DesktopCommandPrompt(
            inputDialog,
            confirm: (descriptor, message) => SurfaceCommandPolicy.DeleteCommandIds.Contains(descriptor.Id)
                ? ConfirmDeleteAsync(message)
                : confirmationDialog.ConfirmAsync("Confirm", message, "Continue"));
        ribbon.ParameterPrompt = commandPrompt.Prompt;

        var actionReporter = new ActionOutcomeReporter(statusBar, toastHost, callbacks.RecordHistory);

        var kindEditorDeclarations = new KindEditorDeclarationRegistry();
        KindEditorDeclarations.RegisterAll(kindEditorDeclarations);

        var evidenceFilePicker = evidenceFilePickerOverride ?? new AvaloniaFilePicker(window);
        var citationPicker = new CitationPicker(ct => LibrariesView.ReadAllAsync(
            host.Materials!, host.Fasteners!, host.Bearings!, host.Standards!, host.Constants!, ct));
        var subjectPicker = new SubjectPicker(composition.DomainContext);
        var declaredFigureEntry = new DeclaredFigureEntry();
        var checkEntry = new CheckEntry();
        var issueEntry = new IssueEntry();
        var reviseReferenceRecordEntry = new ReviseReferenceRecordEntry();

        var evidenceSupport = new EvidenceEditorSupport(
            evidenceFilePicker,
            ct => citationPicker.PickAsync(ct),
            ct => declaredFigureEntry.PromptAsync(ct),
            ct => subjectPicker.PickAsync(ct),
            ct => checkEntry.PromptAsync(ct),
            ct => issueEntry.PromptAsync(ct));

        var navigationRail = new GlobalNavigationRail(host.ShellNavigator!);

        var header = new ShellHeaderView();
        header.SetPrincipal(host.SessionPrincipal?.Identity.DisplayName);

        var moduleHost = new ContentControl();

        var projectDirectory = host.ProjectDirectory!;
        var projectBrowser = new ProjectBrowserView(projectDirectory, host.ShellNavigator!, callbacks.PromptForNewProjectAsync);
        var projectWorkspace = new ProjectWorkspaceView(
            host.ProjectContext!, host.ProjectDirectory!, host.ShellNavigator!, host.ProjectDocuments!, host.ProjectRequirements!,
            host.ProjectTasks!, host.ProjectGovernance!, host.ProjectMilestones!);

        var engineeringCalculation = new EngineeringCalculationView(principals.Describe);

        var librariesView = new LibrariesView(
            host.Materials!, host.Fasteners!, host.Bearings!, host.Standards!, host.Constants!,
            host.ReferenceReview!, host.BracketCalculations!)
        {
            ReviseRecordPrompt = (label, definitionJson, source, ct) => reviseReferenceRecordEntry.PromptAsync(label, definitionJson, source, ct),
        };
        librariesView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        return new ComposedViews(
            composition, diagnostics, session, theme, settingsDialog, toastHost, new BusyOverlay(), confirmationDialog, inputDialog, messageDialog,
            macroManagerDialog, explorerView, inspectorView, statusBar, commandPalette, documentArea, ribbon, commandPrompt, actionReporter,
            citationPicker, subjectPicker, declaredFigureEntry, checkEntry, issueEntry, reviseReferenceRecordEntry, evidenceFilePicker,
            evidenceSupport, kindEditorDeclarations, navigationRail, header, moduleHost, projectDirectory, projectBrowser, projectWorkspace,
            engineeringCalculation, librariesView, [], commandHistory, backgroundTaskRunner, keyboardBindingProvider, workspace, manager, principals);
    }
}
