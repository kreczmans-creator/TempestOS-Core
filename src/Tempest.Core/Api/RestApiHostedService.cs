using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Tempest.Core.Audit;
using Tempest.Core.BackgroundServices;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;
using Tempest.Core.Logging;

namespace Tempest.Core.Api;

/// <summary>
/// The REST API's own hosted-service scaffold — hosts an HTTP listener
/// (ASP.NET Core/Kestrel, <c>ADR-0049</c>) and maps every registered
/// <see cref="ApiRouteDescriptor"/> to <see cref="ApiRequestHandler"/>.
/// </summary>
/// <remarks>
/// <para>
/// Discovered and orchestrated identically to any other hosted service —
/// started Phase 8.1, stopped Phase 10.1 (<c>ADR-0030</c>), per
/// <c>ADR-0047</c>. Isolated by default, not critical
/// (<c>ADR-0021</c>) — does not implement
/// <see cref="ICriticalBackgroundService"/>; a failure to start (for
/// example, the configured port is already in use) is logged and
/// isolated, not Host-fatal, exactly like
/// <c>NotificationSampleHostedService</c>'s own default.
/// </para>
/// <para>
/// <b>ASP.NET Core's own hosting infrastructure is confined to this one
/// type.</b> <see cref="WebApplication"/> necessarily builds its own,
/// internal <see cref="IServiceProvider"/> as an implementation detail of
/// hosting HTTP requests — this is unavoidable when using the shared
/// framework's own <see cref="WebApplicationBuilder"/>. Per
/// <c>ADR-0049</c>'s own scope ("this platform's own DI container...
/// remain entirely unchanged and unreplaced"), that internal container
/// is never used to resolve a single <c>Tempest.Core</c> service: every
/// route delegate closes over the exact <see cref="ApiRequestHandler"/>
/// instance this hosted service itself received via ordinary constructor
/// injection from TempestOS's own container, never calling
/// <c>HttpContext.RequestServices</c> for anything TempestOS-specific.
/// </para>
/// <para>
/// Binds to the loopback address only by default
/// (<see cref="DefaultPort"/>), overridable via
/// <see cref="PortConfigurationKey"/> — a disclosed mitigation for the
/// absence of real authentication (<see cref="ApiRequestHandler"/>'s own
/// remarks) until a genuine credential-verification mechanism is
/// designed. No TLS is configured this release — see this Work Package's
/// own Technical Debt Assessment.
/// </para>
/// </remarks>
public sealed class RestApiHostedService : IHostedService
{
    /// <summary>The configuration key read for the listening port.</summary>
    public const string PortConfigurationKey = "Api:Port";

    /// <summary>The port used when <see cref="PortConfigurationKey"/> is not configured.</summary>
    public const int DefaultPort = 5080;

    /// <summary>The conventional path the generated OpenAPI document is served at.</summary>
    public const string OpenApiPath = "/api/v1/openapi.json";

    /// <summary>
    /// Gets the port this instance is actually listening on, once
    /// started — useful for tests, which configure port <c>0</c> (an
    /// OS-assigned, collision-free ephemeral port) rather than a fixed
    /// number. <see langword="null"/> before <see cref="StartAsync"/>
    /// completes.
    /// </summary>
    public int? BoundPort { get; private set; }

    private readonly IApiEndpointRegistry _endpointRegistry;
    private readonly IApiQueryRegistry _queryRegistry;
    private readonly ApiRequestHandler _requestHandler;
    private readonly ApiQueryRequestHandler _queryRequestHandler;
    private readonly IConfigurationProvider _configuration;
    private readonly ILogger? _logger;
    private WebApplication? _app;

    /// <summary>
    /// Initialises a new instance of the <see cref="RestApiHostedService"/> class.
    /// </summary>
    public RestApiHostedService(
        IApiEndpointRegistry endpointRegistry,
        IApiQueryRegistry queryRegistry,
        ICommandRegistry commandRegistry,
        IIdentityService identityService,
        IPermissionEvaluator permissionEvaluator,
        IAuditRecorder auditRecorder,
        IConfigurationProvider configuration,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(endpointRegistry);
        ArgumentNullException.ThrowIfNull(queryRegistry);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(configuration);

        _endpointRegistry = endpointRegistry;
        _queryRegistry = queryRegistry;
        _requestHandler = new ApiRequestHandler(endpointRegistry, commandRegistry, identityService, permissionEvaluator, auditRecorder, logger);
        _queryRequestHandler = new ApiQueryRequestHandler(queryRegistry, identityService, permissionEvaluator, auditRecorder, logger);
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var port = _configuration.TryGetValue(PortConfigurationKey, out var configuredPort) && int.TryParse(configuredPort, out var parsedPort)
            ? parsedPort
            : DefaultPort;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        var app = builder.Build();

        foreach (var route in _endpointRegistry.Routes)
        {
            app.MapMethods(route.Path, [route.Method], (HttpContext context) => InvokeAsync(context));
        }

        app.MapGet(OpenApiPath, (HttpContext context) => InvokeOpenApiAsync(context));

        // The late-bound query-and-action surface (ADR-0114): one
        // catch-all fallback, resolved per request against
        // IApiQueryRegistry — so a composition root can register routes
        // after this hosted service has already started (which is exactly
        // when the Engineering Workspace's own read models first exist).
        // Statically mapped command routes above always win — a fallback
        // has the lowest possible routing precedence by definition.
        app.MapFallback((HttpContext context) => InvokeQueryAsync(context));

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        _app = app;

        var boundAddress = app.Services.GetService(typeof(IServer)) is IServer server
            ? server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            : null;
        BoundPort = boundAddress is not null && Uri.TryCreate(boundAddress, UriKind.Absolute, out var uri) ? uri.Port : port;

        _logger?.Information($"REST API listening on http://127.0.0.1:{BoundPort}.");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            await _app.StopAsync(cancellationToken).ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task InvokeAsync(HttpContext context)
    {
        var identityHeaderValue = context.Request.Headers.TryGetValue(ApiRequestHandler.IdentityHeaderName, out var values)
            ? values.ToString()
            : null;

        var response = await _requestHandler.HandleAsync(
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            identityHeaderValue,
            context.RequestAborted).ConfigureAwait(false);

        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = "text/plain";

        if (response.Body is not null)
            await context.Response.WriteAsync(response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    private async Task InvokeQueryAsync(HttpContext context)
    {
        var identityHeaderValue = context.Request.Headers.TryGetValue(ApiRequestHandler.IdentityHeaderName, out var values)
            ? values.ToString()
            : null;

        string? requestBody = null;

        if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            using var reader = new StreamReader(context.Request.Body);
            requestBody = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        var (response, isJson) = await _queryRequestHandler.HandleAsync(
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            identityHeaderValue,
            requestBody,
            context.RequestAborted).ConfigureAwait(false);

        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = isJson ? "application/json" : "text/plain";

        if (response.Body is not null)
            await context.Response.WriteAsync(response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    private Task InvokeOpenApiAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(OpenApiDocumentGenerator.Generate(_endpointRegistry.Routes, _queryRegistry.Routes), context.RequestAborted);
    }
}
