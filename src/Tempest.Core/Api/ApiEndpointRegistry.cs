using Tempest.Core.Identity;
using Tempest.Core.Logging;

namespace Tempest.Core.Api;

/// <summary>
/// The concrete <see cref="IApiEndpointRegistry"/> implementation.
/// </summary>
/// <remarks>
/// A single, lock-guarded dictionary keyed by an ordinal, case-insensitive
/// <c>"METHOD path"</c> composite — mirroring the exact-key, first-
/// registration-wins pattern every other registry in this codebase
/// already uses (<see cref="Settings.SettingsProvider"/>,
/// <see cref="Reporting.ReportingService"/>).
/// </remarks>
public sealed class ApiEndpointRegistry : IApiEndpointRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ApiRouteDescriptor> _routesByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ApiEndpointRegistry"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record registration activity via the
    /// logging abstraction. May be <see langword="null"/> if logging is
    /// not required.
    /// </param>
    public ApiEndpointRegistry(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void MapCommand(string method, string path, string commandId, Permission requiredPermission)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method must not be null, empty, or whitespace.", nameof(method));

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be null, empty, or whitespace.", nameof(path));

        if (string.IsNullOrWhiteSpace(commandId))
            throw new ArgumentException("Command Id must not be null, empty, or whitespace.", nameof(commandId));

        var key = RouteKey(method, path);

        lock (_gate)
        {
            if (_routesByKey.ContainsKey(key))
                throw new DuplicateApiRouteException(method, path);

            _routesByKey[key] = new ApiRouteDescriptor(method, path, commandId, requiredPermission);
        }

        _logger?.Information($"Route '{method} {path}' mapped to command '{commandId}'.");
    }

    /// <inheritdoc />
    public IReadOnlyList<ApiRouteDescriptor> Routes
    {
        get
        {
            lock (_gate)
                return _routesByKey.Values.ToList();
        }
    }

    private static string RouteKey(string method, string path) => $"{method} {path}";
}
