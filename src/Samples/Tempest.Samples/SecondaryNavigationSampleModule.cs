using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// A second, independent reference module that contributes its own
/// <see cref="NavigationItem"/>, holding no reference of any kind to
/// <see cref="NavigationSampleModule"/>.
/// </summary>
/// <remarks>
/// Exists solely to prove that multiple, independently-discovered modules
/// can each contribute navigation items without collision or any special
/// coordination between them — mirroring
/// <see cref="ClockLifecycleObserverModule"/>'s own role of proving a second
/// real module composes correctly alongside the first. Registers a
/// <em>grouped</em>, non-default-ordered item, exercising
/// <see cref="NavigationItem.Group"/> and <see cref="NavigationItem.Order"/>
/// against a second, real, SDK-conformant module rather than only a
/// synthetic unit-test value.
/// </remarks>
[ModuleMetadata("tempest.samples.navigation.secondary", "Navigation Secondary Sample", "1.0.0")]
public sealed class SecondaryNavigationSampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The <see cref="NavigationItem.Id"/> this module registers.
    /// </summary>
    public const string NavigationItemId = "tempest.samples.navigation.settings";

    private readonly INavigationProvider _navigationProvider;

    /// <summary>
    /// Initialises a new instance of the <see cref="SecondaryNavigationSampleModule"/> class.
    /// </summary>
    /// <param name="navigationProvider">
    /// The Navigation Framework this module registers its item through,
    /// resolved via ordinary constructor injection.
    /// </param>
    public SecondaryNavigationSampleModule(INavigationProvider navigationProvider)
        : base("tempest.samples.navigation.secondary", "Navigation Secondary Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <inheritdoc />
    /// <remarks>Registers this module's grouped <see cref="NavigationItem"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(NavigationItemId, "Settings", order: 1, group: "Admin"));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>Unregisters this module's <see cref="NavigationItem"/>.</remarks>
    public override Task DisposeAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Unregister(NavigationItemId);

        return Task.CompletedTask;
    }
}
