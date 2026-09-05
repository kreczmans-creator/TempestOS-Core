using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using Tempest.Desktop.Tests;
using Tempest.Desktop.Theming;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Tempest.Desktop.Tests;

/// <summary>
/// A minimal, test-only <see cref="Application"/> — applies the Fluent
/// theme only, deliberately <b>not</b> the production <c>App</c> class's
/// own <c>OnFrameworkInitializationCompleted</c> (which builds a real
/// <see cref="WorkspaceHost"/> and shows a <see cref="MainWindow"/>
/// automatically on process start) — each test below constructs its own
/// <see cref="WorkspaceHost"/> explicitly instead, so tests remain
/// independent of one another rather than sharing one process-wide
/// Workspace instance.
/// </summary>
public sealed class HeadlessTestApp : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // `WP 10.5A`'s own `ApplicationPalette` — registered here too, so
        // every test exercising a control that resolves one of its keys
        // (`PanelHostControl`, `CommandPaletteOverlay`, the new Toast/
        // Dialog/EmptyState controls) sees the identical resolution
        // behaviour a real launch would, never a null/default fallback.
        ApplicationPalette.Register(this);
    }
}

/// <summary>The <c>Avalonia.Headless.XUnit</c> entry point this assembly's own tests run against — a real Avalonia runtime, with no display attached (`WP 10.0B`'s own disclosed new test strategy, `ADR-0094`).</summary>
public static class TestAppBuilder
{
    /// <remarks>
    /// `WP 16.4A` (part 2), `TD-100`: <c>.UseSkia()</c> registers the real
    /// Skia rendering subsystem, and <c>UseHeadlessDrawing = false</c>
    /// tells <c>.UseHeadless</c> not to overwrite that registration with
    /// its own stub renderer (its default, <c>UseHeadlessDrawing = true</c>,
    /// is exactly what made <see cref="Avalonia.Media.Imaging.Bitmap"/>
    /// decodes report every image as 1x1 regardless of its actual bytes —
    /// <c>Avalonia.Headless.AvaloniaHeadlessPlatformExtensions.UseHeadless</c>
    /// only calls <c>UseRenderingSubsystem</c> for the stub when this flag
    /// is true, confirmed by decompiling `Avalonia.Headless` 11.3.20).
    /// <c>.UseSkia()</c> must run before <c>.UseHeadless</c> so the
    /// rendering subsystem it registers is the one still in effect when
    /// <c>UseHeadless</c> declines to replace it. Windows still render
    /// with no real display attached — only the decode/draw path changes.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
