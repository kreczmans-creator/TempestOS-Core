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

    // WP 10.6A — Command Execution & Productivity Experience.
    private readonly CommandHistoryLog _commandHistory = new();
    private readonly IBackgroundTaskRunner _backgroundTaskRunner = new BackgroundTaskRunner();
    private readonly KeyboardCommandBindingProvider _keyboardBindingProvider = new();
    private readonly MacroManagerDialog _macroManagerDialog;

    private readonly IEngineeringScope _engineeringScope;

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
        _session = new DesktopSessionState(composition.SettingsProvider);
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
                var result = await _backgroundTaskRunner.RunAsync(
                    $"Running macro '{title}'…",
                    ct => composition.CommandRegistry.InvokeAsync(IMacroManager.CommandIdPrefix + macroId, ct)).ConfigureAwait(true);

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
        // `() => _cockpitView!.Refresh()` is the same field-closure
        // lazy-capture pattern `_documentArea!` already uses just below;
        // `_cockpitView` is a `readonly` field assigned later, at line
        // ~209, but this lambda is only ever invoked after construction
        // fully completes, by which point it is always assigned.
        _undoRedo = new UndoRedoCoordinator(_statusBar, _toastHost, _explorerView, RecordHistory, refreshCockpit: () => _cockpitView!.Refresh());

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
            refreshStatusBar: () => RefreshStatusBar(manager), recordHistory: RecordHistory, refreshCockpit: () => _cockpitView!.Refresh());

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

        // Ribbon object-action handlers (`ADR-0103` collaborator #6, the
        // ~450-line, ~29%-of-file per-discipline Create/Duplicate/
        // status-transition population). Its own constructor populates
        // `_ribbon.ObjectCreationHandlers` directly.
        _ = new RibbonObjectActionHandlers(_ribbon, workspace, composition.CommandDispatcher, _statusBar, _toastHost, _explorerView, _cockpitView, _confirmationDialog, _inputDialog);

        _ribbon.ActionCompleted += async (message, outcome) =>
        {
            _statusBar.SetText(message);
            _toastHost.Show(message, outcome.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            RecordHistory(message);

            // Refused/failed actions changed nothing — a full Explorer
            // reload and Cockpit rebuild for them was `TD-58`'s core
            // redundant-rebuild path.
            if (outcome.WorkspaceChanged)
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _cockpitView.Refresh();
            }
        };
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

        var engineeringStack = new DockPanel();
        DockPanel.SetDock(topStack, Dock.Top);
        engineeringStack.Children.Add(topStack);
        engineeringStack.Children.Add(_dockingComposer.View);
        _engineeringSurface = engineeringStack;

        _projectDirectory = host.ProjectDirectory!;
        _projectBrowser = new ProjectBrowserView(_projectDirectory, _navigator, PromptForNewProjectAsync);
        _projectWorkspace = new ProjectWorkspaceView(_projectContext, host.ProjectDirectory!, _navigator);
        _navigationRail = new GlobalNavigationRail(_navigator);

        _navigationRail.NavigationRequested += () => _ = RenderCurrentModuleAsync();
        _projectBrowser.ProjectOpened += () => _ = RenderCurrentModuleAsync();
        _projectWorkspace.EngineeringRequested += () => _ = RenderCurrentModuleAsync();
        _projectWorkspace.ProjectClosed += () => _ = RenderCurrentModuleAsync();

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

        _commandPalette.InvokeOverride = async descriptor =>
        {
            if (!descriptor.Id.StartsWith(IMacroManager.CommandIdPrefix, StringComparison.Ordinal))
                return await composition.CommandRegistry.InvokeAsync(descriptor.Id).ConfigureAwait(true);

            // Macro invocation (`WP 10.6A`) routes through the
            // Background Task Runner — the one real "could take a
            // moment" case in this platform.
            return await _backgroundTaskRunner.RunAsync($"Running macro '{descriptor.DisplayName}'…", ct => composition.CommandRegistry.InvokeAsync(descriptor.Id, ct)).ConfigureAwait(true);
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
        _commandPalette.CommandUnavailable += descriptor =>
        {
            var message = $"'{descriptor.DisplayName}' needs a selected object or additional input — try the Ribbon or Project Explorer's own context menu.";
            _statusBar.SetText(message);
            _toastHost.Show(message, FeedbackSeverity.Warning);
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
