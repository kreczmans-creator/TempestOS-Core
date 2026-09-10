using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Tempest.Workspace.Editors;
using Tempest.Workspace.Files;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Shell;
using Tempest.Workspace;
using Tempest.Core.Commands;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Composition;
using Tempest.Desktop.History;
using Tempest.Desktop.Tasks;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop;

/// <summary>
/// The rendered content and per-area entry action one <see cref="ShellArea"/>
/// registers into <see cref="MainWindow"/>'s own area registry (`WP 19.2A`,
/// `TD-109`) — the table <see cref="MainWindow.RenderCurrentModuleAsync"/>
/// reads instead of a switch, so a later Work Package adds an area with one
/// entry, never a new case. <paramref name="OnEnter"/> is
/// <see langword="null"/> for an area with nothing to load on entry; one
/// absent from the table renders <see cref="DeclaredCapabilityView"/>.
/// </summary>
internal sealed record ShellAreaRender(Func<Control> Content, Func<Task>? OnEnter);

/// <summary>
/// The Main Window (`WP 10.0B`, modernised through `WP 10.3B`) —
/// assembles every named implementation item across five Work Packages
/// into one running window over the real, unchanged Engineering Workspace
/// six real disciplines already populate.
/// </summary>
/// <remarks>
/// **Composition root, `WP 12.0B` (`ADR-0103`); recomposed `WP 19.2A`
/// (`TD-109`).** <see cref="MainWindowComposer"/> now owns the whole of
/// what used to be this constructor's own ~830-line body, in four explicit
/// phases — <c>BuildViews</c>, <c>BuildCoordinators</c>, <c>Wire</c>,
/// <c>Layout</c> — each returning a record the next consumes; this
/// constructor is the short call sequence over those four, the window's
/// own self-properties, field assignment from what it still needs, and its
/// own <c>Opened</c>/<c>Closing</c> handlers. This class retains only what
/// is irreducibly a composition root's own job: root visual-tree assembly
/// (it *is* the <see cref="Window"/>), delegating construction to the
/// composer, and the cross-collaborator bridges
/// (<see cref="MainWindowCallbacks"/>) no single collaborator can own. See
/// `docs/architecture/Desktop Composition Architecture.md` and `ADR-0103`.
/// </remarks>
public sealed class MainWindow : Window
{
    private readonly ProjectExplorerView _explorerView;
    private readonly PropertyInspectorView _inspectorView;
    private readonly StatusBarView _statusBar;
    private readonly WorkspaceDockingComposer _dockingComposer;
    private readonly Viewing.AttachmentViewerLauncher _attachmentViewers;
    private readonly IDiagnosticsProvider _diagnostics;
    private readonly RibbonView _ribbon;
    private readonly ToastHost _toastHost;
    private readonly ConfirmationDialog _confirmationDialog;
    private readonly InputDialog _inputDialog;
    private readonly MessageDialog _messageDialog;
    private readonly SettingsDialog _settingsDialog;
    private readonly DesktopSessionState _session;
    private readonly WorkspaceViewCoordinator _viewCoordinator;
    private readonly UndoRedoCoordinator _undoRedo;
    private readonly IWorkspaceManager _workspaceManager;
    private readonly DocumentAreaView _documentArea;
    private readonly CommandPaletteOverlay _commandPalette;
    private readonly MacroManagerDialog _macroManagerDialog;
    private readonly ActionOutcomeReporter _actionReporter;
    private readonly DockPanel _dock;

    // The Evidence workspace's own picker/entry dialogs (`WP 18.2A`).
    private readonly CitationPicker _citationPicker;
    private readonly SubjectPicker _subjectPicker;
    private readonly DeclaredFigureEntry _declaredFigureEntry;
    private readonly CheckEntry _checkEntry;
    private readonly IssueEntry _issueEntry;
    private readonly ReviseReferenceRecordEntry _reviseReferenceRecordEntry;

    // The project Commercial section's own pickers, and the Timesheets/
    // Deliverables prompts (`WP 19.0A`, `ADR-0150`).
    private readonly OrganisationPicker _organisationPicker;
    private readonly RateCardPicker _rateCardPicker;
    private readonly TimesheetEntryPrompt _timesheetEntryPrompt;
    private readonly DeliverableCompletionPrompt _deliverableCompletionPrompt;

    // The Product Spine (`TD-84`) — Module -> Project -> Workspace.
    private readonly IShellNavigator _navigator;
    private readonly IProjectContext _projectContext;
    private readonly GlobalNavigationRail _navigationRail;
    private readonly ShellHeaderView _header;
    private readonly ProjectBrowserView _projectBrowser;
    private readonly ProjectWorkspaceView _projectWorkspace;
    private readonly ContentControl _moduleHost;
    private readonly Control _engineeringSurface;
    private readonly IProjectDirectory _projectDirectory;

    private readonly EngineeringCalculationView _engineeringCalculation;
    private readonly EngineeringCalculationCoordinator _engineeringCalculationCoordinator;
    private bool _engineeringCalculationLoaded;

    // The Evidence workspace (`WP 18.2A`, `ADR-0148`).
    private readonly EvidenceWorkspaceView _evidenceWorkspace;

    // The Timesheets area (`WP 19.0A`, `ADR-0150`).
    private readonly TimesheetWeekView _timesheetWeekView;

    // WP 10.6A — Command Execution & Productivity Experience.
    private readonly CommandHistoryLog _commandHistory;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;

    private readonly IEngineeringScope _engineeringScope;
    private readonly EngineeringDomainContext _domainContext;

    /// <summary>The area registry (`WP 19.2A`, `TD-109`) — see <see cref="ShellAreaRender"/>.</summary>
    private readonly Dictionary<ShellArea, ShellAreaRender> _areaRegistry;

    /// <summary>Initialises a new instance of the <see cref="MainWindow"/> class over an already-started <see cref="WorkspaceHost"/>.</summary>
    /// <param name="host">The already-started Workspace Host this window presents.</param>
    /// <param name="evidenceFilePickerOverride">
    /// The Evidence workspace's own <see cref="IFilePicker"/> — <see langword="null"/>
    /// (the default, used by the real running application) constructs the
    /// real <see cref="AvaloniaFilePicker"/> over this window's own
    /// <see cref="TopLevel"/>. Injectable so a headless journey test can
    /// supply a stub that returns bytes from a temp file with no OS dialog
    /// ever on screen (`WP 18.2A`, Execution Plan §3 decision 6).
    /// </param>
    public MainWindow(WorkspaceHost host, IFilePicker? evidenceFilePickerOverride = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        // The brand's own shell chrome — the window's own job, never the
        // composer's (`WP 19.2A`). The build is in the title bar (`WP
        // 17.9.4`): version and short commit.
        Title = $"TempestOS {DescribeBuild(host.Services!)}";
        MinWidth = 960;
        MinHeight = 600;
        FontFamily = DesignTokens.BodyFont;
        FontSize = 12.5;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, BrandPalette.PageBackgroundBrushKey);
        ChromeStyles.Install(this);
        ApplyBrandIcon();

        // `MainWindowComposer`'s own four explicit phases (`WP 19.2A`,
        // `TD-109`) — see that type's own remarks. `callbacks` is this
        // window's own back half of the one genuine two-way bridge: the
        // few methods below that must stay on this class (several fields
        // at once, or reached directly by an acceptance test), threaded
        // into the composer as plain delegates rather than a reference to
        // this window itself (`ADR-0103`).
        var composer = new MainWindowComposer();
        var callbacks = new MainWindowCallbacks(
            RecordHistory, RefreshOutputPanelExtras, RefreshStatusBar, SetCurrentArea,
            OpenObjectAsync, OpenProjectAttachmentAsync, OpenEvidenceRecordAsync, PromptForNewProjectAsync, RenderCurrentModuleAsync);

        var views = composer.BuildViews(host, this, evidenceFilePickerOverride, callbacks);
        var coordinators = composer.BuildCoordinators(host, this, views, callbacks);
        composer.Wire(host, this, views, coordinators, callbacks);
        var layout = composer.Layout(host, this, views, coordinators);
        Content = layout.Content;

        // This window's own retained fields — every collaborator a method
        // below still needs directly (see each field's own declaration).
        _explorerView = views.ExplorerView;
        _inspectorView = views.InspectorView;
        _statusBar = views.StatusBar;
        _dockingComposer = coordinators.DockingComposer;
        _attachmentViewers = coordinators.AttachmentViewers;
        _diagnostics = views.Diagnostics;
        _ribbon = views.Ribbon;
        _toastHost = views.ToastHost;
        _confirmationDialog = views.ConfirmationDialog;
        _inputDialog = views.InputDialog;
        _messageDialog = views.MessageDialog;
        _settingsDialog = views.SettingsDialog;
        _session = views.Session;
        _viewCoordinator = coordinators.ViewCoordinator;
        _undoRedo = coordinators.UndoRedo;
        _workspaceManager = views.Manager;
        _documentArea = views.DocumentArea;
        _commandPalette = views.CommandPalette;
        _macroManagerDialog = views.MacroManagerDialog;
        _actionReporter = views.ActionReporter;
        _dock = layout.Dock;
        _citationPicker = views.CitationPicker;
        _subjectPicker = views.SubjectPicker;
        _declaredFigureEntry = views.DeclaredFigureEntry;
        _checkEntry = views.CheckEntry;
        _issueEntry = views.IssueEntry;
        _reviseReferenceRecordEntry = views.ReviseReferenceRecordEntry;
        _organisationPicker = views.OrganisationPicker;
        _rateCardPicker = views.RateCardPicker;
        _timesheetEntryPrompt = views.TimesheetEntryPrompt;
        _deliverableCompletionPrompt = views.DeliverableCompletionPrompt;
        _navigator = host.ShellNavigator!;
        _projectContext = host.ProjectContext!;
        _navigationRail = views.NavigationRail;
        _header = views.Header;
        _projectBrowser = views.ProjectBrowser;
        _projectWorkspace = views.ProjectWorkspace;
        _moduleHost = views.ModuleHost;
        _engineeringSurface = layout.EngineeringSurface;
        _projectDirectory = views.ProjectDirectory;
        _engineeringCalculation = views.EngineeringCalculation;
        _engineeringCalculationCoordinator = coordinators.EngineeringCalculationCoordinator;
        _evidenceWorkspace = coordinators.EvidenceWorkspace;
        _timesheetWeekView = views.TimesheetWeekView;
        _commandHistory = views.CommandHistory;
        _backgroundTaskRunner = views.BackgroundTaskRunner;
        _engineeringScope = host.EngineeringScope!;
        _domainContext = views.Composition.DomainContext;

        // The area registry (`WP 19.2A`, `TD-109`) — see
        // `RenderCurrentModuleAsync`/`ShellAreaRender`.
        _areaRegistry = new Dictionary<ShellArea, ShellAreaRender>
        {
            [ShellArea.Projects] = new(() => _projectBrowser, () => _projectBrowser.RefreshAsync()),
            [ShellArea.ProjectWorkspace] = new(() => _projectWorkspace, () => _projectWorkspace.RefreshAsync()),
            // Both render the engineering surface: the Cockpit is a panel
            // within it. `WP 17.9.1`: Engineering alone is not usable
            // without the Project Explorer and Properties panels, so
            // entering it (never Home) guarantees they are present
            // whatever a saved layout says.
            [ShellArea.Home] = new(() => _engineeringSurface, null),
            [ShellArea.Engineering] = new(() => _engineeringSurface, EnterEngineeringAsync),
            // `WP 18.2A`: re-read on every entry, the same "load when you
            // land here" discipline every other area follows.
            [ShellArea.Evidence] = new(() => _evidenceWorkspace, () => _evidenceWorkspace.RefreshAsync()),
            [ShellArea.EngineeringCalculation] = new(() => _engineeringCalculation, EnterEngineeringCalculationAsync),
            // `WP 19.0A` (`ADR-0150`): re-read on every entry, the same
            // "load when you land here" discipline every other area
            // follows.
            [ShellArea.Timesheets] = new(() => _timesheetWeekView, () => _timesheetWeekView.RefreshAsync()),
        };

        // `TD-84`: no Explorer area is selected by default — the
        // Engineering Cockpit, not an Explorer area, is the Workspace's
        // own default landing screen (`ADR-0069`). Selecting the first
        // available area here gives the Project Explorer real content
        // immediately, a presentation-layer default, not a change to that
        // existing "no default area" behaviour itself.
        Opened += async (_, _) =>
        {
            await views.Theme.LoadAsync().ConfigureAwait(true);

            // The workspace arrangement the user left (`TD-72`) —
            // restored before anything renders.
            await _dockingComposer.RestoreLayoutAsync().ConfigureAwait(true);

            var firstArea = views.Workspace.Navigation.Areas.FirstOrDefault();
            if (firstArea is not null)
                await views.Workspace.Navigation.SwitchAreaAsync(firstArea.Id).ConfigureAwait(true);

            await _explorerView.LoadAsync().ConfigureAwait(true);
            SetCurrentArea(firstArea?.Title);
            RefreshStatusBar(views.Manager);
            await coordinators.CockpitView.RefreshAsync().ConfigureAwait(true);

            // Render whichever module the recovered location names
            // (`TD-84`) — the shell opens where the user left it.
            await RenderCurrentModuleAsync().ConfigureAwait(true);
        };

        // Graceful shutdown (`WP 10.5B` scope) — one real, consolidated
        // Closing gate. `Closing`'s own event handler is not awaited by
        // Avalonia itself, so every path synchronously sets `e.Cancel =
        // true` first, then re-closes programmatically once the real,
        // awaited work has actually completed.
        var closeConfirmed = false;
        Closing += async (_, e) =>
        {
            if (closeConfirmed)
                return;

            e.Cancel = true;

            if (views.DocumentArea.HasAnyDirtyTab)
            {
                var discard = await views.ConfirmationDialog.ConfirmAsync(
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

            closeConfirmed = true;
            Close();
        };
    }

    /// <summary>
    /// Persists this Work Package's own Desktop-local panel UI state
    /// (Collapse/Auto-Hide/Output — <see cref="DesktopPanelUiState"/>) —
    /// called from <c>App.cs</c>'s own <c>ShutdownRequested</c> handler,
    /// alongside (never inside) <see cref="WorkspaceHost.ShutdownAsync"/>'s
    /// own, separate save of <see cref="IWorkspaceState"/>.
    /// </summary>
    public Task SaveDesktopUiStateAsync() => _session.PanelUiState.SaveAsync();

    /// <summary>Persists the workspace arrangement (`TD-72`) — where the user put their panels, tabs, splits and floating windows.</summary>
    public Task SaveWorkspaceLayoutAsync() => _dockingComposer.Layout.SaveAsync();

    /// <summary>Restores the saved workspace arrangement, or a returning user's own migrated preferences on first run (`TD-72`).</summary>
    public Task RestoreWorkspaceLayoutAsync() => _dockingComposer.RestoreLayoutAsync();

    /// <summary>The workspace layout controller — the one owner of the arrangement (`TD-72`).</summary>
    public Docking.WorkspaceLayoutController WorkspaceLayout => _dockingComposer.Layout;

    /// <summary>The document and drawing viewer's opener (`TD-80`) — exposed so a test can open a document exactly as the editor's Open button does.</summary>
    public Viewing.AttachmentViewerLauncher AttachmentViewers => _attachmentViewers;

    /// <summary>
    /// Opens one of the open project's own files in the `TD-80` viewer,
    /// resolving the object and attachment the Documents area named — the
    /// same launcher the object editor's Open button uses. Opening never
    /// navigates: the module, the open project and the project area the
    /// user was on are all untouched.
    /// </summary>
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

    /// <summary>Opens an engineering object in the document area, as the Explorer's own activation does — exposed so a test can reach an object's real editor and press its own Open button.</summary>
    public Task NavigateToObjectAsync(Guid id, string kind) => _viewCoordinator.NavigateToObjectAsync(id, kind);

    /// <summary>Professional Error Handling (`WP 10.5B` scope) — a real <see cref="MessageDialog"/> for a genuinely unexpected exception, called from <c>App.cs</c>'s own <see cref="TaskScheduler.UnobservedTaskException"/> handler.</summary>
    public Task ShowUnexpectedErrorAsync(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return _messageDialog.ShowAsync(
            FeedbackSeverity.Error,
            "Unexpected Error",
            "Something went wrong that TempestOS did not anticipate. The application should remain usable — if something looks wrong, save your work and restart.",
            $"{exception.GetType().FullName}: {exception.Message}");
    }

    /// <summary>Refreshes every Status Bar segment from real, current sources.</summary>
    private void RefreshStatusBar(WorkspaceManager manager)
    {
        _statusBar.SetText(manager.StatusBar.StatusText);
        _statusBar.SetDiagnostics(_diagnostics);
        _dockingComposer.OutputView.Refresh(_diagnostics);
        RefreshOutputPanelExtras();
    }

    /// <summary>
    /// Renders whichever module the navigator currently reports (`TD-84`),
    /// from the area registry (`WP 19.2A`, `TD-109`) built in the
    /// constructor — the shell's one place that decides what is on screen,
    /// derived from <see cref="IShellNavigator.Current"/> so it can never
    /// disagree with the navigation state. An area absent from the
    /// registry renders <see cref="DeclaredCapabilityView"/>, honestly
    /// naming what is missing rather than a dead button or a fake screen.
    /// </summary>
    public async Task RenderCurrentModuleAsync()
    {
        var location = _navigator.Current;

        if (_areaRegistry.TryGetValue(location.Area, out var entry))
        {
            if (entry.OnEnter is not null)
                await entry.OnEnter().ConfigureAwait(true);

            _moduleHost.Content = entry.Content();
        }
        else
        {
            _moduleHost.Content = new DeclaredCapabilityView(ShellAreas.For(location.Area), _projectContext.Current?.Label);
        }

        _navigationRail.RefreshSelection();
        RefreshProjectStatus();

        if (location.Area == ShellArea.Engineering)
            await RefreshEngineeringScopeAsync().ConfigureAwait(true);
    }

    /// <summary>The Engineering area's own entry action — see <see cref="_areaRegistry"/>.</summary>
    private Task EnterEngineeringAsync()
    {
        _dockingComposer.EnsureCorePanelsPresent();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The Engineering Calculation area's own entry action — see
    /// <see cref="_areaRegistry"/>. The library is re-read on every entry,
    /// because a material released elsewhere in the session must not
    /// still read as Draft here; the stored calculation is recovered once,
    /// on the first entry of the session, which is what makes a relaunch
    /// show the result the engineer left behind.
    /// </summary>
    private async Task EnterEngineeringCalculationAsync()
    {
        await _engineeringCalculationCoordinator
            .RefreshAsync(restoreInputs: !_engineeringCalculationLoaded)
            .ConfigureAwait(true);
        _engineeringCalculationLoaded = true;
    }

    /// <summary>
    /// Shows the current project in the Status Bar (`TD-84`) — the "see
    /// the current project everywhere appropriate" requirement, met from
    /// the one real context rather than a caption a view sets on itself.
    /// </summary>
    private void RefreshProjectStatus()
    {
        var location = _navigator.Current;
        _statusBar.SetProject(_projectContext.Current?.Label);
        _statusBar.SetLocation(DescribeLocation(location));
        _header.SetContext(DescribeModule(location), _projectContext.Current?.Label, DescribeDetail(location));
    }

    /// <summary>The module the header names — the rail's own title for the current area, so the two can never disagree.</summary>
    internal static string DescribeModule(ShellLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return location.Area == ShellArea.ProjectWorkspace
            ? ShellAreas.For(ShellArea.Projects).Title
            : ShellAreas.For(location.Area).Title;
    }

    /// <summary>The second-level detail the header shows after the project — the project area inside a project, or the engineering scope.</summary>
    internal static string? DescribeDetail(ShellLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

        return location.Area switch
        {
            ShellArea.ProjectWorkspace when location.ProjectArea is { } area => ProjectAreas.For(area).Title,
            ShellArea.Engineering => location.IsStandaloneEngineering ? "Standalone engineering" : "Project engineering",
            _ => null,
        };
    }

    /// <summary>Sets the window's own icon to the TempestOS mark — the brand pack's own app icon artwork, shipped as an embedded asset.</summary>
    private void ApplyBrandIcon()
    {
        var uri = new Uri("avares://Tempest.Desktop/Assets/Brand/tempest-appicon-256.png");
        if (AssetLoader.Exists(uri))
        {
            using var stream = AssetLoader.Open(uri);
            Icon = new WindowIcon(stream);
        }
    }

    /// <summary>
    /// Reports the Engineering Workspace's own current scope and how many
    /// engineering objects are actually in it (`TD-89`).
    /// </summary>
    public async Task RefreshEngineeringScopeAsync()
    {
        if (_navigator.Current.Area != ShellArea.Engineering)
            return;

        var scope = _engineeringScope.Current;
        var objects = await _engineeringScope.ListObjectsAsync().ConfigureAwait(true);

        _statusBar.SetLocation($"{DescribeLocation(_navigator.Current)} · {scope.Label} · {objects.Count} object(s)");
        _header.SetContext(DescribeModule(_navigator.Current), _projectContext.Current?.Label, $"{scope.Label} · {objects.Count} object(s)");
    }

    /// <summary>
    /// A one-line answer to "where am I", derived from the navigation
    /// state (`TD-89`).
    /// </summary>
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
    /// (`WP 10.6A`). <c>succeeded</c> is a disclosed heuristic — inferred
    /// here from whether the message contains "fail", mirroring
    /// <c>EngineeringCockpit</c>'s own established "disclosed heuristic"
    /// precedent (<c>IsOutOfDate</c>/<c>HasMissingEvidence</c>).
    /// </summary>
    private void RecordHistory(string message)
    {
        _commandHistory.Record(message, succeeded: !message.Contains("fail", StringComparison.OrdinalIgnoreCase));
        RefreshOutputPanelExtras();
    }

    /// <summary>"0.17.0 (9e52a53)": the platform's semantic version with the build metadata shortened to a commit prefix, or just the version when there is none.</summary>
    internal static string DescribeBuild(Tempest.Core.DependencyInjection.ITempestServiceProvider services)
    {
        var version = (services.GetService(typeof(Tempest.Core.Versioning.IPlatformVersionProvider)) as Tempest.Core.Versioning.IPlatformVersionProvider)?.Version.SemanticVersion;
        if (string.IsNullOrWhiteSpace(version))
            return string.Empty;

        var plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
            return version;

        var metadata = version[(plus + 1)..];
        return $"{version[..plus]} ({(metadata.Length > 7 ? metadata[..7] : metadata)})";
    }

    /// <summary>Takes the user to an object — one they just made (`WP 17.9.4`) or found (`WP 18.1B`): the Explorer switches to the area that lists its Kind, reloads, expands the path to it and selects it; then the object opens in the editor tab.</summary>
    internal async Task OpenObjectAsync(Guid id, string kind)
    {
        var workspace = _workspaceManager.Current;
        if (workspace is null)
            return;

        var areaId = DisciplineAreas.AreaFor(kind);
        if (areaId is not null && workspace.Navigation.Areas.FirstOrDefault(a => a.Id == areaId) is { } area)
        {
            await workspace.Navigation.SwitchAreaAsync(area.Id).ConfigureAwait(true);
            _ribbon.SelectTabForArea(area.Title);
            SetCurrentArea(area.Title);
        }

        await _explorerView.LoadAsync().ConfigureAwait(true);
        _explorerView.Reveal(id);
        await workspace.Selection.SelectAsync(id, kind).ConfigureAwait(true);
        await _viewCoordinator.NavigateToObjectAsync(id, kind).ConfigureAwait(true);
        _inspectorView.SetCurrentSelection(id, kind);
        await _inspectorView.RefreshFromSourceAsync().ConfigureAwait(true);
    }

    /// <summary>The `WP 17.9.4` name, kept working: an alias for <see cref="OpenObjectAsync"/>, generalised by `WP 18.1B` §2/§5 to open any found object, not only a created one.</summary>
    internal Task OpenCreatedObjectAsync(Guid id, string kind) => OpenObjectAsync(id, kind);

    /// <summary>
    /// Opens an Evidence record's own editor from the Evidence rail area
    /// (`WP 18.2A`) — Create and opening a row both call this. The Object
    /// Editor's own document tabs live in the Engineering module's own
    /// docking layout, which <see cref="_moduleHost"/> does not show while
    /// Evidence itself is on screen; without switching first, "opens right
    /// up" (`WP 17.9.4`) would open the tab behind a module the user is
    /// not looking at.
    /// </summary>
    private async Task OpenEvidenceRecordAsync(Guid id, string kind)
    {
        await _navigator.GoToEngineeringAsync().ConfigureAwait(true);
        await RenderCurrentModuleAsync().ConfigureAwait(true);
        await _viewCoordinator.NavigateToObjectAsync(id, kind).ConfigureAwait(true);
    }

    private void SetCurrentArea(string? title)
    {
        _statusBar.SetArea(title);
        _ribbon.SelectTabForArea(title);
    }
}
