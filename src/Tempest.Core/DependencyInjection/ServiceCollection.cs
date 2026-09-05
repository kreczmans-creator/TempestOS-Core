using Tempest.Core.Logging;

namespace Tempest.Core.DependencyInjection;

/// <summary>
/// The concrete <see cref="IServiceCollection"/> implementation.
/// </summary>
public sealed class ServiceCollection : IServiceCollection
{
    private readonly Dictionary<Type, ServiceDescriptor> _descriptorsByType = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ServiceCollection"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record registrations via the logging
    /// abstraction. May be <see langword="null"/> if logging is not required.
    /// </param>
    public ServiceCollection(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<ServiceDescriptor> Descriptors => _descriptorsByType.Values.ToList();

    /// <inheritdoc />
    public IServiceCollection Add(Type serviceType, Type implementationType, ServiceLifetime lifetime, bool allowReplace = false)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        if (!serviceType.IsAssignableFrom(implementationType))
        {
            throw new ArgumentException(
                $"'{implementationType.Name}' is not assignable to '{serviceType.Name}'.",
                nameof(implementationType));
        }

        if (!allowReplace && _descriptorsByType.ContainsKey(serviceType))
            throw new DuplicateServiceRegistrationException(serviceType);

        _descriptorsByType[serviceType] = new ServiceDescriptor(serviceType, implementationType, lifetime);

        _logger?.Information(
            $"Service registered: '{serviceType.Name}' -> '{implementationType.Name}' ({lifetime}).");

        return this;
    }

    /// <inheritdoc />
    public IServiceCollection AddInstance(Type serviceType, object instance, bool allowReplace = false)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(instance);

        if (!serviceType.IsInstanceOfType(instance))
        {
            throw new ArgumentException(
                $"'{instance.GetType().Name}' is not assignable to '{serviceType.Name}'.",
                nameof(instance));
        }

        if (!allowReplace && _descriptorsByType.ContainsKey(serviceType))
            throw new DuplicateServiceRegistrationException(serviceType);

        _descriptorsByType[serviceType] =
            new ServiceDescriptor(serviceType, instance.GetType(), ServiceLifetime.Singleton, instance);

        _logger?.Information(
            $"Service instance registered: '{serviceType.Name}' -> existing instance of '{instance.GetType().Name}'.");

        return this;
    }
}
