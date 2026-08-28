using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Tempest.Companion.Client;
using Tempest.Companion.Contracts;
using Tempest.Companion.Offline;
using Tempest.Companion.Services;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// The Companion's secondary surface: platform notifications (as
/// log-levelled lines — the pack's <c>INFO WARN ERR OK</c> vocabulary),
/// connection settings, appearance, local-data hygiene, and product
/// identity (the supplied TEMPEST OS lockup artwork, `WP 14.1A`).
/// </summary>
public sealed class MorePage : CompanionPage
{
    private readonly CompanionDataService _data;
    private readonly CompanionClientSettings _settings;
    private readonly Action<CompanionClientSettings> _onSaveSettings;
    private readonly Action _onClearLocalData;

    /// <summary>Initialises a new instance of the <see cref="MorePage"/> class.</summary>
    /// <param name="data">The Companion data service.</param>
    /// <param name="settings">The current settings, rendered into the form.</param>
    /// <param name="onSaveSettings">Invoked with the edited settings when Save is tapped — the host persists and reconnects.</param>
    /// <param name="onClearLocalData">Invoked when Clear Local Data is confirmed — the host clears the snapshot cache.</param>
    public MorePage(
        CompanionDataService data,
        CompanionClientSettings settings,
        Action<CompanionClientSettings> onSaveSettings,
        Action onClearLocalData)
        : base("More")
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(onSaveSettings);
        ArgumentNullException.ThrowIfNull(onClearLocalData);

        _data = data;
        _settings = settings;
        _onSaveSettings = onSaveSettings;
        _onClearLocalData = onClearLocalData;
        ShowLoading();
    }

    /// <inheritdoc />
    public override async Task RefreshAsync()
    {
        ShowLoading();
        var result = await _data.GetNotificationsAsync();

        // Settings and identity must stay reachable even fully offline -
        // the page renders regardless of what the notifications fetch did.
        var column = new StackPanel { Spacing = CompanionTokens.CardSpacing };

        if (result.Data is null)
        {
            column.Children.Add(new CompanionCard("Notifications")
                .AddLine(result.Error ?? "Tempest OS is unavailable.", secondary: true));
        }
        else
        {
            if (result.Freshness != DataFreshness.Live)
                column.Children.Add(new FreshnessBanner(result.Freshness, result.FetchedAtUtc, result.Error));

            column.Children.Add(NotificationsCard(result.Data));
        }

        column.Children.Add(SettingsCard());
        column.Children.Add(AboutCard());

        ShowContent(column);
    }

    private static CompanionCard NotificationsCard(NotificationListDto list)
    {
        var app = Avalonia.Application.Current!;
        var card = new CompanionCard($"Notifications · {list.Notifications.Count}");

        if (list.Notifications.Count == 0)
            return card.AddLine("No platform notifications since Tempest OS started.", secondary: true);

        foreach (var notification in list.Notifications.Take(20))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = CompanionTokens.SpaceMd };
            row.Children.Add(new LogLevelBadge(
                CompanionStatusColors.LogLevelFor(notification.Severity),
                CompanionStatusColors.ForSeverity(notification.Severity)));
            row.Children.Add(new StackPanel
            {
                Spacing = CompanionTokens.SpaceXs,
                Children =
                {
                    new TextBlock
                    {
                        Text = notification.Message,
                        TextWrapping = TextWrapping.Wrap,
                        FontFamily = CompanionTokens.BodyFont,
                        FontSize = CompanionTokens.FontSizeBody,
                        Foreground = BrandPalette.Brush(app, BrandPalette.BodyTextBrushKey),
                        MaxWidth = 260,
                    },
                    new TextBlock
                    {
                        Text = $"{notification.Category.ToLowerInvariant()} · {FormatMoment(notification.OccurredAtUtc)}",
                        FontFamily = CompanionTokens.MonoFont,
                        FontSize = CompanionTokens.FontSizeCaption,
                        Foreground = BrandPalette.Brush(app, BrandPalette.SecondaryTextBrushKey),
                    },
                },
            });
            card.AddContent(row);
        }

        return card;
    }

    private CompanionCard SettingsCard()
    {
        var card = new CompanionCard("Connection & Appearance");

        var serverBox = SettingsBox(_settings.ServerUrl, "Tempest OS server URL");
        var identityBox = SettingsBox(_settings.IdentityId, "Identity id (configured on the Tempest OS host)");

        card.AddLine("Server", secondary: true);
        card.AddContent(serverBox);
        card.AddLine("Identity", secondary: true);
        card.AddContent(identityBox);
        card.AddLine("Identity is asserted to the platform's configured identity model (no password exists in this release); the platform authorises each request per route.", secondary: true);

        var themeToggle = BrandButtons.Quiet(ThemeToggleLabel());
        themeToggle.HorizontalAlignment = HorizontalAlignment.Stretch;
        themeToggle.Click += (_, _) =>
        {
            var app = Avalonia.Application.Current!;
            var toLight = app.RequestedThemeVariant != ThemeVariant.Light;
            app.RequestedThemeVariant = toLight ? ThemeVariant.Light : ThemeVariant.Dark;
            themeToggle.Content = ThemeToggleLabel();
            _onSaveSettings(_settings with { Theme = toLight ? "Light" : "Dark" });
        };
        card.AddContent(themeToggle);

        var save = BrandButtons.Accent("Save & Reconnect");
        save.HorizontalAlignment = HorizontalAlignment.Stretch;
        save.Click += (_, _) => _onSaveSettings(_settings with
        {
            ServerUrl = serverBox.Text?.Trim() ?? string.Empty,
            IdentityId = identityBox.Text?.Trim() ?? string.Empty,
        });
        card.AddContent(save);

        var clear = BrandButtons.Quiet("Clear Local Data");
        clear.HorizontalAlignment = HorizontalAlignment.Stretch;
        clear.Foreground = new SolidColorBrush(BrandPalette.Red500);
        clear.BorderBrush = new SolidColorBrush(BrandPalette.Red500);
        clear.Click += (_, _) =>
        {
            // Two-step confirm - clearing cached engineering data is the
            // device-hygiene path and must not fire on a stray tap.
            if (clear.Content as string == "CLEAR LOCAL DATA")
            {
                clear.Content = "TAP AGAIN TO CONFIRM";
                return;
            }

            _onClearLocalData();
            clear.Content = "LOCAL DATA CLEARED";
            clear.IsEnabled = false;
        };
        card.AddContent(clear);
        card.AddLine("Removes every cached snapshot from this device. Use before lending or retiring the device.", secondary: true);

        return card;
    }

    private static string ThemeToggleLabel() =>
        Avalonia.Application.Current!.RequestedThemeVariant == ThemeVariant.Light
            ? "Switch to instrument (dark) theme"
            : "Switch to paper (light) theme";

    private static TextBox SettingsBox(string text, string automationName)
    {
        var box = new TextBox
        {
            Text = text,
            Watermark = automationName,
            MinHeight = CompanionTokens.MinTouchTarget,
            FontFamily = CompanionTokens.MonoFont,
            FontSize = CompanionTokens.FontSizeCaption,
            CornerRadius = new Avalonia.CornerRadius(CompanionTokens.ControlCornerRadius),
        };
        Avalonia.Automation.AutomationProperties.SetName(box, automationName);
        return box;
    }

    private static CompanionCard AboutCard()
    {
        var app = Avalonia.Application.Current!;
        var card = new CompanionCard("Tempest OS Companion");

        // The supplied lockup artwork itself - the pack's dark-ground
        // variant on the instrument theme, the light-ground variant on
        // paper. Never a redrawn logo.
        var isDark = app.ActualThemeVariant != ThemeVariant.Light;
        var lockupUri = new Uri($"avares://Tempest.Companion/Assets/Brand/tempest-os-logo-{(isDark ? "dark" : "light")}.png");
        card.AddContent(new Image
        {
            Source = new Bitmap(AssetLoader.Open(lockupUri)),
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Avalonia.Thickness(0, CompanionTokens.SpaceSm),
        });

        card.AddLine("The mobile operational window into the Tempest OS engineering platform: awareness, triage, and controlled quick actions. Engineering authoring stays on the desktop Workspace.", secondary: true);
        card.AddMonoLine("chakra-petch · inter · space-mono — SIL OFL 1.1 (Assets/Fonts)");

        return card;
    }
}
