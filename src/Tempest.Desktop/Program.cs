using Avalonia;

namespace Tempest.Desktop;

/// <summary>
/// The desktop application's own process entry point — Application
/// Bootstrap (`WP 10.0B`). `Tempest.Desktop` is now the primary launch
/// target for TempestOS (`WP 10.0B`'s own explicit "replace the console
/// application as the primary launch target" instruction); the console
/// `Tempest.App`/`TempestShell` remains, retained for diagnostics/testing
/// per that same instruction's own explicit exception.
/// </summary>
public static class Program
{
    /// <summary>Process entry point. <c>[STAThread]</c> is unnecessary on non-Windows platforms and harmless on Windows — Avalonia's own template convention.</summary>
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

    /// <summary>Builds the <see cref="AppBuilder"/> — platform auto-detection (Win32/X11/AvaloniaNative), matching the cross-platform reach `ADR-0094` selected Avalonia specifically to preserve.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
