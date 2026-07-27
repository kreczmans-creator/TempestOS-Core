using Tempest.Core.DependencyInjection;

namespace Tempest.Core.BackgroundServices;

/// <summary>
/// Bridges discovered hosted services into the dependency injection container.
/// </summary>
public static class HostedServiceCollectionExtensions
{
    /// <summary>
    /// Registers every discovered hosted service's concrete type with the container,
    /// keyed by its own concrete type, as a singleton.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="hostedServiceTypes">
    /// The discovered hosted service types to register, typically the result of
    /// <see cref="IHostedServiceDiscoveryService.DiscoverHostedServiceTypes()"/>.
    /// </param>
    /// <returns><paramref name="services"/>, to allow chaining.</returns>
    /// <remarks>
    /// Mirrors <c>ModuleServiceCollectionExtensions.AddDiscoveredModules</c> exactly — an
    /// ordinary, self-referential singleton registration requiring no new dependency
    /// injection capability (ADR-0029). Singleton, so <see cref="HostedServiceManager"/>
    /// resolving the same type once (for its later stop call) receives the same instance
    /// it started.
    /// </remarks>
    public static IServiceCollection AddDiscoveredHostedServices(
        this IServiceCollection services,
        IEnumerable<Type> hostedServiceTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(hostedServiceTypes);

        foreach (var type in hostedServiceTypes)
            services.Singleton(type, type);

        return services;
    }
}
