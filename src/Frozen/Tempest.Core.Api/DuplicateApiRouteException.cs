namespace Tempest.Core.Api;

/// <summary>
/// Thrown when <see cref="IApiEndpointRegistry.MapCommand"/> is called for
/// a method + path combination that is already registered.
/// </summary>
/// <remarks>
/// First registration wins; a colliding, later registration is rejected —
/// never a silent override, mirroring
/// <see cref="Reporting.DuplicateReportDefinitionException"/>'s own
/// convention.
/// </remarks>
public sealed class DuplicateApiRouteException : ApiException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateApiRouteException"/> class.
    /// </summary>
    /// <param name="method">The HTTP method that already has a registered route.</param>
    /// <param name="path">The path that already has a registered route.</param>
    public DuplicateApiRouteException(string method, string path)
        : base($"A route is already registered for '{method} {path}'.")
    {
        Method = method;
        Path = path;
    }

    /// <summary>Gets the HTTP method that already has a registered route.</summary>
    public string Method { get; }

    /// <summary>Gets the path that already has a registered route.</summary>
    public string Path { get; }
}
