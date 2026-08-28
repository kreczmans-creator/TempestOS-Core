using Tempest.Core.Identity;
using Tempest.Core.Logging;

namespace Tempest.Core.Api;

/// <summary>
/// The concrete <see cref="IApiQueryRegistry"/> implementation — a single,
/// lock-guarded dictionary keyed by an ordinal, case-insensitive
/// <c>"METHOD path"</c> composite, mirroring
/// <see cref="ApiEndpointRegistry"/>'s own exact-key,
/// first-registration-wins shape. Unlike that registry, registration here
/// is expected from any Host phase — including after startup — so the
/// lock is load-bearing, not merely defensive.
/// </summary>
public sealed class ApiQueryRegistry : IApiQueryRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ApiQueryRouteDescriptor> _routesByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ApiQueryRegistry"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record registration activity via the
    /// logging abstraction. May be <see langword="null"/> if logging is
    /// not required.
    /// </param>
    public ApiQueryRegistry(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void MapQuery(string path, Permission requiredPermission, ApiQueryDelegate query)
    {
        ArgumentNullException.ThrowIfNull(requiredPermission);
        ArgumentNullException.ThrowIfNull(query);

        Register(new ApiQueryRouteDescriptor("GET", ValidatedPath(path), requiredPermission, query, action: null));
    }

    /// <inheritdoc />
    public void MapAction(string path, Permission requiredPermission, ApiActionDelegate action)
    {
        ArgumentNullException.ThrowIfNull(requiredPermission);
        ArgumentNullException.ThrowIfNull(action);

        Register(new ApiQueryRouteDescriptor("POST", ValidatedPath(path), requiredPermission, query: null, action));
    }

    /// <inheritdoc />
    public IReadOnlyList<ApiQueryRouteDescriptor> Routes
    {
        get
        {
            lock (_gate)
                return _routesByKey.Values.ToList();
        }
    }

    /// <inheritdoc />
    public ApiQueryRouteDescriptor? Find(string method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);

        lock (_gate)
            return _routesByKey.TryGetValue(RouteKey(method, path), out var descriptor) ? descriptor : null;
    }

    private void Register(ApiQueryRouteDescriptor descriptor)
    {
        var key = RouteKey(descriptor.Method, descriptor.Path);

        lock (_gate)
        {
            if (_routesByKey.ContainsKey(key))
                throw new DuplicateApiRouteException(descriptor.Method, descriptor.Path);

            _routesByKey[key] = descriptor;
        }

        _logger?.Information($"{(descriptor.Query is null ? "Action" : "Query")} route '{descriptor.Method} {descriptor.Path}' registered.");
    }

    private static string ValidatedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be null, empty, or whitespace.", nameof(path));

        return path;
    }

    private static string RouteKey(string method, string path) => $"{method} {path}";
}
