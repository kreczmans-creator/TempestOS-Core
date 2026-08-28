using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace Tempest.Companion.Android;

/// <summary>
/// The Android head's single Activity — hosts the Companion's
/// single-view shell through Avalonia's own
/// <see cref="AvaloniaMainActivity{TApp}"/> (`WP 14.2A`, `ADR-0116`).
/// The launcher identity (label, the brand-pack icon) lives here; every
/// screen and service lives in <c>Tempest.Companion</c>, unchanged.
/// </summary>
[Activity(
    Label = "Tempest OS Companion",
    Theme = "@style/TempestCompanionTheme",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .WithInterFont();
}
