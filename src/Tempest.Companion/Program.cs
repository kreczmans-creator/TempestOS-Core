using Avalonia;

namespace Tempest.Companion;

/// <summary>
/// The Companion's desktop-lifetime entry point — hosts the single-view
/// shell in a phone-frame window (`TD-57`: the runnable form until
/// Android/iOS platform heads exist; those heads will call
/// <see cref="BuildAvaloniaApp"/> with their own platform initialisation,
/// exactly Avalonia's standard mobile-head shape).
/// </summary>
public static class Program
{
    /// <summary>The process entry point.</summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>Builds the shared <see cref="AppBuilder"/> every platform head starts from.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();
}
