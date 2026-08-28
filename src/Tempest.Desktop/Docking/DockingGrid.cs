using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;
using Avalonia.Layout;
using Tempest.App.Workspace;

namespace Tempest.Desktop.Docking;

/// <summary>
/// The Docking Framework (`WP 10.0B`; extended `WP 10.2B` with a third,
/// bottom dock row) — a five-column, three-row <see cref="Grid"/> (Left
/// panel | splitter | Document Area | splitter | Right panel, over a
/// bottom splitter | Bottom panel), each docked panel resizable by
/// dragging its own <see cref="GridSplitter"/> and independently
/// hideable, mirroring exactly the docking model
/// <c>WorkspaceDockPosition</c>/<c>WorkspacePanelPlacement</c> (`WP 8.0B`)
/// already contracts for — <c>Left</c>/<c>Right</c>/<c>Bottom</c>, no
/// <c>Floating</c> value (`ADR-0095`, reserved, out of this Work
/// Package's own "no contract redesign" scope). `Bottom` was already a
/// real <c>WorkspaceDockPosition</c> enum member since `WP 8.0B` — never
/// wired to any actual dock surface until this Work Package (`WP10.2B
/// Implementation Report.md` §2.1). Introduces no third-party docking
/// library: a hand-rolled <see cref="Grid"/>+<see cref="GridSplitter"/>
/// host is the deliberate, disclosed choice (`WP10.0B Implementation
/// Report.md`) — `WorkspaceDockPosition` supports no floating/undocked
/// panel today, so a full docking framework's own floating-window
/// capability would be unused surface, not a genuine present need.
/// </summary>
public sealed class DockingGrid : Grid
{
    private const int LeftColumn = 0;
    private const int LeftSplitterColumn = 1;
    private const int CenterColumn = 2;
    private const int RightSplitterColumn = 3;
    private const int RightColumn = 4;
    private const int ColumnCount = 5;

    private const int MainRow = 0;
    private const int BottomSplitterRow = 1;
    private const int BottomRow = 2;

    /// <summary>The fixed width/height a collapsed panel's own strip occupies — wide enough for a rotated title, narrow enough to hand most of the space back to the Document Area (`WP10.2B UX Review.md` §3).</summary>
    public const double CollapsedStripSize = 32;

    /// <summary>
    /// The narrowest the Document Area is ever allowed to become before
    /// the side docks start giving space back (`TD-70`). The Document
    /// Area is where the engineer actually works; a fixed-pixel side
    /// dock that consumes half a laptop window is the concrete failure
    /// this floor exists to prevent.
    /// </summary>
    public const double MinDocumentAreaWidth = 420;

    /// <summary>The <see cref="MinDocumentAreaWidth"/> equivalent for the bottom dock (`TD-70`).</summary>
    public const double MinDocumentAreaHeight = 220;

    /// <summary>
    /// The narrowest a side dock is squeezed to before it is collapsed to
    /// its own <see cref="CollapsedStripSize"/> strip instead — below this
    /// a panel is too narrow to read, so handing the space to the Document
    /// Area and leaving the strip's expand affordance is the more useful
    /// outcome (`TD-70`).
    /// </summary>
    public const double MinUsablePanelSize = 140;

    private const double SplitterAllowance = 4;

    private double _preferredLeftWidth = 240;
    private double _preferredRightWidth = 240;
    private double _preferredBottomHeight = 160;

    private bool _leftCollapsed;
    private bool _rightCollapsed;
    private bool _bottomCollapsed;

    private readonly GridSplitter _leftSplitter;
    private readonly GridSplitter _rightSplitter;
    private readonly GridSplitter _bottomSplitter;

    /// <summary>Raised after the left panel is resized by dragging its own splitter, carrying the new width in device-independent pixels.</summary>
    public event Action<double>? LeftPanelResized;

    /// <summary>Raised after the right panel is resized by dragging its own splitter, carrying the new width in device-independent pixels.</summary>
    public event Action<double>? RightPanelResized;

    /// <summary>Raised after the bottom panel is resized by dragging its own splitter, carrying the new height in device-independent pixels.</summary>
    public event Action<double>? BottomPanelResized;

    /// <summary>Initialises a new instance of the <see cref="DockingGrid"/> class with five empty columns and a collapsed bottom row.</summary>
    public DockingGrid()
    {
        ColumnDefinitions =
        [
            new ColumnDefinition(240, GridUnitType.Pixel),
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(1, GridUnitType.Star),
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(240, GridUnitType.Pixel),
        ];

        RowDefinitions =
        [
            new RowDefinition(1, GridUnitType.Star),
            new RowDefinition(GridLength.Auto),
            new RowDefinition(0, GridUnitType.Pixel),
        ];

        _leftSplitter = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetColumn(_leftSplitter, LeftSplitterColumn);
        Grid.SetRow(_leftSplitter, MainRow);
        _leftSplitter.DragCompleted += (_, _) => NotifyLeftPanelResized();
        Children.Add(_leftSplitter);

        _rightSplitter = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetColumn(_rightSplitter, RightSplitterColumn);
        Grid.SetRow(_rightSplitter, MainRow);
        _rightSplitter.DragCompleted += (_, _) => NotifyRightPanelResized();
        Children.Add(_rightSplitter);

        _bottomSplitter = new GridSplitter { Height = 4, VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetColumn(_bottomSplitter, LeftColumn);
        Grid.SetColumnSpan(_bottomSplitter, ColumnCount);
        Grid.SetRow(_bottomSplitter, BottomSplitterRow);
        _bottomSplitter.DragCompleted += (_, _) => NotifyBottomPanelResized();
        Children.Add(_bottomSplitter);

        // Responsive adaptation (`TD-70`): re-clamp whenever the host
        // window changes size. Observed rather than done inside
        // ArrangeOverride so mutating the column definitions can never
        // re-enter the layout pass that produced them.
        this.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver<Rect>(bounds =>
            ApplyResponsiveLayout(bounds.Width, bounds.Height)));
    }

    /// <summary>
    /// Raises <see cref="LeftPanelResized"/> with the left column's own
    /// current width — called by the left <see cref="GridSplitter"/>'s own
    /// <c>DragCompleted</c> handler in production. Public so a test can
    /// drive the identical notification path a real drag would, without a
    /// headless environment needing to simulate an OS-level pointer drag
    /// on a <see cref="GridSplitter"/> specifically.
    /// </summary>
    public void NotifyLeftPanelResized()
    {
        // A drag is a deliberate new user preference — record it, or a
        // later hide/show would silently revert to the pre-drag width
        // (`TD-71`).
        if (!_leftCollapsed && LeftWidth > 0)
            _preferredLeftWidth = LeftWidth;

        LeftPanelResized?.Invoke(LeftWidth);
    }

    /// <summary>The right-column equivalent of <see cref="NotifyLeftPanelResized"/>.</summary>
    public void NotifyRightPanelResized()
    {
        if (!_rightCollapsed && RightWidth > 0)
            _preferredRightWidth = RightWidth;

        RightPanelResized?.Invoke(RightWidth);
    }

    /// <summary>The bottom-row equivalent of <see cref="NotifyLeftPanelResized"/>.</summary>
    public void NotifyBottomPanelResized()
    {
        if (!_bottomCollapsed && BottomHeight > 0)
            _preferredBottomHeight = BottomHeight;

        BottomPanelResized?.Invoke(BottomHeight);
    }

    /// <summary>
    /// Re-applies the side/bottom dock sizes for an available area of
    /// <paramref name="availableWidth"/> × <paramref name="availableHeight"/>,
    /// squeezing and — below <see cref="MinUsablePanelSize"/> — collapsing
    /// docks so the Document Area never falls under
    /// <see cref="MinDocumentAreaWidth"/>/<see cref="MinDocumentAreaHeight"/>
    /// (`TD-70`). The user's own preferred sizes are never overwritten:
    /// they are restored in full as soon as the window is wide enough
    /// again. Public so a test can drive the exact path a real window
    /// resize drives.
    /// </summary>
    public void ApplyResponsiveLayout(double availableWidth, double availableHeight)
    {
        if (availableWidth > 0)
        {
            var leftShown = IsLeftVisible;
            var rightShown = IsRightVisible;
            var splitters = (leftShown ? SplitterAllowance : 0) + (rightShown ? SplitterAllowance : 0);
            var budget = availableWidth - MinDocumentAreaWidth - splitters;

            var (left, right) = FitPair(
                leftShown ? (_leftCollapsed ? CollapsedStripSize : _preferredLeftWidth) : 0,
                rightShown ? (_rightCollapsed ? CollapsedStripSize : _preferredRightWidth) : 0,
                budget);

            if (leftShown)
                ColumnDefinitions[LeftColumn].Width = new GridLength(left, GridUnitType.Pixel);
            if (rightShown)
                ColumnDefinitions[RightColumn].Width = new GridLength(right, GridUnitType.Pixel);

            _leftSplitter.IsEnabled = leftShown && left > CollapsedStripSize;
            _rightSplitter.IsEnabled = rightShown && right > CollapsedStripSize;
        }

        if (availableHeight > 0 && IsBottomVisible)
        {
            var desired = _bottomCollapsed ? CollapsedStripSize : _preferredBottomHeight;
            var budget = availableHeight - MinDocumentAreaHeight - SplitterAllowance;
            var (bottom, _) = FitPair(desired, 0, budget);

            RowDefinitions[BottomRow].Height = new GridLength(bottom, GridUnitType.Pixel);
            _bottomSplitter.IsEnabled = bottom > CollapsedStripSize;
        }
    }

    /// <summary>
    /// Fits two desired dock sizes into <paramref name="budget"/>:
    /// unchanged when they already fit, squeezed proportionally when they
    /// do not, and collapsed to <see cref="CollapsedStripSize"/> rather
    /// than squeezed below <see cref="MinUsablePanelSize"/> (`TD-70`).
    /// </summary>
    private static (double First, double Second) FitPair(double first, double second, double budget)
    {
        var total = first + second;
        if (total <= budget || total <= 0)
            return (first, second);

        var floor = (first > 0 ? CollapsedStripSize : 0) + (second > 0 ? CollapsedStripSize : 0);
        if (budget <= floor)
            return (first > 0 ? CollapsedStripSize : 0, second > 0 ? CollapsedStripSize : 0);

        var scale = budget / total;
        var scaledFirst = first > 0 ? Math.Max(first * scale, CollapsedStripSize) : 0;
        var scaledSecond = second > 0 ? Math.Max(second * scale, CollapsedStripSize) : 0;

        // Too narrow to read is worse than not shown: hand the space to
        // the Document Area and leave the strip's own expand affordance.
        if (scaledFirst > 0 && scaledFirst < MinUsablePanelSize)
            scaledFirst = CollapsedStripSize;
        if (scaledSecond > 0 && scaledSecond < MinUsablePanelSize)
            scaledSecond = CollapsedStripSize;

        return (scaledFirst, scaledSecond);
    }

    /// <summary>Places <paramref name="content"/> as the always-present, centre Document Area (`WP8.0A Workspace Architecture Document.md` §7 — never dockable away).</summary>
    public void SetCenterContent(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Grid.SetColumn(content, CenterColumn);
        Grid.SetRow(content, MainRow);
        Children.Add(content);
    }

    /// <summary>Places <paramref name="content"/> in the left dock at <paramref name="initialWidth"/>, or hides the column entirely if <paramref name="visible"/> is <see langword="false"/>.</summary>
    public void SetLeftPanel(Control content, double initialWidth, bool visible)
    {
        ArgumentNullException.ThrowIfNull(content);
        Grid.SetColumn(content, LeftColumn);
        Grid.SetRow(content, MainRow);
        Children.Add(content);
        _preferredLeftWidth = initialWidth;
        SetLeftVisible(visible);
    }

    /// <summary>Places <paramref name="content"/> in the right dock at <paramref name="initialWidth"/>, or hides the column entirely if <paramref name="visible"/> is <see langword="false"/>.</summary>
    public void SetRightPanel(Control content, double initialWidth, bool visible)
    {
        ArgumentNullException.ThrowIfNull(content);
        Grid.SetColumn(content, RightColumn);
        Grid.SetRow(content, MainRow);
        Children.Add(content);
        _preferredRightWidth = initialWidth;
        SetRightVisible(visible);
    }

    /// <summary>Places <paramref name="content"/> in the bottom dock at <paramref name="initialHeight"/>, spanning every column, or hides the row entirely if <paramref name="visible"/> is <see langword="false"/> — the Output panel's own dock surface (`WP 10.2B`, realising <c>WorkspaceDockPosition.Bottom</c> for the first time).</summary>
    public void SetBottomPanel(Control content, double initialHeight, bool visible)
    {
        ArgumentNullException.ThrowIfNull(content);
        Grid.SetColumn(content, LeftColumn);
        Grid.SetColumnSpan(content, ColumnCount);
        Grid.SetRow(content, BottomRow);
        Children.Add(content);
        _preferredBottomHeight = initialHeight;
        SetBottomVisible(visible);
    }

    /// <summary>Shows or hides the left dock column — hiding collapses it to zero width, preserving the last width to restore on <c>SetLeftVisible(true)</c> (`WP8.0A UI Architecture.md` §2's own "reopening restores the same width" requirement).</summary>
    public void SetLeftVisible(bool visible)
    {
        ColumnDefinitions[LeftColumn].Width = visible ? new GridLength(_leftCollapsed ? CollapsedStripSize : _preferredLeftWidth, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel);
        _leftSplitter.IsEnabled = visible && !_leftCollapsed;

        // Re-clamp against the current window size, so restoring a
        // preference never overflows a narrow window (`TD-70`).
        ApplyResponsiveLayout(Bounds.Width, Bounds.Height);
    }

    /// <summary>Shows or hides the right dock column — same preserved-width behaviour as <see cref="SetLeftVisible"/>.</summary>
    public void SetRightVisible(bool visible)
    {
        ColumnDefinitions[RightColumn].Width = visible ? new GridLength(_rightCollapsed ? CollapsedStripSize : _preferredRightWidth, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel);
        _rightSplitter.IsEnabled = visible && !_rightCollapsed;

        // Re-clamp against the current window size, so restoring a
        // preference never overflows a narrow window (`TD-70`).
        ApplyResponsiveLayout(Bounds.Width, Bounds.Height);
    }

    /// <summary>Shows or hides the bottom dock row — same preserved-height behaviour as <see cref="SetLeftVisible"/>.</summary>
    public void SetBottomVisible(bool visible)
    {
        RowDefinitions[BottomRow].Height = visible ? new GridLength(_bottomCollapsed ? CollapsedStripSize : _preferredBottomHeight, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel);
        _bottomSplitter.IsEnabled = visible && !_bottomCollapsed;

        // Re-clamp against the current window size (`TD-70`).
        ApplyResponsiveLayout(Bounds.Width, Bounds.Height);
    }

    /// <summary>
    /// Collapses or expands the left dock column in place — distinct from
    /// <see cref="SetLeftVisible"/>: a collapsed panel still occupies a
    /// thin <see cref="CollapsedStripSize"/> strip in its own normal dock
    /// slot (its header/expand affordance stays reachable), where a hidden
    /// panel occupies none. No-op while the panel is hidden entirely — a
    /// collapsed-but-hidden state is not a real, reachable UI state (`WP
    /// 10.2B`).
    /// </summary>
    public void SetLeftCollapsed(bool collapsed)
    {
        _leftCollapsed = collapsed;
        if (IsLeftVisible)
            SetLeftVisible(true);
    }

    /// <summary>The right-column equivalent of <see cref="SetLeftCollapsed"/>.</summary>
    public void SetRightCollapsed(bool collapsed)
    {
        _rightCollapsed = collapsed;
        if (IsRightVisible)
            SetRightVisible(true);
    }

    /// <summary>The bottom-row equivalent of <see cref="SetLeftCollapsed"/>.</summary>
    public void SetBottomCollapsed(bool collapsed)
    {
        _bottomCollapsed = collapsed;
        if (IsBottomVisible)
            SetBottomVisible(true);
    }

    /// <summary>
    /// Sets the left dock column's own "last shown width" — the width
    /// <see cref="SetLeftVisible"/> restores to on the next
    /// <c>SetLeftVisible(true)</c>, applied immediately if the column is
    /// already visible. Distinct from <see cref="SetLeftPanel"/>: this
    /// never re-adds content (calling it a second time would duplicate the
    /// panel in <see cref="Grid.Children"/>) — the setter a predefined
    /// layout preset (`WP 10.2B`) calls to change an already-placed
    /// panel's own width.
    /// </summary>
    public void SetLeftWidth(double width)
    {
        _preferredLeftWidth = width;
        if (IsLeftVisible)
            SetLeftVisible(true);
    }

    /// <summary>The right-column equivalent of <see cref="SetLeftWidth"/>.</summary>
    public void SetRightWidth(double width)
    {
        _preferredRightWidth = width;
        if (IsRightVisible)
            SetRightVisible(true);
    }

    /// <summary>The bottom-row equivalent of <see cref="SetLeftWidth"/>.</summary>
    public void SetBottomHeight(double height)
    {
        _preferredBottomHeight = height;
        if (IsBottomVisible)
            SetBottomVisible(true);
    }

    /// <summary>Gets the left dock column's own current width, in device-independent pixels.</summary>
    public double LeftWidth => ColumnDefinitions[LeftColumn].Width.Value;

    /// <summary>Gets the right dock column's own current width, in device-independent pixels.</summary>
    public double RightWidth => ColumnDefinitions[RightColumn].Width.Value;

    /// <summary>Gets the bottom dock row's own current height, in device-independent pixels.</summary>
    public double BottomHeight => RowDefinitions[BottomRow].Height.Value;

    /// <summary>Gets whether the left dock column is currently visible (non-zero width).</summary>
    public bool IsLeftVisible => ColumnDefinitions[LeftColumn].Width.Value > 0;

    /// <summary>Gets whether the right dock column is currently visible (non-zero width).</summary>
    public bool IsRightVisible => ColumnDefinitions[RightColumn].Width.Value > 0;

    /// <summary>Gets whether the bottom dock row is currently visible (non-zero height).</summary>
    public bool IsBottomVisible => RowDefinitions[BottomRow].Height.Value > 0;

    /// <summary>Gets whether the left dock column is currently collapsed to its own thin strip.</summary>
    public bool IsLeftCollapsed => _leftCollapsed;

    /// <summary>Gets whether the right dock column is currently collapsed to its own thin strip.</summary>
    public bool IsRightCollapsed => _rightCollapsed;

    /// <summary>Gets whether the bottom dock row is currently collapsed to its own thin strip.</summary>
    public bool IsBottomCollapsed => _bottomCollapsed;

    private Control? _flyoutContent;
    private FlyoutRestoreState? _flyoutRestore;

    /// <summary>Gets whether an Auto-Hide flyout is currently showing.</summary>
    public bool IsFlyoutOpen => _flyoutContent is not null;

    /// <summary>
    /// Shows <paramref name="content"/> as a temporary overlay anchored to
    /// <paramref name="edge"/>, on top of every normally-docked panel and
    /// the Document Area (raised above every sibling via
    /// <see cref="Visual.ZIndex"/>) — the Auto-Hide architecture's own
    /// "peek" surface (`WP 10.2B`). <paramref name="content"/> is typically
    /// already one of this <see cref="Grid"/>'s own children (the same
    /// <see cref="PanelHostControl"/> normally docked at <paramref
    /// name="edge"/>) — its own prior placement is captured and restored
    /// exactly by <see cref="HideFlyout"/>, so no second, duplicate control
    /// is ever created for the same panel. Closes any flyout already open
    /// first; only one Auto-Hidden panel can be peeking at a time.
    /// </summary>
    public void ShowFlyout(Control content, WorkspaceDockPosition edge, double size)
    {
        ArgumentNullException.ThrowIfNull(content);
        HideFlyout();

        var alreadyPresent = Children.Contains(content);
        _flyoutRestore = alreadyPresent
            ? new FlyoutRestoreState(Grid.GetColumn(content), Grid.GetColumnSpan(content), Grid.GetRow(content), Grid.GetRowSpan(content), content.HorizontalAlignment, content.VerticalAlignment, content.Width, content.Height)
            : null;

        if (!alreadyPresent)
            Children.Add(content);

        Grid.SetColumn(content, LeftColumn);
        Grid.SetColumnSpan(content, ColumnCount);
        Grid.SetRow(content, MainRow);
        Grid.SetRowSpan(content, BottomRow - MainRow + 1);
        content.ZIndex = 100;

        switch (edge)
        {
            case WorkspaceDockPosition.Left:
                content.HorizontalAlignment = HorizontalAlignment.Left;
                content.VerticalAlignment = VerticalAlignment.Stretch;
                content.Width = size;
                break;
            case WorkspaceDockPosition.Right:
                content.HorizontalAlignment = HorizontalAlignment.Right;
                content.VerticalAlignment = VerticalAlignment.Stretch;
                content.Width = size;
                break;
            case WorkspaceDockPosition.Bottom:
                content.HorizontalAlignment = HorizontalAlignment.Stretch;
                content.VerticalAlignment = VerticalAlignment.Bottom;
                content.Height = size;
                break;
        }

        _flyoutContent = content;
    }

    /// <summary>Closes the currently-open Auto-Hide flyout, if any, restoring the panel's own prior placement exactly — a no-op if none is open.</summary>
    public void HideFlyout()
    {
        if (_flyoutContent is null)
            return;

        var content = _flyoutContent;
        content.ZIndex = 0;

        if (_flyoutRestore is { } r)
        {
            Grid.SetColumn(content, r.Column);
            Grid.SetColumnSpan(content, r.ColumnSpan);
            Grid.SetRow(content, r.Row);
            Grid.SetRowSpan(content, r.RowSpan);
            content.HorizontalAlignment = r.HorizontalAlignment;
            content.VerticalAlignment = r.VerticalAlignment;
            content.Width = r.Width;
            content.Height = r.Height;
        }
        else
        {
            Children.Remove(content);
        }

        _flyoutContent = null;
        _flyoutRestore = null;
    }

    private sealed record FlyoutRestoreState(int Column, int ColumnSpan, int Row, int RowSpan, HorizontalAlignment HorizontalAlignment, VerticalAlignment VerticalAlignment, double Width, double Height);
}
