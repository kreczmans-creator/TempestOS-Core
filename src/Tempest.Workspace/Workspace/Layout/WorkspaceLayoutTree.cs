namespace Tempest.App.Workspace.Layout;

/// <summary>
/// A complete workspace arrangement: the docked tree, every floating
/// window, and each panel's own presentation (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// <b>This replaces the compile-time docking geometry, it does not
/// decorate it.</b> The previous arrangement was a five-column,
/// three-row <c>Grid</c> with named slots, so "left panel", "right panel"
/// and "bottom panel" were the only arrangements expressible and floating,
/// tabbing and arbitrary splitting were not expressible at all. Here the
/// arrangement is data: an arbitrary tree of splits and tab groups, plus a
/// set of floating windows.
/// </para>
/// <para>
/// <b>The document area is a panel.</b> There is no privileged centre
/// slot. That is what lets a future surface — a Drawing Viewer, a
/// Materials table, a Tasks board — participate by registering a panel and
/// nothing else, with no change to this model or its renderer.
/// </para>
/// <para>
/// <b>Every operation is a pure function.</b> Each returns a new tree,
/// normalised, so the model cannot hold a half-applied drag, an empty tab
/// group, or a split whose weights disagree with its children. Normalising
/// on every construction rather than trusting callers is deliberate: this
/// type also deserialises layouts written by an older version of itself.
/// </para>
/// </remarks>
/// <param name="Root">The docked arrangement, or <see langword="null"/> when every panel is floating or closed.</param>
/// <param name="Floating">Every panel or subtree in its own top-level window.</param>
/// <param name="Panels">Each panel's own pin/collapse presentation. A panel absent from here uses <see cref="PanelPresentation.Default"/>.</param>
public sealed record WorkspaceLayoutTree(
    WorkspaceLayoutNode? Root,
    IReadOnlyList<FloatingLayoutWindow> Floating,
    IReadOnlyDictionary<Guid, PanelPresentation> Panels)
{
    /// <summary>An arrangement with nothing in it.</summary>
    public static readonly WorkspaceLayoutTree Empty =
        new(null, [], new Dictionary<Guid, PanelPresentation>());

    /// <summary>A single-panel arrangement — the smallest useful layout, and the seed every builder starts from.</summary>
    public static WorkspaceLayoutTree Single(Guid panelId) =>
        Empty with { Root = new LayoutTabGroupNode(Guid.NewGuid(), [panelId]) };

    /// <summary>Every panel in the arrangement, docked and floating alike.</summary>
    public IEnumerable<Guid> AllPanels =>
        (Root?.Panels ?? []).Concat(Floating.SelectMany(f => f.Content.Panels));

    /// <summary>Every panel currently docked in the main window.</summary>
    public IEnumerable<Guid> DockedPanels => Root?.Panels ?? [];

    /// <summary>Whether <paramref name="panelId"/> is anywhere in this arrangement.</summary>
    public bool Contains(Guid panelId) => AllPanels.Contains(panelId);

    /// <summary>Whether <paramref name="panelId"/> is in a floating window rather than docked.</summary>
    public bool IsFloating(Guid panelId) => Floating.Any(f => f.Content.Panels.Contains(panelId));

    /// <summary><paramref name="panelId"/>'s own presentation, defaulted when never set.</summary>
    public PanelPresentation PresentationOf(Guid panelId) =>
        Panels.TryGetValue(panelId, out var presentation) ? presentation : PanelPresentation.Default;

    /// <summary>The node with <paramref name="nodeId"/>, searching docked and floating content alike, or <see langword="null"/>.</summary>
    public WorkspaceLayoutNode? FindNode(Guid nodeId) =>
        AllNodes().FirstOrDefault(n => n.Id == nodeId);

    /// <summary>The tab group holding <paramref name="panelId"/>, or <see langword="null"/> when it holds no such panel.</summary>
    public LayoutTabGroupNode? FindGroupContaining(Guid panelId) =>
        AllNodes().OfType<LayoutTabGroupNode>().FirstOrDefault(g => g.PanelIds.Contains(panelId));

    private IEnumerable<WorkspaceLayoutNode> AllNodes() =>
        (Root?.DescendantsAndSelf ?? []).Concat(Floating.SelectMany(f => f.Content.DescendantsAndSelf));

    // ----------------------------------------------------------------
    // Docking
    // ----------------------------------------------------------------

    /// <summary>
    /// Docks <paramref name="panelId"/> relative to <paramref name="targetNodeId"/>,
    /// removing it from wherever it currently is first — so a dock is
    /// always a move, never an accidental duplication.
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
        if (withoutPanel.FindNode(targetNodeId) is null)
            return this;

        if (withoutPanel.Root is null)
            return withoutPanel with { Root = new LayoutTabGroupNode(Guid.NewGuid(), [panelId]) };

        var rewritten = Insert(withoutPanel.Root, targetNodeId, panelId, relation);
        return (withoutPanel with { Root = rewritten }).Normalised();
    }

    /// <summary>Docks <paramref name="panelId"/> as a new tab group along <paramref name="edge"/> of the whole arrangement — the coarse "drop on the window edge" gesture.</summary>
    public WorkspaceLayoutTree DockToEdge(Guid panelId, DockRelation edge)
    {
        if (edge == DockRelation.Into)
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "An edge dock needs a side, not a tab drop.");

        var withoutPanel = Remove(panelId);
        var panelGroup = new LayoutTabGroupNode(Guid.NewGuid(), [panelId]);

        if (withoutPanel.Root is null)
            return withoutPanel with { Root = panelGroup };

        var orientation = edge is DockRelation.Left or DockRelation.Right ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;
        var before = edge is DockRelation.Left or DockRelation.Above;

        IReadOnlyList<WorkspaceLayoutNode> children = before
            ? [panelGroup, withoutPanel.Root]
            : [withoutPanel.Root, panelGroup];

        return (withoutPanel with { Root = new LayoutSplitNode(Guid.NewGuid(), orientation, children) }).Normalised();
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

    /// <summary>Undocks <paramref name="panelId"/> into its own window at the given screen rectangle.</summary>
    public WorkspaceLayoutTree Float(Guid panelId, double x, double y, double width, double height)
    {
        if (!Contains(panelId))
            return this;

        var withoutPanel = Remove(panelId);
        var window = new FloatingLayoutWindow(
            Guid.NewGuid(), new LayoutTabGroupNode(Guid.NewGuid(), [panelId]), x, y, Math.Max(width, 120), Math.Max(height, 80));

        return (withoutPanel with { Floating = [.. withoutPanel.Floating, window] }).Normalised();
    }

    /// <summary>Moves a floating window, preserving everything else — the model half of dragging a floating panel across monitors.</summary>
    public WorkspaceLayoutTree MoveFloating(Guid windowId, double x, double y, double width, double height)
    {
        var moved = Floating
            .Select(f => f.Id == windowId ? f with { X = x, Y = y, Width = Math.Max(width, 120), Height = Math.Max(height, 80) } : f)
            .ToList();

        return this with { Floating = moved };
    }

    // ----------------------------------------------------------------
    // Removal, selection, sizing, presentation
    // ----------------------------------------------------------------

    /// <summary>Removes <paramref name="panelId"/> from the arrangement entirely, docked or floating, and normalises what is left.</summary>
    public WorkspaceLayoutTree Remove(Guid panelId)
    {
        var root = Root is null ? null : RemoveFrom(Root, panelId);

        // A floating window whose last panel was removed is not an empty
        // window — it is a window that should no longer exist.
        var floating = Floating
            .Select(f => (Window: f, Content: RemoveFrom(f.Content, panelId)))
            .Where(pair => pair.Content is not null)
            .Select(pair => pair.Window with { Content = pair.Content! })
            .ToList();

        var panels = Panels.Where(p => p.Key != panelId).ToDictionary(p => p.Key, p => p.Value);

        return new WorkspaceLayoutTree(root, floating, panels).Normalised();
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

    /// <summary>Rewrites every node through <paramref name="map"/>, bottom-up, in both docked and floating content.</summary>
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
            Root = Root is null ? null : Rewrite(Root),
            Floating = Floating.Select(f => f with { Content = Rewrite(f.Content) }).ToList(),
        };
    }

    /// <summary>
    /// Which side of <paramref name="referencePanelId"/> the panel
    /// <paramref name="panelId"/> sits on, or <see langword="null"/> when
    /// they share a tab group or either is absent.
    /// </summary>
    /// <remarks>
    /// The tree is the truth about arrangement, but the frozen `WP8.0B`
    /// <see cref="IWorkspaceLayout"/> contract still speaks in edges, and
    /// consumers still read it. This is how an edge is recovered from a
    /// tree: find the split that separates the two panels, and read off the
    /// orientation and the order. It is a projection, not a second model —
    /// nothing is stored, and the answer cannot drift from the arrangement.
    /// </remarks>
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
    /// The proportion of the whole arrangement <paramref name="panelId"/>
    /// occupies along its own split's axis, or <c>0</c> when it is not
    /// docked.
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
    /// with a single child, and nested splits sharing their parent's own
    /// orientation.
    /// </summary>
    /// <remarks>
    /// Run after every operation, so repeated docking and undocking cannot
    /// grow an ever-deeper tree of one-child wrappers — the failure mode
    /// that makes hand-rolled docking models degrade over a session.
    /// </remarks>
    public WorkspaceLayoutTree Normalised() => this with
    {
        Root = Root is null ? null : NormaliseNode(Root),
        Floating = Floating.Select(f => f with { Content = NormaliseNode(f.Content) }).ToList(),
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
