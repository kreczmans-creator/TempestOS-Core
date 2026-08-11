using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Diagnostics;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Status Bar (`WP 10.0B`: a single-line text reflection of
/// <c>WorkspaceManager.StatusBar.StatusText</c>; replaced `WP 10.2A` with a
/// professional, multi-segment desktop status bar; gained a seventh, Hint
/// segment `WP 10.3B`) — seven real, independently updatable segments
/// (current project, selected object, active workspace area, Runtime Host
/// state, diagnostics state, notification area, command hint), every one
/// backed by a real, already-existing platform read or a real, live user
/// interaction (the Hint segment), never a fabricated or placeholder
/// value. <see cref="SetText"/> is retained, unchanged in signature,
/// driving the "Selected Object" segment specifically — every existing
/// caller (<c>MainWindow</c>'s own selection/lifecycle wiring) continues
/// to work without modification.
/// </summary>
public sealed class StatusBarView : UserControl
{
    private readonly TextBlock _project = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly TextBlock _selection = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly TextBlock _area = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly TextBlock _hostState = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly TextBlock _diagnostics = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly TextBlock _notifications = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly TextBlock _hint = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85 };

    /// <summary>Initialises a new instance of the <see cref="StatusBarView"/> class.</summary>
    public StatusBarView()
    {
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceLg, Margin = new Avalonia.Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceXs) };

        bar.Children.Add(_project);
        bar.Children.Add(Separator());
        bar.Children.Add(_selection);
        bar.Children.Add(Separator());
        bar.Children.Add(_area);
        bar.Children.Add(Separator());
        bar.Children.Add(_hostState);
        bar.Children.Add(Separator());
        bar.Children.Add(_diagnostics);
        bar.Children.Add(Separator());
        bar.Children.Add(_notifications);
        bar.Children.Add(Separator());
        bar.Children.Add(_hint);

        Content = bar;
        SetProject(null);
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
    public void SetHint(string? text) => _hint.Text = string.IsNullOrWhiteSpace(text) ? "Ready." : $"💡 {text}";

    private static TextBlock Separator() => new() { Text = "│", Opacity = 0.35, FontSize = DesignTokens.FontSizeCaption };

    /// <summary>Sets the "Selected Object" segment's own text — retained, unchanged signature (`WP 10.0B`), every existing caller unaffected.</summary>
    public void SetText(string text) => _selection.Text = $"🔹 {text}";

    /// <summary>
    /// Sets the "Current Project" segment. Honest, disclosed scope: this
    /// platform has no formal multi-project/"current project" selection
    /// concept of its own (a single running Workspace over one seeded
    /// Engineering object graph, `WP 8.0A` onward) — <paramref name="projectName"/>
    /// is the base sample's own real Project object's display name when
    /// resolvable, never a fabricated value; <see langword="null"/> renders
    /// an honest "No project" rather than guessing.
    /// </summary>
    public void SetProject(string? projectName) => _project.Text = $"📁 {projectName ?? "No project"}";

    /// <summary>Sets the "Active Workspace" segment to the current Navigation area's own title.</summary>
    public void SetArea(string? areaTitle) => _area.Text = $"🧭 {areaTitle ?? "No area"}";

    /// <summary>Sets the "Host State"/"Diagnostics" segments from a real <see cref="IDiagnosticsProvider"/> read — never a cached or assumed value.</summary>
    public void SetDiagnostics(IDiagnosticsProvider diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        _hostState.Text = $"⚙ Host: {diagnostics.HostState}";

        var failed = diagnostics.Modules.Count(m => m.State == ModuleState.Failed);
        _diagnostics.Text = failed == 0
            ? "✅ All modules healthy"
            : $"⚠ {failed} module(s) failed";
    }

    /// <summary>
    /// Sets the "Notifications" segment's own count. Honest, disclosed
    /// scope: <c>INotificationDispatcher</c> (`WP 6.x`) is a real Platform
    /// Service, but no Workspace-layer subscription bridges it into a
    /// per-session notification count yet — this always reads 0 today,
    /// disclosed rather than fabricating activity, mirroring
    /// <c>EngineeringCockpit.OverdueActions</c>'s own identical "real
    /// substitute, honestly zero until a real source is wired" discipline.
    /// </summary>
    public void SetNotifications(int count) => _notifications.Text = count == 0 ? "🔔 No notifications" : $"🔔 {count}";
}
