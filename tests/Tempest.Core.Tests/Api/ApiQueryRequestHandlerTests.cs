using System.Text.Json;
using Tempest.Core.Api;
using Tempest.Core.Commands;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Api;

// Proves ApiQueryRequestHandler (ADR-0114) - the late-bound pipeline's
// own status mapping, mirroring ApiRequestHandlerTests' proof of the
// command pipeline: 404 unknown route, 401 missing identity, 403 missing
// permission, audit before execution, 200 JSON for a query, 200/400 for
// an action's own CommandResult, 400 for a binding fault, 500 (detail
// never leaked) for anything else.
public class ApiQueryRequestHandlerTests
{
    private static readonly Permission ReadPermission = new("companion.read");

    private sealed class GrantingIdentityService : IIdentityService
    {
        public IPrincipal GetPrincipal(string identityId) =>
            new PlatformPrincipal(new PlatformIdentity(identityId, identityId), [ReadPermission]);

        public IPrincipal EstablishCurrentPrincipal(string identityId) =>
            throw new InvalidOperationException("The query pipeline must never establish the ambient current principal - ADR-0052.");
    }

    private sealed class DenyingIdentityService : IIdentityService
    {
        public IPrincipal GetPrincipal(string identityId) =>
            new PlatformPrincipal(new PlatformIdentity(identityId, identityId), []);

        public IPrincipal EstablishCurrentPrincipal(string identityId) =>
            throw new InvalidOperationException("The query pipeline must never establish the ambient current principal - ADR-0052.");
    }

    private static ApiQueryRequestHandler BuildHandler(ApiQueryRegistry registry, FakeAuditRecorder? audit = null, bool grantPermission = true) =>
        new(
            registry,
            grantPermission ? new GrantingIdentityService() : new DenyingIdentityService(),
            new PermissionEvaluator(),
            audit ?? new FakeAuditRecorder());

    [Fact]
    public async Task HandleAsync_UnknownRoute_Returns404()
    {
        var handler = BuildHandler(new ApiQueryRegistry());

        var (response, isJson) = await handler.HandleAsync("GET", "/api/v1/companion/none", "caller", null);

        Assert.Equal(404, response.StatusCode);
        Assert.False(isJson);
    }

    [Fact]
    public async Task HandleAsync_NoIdentity_Returns401()
    {
        var registry = new ApiQueryRegistry();
        registry.MapQuery("/api/v1/q", ReadPermission, _ => Task.FromResult("{}"));
        var handler = BuildHandler(registry);

        var (response, _) = await handler.HandleAsync("GET", "/api/v1/q", "   ", null);

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_MissingPermission_Returns403_AndNeverExecutes()
    {
        var executed = false;
        var registry = new ApiQueryRegistry();
        registry.MapQuery("/api/v1/q", ReadPermission, _ =>
        {
            executed = true;
            return Task.FromResult("{}");
        });
        var handler = BuildHandler(registry, grantPermission: false);

        var (response, _) = await handler.HandleAsync("GET", "/api/v1/q", "caller", null);

        Assert.Equal(403, response.StatusCode);
        Assert.Contains("companion.read", response.Body);
        Assert.False(executed);
    }

    [Fact]
    public async Task HandleAsync_Query_Returns200Json_AndAudits()
    {
        var audit = new FakeAuditRecorder();
        var registry = new ApiQueryRegistry();
        registry.MapQuery("/api/v1/q", ReadPermission, _ => Task.FromResult("""{"value":1}"""));
        var handler = BuildHandler(registry, audit);

        var (response, isJson) = await handler.HandleAsync("GET", "/api/v1/q", "caller", null);

        Assert.Equal(200, response.StatusCode);
        Assert.True(isJson);
        Assert.Equal("""{"value":1}""", response.Body);

        var record = Assert.Single(audit.Recorded);
        Assert.Equal(ApiRequestHandler.RequestAuditAction, record.Action);
        Assert.Equal("caller", record.Detail![ApiRequestHandler.CallerIdentityDetailKey]);
    }

    [Fact]
    public async Task HandleAsync_Action_MapsCommandResultTo200And400()
    {
        var registry = new ApiQueryRegistry();
        registry.MapAction("/api/v1/ok", ReadPermission, (_, _) => Task.FromResult(CommandResult.Success("done")));
        registry.MapAction("/api/v1/fail", ReadPermission, (_, _) => Task.FromResult(CommandResult.Failure("nope")));
        var handler = BuildHandler(registry);

        var (ok, okJson) = await handler.HandleAsync("POST", "/api/v1/ok", "caller", "{}");
        var (fail, _) = await handler.HandleAsync("POST", "/api/v1/fail", "caller", "{}");

        Assert.Equal(200, ok.StatusCode);
        Assert.False(okJson);
        Assert.Equal("done", ok.Body);
        Assert.Equal(400, fail.StatusCode);
        Assert.Equal("nope", fail.Body);
    }

    [Fact]
    public async Task HandleAsync_Action_ReceivesTheRequestBody()
    {
        string? received = null;
        var registry = new ApiQueryRegistry();
        registry.MapAction("/api/v1/echo", ReadPermission, (body, _) =>
        {
            received = body;
            return Task.FromResult(CommandResult.Success());
        });
        var handler = BuildHandler(registry);

        await handler.HandleAsync("POST", "/api/v1/echo", "caller", """{"x":42}""");

        Assert.Equal("""{"x":42}""", received);
    }

    [Fact]
    public async Task HandleAsync_BindingFault_Returns400WithTheMessage()
    {
        var registry = new ApiQueryRegistry();
        registry.MapAction("/api/v1/bind", ReadPermission, (_, _) => throw new ApiRequestBindingException("targetObjectId is required."));
        registry.MapAction("/api/v1/json", ReadPermission, (body, ct) =>
        {
            JsonSerializer.Deserialize<Dictionary<string, int>>(body!);
            return Task.FromResult(CommandResult.Success());
        });
        var handler = BuildHandler(registry);

        var (binding, _) = await handler.HandleAsync("POST", "/api/v1/bind", "caller", "{}");
        var (malformed, _) = await handler.HandleAsync("POST", "/api/v1/json", "caller", "not json");

        Assert.Equal(400, binding.StatusCode);
        Assert.Contains("targetObjectId", binding.Body);
        Assert.Equal(400, malformed.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_UnhandledException_Returns500_NeverLeakingDetail()
    {
        var registry = new ApiQueryRegistry();
        registry.MapQuery("/api/v1/boom", ReadPermission, _ => throw new InvalidOperationException("secret internal detail"));
        var handler = BuildHandler(registry);

        var (response, _) = await handler.HandleAsync("GET", "/api/v1/boom", "caller", null);

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("Internal Server Error", response.Body);
        Assert.DoesNotContain("secret", response.Body);
    }

    [Fact]
    public async Task HandleAsync_Cancellation_Rethrows()
    {
        var registry = new ApiQueryRegistry();
        registry.MapQuery("/api/v1/slow", ReadPermission, async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return "{}";
        });
        var handler = BuildHandler(registry);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(20);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync("GET", "/api/v1/slow", "caller", null, cts.Token));
    }

    [Fact]
    public async Task HandleAsync_RouteRegisteredAfterConstruction_Resolves()
    {
        // The late-binding property itself: a route registered after the
        // handler (and, in production, the hosted service) already exists
        // must serve - this is the whole reason ADR-0114's surface is not
        // a second ApiEndpointRegistry.
        var registry = new ApiQueryRegistry();
        var handler = BuildHandler(registry);

        var (before, _) = await handler.HandleAsync("GET", "/api/v1/late", "caller", null);
        registry.MapQuery("/api/v1/late", ReadPermission, _ => Task.FromResult("{}"));
        var (after, _) = await handler.HandleAsync("GET", "/api/v1/late", "caller", null);

        Assert.Equal(404, before.StatusCode);
        Assert.Equal(200, after.StatusCode);
    }
}
