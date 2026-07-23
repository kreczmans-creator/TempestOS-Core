namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Thrown when resolving a service would require constructing a type that is already
/// in the process of being constructed further up the same resolution chain.
/// </summary>
public sealed class CircularServiceDependencyException : ServiceResolutionException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CircularServiceDependencyException"/> class.
    /// </summary>
    /// <param name="serviceType">The type that would need to be constructed a second time.</param>
    /// <param name="resolutionChain">
    /// The chain of types already being constructed, starting with the originally
    /// requested top-level service, up to (but not including) <paramref name="serviceType"/>'s
    /// repeated occurrence.
    /// </param>
    public CircularServiceDependencyException(Type serviceType, IReadOnlyList<Type> resolutionChain)
        : base(BuildMessage(serviceType, resolutionChain))
    {
        ServiceType = serviceType;
        RequestedService = ResolutionChainFormatter.RequestedService(resolutionChain, serviceType);
        ResolutionChain = resolutionChain;
    }

    /// <summary>
    /// Gets the type that would need to be constructed a second time.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// Gets the originally requested top-level service type.
    /// </summary>
    public Type RequestedService { get; }

    /// <summary>
    /// Gets the chain of types already being constructed when the cycle was detected.
    /// </summary>
    public IReadOnlyList<Type> ResolutionChain { get; }

    private static string BuildMessage(Type serviceType, IReadOnlyList<Type> resolutionChain)
    {
        var requested = ResolutionChainFormatter.RequestedService(resolutionChain, serviceType);
        var chain = ResolutionChainFormatter.Format(resolutionChain, serviceType);

        return $"Circular dependency detected while resolving '{requested.Name}'. " +
               $"Construction chain: {chain}.";
    }
}
