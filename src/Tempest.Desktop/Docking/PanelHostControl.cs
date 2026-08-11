using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.App.Workspace;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Docking;

/// <summary>
/// The Panel Host (`WP 10.0B`; visually modernised `WP 10.2A` — real
/// spacing/typography tokens, a hairline separator beneath the header;
/// extended `WP 10.2B` with Collapse and Auto-Hide (pin/unpin) header
/// affordances) — wraps one dockable <see cref="IWorkspacePanel"/>'s own
/// content with a title header, a collapse toggle, a pin (auto-hide)
/// toggle, and a hide (close) button, calling
/// <see cref="IWorkspacePanel.HideAsync"/> exactly as the underlying
/// contract already specifies ("preserving whatever internal state it
/// holds," `WP 8.0B`) — never a Desktop-local visibility flag disconnected
/// from the real panel's own state.
/// </summary>
/// <remarks>
/// <b>Collapse vs. Auto-Hide, deliberately distinct (`WP10.2B UX
/// Review.md` §3):</b> Collapse is a manual, in-place shrink to a thin
/// strip inside the panel's own normal dock slot — clicking the strip
/// expands it back in place, instantly, no overlay. Auto-Hide (unpinning)
/// additionally removes the panel from the reserved dock layout entirely,
/// handing that space back to the Document Area; the same thin strip
/// remains as a reachable edge tab, but clicking it raises
/// <see cref="FlyoutRequested"/> instead of expanding in place — the
/// caller (<c>MainWindow</c>) shows the panel's own content as a
/// temporary overlay via <see cref="DockingGrid.ShowFlyout"/>, closed by
/// clicking away or re-pinning. Both share the identical thin-strip visual
/// deliberately — the same affordance, two different, honestly documented
/// behaviours behind it, not two independently-styled controls to keep in
/// sync. Both are Desktop-local presentation state only — neither concept
/// exists anywhere in the frozen `WP8.0B` Workspace contracts, and neither
/// needed to be added to them.
/// </remarks>
public sealed class PanelHostControl : DockPanel
{
    private readonly IWorkspacePanel _panel;
    private readonly Control _content;
    private readonly Button _collapseButton;
    private readonly Button _pinButton;
    private readonly Button _stripButton;
    private readonly Border _collapsedStrip;
    private readonly StackPanel _headerStack;
    private bool _collapsed;
    private bool _pinned = true;

    /// <summary>Raised after the user requests this panel be hidden — the caller collapses the docking column and updates persisted layout.</summary>
    public event Action? HideRequested;

    /// <summary>Raised after the user toggles Collapse via the header button (only reachable while pinned), carrying the new collapsed state — the caller applies it to the owning <see cref="DockingGrid"/> and persists it.</summary>
    public event Action<bool>? CollapseToggled;

    /// <summary>Raised after the user toggles Pin (Auto-Hide) via the header button, carrying the new pinned state (<see langword="false"/> means the panel just entered Auto-Hide) — the caller applies it and persists it.</summary>
    public event Action<bool>? PinToggled;

    /// <summary>Raised when the user clicks this panel's own edge strip while it is Auto-Hidden (unpinned) — the caller opens a temporary flyout (<see cref="DockingGrid.ShowFlyout"/>) rather than expanding in place.</summary>
    public event Action? FlyoutRequested;

    /// <summary>Initialises a new instance of the <see cref="PanelHostControl"/> class.</summary>
    /// <param name="panel">The panel this host wraps — supplies the header's own title and dock position.</param>
    /// <param name="content">The panel's own rendered content.</param>
    public PanelHostControl(IWorkspacePanel panel, Control content)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(content);
        _panel = panel;
        _content = content;

        // An opaque background — required once this control can also be
        // reparented as an Auto-Hide flyout (`WP 10.2B`), directly over the
        // Document Area's own content; harmless in normal docked mode too.
        // Theme-reactive (`WP 10.5A`, closes `TD-39`) — bound to
        // `ApplicationPalette`'s own overlay resource, not a fixed brush;
        // automatically repaints the moment `ThemeService.ToggleAsync`
        // changes the active `ThemeVariant`, mirroring the identical fix
        // `CommandPaletteOverlay` (`WP 10.0B`) receives below.
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.OverlayBackgroundBrushKey);

        var title = new TextBlock
        {
            Text = panel.Title,
            Margin = DesignTokens.PanelHeaderPadding,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };

        _pinButton = new Button
        {
            Content = "📌",
            Padding = new Avalonia.Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceXs),
            MinWidth = DesignTokens.MinControlSize,
            MinHeight = DesignTokens.MinControlSize,
        };
        ToolTip.SetTip(_pinButton, "Auto-Hide (unpin)");
        _pinButton.Click += (_, _) => SetPinned(!_pinned, raiseEvent: true);

        _collapseButton = new Button
        {
            Padding = new Avalonia.Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceXs),
            MinWidth = DesignTokens.MinControlSize,
            MinHeight = DesignTokens.MinControlSize,
        };
        _collapseButton.Click += (_, _) => SetCollapsed(!_collapsed, raiseEvent: true);

        var hideButton = new Button
        {
            Content = "✕",
            Padding = new Avalonia.Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceXs),
            MinWidth = DesignTokens.MinControlSize,
            MinHeight = DesignTokens.MinControlSize,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTip.SetTip(hideButton, "Hide");
        hideButton.Click += async (_, _) =>
        {
            await _panel.HideAsync().ConfigureAwait(true);
            HideRequested?.Invoke();
        };

        Grid.SetColumn(title, 0);
        Grid.SetColumn(_pinButton, 1);
        Grid.SetColumn(_collapseButton, 2);
        Grid.SetColumn(hideButton, 3);
        header.Children.Add(title);
        header.Children.Add(_pinButton);
        header.Children.Add(_collapseButton);
        header.Children.Add(hideButton);

        var separator = new Separator { Margin = new Avalonia.Thickness(0) };

        _headerStack = new StackPanel();
        _headerStack.Children.Add(header);
        _headerStack.Children.Add(separator);

        _stripButton = new Button { HorizontalAlignment = HorizontalAlignment.Center, MinWidth = DesignTokens.MinControlSize, MinHeight = DesignTokens.MinControlSize };
        _stripButton.Click += (_, _) =>
        {
            if (_pinned)
                SetCollapsed(false, raiseEvent: true);
            else
                FlyoutRequested?.Invoke();
        };
        _collapsedStrip = new Border { IsVisible = false, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand), Child = _stripButton };

        DockPanel.SetDock(_headerStack, Dock.Top);
        Children.Add(_headerStack);
        Children.Add(_collapsedStrip);
        Children.Add(content);

        ApplyVisualState();
    }

    /// <summary>Gets whether this panel is currently, manually collapsed to its own thin strip (distinct from Auto-Hide — see remarks).</summary>
    public bool IsCollapsed => _collapsed;

    /// <summary>Gets whether this panel is currently pinned (docked normally) — <see langword="false"/> means Auto-Hide.</summary>
    public bool IsPinned => _pinned;

    /// <summary>Gets whether this panel's own thin strip is currently showing, for either reason (manually collapsed, or Auto-Hidden).</summary>
    public bool IsStripShowing => _collapsed || !_pinned;

    /// <summary>
    /// Programmatically sets the collapsed state, exactly as clicking the
    /// header's own collapse button would — the same public/testable
    /// pattern <see cref="DockingGrid.NotifyLeftPanelResized"/> already
    /// establishes for a real-drag-only interaction.
    /// </summary>
    public void SetCollapsed(bool collapsed) => SetCollapsed(collapsed, raiseEvent: true);

    /// <summary>Programmatically sets the pinned state, exactly as clicking the header's own pin button would.</summary>
    public void SetPinned(bool pinned) => SetPinned(pinned, raiseEvent: true);

    private void SetCollapsed(bool collapsed, bool raiseEvent)
    {
        _collapsed = collapsed;
        ApplyVisualState();

        if (raiseEvent)
            CollapseToggled?.Invoke(collapsed);
    }

    private void SetPinned(bool pinned, bool raiseEvent)
    {
        _pinned = pinned;
        _pinButton.Content = pinned ? "📌" : "📍";
        ToolTip.SetTip(_pinButton, pinned ? "Auto-Hide (unpin)" : "Dock (pin)");
        ApplyVisualState();

        if (raiseEvent)
            PinToggled?.Invoke(pinned);
    }

    /// <summary>Applies every header/body/strip visual consequence of the current (<see cref="_collapsed"/>, <see cref="_pinned"/>) pair in one place — the single source every mutator above calls, so the two flags can never drift out of sync with what is actually on screen.</summary>
    private void ApplyVisualState()
    {
        var showStrip = IsStripShowing;

        _content.IsVisible = !showStrip;
        _headerStack.IsVisible = !showStrip;
        _collapsedStrip.IsVisible = showStrip;
        _collapseButton.IsVisible = _pinned;

        _stripButton.Content = _pinned ? ExpandGlyph() : AutoHideTabGlyph();
        ToolTip.SetTip(_collapsedStrip, _panel.Title);

        _collapseButton.Content = _collapsed ? ExpandGlyph() : CollapseGlyph();
        ToolTip.SetTip(_collapseButton, _collapsed ? "Expand" : "Collapse");
    }

    /// <summary>The collapse-button glyph for the current, not-yet-collapsed state — a direction hint towards the panel's own dock edge (<see cref="IWorkspacePanel.DockPosition"/>).</summary>
    private string CollapseGlyph() => _panel.DockPosition switch
    {
        WorkspaceDockPosition.Left => "◀",
        WorkspaceDockPosition.Right => "▶",
        WorkspaceDockPosition.Bottom => "▼",
        _ => "◀",
    };

    /// <summary>The collapse-button/strip glyph shown once already collapsed (points back towards expanding).</summary>
    private string ExpandGlyph() => _panel.DockPosition switch
    {
        WorkspaceDockPosition.Left => "▶",
        WorkspaceDockPosition.Right => "◀",
        WorkspaceDockPosition.Bottom => "▲",
        _ => "▶",
    };

    /// <summary>The Auto-Hide edge-tab glyph — a pin outline, distinguishing "click to peek" from Collapse's own "click to expand in place" (<see cref="ExpandGlyph"/>).</summary>
    private static string AutoHideTabGlyph() => "📍";
}
