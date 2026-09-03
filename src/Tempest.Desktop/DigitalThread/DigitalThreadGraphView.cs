using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.DigitalThread;

/// <summary>
/// The Digital Thread graph's own real, interactive node-link viewer
/// (`WP 10.4A`, realising `ADR-0093` and
/// `WP10.0A Digital Thread &amp; Relationship Visualisation.md`) — every
/// named scope item (Interactive relationship graph, Node-link
/// visualisation, Expand/collapse, Relationship filtering/highlighting,
/// Selected object centring, Zoom, Pan, Mini-map, Relationship
/// categories, Object icons, Object status indicators, Graph legend,
/// Object search, Click-to-open object editor, Double-click navigation,
/// Relationship inspector, Breadcrumb path display, Multiple graph
/// layouts) is a real, working control here — never a mock-up. All state
/// and algorithms live in <see cref="DigitalThreadGraphModel"/>; this
/// class only renders that model and forwards user gestures to it,
/// rebuilding its own visual tree from scratch on every model change
/// (mirroring <see cref="Views.RibbonView"/>'s own identical
/// "Rebuild()" discipline, `WP 10.3B`) — never hand-patched incrementally.
/// </summary>
/// <remarks>
/// Implements <see cref="IWorkspaceView"/> directly (a Document Area tab,
/// rather than its own dockable panel) — originally a minimal-footprint
/// choice, because the docking geometry of the day had exactly three
/// fixed slots and adding a fourth would have been a Workspace redesign
/// (`WP10.4A Architecture Review.md` §1). That constraint is gone:
/// `TD-72` made the layout an arbitrary tree, so this view could now
/// register as an ordinary panel and take part in docking, tabbing,
/// splitting and floating like any other. It remains a Document tab
/// because that is where it belongs in the workflow, not because the
/// layout forces it.
/// </remarks>
public sealed class DigitalThreadGraphView : UserControl, IWorkspaceView
{
    private const double NodeWidth = 158;
    private const double NodeHeight = 54;
    private const double CentreNodeWidth = 188;
    private const double CentreNodeHeight = 68;
    private const double CanvasOrigin = 1600;
    private const double CanvasSize = 3200;
    private const double MiniMapWidth = 168;
    private const double MiniMapHeight = 120;

    private readonly DigitalThreadGraphModel _model;
    private readonly EngineeringDomainContext _domainContext;
    private readonly Action<Guid, string> _navigateToObject;

    private readonly TextBlock _titleBlock = new() { FontSize = DesignTokens.FontSizeTitle, FontWeight = FontWeight.Bold };
    private readonly TextBox _searchBox = new() { Watermark = "🔎 Search this graph…", MinWidth = 200, MinHeight = DesignTokens.MinControlSize };
    private readonly ComboBox _layoutSelector = new() { MinHeight = DesignTokens.MinControlSize, MinWidth = 150 };
    private readonly StackPanel _breadcrumbBar = new() { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
    private readonly Canvas _graphCanvas = new() { Width = CanvasSize, Height = CanvasSize };
    private readonly Border _viewport = new() { ClipToBounds = true, Background = Brushes.Transparent };
    private readonly Canvas _miniMap = new() { Width = MiniMapWidth, Height = MiniMapHeight };
    private readonly Border _miniMapViewportRect = new() { BorderBrush = new ImmutableSolidColorBrush(Theming.BrandPalette.Cyan500), BorderThickness = new Thickness(1.5), IsHitTestVisible = false };
    private readonly StackPanel _legendPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _inspectorPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly TranslateTransform _panTransform = new();
    private readonly ScaleTransform _zoomTransform = new();

    private bool _isDragging;
    private Point _dragStart;
    private Vector _panAtDragStart;

    /// <summary>Raised after any action completes, carrying a human-readable status message and its <see cref="ActionOutcome"/> — the caller's own hook to refresh the Status Bar, mirroring every other Desktop View's own identical convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    private DigitalThreadGraphView(DigitalThreadGraphModel model, EngineeringDomainContext domainContext, Action<Guid, string> navigateToObject)
    {
        _model = model;
        _domainContext = domainContext;
        _navigateToObject = navigateToObject;

        Content = BuildLayout();

        _searchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _model.SetSearchText(_searchBox.Text ?? string.Empty);
                Rebuild();
            }
        };

        _layoutSelector.Items.Add(new ComboBoxItem { Content = "Hierarchical", Tag = DigitalThreadLayoutKind.Hierarchical });
        _layoutSelector.Items.Add(new ComboBoxItem { Content = "Force-Directed", Tag = DigitalThreadLayoutKind.ForceDirected });
        _layoutSelector.Items.Add(new ComboBoxItem { Content = "Engineering", Tag = DigitalThreadLayoutKind.Engineering });
        _layoutSelector.SelectedIndex = 0;
        _layoutSelector.SelectionChanged += (_, _) =>
        {
            if (_layoutSelector.SelectedItem is ComboBoxItem { Tag: DigitalThreadLayoutKind kind })
            {
                _model.SetLayout(kind);
                Rebuild();
            }
        };

        _viewport.PointerWheelChanged += (_, e) =>
        {
            _model.ZoomBy(e.Delta.Y > 0 ? 1.1 : 1 / 1.1);
            UpdateTransform();
        };
        _viewport.PointerPressed += (_, e) =>
        {
            if (e.Source != _viewport && e.Source != _graphCanvas)
                return;
            _isDragging = true;
            _dragStart = e.GetPosition(_viewport);
            _panAtDragStart = new Vector(_model.PanOffset.X, _model.PanOffset.Y);
            e.Pointer.Capture(_viewport);
        };
        _viewport.PointerMoved += (_, e) =>
        {
            if (!_isDragging)
                return;
            var current = e.GetPosition(_viewport);
            var delta = current - _dragStart;
            PanTo(_panAtDragStart + new Vector(delta.X, delta.Y));
        };
        _viewport.PointerReleased += (_, e) =>
        {
            _isDragging = false;
            e.Pointer.Capture(null);
        };

        _miniMap.PointerPressed += (_, e) => JumpMiniMap(e.GetPosition(_miniMap));

        Rebuild();
    }

    // ------------------------------------------------------------
    // IWorkspaceView
    // ------------------------------------------------------------

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Title { get; private set; } = "Relationships";

    /// <inheritdoc />
    public Guid ObjectId => _model.CentreId;

    /// <inheritdoc />
    public string ObjectKind { get; private set; } = string.Empty;

    /// <summary>Always <see langword="false"/> — a read-only presentation over the Digital Thread, never a buffered editor (mirrors <see cref="Editors.ObjectEditorView"/>'s own identical <see cref="IWorkspaceView.IsDirty"/> discipline).</summary>
    public bool IsDirty => false;

    /// <inheritdoc />
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _model.Recentre(_model.CentreId, ObjectKind);
        Rebuild();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    // ------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------

    /// <summary>
    /// Attempts to build a real Digital Thread graph centred on
    /// <paramref name="objectId"/> — returns <see langword="null"/> if no
    /// Engineering Domain object with that Id is found, mirroring
    /// <see cref="Editors.ObjectEditorView.TryCreate"/>'s own identical
    /// contract exactly.
    /// </summary>
    public static DigitalThreadGraphView? TryCreate(Guid objectId, string objectKind, EngineeringDomainContext domainContext, Action<Guid, string> navigateToObject)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(navigateToObject);

        var model = new DigitalThreadGraphModel(domainContext);
        return model.Recentre(objectId, objectKind)
            ? new DigitalThreadGraphView(model, domainContext, navigateToObject)
            : null;
    }

    private Control BuildLayout()
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, Margin = DesignTokens.PanelHeaderPadding };
        var resetZoomButton = new Button { Content = "⊙ Reset View", MinHeight = DesignTokens.MinControlSize };
        resetZoomButton.Click += (_, _) => { _model.ResetView(); UpdateTransform(); };
        header.Children.Add(_titleBlock);
        header.Children.Add(_searchBox);
        header.Children.Add(_layoutSelector);
        header.Children.Add(resetZoomButton);

        _graphCanvas.RenderTransform = new TransformGroup { Children = { _zoomTransform, _panTransform } };
        _viewport.Child = _graphCanvas;

        var miniMapHost = new Border
        {
            Width = MiniMapWidth,
            Height = MiniMapHeight,
            Opacity = 0.92,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(DesignTokens.SpaceMd),
            Child = _miniMap,
        };
        // Theme-reactive (`WP 10.5A`) — see the node-fill fix above for the
        // identical reasoning; a fixed black overlay read wrong in Light theme.
        ThemeReactiveBrush.Bind(miniMapHost, Border.BackgroundProperty, ApplicationPalette.OverlayBackgroundBrushKey);
        ThemeReactiveBrush.Bind(miniMapHost, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        _miniMap.Children.Add(_miniMapViewportRect);

        var viewportOverlay = new Panel();
        viewportOverlay.Children.Add(_viewport);
        viewportOverlay.Children.Add(miniMapHost);

        var sidebar = new ScrollViewer
        {
            Width = 260,
            Content = new StackPanel
            {
                Spacing = DesignTokens.SpaceMd,
                Margin = DesignTokens.PanelPadding,
                Children =
                {
                    new TextBlock { Text = "Legend", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading },
                    _legendPanel,
                    new Separator(),
                    new TextBlock { Text = "Relationship Inspector", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading },
                    _inspectorPanel,
                },
            },
        };

        var mainGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(viewportOverlay, 0);
        Grid.SetColumn(sidebar, 1);
        mainGrid.Children.Add(viewportOverlay);
        mainGrid.Children.Add(sidebar);

        var root = new DockPanel();
        DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_breadcrumbBar, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_statusMessage, Avalonia.Controls.Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(_breadcrumbBar);
        root.Children.Add(_statusMessage);
        root.Children.Add(mainGrid);
        return root;
    }

    // ------------------------------------------------------------
    // Public, test-friendly wrappers — avoid needing to simulate real
    // pointer drag/wheel events in a headless test, mirroring the
    // established "public method the test calls directly" pattern.
    // ------------------------------------------------------------

    /// <summary>Multiplies the current zoom level by <paramref name="factor"/> ("Zoom", `WP 10.4A` scope).</summary>
    public void ZoomBy(double factor)
    {
        _model.ZoomBy(factor);
        UpdateTransform();
    }

    /// <summary>Pans by <paramref name="delta"/> ("Pan", `WP 10.4A` scope).</summary>
    public void PanBy(Vector delta) => PanTo(new Vector(_model.PanOffset.X, _model.PanOffset.Y) + delta);

    /// <summary>Expands <paramref name="nodeId"/> ("Expand/collapse relationships", `WP 10.4A` scope).</summary>
    public bool ExpandNode(Guid nodeId)
    {
        var expanded = _model.ExpandNode(nodeId);
        if (expanded)
            Rebuild();
        return expanded;
    }

    /// <summary>Collapses <paramref name="nodeId"/> ("Expand/collapse relationships", `WP 10.4A` scope).</summary>
    public bool CollapseNode(Guid nodeId)
    {
        var collapsed = _model.CollapseNode(nodeId);
        if (collapsed)
            Rebuild();
        return collapsed;
    }

    /// <summary>Re-centres the graph on <paramref name="objectId"/> ("Selected object centring"/"Double-click navigation", `WP 10.4A` scope).</summary>
    public bool Recentre(Guid objectId, string kind)
    {
        var moved = _model.Recentre(objectId, kind);
        if (moved)
            Rebuild();
        return moved;
    }

    /// <summary>Selects <paramref name="nodeId"/>, driving highlighting ("Relationship highlighting", `WP 10.4A` scope).</summary>
    public void SelectNode(Guid? nodeId)
    {
        _model.SelectNode(nodeId);
        Rebuild();
    }

    /// <summary>The current model — exposed for direct, deterministic test assertions over graph state (never mutated by a caller other than through this View's own public methods).</summary>
    internal DigitalThreadGraphModel Model => _model;

    // ------------------------------------------------------------
    // Rendering
    // ------------------------------------------------------------

    private void Rebuild()
    {
        var nodes = _model.Nodes;
        var centre = nodes.FirstOrDefault(n => n.IsCentre);
        Title = $"Relationships: {centre.DisplayName}";
        ObjectKind = centre.Kind;
        _titleBlock.Text = $"🕸 {Title}";

        RebuildBreadcrumb(centre);
        RebuildLegend();
        RebuildInspector();
        RebuildGraphCanvas(nodes);
        RebuildMiniMap(nodes);
        UpdateTransform();

        _statusMessage.Text = $"{nodes.Count} node(s), {_model.Edges.Count} relationship(s) — {_model.Layout} layout.";
    }

    private void RebuildBreadcrumb(DigitalThreadNodeSnapshot centre)
    {
        _breadcrumbBar.Children.Clear();
        for (var i = 0; i < _model.Breadcrumb.Count; i++)
        {
            var entry = _model.Breadcrumb[i];
            var index = i;
            var crumbButton = new Button { Content = entry.DisplayName, FontSize = DesignTokens.FontSizeCaption, Padding = new Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceXs) };
            crumbButton.Click += (_, _) => { if (_model.JumpToBreadcrumb(index)) Rebuild(); };
            _breadcrumbBar.Children.Add(crumbButton);
            _breadcrumbBar.Children.Add(new TextBlock { Text = "›", VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 });
        }
        _breadcrumbBar.Children.Add(new TextBlock { Text = centre.DisplayName, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
    }

    private void RebuildLegend()
    {
        _legendPanel.Children.Clear();
        _legendPanel.Children.Add(new TextBlock { Text = "Relationship categories (uncheck to hide):", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 });

        foreach (RelationshipCategory category in Enum.GetValues<RelationshipCategory>())
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
            var swatch = new Border { Width = 12, Height = 12, Background = CategoryColors.Resolve(category), CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
            var toggle = new CheckBox { Content = category.ToString(), IsChecked = !_model.HiddenCategories.Contains(category), FontSize = DesignTokens.FontSizeCaption };
            toggle.IsCheckedChanged += (_, _) => { _model.SetCategoryVisible(category, toggle.IsChecked == true); Rebuild(); };
            row.Children.Add(swatch);
            row.Children.Add(toggle);
            _legendPanel.Children.Add(row);
        }

        _legendPanel.Children.Add(new Separator());
        _legendPanel.Children.Add(new TextBlock { Text = "Lifecycle status:", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 });
        foreach (LifecycleState state in Enum.GetValues<LifecycleState>())
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
            row.Children.Add(new Border { Width = 10, Height = 10, Background = LifecycleColors.Resolve(state), CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = state.ToString(), FontSize = DesignTokens.FontSizeCaption });
            _legendPanel.Children.Add(row);
        }
    }

    private void RebuildInspector()
    {
        _inspectorPanel.Children.Clear();

        if (_model.SelectedEdge is not { } edge)
        {
            _inspectorPanel.Children.Add(new TextBlock { Text = "Click a relationship line to inspect it.", Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap });
            return;
        }

        var sourceName = _model.Nodes.FirstOrDefault(n => n.ObjectId == edge.SourceId).DisplayName;
        var targetName = _model.Nodes.FirstOrDefault(n => n.ObjectId == edge.TargetId).DisplayName;

        void AddRow(string label, string value)
        {
            _inspectorPanel.Children.Add(new TextBlock { Text = label, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 });
            _inspectorPanel.Children.Add(new TextBlock { Text = value, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceSm) });
        }

        AddRow("Kind", edge.RelationshipKind);
        AddRow("Category", edge.Category.ToString());
        AddRow("From", sourceName);
        AddRow("To", targetName);
    }

    private void RebuildGraphCanvas(IReadOnlyList<DigitalThreadNodeSnapshot> nodes)
    {
        _graphCanvas.Children.Clear();

        var nodesById = nodes.ToDictionary(n => n.ObjectId);

        foreach (var edge in _model.Edges)
        {
            if (_model.HiddenCategories.Contains(edge.Category))
                continue;
            if (!nodesById.TryGetValue(edge.SourceId, out var source) || !nodesById.TryGetValue(edge.TargetId, out var target))
                continue;

            var isHighlighted = _model.SelectedEdge is { } selected && selected.SourceId == edge.SourceId && selected.TargetId == edge.TargetId && selected.RelationshipKind == edge.RelationshipKind
                || _model.SelectedNodeId is { } selectedNode && (selectedNode == edge.SourceId || selectedNode == edge.TargetId);

            var (x1, y1) = ToCanvasCentre(source);
            var (x2, y2) = ToCanvasCentre(target);

            var line = new Line
            {
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2),
                Stroke = CategoryColors.Resolve(edge.Category),
                StrokeThickness = isHighlighted ? 3 : 1.4,
                Opacity = isHighlighted ? 1.0 : 0.75,
                ZIndex = 0,
            };
            line.PointerPressed += (_, e) => { _model.SelectEdge(edge); Rebuild(); e.Handled = true; };
            _graphCanvas.Children.Add(line);

            var label = new TextBlock
            {
                Text = edge.RelationshipKind,
                FontSize = DesignTokens.FontSizeCaption,
                Opacity = isHighlighted ? 1.0 : 0.7,
                Background = Brushes.Transparent,
            };
            Canvas.SetLeft(label, (x1 + x2) / 2 - 20);
            Canvas.SetTop(label, (y1 + y2) / 2 - 8);
            _graphCanvas.Children.Add(label);
        }

        foreach (var node in nodes)
            _graphCanvas.Children.Add(BuildNodeVisual(node));
    }

    private Control BuildNodeVisual(DigitalThreadNodeSnapshot node)
    {
        var width = node.IsCentre ? CentreNodeWidth : NodeWidth;
        var height = node.IsCentre ? CentreNodeHeight : NodeHeight;
        var isSelected = _model.SelectedNodeId == node.ObjectId;
        var isMatch = _model.SearchMatches.Contains(node.ObjectId);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(node.IsCentre || node.IsRecord ? "Auto,*" : "Auto,Auto,*") };

        var col = 0;
        if (!node.IsCentre && !node.IsRecord)
        {
            var toggle = new Button
            {
                Content = node.IsExpanded ? "▾" : "▸",
                FontSize = DesignTokens.FontSizeCaption,
                Padding = new Thickness(DesignTokens.SpaceXs),
                MinWidth = 20,
                MinHeight = 20,
            };
            toggle.Click += (_, e) =>
            {
                if (node.IsExpanded)
                    CollapseNode(node.ObjectId);
                else
                    ExpandNode(node.ObjectId);
                e.Handled = true;
            };
            Grid.SetColumn(toggle, col++);
            grid.Children.Add(toggle);
        }

        var icon = new TextBlock { Text = IconRegistry.Resolve(node.Kind), FontSize = DesignTokens.FontSizeTitle, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(DesignTokens.SpaceXs, 0) };
        Grid.SetColumn(icon, col++);
        grid.Children.Add(icon);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock { Text = node.DisplayName, FontSize = DesignTokens.FontSizeBody, FontWeight = node.IsCentre ? FontWeight.Bold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis });
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
        if (node.Status is { } status)
        {
            statusRow.Children.Add(new Border { Width = 8, Height = 8, Background = LifecycleColors.Resolve(status), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center });
            statusRow.Children.Add(new TextBlock { Text = status.ToString(), FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75 });
        }
        else
        {
            statusRow.Children.Add(new TextBlock { Text = node.Kind, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75 });
        }
        textStack.Children.Add(statusRow);
        Grid.SetColumn(textStack, col);
        grid.Children.Add(textStack);

        var border = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(6),
            BorderBrush = isSelected || isMatch ? new ImmutableSolidColorBrush(Theming.BrandPalette.Amber500) : (node.IsCentre ? new ImmutableSolidColorBrush(Theming.BrandPalette.Cyan500) : new ImmutableSolidColorBrush(Theming.BrandPalette.Slate500)),
            BorderThickness = new Thickness(node.IsCentre || isSelected || isMatch ? 2.5 : 1),
            Padding = new Thickness(DesignTokens.SpaceXs),
            Child = grid,
            Cursor = new Cursor(node.IsRecord ? StandardCursorType.Arrow : StandardCursorType.Hand),
            ZIndex = 1,
        };

        // Theme-reactive node fill (`WP 10.5A`) — replaces this class's own
        // previously-hardcoded, non-theme-reactive hex colours (`#2D4F7C`/
        // `#2A2A2E`, `WP 10.4A`), a genuine finding of this Work Package's
        // own theme audit: those fixed darks read correctly only in Dark
        // theme, wrong (a dark card floating on a light canvas) in Light.
        var backgroundKey = node.IsCentre ? ApplicationPalette.AccentPanelBackgroundBrushKey : ApplicationPalette.PanelBackgroundBrushKey;
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, backgroundKey);

        if (!node.IsRecord)
        {
            border.PointerPressed += (_, e) =>
            {
                if (e.ClickCount >= 2)
                {
                    Recentre(node.ObjectId, node.Kind);
                }
                else
                {
                    SelectNode(node.ObjectId);
                    _navigateToObject(node.ObjectId, node.Kind);
                    ActionCompleted?.Invoke($"Opened {node.Kind} '{node.DisplayName}' from the Digital Thread graph.", ActionOutcome.NoChange);
                }
                e.Handled = true;
            };
        }

        var (x, y) = ToCanvasCentre(node);
        Canvas.SetLeft(border, x - width / 2);
        Canvas.SetTop(border, y - height / 2);
        return border;
    }

    private void RebuildMiniMap(IReadOnlyList<DigitalThreadNodeSnapshot> nodes)
    {
        _miniMap.Children.Clear();
        _miniMap.Children.Add(_miniMapViewportRect);

        if (nodes.Count == 0)
            return;

        var minX = nodes.Min(n => n.X);
        var maxX = nodes.Max(n => n.X);
        var minY = nodes.Min(n => n.Y);
        var maxY = nodes.Max(n => n.Y);
        var spanX = Math.Max(maxX - minX, 1);
        var spanY = Math.Max(maxY - minY, 1);

        foreach (var node in nodes)
        {
            var dotX = (node.X - minX) / spanX * (MiniMapWidth - 10) + 5;
            var dotY = (node.Y - minY) / spanY * (MiniMapHeight - 10) + 5;
            var dot = new Ellipse
            {
                Width = node.IsCentre ? 8 : 5,
                Height = node.IsCentre ? 8 : 5,
                Fill = node.IsCentre ? new ImmutableSolidColorBrush(Theming.BrandPalette.Cyan500) : new ImmutableSolidColorBrush(Theming.BrandPalette.Slate400),
            };
            Canvas.SetLeft(dot, dotX - dot.Width / 2);
            Canvas.SetTop(dot, dotY - dot.Height / 2);
            _miniMap.Children.Add(dot);
        }
    }

    private void UpdateTransform()
    {
        var zoom = _model.ZoomLevel;
        _zoomTransform.ScaleX = zoom;
        _zoomTransform.ScaleY = zoom;

        var viewportWidth = _viewport.Bounds.Width > 0 ? _viewport.Bounds.Width : 600;
        var viewportHeight = _viewport.Bounds.Height > 0 ? _viewport.Bounds.Height : 400;

        _panTransform.X = viewportWidth / 2 - CanvasOrigin * zoom + _model.PanOffset.X;
        _panTransform.Y = viewportHeight / 2 - CanvasOrigin * zoom + _model.PanOffset.Y;

        var viewportRectWidth = Math.Min(MiniMapWidth, viewportWidth / Math.Max(CanvasSize / MiniMapWidth, 0.01) / zoom);
        var viewportRectHeight = Math.Min(MiniMapHeight, viewportHeight / Math.Max(CanvasSize / MiniMapHeight, 0.01) / zoom);
        _miniMapViewportRect.Width = Math.Max(viewportRectWidth, 8);
        _miniMapViewportRect.Height = Math.Max(viewportRectHeight, 8);
        Canvas.SetLeft(_miniMapViewportRect, Math.Clamp(MiniMapWidth / 2 - _miniMapViewportRect.Width / 2, 0, MiniMapWidth - _miniMapViewportRect.Width));
        Canvas.SetTop(_miniMapViewportRect, Math.Clamp(MiniMapHeight / 2 - _miniMapViewportRect.Height / 2, 0, MiniMapHeight - _miniMapViewportRect.Height));
    }

    private void PanTo(Vector newOffset)
    {
        var delta = newOffset - new Vector(_model.PanOffset.X, _model.PanOffset.Y);
        _model.PanBy(delta);
        UpdateTransform();
    }

    private void JumpMiniMap(Point clickPosition)
    {
        var nodes = _model.Nodes;
        if (nodes.Count == 0)
            return;

        var minX = nodes.Min(n => n.X);
        var maxX = nodes.Max(n => n.X);
        var minY = nodes.Min(n => n.Y);
        var maxY = nodes.Max(n => n.Y);
        var spanX = Math.Max(maxX - minX, 1);
        var spanY = Math.Max(maxY - minY, 1);

        var graphX = (clickPosition.X - 5) / (MiniMapWidth - 10) * spanX + minX;
        var graphY = (clickPosition.Y - 5) / (MiniMapHeight - 10) * spanY + minY;

        PanTo(new Vector(-graphX * _model.ZoomLevel, -graphY * _model.ZoomLevel));
    }

    private static (double X, double Y) ToCanvasCentre(DigitalThreadNodeSnapshot node) => (CanvasOrigin + node.X, CanvasOrigin + node.Y);
}

/// <summary>
/// A deterministic, one-colour-per-value palette for
/// <see cref="RelationshipCategory"/> ("Relationship categories"/"Graph
/// legend", `WP 10.4A` scope) — the first such mapping in the platform,
/// mirroring <see cref="HealthColors"/>'s own "one value, one colour,
/// everywhere" rule, extended here to relationship categories rather than
/// health statuses.
/// </summary>
internal static class CategoryColors
{
    private static readonly IReadOnlyList<IBrush> Palette = new IBrush[]
    {
        // Brand-derived, each distinct and legible on navy and on paper.
        new ImmutableSolidColorBrush(Theming.BrandPalette.Cyan500), new ImmutableSolidColorBrush(Color.Parse("#9d6cf0")),
        new ImmutableSolidColorBrush(Theming.BrandPalette.Amber500), new ImmutableSolidColorBrush(Theming.BrandPalette.Green500),
        new ImmutableSolidColorBrush(Theming.BrandPalette.Red500), new ImmutableSolidColorBrush(Color.Parse("#5fb8b0")),
        new ImmutableSolidColorBrush(Color.Parse("#f27e5c")), new ImmutableSolidColorBrush(Theming.BrandPalette.Slate400),
        new ImmutableSolidColorBrush(Color.Parse("#7b8ff5")), new ImmutableSolidColorBrush(Theming.BrandPalette.Cyan600),
        new ImmutableSolidColorBrush(Color.Parse("#c084fc")), new ImmutableSolidColorBrush(Color.Parse("#34d399")),
        new ImmutableSolidColorBrush(Color.Parse("#fbbf24")), new ImmutableSolidColorBrush(Color.Parse("#60a5fa")),
        new ImmutableSolidColorBrush(Color.Parse("#f472b6")), new ImmutableSolidColorBrush(Color.Parse("#a3e635")),
        new ImmutableSolidColorBrush(Color.Parse("#e879f9")),
    };

    public static IBrush Resolve(RelationshipCategory category) => Palette[(int)category % Palette.Count];
}

/// <summary>
/// A deterministic, one-colour-per-value mapping for
/// <see cref="LifecycleState"/> ("Object status indicators", `WP 10.4A`
/// scope) — distinct from <see cref="HealthColors"/> (keyed on
/// <see cref="EngineeringHealthStatus"/>, a different, coarser
/// classification); reuses the same brush values for visual consistency
/// with the rest of the Cockpit's own colour language.
/// </summary>
internal static class LifecycleColors
{
    // The design system's machine-state hues: amber in review, green
    // approved, brand cyan once released, red cancelled, faint otherwise.
    private static readonly IBrush Neutral = new ImmutableSolidColorBrush(Theming.BrandPalette.Slate500);
    private static readonly IBrush Warning = new ImmutableSolidColorBrush(Theming.BrandPalette.Amber500);
    private static readonly IBrush Success = new ImmutableSolidColorBrush(Theming.BrandPalette.Green500);
    private static readonly IBrush Released = new ImmutableSolidColorBrush(Theming.BrandPalette.Cyan500);
    private static readonly IBrush Danger = new ImmutableSolidColorBrush(Theming.BrandPalette.Red500);

    public static IBrush Resolve(LifecycleState state) => state switch
    {
        LifecycleState.Draft => Neutral,
        LifecycleState.InReview => Warning,
        LifecycleState.Approved => Success,
        LifecycleState.Released => Released,
        LifecycleState.Superseded => Neutral,
        LifecycleState.Obsolete => Neutral,
        LifecycleState.Archived => Neutral,
        LifecycleState.Cancelled => Danger,
        _ => Neutral,
    };
}
