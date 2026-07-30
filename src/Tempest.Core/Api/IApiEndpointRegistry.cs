using Tempest.Core.Identity;

namespace Tempest.Core.Api;

/// <summary>
/// Registers a route so it becomes reachable over the REST API's hosted
/// HTTP surface. Route handling itself dispatches through the existing,
/// unmodified <see cref="Commands.ICommandRegistry.InvokeAsync"/> — this
/// interface only describes route-to-command mapping, never a second,
/// competing invocation mechanism.
/// </summary>
public interface IApiEndpointRegistry
{
    /// <summary>
    /// Maps <paramref name="method"/> + <paramref name="path"/> to
    /// <paramref name="commandId"/>, requiring <paramref name="requiredPermission"/>
    /// before dispatch. Expected to be called only during Module
    /// Initialisation (single-threaded by construction, per `Host
    /// Lifecycle.md`) — not itself required to be thread-safe against
    /// concurrent registration.
    /// </summary>
    /// <param name="method">The HTTP method (e.g. <c>"GET"</c>, <c>"POST"</c>).</param>
    /// <param name="path">The route path (e.g. <c>"/api/v1/sample-report"</c>).</param>
    /// <param name="commandId">The registered <see cref="Commands.CommandDescriptor.Id"/> this route invokes.</param>
    /// <param name="requiredPermission">The permission a caller must hold before this route dispatches.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="method"/>, <paramref name="path"/>, or
    /// <paramref name="commandId"/> is <see langword="null"/>, empty, or
    /// whitespace.
    /// </exception>
    /// <exception cref="DuplicateApiRouteException">
    /// A route is already registered for <paramref name="method"/> + <paramref name="path"/>.
    /// </exception>
    void MapCommand(string method, string path, string commandId, Permission requiredPermission);

    /// <summary>Every currently registered route. Never <see langword="null"/>.</summary>
    IReadOnlyList<ApiRouteDescriptor> Routes { get; }
}
