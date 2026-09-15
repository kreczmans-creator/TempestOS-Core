using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.Workspace.Layout;

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

    /// <summary>
    /// Where each panel was docked immediately before it last floated —
    /// consulted when its floating window closes, or when "Show Panel"
    /// redocks it, so it goes back near where it came from rather than to a
    /// fixed edge every time (`WP 20.10D`, PO finding T4).
    /// </summary>
    private readonly Dictionary<Guid, DockAnchor> _lastDockedAnchor = [];

    private WorkspaceLayoutTree _tree = WorkspaceLayoutTree.Empty;
    private Guid? _draggingPanelId;
    private Point _dragOrigin;
    private bool _dragActive;

    /// <summary>Raised after any change to the arrangement.</summary>
    public event Action<WorkspaceLayoutTree>? LayoutChanged;

    /// <summary>
    /// Raised when a gesture completes with something worth telling the
    /// user in the status bar — a deliberate tear-out into a floating
    /// window, or an accidental miss that changed nothing (`WP 20.10D`).
    /// </summary>
    public event Action<string>? Announced;

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

        var position = e.GetPosition(Host);
        CompleteDrag(position, ToScreenPoint(position));
        CurrentDropTarget = null;
        DropTargetChanged?.Invoke(null);
    }

    /// <summary>
    /// <paramref name="hostPosition"/> (in <see cref="Host"/>'s own
    /// coordinates) as a real screen point, or <see langword="null"/> when
    /// <see cref="Host"/> is not attached to a real window — so a torn-out
    /// panel opens where the user actually dropped it (`WP 20.10D`, PO
    /// finding T4: the caller's own fallback previously used
    /// <see cref="Host"/>-local coordinates directly as screen pixels
    /// whenever this returned <see langword="null"/>, which was every time
    /// — nothing ever passed a real screen point in).
    /// </summary>
    /// <remarks>
    /// No public <c>PointToScreen</c> exists on <see cref="Visual"/> or
    /// <see cref="TopLevel"/> in this Avalonia version; this is the
    /// window's own screen <see cref="Window.Position"/> plus the point
    /// translated into the window and scaled by
    /// <see cref="TopLevel.RenderScaling"/> — exact for a borderless
    /// window, and close enough for placement purposes otherwise.
    /// </remarks>
    private PixelPoint? ToScreenPoint(Point hostPosition)
    {
        if (TopLevel.GetTopLevel(Host) is not Window window)
            return null;

        var pointInWindow = Host.TranslatePoint(hostPosition, window) ?? hostPosition;
        var scaling = window.RenderScaling;

        return new PixelPoint(
            window.Position.X + (int)Math.Round(pointInWindow.X * scaling),
            window.Position.Y + (int)Math.Round(pointInWindow.Y * scaling));
    }

    /// <summary>The drop target currently under the pointer during a drag, or <see langword="null"/>.</summary>
    public DockTarget? CurrentDropTarget { get; private set; }

    /// <summary>Raised as the drop target changes during a drag, so an overlay can highlight it.</summary>
    public event Action<DockTarget?>? DropTargetChanged;

    /// <summary>The main window's own layout surface.</summary>
    public WorkspaceLayoutHost Host { get; }

    /// <summary>
    /// The real shell window every floating window is owned by (`WP
    /// 20.10D`, PO finding T4) — so a floating window can never end up
    /// behind the main window with no way back, the way an un-owned
    /// top-level can on some window managers. Set once, by the composition
    /// root, once that window exists; <see langword="null"/> in a test that
    /// never sets it opens floating windows un-owned, exactly as before.
    /// </summary>
    public Window? OwnerWindow { get; set; }

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

            // `WP 20.10D`, PO finding T4: a floating window's saved or
            // dropped position must never be able to place it somewhere the
            // user can never see or reach — clamped before it is ever
            // shown, whether it was just created from a drag or restored
            // from a saved arrangement.
            window.ClampToScreen();

            window.GeometryChanged += (id, x, y, w, h) => Apply(t => t.MoveFloating(id, x, y, w, h));
            window.Host.LayoutChanged += tree => Adopt(tree, render: false);

            // Closing the OS window (the title bar's own close button) is
            // not "discard this panel" — its content goes back to where it
            // was docked before it floated (`WP 20.10D`).
            window.WindowClosed += HandleFloatingWindowClosed;

            _floatingWindows[model.Id] = window;

            // Owned by the shell, so it can never be left behind it with
            // nothing else naming it (`WP 20.10D`). Avalonia refuses
            // `Show(owner)` against a not-yet-visible owner ("Cannot show
            // window with non-visible owner"), which a test constructing
            // `MainWindow` without ever showing it can hit; falling back to
            // an un-owned `Show()` there is exactly the pre-`WP 20.10D`
            // behaviour, never a crash.
            if (OwnerWindow is { IsVisible: true } owner)
                window.Show(owner);
            else
                window.Show();

            // Owned and shown is not yet "in front" — the second half of
            // "never lost": a panel that just floated is exactly the one a
            // user is looking for right now.
            window.Activate();
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
    /// A floating window's own OS-level close (the title bar's close
    /// button, Alt+F4, the platform's own window-close gesture) redocks its
    /// content rather than discarding it (`WP 20.10D`, PO finding T4: a
    /// floating window that simply closed with nothing left naming its
    /// panel is exactly how one "disappeared somewhere and broken away").
    /// A no-op when the model is already gone from the tree — the ordinary
    /// case when this fires because <see cref="SyncFloatingWindows"/>
    /// itself just called <see cref="FloatingPanelWindow.Close"/> on an
    /// orphan (docked back in through the normal drag/redock path, or by
    /// <see cref="ResetTo"/>), which must never redock a second time.
    /// </summary>
    private void HandleFloatingWindowClosed(Guid windowId)
    {
        _floatingWindows.Remove(windowId);

        if (_tree.Floating.FirstOrDefault(f => f.Id == windowId) is not { } floatingModel)
            return;

        Apply(t => floatingModel.Content.Panels.Aggregate(t, RedockToLastPosition));
    }

    /// <summary>
    /// Docks <paramref name="panelId"/> back in from wherever it is
    /// floating, at the position remembered from before it floated, or a
    /// sensible edge when nothing was remembered — the "Show Panel"
    /// command's own half of "never lost" (`WP 20.10D`). A no-op when
    /// <paramref name="panelId"/> is not currently floating.
    /// </summary>
    public void RedockFloating(Guid panelId)
    {
        if (!_tree.IsFloating(panelId))
            return;

        Apply(t => RedockToLastPosition(t, panelId));
    }

    /// <summary>
    /// Returns to <paramref name="defaultTree"/>: every floating window
    /// closes, and any panel <paramref name="defaultTree"/> does not itself
    /// place — one registered after the default was fixed, a floating
    /// attachment viewer, say — is folded back into the docked tree instead
    /// of silently vanishing (`WP 20.10D`, PO finding T4: Reset Layout must
    /// be able to answer "where did my panel go" with "it's docked" for
    /// every panel that existed, not only the ones the preset happens to
    /// know about).
    /// </summary>
    public void ResetTo(WorkspaceLayoutTree defaultTree)
    {
        ArgumentNullException.ThrowIfNull(defaultTree);

        var extra = _tree.AllPanels.Except(defaultTree.AllPanels).ToList();
        var restored = extra.Aggregate(defaultTree, (t, panelId) => t.DockToEdge(panelId, DockRelation.Left));

        _lastDockedAnchor.Clear();
        Load(restored);
    }

    /// <summary>Where a panel was docked, expressed so it can be redocked later: a node that will still exist once the panel is removed, and which of the five zones to drop it back into.</summary>
    private readonly record struct DockAnchor(Guid TargetNodeId, DockRelation Relation);

    /// <summary>Records <paramref name="panelId"/>'s own current dock position as its <see cref="DockAnchor"/>, so a later float-then-close or float-then-redock can put it back close to where it came from.</summary>
    private void RememberDockAnchor(Guid panelId)
    {
        if (ComputeDockAnchor(_tree, panelId) is { } anchor)
            _lastDockedAnchor[panelId] = anchor;
    }

    /// <summary>
    /// Where <paramref name="panelId"/> sits right now, expressed as "dock
    /// it here, this way" against a node that survives its own removal — a
    /// sibling in its own tab group when it shares one, or a sibling in its
    /// immediate parent split when it does not.
    /// </summary>
    private static DockAnchor? ComputeDockAnchor(WorkspaceLayoutTree tree, Guid panelId)
    {
        if (tree.FindGroupContaining(panelId) is not { } group)
            return null;

        if (group.PanelIds.Count > 1)
            return new DockAnchor(group.Id, DockRelation.Into);

        if (FindParentSplit(tree.Root, group.Id) is not { } parentInfo)
            return null;

        var (parent, index) = parentInfo;
        var siblingIndex = index == 0 ? 1 : index - 1;
        if (siblingIndex < 0 || siblingIndex >= parent.Children.Count)
            return null;

        var sibling = parent.Children[siblingIndex];
        var horizontal = parent.Orientation == LayoutOrientation.Horizontal;
        var panelWasBefore = index < siblingIndex;

        var relation = horizontal
            ? panelWasBefore ? DockRelation.Left : DockRelation.Right
            : panelWasBefore ? DockRelation.Above : DockRelation.Below;

        return new DockAnchor(sibling.Id, relation);
    }

    /// <summary>The <see cref="LayoutSplitNode"/> that directly contains <paramref name="childId"/>, and its index among that split's own children — searched from <paramref name="node"/> down.</summary>
    private static (LayoutSplitNode Split, int Index)? FindParentSplit(WorkspaceLayoutNode? node, Guid childId)
    {
        if (node is not LayoutSplitNode split)
            return null;

        var index = split.Children.ToList().FindIndex(c => c.Id == childId);
        if (index >= 0)
            return (split, index);

        foreach (var child in split.Children)
        {
            if (FindParentSplit(child, childId) is { } found)
                return found;
        }

        return null;
    }

    /// <summary>Docks <paramref name="panelId"/> at its remembered <see cref="DockAnchor"/> when that node still exists, or a sensible edge otherwise — and forgets the anchor either way, since it is now stale.</summary>
    private WorkspaceLayoutTree RedockToLastPosition(WorkspaceLayoutTree tree, Guid panelId)
    {
        var redocked = _lastDockedAnchor.TryGetValue(panelId, out var anchor) && tree.FindNode(anchor.TargetNodeId) is not null
            ? tree.Dock(panelId, anchor.TargetNodeId, anchor.Relation)
            : tree.DockToEdge(panelId, DockRelation.Left);

        _lastDockedAnchor.Remove(panelId);
        return redocked;
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
    /// Starts a drag of <paramref name="panelId"/> as an already-past-the-
    /// threshold gesture — the model-level counterpart of a real mouse
    /// press followed by enough travel, needed because <see cref="UpdateDrag"/>
    /// and <see cref="CompleteDrag"/> are already public "the gesture, as an
    /// operation" seams (`WorkspaceLayoutControllerTests`'s own established
    /// convention of driving a gesture through the controller's public
    /// surface rather than synthesising raw pointer input) but nothing let a
    /// test start one from nothing (`WP 20.10D`).
    /// </summary>
    public void BeginDrag(Guid panelId)
    {
        _draggingPanelId = panelId;
        _dragOrigin = default;
        _dragActive = true;
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
    /// target under the pointer; tears the panel out into its own window
    /// when the pointer has left the workspace's own bounds entirely (the
    /// one deliberate "give this its own window" gesture); or, dropped
    /// over no target but still inside the workspace, changes nothing — an
    /// accidental miss is not a gesture (`WP 20.10D`, PO finding T4: every
    /// release outside every candidate used to float the panel, so a
    /// one-pixel miss in the gutter between two panes was
    /// indistinguishable from someone actually tearing it out).
    /// </summary>
    public void CompleteDrag(Point position, PixelPoint? screenPosition = null)
    {
        if (_draggingPanelId is not { } panelId || !_dragActive)
        {
            CancelDrag();
            return;
        }

        var target = DockTargetResolver.Resolve(CurrentCandidates(), position.X, position.Y);
        var title = _registry.Find(panelId)?.Title ?? "Panel";

        if (target is { } dock)
        {
            Apply(t => t.Dock(panelId, dock.NodeId, dock.Relation));
        }
        else if (IsOutsideWorkspace(position))
        {
            // Torn out past the edge of the workspace itself: undock into
            // its own window, at the point it was released.
            RememberDockAnchor(panelId);
            var origin = screenPosition ?? new PixelPoint((int)position.X, (int)position.Y);
            Apply(t => t.Float(panelId, origin.X, origin.Y, 420, 320));
            Announced?.Invoke($"{title} undocked into its own window.");
        }
        else
        {
            // Released inside the workspace but over no target: nothing
            // was ever mutated mid-drag, so the panel is already exactly
            // where it was — this just says so.
            Announced?.Invoke($"{title} stays where it was — drop it on a highlighted target to move it.");
        }

        CancelDrag();
    }

    /// <summary>Whether <paramref name="position"/> — in <see cref="Host"/>'s own coordinates — has left the workspace's own rendered bounds: the one deliberate tear-out gesture <see cref="CompleteDrag"/> honours.</summary>
    private bool IsOutsideWorkspace(Point position) =>
        position.X < 0 || position.Y < 0 || position.X > Host.Bounds.Width || position.Y > Host.Bounds.Height;

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
