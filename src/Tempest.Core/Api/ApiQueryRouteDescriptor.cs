using Tempest.Core.Identity;

namespace Tempest.Core.Api;

/// <summary>
/// The delegate a read-only query route serves — produces the route's own
/// complete JSON response body. Executed per request, at request time, so
/// the data it reads is always the platform's current state, never a
/// snapshot captured at registration.
/// </summary>
/// <param name="cancellationToken">A token observed while the query's own reads run.</param>
public delegate Task<string> ApiQueryDelegate(CancellationToken cancellationToken);

/// <summary>
/// The delegate an action route executes — binds the inbound request body
/// to a typed <see cref="Commands.ICommand"/> and dispatches it through
/// the existing Command Framework (<c>ADR-0048</c>'s own anticipated
/// body-binding evolution, <c>ADR-0114</c>), returning the dispatched
/// command's own <see cref="Commands.CommandResult"/> unchanged.
/// </summary>
/// <param name="requestBody">The inbound request's own body, or <see langword="null"/>/empty if none was supplied.</param>
/// <param name="cancellationToken">A token observed while the command's own handler runs.</param>
public delegate Task<Commands.CommandResult> ApiActionDelegate(string? requestBody, CancellationToken cancellationToken);

/// <summary>
/// One registered late-bound route on the REST API's query-and-action
/// surface (<c>ADR-0114</c>) — either a read-only JSON query
/// (<see cref="Query"/> non-null, served on <c>GET</c>) or a
/// body-binding action (<see cref="Action"/> non-null, served on
/// <c>POST</c>), never both.
/// </summary>
public sealed record ApiQueryRouteDescriptor
{
    internal ApiQueryRouteDescriptor(string method, string path, Permission requiredPermission, ApiQueryDelegate? query, ApiActionDelegate? action)
    {
        Method = method;
        Path = path;
        RequiredPermission = requiredPermission;
        Query = query;
        Action = action;
    }

    /// <summary>Gets the HTTP method this route serves — <c>"GET"</c> for a query, <c>"POST"</c> for an action, fixed by construction.</summary>
    public string Method { get; }

    /// <summary>Gets the route path (e.g. <c>"/api/v1/companion/cockpit"</c>).</summary>
    public string Path { get; }

    /// <summary>Gets the permission a caller must hold before this route executes.</summary>
    public Permission RequiredPermission { get; }

    /// <summary>Gets the query delegate, or <see langword="null"/> if this route is an action.</summary>
    public ApiQueryDelegate? Query { get; }

    /// <summary>Gets the action delegate, or <see langword="null"/> if this route is a query.</summary>
    public ApiActionDelegate? Action { get; }
}
