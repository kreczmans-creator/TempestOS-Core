namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Describes a single service registration: the requested type, the concrete type
/// to construct for it, and how long the constructed instance is kept alive.
/// </summary>
public sealed class ServiceDescriptor
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ServiceDescriptor"/> class.
    /// </summary>
    /// <param name="serviceType">The type consumers ask the container to resolve.</param>
    /// <param name="implementationType">The concrete type to construct for <paramref name="serviceType"/>.</param>
    /// <param name="lifetime">How long the constructed instance is kept alive.</param>
    public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
    }

    /// <summary>
    /// Gets the type consumers ask the container to resolve.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// Gets the concrete type constructed to satisfy <see cref="ServiceType"/>.
    /// </summary>
    public Type ImplementationType { get; }

    /// <summary>
    /// Gets how long the constructed instance is kept alive.
    /// </summary>
    public ServiceLifetime Lifetime { get; }
}
