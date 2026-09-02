namespace Tempest.App.Workspace.Layout;

/// <summary>The axis a <see cref="LayoutSplitNode"/> divides its children along.</summary>
public enum LayoutOrientation
{
    /// <summary>Children sit side by side, left to right.</summary>
    Horizontal,

    /// <summary>Children sit one above another, top to bottom.</summary>
    Vertical,
}

/// <summary>
/// One node in a workspace layout tree (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// <b>The layout is data, and the data is a tree.</b> The arrangement
/// TempestOS renders is not a fixed grid with named slots — it is an
/// arbitrary nesting of splits and tab groups, of any depth, in either
/// orientation. That is what makes drag-to-dock, tabbing and arbitrary
/// splitting expressible at all: each of them is a small, total function
/// from one tree to another.
/// </para>
/// <para>
/// <b>Immutable by construction.</b> Every operation returns a new tree
/// rather than mutating this one, so a half-applied dock cannot exist, a
/// layout can be compared, undone, serialised or diffed, and the whole
/// model is testable with no UI in the process at all — the property the
/// previous compile-time <c>Grid</c> could never have.
/// </para>
/// </remarks>
/// <param name="Id">This node's own stable identity, used to address it for resize and dock operations.</param>
public abstract record WorkspaceLayoutNode(Guid Id)
{
    /// <summary>Every panel this node contains, in document order.</summary>
    public abstract IEnumerable<Guid> Panels { get; }

    /// <summary>Every node in this subtree, this one included.</summary>
    public abstract IEnumerable<WorkspaceLayoutNode> DescendantsAndSelf { get; }
}

/// <summary>
/// A split: two or more children sharing an axis, each taking its own
/// share of the available space.
/// </summary>
/// <remarks>
/// <see cref="Weights"/> are proportional, not pixels — a layout restored
/// into a different window size, or onto a different monitor, keeps its
/// proportions rather than its absolute geometry. They are normalised to
/// sum to one whenever a tree is built, so no operation can leave them
/// inconsistent with <see cref="Children"/>.
/// </remarks>
/// <param name="Id">This split's own identity — the handle a splitter drag addresses.</param>
/// <param name="Orientation">The axis children are divided along.</param>
/// <param name="Children">The child nodes, in order along the axis.</param>
/// <param name="Weights">Each child's proportional share, in the same order, summing to one.</param>
public sealed record LayoutSplitNode : WorkspaceLayoutNode
{
    /// <summary>Initialises a new instance of the <see cref="LayoutSplitNode"/> class, normalising <paramref name="weights"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="children"/> is empty, or <paramref name="weights"/> has a different length.</exception>
    public LayoutSplitNode(Guid id, LayoutOrientation orientation, IReadOnlyList<WorkspaceLayoutNode> children, IReadOnlyList<double>? weights = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(children);

        if (children.Count == 0)
            throw new ArgumentException("A split must have at least one child.", nameof(children));

        if (weights is not null && weights.Count != children.Count)
            throw new ArgumentException("A split needs exactly one weight per child.", nameof(weights));

        Orientation = orientation;
        Children = children;
        Weights = Normalise(weights ?? Enumerable.Repeat(1.0, children.Count).ToList());
    }

    /// <summary>The axis children are divided along.</summary>
    public LayoutOrientation Orientation { get; }

    /// <summary>The child nodes, in order along the axis.</summary>
    public IReadOnlyList<WorkspaceLayoutNode> Children { get; }

    /// <summary>Each child's proportional share, in the same order, summing to one.</summary>
    public IReadOnlyList<double> Weights { get; }

    /// <inheritdoc />
    public override IEnumerable<Guid> Panels => Children.SelectMany(c => c.Panels);

    /// <inheritdoc />
    public override IEnumerable<WorkspaceLayoutNode> DescendantsAndSelf =>
        new WorkspaceLayoutNode[] { this }.Concat(Children.SelectMany(c => c.DescendantsAndSelf));

    /// <summary>
    /// Rescales <paramref name="weights"/> so they sum to one, falling
    /// back to an even share when they cannot (all zero, negative, or
    /// non-finite) — a layout read back from a store this class does not
    /// own must never be able to produce a division by zero or a panel of
    /// infinite width.
    /// </summary>
    private static IReadOnlyList<double> Normalise(IReadOnlyList<double> weights)
    {
        var sanitised = weights.Select(w => double.IsFinite(w) && w > 0 ? w : 0).ToList();
        var total = sanitised.Sum();

        return total > 0
            ? sanitised.Select(w => w / total).ToList()
            : Enumerable.Repeat(1.0 / weights.Count, weights.Count).ToList();
    }
}

/// <summary>
/// A tab group: one or more panels occupying the same space, one of them
/// selected.
/// </summary>
/// <remarks>
/// The only kind of leaf a layout has. A single docked panel is a tab
/// group of one, which is what makes "drag a panel onto another panel to
/// tab them together" an ordinary operation rather than a special case.
/// </remarks>
/// <param name="Id">This group's own identity — the handle a dock operation addresses.</param>
/// <param name="PanelIds">The panels in this group, in tab order.</param>
/// <param name="SelectedIndex">The selected tab, always a valid index into <paramref name="PanelIds"/>.</param>
public sealed record LayoutTabGroupNode : WorkspaceLayoutNode
{
    /// <summary>Initialises a new instance of the <see cref="LayoutTabGroupNode"/> class, clamping <paramref name="selectedIndex"/> into range.</summary>
    /// <exception cref="ArgumentException"><paramref name="panelIds"/> is empty or contains a duplicate.</exception>
    public LayoutTabGroupNode(Guid id, IReadOnlyList<Guid> panelIds, int selectedIndex = 0)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(panelIds);

        if (panelIds.Count == 0)
            throw new ArgumentException("A tab group must contain at least one panel.", nameof(panelIds));

        if (panelIds.Distinct().Count() != panelIds.Count)
            throw new ArgumentException("A panel can appear at most once in a tab group.", nameof(panelIds));

        PanelIds = panelIds;
        SelectedIndex = Math.Clamp(selectedIndex, 0, panelIds.Count - 1);
    }

    /// <summary>The panels in this group, in tab order.</summary>
    public IReadOnlyList<Guid> PanelIds { get; }

    /// <summary>The selected tab.</summary>
    public int SelectedIndex { get; }

    /// <summary>The currently selected panel.</summary>
    public Guid SelectedPanelId => PanelIds[SelectedIndex];

    /// <inheritdoc />
    public override IEnumerable<Guid> Panels => PanelIds;

    /// <inheritdoc />
    public override IEnumerable<WorkspaceLayoutNode> DescendantsAndSelf => [this];
}
