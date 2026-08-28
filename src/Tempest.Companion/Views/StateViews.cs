using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Companion.Branding;
using Tempest.Companion.Offline;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// Brand-styled button factories — the Tempest Engineering Design
/// System's interaction states (`WP 14.1A`): a filled accent button is
/// cyan with ink text, hovers to the LIGHTER cyan, presses to the DARKER
/// cyan, 3px corner, no shadow, no transform; a quiet button is a
/// hairline over the surface. Implemented by overriding the Fluent
/// theme's own state resources per button, so the states are real, not
/// approximated.
/// </summary>
internal static class BrandButtons
{
    /// <summary>Creates a filled accent (primary action) button.</summary>
    public static Button Accent(string label)
    {
        var button = Base(label);
        var onAccent = new SolidColorBrush(BrandPalette.Ink900);

        button.Background = new SolidColorBrush(BrandPalette.Cyan500);
        button.Foreground = onAccent;
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(BrandPalette.Cyan400);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(BrandPalette.Cyan600);
        button.Resources["ButtonForegroundPointerOver"] = onAccent;
        button.Resources["ButtonForegroundPressed"] = onAccent;

        return button;
    }

    /// <summary>Creates a quiet (secondary) button — a hairline over the surface.</summary>
    public static Button Quiet(string label)
    {
        var app = Avalonia.Application.Current!;
        var button = Base(label);

        button.Background = Brushes.Transparent;
        button.BorderBrush = BrandPalette.Brush(app, BrandPalette.CardBorderBrushKey);
        button.BorderThickness = new Avalonia.Thickness(1);
        button.Foreground = BrandPalette.Brush(app, BrandPalette.BodyTextBrushKey);
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(BrandPalette.Paper050, 0.05);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(BrandPalette.Paper050, 0.09);

        return button;
    }

    private static Button Base(string label) => new()
    {
        Content = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = CompanionTokens.TitleFont,
            FontSize = CompanionTokens.FontSizeLabel,
            FontWeight = CompanionTokens.WeightLabel,
            LetterSpacing = CompanionTokens.LabelTracking,
        },
        CornerRadius = new Avalonia.CornerRadius(CompanionTokens.ControlCornerRadius),
        MinHeight = CompanionTokens.MinTouchTarget,
        Padding = new Avalonia.Thickness(CompanionTokens.SpaceXl, 0),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };
}

/// <summary>
/// The blueprint grid — the pack's one texture: a 64px grid in
/// 5.5%-opacity cyan, used behind the OS main column (and never behind
/// body text — every card above it is opaque).
/// </summary>
public sealed class BlueprintGridControl : Control
{
    private static readonly Pen GridPen = new(new SolidColorBrush(BrandPalette.Cyan500, 0.055), 1);

    /// <summary>The grid cell size, per the pack.</summary>
    public const double CellSize = 64;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        for (var x = 0.5; x < Bounds.Width; x += CellSize)
            context.DrawLine(GridPen, new Avalonia.Point(x, 0), new Avalonia.Point(x, Bounds.Height));

        for (var y = 0.5; y < Bounds.Height; y += CellSize)
            context.DrawLine(GridPen, new Avalonia.Point(0, y), new Avalonia.Point(Bounds.Width, y));
    }
}

/// <summary>
/// A four-character log-level badge (<c>INFO WARN ERR OK</c>) — the
/// pack's own machine-status vocabulary, in Space Mono on a 2px-cornered
/// hairline chip. Status is carried by the text and its colour together.
/// </summary>
public sealed class LogLevelBadge : Border
{
    /// <summary>Initialises a new instance of the <see cref="LogLevelBadge"/> class.</summary>
    /// <param name="level">The level text — four characters or fewer.</param>
    /// <param name="colour">The level's status colour.</param>
    public LogLevelBadge(string level, IBrush colour)
    {
        CornerRadius = new Avalonia.CornerRadius(CompanionTokens.BadgeCornerRadius);
        BorderThickness = new Avalonia.Thickness(1);
        BorderBrush = colour;
        Padding = new Avalonia.Thickness(CompanionTokens.SpaceMd, CompanionTokens.SpaceXs);
        VerticalAlignment = VerticalAlignment.Top;

        Child = new TextBlock
        {
            Text = level,
            FontFamily = CompanionTokens.MonoFont,
            FontSize = 10,
            Foreground = colour,
        };
    }
}

/// <summary>
/// A page's own loading state — the brand mark over an indeterminate
/// progress line. Every page uses exactly this control, never an ad-hoc
/// spinner.
/// </summary>
public sealed class LoadingStateView : StackPanel
{
    /// <summary>Initialises a new instance of the <see cref="LoadingStateView"/> class.</summary>
    public LoadingStateView()
    {
        Spacing = CompanionTokens.SpaceXl;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        Children.Add(new TempestLogoControl { Width = 44, Height = 44, HorizontalAlignment = HorizontalAlignment.Center });
        Children.Add(new ProgressBar
        {
            IsIndeterminate = true,
            Width = 128,
            Height = 3,
            Foreground = new SolidColorBrush(BrandPalette.Cyan500),
        });
        Children.Add(new TextBlock
        {
            Text = "CONTACTING TEMPEST OS",
            FontFamily = CompanionTokens.MonoFont,
            FontSize = 10,
            LetterSpacing = CompanionTokens.LabelTracking,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.SecondaryTextBrushKey),
        });
    }
}

/// <summary>
/// A page's own error/unavailable state — an <c>ERR</c> log-level badge,
/// the reason in plain prose, and a Retry action. Raw exception detail
/// never reaches this control.
/// </summary>
public sealed class ErrorStateView : StackPanel
{
    /// <summary>Initialises a new instance of the <see cref="ErrorStateView"/> class.</summary>
    /// <param name="message">The user-presentable reason.</param>
    /// <param name="onRetry">The Retry action.</param>
    public ErrorStateView(string message, Action onRetry)
    {
        ArgumentNullException.ThrowIfNull(onRetry);

        Spacing = CompanionTokens.SpaceXl;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        MaxWidth = 320;

        Children.Add(new LogLevelBadge("ERR", new SolidColorBrush(BrandPalette.Red500)) { HorizontalAlignment = HorizontalAlignment.Center });
        Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            FontFamily = CompanionTokens.BodyFont,
            FontSize = CompanionTokens.FontSizeBody,
            Foreground = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.BodyTextBrushKey),
        });

        var retry = BrandButtons.Accent("Retry");
        retry.MinWidth = 128;
        retry.HorizontalAlignment = HorizontalAlignment.Center;
        retry.Click += (_, _) => onRetry();
        Children.Add(retry);
    }
}

/// <summary>
/// A page's own honest empty state — what is absent, and what to do
/// about it, in one line. Distinct from <see cref="ErrorStateView"/> by
/// design: "no data exists" and "data could not be fetched" are never
/// conflated.
/// </summary>
public sealed class EmptyStateView : StackPanel
{
    /// <summary>Initialises a new instance of the <see cref="EmptyStateView"/> class.</summary>
    /// <param name="message">What is empty, and what would populate it.</param>
    public EmptyStateView(string message)
    {
        Spacing = CompanionTokens.SpaceLg;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        MaxWidth = 320;

        Children.Add(new LogLevelBadge("OK", new SolidColorBrush(BrandPalette.Green500)) { HorizontalAlignment = HorizontalAlignment.Center });
        Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            FontFamily = CompanionTokens.BodyFont,
            FontSize = CompanionTokens.FontSizeBody,
            Foreground = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.SecondaryTextBrushKey),
        });
    }
}

/// <summary>
/// The freshness disclosure banner (<c>ADR-0115</c>) — machine data in
/// Space Mono: the state, the UTC fetch time with a trailing <c>Z</c>
/// (the pack's timestamp convention), and why live data is unavailable.
/// State is conveyed by text and colour together, never colour alone.
/// </summary>
public sealed class FreshnessBanner : Border
{
    /// <summary>Initialises a new instance of the <see cref="FreshnessBanner"/> class.</summary>
    /// <param name="freshness">The data's own freshness.</param>
    /// <param name="fetchedAtUtc">When the data was fetched, or <see langword="null"/>.</param>
    /// <param name="error">Why live data is unavailable, or <see langword="null"/>.</param>
    public FreshnessBanner(DataFreshness freshness, DateTimeOffset? fetchedAtUtc, string? error)
    {
        var colour = CompanionStatusColors.ForFreshness(freshness);

        CornerRadius = new Avalonia.CornerRadius(CompanionTokens.ControlCornerRadius);
        BorderThickness = new Avalonia.Thickness(1);
        BorderBrush = colour;
        Padding = new Avalonia.Thickness(CompanionTokens.SpaceLg, CompanionTokens.SpaceMd);

        var stamp = fetchedAtUtc?.ToUniversalTime();
        var text = freshness switch
        {
            DataFreshness.Cached => $"CACHED · fetched {stamp:HH:mm}Z · {error}",
            DataFreshness.Stale => $"STALE · fetched {stamp:yyyy-MM-dd HH:mm}Z · {error}",
            _ => $"OFFLINE · {error}",
        };

        Child = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = CompanionTokens.MonoFont,
            FontSize = CompanionTokens.FontSizeCaption,
            Foreground = colour,
        };
    }
}

/// <summary>
/// The app bar's connection readout — a status dot plus <c>LIVE</c>/
/// <c>OFFLINE</c> in Space Mono, the pack's status-dot idiom. Updated by
/// the shell from <c>CompanionDataService.ConnectionStateChanged</c>.
/// </summary>
public sealed class StatusPill : StackPanel
{
    private readonly TextBlock _dot;
    private readonly TextBlock _label;

    /// <summary>Initialises a new instance of the <see cref="StatusPill"/> class.</summary>
    public StatusPill()
    {
        Orientation = Orientation.Horizontal;
        Spacing = CompanionTokens.SpaceSm;
        VerticalAlignment = VerticalAlignment.Center;

        _dot = new TextBlock { Text = "●", FontSize = 9, VerticalAlignment = VerticalAlignment.Center };
        _label = new TextBlock
        {
            FontFamily = CompanionTokens.MonoFont,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Children.Add(_dot);
        Children.Add(_label);

        Update(null);
    }

    /// <summary>Updates the readout — <see langword="null"/> before the first fetch.</summary>
    public void Update(bool? connected)
    {
        var colour = connected switch
        {
            true => (IBrush)new SolidColorBrush(BrandPalette.Cyan500),
            false => new SolidColorBrush(BrandPalette.Red500),
            null => new SolidColorBrush(BrandPalette.Slate500),
        };

        _dot.Foreground = colour;
        _label.Foreground = colour;
        _label.Text = connected switch { true => "LIVE", false => "OFFLINE", null => "—" };
    }
}
