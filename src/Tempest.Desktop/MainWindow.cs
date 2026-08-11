using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Input;
using Tempest.Core.Macros;
using Tempest.Core.Requirements;
using Tempest.Core.Settings;
using Tempest.Core.Verification;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Editors;
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
public sealed class MainWindow : Window
{
    private readonly WorkspaceHost _host;
    private readonly ThemeService _theme;
    private readonly ProjectExplorerView _explorerView;
    private readonly PropertyInspectorView _inspectorView;
    private readonly DocumentAreaView _documentArea;
    private readonly StatusBarView _statusBar;
    private readonly CommandPaletteOverlay _commandPalette;
    private readonly DockingGrid _docking;
    private readonly CockpitView _cockpitView;
    private readonly IDiagnosticsProvider _diagnostics;
    private readonly DesktopPanelUiState _uiState;
    private readonly OutputPanel _outputPanel;
    private readonly OutputPanelView _outputView;
    private readonly RibbonView _ribbon;
    private readonly Dictionary<Guid, IWorkspaceView> _openGraphViewsByRootId = new();
    private readonly ToastHost _toastHost = new();
    private readonly BusyOverlay _busyOverlay = new();
    private readonly ConfirmationDialog _confirmationDialog = new();
    private readonly InputDialog _inputDialog = new();
    private readonly MessageDialog _messageDialog = new();
    private readonly WindowUiState _windowUiState;
    private readonly UserSettings _userSettings;
    private readonly SettingsDialog _settingsDialog;

    // WP 10.6A — Command Execution & Productivity Experience.
    private readonly IUndoRedoStack _undoRedoStack = new UndoRedoStack();
    private readonly RecentObjectsState _recentObjects;
    private readonly FavouriteObjectsState _favouriteObjects;
    private readonly CommandHistoryLog _commandHistory = new();
    private readonly IBackgroundTaskRunner _backgroundTaskRunner = new BackgroundTaskRunner();
    private readonly KeyboardCommandBindingProvider _keyboardBindingProvider = new();
    private readonly MacroManagerDialog _macroManagerDialog;
    private readonly Button _undoButton = new() { Content = "↶ Undo", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _redoButton = new() { Content = "↷ Redo", MinHeight = DesignTokens.MinControlSize };

    private string? _currentAreaTitle;
    private WorkspaceDockPosition? _openFlyoutSlot;
    private bool _closeConfirmed;

    /// <summary>Initialises a new instance of the <see cref="MainWindow"/> class over an already-started <see cref="WorkspaceHost"/>.</summary>
    public MainWindow(WorkspaceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;

        var workspace = host.Workspace ?? throw new InvalidOperationException("WorkspaceHost must be started before constructing MainWindow.");
        var manager = host.Manager!;
        var services = host.Services!;
        var settingsProvider = (ISettingsProvider)services.GetService(typeof(ISettingsProvider));
        var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));
        var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        // WP 10.7A — Feature Completion: the Object Editor's own real
        // Requirements Owner/Priority section needs this directly — the
        // data (Owner/Priority) lives only in IRequirementsService's own
        // Requirement DTO, never on the EngineeringDomainContext.Repository
        // object graph itself.
        var requirementsService = (IRequirementsService)services.GetService(typeof(IRequirementsService));
        _diagnostics = (IDiagnosticsProvider)services.GetService(typeof(IDiagnosticsProvider));
        var eventBus = (Tempest.Core.Events.IEventBus)services.GetService(typeof(Tempest.Core.Events.IEventBus));
        var macroManager = (IMacroManager)services.GetService(typeof(IMacroManager));
        var inputBindingRegistry = (IInputBindingRegistry)services.GetService(typeof(IInputBindingRegistry));

        Title = "TempestOS — Engineering Workspace";
        MinWidth = 960;
        MinHeight = 600;

        // Window geometry restoration (`WP 10.5B`, "remembered window
        // size/position/maximised state") — loaded synchronously here,
        // the identical established discipline `DesktopPanelUiState`
        // already uses, so the very first frame already reflects last
        // session's own geometry, never a default-then-jump.
        _windowUiState = new WindowUiState(settingsProvider);
        _windowUiState.LoadAsync().GetAwaiter().GetResult();
        _windowUiState.ApplyTo(this);

        _userSettings = new UserSettings(settingsProvider);
        _userSettings.LoadAsync().GetAwaiter().GetResult();
        _toastHost.DefaultDuration = TimeSpan.FromSeconds(_userSettings.ToastDurationSeconds);

        // Recent/Favourite Objects (`WP 10.6A`) — loaded synchronously
        // here, the identical established discipline every other
        // persisted Desktop-local state above already uses.
        _recentObjects = new RecentObjectsState(settingsProvider);
        _recentObjects.LoadAsync().GetAwaiter().GetResult();
        _favouriteObjects = new FavouriteObjectsState(settingsProvider);
        _favouriteObjects.LoadAsync().GetAwaiter().GetResult();

        // Keyboard is just another IInputBindingProvider (`WP 10.6A`,
        // `ADR-0100`) — registered against the shared IInputBindingRegistry
        // Platform Service, the identical mechanism a future real Stream
        // Deck/MIDI/game controller provider would register against.
        inputBindingRegistry.Register(_keyboardBindingProvider);

        _macroManagerDialog = new MacroManagerDialog(
            macroManager,
            commandRegistry,
            runMacro: async macroId =>
            {
                var descriptor = commandRegistry.Items.FirstOrDefault(d => d.Id == IMacroManager.CommandIdPrefix + macroId);
                var title = descriptor?.DisplayName ?? "Macro";

                // The one real Background Task Runner consumer this
                // Work Package wires (`WP 10.6A` §4) — a macro's own
                // multi-step invocation is the one genuinely "could take
                // a moment" case in this platform today.
                var result = await _backgroundTaskRunner.RunAsync(
                    $"Running macro '{title}'…",
                    ct => commandRegistry.InvokeAsync(IMacroManager.CommandIdPrefix + macroId, ct)).ConfigureAwait(true);

                _commandHistory.Record($"Macro '{title}'", result.Succeeded);
                RefreshOutputPanelExtras();
                return result;
            });

        // The Notification Framework's own first real Desktop consumer
        // (`WP 10.5B`) — every `IPlatformNotification` this platform
        // already publishes (background tasks, sample modules, any
        // future long-running operation) now reaches a real Toast.
        eventBus.Subscribe(new PlatformNotificationToastBridge(_toastHost));

        _theme = new ThemeService(settingsProvider);
        _settingsDialog = new SettingsDialog(_theme, _userSettings);

        // The Delete Confirmation gate (`WP 10.5B`, Dialog Framework) —
        // one real implementation, wired identically into every Delete
        // path (Ribbon button, Project Explorer context menu, Delete key).
        // Honours `UserSettings.ConfirmBeforeDelete` — a user who
        // deliberately opts out via Preferences gets the pre-`WP 10.5B`
        // immediate-delete behaviour back, never forced through a prompt
        // they turned off.
        Task<bool> ConfirmDeleteAsync(string prompt) =>
            _userSettings.ConfirmBeforeDelete
                ? _confirmationDialog.ConfirmAsync("Delete?", prompt, "Delete")
                : Task.FromResult(true);

        _explorerView = new ProjectExplorerView(workspace.ProjectExplorer, manager) { ConfirmDeleteAsync = ConfirmDeleteAsync, RecentSearchCapacity = _userSettings.RecentSearchCapacity };
        _inspectorView = new PropertyInspectorView(workspace.PropertyInspector, manager, domainContext);

        // The Object Editor Framework (`WP 10.3A`) — DocumentAreaView's own
        // injectable content builder. Tries a real per-Kind editor first
        // (ObjectEditorView.TryCreate), falling back to the original
        // generic three-line body for any Kind with no real Engineering
        // Domain object behind it (a synthetic Kind, or the Sample
        // Explorer's own fixed content) — DocumentAreaView itself stays
        // completely agnostic to which.
        // Local functions below are only ever invoked as deferred event-
        // handler delegates, after this constructor has fully returned and
        // every field assigned unconditionally below has a real value — the
        // null-forgiving operator on each field access here suppresses a
        // known, harmless nullable-flow-analysis limitation (a local
        // function's own captured-field flow state is evaluated at its own
        // declaration point, not its own later invocation point), not a
        // genuine possible-null risk.
        async Task NavigateToObjectAsync(System.Guid id, string kind)
        {
            var relatedView = await workspace.Navigation.OpenAsync(id, kind).ConfigureAwait(true);
            _documentArea!.ShowTab(relatedView);
            _cockpitView!.Refresh();
        }
        void NavigateToObject(System.Guid id, string kind) => _ = NavigateToObjectAsync(id, kind);

        Control BuildDocumentContent(IWorkspaceView view)
        {
            // The Digital Thread graph (`WP 10.4A`) is itself both the
            // IWorkspaceView and its own rendered Control — unlike every
            // other document Kind, it is not built from `view` by a
            // per-Kind factory here; it already IS the content. Checked
            // first, generically, so any future self-rendering View gets
            // the identical treatment without another special case.
            if (view is Control alreadyBuilt)
                return alreadyBuilt;

            var editor = ObjectEditorView.TryCreate(view.ObjectId, view.ObjectKind, domainContext, manager, NavigateToObject, commandDispatcher, requirementsService, _host.CalculationTemplates);
            if (editor is null)
                return DocumentAreaView.BuildDefaultBody(view);

            editor.DirtyChanged += dirty => _documentArea!.MarkDirty(view.Id, dirty);
            editor.ActionCompleted += async message =>
            {
                _statusBar!.SetText(message);
                _toastHost!.Show(message, FeedbackSeverity.Success);
                RecordHistory(message);
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _inspectorView.Refresh();
                _cockpitView!.Refresh();
            };
            // Undo/Redo (`WP 10.6A`, `ADR-0099`) — every discipline's own
            // Object Editor shares this one commit path, so this single
            // subscription covers Rename across all six disciplines.
            editor.UndoableActionRecorded += action =>
            {
                _undoRedoStack.Record(action);
                RefreshUndoRedoButtons();
            };
            return editor;
        }

        _documentArea = new DocumentAreaView(BuildDocumentContent);
        _statusBar = new StatusBarView();
        _commandPalette = new CommandPaletteOverlay(commandRegistry);
        _docking = new DockingGrid();

        // Desktop-local panel UI state (Collapse/Auto-Hide/Output — `WP
        // 10.2B`) — loaded synchronously here, exactly as
        // `IWorkspaceState`'s own equivalent load already completed
        // synchronously-from-this-constructor's-perspective inside
        // `host.StartAsync()` before this constructor ever ran (`App.cs`
        // §"Avalonia's own startup path is synchronous"), so the very
        // first frame already reflects last session's own Collapse/
        // Auto-Hide/Output state — "restore previous layout on startup"
        // applied to this Work Package's own, additional, Desktop-local
        // state, the same way `ADR-0064` already applies it to the
        // Workspace's own contracted state.
        _uiState = new DesktopPanelUiState(settingsProvider);
        _uiState.LoadAsync().GetAwaiter().GetResult();

        var explorerPlacement = workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id);
        var inspectorPlacement = workspace.Layout.GetPlacement(workspace.PropertyInspector.Id);

        var explorerHost = new PanelHostControl(workspace.ProjectExplorer, _explorerView);
        var inspectorHost = new PanelHostControl(workspace.PropertyInspector, _inspectorView);
        explorerHost.SetCollapsed(_uiState.ExplorerCollapsed);
        explorerHost.SetPinned(_uiState.ExplorerPinned);
        inspectorHost.SetCollapsed(_uiState.InspectorCollapsed);
        inspectorHost.SetPinned(_uiState.InspectorPinned);

        _outputPanel = new OutputPanel();
        _outputView = new OutputPanelView();
        var outputHost = new PanelHostControl(_outputPanel, _outputView);
        outputHost.SetCollapsed(_uiState.OutputCollapsed);
        outputHost.SetPinned(_uiState.OutputPinned);
        if (_uiState.OutputVisible)
            _outputPanel.ShowAsync().GetAwaiter().GetResult();

        _docking.SetLeftPanel(explorerHost, explorerPlacement.Size == 0 ? 240 : explorerPlacement.Size * 8, explorerPlacement.IsVisible);
        _docking.SetRightPanel(inspectorHost, inspectorPlacement.Size == 0 ? 240 : inspectorPlacement.Size * 8, inspectorPlacement.IsVisible);
        _docking.SetBottomPanel(outputHost, _uiState.OutputHeight, _uiState.OutputVisible);
        _docking.SetCenterContent(_documentArea);
        _docking.SetLeftCollapsed(explorerHost.IsStripShowing);
        _docking.SetRightCollapsed(inspectorHost.IsStripShowing);
        _docking.SetBottomCollapsed(outputHost.IsStripShowing);

        // Resizing: persist the new width back into the real IWorkspaceLayout
        // (WorkspacePanelPlacement is an immutable record — "with" produces
        // the updated snapshot ADR-0064's own SaveAsync later serialises).
        _docking.LeftPanelResized += width =>
            workspace.Layout.SetPlacement(workspace.ProjectExplorer.Id, workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id) with { Size = width });
        _docking.RightPanelResized += width =>
            workspace.Layout.SetPlacement(workspace.PropertyInspector.Id, workspace.Layout.GetPlacement(workspace.PropertyInspector.Id) with { Size = width });
        _docking.BottomPanelResized += height => _uiState.OutputHeight = height;

        explorerHost.HideRequested += () =>
        {
            _docking.SetLeftVisible(false);
            workspace.Layout.SetPlacement(workspace.ProjectExplorer.Id, workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id) with { IsVisible = false });
        };
        inspectorHost.HideRequested += () =>
        {
            _docking.SetRightVisible(false);
            workspace.Layout.SetPlacement(workspace.PropertyInspector.Id, workspace.Layout.GetPlacement(workspace.PropertyInspector.Id) with { IsVisible = false });
        };
        outputHost.HideRequested += () =>
        {
            _docking.SetBottomVisible(false);
            _uiState.OutputVisible = false;
        };

        // Collapse (`WP 10.2B`) — a manual, in-place shrink; Desktop-local
        // state only (`DesktopPanelUiState`, not `IWorkspaceLayout`).
        explorerHost.CollapseToggled += collapsed =>
        {
            _uiState.ExplorerCollapsed = collapsed;
            _docking.SetLeftCollapsed(explorerHost.IsStripShowing);
        };
        inspectorHost.CollapseToggled += collapsed =>
        {
            _uiState.InspectorCollapsed = collapsed;
            _docking.SetRightCollapsed(inspectorHost.IsStripShowing);
        };
        outputHost.CollapseToggled += collapsed =>
        {
            _uiState.OutputCollapsed = collapsed;
            _docking.SetBottomCollapsed(outputHost.IsStripShowing);
        };

        // Auto-Hide (`WP 10.2B`) — unpinning hands the dock column/row back
        // to the Document Area, leaving only the thin edge strip; closes
        // any open flyout for this slot when re-pinned.
        explorerHost.PinToggled += pinned =>
        {
            _uiState.ExplorerPinned = pinned;
            _docking.SetLeftCollapsed(explorerHost.IsStripShowing);
            if (pinned && _openFlyoutSlot == WorkspaceDockPosition.Left)
                CloseFlyout();
        };
        inspectorHost.PinToggled += pinned =>
        {
            _uiState.InspectorPinned = pinned;
            _docking.SetRightCollapsed(inspectorHost.IsStripShowing);
            if (pinned && _openFlyoutSlot == WorkspaceDockPosition.Right)
                CloseFlyout();
        };
        outputHost.PinToggled += pinned =>
        {
            _uiState.OutputPinned = pinned;
            _docking.SetBottomCollapsed(outputHost.IsStripShowing);
            if (pinned && _openFlyoutSlot == WorkspaceDockPosition.Bottom)
                CloseFlyout();
        };

        explorerHost.FlyoutRequested += () => ToggleFlyout(WorkspaceDockPosition.Left, explorerHost, Math.Max(explorerPlacement.Size == 0 ? 240 : explorerPlacement.Size * 8, 240));
        inspectorHost.FlyoutRequested += () => ToggleFlyout(WorkspaceDockPosition.Right, inspectorHost, Math.Max(inspectorPlacement.Size == 0 ? 240 : inspectorPlacement.Size * 8, 240));
        outputHost.FlyoutRequested += () => ToggleFlyout(WorkspaceDockPosition.Bottom, outputHost, Math.Max(_uiState.OutputHeight, 160));

        // Click-away: a pointer press landing directly on the Document
        // Area (never intercepted by the flyout itself, which sits above
        // it in Z-order while open) closes any open Auto-Hide flyout —
        // the deliberate "click to peek, click away to dismiss"
        // interaction model (`WP10.2B UX Review.md` §3), chosen over
        // hover-to-peek/dwell-timer for determinism, keyboard parity
        // (`Escape`, below), and headless testability.
        _documentArea.PointerPressed += (_, _) =>
        {
            if (_docking.IsFlyoutOpen)
                CloseFlyout();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _docking.IsFlyoutOpen)
                CloseFlyout();
        };

        // The Engineering Cockpit (WP 10.1A, ADR-0069) — the Workspace's
        // own default landing screen, realised here as the Document Area's
        // own permanent Home tab. Workspace (the concrete class) is
        // internal, reached via InternalsVisibleTo("Tempest.Desktop")
        // (granted `WP 10.0B`) — the identical, precedented pattern
        // WorkspaceShell itself already uses internally to reach its own
        // concrete Workspace instance. Constructed before the event
        // wiring below, which captures it.
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
            // identical NavigateToObject local function every other
            // Cockpit/Object Editor navigation action already calls.
            favourites: _favouriteObjects,
            onOpenFavourite: NavigateToObject);
        _documentArea.SetHomeTab(_cockpitView);

        // Select-to-inspect / Open-to-edit (WP8.0A UI Architecture.md §4, unchanged).
        _explorerView.ObjectSelected += async (id, kind) =>
        {
            await workspace.Selection.SelectAsync(id, kind).ConfigureAwait(true);
            _inspectorView.SetCurrentSelection(id, kind);
            _inspectorView.Refresh();
            RefreshStatusBar(manager);
            _ribbon!.RefreshEnablement();
        };
        _explorerView.ObjectOpened += async (id, kind) =>
        {
            var view = await workspace.Navigation.OpenAsync(id, kind).ConfigureAwait(true);
            _documentArea.ShowTab(view);
            _cockpitView.Refresh();

            // "Recent objects" (`WP 10.6A`) — recorded here, the one
            // place every Open path already converges (tree double-click/
            // Enter, and this Work Package's own Recent Objects/
            // Favourites flyouts, both of which raise this identical
            // event rather than duplicating this logic).
            _recentObjects.Record(id, kind, view.Title);
        };
        _explorerView.ActionCompleted += message =>
        {
            _statusBar.SetText(message);
            _toastHost.Show(message, FeedbackSeverity.Success);
            RecordHistory(message);
            _cockpitView.Refresh();
        };
        _explorerView.RecentObjects = _recentObjects;
        _explorerView.Favourites = _favouriteObjects;
        _explorerView.ToggleFavouriteRequested = (id, kind, displayName) => ToggleFavourite(id, kind, displayName);

        // WP 10.7A — Feature Completion: real drag-and-drop reparenting,
        // closing the WP10.6D-audited Project Explorer Drop no-op. This
        // View only raises intent (see its own ObjectMoveRequested
        // remarks) — dispatch of the correct discipline's own already-
        // registered Move*Command happens here, the identical "owner
        // dispatches" shape ToggleFavouriteRequested already established.
        _explorerView.ObjectMoveRequested += async (id, kind, newParentId) =>
        {
            IWorkspaceCommand? move = kind switch
            {
                "WorkInstruction" => new MoveDocumentObjectCommand(id, kind, newParentId),
                "Inspection" => new MoveVerificationActivityCommand(id, kind, newParentId),
                "ManufacturingOperation" => new MoveManufacturingObjectCommand(id, kind, newParentId),
                "Calculation" or "CalculationSet" => new MoveCalculationObjectCommand(id, kind, newParentId),
                "VerificationActivity" => new MoveVerificationActivityCommand(id, kind, newParentId),
                "Requirement" => new MoveRequirementCommand(id, newParentId),
                "RequirementGroup" => new MoveRequirementGroupCommand(id, newParentId),
                _ when DocumentObjectFactoryRegistry.SupportedKinds.Contains(kind) => new MoveDocumentObjectCommand(id, kind, newParentId),
                _ when MechanicalObjectFactoryRegistry.SupportedKinds.Contains(kind) => new MoveMechanicalObjectCommand(id, kind, newParentId),
                _ => null,
            };

            if (move is null)
            {
                _statusBar.SetText($"Moving a {kind} isn't supported yet.");
                return;
            }

            var result = await commandDispatcher.DispatchAsync(move, CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? "Moved." : result.Message ?? "Move failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded)
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _cockpitView.Refresh();
            }
        };
        _inspectorView.ActionCompleted += async message =>
        {
            _statusBar.SetText(message);
            _toastHost.Show(message, FeedbackSeverity.Success);
            RecordHistory(message);
            await _explorerView.LoadAsync().ConfigureAwait(true);
            _cockpitView.Refresh();
        };
        async Task CloseDocumentAsync(System.Guid viewId)
        {
            // Closes `TD-40` (`WP 10.5A`): a dirty Object Editor tab's own
            // buffered, unsaved edits are no longer silently discarded —
            // confirmed via the real `ConfirmationDialog` first. Cancelling
            // leaves the tab open, with its edits intact, exactly as if
            // Close had never been requested.
            if (_documentArea.IsMarkedDirty(viewId))
            {
                var discard = await _confirmationDialog.ConfirmAsync(
                    "Discard unsaved changes?",
                    "This tab has unsaved edits. Closing it now will discard them permanently.",
                    "Discard").ConfigureAwait(true);
                if (!discard)
                    return;
            }

            await workspace.Navigation.CloseAsync(viewId).ConfigureAwait(true);
            foreach (var rootId in _openGraphViewsByRootId.Where(kv => kv.Value.Id == viewId).Select(kv => kv.Key).ToList())
                _openGraphViewsByRootId.Remove(rootId);
            _documentArea.RemoveTab(viewId);
            _cockpitView.Refresh();
        }

        _documentArea.TabCloseRequested += viewId => _ = CloseDocumentAsync(viewId);

        var menu = BuildMenuSystem(workspace, explorerHost, inspectorHost, outputHost);
        var quickAccessToolbar = BuildQuickAccessToolbar(workspace, explorerHost, inspectorHost, outputHost, domainContext, NavigateToObject);

        // The Engineering Ribbon (`WP 10.3B`) — a real, tabbed command
        // surface over the existing ICommandRegistry, replacing the old
        // Navigation Framework's own standalone area-switch button row
        // (`WP 10.0B`) — a Ribbon tab click both switches the Navigation
        // area (via CategorySelected, below) and shows that discipline's
        // own real commands, so the two concerns need only one control,
        // not two. Never a second registration mechanism (`ADR-0070`) —
        // every button dispatches a real, already-registered command.
        _ribbon = new RibbonView(
            commandRegistry,
            manager,
            workspace,
            setHint: hint => _statusBar.SetHint(hint),
            openDocument: view => _documentArea.ShowTab(view))
        {
            ConfirmDeleteAsync = ConfirmDeleteAsync,
        };
        _ribbon.ActionCompleted += async message =>
        {
            _statusBar.SetText(message);
            _toastHost.Show(message, FeedbackSeverity.Success);
            RecordHistory(message);
            await _explorerView.LoadAsync().ConfigureAwait(true);
            _cockpitView.Refresh();
        };
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

        // A real, working "Create Object" flow (`WP 10.5B`, Dialog
        // Framework/"object creation experience") — honestly scoped to
        // Mechanical only (`CreateMechanicalObjectCommand`'s own
        // constructor shape is the simplest Ribbon-Create-friendly one of
        // the eight real Create commands across six disciplines; the
        // other seven have genuinely different constructor shapes —
        // Requirements alone has three — extending this to all of them is
        // real, disclosed future work, `WP10.5B Implementation Report.md`
        // §8/`FCR`). Defaults every new object to Kind `"Part"` — the
        // most common creation — rather than offering a Kind picker
        // `InputDialog`'s own single-field shape cannot collect.
        _ribbon.ObjectCreationHandlers["mechanical.create"] = async () =>
        {
            var name = await _inputDialog.PromptAsync(
                "Create Part",
                "Name for the new Part:",
                validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null)
                return;

            var result = await commandDispatcher.DispatchAsync(new CreateMechanicalObjectCommand("Part", name), CancellationToken.None).ConfigureAwait(true);
            _statusBar.SetText(result.Succeeded ? $"Created Part '{name}'." : result.Message ?? "Create failed.");
            _toastHost.Show(result.Succeeded ? $"Created Part '{name}'." : result.Message ?? "Create failed.", result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded)
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _cockpitView.Refresh();
            }
        };

        // A real "Duplicate workflow" (`WP 10.5B` scope) — genuinely
        // simpler than Create: `DuplicateMechanicalObjectCommand` needs
        // only the already-selected object's own Id/Kind, no additional
        // input to collect, so a plain confirmation (never an
        // `InputDialog`) is the complete, honest interaction.
        _ribbon.ObjectCreationHandlers["mechanical.duplicate"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null)
            {
                _statusBar.SetText("Select an object to duplicate first.");
                return;
            }

            var confirmed = await _confirmationDialog.ConfirmAsync(
                "Duplicate?",
                $"Create a duplicate of the selected {selection.Kind}?",
                "Duplicate").ConfigureAwait(true);
            if (!confirmed)
                return;

            var result = await commandDispatcher.DispatchAsync(new DuplicateMechanicalObjectCommand(selection.ObjectId, selection.Kind), CancellationToken.None).ConfigureAwait(true);
            _statusBar.SetText(result.Succeeded ? $"Duplicated the selected {selection.Kind}." : result.Message ?? "Duplicate failed.");
            _toastHost.Show(result.Succeeded ? $"Duplicated the selected {selection.Kind}." : result.Message ?? "Duplicate failed.", result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded)
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _cockpitView.Refresh();
            }
        };

        // WP 10.7A — Feature Completion. Closes the WP10.6D-audited gap
        // where every Ribbon lifecycle/organize button beyond Mechanical's
        // own Create/Duplicate fell through to the honest-but-permanent
        // "needs additional input this ribbon does not yet collect"
        // message, even though every command dispatched below already
        // exists and is already registered — only Mechanical had a real
        // Ribbon handler wired before this Work Package. Every handler
        // below follows the identical four-step shape the two Mechanical
        // handlers above already established: confirm/prompt if needed,
        // dispatch the already-registered command via commandDispatcher,
        // report via StatusBar+Toast, refresh Explorer+Cockpit on success.

        // A shared factory for every discipline's own Approve/Archive/
        // Lock/Unlock/Request-Review/Release button — Calculations/
        // Documents/Verification/Manufacturing's own Set{X}StatusCommand
        // all share the identical (Guid, string, LifecycleState) shape.
        Func<string, LifecycleState, Func<Guid, string, LifecycleState, IWorkspaceCommand>, Func<Task>> statusHandler = (verbLabel, status, factory) => async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null)
            {
                _statusBar.SetText($"Select an object to {verbLabel.ToLowerInvariant()} first.");
                return;
            }

            var result = await commandDispatcher.DispatchAsync(factory(selection.ObjectId, selection.Kind, status), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"'{verbLabel}' applied to the selected {selection.Kind}." : result.Message ?? $"'{verbLabel}' failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded)
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _cockpitView.Refresh();
            }
        };

        _ribbon.ObjectCreationHandlers["calculations.lock"] = statusHandler("Lock", LifecycleState.Approved, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));
        _ribbon.ObjectCreationHandlers["calculations.unlock"] = statusHandler("Unlock", LifecycleState.Draft, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));
        _ribbon.ObjectCreationHandlers["calculations.request-review"] = statusHandler("Request Review", LifecycleState.InReview, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));
        _ribbon.ObjectCreationHandlers["calculations.approve"] = statusHandler("Approve", LifecycleState.Approved, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));
        _ribbon.ObjectCreationHandlers["calculations.archive"] = statusHandler("Archive", LifecycleState.Archived, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));

        _ribbon.ObjectCreationHandlers["documents.request-review"] = statusHandler("Request Review", LifecycleState.InReview, static (id, kind, status) => new SetDocumentStatusCommand(id, kind, status));
        _ribbon.ObjectCreationHandlers["documents.approve"] = statusHandler("Approve", LifecycleState.Approved, static (id, kind, status) => new SetDocumentStatusCommand(id, kind, status));
        _ribbon.ObjectCreationHandlers["documents.release"] = statusHandler("Release", LifecycleState.Released, static (id, kind, status) => new SetDocumentStatusCommand(id, kind, status));

        _ribbon.ObjectCreationHandlers["verification.request-review"] = statusHandler("Request Review", LifecycleState.InReview, static (id, kind, status) => new SetVerificationActivityStatusCommand(id, kind, status));
        _ribbon.ObjectCreationHandlers["verification.approve"] = statusHandler("Approve", LifecycleState.Approved, static (id, kind, status) => new SetVerificationActivityStatusCommand(id, kind, status));
        _ribbon.ObjectCreationHandlers["verification.archive"] = statusHandler("Archive", LifecycleState.Archived, static (id, kind, status) => new SetVerificationActivityStatusCommand(id, kind, status));

        _ribbon.ObjectCreationHandlers["manufacturing.release"] = statusHandler("Release", LifecycleState.Released, static (id, kind, status) => new SetManufacturingObjectStatusCommand(id, kind, status));
        _ribbon.ObjectCreationHandlers["manufacturing.archive"] = statusHandler("Archive", LifecycleState.Archived, static (id, kind, status) => new SetManufacturingObjectStatusCommand(id, kind, status));

        // Requirements' own SetRequirementStatusCommand has a genuinely
        // different shape (RequirementStatus, not LifecycleState; no
        // targetKind parameter) — its own dedicated handler, not the
        // shared factory above. No status picker control exists, so a
        // validated free-text prompt (mirrors Create's own length-
        // validated prompt) is the honest minimum interaction.
        _ribbon.ObjectCreationHandlers["requirements.set-status"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { _statusBar.SetText("Select a Requirement to set status on first."); return; }

            var validStatuses = string.Join(", ", Enum.GetNames<RequirementStatus>());
            var statusText = await _inputDialog.PromptAsync(
                "Set Requirement Status",
                $"New status ({validStatuses}):",
                validate: value => Enum.TryParse<RequirementStatus>(value, ignoreCase: true, out _) ? null : $"Must be one of: {validStatuses}.").ConfigureAwait(true);
            if (statusText is null) return;

            var status = Enum.Parse<RequirementStatus>(statusText, ignoreCase: true);
            var result = await commandDispatcher.DispatchAsync(new SetRequirementStatusCommand(selection.ObjectId, status), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Requirement status set to {status}." : result.Message ?? "Set status failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        _ribbon.ObjectCreationHandlers["requirements.set-owner"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { _statusBar.SetText("Select a Requirement to set an owner on first."); return; }

            var owner = await _inputDialog.PromptAsync("Set Requirement Owner", "Owner:").ConfigureAwait(true);
            if (owner is null) return;

            var result = await commandDispatcher.DispatchAsync(new SetRequirementOwnerCommand(selection.ObjectId, owner), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Owner set to '{owner}'." : result.Message ?? "Set owner failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        _ribbon.ObjectCreationHandlers["requirements.set-priority"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { _statusBar.SetText("Select a Requirement to set a priority on first."); return; }

            var validPriorities = string.Join(", ", Enum.GetNames<RequirementPriority>());
            var priorityText = await _inputDialog.PromptAsync(
                "Set Requirement Priority",
                $"Priority ({validPriorities}):",
                validate: value => Enum.TryParse<RequirementPriority>(value, ignoreCase: true, out _) ? null : $"Must be one of: {validPriorities}.").ConfigureAwait(true);
            if (priorityText is null) return;

            var priority = Enum.Parse<RequirementPriority>(priorityText, ignoreCase: true);
            var result = await commandDispatcher.DispatchAsync(new SetRequirementPriorityCommand(selection.ObjectId, priority), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Priority set to {priority}." : result.Message ?? "Set priority failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        // A shared factory for every discipline's own Duplicate button —
        // mirrors "mechanical.duplicate" above exactly; Calculations/
        // Documents/Verification/Manufacturing's own Duplicate{X}Command
        // all need only the selected object's own Id/Kind (an optional
        // newIdentifier parameter on Calculations/Documents, left null,
        // exactly like Mechanical's own).
        Func<Func<Guid, string, IWorkspaceCommand>, Func<Task>> duplicateHandler = factory => async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { _statusBar.SetText("Select an object to duplicate first."); return; }

            var confirmed = await _confirmationDialog.ConfirmAsync("Duplicate?", $"Create a duplicate of the selected {selection.Kind}?", "Duplicate").ConfigureAwait(true);
            if (!confirmed) return;

            var result = await commandDispatcher.DispatchAsync(factory(selection.ObjectId, selection.Kind), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Duplicated the selected {selection.Kind}." : result.Message ?? "Duplicate failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        _ribbon.ObjectCreationHandlers["calculations.duplicate"] = duplicateHandler(static (id, kind) => new DuplicateCalculationObjectCommand(id, kind));
        _ribbon.ObjectCreationHandlers["documents.duplicate"] = duplicateHandler(static (id, kind) => new DuplicateDocumentObjectCommand(id, kind));
        _ribbon.ObjectCreationHandlers["verification.duplicate"] = duplicateHandler(static (id, kind) => new DuplicateVerificationActivityCommand(id, kind));
        _ribbon.ObjectCreationHandlers["manufacturing.duplicate"] = duplicateHandler(static (id, kind) => new DuplicateManufacturingObjectCommand(id, kind));

        // Requirements' own DuplicateRequirementCommand requires a new
        // identifier (not optional, unlike every other discipline's own
        // Duplicate command) — its own dedicated handler.
        _ribbon.ObjectCreationHandlers["requirements.duplicate"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { _statusBar.SetText("Select a Requirement to duplicate first."); return; }

            var newIdentifier = await _inputDialog.PromptAsync(
                "Duplicate Requirement",
                "New identifier for the duplicate:",
                validate: value => string.IsNullOrWhiteSpace(value) ? "An identifier is required." : null).ConfigureAwait(true);
            if (newIdentifier is null) return;

            var result = await commandDispatcher.DispatchAsync(new DuplicateRequirementCommand(selection.ObjectId, newIdentifier), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Duplicated as '{newIdentifier}'." : result.Message ?? "Duplicate failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        // Create — the four disciplines with one Create{X}ObjectCommand
        // needing only a name default every other optional constructor
        // parameter, mirroring Mechanical's own "defaults to Kind Part"
        // minimal-viable precedent.
        _ribbon.ObjectCreationHandlers["calculations.create"] = async () =>
        {
            var name = await _inputDialog.PromptAsync("Create Calculation", "Name for the new Calculation:", validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateCalculationObjectCommand("Calculation", name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Calculation '{name}'." : result.Message ?? "Create failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        _ribbon.ObjectCreationHandlers["documents.create"] = async () =>
        {
            var name = await _inputDialog.PromptAsync("Create Document", "Name for the new Document:", validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Document '{name}'." : result.Message ?? "Create failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        _ribbon.ObjectCreationHandlers["manufacturing.create"] = async () =>
        {
            var name = await _inputDialog.PromptAsync("Create Manufacturing Operation", "Name for the new Manufacturing Operation:", validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateManufacturingObjectCommand("ManufacturingOperation", name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Manufacturing Operation '{name}'." : result.Message ?? "Create failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        // Verification Create genuinely means "verify the object I have
        // selected" — SubjectId is the current selection's own Id, not a
        // fabricated/default one; Method has no picker anywhere in this
        // platform, defaulted to a fixed, honest "Inspection" (the same
        // word this platform's own Manufacturing "Inspection" Kind
        // already uses for the identical concept).
        _ribbon.ObjectCreationHandlers["verification.create"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { _statusBar.SetText("Select the object to verify first."); return; }

            var name = await _inputDialog.PromptAsync(
                "Create Verification Activity",
                $"Name for the new Verification Activity (verifying the selected {selection.Kind}):",
                validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateVerificationActivityCommand(name, selection.ObjectId, "Inspection"), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Verification Activity '{name}'." : result.Message ?? "Create failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        // Manufacturing's own "Record Inspection Result" (`WP 10.8A`) —
        // disclosed cross-Work-Package reuse, exactly as
        // ManufacturingWorkspaceRegistration's own remarks already
        // document: dispatches Verification.RecordVerificationResultCommand
        // directly, the identical command/handler the Object Editor's own
        // Verification Record Result section (`WP 10.7A`) already uses —
        // no duplicate command, no duplicate handler. No Outcome-picker
        // control exists at the Ribbon level, so a validated InputDialog
        // prompt (mirroring "requirements.set-status"'s own identical
        // shape) is the honest minimum interaction.
        _ribbon.ObjectCreationHandlers["manufacturing.record-inspection-result"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { _statusBar.SetText("Select an Inspection to record a result for first."); return; }

            var validOutcomes = string.Join(", ", Enum.GetNames<VerificationOutcome>());
            var outcomeText = await _inputDialog.PromptAsync(
                "Record Inspection Result",
                $"Outcome ({validOutcomes}):",
                validate: value => Enum.TryParse<VerificationOutcome>(value, ignoreCase: true, out _) ? null : $"Must be one of: {validOutcomes}.").ConfigureAwait(true);
            if (outcomeText is null) return;

            var method = await _inputDialog.PromptAsync("Record Inspection Result", "Method:", initialValue: "Inspection").ConfigureAwait(true);
            if (method is null) return;

            var outcome = Enum.Parse<VerificationOutcome>(outcomeText, ignoreCase: true);
            var result = await commandDispatcher.DispatchAsync(new RecordVerificationResultCommand(selection.ObjectId, selection.Kind, outcome, method), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Result recorded: {outcome}." : result.Message ?? "Record result failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        // Requirements Create — three distinct commands/descriptors
        // ("requirements.create"/"requirements.create-group"/
        // "requirements.create-collection"), each genuinely different.
        // CreateRequirementCommand needs both an identifier and a
        // statement — two sequential prompts, still InputDialog, no new
        // dialog type.
        _ribbon.ObjectCreationHandlers["requirements.create"] = async () =>
        {
            var identifier = await _inputDialog.PromptAsync("Create Requirement", "Identifier (e.g. REQ-001):", validate: value => string.IsNullOrWhiteSpace(value) ? "An identifier is required." : null).ConfigureAwait(true);
            if (identifier is null) return;

            var statement = await _inputDialog.PromptAsync("Create Requirement", "Statement:", validate: value => string.IsNullOrWhiteSpace(value) ? "A statement is required." : null).ConfigureAwait(true);
            if (statement is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateRequirementCommand(identifier, statement), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Requirement '{identifier}'." : result.Message ?? "Create failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        _ribbon.ObjectCreationHandlers["requirements.create-group"] = async () =>
        {
            var name = await _inputDialog.PromptAsync("Create Requirement Group", "Name for the new group:", validate: value => string.IsNullOrWhiteSpace(value) ? "A name is required." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateRequirementGroupCommand(name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Requirement Group '{name}'." : result.Message ?? "Create failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        _ribbon.ObjectCreationHandlers["requirements.create-collection"] = async () =>
        {
            var name = await _inputDialog.PromptAsync("Create Requirement Collection", "Name for the new collection:", validate: value => string.IsNullOrWhiteSpace(value) ? "A name is required." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateRequirementCollectionCommand(name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Requirement Collection '{name}'." : result.Message ?? "Create failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await _explorerView.LoadAsync().ConfigureAwait(true); _cockpitView.Refresh(); }
        };

        var topStack = new StackPanel();
        topStack.Children.Add(menu);
        topStack.Children.Add(quickAccessToolbar);
        topStack.Children.Add(_ribbon);
        topStack.Children.Add(new Separator());

        var dock = new DockPanel();
        DockPanel.SetDock(topStack, Dock.Top);
        DockPanel.SetDock(_statusBar, Dock.Bottom);
        dock.Children.Add(topStack);
        dock.Children.Add(_statusBar);
        dock.Children.Add(_docking);

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
                    _ = CloseDocumentAsync(viewId);
            },
            focusExplorerFilter: () => _explorerView.FocusFilter(),
            undo: () => _ = UndoAsync(),
            redo: () => _ = RedoAsync(),
            toggleFavourite: () =>
            {
                if (workspace.Selection.Current is { } selection)
                {
                    var target = domainContext.Repository.FindAsync(selection.ObjectId).GetAwaiter().GetResult();
                    var title = (target as IHasBusinessIdentifier)?.DisplayName ?? selection.Kind;
                    ToggleFavourite(selection.ObjectId, selection.Kind, title);
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
                return await commandRegistry.InvokeAsync(descriptor.Id).ConfigureAwait(true);

            // Macro invocation (`WP 10.6A`) routes through the
            // Background Task Runner — the one real "could take a
            // moment" case in this platform.
            return await _backgroundTaskRunner.RunAsync($"Running macro '{descriptor.DisplayName}'…", ct => commandRegistry.InvokeAsync(descriptor.Id, ct)).ConfigureAwait(true);
        };
        _commandPalette.CommandInvoked += descriptor =>
        {
            RecordHistory($"Invoked '{descriptor.DisplayName}' via Command Palette.");
            RefreshStatusBar(manager);
            _cockpitView.Refresh();
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

            _windowUiState.CaptureFrom(this);
            await _windowUiState.SaveAsync().ConfigureAwait(true);
            await SaveDesktopUiStateAsync().ConfigureAwait(true);

            // Recent/Favourite Objects (`WP 10.6A`) — saved alongside
            // every other Desktop-local persisted state above; Command
            // History/Undo-Redo/Background Tasks are deliberately
            // session-only and are not saved here (disclosed).
            await _recentObjects.SaveAsync().ConfigureAwait(true);
            await _favouriteObjects.SaveAsync().ConfigureAwait(true);

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
    public Task SaveDesktopUiStateAsync() => _uiState.SaveAsync();

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
        _outputView.Refresh(_diagnostics);
        RefreshOutputPanelExtras();
    }

    /// <summary>Refreshes the Output panel's own Background Tasks/Command History sections (`WP 10.6A`) from their own real, current state.</summary>
    private void RefreshOutputPanelExtras()
    {
        _outputView.RefreshBackgroundTasks(_backgroundTaskRunner);
        _outputView.RefreshHistory(_commandHistory);
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

    /// <summary>Refreshes the Undo/Redo Quick Access Toolbar buttons' own enablement/tooltip from <see cref="_undoRedoStack"/>'s own real, current state (`WP 10.6A`).</summary>
    private void RefreshUndoRedoButtons()
    {
        _undoButton.IsEnabled = _undoRedoStack.CanUndo;
        _redoButton.IsEnabled = _undoRedoStack.CanRedo;
        ToolTip.SetTip(_undoButton, _undoRedoStack.NextUndoDescription is { } undo ? $"Undo: {undo}" : "Nothing to undo");
        ToolTip.SetTip(_redoButton, _undoRedoStack.NextRedoDescription is { } redo ? $"Redo: {redo}" : "Nothing to redo");
    }

    /// <summary>Reverses the most recently recorded action, if any (`WP 10.6A`, `ADR-0099`) — real feedback on both the Status Bar and as a Toast, exactly like every other completed action in this window.</summary>
    private async Task UndoAsync()
    {
        var result = await _undoRedoStack.UndoAsync().ConfigureAwait(true);
        if (result is null)
            return;

        var message = result.Succeeded ? "Undo completed." : result.Message ?? "Undo failed.";
        _statusBar.SetText(message);
        _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
        RecordHistory(message);
        RefreshUndoRedoButtons();
        await _explorerView.LoadAsync().ConfigureAwait(true);
        _cockpitView.Refresh();
    }

    /// <summary>Re-applies the most recently undone action, if any (`WP 10.6A`, `ADR-0099`) — mirrors <see cref="UndoAsync"/>'s own identical shape.</summary>
    private async Task RedoAsync()
    {
        var result = await _undoRedoStack.RedoAsync().ConfigureAwait(true);
        if (result is null)
            return;

        var message = result.Succeeded ? "Redo completed." : result.Message ?? "Redo failed.";
        _statusBar.SetText(message);
        _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
        RecordHistory(message);
        RefreshUndoRedoButtons();
        await _explorerView.LoadAsync().ConfigureAwait(true);
        _cockpitView.Refresh();
    }

    /// <summary>
    /// Toggles <paramref name="id"/>'s own Favourite state (`WP 10.6A`) —
    /// the real, shared implementation both the Project Explorer's own
    /// context menu and the <c>Ctrl+D</c> shortcut call through
    /// (<see cref="ProjectExplorerView.ToggleFavouriteRequested"/>/
    /// <see cref="Input.KeyboardShortcutActions.ToggleFavourite"/>).
    /// Records a real Undo/Redo pair (`ADR-0099`) — trivially
    /// self-inverting, since toggling twice is a no-op.
    /// </summary>
    private void ToggleFavourite(System.Guid id, string kind, string displayName)
    {
        var wasFavourite = _favouriteObjects.IsFavourite(id);
        _favouriteObjects.Toggle(id, kind, displayName);
        _favouriteObjects.SaveAsync().GetAwaiter().GetResult();

        var message = wasFavourite ? $"Removed '{displayName}' from Favourites." : $"Added '{displayName}' to Favourites.";
        _statusBar.SetText(message);
        _toastHost.Show(message, FeedbackSeverity.Success);
        RecordHistory(message);

        var favourites = _favouriteObjects;

        // Toggling twice is a no-op — Undo and Redo are the identical
        // operation, so both share this one local function.
        async Task<CommandResult> ToggleAgainAsync(CancellationToken ct)
        {
            favourites.Toggle(id, kind, displayName);
            await favourites.SaveAsync(ct).ConfigureAwait(false);
            return CommandResult.Success();
        }

        _undoRedoStack.Record(new UndoableAction(message, undo: ToggleAgainAsync, redo: ToggleAgainAsync));
        RefreshUndoRedoButtons();
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

    /// <summary>Opens or closes the Auto-Hide flyout for <paramref name="slot"/> — a toggle, so clicking an already-open panel's own edge strip a second time closes it (`WP 10.2B`).</summary>
    private void ToggleFlyout(WorkspaceDockPosition slot, PanelHostControl host, double size)
    {
        if (_openFlyoutSlot == slot)
        {
            CloseFlyout();
            return;
        }

        _docking.ShowFlyout(host, slot, size);
        _openFlyoutSlot = slot;
    }

    /// <summary>Closes whichever Auto-Hide flyout is currently open, if any — a no-op otherwise.</summary>
    private void CloseFlyout()
    {
        _docking.HideFlyout();
        _openFlyoutSlot = null;
    }

    /// <summary>The Menu System — File/View/Theme, each item dispatching an existing capability directly, never a UI-only affordance with no backing behaviour.</summary>
    private Menu BuildMenuSystem(IWorkspace workspace, PanelHostControl explorerHost, PanelHostControl inspectorHost, PanelHostControl outputHost)
    {
        var view = new MenuItem { Header = "_View" };

        var toggleExplorer = new MenuItem { Header = "Project Explorer" };
        toggleExplorer.Click += (_, _) =>
        {
            var visible = !_docking.IsLeftVisible;
            _docking.SetLeftVisible(visible);
            workspace.Layout.SetPlacement(workspace.ProjectExplorer.Id, workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id) with { IsVisible = visible });
        };

        var toggleInspector = new MenuItem { Header = "Property Inspector" };
        toggleInspector.Click += (_, _) =>
        {
            var visible = !_docking.IsRightVisible;
            _docking.SetRightVisible(visible);
            workspace.Layout.SetPlacement(workspace.PropertyInspector.Id, workspace.Layout.GetPlacement(workspace.PropertyInspector.Id) with { IsVisible = visible });
        };

        var toggleOutput = new MenuItem { Header = "Output Panel" };
        toggleOutput.Click += async (_, _) =>
        {
            var visible = !_docking.IsBottomVisible;
            _docking.SetBottomVisible(visible);
            _uiState.OutputVisible = visible;
            if (visible)
                await _outputPanel.ShowAsync().ConfigureAwait(true);
            else
                await _outputPanel.HideAsync().ConfigureAwait(true);
            if (visible)
                _outputView.Refresh(_diagnostics);
        };

        view.Items.Add(toggleExplorer);
        view.Items.Add(toggleInspector);
        view.Items.Add(toggleOutput);
        view.Items.Add(new Separator());

        var layout = new MenuItem { Header = "_Layout" };
        layout.Items.Add(BuildLayoutPresetItem("Engineering", PredefinedLayouts.WorkspaceLayoutPreset.Engineering, workspace, explorerHost, inspectorHost, outputHost));
        layout.Items.Add(BuildLayoutPresetItem("Review", PredefinedLayouts.WorkspaceLayoutPreset.Review, workspace, explorerHost, inspectorHost, outputHost));
        layout.Items.Add(BuildLayoutPresetItem("Documentation", PredefinedLayouts.WorkspaceLayoutPreset.Documentation, workspace, explorerHost, inspectorHost, outputHost));
        layout.Items.Add(new Separator());
        var resetLayout = new MenuItem { Header = "Reset Layout" };
        resetLayout.Click += (_, _) => ResetLayout(workspace, explorerHost, inspectorHost, outputHost);
        layout.Items.Add(resetLayout);
        view.Items.Add(layout);

        var theme = new MenuItem { Header = "_Theme" };
        var toggleTheme = new MenuItem { Header = "Toggle Light/Dark" };
        toggleTheme.Click += async (_, _) => await _theme.ToggleAsync().ConfigureAwait(true);
        theme.Items.Add(toggleTheme);
        theme.Items.Add(new Separator());
        var preferences = new MenuItem { Header = "Preferences..." };
        preferences.Click += async (_, _) => await _settingsDialog.ShowAsync().ConfigureAwait(true);
        theme.Items.Add(preferences);

        var help = new MenuItem { Header = "_Help" };
        var about = new MenuItem { Header = "About TempestOS..." };
        about.Click += async (_, _) =>
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            await _messageDialog.ShowAsync(
                FeedbackSeverity.Info,
                "About TempestOS",
                $"TempestOS Engineering Workspace\nVersion {version}\n\nA Claude-developed engineering platform.").ConfigureAwait(true);
        };
        help.Items.Add(about);

        var commands = new MenuItem { Header = "_Commands" };
        var openPalette = new MenuItem { Header = "Command Palette...   (Ctrl+K)" };
        openPalette.Click += (_, _) => _commandPalette.Open();
        commands.Items.Add(openPalette);

        var document = new MenuItem { Header = "_Document" };
        var nextDoc = new MenuItem { Header = "Next Tab   (Ctrl+Tab)" };
        nextDoc.Click += (_, _) => _documentArea.SelectNextTab();
        var prevDoc = new MenuItem { Header = "Previous Tab   (Ctrl+Shift+Tab)" };
        prevDoc.Click += (_, _) => _documentArea.SelectPreviousTab();
        document.Items.Add(nextDoc);
        document.Items.Add(prevDoc);

        return new Menu { ItemsSource = new[] { view, document, theme, commands, help } };
    }

    /// <summary>Builds one <c>_Layout</c> submenu item applying <paramref name="preset"/> via <see cref="ApplyPreset"/>.</summary>
    private MenuItem BuildLayoutPresetItem(string header, PredefinedLayouts.WorkspaceLayoutPreset preset, IWorkspace workspace, PanelHostControl explorerHost, PanelHostControl inspectorHost, PanelHostControl outputHost)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => ApplyPreset(preset, workspace, explorerHost, inspectorHost, outputHost);
        return item;
    }

    /// <summary>
    /// Applies one of the three named, fixed panel arrangements
    /// (<see cref="PredefinedLayouts"/>, `WP 10.2B`) — every value it sets
    /// already exists somewhere in <see cref="IWorkspaceLayout"/>
    /// (Explorer/Inspector) or <see cref="DesktopPanelUiState"/> (Output/
    /// Collapse/Auto-Hide); applying a preset introduces no new state of
    /// its own, only a fixed, named combination of existing state.
    /// </summary>
    private void ApplyPreset(PredefinedLayouts.WorkspaceLayoutPreset preset, IWorkspace workspace, PanelHostControl explorerHost, PanelHostControl inspectorHost, PanelHostControl outputHost)
    {
        CloseFlyout();

        var explorerPlacement = PredefinedLayouts.ExplorerPlacement(preset, workspace.ProjectExplorer.Id);
        var inspectorPlacement = PredefinedLayouts.InspectorPlacement(preset, workspace.PropertyInspector.Id);
        var outputPlacement = PredefinedLayouts.OutputPanelPlacement(preset);
        var inspectorPinned = PredefinedLayouts.InspectorPinned(preset);

        workspace.Layout.SetPlacement(workspace.ProjectExplorer.Id, explorerPlacement);
        workspace.Layout.SetPlacement(workspace.PropertyInspector.Id, inspectorPlacement);

        explorerHost.SetCollapsed(false);
        explorerHost.SetPinned(true);
        inspectorHost.SetCollapsed(false);
        inspectorHost.SetPinned(inspectorPinned);
        outputHost.SetCollapsed(false);
        outputHost.SetPinned(true);

        _docking.SetLeftWidth(explorerPlacement.Size);
        _docking.SetLeftVisible(explorerPlacement.IsVisible);
        _docking.SetLeftCollapsed(false);

        _docking.SetRightWidth(inspectorPlacement.Size);
        _docking.SetRightVisible(inspectorPlacement.IsVisible);
        _docking.SetRightCollapsed(!inspectorPinned);

        _docking.SetBottomHeight(outputPlacement.Height);
        _docking.SetBottomVisible(outputPlacement.Visible);
        _docking.SetBottomCollapsed(false);

        if (outputPlacement.Visible)
            _outputPanel.ShowAsync().GetAwaiter().GetResult();
        else
            _outputPanel.HideAsync().GetAwaiter().GetResult();

        _uiState.ExplorerCollapsed = false;
        _uiState.ExplorerPinned = true;
        _uiState.InspectorCollapsed = false;
        _uiState.InspectorPinned = inspectorPinned;
        _uiState.OutputVisible = outputPlacement.Visible;
        _uiState.OutputHeight = outputPlacement.Height;
        _uiState.OutputCollapsed = false;
        _uiState.OutputPinned = true;
        _uiState.LastAppliedPreset = preset.ToString();

        _statusBar.SetText($"Layout: {preset}");
    }

    /// <summary>
    /// Resets every panel back to <see cref="IWorkspaceLayout.ResetToDefault"/>'s
    /// own documented default arrangement (Explorer/Inspector — unchanged
    /// since `WP 8.1A`), plus this Work Package's own Desktop-local
    /// defaults (Output hidden, nothing Collapsed, everything pinned) —
    /// the "reset workspace layout" scope item (`WP 10.2B`).
    /// </summary>
    private void ResetLayout(IWorkspace workspace, PanelHostControl explorerHost, PanelHostControl inspectorHost, PanelHostControl outputHost)
    {
        CloseFlyout();

        var defaults = workspace.Layout.ResetToDefault();
        var explorerPlacement = defaults.GetPlacement(workspace.ProjectExplorer.Id);
        var inspectorPlacement = defaults.GetPlacement(workspace.PropertyInspector.Id);
        workspace.Layout.SetPlacement(workspace.ProjectExplorer.Id, explorerPlacement);
        workspace.Layout.SetPlacement(workspace.PropertyInspector.Id, inspectorPlacement);

        explorerHost.SetCollapsed(false);
        explorerHost.SetPinned(true);
        inspectorHost.SetCollapsed(false);
        inspectorHost.SetPinned(true);
        outputHost.SetCollapsed(false);
        outputHost.SetPinned(true);

        _docking.SetLeftWidth(explorerPlacement.Size == 0 ? 240 : explorerPlacement.Size);
        _docking.SetLeftVisible(explorerPlacement.IsVisible);
        _docking.SetLeftCollapsed(false);

        _docking.SetRightWidth(inspectorPlacement.Size == 0 ? 240 : inspectorPlacement.Size);
        _docking.SetRightVisible(inspectorPlacement.IsVisible);
        _docking.SetRightCollapsed(false);

        _docking.SetBottomHeight(160);
        _docking.SetBottomVisible(false);
        _docking.SetBottomCollapsed(false);
        _outputPanel.HideAsync().GetAwaiter().GetResult();

        _uiState.ExplorerCollapsed = false;
        _uiState.ExplorerPinned = true;
        _uiState.InspectorCollapsed = false;
        _uiState.InspectorPinned = true;
        _uiState.OutputVisible = false;
        _uiState.OutputHeight = 160;
        _uiState.OutputCollapsed = false;
        _uiState.OutputPinned = true;
        _uiState.LastAppliedPreset = null;

        _statusBar.SetText("Layout reset to default.");
    }

    /// <summary>
    /// The Quick Access Toolbar (`WP 10.3B`) — a small, always-visible,
    /// fixed strip of the platform's own highest-frequency convenience
    /// actions, above the Ribbon's own tab strip, exactly like every
    /// mainstream ribbon UI's own QAT convention. Subsumes the old,
    /// minimal two-button Toolbar (`WP 10.0B`) — Command Palette and
    /// Theme are unchanged, joined by Reset Layout (`WP 10.2B`, already
    /// real, previously reachable only via the `_Layout` menu) — a
    /// disclosed consolidation, not a silent duplication: every button
    /// here also remains reachable from the Menu System
    /// (`WP10.0A UX Architecture Document.md` §11's own "convenience,
    /// never capability" rule).
    /// </summary>
    private StackPanel BuildQuickAccessToolbar(IWorkspace workspace, PanelHostControl explorerHost, PanelHostControl inspectorHost, PanelHostControl outputHost, EngineeringDomainContext domainContext, Action<Guid, string> navigateToObject)
    {
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelHeaderPadding };

        var paletteButton = new Button { Content = "🔎 Command Palette", MinHeight = DesignTokens.MinControlSize };
        ToolTip.SetTip(paletteButton, "Search every registered command (Ctrl+K)");
        paletteButton.Click += (_, _) => _commandPalette.Open();

        var themeButton = new Button { Content = "🌓 Theme", MinHeight = DesignTokens.MinControlSize };
        themeButton.Click += async (_, _) => await _theme.ToggleAsync().ConfigureAwait(true);

        var resetLayoutButton = new Button { Content = "↺ Reset Layout", MinHeight = DesignTokens.MinControlSize };
        resetLayoutButton.Click += (_, _) => ResetLayout(workspace, explorerHost, inspectorHost, outputHost);

        // The Digital Thread graph (`WP 10.4A`, `ADR-0093`) — opened for
        // the current selection, deduplicated per root object so
        // re-invoking this button on the same object focuses its existing
        // tab rather than opening a second one. Never routed through
        // `INavigationService.OpenAsync` (that path is Kind-factory
        // dispatch for "open this object"; the graph is a cross-cutting
        // Desktop view, not tied to one Kind's own factory) — shown
        // directly via `DocumentAreaView.ShowTab`, the same bypass the
        // Cockpit's own Home tab and `OpenRecentAsync` already use.
        var graphButton = new Button { Content = "🕸 View Relationships", MinHeight = DesignTokens.MinControlSize };
        ToolTip.SetTip(graphButton, "Open the Digital Thread graph for the current selection");
        graphButton.Click += (_, _) =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null)
            {
                _statusBar.SetText("Select an object first to view its Digital Thread relationships.");
                return;
            }

            if (_openGraphViewsByRootId.TryGetValue(selection.ObjectId, out var existingView))
            {
                _documentArea.ShowTab(existingView);
                return;
            }

            var graphView = DigitalThread.DigitalThreadGraphView.TryCreate(selection.ObjectId, selection.Kind, domainContext, navigateToObject);
            if (graphView is null)
            {
                _statusBar.SetText("No Digital Thread graph is available for the current selection.");
                return;
            }

            graphView.ActionCompleted += message => _statusBar.SetText(message);
            _openGraphViewsByRootId[selection.ObjectId] = graphView;
            _documentArea.ShowTab(graphView);
        };

        // Undo/Redo (`WP 10.6A`, `ADR-0099`) — enablement/tooltip kept
        // live by RefreshUndoRedoButtons, called after every recorded,
        // undone, or redone action.
        ToolTip.SetTip(_undoButton, "Nothing to undo");
        ToolTip.SetTip(_redoButton, "Nothing to redo");
        _undoButton.IsEnabled = false;
        _redoButton.IsEnabled = false;
        _undoButton.Click += (_, _) => _ = UndoAsync();
        _redoButton.Click += (_, _) => _ = RedoAsync();

        // User Command Macros (`WP 10.6A` — "foundation").
        var macrosButton = new Button { Content = "🧩 Macros", MinHeight = DesignTokens.MinControlSize };
        ToolTip.SetTip(macrosButton, "Browse, create, and run Command Macros");
        macrosButton.Click += async (_, _) => await _macroManagerDialog.ShowAsync().ConfigureAwait(true);

        bar.Children.Add(paletteButton);
        bar.Children.Add(themeButton);
        bar.Children.Add(resetLayoutButton);
        bar.Children.Add(graphButton);
        bar.Children.Add(_undoButton);
        bar.Children.Add(_redoButton);
        bar.Children.Add(macrosButton);
        return bar;
    }
}
