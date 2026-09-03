using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.App.Workspace.Layout;

namespace Tempest.Desktop.Docking;

/// <summary>
/// Owns the workspace arrangement (`TD-72`): the one tree, the main
/// window's host, every floating window, drag-to-dock, and persistence.
/// </summary>
/// <remarks>
/// <para>
/// One owner, so there is one answer to "what is the layout". The host
/// renders, the floating windows render, the drag gesture proposes — but
/// only this class holds the tree and only this class applies an operation
/// to it. Everything else is derived and can be rebuilt from it at any
/// time, which is what makes the arrangement restorable, testable, and
/// impossible to get into a state the model cannot describe.
/// </para>
/// <para>
/// Persistence is debounced to the shutdown save the shell already
/// performs plus an explicit <see cref="SaveAsync"/>, rather than a write
/// per drag: a layout is session state, and writing it on every splitter
/// pixel would be noise.
/// </para>
/// </remarks>
public sealed class WorkspaceLayoutController
{
    /// <summary>How far the pointer must travel before a tab press becomes a drag.</summary>
    public const double DragThreshold = 6;

    private readonly WorkspacePanelRegistry _registry;
    private readonly IWorkspaceLayoutStore _store;
    private readonly Dictionary<Guid, FloatingPanelWindow> _floatingWindows = [];
    private readonly Func<FloatingLayoutWindow, FloatingPanelWindow>? _floatingWindowFactory;

    private WorkspaceLayoutTree _tree = WorkspaceLayoutTree.Empty;
    private Guid? _draggingPanelId;
    private Point _dragOrigin;
    private bool _dragActive;

    /// <summary>Raised after any change to the arrangement.</summary>
    public event Action<WorkspaceLayoutTree>? LayoutChanged;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceLayoutController"/> class.</summary>
    /// <param name="registry">The panels that can participate.</param>
    /// <param name="store">Where the arrangement is persisted.</param>
    /// <param name="floatingWindowFactory">
    /// Creates the window for a floating panel. Injected so a headless test
    /// can observe undocking without opening a real top-level window;
    /// production passes <see langword="null"/> and gets real windows.
    /// </param>
    public WorkspaceLayoutController(
        WorkspacePanelRegistry registry,
        IWorkspaceLayoutStore store,
        Func<FloatingLayoutWindow, FloatingPanelWindow>? floatingWindowFactory = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(store);

        _registry = registry;
        _store = store;
        _floatingWindowFactory = floatingWindowFactory;

        Host = new WorkspaceLayoutHost(registry);
        Host.LayoutChanged += tree => Adopt(tree, render: false);
        Host.PanelDragStarted += BeginDrag;

        // The drag is tracked on the host rather than on each tab, so
        // moving off the tab it started on — which is the whole point of
        // dragging — does not end the gesture.
        Host.AddHandler(InputElement.PointerMovedEvent, OnHostPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Host.AddHandler(InputElement.PointerReleasedEvent, OnHostPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Host.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => CancelDrag(), Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnHostPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingPanelId is null)
            return;

        CurrentDropTarget = UpdateDrag(e.GetPosition(Host));
        DropTargetChanged?.Invoke(CurrentDropTarget);
    }

    private void OnHostPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingPanelId is null)
            return;

        CompleteDrag(e.GetPosition(Host));
        CurrentDropTarget = null;
        DropTargetChanged?.Invoke(null);
    }

    /// <summary>The drop target currently under the pointer during a drag, or <see langword="null"/>.</summary>
    public DockTarget? CurrentDropTarget { get; private set; }

    /// <summary>Raised as the drop target changes during a drag, so an overlay can highlight it.</summary>
    public event Action<DockTarget?>? DropTargetChanged;

    /// <summary>The main window's own layout surface.</summary>
    public WorkspaceLayoutHost Host { get; }

    /// <summary>The current arrangement.</summary>
    public WorkspaceLayoutTree Tree => _tree;

    /// <summary>Every floating window currently open, by its own layout id.</summary>
    public IReadOnlyDictionary<Guid, FloatingPanelWindow> FloatingWindows => _floatingWindows;

    /// <summary>Whether a panel drag is currently in progress.</summary>
    public bool IsDragging => _dragActive;

    /// <summary>The panel being dragged, or <see langword="null"/>.</summary>
    public Guid? DraggingPanelId => _dragActive ? _draggingPanelId : null;

    /// <summary>Replaces the arrangement wholesale and re-renders — used on startup and by "reset layout".</summary>
    public void Load(WorkspaceLayoutTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        Adopt(tree, render: true);
    }

    /// <summary>Applies <paramref name="operation"/> to the arrangement and re-renders.</summary>
    public void Apply(Func<WorkspaceLayoutTree, WorkspaceLayoutTree> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var updated = operation(_tree);
        if (updated != _tree)
            Adopt(updated, render: true);
    }

    private void Adopt(WorkspaceLayoutTree tree, bool render)
    {
        _tree = tree;

        if (render)
            Host.Update(tree);

        SyncFloatingWindows();
        LayoutChanged?.Invoke(tree);
    }

    /// <summary>Opens, updates and closes floating windows so they match the model exactly.</summary>
    private void SyncFloatingWindows()
    {
        foreach (var model in _tree.Floating)
        {
            if (_floatingWindows.TryGetValue(model.Id, out var existing))
            {
                existing.LayoutPanels = _tree.Panels;
                existing.Update(model);
                continue;
            }

            var window = _floatingWindowFactory?.Invoke(model) ?? new FloatingPanelWindow(model, _registry);
            window.LayoutPanels = _tree.Panels;
            window.Update(model);
            window.GeometryChanged += (id, x, y, w, h) => Apply(t => t.MoveFloating(id, x, y, w, h));
            window.Host.LayoutChanged += tree => Adopt(tree, render: false);

            _floatingWindows[model.Id] = window;
            window.Show();
        }

        // A window whose panels have all gone back to the docked tree is a
        // window that should no longer exist.
        foreach (var orphan in _floatingWindows.Keys.Where(id => _tree.Floating.All(f => f.Id != id)).ToList())
        {
            var window = _floatingWindows[orphan];
            _floatingWindows.Remove(orphan);
            window.Close();
        }
    }

    /// <summary>
    /// Shows or hides <paramref name="panelId"/> — the View menu's own
    /// per-panel toggle.
    /// </summary>
    /// <remarks>
    /// "Hidden" now means "not in the arrangement" rather than "docked with
    /// zero width", so showing a panel again has to put it somewhere:
    /// <paramref name="restoreEdge"/> is where it goes when it has no
    /// remembered place.
    /// </remarks>
    public void TogglePanel(Guid panelId, DockRelation restoreEdge)
    {
        Apply(t => t.Contains(panelId)
            ? t.Remove(panelId)
            : t.DockToEdge(panelId, restoreEdge));
    }

    /// <summary>Whether <paramref name="panelId"/> is currently in the arrangement at all.</summary>
    public bool IsPanelVisible(Guid panelId) => _tree.Contains(panelId);

    // ----------------------------------------------------------------
    // Drag to dock
    // ----------------------------------------------------------------

    private void BeginDrag(Guid panelId, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Host).Properties.IsLeftButtonPressed)
            return;

        _draggingPanelId = panelId;
        _dragOrigin = e.GetPosition(Host);
        _dragActive = false;
    }

    /// <summary>
    /// Advances an in-progress drag. Returns the drop target the pointer is
    /// currently over, so an overlay can highlight it.
    /// </summary>
    public DockTarget? UpdateDrag(Point position)
    {
        if (_draggingPanelId is null)
            return null;

        // A press is not a drag until the pointer has actually travelled:
        // otherwise every tab click would re-dock the panel it selected.
        if (!_dragActive)
        {
            var travelled = Math.Abs(position.X - _dragOrigin.X) + Math.Abs(position.Y - _dragOrigin.Y);
            if (travelled < DragThreshold)
                return null;

            _dragActive = true;
        }

        return DockTargetResolver.Resolve(CurrentCandidates(), position.X, position.Y);
    }

    /// <summary>
    /// Completes a drag at <paramref name="position"/>: docks onto the
    /// target under the pointer, or — when the pointer is outside the host
    /// entirely — undocks the panel into its own window.
    /// </summary>
    public void CompleteDrag(Point position, PixelPoint? screenPosition = null)
    {
        if (_draggingPanelId is not { } panelId || !_dragActive)
        {
            CancelDrag();
            return;
        }

        var target = DockTargetResolver.Resolve(CurrentCandidates(), position.X, position.Y);

        if (target is { } dock)
        {
            Apply(t => t.Dock(panelId, dock.NodeId, dock.Relation));
        }
        else
        {
            // Dropped outside every pane: the gesture that means "undock
            // this into its own window", at the point it was released.
            var origin = screenPosition ?? new PixelPoint((int)position.X, (int)position.Y);
            Apply(t => t.Float(panelId, origin.X, origin.Y, 420, 320));
        }

        CancelDrag();
    }

    /// <summary>Abandons any in-progress drag without changing the arrangement.</summary>
    public void CancelDrag()
    {
        _draggingPanelId = null;
        _dragActive = false;
    }

    /// <summary>The drop candidates currently on screen, in the host's own coordinates.</summary>
    public IReadOnlyList<DockTargetCandidate> CurrentCandidates()
    {
        var candidates = new List<DockTargetCandidate>();

        foreach (var group in Host.TabGroups)
        {
            if (group.GetVisualRoot() is null)
                continue;

            var origin = group.TranslatePoint(default, Host);
            if (origin is not { } point)
                continue;

            candidates.Add(new DockTargetCandidate(group.NodeId, point.X, point.Y, group.Bounds.Width, group.Bounds.Height));
        }

        return candidates;
    }

    // ----------------------------------------------------------------
    // Persistence
    // ----------------------------------------------------------------

    /// <summary>Writes the arrangement for the next session.</summary>
    public Task SaveAsync(CancellationToken cancellationToken = default) =>
        _store.SaveAsync(_tree, cancellationToken);

    /// <summary>
    /// Restores the saved arrangement, falling back to
    /// <paramref name="fallback"/> when nothing was saved or what was saved
    /// is unreadable, and dropping any panel that is no longer registered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The continuation is marshalled back to the UI thread before any
    /// visual work.</b> <see cref="IWorkspaceLayoutStore.LoadAsync"/> reaches
    /// <c>Tempest.Core</c>'s own settings substrate, whose async methods
    /// <c>ConfigureAwait(false)</c> internally — so once that read genuinely
    /// completes asynchronously (reliably the case on Windows), the
    /// continuation here resumes on a thread-pool thread, not the Avalonia
    /// UI thread. <see cref="Load"/> synchronously drives the visual tree
    /// (<c>Adopt</c> → <see cref="WorkspaceLayoutHost.Update"/> →
    /// <see cref="WorkspaceLayoutHost.HideFlyout"/> → <c>Visual.IsVisible</c>),
    /// and every <see cref="AvaloniaObject"/> read there calls
    /// <see cref="Dispatcher.VerifyAccess"/>. Off the UI thread that throws
    /// <see cref="InvalidOperationException"/> ("Call from invalid thread"),
    /// and because the one caller awaits this from an <c>async void</c>
    /// <see cref="Window.Opened"/> handler the throw was unhandled and killed
    /// the process moments after the window appeared.
    /// </para>
    /// <para>
    /// <c>ConfigureAwait(false)</c> is deliberately <b>kept</b> on the Core
    /// read — that call has no UI affinity and should not pay for a context
    /// capture. Only <see cref="Load"/> is marshalled, using the same
    /// <c>CheckAccess</c>/<c>Invoke</c> shape
    /// <see cref="Tempest.Desktop.Theming.ThemeService"/> already established
    /// for this identical Core-async/UI-thread boundary. Tree construction
    /// (<see cref="DropUnknownPanels"/>) touches no UI state and stays off
    /// the UI thread.
    /// </para>
    /// </remarks>
    public async Task RestoreAsync(WorkspaceLayoutTree fallback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fallback);

        var saved = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var tree = saved is null ? fallback : DropUnknownPanels(saved, fallback);

        if (Dispatcher.UIThread.CheckAccess())
            Load(tree);
        else
            Dispatcher.UIThread.Invoke(() => Load(tree));
    }

    /// <summary>
    /// Removes panels the saved layout names but this build no longer
    /// registers, so an arrangement saved by an older version still opens.
    /// </summary>
    private WorkspaceLayoutTree DropUnknownPanels(WorkspaceLayoutTree saved, WorkspaceLayoutTree fallback)
    {
        var unknown = saved.AllPanels.Where(p => !_registry.Contains(p)).ToList();
        var pruned = unknown.Aggregate(saved, (tree, panelId) => tree.Remove(panelId));

        // A layout that pruned down to nothing is not a layout.
        return pruned.Root is null && pruned.Floating.Count == 0 ? fallback : pruned;
    }
}
