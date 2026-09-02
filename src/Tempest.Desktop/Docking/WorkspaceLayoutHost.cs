using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.App.Workspace.Layout;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Docking;

/// <summary>
/// Renders a <see cref="WorkspaceLayoutTree"/> (`TD-72`) — the docking
/// surface that replaced the compile-time five-column grid.
/// </summary>
/// <remarks>
/// <para>
/// <b>The renderer decides nothing.</b> It is a total function from the
/// layout tree to a visual tree: splits become <see cref="Grid"/>s with
/// proportional star sizing and <see cref="GridSplitter"/>s between
/// children, tab groups become <see cref="LayoutTabGroupView"/>s, and
/// floating windows become real top-level windows. Every gesture the user
/// makes is turned into a pure operation on the tree and re-rendered from
/// the result, so what is on screen can never disagree with the model — the
/// class of bug that makes hand-rolled docking drift after a few drags.
/// </para>
/// <para>
/// <b>Proportional, not pixel, sizing.</b> Splits carry weights, and the
/// renderer maps them to star widths. A layout restored into a smaller
/// window, or onto a different monitor, keeps its proportions instead of
/// pushing the document area off the edge.
/// </para>
/// </remarks>
public sealed class WorkspaceLayoutHost : UserControl
{
    /// <summary>
    /// The narrowest the largest pane is allowed to become before
    /// responsive layout starts collapsing side panels (`TD-70`, preserved).
    /// </summary>
    public const double MinPrimaryPaneWidth = 420;

    /// <summary>The <see cref="MinPrimaryPaneWidth"/> equivalent for vertical splits (`TD-70`, preserved).</summary>
    public const double MinPrimaryPaneHeight = 220;

    /// <summary>The narrowest a panel is squeezed to before it is collapsed to a strip instead (`TD-70`, preserved).</summary>
    public const double MinUsablePanelSize = 140;

    private readonly WorkspacePanelRegistry _registry;
    private readonly Panel _root = new Panel();
    private readonly ContentControl _layoutHost = new();
    private readonly Border _flyout = new() { IsVisible = false, MinWidth = 240, MinHeight = 160 };

    private WorkspaceLayoutTree _tree = WorkspaceLayoutTree.Empty;
    private Guid? _flyoutPanelId;

    /// <summary>Raised whenever a user gesture produces a new arrangement.</summary>
    public event Action<WorkspaceLayoutTree>? LayoutChanged;

    /// <summary>Raised when the user starts dragging a panel's own tab.</summary>
    public event Action<Guid, PointerPressedEventArgs>? PanelDragStarted;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceLayoutHost"/> class.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public WorkspaceLayoutHost(WorkspacePanelRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;

        ThemeReactiveBrush.Bind(_flyout, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        _flyout.HorizontalAlignment = HorizontalAlignment.Left;
        _flyout.VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(_flyout, "Auto-hide flyout");

        _root.Children.Add(_layoutHost);
        _root.Children.Add(_flyout);
        Content = _root;

        // `TD-70`'s responsive rule was previously never wired to anything
        // in the running window — it existed, and only tests called it
        // (`TD-83`). Subscribing here means the guarantee actually holds
        // for a user resizing the window, and holds for floating windows
        // too, since they render through this same host.
        SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    /// <summary>The arrangement currently rendered.</summary>
    public WorkspaceLayoutTree Tree => _tree;

    /// <summary>Whether an auto-hide flyout is currently open.</summary>
    public bool IsFlyoutOpen => _flyout.IsVisible;

    /// <summary>Every tab group currently rendered — the candidate drop targets.</summary>
    public IReadOnlyList<LayoutTabGroupView> TabGroups { get; private set; } = [];

    /// <summary>Renders <paramref name="tree"/>, replacing whatever was shown.</summary>
    public void Update(WorkspaceLayoutTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        _tree = tree;
        HideFlyout();

        var groups = new List<LayoutTabGroupView>();
        _layoutHost.Content = tree.Root is null ? BuildEmptyState() : Render(tree.Root, groups);
        TabGroups = groups;
    }

    /// <summary>Applies <paramref name="operation"/> to the current arrangement, re-renders, and announces the result.</summary>
    public void Apply(Func<WorkspaceLayoutTree, WorkspaceLayoutTree> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var updated = operation(_tree);
        if (updated == _tree)
            return;

        Update(updated);
        LayoutChanged?.Invoke(updated);
    }

    private static Control BuildEmptyState() => new TextBlock
    {
        Text = "Every panel is closed. Restore the default layout from the View menu.",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.7,
    };

    private Control Render(WorkspaceLayoutNode node, List<LayoutTabGroupView> groups) => node switch
    {
        LayoutTabGroupNode tabs => RenderTabGroup(tabs, groups),
        LayoutSplitNode split => RenderSplit(split, groups),
        _ => new Control(),
    };

    private Control RenderTabGroup(LayoutTabGroupNode node, List<LayoutTabGroupView> groups)
    {
        var view = new LayoutTabGroupView(node, _registry, _tree);
        groups.Add(view);

        view.PanelSelected += panelId => Apply(t => t.SelectPanel(panelId));
        view.PanelClosed += panelId => Apply(t => t.Remove(panelId));
        view.CollapseToggled += (panelId, collapsed) => Apply(t => t.SetCollapsed(panelId, collapsed));
        view.PinToggled += (panelId, pinned) => Apply(t => t.SetPinned(panelId, pinned));
        view.FlyoutRequested += ShowFlyout;
        view.TabDragStarted += (panelId, e) => PanelDragStarted?.Invoke(panelId, e);

        return view;
    }

    private Control RenderSplit(LayoutSplitNode split, List<LayoutTabGroupView> groups)
    {
        var grid = new Grid { Tag = split.Id };
        var horizontal = split.Orientation == LayoutOrientation.Horizontal;

        for (var i = 0; i < split.Children.Count; i++)
        {
            var child = split.Children[i];
            var isStrip = IsStrip(child);

            // A collapsed or auto-hidden pane takes exactly its strip, and
            // hands the rest of its share back to its siblings.
            var length = isStrip
                ? new GridLength(LayoutTabGroupView.StripSize, GridUnitType.Pixel)
                : new GridLength(split.Weights[i], GridUnitType.Star);

            if (i > 0)
            {
                var splitterLength = new GridLength(4, GridUnitType.Pixel);
                if (horizontal)
                    grid.ColumnDefinitions.Add(new ColumnDefinition(splitterLength));
                else
                    grid.RowDefinitions.Add(new RowDefinition(splitterLength));

                var splitter = BuildSplitter(grid, split, horizontal);
                var splitterIndex = horizontal ? grid.ColumnDefinitions.Count - 1 : grid.RowDefinitions.Count - 1;
                if (horizontal)
                    Grid.SetColumn(splitter, splitterIndex);
                else
                    Grid.SetRow(splitter, splitterIndex);

                grid.Children.Add(splitter);
            }

            if (horizontal)
                grid.ColumnDefinitions.Add(new ColumnDefinition(length));
            else
                grid.RowDefinitions.Add(new RowDefinition(length));

            var rendered = Render(child, groups);
            var index = horizontal ? grid.ColumnDefinitions.Count - 1 : grid.RowDefinitions.Count - 1;
            if (horizontal)
                Grid.SetColumn(rendered, index);
            else
                Grid.SetRow(rendered, index);

            grid.Children.Add(rendered);
        }

        return grid;
    }

    private GridSplitter BuildSplitter(Grid grid, LayoutSplitNode split, bool horizontal)
    {
        var splitter = horizontal
            ? new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch }
            : new GridSplitter { Height = 4, VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };

        AutomationProperties.SetName(splitter, "Resize panels");

        // A drag changes pixel sizes on the grid; the model stores
        // proportions. Reading them back out on drag completion is what
        // keeps a resize durable and resolution-independent.
        splitter.DragCompleted += (_, _) => Apply(t => t.SetWeights(split.Id, ReadWeights(grid, split, horizontal)));

        return splitter;
    }

    /// <summary>Reads the current pane sizes back out of <paramref name="grid"/> as proportional weights.</summary>
    private static IReadOnlyList<double> ReadWeights(Grid grid, LayoutSplitNode split, bool horizontal)
    {
        var sizes = new List<double>(split.Children.Count);

        for (var i = 0; i < split.Children.Count; i++)
        {
            // Panes sit at even indices; odd indices are the splitters.
            var definitionIndex = i * 2;

            var size = horizontal
                ? grid.ColumnDefinitions[definitionIndex].ActualWidth
                : grid.RowDefinitions[definitionIndex].ActualHeight;

            sizes.Add(size > 0 ? size : split.Weights[i]);
        }

        return sizes;
    }

    private bool IsStrip(WorkspaceLayoutNode node) =>
        node is LayoutTabGroupNode group
        && (_tree.PresentationOf(group.SelectedPanelId).IsCollapsed || !_tree.PresentationOf(group.SelectedPanelId).IsPinned);

    // ----------------------------------------------------------------
    // Auto-hide flyout
    // ----------------------------------------------------------------

    /// <summary>Shows <paramref name="panelId"/>'s own content as a flyout over the layout, without re-docking it.</summary>
    public void ShowFlyout(Guid panelId)
    {
        if (_registry.Find(panelId) is not { } descriptor)
            return;

        LayoutTabGroupView.Detach(descriptor.Content);
        _flyout.Child = descriptor.Content;
        _flyout.Width = 280;
        _flyout.IsVisible = true;
        _flyoutPanelId = panelId;
    }

    /// <summary>Closes the auto-hide flyout, returning its panel to its strip.</summary>
    public void HideFlyout()
    {
        if (!_flyout.IsVisible)
            return;

        _flyout.Child = null;
        _flyout.IsVisible = false;
        _flyoutPanelId = null;
    }

    /// <summary>The panel currently shown in the flyout, or <see langword="null"/>.</summary>
    public Guid? FlyoutPanelId => _flyoutPanelId;

    // ----------------------------------------------------------------
    // Responsive behaviour (`TD-70`, preserved)
    // ----------------------------------------------------------------

    /// <summary>
    /// Collapses whatever must give way for the largest pane to stay
    /// usable at <paramref name="availableWidth"/> by
    /// <paramref name="availableHeight"/>, and re-expands panels once the
    /// room comes back.
    /// </summary>
    /// <remarks>
    /// The `TD-70` guarantee, carried forward: the pane the engineer
    /// actually works in never gets squeezed below a usable width by side
    /// panels. Expressed against the tree rather than three named docks, so
    /// it applies to any arrangement the user builds, however deeply
    /// nested — which the fixed grid could not do.
    /// </remarks>
    public void ApplyResponsiveLayout(double availableWidth, double availableHeight)
    {
        // Applying a responsive change re-renders, which raises SizeChanged
        // again. Without this guard the first resize would recurse.
        if (_applyingResponsive || _tree.Root is not LayoutSplitNode root)
            return;

        var horizontal = root.Orientation == LayoutOrientation.Horizontal;
        var available = horizontal ? availableWidth : availableHeight;
        var minimum = horizontal ? MinPrimaryPaneWidth : MinPrimaryPaneHeight;

        if (available <= 0)
            return;

        // The widest child is the working pane; every other child is a side
        // panel that can be asked to give way.
        var primaryIndex = root.Weights.ToList().IndexOf(root.Weights.Max());
        var updated = _tree;

        for (var i = 0; i < root.Children.Count; i++)
        {
            if (i == primaryIndex || root.Children[i] is not LayoutTabGroupNode group)
                continue;

            var panelId = group.SelectedPanelId;
            var share = root.Weights[i] * available;
            var primaryShare = root.Weights[primaryIndex] * available;
            var presentation = updated.PresentationOf(panelId);

            var mustCollapse = primaryShare < minimum || share < MinUsablePanelSize;

            if (mustCollapse && !presentation.IsCollapsed)
                updated = updated.SetCollapsed(panelId, true);
            else if (!mustCollapse && presentation.IsCollapsed && _autoCollapsed.Contains(panelId))
                updated = updated.SetCollapsed(panelId, false);

            if (mustCollapse)
                _autoCollapsed.Add(panelId);
            else
                _autoCollapsed.Remove(panelId);
        }

        if (updated == _tree)
            return;

        _applyingResponsive = true;
        try
        {
            Apply(_ => updated);
        }
        finally
        {
            _applyingResponsive = false;
        }
    }

    private bool _applyingResponsive;

    /// <summary>
    /// Panels this control collapsed on the user's behalf, so growing the
    /// window restores them — and one the user collapsed themselves is
    /// left collapsed, because that was their decision, not the layout's.
    /// </summary>
    private readonly HashSet<Guid> _autoCollapsed = [];
}
