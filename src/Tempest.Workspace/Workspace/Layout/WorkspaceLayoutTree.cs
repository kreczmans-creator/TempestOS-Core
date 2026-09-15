namespace Tempest.Workspace.Layout;

/// <summary>
/// A complete workspace arrangement: a forest of windows, and each panel's
/// own presentation (`TD-72`, generalised to many windows by `ADR-0153`).
/// </summary>
/// <remarks>
/// <para>
/// <b>This replaces the compile-time docking geometry, it does not
/// decorate it.</b> The previous arrangement was a five-column,
/// three-row <c>Grid</c> with named slots, so "left panel", "right panel"
/// and "bottom panel" were the only arrangements expressible and floating,
/// tabbing and arbitrary splitting were not expressible at all. Here the
/// arrangement is data: an arbitrary tree of splits and tab groups, in
/// every window that currently exists.
/// </para>
/// <para>
/// <b>Every window is a peer.</b> `ADR-0095` (`TD-72`) first expressed this
/// as "a root plus a floating list" — one privileged docked tree, and a
/// separate list of floating panels. `ADR-0153` decision 1 generalises that
/// into <see cref="Windows"/>: an ordered set of <see cref="WorkspaceLayoutWindow"/>
/// entries, exactly one of them <see cref="WorkspaceLayoutWindow.IsPrimary"/>.
/// The document area is a panel inside one of them; there is no privileged
/// centre slot, and now no privileged window either — the primary window is
/// simply the one the rail, header and ribbon live around.
/// </para>
/// <para>
/// <b>Every operation is a pure function.</b> Each returns a new tree,
/// normalised, so the model cannot hold a half-applied drag, an empty tab
/// group, a window with no panels left in it (other than the primary one,
/// which persists empty), or a split whose weights disagree with its
/// children. Normalising on every construction rather than trusting callers
/// is deliberate: this type also deserialises layouts written by an older
/// version of itself.
/// </para>
/// </remarks>
/// <param name="Windows">Every window in the arrangement, the primary one included.</param>
/// <param name="Panels">Each panel's own pin/collapse presentation. A panel absent from here uses <see cref="PanelPresentation.Default"/>.</param>
public sealed record WorkspaceLayoutTree(
    IReadOnlyList<WorkspaceLayoutWindow> Windows,
    IReadOnlyDictionary<Guid, PanelPresentation> Panels)
{
    /// <summary>An arrangement with nothing in it: one primary window, holding nothing.</summary>
    public static readonly WorkspaceLayoutTree Empty = new(
        [new WorkspaceLayoutWindow(Guid.NewGuid(), null, IsPrimary: true, null, 0, 0, 0, 0)],
        new Dictionary<Guid, PanelPresentation>());

    /// <summary>A single-panel arrangement — the smallest useful layout, and the seed every builder starts from.</summary>
    public static WorkspaceLayoutTree Single(Guid panelId) =>
        Empty.WithPrimaryRoot(new LayoutTabGroupNode(Guid.NewGuid(), [panelId]));

    /// <summary>
    /// Rebuilds this arrangement's pre-`ADR-0153` shape — a docked tree
    /// plus a list of floating windows — the constructor
    /// <see cref="FloatingPanelWindow"/> and the rest of `Tempest.Desktop`'s
    /// own rendering still use, so no view changed to make room for the
    /// forest underneath it.
    /// </summary>
    public WorkspaceLayoutTree(WorkspaceLayoutNode? root, IReadOnlyList<FloatingLayoutWindow> floating, IReadOnlyDictionary<Guid, PanelPresentation> panels)
        : this(BuildWindows(root, floating), panels)
    {
    }

    private static IReadOnlyList<WorkspaceLayoutWindow> BuildWindows(WorkspaceLayoutNode? root, IReadOnlyList<FloatingLayoutWindow> floating)
    {
        ArgumentNullException.ThrowIfNull(floating);

        var windows = new List<WorkspaceLayoutWindow> { new(Guid.NewGuid(), root, IsPrimary: true, null, 0, 0, 0, 0) };
        windows.AddRange(floating.Select(f => new WorkspaceLayoutWindow(f.Id, f.Content, IsPrimary: false, null, f.X, f.Y, f.Width, f.Height)));
        return windows;
    }

    // ----------------------------------------------------------------
    // The pre-`ADR-0153` shape, as a read-only projection
    // ----------------------------------------------------------------

    /// <summary>The application's own main window — the entry carrying the rail and header.</summary>
    public WorkspaceLayoutWindow PrimaryWindow => Windows.First(w => w.IsPrimary);

    /// <summary>The primary window's own docked arrangement, or <see langword="null"/> when it holds nothing.</summary>
    public WorkspaceLayoutNode? Root => PrimaryWindow.Root;

    /// <summary>Every non-primary window, in the pre-`ADR-0153` shape every existing caller still reads.</summary>
    public IReadOnlyList<FloatingLayoutWindow> Floating =>
        Windows.Where(w => !w.IsPrimary)
            .Select(w => new FloatingLayoutWindow(w.Id, w.Root!, w.X, w.Y, w.Width, w.Height))
            .ToList();

    /// <summary>Replaces the primary window's own root — the seed every builder that starts from a hand-built tree uses.</summary>
    public WorkspaceLayoutTree WithPrimaryRoot(WorkspaceLayoutNode? root) =>
        ReplaceWindow(PrimaryWindow with { Root = root });

    private WorkspaceLayoutTree ReplaceWindow(WorkspaceLayoutWindow updated) =>
        this with { Windows = Windows.Select(w => w.Id == updated.Id ? updated : w).ToList() };

    /// <summary>Every panel in the arrangement, in every window.</summary>
    public IEnumerable<Guid> AllPanels => Windows.SelectMany(w => w.Panels);

    /// <summary>Every panel currently docked in the primary window.</summary>
    public IEnumerable<Guid> DockedPanels => Root?.Panels ?? [];

    /// <summary>Whether <paramref name="panelId"/> is anywhere in this arrangement.</summary>
    public bool Contains(Guid panelId) => AllPanels.Contains(panelId);

    /// <summary>Whether <paramref name="panelId"/> is in a non-primary window rather than docked in the primary one.</summary>
    public bool IsFloating(Guid panelId) => Windows.Any(w => !w.IsPrimary && w.Panels.Contains(panelId));

    /// <summary><paramref name="panelId"/>'s own presentation, defaulted when never set.</summary>
    public PanelPresentation PresentationOf(Guid panelId) =>
        Panels.TryGetValue(panelId, out var presentation) ? presentation : PanelPresentation.Default;

    /// <summary>The node with <paramref name="nodeId"/>, searching every window, or <see langword="null"/>.</summary>
    public WorkspaceLayoutNode? FindNode(Guid nodeId) =>
        AllNodes().FirstOrDefault(n => n.Id == nodeId);

    /// <summary>The tab group holding <paramref name="panelId"/>, or <see langword="null"/> when it holds no such panel.</summary>
    public LayoutTabGroupNode? FindGroupContaining(Guid panelId) =>
        AllNodes().OfType<LayoutTabGroupNode>().FirstOrDefault(g => g.PanelIds.Contains(panelId));

    /// <summary>The window whose subtree contains <paramref name="nodeId"/>, or <see langword="null"/>.</summary>
    public WorkspaceLayoutWindow? FindWindowContaining(Guid nodeId) =>
        Windows.FirstOrDefault(w => w.Root is not null && w.Root.DescendantsAndSelf.Any(n => n.Id == nodeId));

    private IEnumerable<WorkspaceLayoutNode> AllNodes() =>
        Windows.SelectMany(w => w.Root?.DescendantsAndSelf ?? []);

    // ----------------------------------------------------------------
    // Docking
    // ----------------------------------------------------------------

    /// <summary>
    /// Docks <paramref name="panelId"/> relative to <paramref name="targetNodeId"/>,
    /// removing it from wherever it currently is first — so a dock is
    /// always a move, never an accidental duplication — and from whichever
    /// window it was previously in, into whichever window
    /// <paramref name="targetNodeId"/> is actually in (`ADR-0153` decision
    /// 4: a cross-window dock is one edit to one tree, not two).
    /// </summary>
    /// <param name="panelId">The panel being docked.</param>
    /// <param name="targetNodeId">The node it is dropped on — a tab group, or a split.</param>
    /// <param name="relation">Which of the five drop zones was used.</param>
    /// <returns>The new arrangement, or this one unchanged when the target no longer exists.</returns>
    public WorkspaceLayoutTree Dock(Guid panelId, Guid targetNodeId, DockRelation relation)
    {
        // Dropping a panel onto its own group, as a tab, is a no-op rather
        // than a remove-then-fail: the user has expressed nothing.
        if (relation == DockRelation.Into && FindGroupContaining(panelId) is { } own && own.Id == targetNodeId && own.PanelIds.Count == 1)
            return this;

        var withoutPanel = Remove(panelId);
        if (withoutPanel.FindWindowContaining(targetNodeId) is not { } targetWindow)
            return this;

        var rewritten = Insert(targetWindow.Root!, targetNodeId, panelId, relation);
        return withoutPanel.ReplaceWindow(targetWindow with { Root = rewritten }).Normalised();
    }

    /// <summary>Docks <paramref name="panelId"/> as a new tab group along <paramref name="edge"/> of the primary window's own arrangement — the coarse "drop on the window edge" gesture.</summary>
    public WorkspaceLayoutTree DockToEdge(Guid panelId, DockRelation edge)
    {
        if (edge == DockRelation.Into)
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "An edge dock needs a side, not a tab drop.");

        var withoutPanel = Remove(panelId);
        var panelGroup = new LayoutTabGroupNode(Guid.NewGuid(), [panelId]);
        var primary = withoutPanel.PrimaryWindow;

        if (primary.Root is null)
            return withoutPanel.ReplaceWindow(primary with { Root = panelGroup }).Normalised();

        var orientation = edge is DockRelation.Left or DockRelation.Right ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;
        var before = edge is DockRelation.Left or DockRelation.Above;

        IReadOnlyList<WorkspaceLayoutNode> children = before
            ? [panelGroup, primary.Root]
            : [primary.Root, panelGroup];

        var newRoot = new LayoutSplitNode(Guid.NewGuid(), orientation, children);
        return withoutPanel.ReplaceWindow(primary with { Root = newRoot }).Normalised();
    }

    private static WorkspaceLayoutNode Insert(WorkspaceLayoutNode node, Guid targetNodeId, Guid panelId, DockRelation relation)
    {
        if (node.Id == targetNodeId)
            return Combine(node, panelId, relation);

        if (node is not LayoutSplitNode split)
            return node;

        var children = split.Children.Select(c => Insert(c, targetNodeId, panelId, relation)).ToList();
        return new LayoutSplitNode(split.Id, split.Orientation, children, split.Weights);
    }

    private static WorkspaceLayoutNode Combine(WorkspaceLayoutNode target, Guid panelId, DockRelation relation)
    {
        if (relation == DockRelation.Into)
        {
            // Dropping into a split rather than a leaf tabs onto its first
            // leaf — the nearest sensible reading of the gesture.
            if (target is LayoutTabGroupNode group)
                return new LayoutTabGroupNode(group.Id, [.. group.PanelIds, panelId], group.PanelIds.Count);

            var split = (LayoutSplitNode)target;
            var firstLeaf = split.DescendantsAndSelf.OfType<LayoutTabGroupNode>().First();
            return Insert(split, firstLeaf.Id, panelId, DockRelation.Into);
        }

        var newGroup = new LayoutTabGroupNode(Guid.NewGuid(), [panelId]);
        var orientation = relation is DockRelation.Left or DockRelation.Right ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;
        var before = relation is DockRelation.Left or DockRelation.Above;

        IReadOnlyList<WorkspaceLayoutNode> children = before ? [newGroup, target] : [target, newGroup];
        return new LayoutSplitNode(Guid.NewGuid(), orientation, children);
    }

    // ----------------------------------------------------------------
    // Floating
    // ----------------------------------------------------------------

    /// <summary>Undocks <paramref name="panelId"/> into its own new window at the given screen rectangle.</summary>
    public WorkspaceLayoutTree Float(Guid panelId, double x, double y, double width, double height)
    {
        if (!Contains(panelId))
            return this;

        var withoutPanel = Remove(panelId);
        var window = new WorkspaceLayoutWindow(
            Guid.NewGuid(), new LayoutTabGroupNode(Guid.NewGuid(), [panelId]), IsPrimary: false, null,
            x, y, Math.Max(width, 120), Math.Max(height, 80));

        return (withoutPanel with { Windows = [.. withoutPanel.Windows, window] }).Normalised();
    }

    /// <summary>Moves window <paramref name="windowId"/>, preserving everything else — the model half of dragging a floating window across monitors. A no-op when no window carries that id.</summary>
    public WorkspaceLayoutTree MoveFloating(Guid windowId, double x, double y, double width, double height)
    {
        var moved = Windows
            .Select(w => w.Id == windowId ? w with { X = x, Y = y, Width = Math.Max(width, 120), Height = Math.Max(height, 80) } : w)
            .ToList();

        return this with { Windows = moved };
    }

    // ----------------------------------------------------------------
    // Removal, selection, sizing, presentation
    // ----------------------------------------------------------------

    /// <summary>Removes <paramref name="panelId"/> from the arrangement entirely, in whichever window it is, and normalises what is left.</summary>
    public WorkspaceLayoutTree Remove(Guid panelId)
    {
        var windows = new List<WorkspaceLayoutWindow>(Windows.Count);

        foreach (var window in Windows)
        {
            var newRoot = window.Root is null ? null : RemoveFrom(window.Root, panelId);

            // A non-primary window whose last panel was removed is not an
            // empty window — it is a window that should no longer exist.
            // The primary window always persists, even holding nothing.
            if (newRoot is null && !window.IsPrimary)
                continue;

            windows.Add(window with { Root = newRoot });
        }

        var panels = Panels.Where(p => p.Key != panelId).ToDictionary(p => p.Key, p => p.Value);

        return new WorkspaceLayoutTree(windows, panels).Normalised();
    }

    private static WorkspaceLayoutNode? RemoveFrom(WorkspaceLayoutNode node, Guid panelId)
    {
        switch (node)
        {
            case LayoutTabGroupNode group when group.PanelIds.Contains(panelId):
            {
                var remaining = group.PanelIds.Where(p => p != panelId).ToList();
                if (remaining.Count == 0)
                    return null;

                var selected = Math.Min(group.SelectedIndex, remaining.Count - 1);
                return new LayoutTabGroupNode(group.Id, remaining, selected);
            }

            case LayoutSplitNode split:
            {
                var kept = new List<WorkspaceLayoutNode>();
                var weights = new List<double>();

                for (var i = 0; i < split.Children.Count; i++)
                {
                    if (RemoveFrom(split.Children[i], panelId) is { } child)
                    {
                        kept.Add(child);
                        weights.Add(split.Weights[i]);
                    }
                }

                return kept.Count == 0 ? null : new LayoutSplitNode(split.Id, split.Orientation, kept, weights);
            }

            default:
                return node;
        }
    }

    /// <summary>Brings <paramref name="panelId"/> to the front of whichever tab group holds it.</summary>
    public WorkspaceLayoutTree SelectPanel(Guid panelId)
    {
        if (FindGroupContaining(panelId) is not { } group)
            return this;

        var updated = new LayoutTabGroupNode(group.Id, group.PanelIds, group.PanelIds.ToList().IndexOf(panelId));
        return MapNodes(n => n.Id == group.Id ? updated : n);
    }

    /// <summary>
    /// Moves <paramref name="panelId"/> one position within its own tab
    /// group (`ADR-0153` decision 8, `TD-133`'s own named reordering
    /// residual) — <paramref name="direction"/> negative for one position
    /// earlier, positive for one position later. A no-op off either end of
    /// the group, or when the group does not hold the panel.
    /// </summary>
    public WorkspaceLayoutTree ReorderTab(Guid groupId, Guid panelId, int direction)
    {
        if (direction == 0 || FindNode(groupId) is not LayoutTabGroupNode group || !group.PanelIds.Contains(panelId))
            return this;

        var ids = group.PanelIds.ToList();
        var from = ids.IndexOf(panelId);
        var to = from + (direction > 0 ? 1 : -1);
        if (to < 0 || to >= ids.Count)
            return this;

        var selectedPanelId = group.SelectedPanelId;
        (ids[from], ids[to]) = (ids[to], ids[from]);

        var updated = new LayoutTabGroupNode(group.Id, ids, ids.IndexOf(selectedPanelId));
        return MapNodes(n => n.Id == group.Id ? updated : n);
    }

    /// <summary>Sets the proportional shares of the split with <paramref name="splitId"/> — the model half of a splitter drag.</summary>
    public WorkspaceLayoutTree SetWeights(Guid splitId, IReadOnlyList<double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        if (FindNode(splitId) is not LayoutSplitNode split || weights.Count != split.Children.Count)
            return this;

        return MapNodes(n => n.Id == splitId
            ? new LayoutSplitNode(split.Id, split.Orientation, split.Children, weights)
            : n);
    }

    /// <summary>
    /// Shrinks or grows <paramref name="panelId"/>'s own proportional
    /// share of the split it sits in, within its own window, by
    /// <paramref name="delta"/> (`WP 19.2B`, `TD-133` — the keyboard
    /// counterpart of dragging that split's own <c>GridSplitter</c>),
    /// taking the change evenly from every sibling and renormalising so the
    /// split's own weights still sum to one.
    /// </summary>
    /// <param name="panelId">The panel being resized.</param>
    /// <param name="delta">
    /// The proportional change — positive grows <paramref name="panelId"/>,
    /// negative shrinks it.
    /// </param>
    /// <returns>
    /// The new arrangement; this one unchanged when <paramref name="panelId"/>
    /// is not docked, shares its split with nothing (nothing to take the
    /// change from), or the change would shrink any sibling below a usable
    /// minimum share.
    /// </returns>
    public WorkspaceLayoutTree ResizeSplit(Guid panelId, double delta)
    {
        const double minimumShare = 0.05;

        var window = Windows.FirstOrDefault(w => w.Root is not null && w.Panels.Contains(panelId));
        if (window?.Root is null)
            return this;

        foreach (var split in window.Root.DescendantsAndSelf.OfType<LayoutSplitNode>())
        {
            var index = split.Children.ToList().FindIndex(c => c.Panels.Contains(panelId));
            if (index < 0 || split.Children.Count < 2)
                continue;

            var othersCount = split.Children.Count - 1;
            var perOther = delta / othersCount;

            var proposed = split.Weights
                .Select((weight, i) => i == index ? weight + delta : weight - perOther)
                .ToList();

            if (proposed.Any(weight => weight < minimumShare))
                return this;

            var sum = proposed.Sum();
            var normalised = proposed.Select(weight => weight / sum).ToList();

            return SetWeights(split.Id, normalised);
        }

        return this;
    }

    /// <summary>Sets whether <paramref name="panelId"/> is pinned, or Auto-Hidden to an edge strip.</summary>
    public WorkspaceLayoutTree SetPinned(Guid panelId, bool isPinned) =>
        WithPresentation(panelId, PresentationOf(panelId) with { IsPinned = isPinned });

    /// <summary>Sets whether <paramref name="panelId"/> is collapsed to its own strip in place.</summary>
    public WorkspaceLayoutTree SetCollapsed(Guid panelId, bool isCollapsed) =>
        WithPresentation(panelId, PresentationOf(panelId) with { IsCollapsed = isCollapsed });

    private WorkspaceLayoutTree WithPresentation(Guid panelId, PanelPresentation presentation)
    {
        var panels = new Dictionary<Guid, PanelPresentation>(Panels) { [panelId] = presentation };
        return this with { Panels = panels };
    }

    /// <summary>Rewrites every node through <paramref name="map"/>, bottom-up, in every window.</summary>
    private WorkspaceLayoutTree MapNodes(Func<WorkspaceLayoutNode, WorkspaceLayoutNode> map)
    {
        WorkspaceLayoutNode Rewrite(WorkspaceLayoutNode node)
        {
            var mapped = map(node);

            return mapped is LayoutSplitNode split
                ? new LayoutSplitNode(split.Id, split.Orientation, split.Children.Select(Rewrite).ToList(), split.Weights)
                : mapped;
        }

        return this with
        {
            Windows = Windows.Select(w => w.Root is null ? w : w with { Root = Rewrite(w.Root) }).ToList(),
        };
    }

    /// <summary>
    /// Which side of <paramref name="referencePanelId"/> the panel
    /// <paramref name="panelId"/> sits on, or <see langword="null"/> when
    /// they share a tab group, either is absent, or either is outside the
    /// primary window. This is a projection over the primary window's own
    /// arrangement only — a panel on a different window has no edge
    /// relationship to report short of naming the window itself, which the
    /// frozen `WP8.0B` <see cref="IWorkspaceLayout"/> contract this serves
    /// cannot express either (`ADR-0153` decision 6 leaves that contract
    /// exactly as lossy as it already was).
    /// </summary>
    public DockRelation? InferEdge(Guid panelId, Guid referencePanelId)
    {
        if (Root is null || panelId == referencePanelId)
            return null;

        foreach (var split in Root.DescendantsAndSelf.OfType<LayoutSplitNode>())
        {
            var panelIndex = split.Children.ToList().FindIndex(c => c.Panels.Contains(panelId));
            var referenceIndex = split.Children.ToList().FindIndex(c => c.Panels.Contains(referencePanelId));

            if (panelIndex < 0 || referenceIndex < 0 || panelIndex == referenceIndex)
                continue;

            var before = panelIndex < referenceIndex;

            return split.Orientation == LayoutOrientation.Horizontal
                ? before ? DockRelation.Left : DockRelation.Right
                : before ? DockRelation.Above : DockRelation.Below;
        }

        return null;
    }

    /// <summary>
    /// The proportion of the primary window's own arrangement
    /// <paramref name="panelId"/> occupies along its own split's axis, or
    /// <c>0</c> when it is not docked there.
    /// </summary>
    public double ShareOf(Guid panelId)
    {
        if (Root is null || !DockedPanels.Contains(panelId))
            return 0;

        foreach (var split in Root.DescendantsAndSelf.OfType<LayoutSplitNode>())
        {
            var index = split.Children.ToList().FindIndex(c => c.Panels.Contains(panelId));
            if (index >= 0)
                return split.Weights[index];
        }

        // The whole arrangement is this one panel.
        return 1;
    }

    // ----------------------------------------------------------------
    // Normalisation
    // ----------------------------------------------------------------

    /// <summary>
    /// Collapses the structural debris every edit leaves behind: splits
    /// with a single child, nested splits sharing their parent's own
    /// orientation, and a non-primary window with nothing left in it.
    /// </summary>
    /// <remarks>
    /// Run after every operation, so repeated docking and undocking cannot
    /// grow an ever-deeper tree of one-child wrappers — the failure mode
    /// that makes hand-rolled docking models degrade over a session.
    /// </remarks>
    public WorkspaceLayoutTree Normalised() => this with
    {
        Windows = Windows
            .Select(w => w.Root is null ? w : w with { Root = NormaliseNode(w.Root) })
            .Where(w => w.IsPrimary || w.Root is not null)
            .ToList(),
    };

    private static WorkspaceLayoutNode NormaliseNode(WorkspaceLayoutNode node)
    {
        if (node is not LayoutSplitNode split)
            return node;

        var children = new List<WorkspaceLayoutNode>();
        var weights = new List<double>();

        for (var i = 0; i < split.Children.Count; i++)
        {
            var child = NormaliseNode(split.Children[i]);
            var weight = split.Weights[i];

            // A nested split along the same axis is the same split: flatten
            // it, distributing its own share across its children.
            if (child is LayoutSplitNode nested && nested.Orientation == split.Orientation)
            {
                for (var j = 0; j < nested.Children.Count; j++)
                {
                    children.Add(nested.Children[j]);
                    weights.Add(weight * nested.Weights[j]);
                }
            }
            else
            {
                children.Add(child);
                weights.Add(weight);
            }
        }

        return children.Count == 1
            ? children[0]
            : new LayoutSplitNode(split.Id, split.Orientation, children, weights);
    }
}
