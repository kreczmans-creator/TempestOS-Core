namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Thrown when resolving a service requires a type that has no registration.
/// </summary>
public sealed class ServiceNotRegisteredException : ServiceResolutionException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ServiceNotRegisteredException"/> class.
    /// </summary>
    /// <param name="missingServiceType">The type that has no registration.</param>
    /// <param name="resolutionChain">
    /// The chain of types already being constructed when the missing dependency was
    /// requested, starting with the originally requested top-level service.
    /// </param>
    public ServiceNotRegisteredException(Type missingServiceType, IReadOnlyList<Type> resolutionChain)
        : base(BuildMessage(missingServiceType, resolutionChain))
    {
        MissingServiceType = missingServiceType;
        RequestedService = ResolutionChainFormatter.RequestedService(resolutionChain, missingServiceType);
        ResolutionChain = resolutionChain;
    }

    /// <summary>
    /// Gets the type that has no registration.
    /// </summary>
    public Type MissingServiceType { get; }

    /// <summary>
    /// Gets the originally requested top-level service type.
    /// </summary>
    public Type RequestedService { get; }

    /// <summary>
    /// Gets the chain of types already being constructed when the missing dependency
    /// was requested (not including <see cref="MissingServiceType"/> itself).
    /// </summary>
    public IReadOnlyList<Type> ResolutionChain { get; }

    private static string BuildMessage(Type missingServiceType, IReadOnlyList<Type> resolutionChain)
    {
        var requested = ResolutionChainFormatter.RequestedService(resolutionChain, missingServiceType);
        var chain = ResolutionChainFormatter.Format(resolutionChain, missingServiceType);

        return $"Cannot resolve '{requested.Name}': no service is registered for " +
               $"'{missingServiceType.Name}'. Construction chain: {chain}.";
    }
}
