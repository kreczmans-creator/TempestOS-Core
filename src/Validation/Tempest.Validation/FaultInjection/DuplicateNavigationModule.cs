using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Samples;

namespace Tempest.Validation.FaultInjection;

/// <summary>
/// A fault-injection module that deliberately registers the same
/// <see cref="NavigationItem.Id"/> as <see cref="NavigationSampleModule"/>,
/// so its own <see cref="InitialiseAsync"/> throws
/// <see cref="DuplicateNavigationItemException"/>.
/// </summary>
/// <remarks>
/// <para>
/// Exists solely to prove that a duplicate navigation registration failing
/// inside a module's own <see cref="InitialiseAsync"/> is isolated by
/// <see cref="ModuleLifecycleManager"/>'s existing, unmodified per-module
/// isolation (ADR-0013) — exactly as ADR-0032 states no new Host-level
/// failure policy is needed for Navigation. Moved here from
/// <c>Tempest.Samples</c> (originally <c>DuplicateNavigationSampleModule</c>,
/// <c>WP 5.0B</c>) by <c>WP 12.3B</c>, ADR-0102: a deliberately-failing
/// module is not a genuine application capability, so it does not belong
/// among <c>Tempest.Samples</c>'s own reference modules, and — the actual
/// defect this move fixes — it was, until now, discovered and permanently
/// left <see cref="ModuleState.Failed"/> by every real run of
/// <c>Tempest.App</c>/<c>Tempest.Desktop</c>, since both reference
/// <c>Tempest.Samples</c>. Implements <see cref="IFaultInjectionModule"/>
/// and lives in a project neither production composition root references,
/// so <see cref="ReflectionFrameworkDiscoveryService"/> excludes it from
/// ordinary startup twice over: the assembly is never loaded, and even if
/// it were, discovery still ignores it unless
/// <see cref="Runtime.ITempestHostBuilder.EnableFaultInjectionModules"/>
/// was called on the host's own builder.
/// </para>
/// <para>
/// Its own <see cref="IModule.Id"/>, <c>tempest.validation.faultinjection.navigation-duplicate</c>,
/// still sorts ordinally after <see cref="NavigationSampleModule"/>'s own
/// <c>tempest.samples.navigation</c> (<c>"s"</c> &lt; <c>"v"</c>) — the
/// module pipeline's ascending-order Initialise batch still always
/// initialises <see cref="NavigationSampleModule"/> (and its successful
/// registration) first, with no ordinal-hack suffix needed now that the Id
/// no longer has to sort within <c>Tempest.Samples</c>'s own Id space.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.validation.faultinjection.navigation-duplicate", "Navigation Duplicate Fault Injection", "1.0.0")]
public sealed class DuplicateNavigationModule : ModuleLifecycleBase, IFaultInjectionModule
{
    private readonly INavigationProvider _navigationProvider;

    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateNavigationModule"/> class.
    /// </summary>
    /// <param name="navigationProvider">
    /// The Navigation Framework this module attempts to register its
    /// (already-taken) item through, resolved via ordinary constructor
    /// injection.
    /// </param>
    public DuplicateNavigationModule(INavigationProvider navigationProvider)
        : base("tempest.validation.faultinjection.navigation-duplicate", "Navigation Duplicate Fault Injection", "1.0.0")
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
