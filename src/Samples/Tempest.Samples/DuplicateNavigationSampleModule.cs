using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// A reference module that deliberately registers the same
/// <see cref="NavigationItem.Id"/> as <see cref="NavigationSampleModule"/>,
/// so its own <see cref="InitialiseAsync"/> throws
/// <see cref="DuplicateNavigationItemException"/>.
/// </summary>
/// <remarks>
/// Exists solely to prove that a duplicate navigation registration failing
/// inside a module's own <see cref="InitialiseAsync"/> is isolated by
/// <see cref="Modules.ModuleLifecycleManager"/>'s existing, unmodified
/// per-module isolation (ADR-0013) — exactly as ADR-0032 states no new
/// Host-level failure policy is needed for Navigation. Its own <see cref="IModule.Id"/>
/// sorts ordinally after <see cref="NavigationSampleModule"/>'s, so the
/// module pipeline's ascending-order Initialise batch always initialises
/// <see cref="NavigationSampleModule"/> (and its successful registration)
/// first.
/// </remarks>
[ModuleMetadata("tempest.samples.navigation.zzz-duplicate", "Navigation Duplicate Sample", "1.0.0")]
public sealed class DuplicateNavigationSampleModule : ModuleLifecycleBase
{
    private readonly INavigationProvider _navigationProvider;

    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateNavigationSampleModule"/> class.
    /// </summary>
    /// <param name="navigationProvider">
    /// The Navigation Framework this module attempts to register its
    /// (already-taken) item through, resolved via ordinary constructor
    /// injection.
    /// </param>
    public DuplicateNavigationSampleModule(INavigationProvider navigationProvider)
        : base("tempest.samples.navigation.zzz-duplicate", "Navigation Duplicate Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always throws <see cref="DuplicateNavigationItemException"/> — this
    /// module exists solely to trigger and prove isolation of that failure.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(NavigationSampleModule.NavigationItemId, "Duplicate Home"));

        return Task.CompletedTask;
    }
}
