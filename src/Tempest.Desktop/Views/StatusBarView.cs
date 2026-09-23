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
    // Review board finding #5 (`WP 16.5A-R1`): every other segment's own
    // text changes because a real, discrete backend/navigation event
    // happened — a project opened, a location or area was navigated to,
    // an object was selected, host/diagnostics/notification state
    // changed. `Hint` alone is driven by `RibbonView`'s own
    // `PointerEntered`/`PointerExited`, wired onto *every* ribbon
    // button — a screen-reader user sweeping the pointer across the
    // ribbon got one `Polite` announcement per hover-enter/exit, on top
    // of the button's own accessible name, for every button passed over.
    // `announceChanges: false` below is what excludes it; see `Value`'s
    // own remarks.
    private readonly TextBlock _hint = Value(announceChanges: false);
    private readonly Border _hostDot = Dot();
    private readonly Border _diagnosticsDot = Dot();

    /// <summary>
    /// `WP 19.3A-R1` — every segment this bar will hide before the message
    /// area, each paired with its own leading separator so the two always
    /// go together, ordered lowest priority (hidden first) to highest
    /// (hidden last). See <see cref="MeasureOverride"/>.
    /// </summary>
    private readonly (Control Segment, Control Separator)[] _collapsibleSegments;

    /// <summary>The "SELECTED" message/toast segment's own root control — never hidden; see <see cref="MeasureOverride"/>.</summary>
    private readonly Control _selectedSegment;

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
        var projectSegment = Segment("PROJECT", _project);
        var projectSeparator = Separator();
        AddLeft(bar, projectSegment);
        AddLeft(bar, projectSeparator);
        var locationSegment = Segment("LOCATION", _location);
        var locationSeparator = Separator();
        AddLeft(bar, locationSegment);
        AddLeft(bar, locationSeparator);
        var areaSegment = Segment("AREA", _area);
        var areaSeparator = Separator();
        AddLeft(bar, areaSegment);
        AddLeft(bar, areaSeparator);

        // Right: machine state, read live from the platform.
        var hintSegment = Segment("HINT", _hint);
        var hintSeparator = Separator();
        AddRight(bar, hintSegment);
        AddRight(bar, hintSeparator);
        var notificationsSegment = Segment(null, _notifications, IconGeometry.Build(IconGeometry.Bell, 12));
        var notificationsSeparator = Separator();
        AddRight(bar, notificationsSegment);
        AddRight(bar, notificationsSeparator);
        var diagnosticsSegment = Segment(null, _diagnostics, _diagnosticsDot);
        var diagnosticsSeparator = Separator();
        AddRight(bar, diagnosticsSegment);
        AddRight(bar, diagnosticsSeparator);
        var hostSegment = Segment("HOST", _hostState, _hostDot);
        var hostSeparator = Separator();
        AddRight(bar, hostSegment);
        AddRight(bar, hostSeparator);

        // Middle, filling: the selected object / last action — the status
        // bar's own message/toast area (`ActionOutcomeReporter`,
        // `QuickAccessToolbarFactory`'s honest-failure messages), never
        // included in `_collapsibleSegments` below, so it is the one
        // segment that always yields last.
        _selectedSegment = Segment("SELECTED", _selection);
        bar.Children.Add(_selectedSegment);

        // `WP 19.3A-R1`: below `DesignTokens.CompactShellWidth` the docked
        // segments' own combined natural width can exceed what the bar
        // actually has (`MeasureOverride` below) — a squeezed-to-zero
        // segment does not shrink its own content with it, so its text
        // overflowed the bar rather than being cropped or hidden (found by
        // the layout walk at 1180×760). Priority order, lowest first: a
        // transient hover hint and machine-state trivia give way well
        // before where the user actually is (area, location, project), and
        // the message area above never gives way at all.
        _collapsibleSegments =
        [
            (hintSegment, hintSeparator),
            (notificationsSegment, notificationsSeparator),
            (diagnosticsSegment, diagnosticsSeparator),
            (hostSegment, hostSeparator),
            (areaSegment, areaSeparator),
            (locationSegment, locationSeparator),
            (projectSegment, projectSeparator),
        ];

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
    /// `WP 19.3A-R1` — resets every collapsible segment to visible, then,
    /// only if the bar does not actually have room for all of them plus the
    /// message/toast area's own natural width, hides segments one at a time
    /// in <see cref="_collapsibleSegments"/>'s own priority order until the
    /// budget fits or every collapsible segment is gone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Each segment's own natural width, not the <see cref="DockPanel"/>'s
    /// aggregate <c>DesiredSize</c>.</b> A first version compared
    /// <c>base.MeasureOverride(availableSize).Width</c> against
    /// <paramref name="availableSize"/> and hid nothing: a
    /// <see cref="DockPanel"/> gives its fill child (`SELECTED`) only
    /// whatever is left after every docked sibling, so at measure time the
    /// whole bar's own reported size already, correctly, fits — the fill
    /// child is squeezed toward zero instead, and *its* content, with no
    /// <see cref="TextBlock.TextTrimming"/> on the "SELECTED" caption to
    /// shrink it, is what actually overflowed (found by the layout walk at
    /// 1180×760). Measuring each segment against
    /// <see cref="Avalonia.Size.Infinity"/> directly asks the question that
    /// matters instead: does everything's own natural width actually add up
    /// within what this bar has, budgeting the message area's own natural
    /// width in from the start rather than treating it as whatever is left.
    /// </para>
    /// <para>
    /// Resetting to all-visible before every measure, rather than only ever
    /// hiding more, is what makes this correct on a widen as well as a
    /// narrow: the same real measure pass a resize already runs is what
    /// decides visibility, so there is no stale "was hidden at 1180px,
    /// stayed hidden at 1600px" state to track by hand.
    /// <paramref name="availableSize"/>'s width is infinite only when
    /// nothing has constrained this control yet (an isolated
    /// <c>new StatusBarView()</c> in a unit test, never the running shell,
    /// where it is always docked to the window's own bottom edge) — nothing
    /// is hidden in that case, since there is no real width to be short of.
    /// </para>
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var (segment, separator) in _collapsibleSegments)
        {
            segment.IsVisible = true;
            separator.IsVisible = true;
        }

        LastMeasuredWidth = availableSize.Width;
        MeasurePasses++;

        if (!double.IsInfinity(availableSize.Width))
        {
            var budget = availableSize.Width - DesignTokens.SpaceLg * 2 - NaturalWidth(_selectedSegment);

            foreach (var (segment, separator) in _collapsibleSegments)
                budget -= NaturalWidth(segment) + NaturalWidth(separator);

            foreach (var (segment, separator) in _collapsibleSegments)
            {
                if (budget >= 0)
                    break;

                budget += NaturalWidth(segment) + NaturalWidth(separator);
                segment.IsVisible = false;
                separator.IsVisible = false;
            }
        }

        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// Every text setter ends here. A longer text re-measures its own
    /// segment and the bar's <see cref="DockPanel"/>, but the panel's
    /// desired size is clamped to what it was given, so the change never
    /// reaches this control's own <see cref="MeasureOverride"/> — the
    /// budget stayed as it was when the text was shorter, and the message
    /// area was squeezed 11 px short at 1180×760 once the first Explorer
    /// area's long title landed in AREA (the layout walk, `WP 19.9.0`).
    /// Invalidating this control's own measure makes every text change
    /// re-run the budget.
    /// </summary>
    private void TextChanged() => InvalidateMeasure();

    /// <summary>The width the last <see cref="MeasureOverride"/> was given — read by the layout tests when the collapse did not do what the bar's own contents required.</summary>
    internal double LastMeasuredWidth { get; private set; } = double.NaN;

    /// <summary>How many times <see cref="MeasureOverride"/> has run — the same diagnostic.</summary>
    internal int MeasurePasses { get; private set; }

    private static double NaturalWidth(Control control)
    {
        // A segment whose text grew since it was last measured against
        // infinity still reports the old width: Avalonia re-measures the
        // grown TextBlock on its own, its StackPanel and the DockPanel
        // follow, but the DockPanel's desired size is clamped to what it
        // was given, so the change stops there and never reaches this
        // bar's own measure — and a valid measure against the same
        // infinity is not repeated. The layout walk at 1180×760 found the
        // message area budgeted at 81 px when its text needed 92 (`WP
        // 19.9.0`). Invalidating first makes the answer current.
        control.InvalidateMeasure();
        control.Measure(Size.Infinity);
        return control.DesiredSize.Width;
    }

    /// <summary>
    /// Sets the "Hint" segment (`WP 10.3B`) — a transient, real reflection
    /// of whatever Ribbon command the pointer is currently hovering over
    /// (<see cref="RibbonView"/>'s own <c>PointerEntered</c>/<c>PointerExited</c>
    /// wiring), never a fabricated or scripted value.
    /// <see langword="null"/>/empty renders an honest "Ready." rather
    /// than a blank segment.
    /// </summary>
    public void SetHint(string? text)
    {
        _hint.Text = string.IsNullOrWhiteSpace(text) ? "Ready." : text;
        TextChanged();
    }

    /// <summary>Sets the "Selected Object" segment's own text — retained, unchanged signature (`WP 10.0B`), every existing caller unaffected.</summary>
    public void SetText(string text)
    {
        _selection.Text = text;
        TextChanged();
    }

    /// <summary>
    /// Sets the "Current Project" segment from the one real
    /// <c>IProjectContext</c> (`TD-84`) — <see langword="null"/> renders an
    /// honest "No project" rather than guessing.
    /// </summary>
    public void SetProject(string? projectName)
    {
        _project.Text = projectName ?? "No project";
        TextChanged();
    }

    /// <summary>Sets the "Active Workspace" segment to the current Navigation area's own title.</summary>
    public void SetArea(string? areaTitle)
    {
        _area.Text = areaTitle ?? "No area";
        TextChanged();
    }

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
    public void SetLocation(string? location)
    {
        _location.Text = location ?? "—";
        TextChanged();
    }

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
        TextChanged();
    }

    /// <summary>
    /// Sets the "Notifications" segment's own count. Honest, disclosed
    /// scope: <c>INotificationDispatcher</c> (`WP 6.x`) is a real Platform
    /// Service, but no Workspace-layer subscription bridges it into a
    /// per-session notification count yet — this always reads 0 today,
    /// disclosed rather than fabricating activity.
    /// </summary>
    public void SetNotifications(int count)
    {
        _notifications.Text = count == 0 ? "No notifications" : $"{count}";
        TextChanged();
    }

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

    /// <param name="announceChanges">
    /// Whether this segment's own text changes are announced at all
    /// (review board finding #5, `WP 16.5A-R1`). <see langword="true"/>
    /// (the default) for every segment whose text changes because a
    /// real, discrete state change happened — <see langword="false"/>
    /// only for `Hint`, whose changes are driven by continuous pointer
    /// movement across the Ribbon rather than a discrete event, and would
    /// otherwise announce once per hover-enter/exit on every button swept
    /// over.
    /// </param>
    private static TextBlock Value(bool announceChanges = true)
    {
        var text = new TextBlock { FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center };
        ThemeReactiveBrush.Bind(text, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        // A live region (`WP 16.5A`, `TD-65`) — every segment's own text
        // change is announced. `Polite` (waits for the screen reader to
        // finish whatever it is already saying) rather than `Assertive`:
        // this bar updates constantly (project, location, area, selection,
        // host/diagnostics state) and interrupting on every one of those
        // would be worse than saying nothing. Deliberately NOT set at all
        // for `Hint` (`announceChanges: false`) — an explicit `Off` would
        // still be a live region declaration; omitting the property
        // entirely is the more honest "this is not one".
        if (announceChanges)
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
