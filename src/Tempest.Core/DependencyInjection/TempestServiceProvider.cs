using System.Reflection;
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
/// throws <see cref="ServiceNotRegisteredException"/> — <b>unless</b> the constructor
/// parameter itself declares a default value (<see cref="System.Reflection.ParameterInfo.HasDefaultValue"/>),
/// in which case that declared default is passed instead (TD-69), exactly as platform
/// types such as <c>EventBus(ILogger? logger = null, ...)</c> already expect. Both
/// exceptions, when thrown, carry the full construction chain, not just the immediately
/// failing type.
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

        // `WP 17.2A` (ADR-0146): every resolve, successful or not, is
        // Debug-level chatter, not Information — an Information log reads
        // as lifecycle phases and user-visible actions, and a resolution
        // happens far too often, for far too routine a reason, to qualify.
        // The one-time "Service provider built" message above stays
        // Information.
        _logger?.Debug($"Resolving service '{serviceType.Name}'.");

        try
        {
            var instance = Resolve(serviceType, []);

            _logger?.Debug($"Resolved service '{serviceType.Name}' -> '{instance.GetType().Name}'.");

            return instance;
        }
        catch (Exception ex)
        {
            _logger?.Debug($"Failed to resolve service '{serviceType.Name}': {ex.Message}");
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
        var arguments = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;

            // TD-69: platform types declare optional constructor parameters
            // precisely so the container can still build them when the
            // parameter's own type has no registration at all (e.g.
            // EventBus(ILogger? logger = null, ...)). Only that specific
            // case - no descriptor for the parameter's exact type - falls
            // back to the declared default; a registered type that fails to
            // resolve for some other reason (a circular dependency, a
            // deeper missing dependency of its own) still propagates
            // through the ordinary Resolve call below exactly as before,
            // rather than being silently masked by the fallback.
            if (!_descriptorsByType.ContainsKey(parameterType) && parameters[i].HasDefaultValue)
            {
                // `WP 17.0A`: the fallback is allowed only where the author
                // said absence is a legitimate state — a nullable reference
                // (`ILogger? logger = null`) or a value type with a default.
                // A NON-nullable reference parameter that merely carries a
                // default is not that: silently passing its default here is
                // how `TD-64` disarmed a permission gate, so it is refused
                // with the ordinary not-registered error instead. And a
                // fallback that IS taken is logged at Warning, because a
                // registration slip that lands here is otherwise invisible.
                if (!IsAbsenceLegitimate(parameters[i]))
                    arguments[i] = Resolve(parameterType, resolutionChain);
                else
                {
                    _logger?.Warning(
                        $"Constructing '{implementationType.Name}': parameter '{parameters[i].Name}' of type " +
                        $"'{parameterType.Name}' has no registration; using its declared default. If that type " +
                        "was meant to be registered, this is a registration slip, not an optional dependency.");
                    arguments[i] = parameters[i].DefaultValue;
                }
            }
            else
                arguments[i] = Resolve(parameterType, resolutionChain);
        }

        return constructors[0].Invoke(arguments);
    }

    /// <summary>
    /// Whether a constructor parameter's own declaration says it may be
    /// absent: a value type (whose default is a real value), a
    /// <see cref="Nullable{T}"/>, or a reference type annotated nullable.
    /// A non-nullable reference type with a default value is NOT counted,
    /// however the default is spelled — the annotation is the author's
    /// statement, and this container honours it.
    /// </summary>
    private static bool IsAbsenceLegitimate(ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsValueType)
            return true;

        var nullability = new NullabilityInfoContext().Create(parameter);
        return nullability.WriteState == NullabilityState.Nullable;
    }
}
