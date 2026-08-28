using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Tempest.Companion.Client;
using Tempest.Companion.Offline;
using Tempest.Companion.Services;
using Tempest.Companion.Theming;
using Tempest.Companion.Views;

namespace Tempest.Companion;

/// <summary>
/// The TempestOS Companion application — a single-view mobile shell
/// (`WP 14.0A`, <c>ADR-0113</c>). Mirrors <c>Tempest.Desktop.App</c>'s
/// own composition shape (Fluent theme, palette registration, view built
/// in <see cref="OnFrameworkInitializationCompleted"/>) while supporting
/// both lifetimes: <see cref="ISingleViewApplicationLifetime"/> (the
/// mobile form — Android/iOS heads plug in here, `TD-57`) and
/// <see cref="IClassicDesktopStyleApplicationLifetime"/> (a phone-frame
/// desktop window — the runnable form today, and the development
/// harness).
/// </summary>
public sealed class App : Application
{
    private CompanionSettingsStore? _settingsStore;
    private CompanionApiClient? _apiClient;

    /// <inheritdoc />
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        BrandPalette.Register(this);

        // Dark first - the instrument theme is the brand's home ground
        // (WP 14.1A); the paper theme remains a deliberate choice.
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        _settingsStore = new CompanionSettingsStore();
        var settings = _settingsStore.Load();

        RequestedThemeVariant = settings.Theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        var shell = BuildShell(settings);

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new Window
                {
                    Title = "TempestOS Companion",
                    Width = 393,
                    Height = 852,
                    MinWidth = 320,
                    MinHeight = 480,
                    Content = shell,
                };
                break;

            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = shell;
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private CompanionShellView BuildShell(CompanionClientSettings settings)
    {
        _apiClient?.Dispose();
        _apiClient = new CompanionApiClient(
            Uri.TryCreate(settings.ServerUrl, UriKind.Absolute, out _) ? settings.ServerUrl : CompanionClientSettings.Default.ServerUrl,
            settings.IdentityId);

        var cache = new SnapshotCache(_settingsStore!.RootPath);
        var dataService = new CompanionDataService(_apiClient, cache);

        var shell = new CompanionShellView(dataService, settings);

        shell.SettingsSaved += edited =>
        {
            _settingsStore.Save(edited);

            // Reconnect with the edited settings by rebuilding the whole
            // connected stack - one composition path, no partially-updated
            // client state.
            var rebuilt = BuildShell(edited);
            SwapShell(rebuilt);
        };

        shell.ClearLocalDataRequested += cache.Clear;

        return shell;
    }

    private void SwapShell(CompanionShellView shell)
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime { MainWindow: not null } desktop:
                desktop.MainWindow.Content = shell;
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = shell;
                break;
        }
    }
}
