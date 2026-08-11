using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// One card on the Engineering Cockpit dashboard (`WP 10.1A`) — a
/// consistent header (glyph + title) plus content region, used for every
/// one of the twenty named Cockpit regions so the dashboard reads as one
/// coherent surface rather than twenty independently-styled ones
/// (`WP10.0A UX Architecture Document.md` §1's own "one reason to
/// change" discipline, applied to visual composition).
/// </summary>
public sealed class CockpitCardControl : Border
{
    private readonly StackPanel _content = new() { Spacing = 4 };

    /// <summary>Initialises a new instance of the <see cref="CockpitCardControl"/> class.</summary>
    /// <param name="glyph">A short icon glyph (`WP10.0A Visual Design System.md` §2 — Icon Framework, `Tempest.Desktop.Icons.IconRegistry`).</param>
    /// <param name="title">The card's own display title.</param>
    /// <param name="accent">An optional accent brush (a lifecycle/health colour) shown as a thin left border — <see langword="null"/> for a neutral card.</param>
    public CockpitCardControl(string glyph, string title, IBrush? accent = null)
    {
        CornerRadius = new Avalonia.CornerRadius(6);
        BorderThickness = accent is null ? new Avalonia.Thickness(1) : new Avalonia.Thickness(3, 1, 1, 1);

        // A genuine, real theme-reactive fix (`WP 10.5C`) — this card's
        // own neutral (no-accent) border was a fixed `Brushes.Gray` since
        // `WP 10.1A`, the identical `TD-39` class of defect `WP 10.5A`
        // already found and fixed for `PanelHostControl`/
        // `CommandPaletteOverlay`, never previously found here since this
        // control renders text-only content whose own readability
        // tolerates a slightly-wrong border colour far better than a
        // fully-opaque overlay background does. An explicit `accent` still
        // wins outright — a caller-supplied health/lifecycle colour is
        // never overridden by the neutral theme default.
        if (accent is null)
            ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        else
            BorderBrush = accent;

        Padding = new Avalonia.Thickness(12, 10);
        Margin = new Avalonia.Thickness(6);
        MinWidth = 260;
        MaxWidth = 420;

        var root = new StackPanel { Spacing = 6 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new TextBlock { Text = glyph, FontSize = 18 });
        header.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });

        root.Children.Add(header);
        root.Children.Add(new Separator());
        root.Children.Add(_content);

        Child = root;
    }

    /// <summary>Adds a plain text line to this card's own content region.</summary>
    public CockpitCardControl AddLine(string text, double opacity = 1.0)
    {
        _content.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = opacity });
        return this;
    }

    /// <summary>Adds a clickable line (a button styled as text) to this card's own content region.</summary>
    public CockpitCardControl AddAction(string text, Action onClick)
    {
        var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => onClick();
        _content.Children.Add(button);
        return this;
    }

    /// <summary>Adds an arbitrary control to this card's own content region — used for the KPI-grid cards.</summary>
    public CockpitCardControl AddContent(Control control)
    {
        _content.Children.Add(control);
        return this;
    }

    /// <summary>
    /// Adds one real KPI row (`WP 10.5C`, "KPI cards... progress bars...
    /// verification coverage, requirements coverage") — a label, a real,
    /// coloured <see cref="ProgressBar"/> reading <paramref name="percent"/>
    /// directly (never a second, independently-drawn approximation of the
    /// text beside it), and the identical text
    /// <c>EngineeringCockpit.FormatCoverage</c> already produces, so the
    /// bar and the text can never disagree — both come from the one real
    /// numerator/denominator. Falls back to a plain text line (via
    /// <see cref="AddLine"/>) when <paramref name="percent"/> is
    /// <see langword="null"/> — a non-coverage KPI (a raw count) or a
    /// genuine zero-denominator case, never a fabricated `0%` bar.
    /// </summary>
    public CockpitCardControl AddKpiRow(string label, string value, int? percent, bool isPlaceholder = false)
    {
        if (percent is not { } pct)
            return AddLine(isPlaceholder ? $"{label}: {value}  (placeholder)" : $"{label}: {value}", isPlaceholder ? 0.6 : 1.0);

        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(new TextBlock { Text = $"{label}: {value}", FontSize = 12 });

        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = pct,
            Height = 6,
            CornerRadius = new Avalonia.CornerRadius(3),
            Foreground = PercentColour(pct),
        };
        row.Children.Add(bar);

        _content.Children.Add(row);
        return this;
    }

    /// <summary>
    /// The same Healthy/Attention/Blocked thresholds and colours
    /// <see cref="HealthColors"/> already established platform-wide
    /// (`WP 10.1A`), applied to a raw coverage percentage rather than an
    /// <see cref="Tempest.App.Workspace.EngineeringHealthStatus"/> value —
    /// a real, deliberate reuse of the platform's own existing colour
    /// language, never a new, competing percentage-colour scheme.
    /// </summary>
    private static IBrush PercentColour(int percent) => percent switch
    {
        >= 80 => Brushes.SeaGreen,
        >= 40 => Brushes.DarkOrange,
        _ => Brushes.Crimson,
    };

    /// <summary>Clears every line/control this card currently shows — used on <c>Refresh</c>.</summary>
    public void Clear() => _content.Children.Clear();
}
