using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// One card on the Engineering Cockpit dashboard (`WP 10.1A`) — a
/// consistent header (icon + UPPERCASE title) plus content region, used
/// for every Cockpit region so the dashboard reads as one coherent
/// surface rather than twenty independently-styled ones
/// (`WP10.0A UX Architecture Document.md` §1's own "one reason to
/// change" discipline, applied to visual composition).
/// </summary>
/// <remarks>
/// The design system's card: a flat surface fill, one hairline, no
/// shadow, squared 5px corners, and an optional 2px status rule on the
/// <em>top</em> edge (cyan by default, amber/red for state) — never a
/// coloured left border. Content rows come in four shapes — a prose
/// line, an action, a KPI row with a real coverage bar, and a numeric
/// readout — so every card's own information sits in the same grid.
/// </remarks>
public sealed class CockpitCardControl : Border
{
    private readonly StackPanel _content = new() { Spacing = DesignTokens.SpaceSm + 2 };
    private readonly Border _rule = new() { Height = DesignTokens.RuleThickness, VerticalAlignment = VerticalAlignment.Top, IsVisible = false };
    private readonly TextBlock _title;

    /// <summary>Initialises a new instance of the <see cref="CockpitCardControl"/> class with a text glyph header — retained for callers that key their header on <see cref="IconRegistry"/>'s own Kind glyphs.</summary>
    /// <param name="glyph">A short text glyph (`WP10.0A Visual Design System.md` §2 — Icon Framework, <see cref="IconRegistry"/>).</param>
    /// <param name="title">The card's own display title.</param>
    /// <param name="accent">An optional accent brush (a lifecycle/health colour) shown as the 2px top rule — <see langword="null"/> for a neutral card.</param>
    public CockpitCardControl(string glyph, string title, IBrush? accent = null)
        : this(BuildGlyph(glyph), title, accent)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="CockpitCardControl"/> class with a vector icon header.</summary>
    /// <param name="icon">The card's own <see cref="IconGeometry"/> icon.</param>
    /// <param name="title">The card's own display title.</param>
    /// <param name="accent">An optional accent brush shown as the 2px top rule.</param>
    public CockpitCardControl(StreamGeometry icon, string title, IBrush? accent = null)
        : this(IconGeometry.Build(icon ?? throw new ArgumentNullException(nameof(icon)), 14), title, accent)
    {
    }

    private CockpitCardControl(Control? headerIcon, string title, IBrush? accent)
    {
        ArgumentNullException.ThrowIfNull(title);

        CornerRadius = new CornerRadius(DesignTokens.PanelCornerRadius);
        BorderThickness = new Thickness(1);
        ThemeReactiveBrush.Bind(this, BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, BrandPalette.HairlineBrushKey);
        ClipToBounds = true;

        Padding = new Thickness(0);
        Margin = new Thickness(DesignTokens.SpaceMd);
        MinWidth = 280;
        MaxWidth = 440;
        Title = title;
        AutomationProperties.SetName(this, title);

        // The status rule on the top edge — an explicit accent wins
        // outright; a neutral card carries none.
        if (accent is not null)
        {
            _rule.Background = accent;
            _rule.IsVisible = true;
        }

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, VerticalAlignment = VerticalAlignment.Center };
        if (headerIcon is not null)
        {
            var iconHost = new ContentControl { Content = headerIcon, VerticalAlignment = VerticalAlignment.Center };
            ThemeReactiveBrush.Bind(iconHost, TextElement.ForegroundProperty, accent is null ? BrandPalette.MutedTextBrushKey : BrandPalette.HeadingTextBrushKey);
            if (accent is not null)
                iconHost.Foreground = accent;
            header.Children.Add(iconHost);
        }

        _title = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeLabel + 1,
            FontWeight = DesignTokens.WeightHeading,
            LetterSpacing = DesignTokens.LabelTracking,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeReactiveBrush.Bind(_title, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        header.Children.Add(_title);

        var headerRow = new Border { Child = header, Padding = new Thickness(DesignTokens.SpaceXl, DesignTokens.SpaceLg, DesignTokens.SpaceXl, DesignTokens.SpaceMd) };

        var body = new Border { Child = _content, Padding = new Thickness(DesignTokens.SpaceXl, 0, DesignTokens.SpaceXl, DesignTokens.SpaceXl) };

        var root = new StackPanel();
        root.Children.Add(headerRow);
        root.Children.Add(body);

        var layers = new Panel();
        layers.Children.Add(root);
        layers.Children.Add(_rule);
        Child = layers;
    }

    /// <summary>The card's own title, as constructed.</summary>
    public string Title { get; }

    /// <summary>Adds a plain text line to this card's own content region.</summary>
    public CockpitCardControl AddLine(string text, double opacity = 1.0)
    {
        var line = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody, Opacity = opacity, LineHeight = 17 };
        ThemeReactiveBrush.Bind(line, TextBlock.ForegroundProperty, opacity < 1.0 ? BrandPalette.MutedTextBrushKey : BrandPalette.BodyTextBrushKey);
        _content.Children.Add(line);
        return this;
    }

    /// <summary>Adds a small, muted metadata line — a count, a timestamp, a source — set in the mono face the design system reserves for machine data.</summary>
    public CockpitCardControl AddMeta(string text)
    {
        var line = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeCaption };
        ThemeReactiveBrush.Bind(line, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);
        _content.Children.Add(line);
        return this;
    }

    /// <summary>Adds a hero numeric readout — the value in the structural face at readout size, with a caption beneath it.</summary>
    public CockpitCardControl AddReadout(string value, string caption, IBrush? valueBrush = null)
    {
        var stack = new StackPanel { Spacing = DesignTokens.SpaceXs };
        var number = new TextBlock
        {
            Text = value,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHero,
            FontWeight = DesignTokens.WeightHeading,
            LineHeight = DesignTokens.FontSizeHero + 2,
        };
        if (valueBrush is not null)
            number.Foreground = valueBrush;
        else
            ThemeReactiveBrush.Bind(number, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        stack.Children.Add(number);

        var text = new TextBlock { Text = caption, FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap };
        ThemeReactiveBrush.Bind(text, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        stack.Children.Add(text);

        _content.Children.Add(stack);
        return this;
    }

    /// <summary>Adds a clickable line (a flat button, accent-coloured, left-aligned) to this card's own content region.</summary>
    public CockpitCardControl AddAction(string text, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceSm + 1),
            Margin = new Thickness(-DesignTokens.SpaceMd, 0, 0, 0),
            FontSize = DesignTokens.FontSizeBody,
        };
        button.Classes.Add(ChromeStyles.Flat);
        ThemeReactiveBrush.Bind(button, TextElement.ForegroundProperty, BrandPalette.AccentBrushKey);
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

        var row = new StackPanel { Spacing = DesignTokens.SpaceSm };
        var caption = new TextBlock { Text = $"{label}: {value}", FontSize = DesignTokens.FontSizeBody };
        ThemeReactiveBrush.Bind(caption, TextBlock.ForegroundProperty, BrandPalette.BodyTextBrushKey);
        row.Children.Add(caption);

        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = pct,
            Height = 4,
            MinHeight = 4,
            CornerRadius = new CornerRadius(DesignTokens.BadgeCornerRadius),
            Foreground = PercentColour(pct),
        };
        ThemeReactiveBrush.Bind(bar, BackgroundProperty, BrandPalette.HairlineStrongBrushKey);
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
        >= 80 => Healthy,
        >= 40 => Attention,
        _ => Blocked,
    };

    private static readonly IBrush Healthy = new ImmutableSolidColorBrush(BrandPalette.Green500);
    private static readonly IBrush Attention = new ImmutableSolidColorBrush(BrandPalette.Amber500);
    private static readonly IBrush Blocked = new ImmutableSolidColorBrush(BrandPalette.Red500);

    /// <summary>Clears every line/control this card currently shows — used on <c>Refresh</c>.</summary>
    public void Clear() => _content.Children.Clear();

    /// <summary>Renders a caller-supplied text glyph as the header icon — only a text-presentation symbol (an <see cref="IconRegistry"/> Kind glyph) is shown; a colour-emoji codepoint is dropped, since the design system admits no emoji anywhere in the shell.</summary>
    private static Control? BuildGlyph(string glyph)
    {
        ArgumentNullException.ThrowIfNull(glyph);

        if (glyph.Length == 0 || glyph.Any(c => char.IsSurrogate(c)))
            return null;

        return new TextBlock { Text = glyph, FontSize = DesignTokens.FontSizeHeading, VerticalAlignment = VerticalAlignment.Center };
    }
}
