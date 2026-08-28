using System.Text.Json;
using Tempest.Core.Audit;
using Tempest.Core.Identity;
using Tempest.Core.Logging;

namespace Tempest.Core.Api;

/// <summary>
/// The REST API's late-bound query-and-action request pipeline
/// (<c>ADR-0114</c>) — route lookup at request time against
/// <see cref="IApiQueryRegistry"/>, then the identical identity
/// resolution, permission enforcement, audit, and failure-mapping
/// sequence <see cref="ApiRequestHandler"/> already established
/// (<c>ADR-0052</c>: pure <see cref="IIdentityService.GetPrincipal"/>
/// only, never the ambient current principal). Deliberately independent
/// of Kestrel/ASP.NET Core itself, so it can be exercised directly in
/// tests without a real HTTP listener — exactly as
/// <see cref="ApiRequestHandler"/> already is.
/// </summary>
/// <remarks>
/// A query outcome is a <c>200</c> with an <c>application/json</c> body;
/// an action outcome maps the dispatched command's own
/// <see cref="Commands.CommandResult"/> to <c>200</c>/<c>400</c> exactly
/// as command routes already do. A binding fault
/// (<see cref="ApiRequestBindingException"/> or
/// <see cref="JsonException"/>) maps to <c>400</c>; any other exception
/// maps to <c>500</c> with the detail logged, never leaked.
/// </remarks>
public sealed class ApiQueryRequestHandler
{
    private readonly IApiQueryRegistry _queryRegistry;
    private readonly IIdentityService _identityService;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IAuditRecorder _auditRecorder;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ApiQueryRequestHandler"/> class.
    /// </summary>
    public ApiQueryRequestHandler(
        IApiQueryRegistry queryRegistry,
        IIdentityService identityService,
        IPermissionEvaluator permissionEvaluator,
        IAuditRecorder auditRecorder,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(queryRegistry);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(auditRecorder);

        _queryRegistry = queryRegistry;
        _identityService = identityService;
        _permissionEvaluator = permissionEvaluator;
        _auditRecorder = auditRecorder;
        _logger = logger;
    }

    /// <summary>
    /// Handles one late-bound REST request: looks the route up in
    /// <see cref="IApiQueryRegistry"/> at request time, resolves and
    /// authorizes the caller, executes the query or dispatches the
    /// action, and maps the outcome to an HTTP status code — never
    /// leaking internal exception detail into the response body.
    /// </summary>
    /// <param name="method">The inbound request's own HTTP method.</param>
    /// <param name="path">The inbound request's own path.</param>
    /// <param name="identityHeaderValue">
    /// The value of <see cref="ApiRequestHandler.IdentityHeaderName"/>, or
    /// <see langword="null"/> if the caller supplied none.
    /// </param>
    /// <param name="requestBody">The inbound request's own body, or <see langword="null"/> if none was supplied — consumed only by action routes.</param>
    /// <param name="cancellationToken">A token observed while the query or the command's own handler runs.</param>
    /// <returns>
    /// The response, plus whether its body is JSON — <see langword="true"/>
    /// for a successful query response, <see langword="false"/> for every
    /// plain-text outcome (errors, and action results).
    /// </returns>
    public async Task<(ApiResponse Response, bool IsJson)> HandleAsync(
        string method, string path, string? identityHeaderValue, string? requestBody, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);

        var route = _queryRegistry.Find(method, path);

        if (route is null)
        {
            Log(method, path, 404, null);
            return (new ApiResponse(404, "Not Found"), IsJson: false);
        }

        if (string.IsNullOrWhiteSpace(identityHeaderValue))
        {
            Log(method, path, 401, null);
            return (new ApiResponse(401, "Unauthorized: no identity supplied."), IsJson: false);
        }

        var principal = _identityService.GetPrincipal(identityHeaderValue);

        if (!_permissionEvaluator.HasPermission(principal, route.RequiredPermission))
        {
            Log(method, path, 403, identityHeaderValue);
            return (new ApiResponse(403, $"Forbidden: principal '{identityHeaderValue}' does not hold '{route.RequiredPermission.Key}'."), IsJson: false);
        }

        await _auditRecorder.RecordAsync(
            ApiRequestHandler.RequestAuditAction,
            new Dictionary<string, string>
            {
                ["Method"] = method,
                ["Path"] = path,
                [ApiRequestHandler.CallerIdentityDetailKey] = identityHeaderValue,
            },
            cancellationToken).ConfigureAwait(false);

        try
        {
            if (route.Query is { } query)
            {
                var json = await query(cancellationToken).ConfigureAwait(false);
                Log(method, path, 200, identityHeaderValue);
                return (new ApiResponse(200, json), IsJson: true);
            }

            var result = await route.Action!(requestBody, cancellationToken).ConfigureAwait(false);
            var statusCode = result.Succeeded ? 200 : 400;
            Log(method, path, statusCode, identityHeaderValue);
            return (new ApiResponse(statusCode, result.Message), IsJson: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ApiRequestBindingException or JsonException)
        {
            Log(method, path, 400, identityHeaderValue);
            return (new ApiResponse(400, $"Bad Request: {ex.Message}"), IsJson: false);
        }
        catch (Exception ex)
        {
            _logger?.Error($"Unhandled exception executing '{method} {path}'.", ex);
            Log(method, path, 500, identityHeaderValue);
            return (new ApiResponse(500, "Internal Server Error"), IsJson: false);
        }
    }

    private void Log(string method, string path, int statusCode, string? principalId) =>
        _logger?.Information($"{method} {path} -> {statusCode}" + (principalId is null ? string.Empty : $" (principal '{principalId}')"));
}
