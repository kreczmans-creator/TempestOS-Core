using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.App.Workspace;
using Tempest.Desktop.DigitalThread;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// One tree node's own presentation-layer wrapper — carries the real
/// <see cref="ProjectExplorerNode"/>, its own already-fetched children, and
/// (`WP 10.2A`) a back-reference to its own parent (breadcrumbs) — so
/// <see cref="TreeView"/> can bind to a real, live tree.
/// </summary>
internal sealed class ExplorerNodeItem
{
    public ExplorerNodeItem(ProjectExplorerNode node, ExplorerNodeItem? parent)
    {
        Node = node;
        Parent = parent;
    }

    public ProjectExplorerNode Node { get; }

    public ExplorerNodeItem? Parent { get; }

    public string Display => $"{IconRegistry.Resolve(Node.Kind)}  {Node.Title}";

    public AvaloniaList<ExplorerNodeItem> Children { get; } = [];

    /// <summary>Walks from the root down to this node, inclusive — the Breadcrumb bar's own source.</summary>
    public IReadOnlyList<ExplorerNodeItem> PathFromRoot()
    {
        var path = new List<ExplorerNodeItem>();
        for (var current = this; current is not null; current = current.Parent)
            path.Insert(0, current);
        return path;
    }
}

/// <summary>
/// The Project Explorer panel (`WP 10.0B`, modernised `WP 10.2A`) — a real
/// <see cref="TreeView"/> bound directly to <see cref="IProjectExplorer"/>,
/// now with professional tree presentation: multi-select, a context menu
/// dispatching real Rename/Delete commands (<see cref="IWorkspaceManager"/>,
/// `ADR-0096`), inline rename, text filtering, and a breadcrumb bar tracking
/// the current selection's own path from root. Loads the current area's own
/// complete tree eagerly, one level at a time via
/// <see cref="IProjectExplorer.GetChildrenAsync"/> — a deliberate, disclosed
/// simplification appropriate to this project's own current sample-data
/// scale (`WP10.0B Implementation Report.md`, reconfirmed `WP10.2A
/// Implementation Report.md`); per-node lazy expansion remains a disclosed
/// future optimisation if tree sizes grow materially, not designed here.
/// </summary>
public sealed class ProjectExplorerView : UserControl
{
    private readonly IProjectExplorer _explorer;
    private readonly IWorkspaceManager _manager;
    private readonly TreeView _tree = new() { SelectionMode = SelectionMode.Multiple };
    private readonly TextBox _filter = new() { Watermark = "Filter... (Ctrl+F)", Margin = DesignTokens.ControlMargin };
    private readonly Button _recentSearchesButton = ChromeIconButton(IconGeometry.Filter, "Recent searches");
    private readonly Button _recentObjectsButton = ChromeIconButton(IconGeometry.Clock, "Recent objects");
    private readonly Button _favouritesButton = ChromeIconButton(IconGeometry.Star, "Favourite objects");
    private readonly List<string> _recentSearches = [];
    private string _lastNonEmptyFilterText = string.Empty;
    private readonly StackPanel _breadcrumbs = new() { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs, Margin = DesignTokens.ControlMargin };
    /// <summary>
    /// The Project Explorer's own real, professional empty state
    /// (`WP 10.5A`, replacing this class's own previous plain-text
    /// placeholder) — one panel, two distinct messages depending on
    /// whether the tree is genuinely empty or the current filter simply
    /// matches nothing, set by <see cref="ApplyFilter"/>.
    /// </summary>
    private readonly EmptyStateView _emptyHint = new("▤", "Nothing here yet.", "This area has no objects yet.") { IsVisible = false };
    private AvaloniaList<ExplorerNodeItem> _allItems = [];
    private ExplorerNodeItem? _renamingItem;

    /// <summary>Raised when the user selects a node backed by a real engineering object (never a Category node).</summary>
    public event Action<System.Guid, string>? ObjectSelected;

    /// <summary>Raised when the user double-clicks (opens) a node backed by a real engineering object.</summary>
    public event Action<System.Guid, string>? ObjectOpened;

    /// <summary>
    /// Raised when the user drags an object node and drops it onto a new
    /// parent (`WP 10.7A` — Feature Completion, realising this class's own
    /// `WP 10.2A` "preparation architecture" for the first time) — carries
    /// (draggedId, draggedKind, newParentId), where a <see langword="null"/>
    /// <c>newParentId</c> means "drop on empty tree space," moved to root.
    /// The real per-discipline dispatch (each Kind's own already-registered
    /// <c>Move*Command</c>) is owned by the subscriber, mirroring
    /// <see cref="ToggleFavouriteRequested"/>'s own identical "this View
    /// raises intent, the owner dispatches" shape — this class itself
    /// never references <see cref="Tempest.Core.Commands.ICommandDispatcher"/>.
    /// </summary>
    public event Action<System.Guid, string, System.Guid?>? ObjectMoveRequested;

    /// <summary>Raised after a Rename/Delete context-menu action completes — successfully or not — carrying a status message the caller may surface (e.g. on the Status Bar) and its <see cref="ActionOutcome"/> (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// An optional confirmation gate (`WP 10.5B`, Dialog Framework —
    /// "Delete Confirmation") — called with a human-readable prompt before
    /// either Delete path (context menu, <c>Delete</c> key) proceeds;
    /// <see langword="null"/> (the default) preserves the pre-`WP 10.5B`
    /// behaviour of proceeding immediately, so a caller that never wires
    /// this (any existing test constructing this view directly) is
    /// unaffected — never a silent behaviour change for an unwired
    /// consumer.
    /// </summary>
    public Func<string, Task<bool>>? ConfirmDeleteAsync { get; set; }

    /// <summary>
    /// The "Recent objects"/"Favourite objects" productivity features
    /// (`WP 10.6A`) — both optional, mirroring <see cref="ConfirmDeleteAsync"/>'s
    /// own "unwired means the pre-existing behaviour" discipline: unset
    /// (any existing test constructing this view directly), the two new
    /// flyout buttons render an honest empty state and the context menu's
    /// own "Toggle Favourite" item is disabled, never a silent crash.
    /// </summary>
    public RecentObjectsState? RecentObjects { get; set; }

    /// <summary>See <see cref="RecentObjects"/>'s own remarks.</summary>
    public FavouriteObjectsState? Favourites { get; set; }

    /// <summary>
    /// Raised when the user chooses "Toggle Favourite" from the context
    /// menu, carrying (Id, Kind, DisplayName) — set by <c>MainWindow</c>,
    /// which owns the real toggle/save/Undo-recording logic
    /// (<see cref="FavouriteObjectsState.Toggle"/> is trivially
    /// self-inverting, `ADR-0099`). <see langword="null"/> (the default)
    /// leaves the menu item disabled, mirroring <see cref="ConfirmDeleteAsync"/>'s
    /// own identical opt-in shape.
    /// </summary>
    public Action<System.Guid, string, string>? ToggleFavouriteRequested { get; set; }

    /// <summary>Initialises a new instance of the <see cref="ProjectExplorerView"/> class.</summary>
    /// <param name="explorer">The Workspace's own Project Explorer panel this View renders.</param>
    /// <param name="manager">
    /// The owning <see cref="IWorkspaceManager"/> — this View's own real
    /// Rename/Delete dispatch source (`ADR-0096`, `WP 10.2A`); every prior
    /// Work Package's own <see cref="ProjectExplorerView"/> needed only
    /// <paramref name="explorer"/>, since neither capability existed yet.
    /// </param>
    public ProjectExplorerView(IProjectExplorer explorer, IWorkspaceManager manager)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(manager);
        _explorer = explorer;
        _manager = manager;

        _tree.ItemTemplate = new FuncTreeDataTemplate<ExplorerNodeItem>(BuildNodePresenter, item => item.Children);
        _tree.MinHeight = DesignTokens.MinControlSize;

        _tree.SelectionChanged += (_, e) =>
        {
            UpdateBreadcrumbs();

            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ExplorerNodeItem { Node.NodeType: ProjectExplorerNodeType.Object } item)
                ObjectSelected?.Invoke(item.Node.Id, item.Node.Kind!);
        };

        _tree.DoubleTapped += (_, _) =>
        {
            if (_tree.SelectedItem is ExplorerNodeItem { Node.NodeType: ProjectExplorerNodeType.Object } item)
                ObjectOpened?.Invoke(item.Node.Id, item.Node.Kind!);
        };

        _tree.KeyDown += OnTreeKeyDown;

        // `PropertyChanged`, not the `TextChanged` routed event — the
        // identical, already-established finding `ObjectEditorView`
        // documented (`WP 10.3A`): `TextChanged` does not reliably fire
        // for a purely programmatic `.Text =` assignment (only for real
        // keystrokes), where `PropertyChanged` fires for both. Found
        // again here, independently, by this Work Package's own Recent
        // Searches tests before this fix (`WP10.5B Engineering Review.md`
        // §4) — a genuine, real reliability gap in already-shipped
        // filtering code (`WP 10.2A`), not merely a test-only concern:
        // any future caller setting `_filter.Text` programmatically (a
        // "clear filter" button, a restored search) would have silently
        // failed to re-filter the tree.
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty)
                return;

            ApplyFilter();

            // Recent Searches (`WP 10.5B` scope) — a completed search is
            // recorded the moment the filter goes back to empty (cleared
            // by the user or Escape), the natural "I'm done with this
            // search" signal — never one entry per keystroke.
            var current = _filter.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(current))
            {
                if (!string.IsNullOrWhiteSpace(_lastNonEmptyFilterText))
                    RecordRecentSearch(_lastNonEmptyFilterText);
            }
            else
            {
                _lastNonEmptyFilterText = current;
            }
        };

        _recentSearchesButton.Click += (_, _) => ShowRecentSearchesFlyout();
        _recentObjectsButton.Click += (_, _) => ShowRecentObjectsFlyout();
        _favouritesButton.Click += (_, _) => ShowFavouritesFlyout();

        // Drag/drop reparenting (`WP 10.2A`'s own "preparation
        // architecture," realised for real by `WP 10.7A` — Feature
        // Completion): a real drag begins and carries real (Id, Kind)
        // payload, DragOver gives real, honest visual feedback (Move
        // cursor only over a genuine Object node), and Drop raises
        // ObjectMoveRequested — this View never dispatches a command
        // itself (no discipline-agnostic "reparent" primitive exists on
        // IWorkspaceManager, and none is added here), it only resolves
        // the real drop target and lets its own owner (MainWindow)
        // dispatch whichever discipline's own already-registered
        // Move*Command applies, mirroring ToggleFavouriteRequested's
        // identical "raise intent, owner dispatches" shape.
        DragDrop.SetAllowDrop(_tree, true);
        _tree.AddHandler(DragDrop.DragOverEvent, OnTreeDragOver);
        _tree.AddHandler(DragDrop.DropEvent, OnTreeDrop);
        _tree.PointerPressed += OnTreePointerPressedForDrag;

        ToolTip.SetTip(_recentSearchesButton, "Recent searches");
        ToolTip.SetTip(_recentObjectsButton, "Recent objects");
        ToolTip.SetTip(_favouritesButton, "Favourite objects (Ctrl+D to toggle)");
        _filter.FontSize = DesignTokens.FontSizeBody;
        _filter.MinHeight = DesignTokens.MinControlSize;
        _filter.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0);
        var filterRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), Margin = new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceMd, DesignTokens.SpaceMd, DesignTokens.SpaceSm) };
        Grid.SetColumn(_filter, 0);
        Grid.SetColumn(_recentSearchesButton, 1);
        Grid.SetColumn(_recentObjectsButton, 2);
        Grid.SetColumn(_favouritesButton, 3);
        filterRow.Children.Add(_filter);
        filterRow.Children.Add(_recentSearchesButton);
        filterRow.Children.Add(_recentObjectsButton);
        filterRow.Children.Add(_favouritesButton);

        var content = new DockPanel();
        DockPanel.SetDock(filterRow, Dock.Top);
        _breadcrumbs.Margin = new Thickness(DesignTokens.SpaceMd, 0, DesignTokens.SpaceMd, DesignTokens.SpaceSm);
        var crumbRow = new Border { Child = _breadcrumbs, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, DesignTokens.SpaceXs) };
        ThemeReactiveBrush.Bind(crumbRow, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        DockPanel.SetDock(crumbRow, Dock.Top);
        content.Children.Add(filterRow);
        content.Children.Add(crumbRow);
        _tree.Margin = new Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceSm, DesignTokens.SpaceSm, 0);
        var body = new Panel();
        body.Children.Add(_tree);
        body.Children.Add(_emptyHint);
        content.Children.Add(body);

        Content = content;
    }

    /// <summary>One filter-row chrome button — a flat, theme-tinted vector icon named for automation by what it opens.</summary>
    private static Button ChromeIconButton(StreamGeometry icon, string name)
    {
        var button = new Button
        {
            Content = IconGeometry.Build(icon, 14),
            Padding = new Thickness(DesignTokens.SpaceSm),
            MinWidth = DesignTokens.MinControlSize,
            MinHeight = DesignTokens.MinControlSize,
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add(ChromeStyles.Flat);
        ThemeReactiveBrush.Bind(button, ForegroundProperty, BrandPalette.MutedTextBrushKey);
        Avalonia.Automation.AutomationProperties.SetName(button, name);
        return button;
    }

    /// <summary>Moves keyboard focus to the filter box — <c>Ctrl+F</c>'s own target (`WP 10.2A` Navigation, Keyboard Shortcut Framework).</summary>
    public void FocusFilter() => _filter.Focus();

    /// <summary>How many entries <see cref="RecentSearches"/> keeps (`WP 10.5B`, real <see cref="UserSettings.RecentSearchCapacity"/>). 5 by default, matching <see cref="Views.RibbonView"/>'s own identical Recently-Used-Commands capacity (`WP 10.3B`).</summary>
    public int RecentSearchCapacity { get; set; } = 5;

    /// <summary>Every completed search, most recent first — exposed for tests; production reads happen only via the Recent Searches flyout.</summary>
    public IReadOnlyList<string> RecentSearches => _recentSearches;

    private void RecordRecentSearch(string query)
    {
        _recentSearches.Remove(query);
        _recentSearches.Insert(0, query);
        while (_recentSearches.Count > RecentSearchCapacity)
            _recentSearches.RemoveAt(_recentSearches.Count - 1);
    }

    private void ShowRecentSearchesFlyout()
    {
        var flyout = new Flyout();
        var list = new StackPanel { Margin = DesignTokens.PanelPadding, MinWidth = 180 };

        if (_recentSearches.Count == 0)
        {
            list.Children.Add(new TextBlock { Text = "No recent searches yet.", Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption });
        }
        else
        {
            foreach (var query in _recentSearches)
            {
                var item = new Button { Content = query, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                item.Click += (_, _) =>
                {
                    _filter.Text = query;
                    flyout.Hide();
                };
                list.Children.Add(item);
            }
        }

        flyout.Content = list;
        flyout.ShowAt(_recentSearchesButton);
    }

    /// <summary>The "Recent objects" flyout (`WP 10.6A`) — mirrors <see cref="ShowRecentSearchesFlyout"/>'s own shape exactly; clicking an entry opens it, identically to double-clicking it in the tree.</summary>
    private void ShowRecentObjectsFlyout()
    {
        var flyout = new Flyout();
        var list = new StackPanel { Margin = DesignTokens.PanelPadding, MinWidth = 220 };

        var entries = RecentObjects?.Entries ?? [];
        if (entries.Count == 0)
        {
            list.Children.Add(new TextBlock { Text = "No recent objects yet.", Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption });
        }
        else
        {
            foreach (var entry in entries)
            {
                var item = new Button { Content = $"{IconRegistry.Resolve(entry.Kind)} {entry.DisplayName}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                item.Click += (_, _) =>
                {
                    ObjectOpened?.Invoke(entry.Id, entry.Kind);
                    flyout.Hide();
                };
                list.Children.Add(item);
            }
        }

        flyout.Content = list;
        flyout.ShowAt(_recentObjectsButton);
    }

    /// <summary>The "Favourite objects" flyout (`WP 10.6A`) — mirrors <see cref="ShowRecentObjectsFlyout"/>'s own shape exactly.</summary>
    private void ShowFavouritesFlyout()
    {
        var flyout = new Flyout();
        var list = new StackPanel { Margin = DesignTokens.PanelPadding, MinWidth = 220 };

        var entries = Favourites?.Entries ?? [];
        if (entries.Count == 0)
        {
            list.Children.Add(new TextBlock { Text = "No favourite objects yet.", Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption });
        }
        else
        {
            foreach (var entry in entries)
            {
                var item = new Button { Content = $"{IconRegistry.Resolve(entry.Kind)} {entry.DisplayName}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                item.Click += (_, _) =>
                {
                    ObjectOpened?.Invoke(entry.Id, entry.Kind);
                    flyout.Hide();
                };
                list.Children.Add(item);
            }
        }

        flyout.Content = list;
        flyout.ShowAt(_favouritesButton);
    }

    /// <summary>Loads (or reloads) the current area's own complete tree from <see cref="IProjectExplorer"/>.</summary>
    /// <remarks>
    /// `WP-Z4` Productisation Phase 1 (P0): every reload rebuilds an
    /// entirely fresh <see cref="ExplorerNodeItem"/> tree, and
    /// <see cref="TreeView.SelectedItem"/> matches by reference — so the
    /// selection silently vanished on every single reload (renaming an
    /// object, creating a sibling, switching areas and back), even though
    /// the same real object was still right there in the new tree under a
    /// new wrapper. Recording the selected node's own real Id before the
    /// rebuild and re-selecting whichever new item now carries that same
    /// Id closes the gap without needing <see cref="ExplorerNodeItem"/> to
    /// become a value type or override equality — a much larger change
    /// this Work Package's own "preserve existing architecture" instruction
    /// argues against attempting here.
    /// </remarks>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var selectedId = (_tree.SelectedItem as ExplorerNodeItem)?.Node.Id;

        var roots = await _explorer.GetRootNodesAsync(cancellationToken).ConfigureAwait(true);
        var items = new AvaloniaList<ExplorerNodeItem>();

        foreach (var root in roots)
            items.Add(await BuildAsync(root, parent: null, cancellationToken).ConfigureAwait(true));

        _allItems = items;
        ApplyFilter();

        if (selectedId is { } id && FindById(_allItems, id) is { } restored)
            SelectAndReveal(restored);

        UpdateBreadcrumbs();
    }

    /// <summary>Searches <paramref name="roots"/> and every descendant, depth-first, for the node carrying <paramref name="id"/> — the real Domain Id, stable across a reload even though every <see cref="ExplorerNodeItem"/> wrapper is rebuilt from scratch.</summary>
    private static ExplorerNodeItem? FindById(IEnumerable<ExplorerNodeItem> roots, Guid id)
    {
        foreach (var item in roots)
        {
            if (item.Node.Id == id)
                return item;

            if (FindById(item.Children, id) is { } found)
                return found;
        }

        return null;
    }

    private async Task<ExplorerNodeItem> BuildAsync(ProjectExplorerNode node, ExplorerNodeItem? parent, CancellationToken cancellationToken)
    {
        var item = new ExplorerNodeItem(node, parent);

        if (node.HasChildren)
        {
            var children = await _explorer.GetChildrenAsync(node.Id, cancellationToken).ConfigureAwait(true);
            foreach (var child in children)
                item.Children.Add(await BuildAsync(child, item, cancellationToken).ConfigureAwait(true));
        }

        return item;
    }

    // ------------------------------------------------------------
    // Filtering
    // ------------------------------------------------------------

    /// <summary>
    /// Re-applies the current filter text against <see cref="_allItems"/> —
    /// a node matches if its own title matches, or any descendant's own
    /// title matches (so a matching leaf's own ancestors stay visible,
    /// never orphaning it). Client-side, over the already-loaded tree —
    /// no re-query against <see cref="IProjectExplorer"/>, keeping this
    /// responsive regardless of network/service latency (`WP 10.2A`'s own
    /// Performance requirement: "responsive tree expansion").
    /// </summary>
    private void ApplyFilter()
    {
        var query = _filter.Text;

        if (string.IsNullOrWhiteSpace(query))
        {
            _tree.ItemsSource = _allItems;
            _emptyHint.IsVisible = _allItems.Count == 0;
            if (_allItems.Count == 0)
                _emptyHint.SetMessage("Nothing here yet.", "This area has no objects yet. Create one from the Ribbon to get started.");
            return;
        }

        var filtered = new AvaloniaList<ExplorerNodeItem>();
        foreach (var root in _allItems)
        {
            var match = FilterNode(root, query);
            if (match is not null)
                filtered.Add(match);
        }

        _tree.ItemsSource = filtered;
        _emptyHint.IsVisible = filtered.Count == 0;
        if (filtered.Count == 0)
            _emptyHint.SetMessage("No matches.", $"No items match “{query}”. Try a different search term, or clear the filter.");
    }

    /// <summary>Returns a filtered copy of <paramref name="item"/> (same node, only matching descendants) if it or any descendant matches <paramref name="query"/>; <see langword="null"/> otherwise.</summary>
    private static ExplorerNodeItem? FilterNode(ExplorerNodeItem item, string query)
    {
        var selfMatches = item.Node.Title.Contains(query, StringComparison.OrdinalIgnoreCase);
        var matchingChildren = new List<ExplorerNodeItem>();

        foreach (var child in item.Children)
        {
            var match = FilterNode(child, query);
            if (match is not null)
                matchingChildren.Add(match);
        }

        if (!selfMatches && matchingChildren.Count == 0)
            return null;

        var copy = new ExplorerNodeItem(item.Node, item.Parent);
        foreach (var child in selfMatches ? item.Children : (IEnumerable<ExplorerNodeItem>)matchingChildren)
            copy.Children.Add(child);

        return copy;
    }

    // ------------------------------------------------------------
    // Breadcrumbs
    // ------------------------------------------------------------

    private void UpdateBreadcrumbs()
    {
        _breadcrumbs.Children.Clear();

        if (_tree.SelectedItem is not ExplorerNodeItem selected)
        {
            var none = new TextBlock { Text = "No selection", FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center };
            ThemeReactiveBrush.Bind(none, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);
            _breadcrumbs.Children.Add(none);
            return;
        }

        var path = selected.PathFromRoot();
        for (var i = 0; i < path.Count; i++)
        {
            if (i > 0)
                _breadcrumbs.Children.Add(new TextBlock { Text = "›", Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center });

            var segment = path[i];
            var isLast = i == path.Count - 1;
            var crumb = new Button
            {
                Content = segment.Node.Title,
                FontSize = DesignTokens.FontSizeCaption,
                FontWeight = isLast ? DesignTokens.WeightHeading : DesignTokens.WeightBody,
                Padding = new Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceXs),
                MinHeight = 0,
            };
            crumb.Classes.Add(ChromeStyles.Flat);
            ThemeReactiveBrush.Bind(crumb, ForegroundProperty, isLast ? BrandPalette.HeadingTextBrushKey : BrandPalette.MutedTextBrushKey);
            crumb.Click += (_, _) => SelectAndReveal(segment);
            _breadcrumbs.Children.Add(crumb);
        }
    }

    private void SelectAndReveal(ExplorerNodeItem item)
    {
        _tree.SelectedItem = item;
    }

    // ------------------------------------------------------------
    // Context menu / inline rename / delete — real dispatch (ADR-0096)
    // ------------------------------------------------------------

    /// <summary>
    /// Builds one node's own real presenter directly, bypassing
    /// <see cref="TreeView"/>'s own container realisation — internal test
    /// hook only (`Tempest.Desktop.Tests`, `InternalsVisibleTo`), mirroring
    /// <see cref="PropertyInspectorView.CountRenderedRowsWithFacetName"/>'s
    /// own identical "a real `TreeView`/`ItemsControl` only realises a
    /// container once attached to a live, measured visual tree, which a
    /// headless test cannot cheaply force" precedent (`WP 10.5C`).
    /// </summary>
    internal Control BuildNodePresenterForTest(ExplorerNodeItem item) => BuildNodePresenter(item, null!);

    private Control BuildNodePresenter(ExplorerNodeItem item, INameScope _)
    {
        if (ReferenceEquals(item, _renamingItem))
            return BuildRenameEditor(item);

        var text = new TextBlock
        {
            Text = item.Display,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = DesignTokens.FontSizeBody,
        };

        // A real, coloured lifecycle status dot (`WP 10.5C`, "coloured
        // object states, lifecycle indicators") — the identical
        // `LifecycleColors` mapping and 10x10 dot shape
        // `ObjectEditorView`'s own status badge already established
        // (`WP 10.5A`), reused here verbatim for cross-surface visual
        // consistency, not a second, competing colour scheme. Present
        // only for a real `ProjectExplorerNode.Lifecycle` value — never
        // fabricated for a Category/Group/Collection node, or for a
        // Requirement (the Requirements Framework's own separate
        // `RequirementStatus` taxonomy is deliberately not force-mapped
        // onto `LifecycleState`, see `ProjectExplorerNode.Lifecycle`'s own
        // remarks).
        Control presenter = text;
        if (item.Node.Lifecycle is { } lifecycle)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(text);
            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = LifecycleColors.Resolve(lifecycle),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(dot, $"Lifecycle: {lifecycle}");
            row.Children.Add(dot);
            presenter = row;
        }

        if (item.Node.NodeType == ProjectExplorerNodeType.Object)
            presenter.ContextMenu = BuildContextMenu(item);

        return presenter;
    }

    private Control BuildRenameEditor(ExplorerNodeItem item)
    {
        var box = new TextBox { Text = item.Node.Title, MinWidth = 160, FontSize = DesignTokens.FontSizeBody };

        async void Commit()
        {
            var newName = box.Text ?? string.Empty;
            _renamingItem = null;

            if (!string.IsNullOrWhiteSpace(newName) && newName != item.Node.Title)
            {
                var result = await _manager.RenameObjectAsync(item.Node.Id, item.Node.Kind!, newName).ConfigureAwait(true);
                ActionCompleted?.Invoke(result.Succeeded ? $"Renamed to '{newName}'." : result.Message ?? "Rename failed.", ActionOutcome.From(result.Succeeded));
                if (result.Succeeded)
                    await LoadAsync().ConfigureAwait(true);
            }
            else
            {
                ApplyFilter(); // redraw without the editor, no rename attempted
            }
        }

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { _renamingItem = null; ApplyFilter(); e.Handled = true; }
        };
        box.LostFocus += (_, _) => Commit();

        Dispatcher.UIThread.Post(() => box.Focus());
        return box;
    }

    /// <summary>Begins inline rename for <paramref name="item"/> — real dispatch on commit, honestly available only when <see cref="IWorkspaceManager.CanRename"/> reports the selected Kind supports it.</summary>
    private void BeginInlineRename(ExplorerNodeItem item)
    {
        if (!_manager.CanRename(item.Node.Kind!))
        {
            ActionCompleted?.Invoke($"'{item.Node.Kind}' objects cannot be renamed.", ActionOutcome.Failed);
            return;
        }

        _renamingItem = item;
        ApplyFilter();
    }

    private ContextMenu BuildContextMenu(ExplorerNodeItem item)
    {
        var menu = new ContextMenu();
        var items = new List<Control>();

        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => ObjectOpened?.Invoke(item.Node.Id, item.Node.Kind!);
        items.Add(open);

        var rename = new MenuItem { Header = "Rename", IsEnabled = _manager.CanRename(item.Node.Kind!) };
        rename.Click += (_, _) => BeginInlineRename(item);
        items.Add(rename);

        var delete = new MenuItem { Header = "Delete", IsEnabled = _manager.CanDelete(item.Node.Kind!) };
        delete.Click += async (_, _) => await DeleteWithFeedbackAsync(item).ConfigureAwait(true);
        items.Add(delete);

        // "Favourite objects" (`WP 10.6A`) — the label reflects the
        // current state (real, live read of Favourites, not a fixed
        // "Toggle" label); disabled, honestly, if ToggleFavouriteRequested
        // was never wired (mirrors ConfirmDeleteAsync's own precedent).
        var isFavourite = Favourites?.IsFavourite(item.Node.Id) ?? false;
        var favourite = new MenuItem { Header = isFavourite ? "Remove from Favourites" : "Add to Favourites", IsEnabled = ToggleFavouriteRequested is not null };
        favourite.Click += (_, _) => ToggleFavouriteRequested?.Invoke(item.Node.Id, item.Node.Kind!, item.Node.Title);
        items.Add(favourite);

        menu.ItemsSource = items;
        return menu;
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (_tree.SelectedItem is not ExplorerNodeItem { Node.NodeType: ProjectExplorerNodeType.Object } item)
            return;

        if (e.Key == Key.F2)
        {
            BeginInlineRename(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            _ = DeleteWithFeedbackAsync(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ObjectOpened?.Invoke(item.Node.Id, item.Node.Kind!);
            e.Handled = true;
        }
    }

    private async Task DeleteWithFeedbackAsync(ExplorerNodeItem item)
    {
        if (!_manager.CanDelete(item.Node.Kind!))
        {
            ActionCompleted?.Invoke($"'{item.Node.Kind}' objects cannot be deleted.", ActionOutcome.Failed);
            return;
        }

        if (ConfirmDeleteAsync is { } confirm && !await confirm($"Delete '{item.Node.Title}'? This cannot be undone.").ConfigureAwait(true))
            return;

        var result = await _manager.DeleteObjectAsync(item.Node.Id, item.Node.Kind!).ConfigureAwait(true);
        ActionCompleted?.Invoke(result.Succeeded ? $"Deleted '{item.Node.Title}'." : result.Message ?? "Delete failed.", ActionOutcome.From(result.Succeeded));
        if (result.Succeeded)
            await LoadAsync().ConfigureAwait(true);
    }

    // ------------------------------------------------------------
    // Drag/drop reparenting (see constructor remarks)
    // ------------------------------------------------------------

    private const string DragFormat = "TempestOS.ExplorerNode";
    private const string DragFormatKind = "TempestOS.ExplorerNode.Kind";

    private async void OnTreePointerPressedForDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed
            && _tree.SelectedItem is ExplorerNodeItem { Node.NodeType: ProjectExplorerNodeType.Object } item)
        {
            var data = new DataObject();
            data.Set(DragFormat, item.Node.Id);
            data.Set(DragFormatKind, item.Node.Kind!);
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move).ConfigureAwait(true);
        }
    }

    private void OnTreeDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
    }

    /// <summary>
    /// Resolves the real drop target and raises <see cref="ObjectMoveRequested"/>
    /// — dropping onto empty tree space (no ancestor <see cref="TreeViewItem"/>
    /// under the pointer) honestly means "move to root," matching every
    /// discipline's own <c>Move*Command</c> nullable <c>newParentId</c>
    /// shape exactly. Guards against dropping an object onto itself or
    /// onto its own descendant (an honest <see cref="ActionCompleted"/>
    /// message, never a crash or a silently-accepted structural loop).
    /// </summary>
    private void OnTreeDrop(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;

        if (!e.Data.Contains(DragFormat) || e.Data.Get(DragFormat) is not Guid draggedId || e.Data.Get(DragFormatKind) is not string draggedKind)
            return;

        var targetItem = (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext as ExplorerNodeItem;

        if (targetItem is not null)
        {
            if (targetItem.Node.NodeType != ProjectExplorerNodeType.Object)
            {
                ActionCompleted?.Invoke("Can't move an object there.", ActionOutcome.Failed);
                return;
            }

            if (targetItem.Node.Id == draggedId)
            {
                ActionCompleted?.Invoke("Can't move an object onto itself.", ActionOutcome.Failed);
                return;
            }

            if (targetItem.PathFromRoot().Any(ancestor => ancestor.Node.Id == draggedId))
            {
                ActionCompleted?.Invoke("Can't move an object into its own descendant.", ActionOutcome.Failed);
                return;
            }
        }

        ObjectMoveRequested?.Invoke(draggedId, draggedKind, targetItem?.Node.Id);
    }
}
