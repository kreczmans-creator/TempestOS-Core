using Avalonia;
using Avalonia.iOS;
using Foundation;

namespace Tempest.Companion.iOS;

/// <summary>
/// The iOS head's application delegate — hosts the Companion's
/// single-view shell through Avalonia's own
/// <see cref="AvaloniaAppDelegate{TApp}"/> (`WP 14.2A`, `ADR-0116`).
/// Every screen and service lives in <c>Tempest.Companion</c>, unchanged.
/// </summary>
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    /// <inheritdoc />
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .WithInterFont();
}
