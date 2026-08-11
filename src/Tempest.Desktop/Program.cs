using Avalonia;

namespace Tempest.Desktop;

/// <summary>
/// The desktop application's own process entry point — Application
/// Bootstrap (`WP 10.0B`). `Tempest.Desktop` is TempestOS's shipped
/// desktop application. `Tempest.App` (`WorkspaceShell`) is TempestOS's
/// Internal Engineering Harness, formally classified as such by
/// `ADR-0101` (`WP 11.3B`) — not a second shipped product; see that ADR
/// for the full reasoning. `WP11.3A Presentation Strategy Review.md`
/// found the prior wording here ("`WP 10.0B`'s own explicit... instruction")
/// unverifiable against `WP 10.0B`'s own documentation record; corrected,
/// not repeated, per that review's own disclosed finding.
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
