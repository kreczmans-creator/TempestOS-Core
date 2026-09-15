using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.Workspace.Layout;

namespace Tempest.Desktop.Docking;

/// <summary>
/// Owns the workspace arrangement (`TD-72`, generalised to many windows by
/// `ADR-0153`): the one forest, the primary window's own host, every
/// secondary window, cross-window drag-to-dock, focus, and persistence.
/// </summary>
/// <remarks>
/// <para>
/// One owner, so there is one answer to "what is the layout" — restated for
/// N windows rather than one-plus-floating (`ADR-0153` decision 1). The
/// hosts render, the secondary windows render, the drag gesture proposes —
/// but only this class holds the tree and only this class applies an
/// operation to it. Everything else is derived and can be rebuilt from it
/// at any time, which is what makes the arrangement restorable, testable,
/// and impossible to get into a state the model cannot describe.
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
    private readonly Func<IScreenList>? _screenListProvider;

    /// <summary>
    /// Where each panel was docked immediately before it last floated —
    /// consulted when its floating window closes, or when "Show Panel"
    /// redocks it, so it goes back near where it came from rather than to a
    /// fixed edge every time (`WP 20.10D`, PO finding T4).
    /// </summary>
    private readonly Dictionary<Guid, DockAnchor> _lastDockedAnchor = [];

    private WorkspaceLayoutTree _tree = WorkspaceLayoutTree.Empty;
    private Guid? _draggingPanelId;
    private WorkspaceLayoutHost? _dragOriginHost;
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
    /// Creates the window for a secondary window entry. Injected so a
    /// headless test can observe undocking without opening a real top-level
    /// window; production passes <see langword="null"/> and gets real
    /// windows.
    /// </param>
    /// <param name="screenListProvider">
    /// Supplies the screens persistence resolves a window's own monitor
    /// against (`ADR-0153` decision 5). Injected so a test can prove
    /// monitor-fallback and per-monitor DPI conversion without a real
    /// multi-monitor rig — Avalonia's own headless platform reports exactly
    /// one screen (the ADR's own risk 3). Production passes
    /// <see langword="null"/> and gets the real screens of whichever window
    /// is available (<see cref="OwnerWindow"/>, or else <see cref="Host"/>'s
    /// own top level) at the moment persistence runs.
    /// </param>
    public WorkspaceLayoutController(
        WorkspacePanelRegistry registry,
        IWorkspaceLayoutStore store,
        Func<FloatingLayoutWindow, FloatingPanelWindow>? floatingWindowFactory = null,
        Func<IScreenList>? screenListProvider = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(store);

        _registry = registry;
        _store = store;
        _floatingWindowFactory = floatingWindowFactory;
        _screenListProvider = screenListProvider;

        Host = new WorkspaceLayoutHost(registry);
        Host.LayoutChanged += tree => Adopt(tree, render: false);
        WireHost(Host);
    }

    /// <summary>
    /// Wires one window's own host into the shared drag machinery — the
    /// primary <see cref="Host"/>, in the constructor, and every secondary
    /// window's own host as it is created by <see cref="SyncFloatingWindows"/>
    /// — so a drag can start from, and a drop can resolve against, any
    /// window (`ADR-0153` decision 4).
    /// </summary>
    private void WireHost(WorkspaceLayoutHost host)
    {
        host.PanelDragStarted += (panelId, e) => BeginDrag(panelId, host, e);

        // `WP 19.2B` (`TD-133`) and `ADR-0153` decision 8: the three
        // keyboard gestures a focused tab header raises, applied here —
        // the one canonical `Apply`, so each one also gets decision 7's
        // focus restore, which the header that was operated needs more
        // than any mouse gesture does (the re-render destroys it).
        host.PanelMoveRequested += (panelId, edge) => Apply(t => t.DockToEdge(panelId, edge));
        host.PanelResizeRequested += (panelId, delta) => Apply(t => t.ResizeSplit(panelId, delta));
        host.PanelReorderRequested += (groupId, panelId, direction) => Apply(t => t.ReorderTab(groupId, panelId, direction));

        // The drag is tracked on the host rather than on each tab, so
        // moving off the tab it started on — which is the whole point of
        // dragging — does not end the gesture.
        host.AddHandler(InputElement.PointerMovedEvent, (object? _, PointerEventArgs e) => OnPointerMoved(host, e), RoutingStrategies.Tunnel);
        host.AddHandler(InputElement.PointerReleasedEvent, (object? _, PointerReleasedEventArgs e) => OnPointerReleased(host, e), RoutingStrategies.Tunnel);

        // `ADR-0153` decision 9: `PointerCaptureLostEvent` is declared
        // `RoutingStrategies.Direct` by Avalonia 11.3.20 (confirmed by
        // reflection, and by `WorkspaceLayoutHost`'s own neighbouring,
        // already-correct handler) — a handler registered for a routing
        // strategy the event never uses is never invoked, so registering
        // `Tunnel` here (as this class did before this package) meant
        // `CancelDrag()` likely never ran on a real OS-forced capture loss.
        host.AddHandler(InputElement.PointerCaptureLostEvent, (object? _, RoutedEventArgs _) => CancelDrag(), RoutingStrategies.Direct);
    }

    private void OnPointerMoved(WorkspaceLayoutHost host, PointerEventArgs e)
    {
        // `ADR-0153` decision 4's own resolved risk: pointer capture is per
        // top-level window, so once a drag starts, only the window that
        // holds capture is trusted to report the drag's own position —
        // never a different window's independently-raised pointer event.
        if (_draggingPanelId is null || !ReferenceEquals(host, _dragOriginHost))
            return;

        CurrentDropTarget = UpdateDrag(e.GetPosition(host));
        DropTargetChanged?.Invoke(CurrentDropTarget);
    }

    private void OnPointerReleased(WorkspaceLayoutHost host, PointerReleasedEventArgs e)
    {
        if (_draggingPanelId is null || !ReferenceEquals(host, _dragOriginHost))
            return;

        var position = e.GetPosition(host);
        CompleteDrag(position, ToScreenPoint(host, position));
        CurrentDropTarget = null;
        DropTargetChanged?.Invoke(null);
    }

    /// <summary>
    /// <paramref name="localPoint"/>, in <paramref name="control"/>'s own
    /// coordinates, as a real screen point, or <see langword="null"/> when
    /// <paramref name="control"/> is not attached to a real window.
    /// </summary>
    /// <remarks>
    /// `ADR-0153` decision 4 names <see cref="Visual.PointToScreen"/>
    /// directly: contrary to this class's own previous doc comment here (a
    /// hand-rolled <c>Position</c>-plus-<c>RenderScaling</c> workaround,
    /// dated `WP 20.10D`), the method already exists as public API on the
    /// Avalonia 11.3.20 this solution references — confirmed directly
    /// against the referenced package during this Work Package, not
    /// assumed. It gives every candidate, in every window, a common
    /// coordinate space to compare a drag's current position against,
    /// with no per-window scaling arithmetic of this class's own to get
    /// wrong.
    /// </remarks>
    private static PixelPoint? ToScreenPoint(Visual control, Point localPoint) =>
        TopLevel.GetTopLevel(control) is Window ? control.PointToScreen(localPoint) : null;

    /// <summary>The drop target currently under the pointer during a drag, or <see langword="null"/>.</summary>
    public DockTarget? CurrentDropTarget { get; private set; }

    /// <summary>Raised as the drop target changes during a drag, so an overlay can highlight it.</summary>
    public event Action<DockTarget?>? DropTargetChanged;

    /// <summary>The primary window's own layout surface.</summary>
    public WorkspaceLayoutHost Host { get; }

    /// <summary>
    /// The real shell window every secondary window is owned by (`WP
    /// 20.10D`, PO finding T4) — so a floating window can never end up
    /// behind the main window with no way back, the way an un-owned
    /// top-level can on some window managers, and the window whose own
    /// <see cref="Avalonia.Controls.Screens"/> persistence resolves a
    /// monitor against when no test has injected one (`ADR-0153` decision
    /// 5). Set once, by the composition root, once that window exists;
    /// <see langword="null"/> in a test that never sets it opens floating
    /// windows un-owned, exactly as before.
    /// </summary>
    public Window? OwnerWindow { get; set; }

    /// <summary>The current arrangement.</summary>
    public WorkspaceLayoutTree Tree => _tree;

    /// <summary>Every secondary window currently open, by its own layout id.</summary>
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
        if (updated == _tree)
            return;

        // `ADR-0153` decision 7, closing `TD-90`: which panel held keyboard
        // focus is captured before the operation runs, and restored to
        // that panel's own new tab header afterwards — by panel id, not by
        // control identity, since every `LayoutTabGroupView` is rebuilt by
        // this same re-render and cannot be matched by reference.
        var focusedPanelId = CaptureFocusedPanelId();
        Adopt(updated, render: true);

        // Posted rather than called inline: the re-render just rebuilt the
        // panel's own new tab header as a brand-new control, and Avalonia
        // has not yet run the layout pass that attaches it to a real input
        // root — `Control.Focus()` called before that pass silently
        // returns `false`. `DispatcherPriority.Loaded` is the standard
        // "after layout, rendering and data binding have settled" point.
        if (focusedPanelId is { } id)
            Dispatcher.UIThread.Post(() => RestoreFocus(id), DispatcherPriority.Loaded);
    }

    private void Adopt(WorkspaceLayoutTree tree, bool render)
    {
        _tree = tree;

        if (render)
            Host.Update(tree);

        SyncFloatingWindows();
        LayoutChanged?.Invoke(tree);
    }

    /// <summary>Opens, updates and closes secondary windows so they match the model exactly.</summary>
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

            // `ADR-0153` decision 4: every window's own host joins the same
            // drag machinery the primary one already has, so a drag can
            // both start from, and be dropped onto, a secondary window.
            WireHost(window.Host);

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
    /// Returns to <paramref name="defaultTree"/>: every secondary window
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

        if (tree.FindWindowContaining(group.Id) is not { Root: { } windowRoot })
            return null;

        if (FindParentSplit(windowRoot, group.Id) is not { } parentInfo)
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
    // Focus restore (`ADR-0153` decision 7, `TD-90`)
    // ----------------------------------------------------------------

    /// <summary>
    /// The panel whose tab header, or whose selected content, currently
    /// holds keyboard focus in any known window — captured before an
    /// operation runs so it can be restored to that panel's own new home
    /// afterwards.
    /// </summary>
    private Guid? CaptureFocusedPanelId()
    {
        foreach (var host in AllHosts())
        {
            if (TopLevel.GetTopLevel(host)?.FocusManager?.GetFocusedElement() is not Control focused)
                continue;

            foreach (var group in host.TabGroups)
            {
                foreach (var panelId in group.PanelIds)
                {
                    if (OwnsFocus(group, panelId, focused))
                        return panelId;
                }
            }
        }

        return null;
    }

    private bool OwnsFocus(LayoutTabGroupView group, Guid panelId, Control focused)
    {
        if (group.FindHeader(panelId) is { } header && ReferenceEquals(header, focused))
            return true;

        // Only the selected tab's own content is actually reachable, so
        // only it is worth checking — an unselected tab's content is
        // detached and cannot itself hold focus.
        if (panelId != group.SelectedPanelId || _registry.Find(panelId) is not { } descriptor)
            return false;

        return ReferenceEquals(descriptor.Content, focused) || focused.GetVisualAncestors().Contains(descriptor.Content);
    }

    /// <summary>
    /// Focuses <paramref name="id"/>'s own tab header, in whichever window
    /// it now lives — activating that window first when it is not already
    /// the one with input focus, since a focused control in a background
    /// window is invisible to the keyboard until the window is
    /// (`ADR-0153` decision 7). A no-op when <paramref name="id"/> is no
    /// longer anywhere in the arrangement by the time this runs.
    /// </summary>
    private void RestoreFocus(Guid id)
    {
        foreach (var host in AllHosts())
        {
            if (host.FindPanelHeader(id) is not { } header)
                continue;

            if (TopLevel.GetTopLevel(host) is Window { IsActive: false } window)
                window.Activate();

            header.Focus();
            return;
        }
    }

    /// <summary>Every window's own host currently known to this controller — the primary one first, then every secondary window, in no particular further order.</summary>
    private IEnumerable<WorkspaceLayoutHost> AllHosts()
    {
        yield return Host;

        foreach (var window in _floatingWindows.Values)
            yield return window.Host;
    }

    // ----------------------------------------------------------------
    // Drag to dock
    // ----------------------------------------------------------------

    private void BeginDrag(Guid panelId, WorkspaceLayoutHost originHost, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(originHost).Properties.IsLeftButtonPressed)
            return;

        _draggingPanelId = panelId;
        _dragOriginHost = originHost;
        _dragOrigin = e.GetPosition(originHost);
        _dragActive = false;
    }

    /// <summary>
    /// Starts a drag of <paramref name="panelId"/>, from the primary
    /// window, as an already-past-the-threshold gesture — the model-level
    /// counterpart of a real mouse press followed by enough travel, needed
    /// because <see cref="UpdateDrag"/> and <see cref="CompleteDrag"/> are
    /// already public "the gesture, as an operation" seams
    /// (`WorkspaceLayoutControllerTests`'s own established convention of
    /// driving a gesture through the controller's public surface rather
    /// than synthesising raw pointer input) but nothing let a test start
    /// one from nothing (`WP 20.10D`).
    /// </summary>
    public void BeginDrag(Guid panelId) => BeginDrag(panelId, Host);

    /// <summary>
    /// <see cref="BeginDrag(Guid)"/>, naming which window's own host the
    /// drag originates from — the test seam a cross-window drag needs
    /// (`ADR-0153` decision 4): production always starts a drag from
    /// whichever host the pointer was actually pressed in, via the real
    /// gesture wired by <see cref="WireHost"/>.
    /// </summary>
    public void BeginDrag(Guid panelId, WorkspaceLayoutHost originHost)
    {
        ArgumentNullException.ThrowIfNull(originHost);

        _draggingPanelId = panelId;
        _dragOriginHost = originHost;
        _dragOrigin = default;
        _dragActive = true;
    }

    /// <summary>
    /// Advances an in-progress drag. Returns the drop target the pointer is
    /// currently over, so an overlay can highlight it.
    /// </summary>
    /// <param name="position">The pointer's current position, in the drag's own origin window's coordinates (see <see cref="BeginDrag(Guid,WorkspaceLayoutHost)"/>).</param>
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

        var originHost = _dragOriginHost ?? Host;

        var local = DockTargetResolver.Resolve(CandidatesOf(originHost), position.X, position.Y);
        if (local is not null)
            return local;

        // `ADR-0153` decision 4: past the origin window's own candidates,
        // a screen-space search against every other known window's own
        // candidates gets first refusal before this falls back to
        // "outside every window entirely" (`CompleteDrag`'s own float
        // gesture).
        return ToScreenPoint(originHost, position) is { } screenPoint
            ? ResolveCrossWindow(screenPoint, originHost)
            : null;
    }

    /// <summary>
    /// Completes a drag at <paramref name="position"/>: docks onto the
    /// target under the pointer — in the origin window, or, past its own
    /// candidates, in any other known window (`ADR-0153` decision 4);
    /// tears the panel out into its own new window when the pointer has
    /// left every known window's own bounds entirely (the one deliberate
    /// "give this its own window" gesture); or, dropped over no target but
    /// still inside the origin window, changes nothing — an accidental
    /// miss is not a gesture (`WP 20.10D`, PO finding T4: every release
    /// outside every candidate used to float the panel, so a one-pixel
    /// miss in the gutter between two panes was indistinguishable from
    /// someone actually tearing it out).
    /// </summary>
    /// <param name="position">Where the pointer was released, in the drag's own origin window's coordinates.</param>
    /// <param name="screenPosition">The same release point in real screen coordinates, when known — used only for a fresh tear-out's own placement.</param>
    public void CompleteDrag(Point position, PixelPoint? screenPosition = null)
    {
        if (_draggingPanelId is not { } panelId || !_dragActive)
        {
            CancelDrag();
            return;
        }

        var originHost = _dragOriginHost ?? Host;
        var title = _registry.Find(panelId)?.Title ?? "Panel";

        var target = DockTargetResolver.Resolve(CandidatesOf(originHost), position.X, position.Y);
        var screenPoint = screenPosition ?? ToScreenPoint(originHost, position);

        if (target is null && screenPoint is { } sp)
            target = ResolveCrossWindow(sp, originHost);

        if (target is { } dock)
        {
            Apply(t => t.Dock(panelId, dock.NodeId, dock.Relation));
        }
        else if (IsOutsideEveryWindow(originHost, position, screenPoint))
        {
            // Torn out past the edge of every known window: undock into
            // its own new window, at the point it was released.
            RememberDockAnchor(panelId);
            var origin = screenPoint ?? new PixelPoint((int)position.X, (int)position.Y);
            Apply(t => t.Float(panelId, origin.X, origin.Y, 420, 320));
            Announced?.Invoke($"{title} undocked into its own window.");
        }
        else
        {
            // Released inside a known window but over no target: nothing
            // was ever mutated mid-drag, so the panel is already exactly
            // where it was — this just says so.
            Announced?.Invoke($"{title} stays where it was — drop it on a highlighted target to move it.");
        }

        CancelDrag();
    }

    /// <summary>
    /// Whether <paramref name="position"/> — in <paramref name="originHost"/>'s
    /// own coordinates — has left every known window's own rendered bounds
    /// entirely: the one deliberate tear-out gesture <see cref="CompleteDrag"/>
    /// honours. A release outside the origin window's own bounds but still
    /// inside a <em>different</em> known window's own screen rectangle is
    /// not a tear-out — it is a cross-window release over no candidate,
    /// which <see cref="CompleteDrag"/> already reads as "stays where it
    /// was".
    /// </summary>
    private bool IsOutsideEveryWindow(WorkspaceLayoutHost originHost, Point position, PixelPoint? screenPoint)
    {
        if (position.X >= 0 && position.Y >= 0 && position.X <= originHost.Bounds.Width && position.Y <= originHost.Bounds.Height)
            return false;

        if (screenPoint is not { } point)
            return true;

        foreach (var host in AllHosts())
        {
            if (ReferenceEquals(host, originHost))
                continue;

            if (ToScreenPoint(host, default) is not { } topLeft)
                continue;

            var bounds = new PixelRect(topLeft.X, topLeft.Y, (int)Math.Max(0, host.Bounds.Width), (int)Math.Max(0, host.Bounds.Height));
            if (bounds.Contains(point))
                return false;
        }

        return true;
    }

    /// <summary>Abandons any in-progress drag without changing the arrangement.</summary>
    public void CancelDrag()
    {
        _draggingPanelId = null;
        _dragOriginHost = null;
        _dragActive = false;
    }

    /// <summary>The drop candidates currently on screen in the primary window, in that window's own coordinates.</summary>
    public IReadOnlyList<DockTargetCandidate> CurrentCandidates() => CandidatesOf(Host);

    private static IReadOnlyList<DockTargetCandidate> CandidatesOf(WorkspaceLayoutHost host)
    {
        var candidates = new List<DockTargetCandidate>();

        foreach (var group in host.TabGroups)
        {
            if (group.GetVisualRoot() is null)
                continue;

            var origin = group.TranslatePoint(default, host);
            if (origin is not { } point)
                continue;

            candidates.Add(new DockTargetCandidate(group.NodeId, point.X, point.Y, group.Bounds.Width, group.Bounds.Height));
        }

        return candidates;
    }

    /// <summary>
    /// The drop target under <paramref name="screenPosition"/>, in any
    /// known window other than <paramref name="originHost"/>'s own —
    /// `ADR-0153` decision 4's cross-window resolution, entirely in screen
    /// coordinates (via <see cref="Visual.PointToScreen"/>) so a candidate
    /// in a different top-level window compares against the drag's own
    /// current position on equal terms.
    /// </summary>
    private DockTarget? ResolveCrossWindow(PixelPoint screenPosition, WorkspaceLayoutHost originHost)
    {
        foreach (var host in AllHosts())
        {
            if (ReferenceEquals(host, originHost))
                continue;

            if (DockTargetResolver.Resolve(ScreenCandidatesOf(host), screenPosition.X, screenPosition.Y) is { } target)
                return target;
        }

        return null;
    }

    /// <summary>Every candidate pane <paramref name="host"/> currently renders, translated into screen coordinates.</summary>
    private static IReadOnlyList<DockTargetCandidate> ScreenCandidatesOf(WorkspaceLayoutHost host)
    {
        var candidates = new List<DockTargetCandidate>();

        foreach (var group in host.TabGroups)
        {
            if (group.GetVisualRoot() is null)
                continue;

            if (ToScreenPoint(group, default) is not { } topLeft)
                continue;

            if (ToScreenPoint(group, new Point(group.Bounds.Width, group.Bounds.Height)) is not { } bottomRight)
                continue;

            candidates.Add(new DockTargetCandidate(group.NodeId, topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y));
        }

        return candidates;
    }

    // ----------------------------------------------------------------
    // Persistence
    // ----------------------------------------------------------------

    /// <summary>Writes the arrangement for the next session.</summary>
    public Task SaveAsync(CancellationToken cancellationToken = default) =>
        _store.SaveAsync(ToPersistedShape(_tree), cancellationToken);

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
    /// (<see cref="DropUnknownPanels"/>, <see cref="FromPersistedShape"/>)
    /// touches no UI state and stays off the UI thread.
    /// </para>
    /// </remarks>
    public async Task RestoreAsync(WorkspaceLayoutTree fallback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fallback);

        var saved = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var tree = saved is null ? fallback : DropUnknownPanels(saved, fallback);
        tree = FromPersistedShape(tree);

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

    /// <summary>
    /// <paramref name="tree"/>, with every secondary window's own geometry
    /// converted from this session's live, absolute terms into the
    /// monitor-relative, DPI-normalised terms `ADR-0153` decision 5
    /// persists — resolved against whichever screen that window's own
    /// current position actually overlaps, not merely copied from the
    /// model, so a drag still settling when the shell shuts down is saved
    /// against where the window really is.
    /// </summary>
    private WorkspaceLayoutTree ToPersistedShape(WorkspaceLayoutTree tree)
    {
        var screens = ResolveScreenList();

        var windows = tree.Windows.Select(window =>
        {
            if (window.IsPrimary)
                return window;

            var (position, widthDip, heightDip) = _floatingWindows.TryGetValue(window.Id, out var live)
                ? (live.Position, live.Width, live.Height)
                : (new PixelPoint((int)window.X, (int)window.Y), window.Width, window.Height);

            var bounds = new PixelRect(position.X, position.Y, (int)Math.Max(0, widthDip), (int)Math.Max(0, heightDip));
            var screen = MonitorRelativePlacement.ResolveCurrentScreen(bounds, screens);
            var (key, x, y, width, height) = MonitorRelativePlacement.ToRelative(position, widthDip, heightDip, screen);

            return window with { MonitorKey = key, X = x, Y = y, Width = width, Height = height };
        }).ToList();

        return tree with { Windows = windows };
    }

    /// <summary>
    /// The reverse of <see cref="ToPersistedShape"/>: <paramref name="tree"/>,
    /// with every secondary window's own saved, monitor-relative geometry
    /// resolved back into this session's live, absolute terms — against
    /// the screen its own saved <c>MonitorKey</c> names, or the primary
    /// screen when that monitor is not currently attached (`ADR-0153`
    /// decision 5's own named fallback). A window with no
    /// <c>MonitorKey</c> at all (a version-1 document, migrated by
    /// <see cref="Tempest.Workspace.Layout.WorkspaceLayoutSerializer"/>
    /// into a forest that never recorded one) is left exactly as read —
    /// already in absolute terms, on the pre-`ADR-0153` convention.
    /// </summary>
    private WorkspaceLayoutTree FromPersistedShape(WorkspaceLayoutTree tree)
    {
        var screens = ResolveScreenList();

        var windows = tree.Windows.Select(window =>
        {
            if (window.IsPrimary || window.MonitorKey is null)
                return window;

            var screen = MonitorRelativePlacement.ResolveSavedScreen(window.MonitorKey, screens);
            var (position, widthDip, heightDip) = MonitorRelativePlacement.ToAbsolute(window.X, window.Y, window.Width, window.Height, screen);

            return window with { MonitorKey = null, X = position.X, Y = position.Y, Width = widthDip, Height = heightDip };
        }).ToList();

        return tree with { Windows = windows };
    }

    private IScreenList ResolveScreenList()
    {
        if (_screenListProvider is not null)
            return _screenListProvider();

        var window = OwnerWindow ?? TopLevel.GetTopLevel(Host) as Window;
        return window is not null ? new AvaloniaScreenList(window) : NoScreens.Instance;
    }

    /// <summary>Used only when no real window is available to ask (never yet shown, and no test injected a screen list) — geometry math elsewhere still has a sensible default to divide by rather than needing its own null path.</summary>
    private sealed class NoScreens : IScreenList
    {
        public static readonly NoScreens Instance = new();
        public IReadOnlyList<ScreenSnapshot> All => [];
        public ScreenSnapshot Primary { get; } = new("(no window)", new PixelRect(0, 0, 1920, 1080), 1.0);
    }
}
