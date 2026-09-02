using Avalonia.Controls;
using Avalonia.Input;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Macros;
using Tempest.Desktop.Composition;
using Tempest.Desktop.History;
using Tempest.Desktop.Input;
using Tempest.Desktop.Tasks;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop;

/// <summary>
/// The Main Window (`WP 10.0B`; modernised `WP 10.2A` — Workspace
/// Modernisation; extended `WP 10.2B` — Docking &amp; Workspace Layouts;
/// given real Object Editors `WP 10.3A`; given the Engineering Ribbon
/// &amp; Command Experience `WP 10.3B`) — assembles every named
/// implementation item across all five Work Packages (Docking framework
/// incl. Bottom dock and the Output panel, Collapse, Auto-Hide,
/// predefined/saved/reset layouts, Panel host, Document host wired to the
/// real Object Editor Framework, a real, tabbed Engineering Ribbon
/// consolidating the old Navigation Framework button row, a Quick Access
/// Toolbar, Menu system, Command Palette host, Keyboard shortcut
/// framework; real object Rename/Delete/Revise dispatch, `ADR-0096`/
/// `ADR-0097`, a seven-segment Status Bar including a live command hint,
/// and document switching shortcuts) into one running window over the
/// real, unchanged Engineering Workspace six real disciplines already
/// populate.
/// </summary>
/// <remarks>
/// **Composition root, `WP 12.0B` (`ADR-0103`).** Previously a
/// ~1,550-line, ~1,000-line-constructor "God Object" (Finding A-1,
/// `WP11.0A Platform Architecture Review.md`). Every object-graph
/// responsibility this class does not need to keep for itself now lives
/// in its own <c>Tempest.Desktop.Composition</c> collaborator — see each
/// one's own XML docs for its own single reason to change. This class
/// itself, per `ADR-0103`, retains only what is irreducibly a composition
/// root's own job: root visual-tree assembly (it *is* the
/// <see cref="Window"/>), constructing every collaborator with
/// <c>new</c>, and wiring the genuinely cross-collaborator bridges no
/// single collaborator can own. See `docs/architecture/Desktop
/// Composition Architecture.md` and `ADR-0103` for the full pattern this
/// class is the motivating realisation of.
/// </remarks>
public sealed class MainWindow : Window
{
    private readonly ThemeService _theme;
    private readonly ProjectExplorerView _explorerView;
    private readonly PropertyInspectorView _inspectorView;
    private readonly DocumentAreaView _documentArea;
    private readonly StatusBarView _statusBar;
    private readonly CommandPaletteOverlay _commandPalette;
    private readonly WorkspaceDockingComposer _dockingComposer;
    private readonly Viewing.AttachmentViewerLauncher _attachmentViewers;
    private readonly CockpitView _cockpitView;
    private readonly IDiagnosticsProvider _diagnostics;
    private readonly RibbonView _ribbon;
    private readonly Dictionary<Guid, IWorkspaceView> _openGraphViewsByRootId = new();
    private readonly ToastHost _toastHost = new();
    private readonly BusyOverlay _busyOverlay = new();
    private readonly ConfirmationDialog _confirmationDialog = new();
    private readonly InputDialog _inputDialog = new();
    private readonly MessageDialog _messageDialog = new();
    private readonly DesktopSessionState _session;
    private readonly SettingsDialog _settingsDialog;
    private readonly WorkspaceViewCoordinator _viewCoordinator;
    private readonly UndoRedoCoordinator _undoRedo;
    private readonly WorkspaceLayoutPresetCoordinator _layoutPresets;

    // The Product Spine (`TD-84`) — Module -> Project -> Workspace.
    private readonly IShellNavigator _navigator;
    private readonly IProjectContext _projectContext;
    private readonly GlobalNavigationRail _navigationRail;
    private readonly ProjectBrowserView _projectBrowser;
    private readonly ProjectWorkspaceView _projectWorkspace;
    private readonly ContentControl _moduleHost = new();
    private readonly Control _engineeringSurface;
    private readonly IProjectDirectory _projectDirectory;

    // The open project's own tasks/milestones/deliverables and its
    // risks/issues/decisions (`ADR-0103` collaborators, `WP-G`) — the CRUD
    // interaction logic `TD-109` named, moved out of this class verbatim.
    private readonly ProjectDeliveryCoordinator _projectDelivery;
    private readonly ProjectGovernanceCoordinator _projectGovernanceCoordinator;

    // WP 10.6A — Command Execution & Productivity Experience.
    private readonly CommandHistoryLog _commandHistory = new();
    private readonly IBackgroundTaskRunner _backgroundTaskRunner = new BackgroundTaskRunner();
    private readonly ActionOutcomeReporter _actionReporter;
    private readonly KeyboardCommandBindingProvider _keyboardBindingProvider = new();
    private readonly MacroManagerDialog _macroManagerDialog;

    private readonly IEngineeringScope _engineeringScope;
    private readonly EngineeringDomainContext _domainContext;

    private string? _currentAreaTitle;
    private bool _closeConfirmed;

    /// <summary>Initialises a new instance of the <see cref="MainWindow"/> class over an already-started <see cref="WorkspaceHost"/>.</summary>
    public MainWindow(WorkspaceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var workspace = host.Workspace ?? throw new InvalidOperationException("WorkspaceHost must be started before constructing MainWindow.");
        var manager = host.Manager!;
        var services = host.Services!;

        // Platform Service resolution (`ADR-0103` collaborator #1) —
        // mirrors EngineeringWorkspaceComposer's own "Desktop-specific
        // composition step" shape.
        var composition = new DesktopCompositionRoot(services);
        _diagnostics = composition.Diagnostics;

        Title = "TempestOS — Engineering Workspace";
        MinWidth = 960;
        MinHeight = 600;

        // Desktop-local session state (`ADR-0103` collaborator #2) —
        // every independent, synchronously-loaded, per-session persisted
        // state. Window geometry restoration applies immediately, the
        // identical established discipline this codebase already uses,
        // so the very first frame already reflects last session's own
        // geometry, never a default-then-jump.
        _session = new DesktopSessionState(composition.SettingsProvider, composition.Logger);
        _session.WindowUiState.ApplyTo(this);
        _toastHost.DefaultDuration = TimeSpan.FromSeconds(_session.UserSettings.ToastDurationSeconds);

        // Keyboard is just another IInputBindingProvider (`WP 10.6A`,
        // `ADR-0100`) — registered against the shared IInputBindingRegistry
        // Platform Service, the identical mechanism a future real Stream
        // Deck/MIDI/game controller provider would register against.
        composition.InputBindingRegistry.Register(_keyboardBindingProvider);

        _macroManagerDialog = new MacroManagerDialog(
            composition.MacroManager,
            composition.CommandRegistry,
            runMacro: async macroId =>
            {
                var descriptor = composition.CommandRegistry.Items.FirstOrDefault(d => d.Id == IMacroManager.CommandIdPrefix + macroId);
                var title = descriptor?.DisplayName ?? "Macro";

                // The one real Background Task Runner consumer this
                // Work Package wires (`WP 10.6A` §4) — a macro's own
                // multi-step invocation is the one genuinely "could take
                // a moment" case in this platform today.
                //
                // The context is captured here, at macro start, and every
                // step replays it (`ADR-0098`) — the identical shape the
                // palette's own macro path uses (`WP-A1`); before that this
                // ran through the obsolete Id-only overload with no context
                // at all, so every object-scoped step reported "needs a
                // selected object" however the workspace was selected. No
                // prompt is passed, so a parameterised step fails honestly
                // rather than interrupting an unattended run.
                var context = WorkspaceCommandContext.From(workspace.Selection);
                var result = await _backgroundTaskRunner.RunAsync(
                    $"Running macro '{title}'…",
                    async ct =>
                    {
                        var invocation = await composition.CommandRegistry
                            .InvokeAsync(IMacroManager.CommandIdPrefix + macroId, context, prompt: null, ct)
                            .ConfigureAwait(false);

                        return invocation.Result
                            ?? CommandResult.Failure(invocation.Reason ?? "The macro could not be run.");
                    }).ConfigureAwait(true);

                _commandHistory.Record($"Macro '{title}'", result.Succeeded);
                RefreshOutputPanelExtras();

                // A macro is an arbitrary multi-command mutation — the
                // Explorer/Cockpit previously stayed stale after one (`TD-58`).
                //
                // `!` on both fields is the same field-closure lazy-capture
                // pattern `_cockpitView!`/`_documentArea!` already use below
                // (`WP 12.4B`, `ADR-0104`): this lambda is *constructed* here,
                // before `_explorerView` (assigned ~line 189) and
                // `_cockpitView` (~line 247) exist, but it is only ever
                // *invoked* by MacroManagerDialog after construction has
                // fully completed, by which point both are always assigned.
                // Suppressed at exactly these two provably-safe dereferences
                // rather than by relaxing nullable analysis anywhere.
                if (result.Succeeded)
                {
                    await _explorerView!.LoadAsync().ConfigureAwait(true);
                    _cockpitView!.Refresh();
                }

                return result;
            });

        // The Notification Framework's own first real Desktop consumer
        // (`WP 10.5B`) — every `IPlatformNotification` this platform
        // already publishes (background tasks, sample modules, any
        // future long-running operation) now reaches a real Toast.
        var toastBridge = new PlatformNotificationToastBridge(_toastHost);
        composition.EventBus.Subscribe(toastBridge);

        // Every real producer publishes through INotificationDispatcher,
        // not the event bus — without this second subscription no
        // platform notification ever reached a toast (`TD-58` stale-UI
        // closure; confirmed dead wiring by whole-repository search).
        composition.NotificationDispatcher.Subscribe<Tempest.Core.Notifications.IPlatformNotification>(toastBridge);

        _theme = new ThemeService(composition.SettingsProvider);
        _settingsDialog = new SettingsDialog(_theme, _session.UserSettings);

        // The Delete Confirmation gate (`WP 10.5B`, Dialog Framework) —
        // one real implementation, wired identically into every Delete
        // path (Ribbon button, Project Explorer context menu, Delete key).
        // Honours `UserSettings.ConfirmBeforeDelete` — a user who
        // deliberately opts out via Preferences gets the pre-`WP 10.5B`
        // immediate-delete behaviour back, never forced through a prompt
        // they turned off.
        Task<bool> ConfirmDeleteAsync(string prompt) =>
            _session.UserSettings.ConfirmBeforeDelete
                ? _confirmationDialog.ConfirmAsync("Delete?", prompt, "Delete")
                : Task.FromResult(true);

        _explorerView = new ProjectExplorerView(workspace.ProjectExplorer, manager) { ConfirmDeleteAsync = ConfirmDeleteAsync, RecentSearchCapacity = _session.UserSettings.RecentSearchCapacity };
        _inspectorView = new PropertyInspectorView(workspace.PropertyInspector, manager, composition.DomainContext);
        _statusBar = new StatusBarView();
        _commandPalette = new CommandPaletteOverlay(composition.CommandRegistry);

        // The Engineering Ribbon (`WP 10.3B`) — a real, tabbed command
        // surface over the existing ICommandRegistry, replacing the old
        // Navigation Framework's own standalone area-switch button row
        // (`WP 10.0B`) — a Ribbon tab click both switches the Navigation
        // area (via CategorySelected, below) and shows that discipline's
        // own real commands, so the two concerns need only one control,
        // not two. Never a second registration mechanism (`ADR-0070`) —
        // every button dispatches a real, already-registered command.
        // `openDocument` reads `_documentArea` lazily (assigned below) —
        // never invoked before this constructor fully returns.
        _ribbon = new RibbonView(
            composition.CommandRegistry,
            manager,
            workspace,
            setHint: hint => _statusBar.SetHint(hint),
            openDocument: view => _documentArea!.ShowTab(view))
        {
            ConfirmDeleteAsync = ConfirmDeleteAsync,
        };

        // Undo/Redo (`ADR-0103` collaborator #3, `WP 10.6A`/`ADR-0099`) —
        // constructed before WorkspaceViewCoordinator, which needs its
        // own Stack. `WP 12.4B` (`ADR-0104`): its own CockpitView-refresh
        // need is a plain `Action` delegate, not an object reference —
        // The one report-then-refresh tail (`WP-D1`, `TD-111`) — built here,
        // in the composition root, and handed to every collaborator that
        // reports an action. It owns the presentation consequences only;
        // each caller still supplies its own refresh set.
        _actionReporter = new ActionOutcomeReporter(_statusBar, _toastHost, RecordHistory);

        // `() => _cockpitView!.Refresh()` is the same field-closure
        // lazy-capture pattern `_documentArea!` already uses just below;
        // `_cockpitView` is a `readonly` field assigned later, at line
        // ~209, but this lambda is only ever invoked after construction
        // fully completes, by which point it is always assigned.
        _undoRedo = new UndoRedoCoordinator(_explorerView, refreshCockpit: () => _cockpitView!.Refresh(), _actionReporter);

        // Explorer/Inspector/Document-Area cross-view coordination
        // (`ADR-0103` collaborator #4) — DocumentAreaView is attached
        // once it exists, below (see WorkspaceViewCoordinator's own
        // remarks for why that one cycle needs two phases); its own
        // CockpitView-refresh need is the identical `Action` delegate
        // passed to UndoRedoCoordinator above, `WP 12.4B` (`ADR-0104`).
        _viewCoordinator = new WorkspaceViewCoordinator(
            workspace, manager, composition.DomainContext, composition.CommandDispatcher, composition.RequirementsService, host.CalculationTemplates,
            _explorerView, _inspectorView, _ribbon, _statusBar, _toastHost, _confirmationDialog, _undoRedo.Stack,
            _session.RecentObjects, _session.FavouriteObjects, _openGraphViewsByRootId,
            refreshStatusBar: () => RefreshStatusBar(manager), recordHistory: RecordHistory, refreshCockpit: () => _cockpitView!.Refresh(), _actionReporter);

        _documentArea = new DocumentAreaView(_viewCoordinator.BuildDocumentContent);

        // The Engineering Cockpit (WP 10.1A, ADR-0069) — the Workspace's
        // own default landing screen, realised here as the Document Area's
        // own permanent Home tab. Workspace (the concrete class) is
        // internal, reached via InternalsVisibleTo("Tempest.Desktop")
        // (granted `WP 10.0B`) — the identical, precedented pattern
        // WorkspaceShell itself already uses internally to reach its own
        // concrete Workspace instance.
        var cockpit = ((Workspace)workspace).Cockpit;
        _cockpitView = new CockpitView(
            cockpit,
            workspace.Navigation.Areas,
            onContinue: () => cockpit.ContinueAsync().GetAwaiter().GetResult(),
            onOpenRecent: index =>
            {
                var view = cockpit.OpenRecentAsync(index).GetAwaiter().GetResult();
                _documentArea.ShowTab(view);
            },
            onOpenCommandPalette: () => _commandPalette.Open(),
            onSwitchArea: async areaId =>
            {
                await workspace.Navigation.SwitchAreaAsync(areaId).ConfigureAwait(true);
                await _explorerView.LoadAsync().ConfigureAwait(true);
                SetCurrentArea(workspace.Navigation.Areas.FirstOrDefault(a => a.Id == areaId)?.Title);
            },
            // WP 10.7A — Feature Completion: the "Favourite Projects"
            // card's own real source and open-callback, reusing the
            // identical NavigateToObject every other Cockpit/Object
            // Editor navigation action already calls.
            favourites: _session.FavouriteObjects,
            onOpenFavourite: _viewCoordinator.NavigateToObject);
        _documentArea.SetHomeTab(_cockpitView);

        _viewCoordinator.Attach(_documentArea);

        // Panel construction/resize/hide/collapse/pin/flyout wiring
        // (`ADR-0103` collaborator #5, `WP 10.2B`).
        _dockingComposer = new WorkspaceDockingComposer(workspace, _explorerView, _inspectorView, _documentArea, _session.PanelUiState, _session.LayoutStore);

        // `TD-80`: the document and drawing viewer. Constructed over the
        // docking composer's own registry and layout controller, so a
        // viewer is an ordinary `TD-72` panel — it tabs with the document
        // area, splits, floats onto a second monitor and persists with no
        // code here for any of it, and opening a second document is the
        // same call again rather than a second reserved slot.
        _attachmentViewers = new Viewing.AttachmentViewerLauncher(
            _dockingComposer.Registry, _dockingComposer.Layout, _dockingComposer.DocumentPanelId);

        // Opening a document never navigates: the shell stays where it is,
        // so the project, the open object and the Explorer selection are
        // all still there when the viewer tab is closed.
        _viewCoordinator.OpenAttachmentAsync = (owner, attachment) =>
            _attachmentViewers.OpenAsync(owner, attachment, Bounds.Width, Bounds.Height);

        // Click-away: a pointer press landing directly on the Document
        // Area (never intercepted by the flyout itself, which sits above
        // it in Z-order while open) closes any open Auto-Hide flyout —
        // the deliberate "click to peek, click away to dismiss"
        // interaction model (`WP10.2B UX Review.md` §3), chosen over
        // hover-to-peek/dwell-timer for determinism, keyboard parity
        // (`Escape`, below), and headless testability.
        _documentArea.PointerPressed += (_, _) =>
        {
            if (_dockingComposer.IsFlyoutOpen)
                _dockingComposer.CloseFlyout();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _dockingComposer.IsFlyoutOpen)
                _dockingComposer.CloseFlyout();
        };

        // TD-77 Stage 5. The ~390-line RibbonObjectActionHandlers is gone:
        // every one of its per-discipline Create/Duplicate/status-transition
        // closures re-derived, by hand, what a command needed and how to
        // build it - which is exactly what a CommandBinding now declares
        // once, beside the handler it was registered with. The Ribbon and
        // the Palette both invoke through the registry instead, and this is
        // the one prompt implementation they share.
        //
        // Confirmation policy stays here, where the settings live: a delete
        // honours UserSettings.ConfirmBeforeDelete exactly as before, while
        // every other declared confirmation (the six Duplicates) is
        // unconditional, which is what their bindings mean.
        var commandPrompt = new DesktopCommandPrompt(
            _inputDialog,
            confirm: (descriptor, message) => SurfaceCommandPolicy.DeleteCommandIds.Contains(descriptor.Id)
                ? ConfirmDeleteAsync(message)
                : _confirmationDialog.ConfirmAsync("Confirm", message, "Continue"));

        _ribbon.ParameterPrompt = commandPrompt.Prompt;

        // Reported through the one shared tail (`WP-D1`). Refused/failed
        // actions changed nothing — a full Explorer reload and Cockpit
        // rebuild for them was `TD-58`'s core redundant-rebuild path.
        _ribbon.ActionCompleted += async (message, outcome) =>
            await _actionReporter.ReportAsync(message, outcome, refresh: async () =>
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _cockpitView.Refresh();
            }).ConfigureAwait(true);

        // Background-task state changes drive the Output panel's own
        // Background Tasks list directly (`TD-58` stale-UI closure) —
        // previously `Changed` had no subscriber at all, so a running
        // macro never showed "Running" and completion appeared only
        // after the next unrelated action happened to refresh the panel.
        _backgroundTaskRunner.Changed += RefreshOutputPanelExtras;

        // Ribbon minimise (`TD-70`) — restore the persisted state, and
        // record every later change so it survives the next restart.
        _ribbon.SetCollapsed(_session.PanelUiState.RibbonCollapsed);
        _ribbon.CollapsedChanged += collapsed => _session.PanelUiState.RibbonCollapsed = collapsed;

        _ribbon.CategorySelected += async category =>
        {
            var area = workspace.Navigation.Areas.FirstOrDefault(a => a.Title.Contains(category, StringComparison.OrdinalIgnoreCase));
            if (area is null)
                return;

            // A real Busy Overlay usage site (`WP 10.5A`) — switching
            // Navigation area re-populates the entire Project Explorer
            // tree; genuinely worth a real loading indicator, unlike the
            // many single-object `LoadAsync` refreshes elsewhere in this
            // class, which stay fast enough that a busy overlay would only
            // flicker.
            await _busyOverlay.RunAsync($"Switching to {area.Title}…", async () =>
            {
                await workspace.Navigation.SwitchAreaAsync(area.Id).ConfigureAwait(true);
                await _explorerView.LoadAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            SetCurrentArea(area.Title);
        };

        // Named layout presets (`ADR-0103` collaborator #7, `WP 10.2B`).
        _layoutPresets = new WorkspaceLayoutPresetCoordinator(
            _dockingComposer.ApplyPreset, _dockingComposer.ResetLayout, _statusBar);

        // Menu System / Quick Access Toolbar (`ADR-0103` collaborators #8
        // — stateless build functions, `WP 10.0B`/`WP 10.3B`).
        var menu = MainMenuFactory.Build(
            workspace, _dockingComposer.Layout,
            _dockingComposer.ExplorerPanelId, _dockingComposer.InspectorPanelId, _dockingComposer.OutputPanelId,
            _session.PanelUiState, _dockingComposer.OutputPanel, _dockingComposer.OutputView, _diagnostics,
            _theme, _settingsDialog, _messageDialog, _commandPalette, _documentArea, _ribbon, _layoutPresets.Apply, _layoutPresets.Reset);
        var quickAccessToolbar = QuickAccessToolbarFactory.Build(
            workspace, composition.DomainContext, _viewCoordinator.NavigateToObject, _statusBar, _documentArea, _commandPalette, _theme,
            _layoutPresets.Reset, _macroManagerDialog, _undoRedo.UndoButton, _undoRedo.RedoButton, _openGraphViewsByRootId);

        var topStack = new StackPanel();
        topStack.Children.Add(menu);
        topStack.Children.Add(quickAccessToolbar);
        topStack.Children.Add(_ribbon);
        topStack.Children.Add(new Separator());

        // ---- The Product Spine's own shell composition (`TD-84`) ----
        // The Engineering surface (ribbon + docking grid) is no longer the
        // whole application: it is one module inside a global shell whose
        // first level is the navigation rail and whose second is a project.
        _navigator = host.ShellNavigator!;
        _projectContext = host.ProjectContext!;
        _engineeringScope = host.EngineeringScope!;
        _domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));

        var engineeringStack = new DockPanel();
        DockPanel.SetDock(topStack, Dock.Top);
        engineeringStack.Children.Add(topStack);
        engineeringStack.Children.Add(_dockingComposer.View);
        _engineeringSurface = engineeringStack;

        _projectDirectory = host.ProjectDirectory!;
        _projectBrowser = new ProjectBrowserView(_projectDirectory, _navigator, PromptForNewProjectAsync);
        _projectWorkspace = new ProjectWorkspaceView(
            _projectContext, host.ProjectDirectory!, _navigator, host.ProjectDocuments!, host.ProjectRequirements!,
            host.ProjectTasks!, host.ProjectGovernance!, host.ProjectMilestones!);

        // The two project-CRUD collaborators (`WP-G`, `ADR-0103`). They own
        // the interaction logic this class used to hold inline; the wiring
        // below still says which surface event runs which operation, which
        // is the composition root's own job and stays here.
        _projectDelivery = new ProjectDeliveryCoordinator(
            _projectContext, host.ProjectTaskWorkflow!, host.ProjectTasks!,
            host.ProjectMilestoneWorkflow!, host.ProjectMilestones!,
            _projectWorkspace, _inputDialog, _toastHost, RecordHistory);

        _projectGovernanceCoordinator = new ProjectGovernanceCoordinator(
            _projectContext, host.ProjectGovernanceWorkflow!, host.ProjectGovernance!,
            _projectWorkspace, _inputDialog, _toastHost, RecordHistory);

        _navigationRail = new GlobalNavigationRail(_navigator);

        _navigationRail.NavigationRequested += () => _ = RenderCurrentModuleAsync();
        _projectBrowser.ProjectOpened += () => _ = RenderCurrentModuleAsync();
        _projectWorkspace.EngineeringRequested += () => _ = RenderCurrentModuleAsync();
        _projectWorkspace.ProjectClosed += () => _ = RenderCurrentModuleAsync();

        // The Documents area opens a file through the same `TD-80`
        // launcher the object editor uses. It goes through the shell
        // rather than straight to the launcher for the same reason the
        // editor does: the shell owns where a document opens, and the
        // project area that asked keeps no knowledge of the workspace
        // layout at all.
        _projectWorkspace.OpenAttachmentRequested += (ownerId, attachmentId) =>
            _ = OpenProjectAttachmentAsync(ownerId, attachmentId);

        // The Tasks area raises intent; the shell performs it through
        // IProjectTaskService and re-renders. Same shape as the Documents
        // area's own Open button, and for the same reason: the surface
        // that asked stays free of both the domain and the layout.
        _projectWorkspace.CreateTaskRequested += () => _ = _projectDelivery.CreateProjectTaskAsync();
        _projectWorkspace.AssignTaskToMeRequested += taskId => _ = _projectDelivery.AssignProjectTaskToMeAsync(taskId);
        _projectWorkspace.TaskWorkStateChangeRequested += (taskId, target) => _ = _projectDelivery.ChangeProjectTaskWorkStateAsync(taskId, target);
        _projectWorkspace.CreateRiskRequested += () => _ = _projectGovernanceCoordinator.CreateProjectRiskAsync();
        _projectWorkspace.CreateIssueRequested += () => _ = _projectGovernanceCoordinator.CreateProjectIssueAsync();
        _projectWorkspace.CreateDecisionRequested += () => _ = _projectGovernanceCoordinator.CreateProjectDecisionAsync();
        _projectWorkspace.RiskStatusChangeRequested += (id, target) => _ = _projectGovernanceCoordinator.ChangeProjectRiskStatusAsync(id, target);
        _projectWorkspace.IssueStatusChangeRequested += (id, target) => _ = _projectGovernanceCoordinator.ChangeProjectIssueStatusAsync(id, target);
        _projectWorkspace.DecisionStatusChangeRequested += (id, target) => _ = _projectGovernanceCoordinator.DecideProjectDecisionAsync(id, target);
        _projectWorkspace.OwnRiskRequested += id => _ = _projectGovernanceCoordinator.OwnProjectRiskAsync(id);
        _projectWorkspace.AssignIssueToMeRequested += id => _ = _projectGovernanceCoordinator.AssignProjectIssueToMeAsync(id);
        _projectWorkspace.ScoreRiskRequested += id => _ = _projectGovernanceCoordinator.ScoreProjectRiskAsync(id);
        _projectWorkspace.EditRiskRequested += id => _ = _projectGovernanceCoordinator.EditProjectGovernanceObjectAsync(id, GovernanceFamily.Risk);
        _projectWorkspace.EditIssueRequested += id => _ = _projectGovernanceCoordinator.EditProjectGovernanceObjectAsync(id, GovernanceFamily.Issue);
        _projectWorkspace.EditDecisionRequested += id => _ = _projectGovernanceCoordinator.EditProjectGovernanceObjectAsync(id, GovernanceFamily.Decision);
        _projectWorkspace.CreateMilestoneRequested += () => _ = _projectDelivery.CreateProjectMilestoneAsync();
        _projectWorkspace.AddDeliverableRequested += id => _ = _projectDelivery.AddProjectDeliverableAsync(id);
        _projectWorkspace.EditMilestoneRequested += id => _ = _projectDelivery.EditProjectMilestoneAsync(id);
        _projectWorkspace.EditTaskRequested += taskId => _ = _projectDelivery.EditProjectTaskAsync(taskId);
        _projectWorkspace.TaskDueDateChangeRequested += taskId => _ = _projectDelivery.ChangeProjectTaskDueDateAsync(taskId);

        // `TD-104`: rehydration that could not recover everything is a
        // fact about the user's own engineering work, so it is said out
        // loud here rather than left in a log file. Recovery still
        // recovers everything it can — refusing to start would lose the
        // rest — but an unknown Kind means durable work is missing from
        // this session, and a user who is never told will assume it was
        // never saved.
        ReportIncompleteRehydration(host.RehydrationResult);

        // The shell carries its module surface from construction, not from
        // a later window event: a window that exists but hosts nothing is a
        // window whose menus and commands are unreachable.
        _moduleHost.Content = _navigator.Current.Area switch
        {
            ShellArea.Projects => _projectBrowser,
            ShellArea.ProjectWorkspace => _projectWorkspace,
            _ => _engineeringSurface,
        };

        var shell = new DockPanel();
        DockPanel.SetDock(_navigationRail, Dock.Left);
        shell.Children.Add(_navigationRail);
        shell.Children.Add(_moduleHost);

        var dock = new DockPanel();
        DockPanel.SetDock(_statusBar, Dock.Bottom);
        dock.Children.Add(_statusBar);
        dock.Children.Add(shell);

        // `WP 10.5A`'s own three new overlay surfaces — added last, so
        // each renders above every other root child (Grid Z-order follows
        // Children order for overlapping siblings), exactly like
        // `_commandPalette` already does.
        var root = new Grid();
        root.Children.Add(dock);
        root.Children.Add(_busyOverlay);
        root.Children.Add(_commandPalette);
        root.Children.Add(_confirmationDialog);
        root.Children.Add(_inputDialog);
        root.Children.Add(_messageDialog);
        root.Children.Add(_settingsDialog);
        root.Children.Add(_macroManagerDialog);
        root.Children.Add(_toastHost);
        Content = root;

        var shortcutActions = new KeyboardShortcutActions(
            openCommandPalette: () => _commandPalette.Open(),
            selectNextDocument: () => _documentArea.SelectNextTab(),
            selectPreviousDocument: () => _documentArea.SelectPreviousTab(),
            closeActiveDocument: () =>
            {
                if (_documentArea.ActiveClosableViewId is { } viewId)
                    _ = _viewCoordinator.CloseDocumentAsync(viewId);
            },
            focusExplorerFilter: () => _explorerView.FocusFilter(),
            undo: () => _ = _undoRedo.UndoAsync(),
            redo: () => _ = _undoRedo.RedoAsync(),
            toggleFavourite: () =>
            {
                if (workspace.Selection.Current is { } selection)
                {
                    var target = composition.DomainContext.Repository.FindAsync(selection.ObjectId).GetAwaiter().GetResult();
                    var title = (target as IHasBusinessIdentifier)?.DisplayName ?? selection.Kind;
                    _viewCoordinator.ToggleFavourite(selection.ObjectId, selection.Kind, title);
                }
                else
                {
                    _statusBar.SetText("Select an object first to favourite it.");
                }
            });
        KeyboardShortcuts.Register(this, shortcutActions);

        // Keyboard as an IInputBindingProvider (`WP 10.6A`) — handled
        // after the fixed KeyboardShortcuts above, so a fixed binding
        // always takes priority over a user-configured one for the same
        // gesture (KeyboardCommandBindingProvider.HandleKeyDown's own
        // "already Handled" guard).
        KeyDown += (_, e) => _keyboardBindingProvider.HandleKeyDown(e);

        // TD-77 Stage 5: the palette evaluates and invokes against the real
        // selection, through the same adapter the Ribbon uses, and collects
        // declared values through the same one prompt.
        _commandPalette.ContextSource = () => WorkspaceCommandContext.From(workspace.Selection);
        _commandPalette.ParameterPrompt = commandPrompt.Prompt;

        // `WP-A2`: a bound gesture now asks the same question the Ribbon and
        // the Palette ask, and gets the same answers — the same selection
        // adapter and the same one prompt. Until then the router used the
        // obsolete Id-only overload, which throws for every production
        // command, so a bound key would have looked like a dead key. Nothing
        // is bound today (`AT-23`, a product choice, not a defect shield);
        // this is what makes the first binding anyone adds actually work.
        composition.InputBindingRegistry.ContextSource = () => WorkspaceCommandContext.From(workspace.Selection);
        composition.InputBindingRegistry.ParameterPrompt = commandPrompt.Prompt;

        _commandPalette.InvokeOverride = async (descriptor, context) =>
        {
            if (!descriptor.Id.StartsWith(IMacroManager.CommandIdPrefix, StringComparison.Ordinal))
            {
                return await composition.CommandRegistry
                    .InvokeAsync(descriptor.Id, context, commandPrompt.Prompt)
                    .ConfigureAwait(true);
            }

            // Macro invocation (`WP 10.6A`) routes through the
            // Background Task Runner — the one real "could take a
            // moment" case in this platform. The context is captured here,
            // at macro start, and every step replays it (`ADR-0098`); no
            // prompt is passed, so a parameterised step fails honestly
            // rather than interrupting an unattended run.
            // IBackgroundTaskRunner reports a CommandResult, unchanged by
            // TD-77. A macro that could not be run at all is reported as
            // the failure it is, rather than widening that contract.
            var macroResult = await _backgroundTaskRunner.RunAsync(
                $"Running macro '{descriptor.DisplayName}'…",
                async ct =>
                {
                    var invocation = await composition.CommandRegistry
                        .InvokeAsync(descriptor.Id, context, prompt: null, ct)
                        .ConfigureAwait(false);

                    return invocation.Result
                        ?? CommandResult.Failure(invocation.Reason ?? "The macro could not be run.");
                }).ConfigureAwait(true);

            return CommandInvocation.Executed(macroResult);
        };
        _commandPalette.CommandInvoked += async (descriptor, result) =>
        {
            RecordHistory(result.Succeeded
                ? $"Invoked '{descriptor.DisplayName}' via Command Palette."
                : $"'{descriptor.DisplayName}' failed via Command Palette: {result.Message ?? "Command failed."}");
            RefreshStatusBar(manager);

            // Success-gated (`TD-58`): a failed command changed nothing;
            // a successful one may have mutated the domain, so the
            // Explorer (previously left stale here) reloads too.
            if (result.Succeeded)
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _cockpitView.Refresh();
            }
        };
        _commandPalette.CommandUnavailable += (descriptor, reason) =>
        {
            // The command's own declared reason (TD-77 Stage 5) — what is
            // actually missing, not a guess at where else to try.
            _statusBar.SetText(reason);
            _toastHost.Show(reason, FeedbackSeverity.Warning);
        };

        Opened += async (_, _) =>
        {
            await _theme.LoadAsync().ConfigureAwait(true);

            // The workspace arrangement the user left (`TD-72`) — restored
            // before anything renders, so the shell never flashes a default
            // layout and then rearranges itself underneath them.
            await _dockingComposer.RestoreLayoutAsync().ConfigureAwait(true);

            // No Explorer area is selected by default — the Engineering
            // Cockpit, not an Explorer area, is the Workspace's own default
            // landing screen (ADR-0069). Selecting the first available area
            // here gives the Project Explorer real content to show
            // immediately, mirroring what clicking the first Navigation
            // Framework button would do — a presentation-layer default,
            // not a change to that existing "no default area" behaviour
            // itself.
            var firstArea = workspace.Navigation.Areas.FirstOrDefault();
            if (firstArea is not null)
                await workspace.Navigation.SwitchAreaAsync(firstArea.Id).ConfigureAwait(true);

            await _explorerView.LoadAsync().ConfigureAwait(true);
            SetCurrentArea(firstArea?.Title);
            RefreshStatusBar(manager);
            _cockpitView.Refresh();

            // Render whichever module the recovered location names
            // (`TD-84`) — the shell opens where the user left it, with the
            // project they left it in, not always at Engineering.
            await RenderCurrentModuleAsync().ConfigureAwait(true);
        };

        // Graceful shutdown (`WP 10.5B` scope: "unsaved work handling,
        // clean application exit") — one real, consolidated Closing gate,
        // replacing the old two-separate-places arrangement
        // (`App.cs`'s own `ShutdownRequested` previously called
        // `SaveDesktopUiStateAsync` directly, with no unsaved-work check
        // at all). `Closing`'s own event handler is not awaited by
        // Avalonia itself, so every path synchronously sets `e.Cancel =
        // true` first, then re-closes programmatically once the real,
        // awaited work (the confirmation prompt, if needed; both state
        // saves) has actually completed — the standard async-Closing
        // pattern, never a fire-and-forget save racing the real close.
        Closing += async (_, e) =>
        {
            if (_closeConfirmed)
                return;

            e.Cancel = true;

            if (_documentArea.HasAnyDirtyTab)
            {
                var discard = await _confirmationDialog.ConfirmAsync(
                    "Unsaved changes",
                    "One or more open tabs have unsaved edits. Exiting now will discard them permanently.",
                    "Exit").ConfigureAwait(true);
                if (!discard)
                    return;
            }

            _session.WindowUiState.CaptureFrom(this);
            await _session.WindowUiState.SaveAsync().ConfigureAwait(true);
            await SaveDesktopUiStateAsync().ConfigureAwait(true);
            await _dockingComposer.Layout.SaveAsync().ConfigureAwait(true);

            // Recent/Favourite Objects (`WP 10.6A`) — saved alongside
            // every other Desktop-local persisted state above; Command
            // History/Undo-Redo/Background Tasks are deliberately
            // session-only and are not saved here (disclosed).
            await _session.RecentObjects.SaveAsync().ConfigureAwait(true);
            await _session.FavouriteObjects.SaveAsync().ConfigureAwait(true);

            _closeConfirmed = true;
            Close();
        };
    }

    /// <summary>
    /// Persists this Work Package's own Desktop-local panel UI state
    /// (Collapse/Auto-Hide/Output — <see cref="DesktopPanelUiState"/>) —
    /// called from <c>App.cs</c>'s own <c>ShutdownRequested</c> handler,
    /// alongside (never inside) <see cref="WorkspaceHost.ShutdownAsync"/>'s
    /// own, separate save of <see cref="IWorkspaceState"/> — two
    /// independent writes to two independent Settings keys.
    /// </summary>
    public Task SaveDesktopUiStateAsync() => _session.PanelUiState.SaveAsync();

    /// <summary>Persists the workspace arrangement (`TD-72`) — where the user put their panels, tabs, splits and floating windows.</summary>
    public Task SaveWorkspaceLayoutAsync() => _dockingComposer.Layout.SaveAsync();

    /// <summary>Restores the saved workspace arrangement, or a returning user's own migrated preferences on first run (`TD-72`).</summary>
    public Task RestoreWorkspaceLayoutAsync() => _dockingComposer.RestoreLayoutAsync();

    /// <summary>The workspace layout controller — the one owner of the arrangement (`TD-72`).</summary>
    public Docking.WorkspaceLayoutController WorkspaceLayout => _dockingComposer.Layout;

    /// <summary>The document and drawing viewer's opener (`TD-80`).</summary>
    /// <remarks>
    /// Exposed so an acceptance test can open a document exactly as the
    /// editor's Open button does, rather than reaching past the shell to
    /// construct a viewer of its own.
    /// </remarks>
    public Viewing.AttachmentViewerLauncher AttachmentViewers => _attachmentViewers;

    /// <summary>
    /// Opens one of the open project's own files in the `TD-80` viewer,
    /// resolving the object and attachment the Documents area named.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Goes through the same <see cref="AttachmentViewers"/> launcher the
    /// object editor's Open button uses — one entry point to the viewer,
    /// so a document opened from the project register behaves identically
    /// to one opened from an editor: an ordinary `TD-72` tab that splits,
    /// floats and persists, and a second document is the same call again.
    /// </para>
    /// <para>
    /// Opening never navigates. The module, the open project and the
    /// project area the user was on are all untouched, so looking at a
    /// drawing never costs the user their place.
    /// </para>
    /// </remarks>
    public async Task OpenProjectAttachmentAsync(Guid ownerId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (await _domainContext.Repository.FindAsync(ownerId, cancellationToken).ConfigureAwait(true) is not IHasAttachments owner)
            return;

        var attachments = await owner.GetAttachmentsAsync(cancellationToken).ConfigureAwait(true);
        if (attachments.FirstOrDefault(a => a.Id == attachmentId) is not { } attachment)
            return;

        await _attachmentViewers.OpenAsync(owner, attachment, Bounds.Width, Bounds.Height, cancellationToken).ConfigureAwait(true);
        _projectWorkspace.MarkDocumentOpened(attachmentId);
    }

    /// <summary>
    /// Tells the user when startup rehydration could not bring everything
    /// back, and exactly what was missed.
    /// </summary>
    /// <remarks>
    /// Silence here would be the worst outcome available: the workspace
    /// would simply look emptier than the user left it, which is
    /// indistinguishable from having lost the work. The message names the
    /// unrecoverable Kinds, because "some objects" is not something anyone
    /// can act on.
    /// </remarks>
    private void ReportIncompleteRehydration(EngineeringRehydrationResult? result)
    {
        if (result is null || result.IsComplete)
            return;

        var parts = new List<string>();

        if (result.UnknownKinds.Count > 0)
            parts.Add($"{result.UnknownKinds.Count} unrecognised kind(s): {string.Join(", ", result.UnknownKinds.Distinct().Order(StringComparer.Ordinal))}");

        if (result.OrphanedStateIds.Count > 0)
            parts.Add($"{result.OrphanedStateIds.Count} object(s) with no backing document");

        if (result.FailedObjectIds.Count > 0)
            parts.Add($"{result.FailedObjectIds.Count} object(s) that could not be reconstructed");

        var message = $"Some saved engineering work could not be reopened — {string.Join("; ", parts)}. It is still on disk; see the Output panel.";

        // The rehydration service has already logged each unrecoverable
        // object at Error level with the Kind and the fix; this is the
        // half a user actually sees.
        _toastHost.Show(message, FeedbackSeverity.Error, TimeSpan.FromSeconds(20));
    }

    /// <summary>Opens an engineering object in the document area, as the Explorer's own activation does.</summary>
    /// <remarks>
    /// Exposed for the same reason as <see cref="AttachmentViewers"/>: so
    /// an acceptance test can reach an object's real editor and press its
    /// own Open button, proving the wiring from that button through to the
    /// viewer — rather than calling the launcher directly and leaving the
    /// button itself untested.
    /// </remarks>
    public Task NavigateToObjectAsync(Guid id, string kind) => _viewCoordinator.NavigateToObjectAsync(id, kind);

    /// <summary>
    /// Professional Error Handling (`WP 10.5B` scope: "unexpected
    /// exceptions") — shows a real <see cref="MessageDialog"/> for a
    /// genuinely unexpected exception (never a foreseen
    /// <see cref="Tempest.Core.Commands.CommandResult.Failure"/>, which
    /// already surfaces via the Status Bar/Toast, unchanged), with the
    /// exception's own message and type name as the collapsible
    /// "Details" section — real diagnostics, not a bare crash dialog.
    /// Called from <c>App.cs</c>'s own <see cref="TaskScheduler.UnobservedTaskException"/>
    /// handler — the one unhandled-exception surface that reaches this
    /// point without the process already unwinding/terminating, so a
    /// dialog genuinely has time to render (disclosed scope: this is not
    /// a global crash handler — an `AppDomain.UnhandledException` is, by
    /// definition, already fatal by the time it fires, too late for an
    /// interactive dialog to help; `WP10.5B UX Review.md` §3).
    /// </summary>
    public Task ShowUnexpectedErrorAsync(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return _messageDialog.ShowAsync(
            FeedbackSeverity.Error,
            "Unexpected Error",
            "Something went wrong that TempestOS did not anticipate. The application should remain usable — if something looks wrong, save your work and restart.",
            $"{exception.GetType().FullName}: {exception.Message}");
    }

    /// <summary>Refreshes every Status Bar segment from real, current sources — the Selected Object segment from <c>WorkspaceManager.StatusBar</c> (unchanged since `WP 10.0B`), Host State/Diagnostics from a fresh <see cref="IDiagnosticsProvider"/> read (`WP 10.2A`), the Output panel from the identical read (`WP 10.2B`).</summary>
    private void RefreshStatusBar(WorkspaceManager manager)
    {
        _statusBar.SetText(manager.StatusBar.StatusText);
        _statusBar.SetDiagnostics(_diagnostics);
        _dockingComposer.OutputView.Refresh(_diagnostics);
        RefreshOutputPanelExtras();
    }

    /// <summary>Refreshes the Output panel's own Background Tasks/Command History sections (`WP 10.6A`) from their own real, current state.</summary>
    /// <summary>
    /// Renders whichever module the navigator currently reports (`TD-84`).
    /// </summary>
    /// <remarks>
    /// The shell has exactly one place that decides what is on screen, and
    /// it derives that from <see cref="IShellNavigator.Current"/> — so the
    /// rendered surface can never disagree with the navigation state, and a
    /// test can assert the surface by setting the location.
    /// </remarks>
    public async Task RenderCurrentModuleAsync()
    {
        var location = _navigator.Current;

        switch (location.Area)
        {
            case ShellArea.Projects:
                await _projectBrowser.RefreshAsync().ConfigureAwait(true);
                _moduleHost.Content = _projectBrowser;
                break;

            case ShellArea.ProjectWorkspace:
                await _projectWorkspace.RefreshAsync().ConfigureAwait(true);
                _moduleHost.Content = _projectWorkspace;
                break;

            case ShellArea.Home:
            case ShellArea.Engineering:
                // Both render the engineering surface today: the Cockpit is
                // a panel within it. Engineering carries its own scope —
                // the open project, or standalone (`TD-89`) — which the
                // surface reads from the navigator rather than from here.
                _moduleHost.Content = _engineeringSurface;
                break;

            default:
                // A module the product declares but has not built. It gets a
                // real, honest surface naming what is missing and what
                // tracks it — never a dead button, and never a fake screen.
                _moduleHost.Content = new DeclaredCapabilityView(
                    ShellAreas.For(location.Area), _projectContext.Current?.Label);
                break;
        }

        _navigationRail.RefreshSelection();
        RefreshProjectStatus();

        if (location.Area == ShellArea.Engineering)
            await RefreshEngineeringScopeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Shows the current project in the Status Bar (`TD-84`) — the
    /// "see the current project everywhere appropriate" requirement, met
    /// from the one real context rather than a caption a view sets on
    /// itself. Before the spine this segment read "No project" permanently,
    /// because nothing could ever set it.
    /// </summary>
    private void RefreshProjectStatus()
    {
        _statusBar.SetProject(_projectContext.Current?.Label);
        _statusBar.SetLocation(DescribeLocation(_navigator.Current));
    }

    /// <summary>
    /// Reports the Engineering Workspace's own current scope and how many
    /// engineering objects are actually in it (`TD-89`).
    /// </summary>
    /// <remarks>
    /// A real read of the real object graph through
    /// <see cref="IEngineeringScope"/>, not a caption: it is what makes
    /// "this Engineering session is scoped to Apollo, and Apollo contains
    /// eleven objects" a checkable statement rather than a claim.
    /// </remarks>
    public async Task RefreshEngineeringScopeAsync()
    {
        if (_navigator.Current.Area != ShellArea.Engineering)
            return;

        var scope = _engineeringScope.Current;
        var objects = await _engineeringScope.ListObjectsAsync().ConfigureAwait(true);

        _statusBar.SetLocation($"{DescribeLocation(_navigator.Current)} · {scope.Label} · {objects.Count} object(s)");
    }

    /// <summary>
    /// A one-line answer to "where am I", derived from the navigation
    /// state (`TD-89`).
    /// </summary>
    /// <remarks>
    /// The product rule is that a user must always be able to tell where
    /// they are, which project they are in, and which workspace — so the
    /// Status Bar states the module, the project area when inside one, and
    /// crucially <b>which engineering scope is active</b>, because
    /// "Engineering" alone no longer says whether the work belongs to a
    /// project or is standalone.
    /// </remarks>
    internal static string DescribeLocation(ShellLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return location.Area switch
        {
            ShellArea.ProjectWorkspace when location.ProjectArea is { } area =>
                $"{ShellAreas.For(location.Area).Title} · {ProjectAreas.For(area).Title}",
            ShellArea.Engineering =>
                location.IsStandaloneEngineering ? "Engineering · Standalone" : "Engineering · Project",
            _ => ShellAreas.For(location.Area).Title,
        };
    }

    /// <summary>Collects an identifier and name for a new project, creating it on confirmation. Returns whether a project was created.</summary>
    private async Task<bool> PromptForNewProjectAsync(string suggestedIdentifier, string _)
    {
        var name = await _inputDialog.PromptAsync(
            "New Project",
            $"Name for {suggestedIdentifier}:",
            validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);

        if (name is null)
            return false;

        try
        {
            var created = await _projectDirectory.CreateAsync(suggestedIdentifier, name).ConfigureAwait(true);
            _toastHost.Show($"Created {created.Label}.", FeedbackSeverity.Success);
            RecordHistory($"Created project {created.Label}.");
            return true;
        }
        catch (DuplicateProjectIdentifierException ex)
        {
            _toastHost.Show(ex.Message, FeedbackSeverity.Error);
            return false;
        }
    }

    private void RefreshOutputPanelExtras()
    {
        _dockingComposer.OutputView.RefreshBackgroundTasks(_backgroundTaskRunner);
        _dockingComposer.OutputView.RefreshHistory(_commandHistory);
    }

    /// <summary>
    /// Records <paramref name="message"/> into the Command History
    /// (`WP 10.6A`). <c>succeeded</c> is a disclosed heuristic — every
    /// existing <c>ActionCompleted</c> surface carries only a
    /// human-readable string, not a <see cref="Tempest.Core.Commands.CommandResult"/>
    /// (changing every one of those event signatures was judged too large
    /// a change for this Work Package) — inferred here from whether the
    /// message contains "fail", mirroring <c>EngineeringCockpit</c>'s own
    /// established "disclosed heuristic" precedent
    /// (<c>IsOutOfDate</c>/<c>HasMissingEvidence</c>).
    /// </summary>
    private void RecordHistory(string message)
    {
        _commandHistory.Record(message, succeeded: !message.Contains("fail", StringComparison.OrdinalIgnoreCase));
        RefreshOutputPanelExtras();
    }

    /// <summary>
    /// Sets the Status Bar's own "Active Workspace" segment (`WP 10.2A`)
    /// and the Ribbon's own current tab (`WP 10.3B`) — called from every
    /// place this window switches the current Navigation area, the one
    /// consolidation point "Context-sensitive ribbon tabs" needs, so every
    /// area-switch path (Ribbon tab click, Engineering Cockpit's own area
    /// cards, the default first-area selection on startup) keeps both in
    /// sync without each needing its own separate call.
    /// </summary>
    private void SetCurrentArea(string? title)
    {
        _currentAreaTitle = title;
        _statusBar.SetArea(_currentAreaTitle);
        _ribbon.SelectTabForArea(title);
    }

}
