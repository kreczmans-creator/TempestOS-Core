using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Toast Notification Framework (`WP 10.5A` scope: "toast
/// notifications, success messages, warning messages, error
/// presentation") — a real, transient, auto-dismissing feedback surface,
/// stacked bottom-right, complementing (never replacing)
/// <see cref="StatusBarView"/>'s own persistent one-line text. Every
/// existing <c>ActionCompleted</c> event this platform already raises
/// (<see cref="Editors.ObjectEditorView"/>, <see cref="RibbonView"/>,
/// <see cref="ProjectExplorerView"/>, <see cref="PropertyInspectorView"/>,
/// <see cref="DigitalThread.DigitalThreadGraphView"/>) can now feed a
/// Toast in addition to the Status Bar — a second, more prominent
/// rendering of the identical message, never a new data path
/// (`WP10.5A Architecture Review.md` §1).
/// </summary>
public sealed class ToastHost : StackPanel
{
    /// <summary>Initialises a new instance of the <see cref="ToastHost"/> class.</summary>
    public ToastHost()
    {
        Orientation = Orientation.Vertical;
        Spacing = DesignTokens.SpaceSm;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Avalonia.Thickness(DesignTokens.SpaceXl);
        IsHitTestVisible = true;
    }

    /// <summary>How many toasts are currently visible — exposed for tests, never used by production logic to make a decision.</summary>
    public int ActiveToastCount => Children.Count;

    /// <summary>
    /// The auto-dismiss duration used whenever <see cref="Show"/> is
    /// called without an explicit <c>duration</c> (`WP 10.5B`, real
    /// "Notifications" User Setting — <see cref="UserSettings.ToastDurationSeconds"/>).
    /// Defaults to 4.5s, the same value this platform already used
    /// before it became configurable (`WP 10.5A`).
    /// </summary>
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromSeconds(4.5);

    /// <summary>
    /// Shows a new toast. Auto-dismisses after <paramref name="duration"/>
    /// (default ~4.5s) unless the user dismisses it first via its own
    /// close glyph. Never throws — a real, working feedback surface, not
    /// a placeholder.
    /// </summary>
    public void Show(string message, FeedbackSeverity severity, TimeSpan? duration = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var toast = new ToastNotification(message, severity);
        toast.Dismissed += () => Children.Remove(toast);
        Children.Add(toast);

        var timer = new DispatcherTimer { Interval = duration ?? DefaultDuration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            toast.RequestDismiss();
        };
        timer.Start();
    }

    /// <summary>Dismisses every currently-visible toast immediately — used by tests, and available for a real "clear all" affordance later.</summary>
    public void DismissAll()
    {
        foreach (var child in Children.ToArray())
        {
            if (child is ToastNotification toast)
                toast.RequestDismiss();
        }
    }
}

/// <summary>One toast's own real, rendered content — an icon, a message, and a dismiss button, coloured by <see cref="FeedbackSeverity"/> (<see cref="SeverityColors"/>).</summary>
public sealed class ToastNotification : Border
{
    /// <summary>Raised once this toast should be removed from its host — either the auto-dismiss timer elapsed or the user clicked the close glyph.</summary>
    public event Action? Dismissed;

    /// <summary>Initialises a new instance of the <see cref="ToastNotification"/> class.</summary>
    public ToastNotification(string message, FeedbackSeverity severity)
    {
        ArgumentNullException.ThrowIfNull(message);
        Severity = severity;
        Message = message;

        MinWidth = 260;
        MaxWidth = 380;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Avalonia.Thickness(1);
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);

        // A live region (`WP 16.5A`, `TD-65`) — a screen reader announces
        // this the moment it appears, without the user needing to have
        // focus anywhere near it; `Assertive` (interrupts, rather than
        // waiting its turn) because a toast is transient and auto-dismisses
        // — `Polite` could mean it is gone again before it is ever
        // announced.
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Assertive);

        // The severity as a 2px rule on the top edge — the design system's
        // card status rule — beside the glyph and the word, never colour alone.
        BorderThickness = new Avalonia.Thickness(1, DesignTokens.RuleThickness + 1, 1, 1);
        BorderBrush = SeverityColors.Resolve(severity);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        var icon = new TextBlock
        {
            Text = SeverityColors.Glyph(severity),
            Foreground = SeverityColors.Resolve(severity),
            FontSize = DesignTokens.IconSizeSmall,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(0, 0, DesignTokens.SpaceSm, 0),
        };
        Grid.SetColumn(icon, 0);

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 1);

        var close = new Button
        {
            Content = Icons.IconGeometry.Build(Icons.IconGeometry.Close, 10),
            Padding = new Avalonia.Thickness(DesignTokens.SpaceXs),
            MinWidth = 0,
            MinHeight = 0,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        close.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(close, "Dismiss notification");
        ToolTip.SetTip(close, "Dismiss");
        close.Click += (_, _) => RequestDismiss();
        Grid.SetColumn(close, 2);

        row.Children.Add(icon);
        row.Children.Add(text);
        row.Children.Add(close);
        Child = row;
    }

    /// <summary>This toast's own severity.</summary>
    public FeedbackSeverity Severity { get; }

    /// <summary>This toast's own message text.</summary>
    public string Message { get; }

    /// <summary>Requests dismissal — safe to call more than once (the second call is a harmless no-op, since <see cref="Dismissed"/>'s own subscriber removes this control from its parent on the first).</summary>
    public void RequestDismiss() => Dismissed?.Invoke();
}
