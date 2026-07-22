using Tempest.Core.DependencyInjection;

namespace Tempest.Core.Modules;

/// <summary>
/// Bridges discovered modules into the dependency injection container.
/// </summary>
public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// Registers every discovered module's concrete type with the container, keyed by
    /// its own concrete type, as a singleton.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="descriptors">The discovered modules to register, typically the
    /// result of <c>IFrameworkDiscoveryService.DiscoverModules()</c>.</param>
    /// <returns><paramref name="services"/>, to allow chaining.</returns>
    /// <remarks>
    /// Modules are registered as <see cref="ServiceLifetime.Singleton"/> so that
    /// <see cref="ModuleLifecycleManager"/> resolving the same module type more than
    /// once (across Initialise/Start/Stop/Dispose) receives the same instance. This is
    /// a registration convention, not a correctness requirement enforced by
    /// <see cref="ModuleLifecycleManager"/> itself — it caches the instance it resolves
    /// during initialisation and reuses that reference directly, so a module
    /// mistakenly registered as transient would still behave correctly for lifecycle
    /// purposes, just wastefully, since the container's own transient instance would
    /// never be reused after that first resolution.
    /// </remarks>
    public static IServiceCollection AddDiscoveredModules(
        this IServiceCollection services,
        IEnumerable<ModuleDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptors);

        foreach (var descriptor in descriptors)
            services.Singleton(descriptor.ModuleType, descriptor.ModuleType);

        return services;
    }
}
