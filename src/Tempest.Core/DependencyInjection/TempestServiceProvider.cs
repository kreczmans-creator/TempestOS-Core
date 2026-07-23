using Tempest.Core.Logging;

namespace Tempest.Core.DependencyInjection;

/// <summary>
/// The concrete <see cref="ITempestServiceProvider"/> implementation: TempestOS's own
/// lightweight dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// Built once from the registrations recorded in an <see cref="IServiceCollection"/> at
/// construction time; registering further services on that collection afterwards has no
/// effect on an already-constructed provider.
/// </para>
/// <para>
/// <b>Constructor selection:</b> the implementation type for a service must declare
/// exactly one public constructor. Zero or more than one public constructors both
/// result in a descriptive exception (<see cref="ServiceResolutionException"/> for zero,
/// <see cref="AmbiguousConstructorException"/> for more than one) rather than an
/// arbitrary, non-deterministic choice.
/// </para>
/// <para>
/// <b>Dependency resolution:</b> every constructor parameter type is resolved
/// recursively through this same provider, so a service's dependencies, and their own
/// dependencies, are constructed automatically. A dependency chain that revisits a type
/// already being constructed is a circular dependency and throws
/// <see cref="CircularServiceDependencyException"/>; a dependency with no registration
/// throws <see cref="ServiceNotRegisteredException"/>. Both exceptions carry the full
/// construction chain, not just the immediately failing type.
/// </para>
/// <para>
/// Singleton instances are cached per <see cref="ServiceDescriptor.ServiceType"/> and
/// created at most once, guarded by a single lock; transient services are constructed
/// fresh on every resolution.
/// </para>
/// </remarks>
public sealed class TempestServiceProvider : ITempestServiceProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, ServiceDescriptor> _descriptorsByType;
    private readonly Dictionary<Type, object> _singletonInstances = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="TempestServiceProvider"/> class from
    /// the registrations currently recorded in <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to build this provider from.</param>
    /// <param name="logger">
    /// An optional logger used to record resolutions and construction failures via the
    /// logging abstraction. May be <see langword="null"/> if logging is not required.
    /// </param>
    public TempestServiceProvider(IServiceCollection services, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        _descriptorsByType = services.Descriptors.ToDictionary(descriptor => descriptor.ServiceType);
        _logger = logger;

        // Registrations created via IServiceCollection.AddInstance already carry a
        // fully-constructed instance. Seed the singleton cache with it directly, up
        // front, so GetService returns it via the ordinary singleton-cache lookup
        // below without ever calling Construct — no other change to Resolve/Construct
        // is needed for instance registrations to work correctly.
        foreach (var descriptor in services.Descriptors)
        {
            if (descriptor.ExistingInstance is not null)
                _singletonInstances[descriptor.ServiceType] = descriptor.ExistingInstance;
        }

        _logger?.Information($"Service provider built: {_descriptorsByType.Count} registration(s).");
    }

    /// <inheritdoc />
    public object GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        _logger?.Information($"Resolving service '{serviceType.Name}'.");

        try
        {
            var instance = Resolve(serviceType, []);

            _logger?.Information($"Resolved service '{serviceType.Name}' -> '{instance.GetType().Name}'.");

            return instance;
        }
        catch (Exception ex)
        {
            _logger?.Information($"Failed to resolve service '{serviceType.Name}': {ex.Message}");
            throw;
        }
    }

    private object Resolve(Type serviceType, IReadOnlyList<Type> resolutionChain)
    {
        if (resolutionChain.Contains(serviceType))
            throw new CircularServiceDependencyException(serviceType, resolutionChain);

        if (!_descriptorsByType.TryGetValue(serviceType, out var descriptor))
            throw new ServiceNotRegisteredException(serviceType, resolutionChain);

        var childChain = resolutionChain.Append(serviceType).ToList();

        if (descriptor.Lifetime == ServiceLifetime.Transient)
            return Construct(descriptor.ImplementationType, childChain);

        lock (_gate)
        {
            if (_singletonInstances.TryGetValue(descriptor.ServiceType, out var existing))
                return existing;

            var instance = Construct(descriptor.ImplementationType, childChain);
            _singletonInstances[descriptor.ServiceType] = instance;
            return instance;
        }
    }

    private object Construct(Type implementationType, IReadOnlyList<Type> resolutionChain)
    {
        var constructors = implementationType.GetConstructors();

        if (constructors.Length == 0)
        {
            var requested = ResolutionChainFormatter.RequestedService(resolutionChain, implementationType);
            var chain = ResolutionChainFormatter.Format(resolutionChain, implementationType);

            throw new ServiceResolutionException(
                $"Cannot resolve '{requested.Name}': type '{implementationType.Name}' has no " +
                $"public constructor. Construction chain: {chain}.");
        }

        if (constructors.Length > 1)
            throw new AmbiguousConstructorException(implementationType, constructors.Length, resolutionChain);

        var parameters = constructors[0].GetParameters();
        var arguments = new object[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
            arguments[i] = Resolve(parameters[i].ParameterType, resolutionChain);

        return constructors[0].Invoke(arguments);
    }
}
