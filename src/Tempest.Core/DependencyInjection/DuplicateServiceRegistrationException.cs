namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Thrown when <see cref="IServiceCollection.Add"/> or
/// <see cref="IServiceCollection.AddInstance(Type, object)"/> is called for
/// a service type that already has a registration.
/// </summary>
/// <remarks>
/// First registration wins; a colliding, later registration is rejected —
/// never a silent override, mirroring
/// <see cref="Api.DuplicateApiRouteException"/>'s and
/// <see cref="Reporting.DuplicateReportDefinitionException"/>'s own
/// convention (TD-69: a mistaken re-registration — e.g. of
/// <c>IEventBus</c> — used to silently swap the platform implementation
/// with no exception and no log). The rare, genuinely deliberate case that
/// needs to replace an existing registration passes
/// <c>allowReplace: true</c> to <see cref="IServiceCollection.Add"/> or
/// <see cref="IServiceCollection.AddInstance(Type, object)"/> instead.
/// </remarks>
public sealed class DuplicateServiceRegistrationException : ServiceRegistrationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateServiceRegistrationException"/> class.
    /// </summary>
    /// <param name="serviceType">The service type that already has a registration.</param>
    public DuplicateServiceRegistrationException(Type serviceType)
        : base(
            $"A service is already registered for '{serviceType.Name}'. " +
            "Pass allowReplace: true if replacing it is deliberate.")
    {
        ServiceType = serviceType;
    }

    /// <summary>
    /// Gets the service type that already has a registration.
    /// </summary>
    public Type ServiceType { get; }
}
