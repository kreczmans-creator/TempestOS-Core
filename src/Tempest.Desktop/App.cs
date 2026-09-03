using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop;

/// <summary>
/// The Application Bootstrap's own Avalonia entry point (`WP 10.0B`).
/// Applies the TempestOS theme — the Fluent control theme recoloured and
/// re-shaped to the Tempest Engineering Design System
/// (<see cref="TempestTheme"/>; `WP10.0A Visual Design System.md` §1's
/// Theme Framework, realigned to the brand) — and, once Avalonia's own
/// framework initialisation completes, builds the Engineering Workspace
/// (<see cref="WorkspaceHost"/>) and shows the <see cref="MainWindow"/> —
/// the graphical presentation layer's own equivalent of
/// <see cref="Tempest.App.Workspace.WorkspaceShell"/>'s construction plus
/// <c>RunAsync</c>.
/// </summary>
public sealed class App : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        // The crash record (`WP-Z4` Stage 28) — installed first, before any
        // theme, window or Runtime Host exists, so a failure anywhere in
        // start-up still leaves a trace. `Tempest.Desktop` is a `WinExe`
        // with no console and the platform has no file log sink, so without
        // this an unhandled start-up exception is invisible.
        Diagnostics.CrashLog.Install();

        Name = "TempestOS";

        // The brand's home ground is the instrument (dark) theme —
        // "Dark first", the design system's own words. The persisted
        // choice (`ThemeService`) still wins the moment the window opens.
        RequestedThemeVariant = ThemeVariant.Dark;

        // The control theme, the platform's own theme-reactive brush
        // resources (`WP 10.5A`, `ApplicationPalette`) and the brand's
        // semantic palette — registered here, before any Window/control
        // exists, so every control's own first render already resolves
        // the correct variant value.
        TempestTheme.Apply(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var host = new WorkspaceHost();

            // Avalonia's own startup path is synchronous; the Engineering
            // Workspace's own StartAsync (Runtime Host discovery/DI
            // construction, then all six real disciplines) completes here,
            // before the first frame renders — the graphical equivalent of
            // WorkspaceShell.StartAsync's own synchronous-from-the-caller's-
            // perspective await, just performed once at process start
            // rather than awaited inline in top-level statements.
            host.StartAsync().GetAwaiter().GetResult();

            var window = new MainWindow(host);
            desktop.MainWindow = window;

            // Professional Error Handling (`WP 10.5B` scope: "unexpected
            // exceptions") — a genuinely unobserved faulted `Task`
            // (fire-and-forget code that threw and was never awaited) is
            // the one unhandled-exception surface that reaches this point
            // without the process already terminating, so routing it to a
            // real, visible dialog is both safe and honest — never a
            // silent swallow, and never claiming to catch a truly fatal
            // `AppDomain`-level crash (see `MainWindow.ShowUnexpectedErrorAsync`'s
            // own remarks).
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                e.SetObserved();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = window.ShowUnexpectedErrorAsync(e.Exception));
            };

            // Window Lifecycle: persist session state on the way out,
            // exactly as the console WorkspaceShell.StopAsync path already
            // does via WorkspaceManager.ShutdownAsync (ADR-0064, unchanged).
            // Every Desktop-local save (panel UI state, `WP 10.2B`; window
            // geometry, `WP 10.5B`) — plus the real unsaved-work
            // confirmation gate — lives entirely in `MainWindow.Closing`
            // (`WP 10.5B`): by the time `ShutdownRequested` fires, `Closing`
            // has already run to completion (a genuine `Closing`
            // cancellation keeps the window, and therefore the application,
            // open — this handler is never reached until the user has
            // actually agreed to exit), so only the Workspace's own
            // separate save remains here.
            desktop.ShutdownRequested += (_, _) =>
            {
                host.ShutdownAsync().GetAwaiter().GetResult();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
