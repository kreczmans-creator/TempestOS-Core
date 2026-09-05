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
/// <b>Disabled by default</b> (<c>D-024</c>, Proposed — awaiting Product
/// Owner approval): <see cref="StartAsync"/> reads
/// <see cref="EnabledConfigurationKey"/> before constructing any ASP.NET
/// Core object at all, and returns without binding anything unless that
/// key is present and parses as <see langword="true"/>. When enabled, the
/// listener binds to the loopback address only
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
    /// <summary>
    /// The configuration key gating whether the REST API's listener
    /// starts at all (<c>D-024</c>, Proposed — awaiting Product Owner
    /// approval). Absent, empty, or unparseable resolves to
    /// <see langword="false"/> — the platform's own fail-closed default
    /// for a configuration switch, matching <c>ADR-0112</c>'s identical
    /// convention for <c>Plugins:AllowUnsignedLoad</c> (<see cref="StartAsync"/>).
    /// </summary>
    public const string EnabledConfigurationKey = "Runtime:RestApi:Enabled";

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
    private readonly ApiRequestHandler _requestHandler;
    private readonly IConfigurationProvider _configuration;
    private readonly ILogger? _logger;
    private WebApplication? _app;

    /// <summary>
    /// Initialises a new instance of the <see cref="RestApiHostedService"/> class.
    /// </summary>
    public RestApiHostedService(
        IApiEndpointRegistry endpointRegistry,
        ICommandRegistry commandRegistry,
        IIdentityService identityService,
        IPermissionEvaluator permissionEvaluator,
        IAuditRecorder auditRecorder,
        IConfigurationProvider configuration,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(endpointRegistry);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(configuration);

        _endpointRegistry = endpointRegistry;
        _requestHandler = new ApiRequestHandler(endpointRegistry, commandRegistry, identityService, permissionEvaluator, auditRecorder, logger);
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // D-024 (Proposed): evaluated before any ASP.NET Core object is
        // constructed - no WebApplication, no port binding, when the
        // operator has not explicitly opted in. Mirrors ADR-0112's own
        // fail-closed reading of Plugins:AllowUnsignedLoad: absent, empty,
        // or unparseable all resolve to disabled.
        var enabled = _configuration.TryGetValue(EnabledConfigurationKey, out var rawEnabled)
            && bool.TryParse(rawEnabled, out var parsedEnabled)
            && parsedEnabled;

        if (!enabled)
        {
            _logger?.Information($"REST API is disabled (default). Set '{EnabledConfigurationKey}' to 'true' to enable it.");
            return;
        }

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

    private async Task InvokeOpenApiAsync(HttpContext context)
    {
        var identityHeaderValue = context.Request.Headers.TryGetValue(ApiRequestHandler.IdentityHeaderName, out var values)
            ? values.ToString()
            : null;

        var response = await _requestHandler.HandleOpenApiDocumentAsync(
            context.Request.Path.Value ?? string.Empty,
            identityHeaderValue,
            context.RequestAborted).ConfigureAwait(false);

        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = response.StatusCode == 200 ? "application/json" : "text/plain";

        if (response.Body is not null)
            await context.Response.WriteAsync(response.Body, context.RequestAborted).ConfigureAwait(false);
    }
}
