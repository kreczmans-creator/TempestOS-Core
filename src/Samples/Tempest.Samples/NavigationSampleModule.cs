using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that contributes a single
/// <see cref="NavigationItem"/> to the platform's Navigation Framework
/// during its own lifecycle, and removes it again on disposal.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module <c>WP 5.0B</c> validates the Navigation
/// Framework against — mirrors <see cref="ClockModule"/>'s own role for the
/// Event Bus. Carries <see cref="ModuleMetadataAttribute"/> so Discovery can
/// read its identity without instantiating it (ADR-0027), freeing its
/// constructor to request <see cref="INavigationProvider"/> — a DI-public
/// platform service — via ordinary constructor injection, exactly as
/// <c>Building a Module.md</c> documents. See ADR-0031, ADR-0032, and
/// <c>Navigation Framework Architecture.md</c> for the Navigation
/// Framework's own ownership and registration model.
/// </para>
/// <para>
/// Registers its item during <see cref="InitialiseAsync"/> and unregisters
/// it during <see cref="DisposeAsync"/> — proving, end to end, that no
/// orphaned navigation entry remains once this module is disposed.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.navigation", "Navigation Sample", "1.0.0")]
public sealed class NavigationSampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The <see cref="NavigationItem.Id"/> this module registers.
    /// </summary>
    public const string NavigationItemId = "tempest.samples.navigation.home";

    private readonly INavigationProvider _navigationProvider;

    /// <summary>
    /// Initialises a new instance of the <see cref="NavigationSampleModule"/> class.
    /// </summary>
    /// <param name="navigationProvider">
    /// The Navigation Framework this module registers its item through,
    /// resolved via ordinary constructor injection.
    /// </param>
    public NavigationSampleModule(INavigationProvider navigationProvider)
        : base("tempest.samples.navigation", "Navigation Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's navigation item.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>Registers this module's <see cref="NavigationItem"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(NavigationItemId, "Home"));
        HasRegistered = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>Unregisters this module's <see cref="NavigationItem"/>.</remarks>
    public override Task DisposeAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Unregister(NavigationItemId);
        HasRegistered = false;

        return Task.CompletedTask;
    }
}
