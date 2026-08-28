using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using Tempest.Companion.Tests;
using Tempest.Companion.Theming;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Tempest.Companion.Tests;

/// <summary>
/// A minimal, test-only <see cref="Application"/> — Fluent theme plus the
/// Companion's own <see cref="BrandPalette"/>, deliberately not the
/// production <c>App</c> class (which builds a connected client stack and
/// shows a window on start) — each view test constructs its own fake data
/// service instead, the identical strategy
/// <c>Tempest.Desktop.Tests.HeadlessTestApp</c> established.
/// </summary>
public sealed class HeadlessTestApp : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        BrandPalette.Register(this);
    }
}

/// <summary>The <c>Avalonia.Headless.XUnit</c> entry point — a real Avalonia runtime with no display attached (`ADR-0094`'s disclosed test strategy, applied to the Companion).</summary>
public static class TestAppBuilder
{
    /// <summary>Builds the headless application — <c>WithInterFont</c> registers the same embedded Inter collection the production <c>Program.BuildAvaloniaApp</c> does, so font resolution behaves identically under test.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
