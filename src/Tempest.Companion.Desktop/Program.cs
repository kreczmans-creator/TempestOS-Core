using Avalonia;

namespace Tempest.Companion;

/// <summary>
/// The Companion's desktop head — hosts the shared single-view shell in
/// a phone-frame window (`ADR-0113`; head split per `WP 14.2A`,
/// `ADR-0116`): the development harness, and the runnable form on a
/// desktop OS. The Android and iOS heads bootstrap the identical shared
/// <see cref="App"/> through their own platform entry points.
/// </summary>
public static class Program
{
    /// <summary>The process entry point.</summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>Builds the desktop head's <see cref="AppBuilder"/>.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();
}
