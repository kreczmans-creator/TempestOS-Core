namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Thrown when the implementation type being constructed declares more than one
/// public constructor, so the container has no deterministic way to choose one.
/// </summary>
public sealed class AmbiguousConstructorException : ServiceResolutionException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="AmbiguousConstructorException"/> class.
    /// </summary>
    /// <param name="implementationType">The type with more than one public constructor.</param>
    /// <param name="publicConstructorCount">The number of public constructors found.</param>
    /// <param name="resolutionChain">
    /// The chain of types already being constructed, starting with the originally
    /// requested top-level service.
    /// </param>
    public AmbiguousConstructorException(Type implementationType, int publicConstructorCount, IReadOnlyList<Type> resolutionChain)
        : base(BuildMessage(implementationType, publicConstructorCount, resolutionChain))
    {
        ImplementationType = implementationType;
        PublicConstructorCount = publicConstructorCount;
        RequestedService = ResolutionChainFormatter.RequestedService(resolutionChain, implementationType);
        ResolutionChain = resolutionChain;
    }

    /// <summary>
    /// Gets the type with more than one public constructor.
    /// </summary>
    public Type ImplementationType { get; }

    /// <summary>
    /// Gets the number of public constructors found on <see cref="ImplementationType"/>.
    /// </summary>
    public int PublicConstructorCount { get; }

    /// <summary>
    /// Gets the originally requested top-level service type.
    /// </summary>
    public Type RequestedService { get; }

    /// <summary>
    /// Gets the chain of types already being constructed when the ambiguity was found.
    /// </summary>
    public IReadOnlyList<Type> ResolutionChain { get; }

    private static string BuildMessage(Type implementationType, int publicConstructorCount, IReadOnlyList<Type> resolutionChain)
    {
        var requested = ResolutionChainFormatter.RequestedService(resolutionChain, implementationType);
        var chain = ResolutionChainFormatter.Format(resolutionChain, implementationType);

        return $"Cannot resolve '{requested.Name}': type '{implementationType.Name}' has " +
               $"{publicConstructorCount} public constructors; exactly one is required for " +
               $"constructor injection. Construction chain: {chain}.";
    }
}
