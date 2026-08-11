using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop;

/// <summary>
/// The Application Bootstrap's own Avalonia entry point (`WP 10.0B`).
/// Applies the Fluent theme (Theme Framework's own visual foundation —
/// `WP10.0A Visual Design System.md` §1) and, once Avalonia's own framework
/// initialisation completes, builds the Engineering Workspace
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
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Light;

        // The platform's own theme-reactive custom brush resources
        // (`WP 10.5A`, `ApplicationPalette`) — registered here, before any
        // Window/control exists, so every control's own first render
        // already resolves the correct Light-variant value.
        ApplicationPalette.Register(this);
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
            // confirmation gate — now lives entirely in `MainWindow.Closing`
            // (`WP 10.5B`, consolidated from this handler's own previous,
            // no-confirmation, unconditional `SaveDesktopUiStateAsync`
            // call): by the time `ShutdownRequested` fires, `Closing` has
            // already run to completion (a genuine `Closing` cancellation
            // keeps the window, and therefore the application, open — this
            // handler is never reached until the user has actually agreed
            // to exit), so only the Workspace's own separate save remains
            // here.
            desktop.ShutdownRequested += (_, _) =>
            {
                host.ShutdownAsync().GetAwaiter().GetResult();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
