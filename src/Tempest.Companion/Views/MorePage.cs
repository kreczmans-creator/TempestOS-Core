using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Tempest.Companion.Branding;
using Tempest.Companion.Client;
using Tempest.Companion.Contracts;
using Tempest.Companion.Offline;
using Tempest.Companion.Services;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// The Companion's secondary surface: platform notifications, connection
/// settings, appearance, local-data hygiene, and product identity.
/// Everything operational lives on the four primary tabs — this page
/// deliberately holds only what does not compete for the cockpit-first
/// landing experience.
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
        var column = new Avalonia.Controls.StackPanel { Spacing = CompanionTokens.CardSpacing };

        if (result.Data is null)
        {
            column.Children.Add(new CompanionCard("◷", "Notifications")
                .AddLine(result.Error ?? "TempestOS is unavailable.", secondary: true));
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
        var card = new CompanionCard("◷", $"Notifications ({list.Notifications.Count})");

        if (list.Notifications.Count == 0)
            return card.AddLine("No platform notifications since TempestOS started.", secondary: true);

        foreach (var notification in list.Notifications.Take(20))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = CompanionTokens.SpaceMd };
            row.Children.Add(new TextBlock
            {
                Text = notification.Severity switch { "Success" => "✓", "Warning" => "⚠", "Error" => "⊗", _ => "ⓘ" },
                Foreground = CompanionStatusColors.ForSeverity(notification.Severity),
                FontSize = CompanionTokens.FontSizeBody,
                VerticalAlignment = VerticalAlignment.Top,
            });
            row.Children.Add(new StackPanel
            {
                Spacing = CompanionTokens.SpaceXs,
                Children =
                {
                    new TextBlock
                    {
                        Text = notification.Message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontFamily = CompanionTokens.BodyFont,
                        FontSize = CompanionTokens.FontSizeBody,
                        MaxWidth = 280,
                    },
                    new TextBlock
                    {
                        Text = $"{notification.Category} · {FormatMoment(notification.OccurredAtUtc)}",
                        FontFamily = CompanionTokens.MonoFont,
                        FontSize = CompanionTokens.FontSizeCaption,
                        Opacity = 0.7,
                    },
                },
            });
            card.AddContent(row);
        }

        return card;
    }

    private CompanionCard SettingsCard()
    {
        var card = new CompanionCard("⚙", "Connection & Appearance");

        var serverBox = new TextBox
        {
            Text = _settings.ServerUrl,
            Watermark = "TempestOS server URL",
            MinHeight = CompanionTokens.MinTouchTarget,
        };
        Avalonia.Automation.AutomationProperties.SetName(serverBox, "TempestOS server URL");

        var identityBox = new TextBox
        {
            Text = _settings.IdentityId,
            Watermark = "Identity id (configured on the TempestOS host)",
            MinHeight = CompanionTokens.MinTouchTarget,
        };
        Avalonia.Automation.AutomationProperties.SetName(identityBox, "Identity id");

        card.AddLine("Server", secondary: true);
        card.AddContent(serverBox);
        card.AddLine("Identity", secondary: true);
        card.AddContent(identityBox);
        card.AddLine("Identity is asserted to the platform's configured identity model (no password exists in this release); the platform authorises each request per route.", secondary: true);

        var themeToggle = new Button
        {
            Content = Avalonia.Application.Current!.RequestedThemeVariant == ThemeVariant.Dark ? "Switch to Light theme" : "Switch to Dark theme",
            MinHeight = CompanionTokens.MinTouchTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        themeToggle.Click += (_, _) =>
        {
            var app = Avalonia.Application.Current!;
            var toDark = app.RequestedThemeVariant != ThemeVariant.Dark;
            app.RequestedThemeVariant = toDark ? ThemeVariant.Dark : ThemeVariant.Light;
            themeToggle.Content = toDark ? "Switch to Light theme" : "Switch to Dark theme";
            _onSaveSettings(_settings with { Theme = toDark ? "Dark" : "Light" });
        };
        card.AddContent(themeToggle);

        var save = new Button
        {
            Content = "Save & Reconnect",
            MinHeight = CompanionTokens.MinTouchTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        save.Click += (_, _) => _onSaveSettings(_settings with
        {
            ServerUrl = serverBox.Text?.Trim() ?? string.Empty,
            IdentityId = identityBox.Text?.Trim() ?? string.Empty,
        });
        card.AddContent(save);

        var clear = new Button
        {
            Content = "Clear Local Data",
            MinHeight = CompanionTokens.MinTouchTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        clear.Click += (_, _) =>
        {
            // Two-step confirm - clearing cached engineering data is the
            // device-hygiene path and must not fire on a stray tap.
            if (clear.Content as string == "Clear Local Data")
            {
                clear.Content = "Tap again to confirm clearing cached data";
                return;
            }

            _onClearLocalData();
            clear.Content = "Local data cleared";
            clear.IsEnabled = false;
        };
        card.AddContent(clear);
        card.AddLine("Removes every cached snapshot from this device. Use before lending or retiring the device.", secondary: true);

        return card;
    }

    private static CompanionCard AboutCard()
    {
        var card = new CompanionCard("▣", "TempestOS Companion");

        var identity = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = CompanionTokens.SpaceLg,
            Margin = new Avalonia.Thickness(0, CompanionTokens.SpaceSm),
        };
        identity.Children.Add(new TempestLogoControl
        {
            Width = 36,
            Height = 36,
            Foreground = new Avalonia.Media.SolidColorBrush(BrandPalette.RoyalBlue),
        });
        identity.Children.Add(new TextBlock
        {
            Text = "TEMPEST OS",
            FontFamily = CompanionTokens.TitleFont,
            FontSize = CompanionTokens.FontSizeTitle,
            FontWeight = CompanionTokens.WeightHeading,
            LetterSpacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        });
        card.AddContent(identity);

        card.AddLine("The mobile operational window into the TempestOS engineering platform: awareness, triage, and controlled quick actions. Engineering authoring stays on the desktop Workspace.", secondary: true);
        card.AddMonoLine("Chakra Petch & Space Mono © their authors, SIL OFL 1.1 (Assets/Fonts).");

        return card;
    }
}
