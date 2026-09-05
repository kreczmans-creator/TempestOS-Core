namespace Tempest.Core.DependencyInjection;

/// <summary>
/// The base exception thrown when registering a service with an
/// <see cref="IServiceCollection"/> fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Api.ApiException"/>'s and
/// <see cref="Reporting.ReportingException"/>'s own base-plus-subtype
/// pattern (never thrown directly itself), and is deliberately a separate
/// root category from <see cref="ServiceResolutionException"/>: that type
/// covers failures while an already-built <see cref="ITempestServiceProvider"/>
/// resolves a service; this one covers failures while an
/// <see cref="IServiceCollection"/> is still being built up, before any
/// provider exists — the same registration/resolution split
/// <see cref="Modules.ModuleRegistrationException"/> and
/// <c>ModuleDiscoveryException</c> already draw for modules (TD-69).
/// </remarks>
public class ServiceRegistrationException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ServiceRegistrationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the registration failure.</param>
    public ServiceRegistrationException(string message)
        : base(message)
    {
    }
}
