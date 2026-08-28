using Tempest.Core.Identity;

namespace Tempest.Core.Api;

/// <summary>
/// Registers late-bound query and action routes on the REST API's own
/// hosted HTTP surface (<c>ADR-0114</c>) — the API-boundary expression of
/// <c>ADR-0063</c>'s standing rule: views (here, remote clients) read
/// directly through a read-only projection, while every mutation still
/// dispatches through the existing, unmodified Command Framework.
/// </summary>
/// <remarks>
/// <para>
/// <b>Late-bound by design.</b> Unlike <see cref="IApiEndpointRegistry"/>
/// — whose routes are snapshotted into Kestrel's own route table when
/// <see cref="RestApiHostedService"/> starts (Host Phase 8.1) — routes
/// registered here are resolved per request, at request time, through one
/// catch-all mapping. That lets a composition root register routes
/// <em>after</em> the Host has started, which is exactly when the
/// Engineering Workspace's own read models first exist
/// (<c>EngineeringWorkspaceComposer.RegisterEngineeringDisciplines</c>'s
/// own "Host already started" precondition).
/// </para>
/// <para>
/// A query serves <c>GET</c> and returns a complete JSON body; an action
/// serves <c>POST</c>, binds the request body to a typed command, and
/// dispatches it through the Command Framework. The registering layer owns
/// both the projection to JSON and the body-to-command binding — this
/// registry stores and serves delegates, never interpreting either.
/// </para>
/// </remarks>
public interface IApiQueryRegistry
{
    /// <summary>
    /// Maps <c>GET</c> <paramref name="path"/> to <paramref name="query"/>,
    /// requiring <paramref name="requiredPermission"/> before it executes.
    /// May be called at any time, including after the Host has started.
    /// </summary>
    /// <param name="path">The route path (e.g. <c>"/api/v1/companion/cockpit"</c>).</param>
    /// <param name="requiredPermission">The permission a caller must hold before this query executes.</param>
    /// <param name="query">The delegate producing the route's own complete JSON response body.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="requiredPermission"/> or <paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateApiRouteException">A query is already registered for <paramref name="path"/>.</exception>
    void MapQuery(string path, Permission requiredPermission, ApiQueryDelegate query);

    /// <summary>
    /// Maps <c>POST</c> <paramref name="path"/> to <paramref name="action"/>,
    /// requiring <paramref name="requiredPermission"/> before it executes.
    /// May be called at any time, including after the Host has started.
    /// </summary>
    /// <param name="path">The route path (e.g. <c>"/api/v1/companion/actions/set-document-status"</c>).</param>
    /// <param name="requiredPermission">The permission a caller must hold before this action executes.</param>
    /// <param name="action">The delegate binding the request body to a typed command and dispatching it through the Command Framework.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="requiredPermission"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateApiRouteException">An action is already registered for <paramref name="path"/>.</exception>
    void MapAction(string path, Permission requiredPermission, ApiActionDelegate action);

    /// <summary>Every currently registered query/action route. Never <see langword="null"/>.</summary>
    IReadOnlyList<ApiQueryRouteDescriptor> Routes { get; }

    /// <summary>
    /// Finds the route registered for <paramref name="method"/> +
    /// <paramref name="path"/> (ordinal, case-insensitive — the identical
    /// matching rule <see cref="ApiRequestHandler"/> already applies), or
    /// <see langword="null"/> if none is registered.
    /// </summary>
    /// <param name="method">The inbound request's own HTTP method.</param>
    /// <param name="path">The inbound request's own path.</param>
    ApiQueryRouteDescriptor? Find(string method, string path);
}
