namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Convenience registration methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a non-generic singleton service where the service type and the
    /// implementation type are the same.
    /// </summary>
    public static IServiceCollection Singleton(this IServiceCollection services, Type serviceType, Type implementationType) =>
        services.Add(serviceType, implementationType, ServiceLifetime.Singleton);

    /// <summary>
    /// Registers <typeparamref name="TService"/> as a singleton, constructing
    /// <typeparamref name="TService"/> itself.
    /// </summary>
    public static IServiceCollection Singleton<TService>(this IServiceCollection services)
        where TService : class =>
        services.Singleton(typeof(TService), typeof(TService));

    /// <summary>
    /// Registers <typeparamref name="TImplementation"/> as a singleton to satisfy
    /// requests for <typeparamref name="TService"/>.
    /// </summary>
    public static IServiceCollection Singleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService =>
        services.Singleton(typeof(TService), typeof(TImplementation));

    /// <summary>
    /// Registers a non-generic transient service where the service type and the
    /// implementation type are the same.
    /// </summary>
    public static IServiceCollection Transient(this IServiceCollection services, Type serviceType, Type implementationType) =>
        services.Add(serviceType, implementationType, ServiceLifetime.Transient);

    /// <summary>
    /// Registers <typeparamref name="TService"/> as transient, constructing
    /// <typeparamref name="TService"/> itself.
    /// </summary>
    public static IServiceCollection Transient<TService>(this IServiceCollection services)
        where TService : class =>
        services.Transient(typeof(TService), typeof(TService));

    /// <summary>
    /// Registers <typeparamref name="TImplementation"/> as transient to satisfy
    /// requests for <typeparamref name="TService"/>.
    /// </summary>
    public static IServiceCollection Transient<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService =>
        services.Transient(typeof(TService), typeof(TImplementation));

    /// <summary>
    /// Registers an already-constructed <typeparamref name="TService"/> instance.
    /// </summary>
    public static IServiceCollection AddInstance<TService>(this IServiceCollection services, TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        return services.AddInstance(typeof(TService), instance);
    }
}
