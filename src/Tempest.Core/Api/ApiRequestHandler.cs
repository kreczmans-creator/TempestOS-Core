using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Logging;

namespace Tempest.Core.Api;

/// <summary>
/// The REST API's own thin request-handling pipeline — route lookup,
/// identity resolution, permission enforcement, and dispatch through the
/// existing, unmodified <see cref="ICommandRegistry.InvokeAsync"/>.
/// Deliberately independent of Kestrel/ASP.NET Core itself, so it can be
/// exercised directly in tests without a real HTTP listener — the actual
/// HTTP transport (<see cref="RestApiHostedService"/>) is a thin
/// translation layer over this type, never the other way around.
/// </summary>
/// <remarks>
/// <para>
/// <b>Identity resolution carries no real authentication.</b> This
/// release's identity model is local-only (<c>ADR-0043</c>): "a caller
/// supplies an identity id it already trusts... there is no
/// authentication (verifying a password, token, or credential)." This
/// handler extends that exact same trust model over HTTP, via the
/// <see cref="IdentityHeaderName"/> request header — it does not verify
/// the header's own value against any credential. This is a disclosed,
/// deliberate limitation (see <c>ADR-0052</c>), not an oversight; when
/// enabled (see <see cref="RestApiHostedService"/> — the listener is
/// disabled by default, <c>D-024</c>, Proposed) this platform binds only
/// to the loopback address, limiting real-world exposure until a genuine
/// authentication mechanism is designed.
/// </para>
/// <para>
/// <b>Never establishes the shared, ambient current principal.</b>
/// Resolving a principal via <see cref="IIdentityService.GetPrincipal"/>
/// is a pure, non-mutating lookup, safe for concurrent requests. Calling
/// <see cref="IIdentityService.EstablishCurrentPrincipal"/> instead — or
/// migrating <see cref="ICurrentPrincipalAccessor"/> to an
/// <see cref="AsyncLocal{T}"/>-backed implementation — was considered
/// and rejected: the latter was verified directly (not merely reasoned
/// about) to regress 17 pre-existing tests that depend on a principal
/// established during Module Initialisation remaining visible to a
/// later, separate call chain. See <c>ADR-0052</c> for the complete
/// account. Consequently, a per-request caller identity is carried
/// explicitly in this handler's own Audit <c>Detail</c> entry
/// (<see cref="CallerIdentityDetailKey"/>), never via ambient-principal
/// auto-attribution.
/// </para>
/// </remarks>
public sealed class ApiRequestHandler
{
    /// <summary>The request header a caller supplies its claimed identity id through.</summary>
    public const string IdentityHeaderName = "X-Identity-Id";

    /// <summary>The action recorded through <see cref="IAuditRecorder"/> for every authorized request.</summary>
    public const string RequestAuditAction = "api.request";

    /// <summary>
    /// The <see cref="IAuditRecord.Detail"/> key the resolved caller
    /// identity id is carried under — see this type's own remarks for
    /// why this is not ambient-principal auto-attribution.
    /// </summary>
    public const string CallerIdentityDetailKey = "CallerIdentityId";

    /// <summary>
    /// The permission a caller must hold to read the generated OpenAPI
    /// document (<see cref="RestApiHostedService.OpenApiPath"/>) via
    /// <see cref="HandleOpenApiDocumentAsync"/> — closing <c>TD-62</c>:
    /// the document previously disclosed the entire route →
    /// required-permission map to any caller with no identity and no
    /// permission check at all. No existing permission (all either
    /// sample-module-scoped, e.g. <c>reporting.generate</c>, or
    /// unrelated) fit this document's own Core-level concern, so this is
    /// a new, dedicated key — following
    /// <see cref="Audit.AuditQuery.QueryPermission"/>'s own precedent for
    /// a Core-defined, non-sample permission.
    /// </summary>
    public static readonly Permission OpenApiDocumentPermission = new("api.openapi.read");

    private readonly IApiEndpointRegistry _endpointRegistry;
    private readonly ICommandRegistry _commandRegistry;
    private readonly IIdentityService _identityService;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IAuditRecorder _auditRecorder;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ApiRequestHandler"/> class.
    /// </summary>
    public ApiRequestHandler(
        IApiEndpointRegistry endpointRegistry,
        ICommandRegistry commandRegistry,
        IIdentityService identityService,
        IPermissionEvaluator permissionEvaluator,
        IAuditRecorder auditRecorder,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(endpointRegistry);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(auditRecorder);

        _endpointRegistry = endpointRegistry;
        _commandRegistry = commandRegistry;
        _identityService = identityService;
        _permissionEvaluator = permissionEvaluator;
        _auditRecorder = auditRecorder;
        _logger = logger;
    }

    /// <summary>
    /// Handles one REST request: looks up a matching route, resolves and
    /// authorizes the caller, dispatches through the Command Framework,
    /// and maps the outcome to an HTTP status code — never leaking
    /// internal exception detail into the response body.
    /// </summary>
    /// <param name="method">The inbound request's own HTTP method.</param>
    /// <param name="path">The inbound request's own path.</param>
    /// <param name="identityHeaderValue">
    /// The value of <see cref="IdentityHeaderName"/>, or <see langword="null"/>
    /// if the caller supplied none.
    /// </param>
    /// <param name="cancellationToken">A token observed while the command's own handler runs.</param>
    public async Task<ApiResponse> HandleAsync(string method, string path, string? identityHeaderValue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);

        var route = _endpointRegistry.Routes.FirstOrDefault(r =>
            string.Equals(r.Method, method, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));

        if (route is null)
        {
            Log(method, path, 404, null);
            return new ApiResponse(404, "Not Found");
        }

        var denial = await AuthorizeAsync(method, path, identityHeaderValue, route.RequiredPermission, cancellationToken).ConfigureAwait(false);
        if (denial is not null)
            return denial;

        CommandResult result;

        try
        {
            result = await _commandRegistry.InvokeAsync(route.CommandId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CommandNotFoundException)
        {
            Log(method, path, 404, identityHeaderValue);
            return new ApiResponse(404, "Not Found");
        }
        catch (Exception ex)
        {
            _logger?.Error($"Unhandled exception dispatching command '{route.CommandId}' for '{method} {path}'.", ex);
            Log(method, path, 500, identityHeaderValue);
            return new ApiResponse(500, "Internal Server Error");
        }

        var statusCode = result.Succeeded ? 200 : 400;
        Log(method, path, statusCode, identityHeaderValue);
        return new ApiResponse(statusCode, result.Message);
    }

    /// <summary>
    /// Handles a request for the generated OpenAPI document
    /// (<see cref="RestApiHostedService.OpenApiPath"/>) — routed through
    /// exactly the same identity resolution, permission enforcement, and
    /// audit recording every command route goes through
    /// (<see cref="AuthorizeAsync"/>), guarded by
    /// <see cref="OpenApiDocumentPermission"/>, rather than a second,
    /// parallel check (<c>TD-62</c>). An unauthenticated caller gets the
    /// identical <c>401</c> a command-route caller with no identity gets;
    /// a caller lacking the permission gets the identical <c>403</c>.
    /// </summary>
    /// <param name="path">The inbound request's own path.</param>
    /// <param name="identityHeaderValue">
    /// The value of <see cref="IdentityHeaderName"/>, or <see langword="null"/>
    /// if the caller supplied none.
    /// </param>
    /// <param name="cancellationToken">A token observed while the audit record is written.</param>
    public async Task<ApiResponse> HandleOpenApiDocumentAsync(string path, string? identityHeaderValue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        const string method = "GET";

        var denial = await AuthorizeAsync(method, path, identityHeaderValue, OpenApiDocumentPermission, cancellationToken).ConfigureAwait(false);
        if (denial is not null)
            return denial;

        var document = OpenApiDocumentGenerator.Generate(_endpointRegistry.Routes);
        Log(method, path, 200, identityHeaderValue);
        return new ApiResponse(200, document);
    }

    /// <summary>
    /// The identity + permission + audit path shared by
    /// <see cref="HandleAsync"/> and <see cref="HandleOpenApiDocumentAsync"/>:
    /// resolves the caller, enforces <paramref name="requiredPermission"/>,
    /// and records the Audit entry for an authorized request. Returns the
    /// <see cref="ApiResponse"/> to send back (401/403) if authorization
    /// fails; <see langword="null"/> if the caller is authorized to proceed.
    /// </summary>
    private async Task<ApiResponse?> AuthorizeAsync(string method, string path, string? identityHeaderValue, Permission requiredPermission, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identityHeaderValue))
        {
            Log(method, path, 401, null);
            return new ApiResponse(401, "Unauthorized: no identity supplied.");
        }

        var principal = _identityService.GetPrincipal(identityHeaderValue);

        if (!_permissionEvaluator.HasPermission(principal, requiredPermission))
        {
            Log(method, path, 403, identityHeaderValue);
            return new ApiResponse(403, $"Forbidden: principal '{identityHeaderValue}' does not hold '{requiredPermission.Key}'.");
        }

        await _auditRecorder.RecordAsync(
            RequestAuditAction,
            new Dictionary<string, string>
            {
                ["Method"] = method,
                ["Path"] = path,
                [CallerIdentityDetailKey] = identityHeaderValue,
            },
            cancellationToken).ConfigureAwait(false);

        return null;
    }

    private void Log(string method, string path, int statusCode, string? principalId) =>
        _logger?.Information($"{method} {path} -> {statusCode}" + (principalId is null ? string.Empty : $" (principal '{principalId}')"));
}
