using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Companion.Branding;
using Tempest.Companion.Offline;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// A page's own loading state — the branded mark plus a determinate-free
/// progress ring, shown while a refresh is in flight. Every page uses
/// exactly this control, never an ad-hoc spinner.
/// </summary>
public sealed class LoadingStateView : StackPanel
{
    /// <summary>Initialises a new instance of the <see cref="LoadingStateView"/> class.</summary>
    public LoadingStateView()
    {
        Spacing = CompanionTokens.SpaceXl;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        Children.Add(new TempestLogoControl
        {
            Width = 40,
            Height = 40,
            Foreground = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.AccentBrushKey),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        Children.Add(new ProgressBar { IsIndeterminate = true, Width = 120, Height = 4 });
        Children.Add(new TextBlock
        {
            Text = "Contacting TempestOS…",
            FontFamily = CompanionTokens.BodyFont,
            FontSize = CompanionTokens.FontSizeCaption,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.SecondaryTextBrushKey),
        });
    }
}

/// <summary>
/// A page's own error/unavailable state — what went wrong, in plain
/// words, with a Retry action. Raw exception detail never reaches this
/// control; the API boundary already normalised it
/// (<c>CompanionApiException</c>).
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

        Children.Add(new TextBlock
        {
            Text = "⊗",
            FontSize = 36,
            Foreground = Brushes.Crimson,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            FontFamily = CompanionTokens.BodyFont,
            FontSize = CompanionTokens.FontSizeBody,
        });

        var retry = new Button
        {
            Content = "Retry",
            MinHeight = CompanionTokens.MinTouchTarget,
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        retry.Click += (_, _) => onRetry();
        Children.Add(retry);
    }
}

/// <summary>
/// A page's own honest empty state — the platform answered and there is
/// genuinely nothing to show. Distinct from <see cref="ErrorStateView"/>
/// by design: "no data exists" and "data could not be fetched" are never
/// conflated.
/// </summary>
public sealed class EmptyStateView : StackPanel
{
    /// <summary>Initialises a new instance of the <see cref="EmptyStateView"/> class.</summary>
    /// <param name="glyph">A large icon glyph.</param>
    /// <param name="message">What is empty, and (where useful) what would populate it.</param>
    public EmptyStateView(string glyph, string message)
    {
        Spacing = CompanionTokens.SpaceLg;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        MaxWidth = 320;

        Children.Add(new TextBlock
        {
            Text = glyph,
            FontSize = 36,
            Opacity = 0.5,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
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
/// The freshness disclosure banner (<c>ADR-0115</c>) — shown at the top
/// of a page whenever its data is anything but
/// <see cref="DataFreshness.Live"/>: the state name, when the data was
/// fetched, and why live data is unavailable. State is conveyed by text
/// and colour together, never colour alone.
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

        CornerRadius = new Avalonia.CornerRadius(CompanionTokens.CornerRadius);
        BorderThickness = new Avalonia.Thickness(1);
        BorderBrush = colour;
        Padding = new Avalonia.Thickness(CompanionTokens.SpaceLg, CompanionTokens.SpaceMd);

        var text = freshness switch
        {
            DataFreshness.Cached => $"CACHED — fetched {fetchedAtUtc?.ToLocalTime():HH:mm}. {error}",
            DataFreshness.Stale => $"STALE — fetched {fetchedAtUtc?.ToLocalTime():yyyy-MM-dd HH:mm}. {error}",
            _ => $"OFFLINE — {error}",
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
/// The app bar's connection pill — LIVE/OFFLINE as text plus colour,
/// updated by the shell from
/// <c>CompanionDataService.ConnectionStateChanged</c>.
/// </summary>
public sealed class StatusPill : Border
{
    private readonly TextBlock _label;

    /// <summary>Initialises a new instance of the <see cref="StatusPill"/> class.</summary>
    public StatusPill()
    {
        CornerRadius = new Avalonia.CornerRadius(10);
        Padding = new Avalonia.Thickness(CompanionTokens.SpaceMd, CompanionTokens.SpaceXs);
        VerticalAlignment = VerticalAlignment.Center;

        _label = new TextBlock
        {
            FontFamily = CompanionTokens.MonoFont,
            FontSize = 11,
            Foreground = Brushes.White,
        };
        Child = _label;

        Update(null);
    }

    /// <summary>Updates the pill — <see langword="null"/> before the first fetch.</summary>
    public void Update(bool? connected)
    {
        (_label.Text, Background) = connected switch
        {
            true => ("LIVE", (IBrush)new SolidColorBrush(BrandPalette.ElectricBlue)),
            false => ("OFFLINE", Brushes.Crimson),
            null => ("—", Brushes.Gray),
        };
    }
}
