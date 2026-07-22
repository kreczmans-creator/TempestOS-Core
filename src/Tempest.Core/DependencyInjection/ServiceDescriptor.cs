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
    /// <param name="existingInstance">
    /// An already-constructed instance to hand out instead of constructing one, or
    /// <see langword="null"/> for a normal, reflection-constructed registration. See
    /// <see cref="IServiceCollection.AddInstance"/>.
    /// </param>
    public ServiceDescriptor(
        Type serviceType,
        Type implementationType,
        ServiceLifetime lifetime,
        object? existingInstance = null)
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
        ExistingInstance = existingInstance;
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

    /// <summary>
    /// Gets the already-constructed instance to hand out for this registration, if
    /// this descriptor was created via <see cref="IServiceCollection.AddInstance"/>;
    /// otherwise, <see langword="null"/>.
    /// </summary>
    public object? ExistingInstance { get; }
}
