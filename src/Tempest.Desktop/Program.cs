using Avalonia;
using Velopack;

namespace Tempest.Desktop;

/// <summary>
/// The desktop application's own process entry point — Application
/// Bootstrap (`WP 10.0B`). `Tempest.Desktop` is TempestOS's shipped
/// desktop application. `Tempest.Harness` (`WorkspaceShell`) is TempestOS's
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
    public static void Main(string[] args)
    {
        // DB-08 review: `--svg-probe <file>` reads one hostile SVG in a
        // process of its own and exits with the outcome, for the bounded
        // probes CI runs under memory and time limits
        // (`SvgBoundedProbeTests`). Nothing else starts — not Velopack, not
        // Avalonia — so the exit code and the peak working set are the
        // reader's own.
        if (args.Length > 0 && args[0] == Viewing.SvgProbeRunner.Switch)
        {
            Environment.ExitCode = Viewing.SvgProbeRunner.Run(args, Console.Out);
            return;
        }

        // `WP 21.5A` (`WP RC.0A`): must run first, before any Avalonia or
        // Runtime Host start-up — it is what makes an installed run
        // reportable at all (handles Velopack's own install/update/
        // uninstall/first-run hooks, and publishes
        // `Velopack.Locators.VelopackLocator.Current`, which
        // `Tempest.Desktop.Startup.VelopackInstalledAppLocator` reads).
        // Safe to call unconditionally: verified directly (not merely
        // assumed from documentation) that calling this from a process
        // Velopack never packaged or installed — `dotnet run`, a plain
        // `bin/` exe, the plain release zip — does not throw; it simply
        // reports "not installed", which is exactly what those three run
        // shapes are. `Tempest.Harness` (the Internal Engineering Harness,
        // never shipped — ADR-0101) does not call this: it is not a
        // Velopack-packaged application and has no `Velopack` package
        // reference at all.
        VelopackApp.Build().Run();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Builds the <see cref="AppBuilder"/> — platform auto-detection (Win32/X11/AvaloniaNative), matching the cross-platform reach `ADR-0094` selected Avalonia specifically to preserve.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
