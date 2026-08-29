using Avalonia.Controls;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Requirements;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Wires every Explorer/Inspector/Document-Area cross-view interaction —
/// select-to-inspect, open-to-edit, the Object Editor Framework's own
/// injectable content builder, drag-and-drop reparenting, Favourite
/// toggling, and dirty-aware document close — extracted, `WP 12.0B`
/// (`ADR-0103`), from <see cref="MainWindow"/>'s own previous
/// <c>NavigateToObjectAsync</c>/<c>BuildDocumentContent</c>/Explorer-and-
/// Inspector-event-wiring/<c>CloseDocumentAsync</c>/<c>ToggleFavourite</c>
/// members, unmodified in behaviour. A collaborator under `ADR-0103`:
/// constructed once by <see cref="MainWindow"/> (the composition root),
/// declaring only the dependencies it actually needs, never
/// DI-registered, never referencing <see cref="MainWindow"/> or any
/// sibling collaborator back.
/// </summary>
/// <remarks>
/// <see cref="Attach"/> exists because <see cref="DocumentAreaView"/> must
/// itself be constructed with <see cref="BuildDocumentContent"/> as its
/// own injectable content builder — a genuine construction-order cycle,
/// this collaborator needs to exist before <see cref="DocumentAreaView"/>
/// can, and <see cref="DocumentAreaView"/> must exist before its own
/// <c>TabCloseRequested</c> event can be subscribed. The pre-decomposition
/// source resolved the identical cycle with a "field assigned after
/// construction, read lazily by a deferred delegate" two-phase sequencing
/// (its own constructor remarks: "a local function's own captured-field
/// flow state is evaluated at its own declaration point, not its own
/// later invocation point"); <see cref="Attach"/> is that same, unchanged
/// resolution, one collaborator boundary away rather than one field
/// assignment away. Undo/Redo button refresh needs no explicit call here
/// — <c>Stack</c> is the plain <see cref="IUndoRedoStack"/>
/// <c>UndoRedoCoordinator</c> already owns and reactively refreshes from
/// (<see cref="IUndoRedoStack.Changed"/>), passed once as a value, never
/// a reference to that collaborator itself.
/// </remarks>
/// <remarks>
/// **`WP 12.4B` (`ADR-0104`).** Previously also carried
/// <see cref="CockpitView"/> through this same two-phase-construction
/// cycle purely to call its own <c>Refresh()</c> — WP12.0B's own
/// architecture review, Finding 5, flagged this as heavier than the
/// actual need warranted, since <see cref="CockpitView"/>'s only other
/// use anywhere in this class was as <see cref="Attach"/>'s own second
/// parameter. Replaced with a plain <c>Action refreshCockpit</c>
/// constructor parameter — `ADR-0104`'s own "direct delegate over object
/// reference" default — supplied by <see cref="MainWindow"/> (the
/// composition root) via the same field-closure lazy-capture pattern
/// already used here for <see cref="DocumentAreaView"/> itself. Only one
/// genuine construction-order cycle (<see cref="DocumentAreaView"/>)
/// remains after this change.
/// </remarks>
internal sealed class WorkspaceViewCoordinator
{
    private readonly IWorkspace _workspace;
    private readonly WorkspaceManager _manager;
    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IRequirementsService _requirementsService;
    private readonly CalculationTemplateRegistry? _calculationTemplates;
    private readonly ProjectExplorerView _explorerView;
    private readonly PropertyInspectorView _inspectorView;
    private readonly RibbonView _ribbon;
    private readonly StatusBarView _statusBar;
    private readonly ToastHost _toastHost;
    private readonly ConfirmationDialog _confirmationDialog;
    private readonly IUndoRedoStack _undoRedoStack;
    private readonly RecentObjectsState _recentObjects;
    private readonly FavouriteObjectsState _favouriteObjects;
    private readonly Dictionary<Guid, IWorkspaceView> _openGraphViewsByRootId;
    private readonly Action _refreshStatusBar;
    private readonly Action<string> _recordHistory;
    private readonly Action _refreshCockpit;

    private DocumentAreaView? _documentArea;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceViewCoordinator"/> class, wiring every Explorer/Inspector cross-view interaction that does not need <see cref="DocumentAreaView"/> to already exist (see <see cref="Attach"/>).</summary>
    /// <summary>
    /// Opens one of an object's attachments in the document viewer
    /// (`TD-80`), set by the shell that owns the workspace.
    /// </summary>
    /// <remarks>
    /// A settable collaborator rather than a twentieth constructor
    /// parameter, and deliberately optional: a coordinator used outside
    /// the docked workspace has nowhere to open a document, and the
    /// editor's Open affordance simply does not appear.
    /// </remarks>
    public Func<IHasAttachments, IAttachment, Task>? OpenAttachmentAsync { get; set; }

    public WorkspaceViewCoordinator(
        IWorkspace workspace, WorkspaceManager manager, EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher,
        IRequirementsService requirementsService, CalculationTemplateRegistry? calculationTemplates,
        ProjectExplorerView explorerView, PropertyInspectorView inspectorView, RibbonView ribbon,
        StatusBarView statusBar, ToastHost toastHost, ConfirmationDialog confirmationDialog, IUndoRedoStack undoRedoStack,
        RecentObjectsState recentObjects, FavouriteObjectsState favouriteObjects, Dictionary<Guid, IWorkspaceView> openGraphViewsByRootId,
        Action refreshStatusBar, Action<string> recordHistory, Action refreshCockpit)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(explorerView);
        ArgumentNullException.ThrowIfNull(inspectorView);
        ArgumentNullException.ThrowIfNull(ribbon);
        ArgumentNullException.ThrowIfNull(statusBar);
        ArgumentNullException.ThrowIfNull(toastHost);
        ArgumentNullException.ThrowIfNull(confirmationDialog);
        ArgumentNullException.ThrowIfNull(undoRedoStack);
        ArgumentNullException.ThrowIfNull(recentObjects);
        ArgumentNullException.ThrowIfNull(favouriteObjects);
        ArgumentNullException.ThrowIfNull(openGraphViewsByRootId);
        ArgumentNullException.ThrowIfNull(refreshStatusBar);
        ArgumentNullException.ThrowIfNull(recordHistory);
        ArgumentNullException.ThrowIfNull(refreshCockpit);

        _workspace = workspace;
        _manager = manager;
        _domainContext = domainContext;
        _commandDispatcher = commandDispatcher;
        _requirementsService = requirementsService;
        _calculationTemplates = calculationTemplates;
        _explorerView = explorerView;
        _inspectorView = inspectorView;
        _ribbon = ribbon;
        _statusBar = statusBar;
        _toastHost = toastHost;
        _confirmationDialog = confirmationDialog;
        _undoRedoStack = undoRedoStack;
        _recentObjects = recentObjects;
        _favouriteObjects = favouriteObjects;
        _openGraphViewsByRootId = openGraphViewsByRootId;
        _refreshStatusBar = refreshStatusBar;
        _recordHistory = recordHistory;
        _refreshCockpit = refreshCockpit;

        // Select-to-inspect / Open-to-edit (WP8.0A UI Architecture.md §4, unchanged).
        _explorerView.ObjectSelected += async (id, kind) =>
        {
            await _workspace.Selection.SelectAsync(id, kind).ConfigureAwait(true);
            _inspectorView.SetCurrentSelection(id, kind);
            _inspectorView.Refresh();
            _refreshStatusBar();
            _ribbon.RefreshEnablement();
        };
        _explorerView.ObjectOpened += async (id, kind) =>
        {
            var view = await _workspace.Navigation.OpenAsync(id, kind).ConfigureAwait(true);
            _documentArea!.ShowTab(view);
            _refreshCockpit();

            // "Recent objects" (`WP 10.6A`) — recorded here, the one
            // place every Open path already converges (tree double-click/
            // Enter, and this Work Package's own Recent Objects/
            // Favourites flyouts, both of which raise this identical
            // event rather than duplicating this logic).
            _recentObjects.Record(id, kind, view.Title);
        };
        _explorerView.ActionCompleted += (message, outcome) =>
        {
            _statusBar.SetText(message);
            _toastHost.Show(message, outcome.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            _recordHistory(message);

            // Success-gated (`TD-58`): a refused rename/delete/move
            // changed nothing, so no Cockpit rebuild — and a failure no
            // longer reports itself as a Success toast. The Inspector
            // re-renders too: a successful delete cleared the selection,
            // and a successful rename changed the displayed facets.
            if (outcome.WorkspaceChanged)
            {
                _ = _inspectorView.RefreshFromSourceAsync();
                _refreshCockpit();
            }
        };
        _explorerView.RecentObjects = _recentObjects;
        _explorerView.Favourites = _favouriteObjects;
        _explorerView.ToggleFavouriteRequested = ToggleFavourite;

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

            var result = await _commandDispatcher.DispatchAsync(move, CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? "Moved." : result.Message ?? "Move failed.";
            _statusBar.SetText(message);
            _toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded)
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _refreshCockpit();
            }
        };
        _inspectorView.ActionCompleted += async (message, outcome) =>
        {
            _statusBar.SetText(message);
            _toastHost.Show(message, outcome.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            _recordHistory(message);

            // Success-gated (`TD-58`).
            if (outcome.WorkspaceChanged)
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                _refreshCockpit();
            }
        };
    }

    /// <summary>
    /// Attaches the now-constructed <see cref="DocumentAreaView"/> — must
    /// be called exactly once, immediately after it is constructed (see
    /// this type's own remarks for why it needs this collaborator to
    /// exist first), wiring the one piece of Document Area interaction
    /// (Tab Close) that could not be wired inside this collaborator's own
    /// constructor.
    /// </summary>
    public void Attach(DocumentAreaView documentArea)
    {
        ArgumentNullException.ThrowIfNull(documentArea);

        _documentArea = documentArea;
        _documentArea.TabCloseRequested += viewId => _ = CloseDocumentAsync(viewId);
    }

    /// <summary>
    /// The Object Editor Framework's own injectable content builder (`WP
    /// 10.3A`) — <see cref="DocumentAreaView"/>'s own constructor
    /// parameter. Tries a real per-Kind editor first
    /// (<see cref="ObjectEditorView.TryCreate"/>), falling back to the
    /// original generic three-line body for any Kind with no real
    /// Engineering Domain object behind it (a synthetic Kind, or the
    /// Sample Explorer's own fixed content) — <see cref="DocumentAreaView"/>
    /// itself stays completely agnostic to which.
    /// </summary>
    /// <remarks>
    /// Only ever invoked as a deferred delegate, after
    /// <see cref="Attach"/> has already run — the
    /// null-forgiving operator on <see cref="_documentArea"/> here
    /// suppresses a known, harmless nullable-flow-analysis limitation (a
    /// method's own captured-field flow state is evaluated at its own
    /// declaration point, not its own later invocation point), not a
    /// genuine possible-null risk.
    /// </remarks>
    public Control BuildDocumentContent(IWorkspaceView view)
    {
        // The Digital Thread graph (`WP 10.4A`) is itself both the
        // IWorkspaceView and its own rendered Control — unlike every
        // other document Kind, it is not built from `view` by a
        // per-Kind factory here; it already IS the content. Checked
        // first, generically, so any future self-rendering View gets
        // the identical treatment without another special case.
        if (view is Control alreadyBuilt)
            return alreadyBuilt;

        var editor = ObjectEditorView.TryCreate(view.ObjectId, view.ObjectKind, _domainContext, _manager, NavigateToObject, _commandDispatcher, _requirementsService, _calculationTemplates);
        if (editor is null)
            return DocumentAreaView.BuildDefaultBody(view);

        editor.DirtyChanged += dirty => _documentArea!.MarkDirty(view.Id, dirty);

        // `TD-80`: the editor asks; the shell decides where a document
        // opens. Fire-and-forget because opening is the user's gesture and
        // must not block the editor's own event dispatch; the viewer
        // surfaces its own Missing/Corrupt/Unsupported state, so there is
        // no result here worth awaiting.
        editor.OpenAttachmentRequested += (owner, attachment) => _ = OpenAttachmentAsync?.Invoke(owner, attachment);
        editor.ActionCompleted += async (message, outcome) =>
        {
            _statusBar.SetText(message);
            _toastHost.Show(message, outcome.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            _recordHistory(message);

            // Success-gated (`TD-58`): a rejected Save/Execute/Attach
            // changed nothing, so the Explorer/Inspector/Cockpit keep
            // their current, still-correct state. The Inspector re-reads
            // its facets from source — a plain Refresh() would re-render
            // the cached, pre-mutation values.
            if (outcome.WorkspaceChanged)
            {
                await _explorerView.LoadAsync().ConfigureAwait(true);
                await _inspectorView.RefreshFromSourceAsync().ConfigureAwait(true);
                _refreshCockpit();
            }
        };
        // Undo/Redo (`WP 10.6A`, `ADR-0099`) — every discipline's own
        // Object Editor shares this one commit path, so this single
        // subscription covers Rename across all six disciplines.
        editor.UndoableActionRecorded += action => _undoRedoStack.Record(action);
        return editor;
    }

    /// <summary>Opens or focuses <paramref name="id"/>, then shows it in the Document Area — the shared navigation path every Cockpit/Object Editor/Digital Thread graph "open this object" gesture calls through.</summary>
    public async Task NavigateToObjectAsync(Guid id, string kind)
    {
        var relatedView = await _workspace.Navigation.OpenAsync(id, kind).ConfigureAwait(true);
        _documentArea!.ShowTab(relatedView);
        _refreshCockpit();
    }

    /// <summary>Fire-and-forget wrapper over <see cref="NavigateToObjectAsync"/> — the delegate shape every synchronous callback site (Object Editor "Open →" links, the Cockpit's own Favourite Projects card, the Digital Thread graph) needs.</summary>
    public void NavigateToObject(Guid id, string kind) => _ = NavigateToObjectAsync(id, kind);

    /// <summary>
    /// Toggles <paramref name="id"/>'s own Favourite state (`WP 10.6A`) —
    /// the real, shared implementation both the Project Explorer's own
    /// context menu and the <c>Ctrl+D</c> shortcut call through. Records
    /// a real Undo/Redo pair (`ADR-0099`) — trivially self-inverting,
    /// since toggling twice is a no-op.
    /// </summary>
    public void ToggleFavourite(Guid id, string kind, string displayName)
    {
        var wasFavourite = _favouriteObjects.IsFavourite(id);
        _favouriteObjects.Toggle(id, kind, displayName);
        _favouriteObjects.SaveAsync().GetAwaiter().GetResult();

        var message = wasFavourite ? $"Removed '{displayName}' from Favourites." : $"Added '{displayName}' to Favourites.";
        _statusBar.SetText(message);
        _toastHost.Show(message, FeedbackSeverity.Success);
        _recordHistory(message);

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
    }

    /// <summary>
    /// Closes <paramref name="viewId"/>'s own open document tab (`TD-40`,
    /// `WP 10.5A`) — a dirty Object Editor tab's own buffered, unsaved
    /// edits are no longer silently discarded, confirmed via the real
    /// <see cref="ConfirmationDialog"/> first. Cancelling leaves the tab
    /// open, with its edits intact, exactly as if Close had never been
    /// requested. Public — both <see cref="DocumentAreaView.TabCloseRequested"/>
    /// (wired inside <see cref="Attach"/>) and <see cref="MainWindow"/>'s
    /// own <c>Ctrl+W</c> keyboard shortcut call through this identical
    /// path.
    /// </summary>
    public async Task CloseDocumentAsync(Guid viewId)
    {
        if (_documentArea!.IsMarkedDirty(viewId))
        {
            var discard = await _confirmationDialog.ConfirmAsync(
                "Discard unsaved changes?",
                "This tab has unsaved edits. Closing it now will discard them permanently.",
                "Discard").ConfigureAwait(true);
            if (!discard)
                return;
        }

        await _workspace.Navigation.CloseAsync(viewId).ConfigureAwait(true);
        foreach (var rootId in _openGraphViewsByRootId.Where(kv => kv.Value.Id == viewId).Select(kv => kv.Key).ToList())
            _openGraphViewsByRootId.Remove(rootId);
        _documentArea.RemoveTab(viewId);
        _refreshCockpit();
    }
}
