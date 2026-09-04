using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Diagnostics;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Status Bar (`WP 10.0B`: a single-line text reflection of
/// <c>WorkspaceManager.StatusBar.StatusText</c>; replaced `WP 10.2A` with a
/// professional, multi-segment desktop status bar; gained a Hint segment
/// `WP 10.3B`; realigned to the brand as a sunken instrument strip) —
/// eight real, independently updatable segments (current project, shell
/// location, selected object, active workspace area, Runtime Host state,
/// diagnostics state, notification area, command hint), every one backed
/// by a real, already-existing platform read or a real, live user
/// interaction, never a fabricated or placeholder value. <see cref="SetText"/>
/// is retained, unchanged in signature, driving the "Selected Object"
/// segment specifically — every existing caller continues to work
/// without modification.
/// </summary>
/// <remarks>
/// Each segment is an UPPERCASE micro label (Chakra Petch, wide-tracked)
/// beside its value, per the design system's chrome rules; machine state
/// (host, diagnostics) carries a coloured dot beside its word, never the
/// colour alone. No emoji anywhere — the pack's own rule.
/// </remarks>
public sealed class StatusBarView : UserControl
{
    private readonly TextBlock _project = Value();
    private readonly TextBlock _location = Value();
    private readonly TextBlock _selection = Value();
    private readonly TextBlock _area = Value();
    private readonly TextBlock _hostState = Value();
    private readonly TextBlock _diagnostics = Value();
    private readonly TextBlock _notifications = Value();
    private readonly TextBlock _hint = Value();
    private readonly Border _hostDot = Dot();
    private readonly Border _diagnosticsDot = Dot();

    /// <summary>Initialises a new instance of the <see cref="StatusBarView"/> class.</summary>
    public StatusBarView()
    {
        Height = DesignTokens.StatusBarHeight;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, BrandPalette.SunkenBackgroundBrushKey);

        _selection.TextTrimming = TextTrimming.CharacterEllipsis;
        _hint.TextTrimming = TextTrimming.CharacterEllipsis;
        _project.TextTrimming = TextTrimming.CharacterEllipsis;
        _project.MaxWidth = 260;
        _location.TextTrimming = TextTrimming.CharacterEllipsis;
        _location.MaxWidth = 300;

        AutomationProperties.SetName(_project, "Current project");
        AutomationProperties.SetName(_location, "Location");
        AutomationProperties.SetName(_selection, "Selected object");
        AutomationProperties.SetName(_area, "Active area");
        AutomationProperties.SetName(_hostState, "Host state");
        AutomationProperties.SetName(_diagnostics, "Diagnostics");
        AutomationProperties.SetName(_notifications, "Notifications");
        AutomationProperties.SetName(_hint, "Hint");

        var bar = new DockPanel { Margin = new Thickness(DesignTokens.SpaceLg, 0, DesignTokens.SpaceLg, 0), LastChildFill = true };

        // Left: where the user is and what they are working on.
        AddLeft(bar, Segment("PROJECT", _project));
        AddLeft(bar, Separator());
        AddLeft(bar, Segment("LOCATION", _location));
        AddLeft(bar, Separator());
        AddLeft(bar, Segment("AREA", _area));
        AddLeft(bar, Separator());

        // Right: machine state, read live from the platform.
        AddRight(bar, Segment("HINT", _hint));
        AddRight(bar, Separator());
        AddRight(bar, Segment(null, _notifications, IconGeometry.Build(IconGeometry.Bell, 12)));
        AddRight(bar, Separator());
        AddRight(bar, Segment(null, _diagnostics, _diagnosticsDot));
        AddRight(bar, Separator());
        AddRight(bar, Segment("HOST", _hostState, _hostDot));
        AddRight(bar, Separator());

        // Middle, filling: the selected object / last action.
        bar.Children.Add(Segment("SELECTED", _selection));

        var frame = new Border { Child = bar, BorderThickness = new Thickness(0, 1, 0, 0) };
        ThemeReactiveBrush.Bind(frame, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        Content = frame;

        SetProject(null);
        SetLocation(null);
        SetText("Ready.");
        SetArea(null);
        SetNotifications(0);
        SetHint(null);
    }

    /// <summary>
    /// Sets the "Hint" segment (`WP 10.3B`) — a transient, real reflection
    /// of whatever Ribbon command the pointer is currently hovering over
    /// (<see cref="RibbonView"/>'s own <c>PointerEntered</c>/<c>PointerExited</c>
    /// wiring), never a fabricated or scripted value.
    /// <see langword="null"/>/empty renders an honest "Ready." rather
    /// than a blank segment.
    /// </summary>
    public void SetHint(string? text) => _hint.Text = string.IsNullOrWhiteSpace(text) ? "Ready." : text;

    /// <summary>Sets the "Selected Object" segment's own text — retained, unchanged signature (`WP 10.0B`), every existing caller unaffected.</summary>
    public void SetText(string text) => _selection.Text = text;

    /// <summary>
    /// Sets the "Current Project" segment from the one real
    /// <c>IProjectContext</c> (`TD-84`) — <see langword="null"/> renders an
    /// honest "No project" rather than guessing.
    /// </summary>
    public void SetProject(string? projectName) => _project.Text = projectName ?? "No project";

    /// <summary>Sets the "Active Workspace" segment to the current Navigation area's own title.</summary>
    public void SetArea(string? areaTitle) => _area.Text = areaTitle ?? "No area";

    /// <summary>
    /// Sets the shell-location segment (`TD-89`) — which global module the
    /// user is in, which project area when inside one, and which
    /// engineering scope when in Engineering.
    /// </summary>
    /// <remarks>
    /// Deliberately a <b>separate</b> segment from <see cref="SetArea"/>.
    /// That one names the Engineering Workspace's own discipline area
    /// (Mechanical, Requirements, …) and is owned by the Ribbon; this one
    /// names where the user is in the product. Sharing a segment would have
    /// meant each overwriting the other, so the user could never see both
    /// at once — and the product rule is that they must always be able to
    /// tell where they are <em>and</em> what they are working in.
    /// </remarks>
    public void SetLocation(string? location) => _location.Text = location ?? "—";

    /// <summary>Sets the "Host State"/"Diagnostics" segments from a real <see cref="IDiagnosticsProvider"/> read — never a cached or assumed value.</summary>
    public void SetDiagnostics(IDiagnosticsProvider diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        _hostState.Text = diagnostics.HostState.ToString();
        ThemeReactiveBrush.Bind(_hostDot, Border.BackgroundProperty,
            diagnostics.HostState == HostState.Running ? BrandPalette.SuccessBrushKey : BrandPalette.WarningBrushKey);

        var failed = diagnostics.Modules.Count(m => m.State == ModuleState.Failed);
        _diagnostics.Text = failed == 0
            ? "All modules healthy"
            : $"{failed} module(s) failed";
        ThemeReactiveBrush.Bind(_diagnosticsDot, Border.BackgroundProperty,
            failed == 0 ? BrandPalette.SuccessBrushKey : BrandPalette.DangerBrushKey);
    }

    /// <summary>
    /// Sets the "Notifications" segment's own count. Honest, disclosed
    /// scope: <c>INotificationDispatcher</c> (`WP 6.x`) is a real Platform
    /// Service, but no Workspace-layer subscription bridges it into a
    /// per-session notification count yet — this always reads 0 today,
    /// disclosed rather than fabricating activity.
    /// </summary>
    public void SetNotifications(int count) => _notifications.Text = count == 0 ? "No notifications" : $"{count}";

    // ----------------------------------------------------------------

    private static void AddLeft(DockPanel bar, Control control)
    {
        DockPanel.SetDock(control, Dock.Left);
        bar.Children.Add(control);
    }

    private static void AddRight(DockPanel bar, Control control)
    {
        DockPanel.SetDock(control, Dock.Right);
        bar.Children.Add(control);
    }

    private static StackPanel Segment(string? label, TextBlock value, Control? leading = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm + 1, VerticalAlignment = VerticalAlignment.Center };

        if (leading is not null)
            row.Children.Add(leading);

        if (label is not null)
        {
            var caption = new TextBlock
            {
                Text = label,
                FontFamily = DesignTokens.TitleFont,
                FontSize = DesignTokens.FontSizeLabel - 1,
                FontWeight = DesignTokens.WeightLabel,
                LetterSpacing = DesignTokens.LabelTracking,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ThemeReactiveBrush.Bind(caption, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);
            row.Children.Add(caption);
        }

        row.Children.Add(value);
        return row;
    }

    private static TextBlock Value()
    {
        var text = new TextBlock { FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center };
        ThemeReactiveBrush.Bind(text, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        // A live region (`WP 16.5A`, `TD-65`) — every segment's own text
        // change is announced. `Polite` (waits for the screen reader to
        // finish whatever it is already saying) rather than `Assertive`:
        // this bar updates constantly (selection, hint, host/diagnostics
        // state) and interrupting on every one of those would be worse
        // than saying nothing.
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
        return text;
    }

    private static Border Dot()
    {
        var dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(3.5), VerticalAlignment = VerticalAlignment.Center };
        ThemeReactiveBrush.Bind(dot, Border.BackgroundProperty, BrandPalette.FaintTextBrushKey);
        return dot;
    }

    private static Border Separator()
    {
        var line = new Border { Width = 1, Height = 12, Margin = new Thickness(DesignTokens.SpaceLg, 0), VerticalAlignment = VerticalAlignment.Center };
        ThemeReactiveBrush.Bind(line, Border.BackgroundProperty, BrandPalette.HairlineStrongBrushKey);
        return line;
    }
}
