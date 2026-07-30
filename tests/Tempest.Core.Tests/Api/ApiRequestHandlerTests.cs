using Tempest.Core.Api;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Api;

// Proves the approved REST API contract against the real ApiRequestHandler
// implementation - route lookup, identity resolution, permission
// enforcement, dispatch through the existing, unmodified
// ICommandRegistry.InvokeAsync, and error-mapping (401/403/404/500) with
// no leaked internal exception detail. Deliberately independent of
// Kestrel/ASP.NET Core itself - see ApiSampleModuleIntegrationTests for
// the real-HTTP end-to-end proof.
public class ApiRequestHandlerTests
{
    private const string GrantedIdentityId = "granted-user";
    private const string UngrantedIdentityId = "ungranted-user";
    private static readonly Permission RequiredPermission = new("test.invoke");

    private static (ApiRequestHandler Handler, IApiEndpointRegistry EndpointRegistry, RecordingApiCommandHandler CommandHandler, FakeAuditRecorder AuditRecorder)
        BuildHandler(Func<CommandResult>? onHandle = null)
    {
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Identity:Roles:Tester:Permissions", RequiredPermission.Key),
            new KeyValuePair<string, string>($"Identity:Principals:{GrantedIdentityId}:Roles", "Tester"),
        ])).Build();

        var roleProvider = new RoleProvider(configuration);
        var permissionEvaluator = new PermissionEvaluator();
        var identityService = new IdentityService(configuration, roleProvider, new CurrentPrincipalAccessor());

        var table = new CommandHandlerTable();
        var dispatcher = new CommandDispatcher(table);
        var registry = new CommandRegistry(table);
        var commandHandler = new RecordingApiCommandHandler(onHandle);
        dispatcher.RegisterHandler(commandHandler);
        registry.RegisterDescriptor(new CommandDescriptor(
            id: "test.command",
            displayName: "Test Command",
            category: "Test",
            description: "A test command.",
            createDefault: () => new RecordedApiCommand()));

        var endpointRegistry = new ApiEndpointRegistry();
        endpointRegistry.MapCommand("GET", "/api/v1/test", "test.command", RequiredPermission);

        var auditRecorder = new FakeAuditRecorder();
        var handler = new ApiRequestHandler(endpointRegistry, registry, identityService, permissionEvaluator, auditRecorder);

        return (handler, endpointRegistry, commandHandler, auditRecorder);
    }

    // ------------------------------------------------------------------
    // Route lookup
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_UnmappedPath_Returns404()
    {
        var (handler, _, _, _) = BuildHandler();

        var response = await handler.HandleAsync("GET", "/api/v1/no-such-route", GrantedIdentityId);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_MappedPathWrongMethod_Returns404()
    {
        var (handler, _, _, _) = BuildHandler();

        var response = await handler.HandleAsync("POST", "/api/v1/test", GrantedIdentityId);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_MethodMatchIsCaseInsensitive()
    {
        var (handler, _, _, _) = BuildHandler();

        var response = await handler.HandleAsync("get", "/api/v1/test", GrantedIdentityId);

        Assert.Equal(200, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Identity resolution and permission enforcement
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_NoIdentityHeader_Returns401()
    {
        var (handler, _, commandHandler, _) = BuildHandler();

        var response = await handler.HandleAsync("GET", "/api/v1/test", identityHeaderValue: null);

        Assert.Equal(401, response.StatusCode);
        Assert.Equal(0, commandHandler.CallCount);
    }

    [Fact]
    public async Task HandleAsync_EmptyIdentityHeader_Returns401()
    {
        var (handler, _, _, _) = BuildHandler();

        var response = await handler.HandleAsync("GET", "/api/v1/test", identityHeaderValue: "   ");

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_UnrecognisedIdentity_ResolvesToZeroPermissionsAndReturns403()
    {
        var (handler, _, commandHandler, _) = BuildHandler();

        var response = await handler.HandleAsync("GET", "/api/v1/test", "nobody-configured");

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(0, commandHandler.CallCount);
    }

    [Fact]
    public async Task HandleAsync_IdentityWithoutRequiredPermission_Returns403()
    {
        var (handler, _, commandHandler, _) = BuildHandler();

        var response = await handler.HandleAsync("GET", "/api/v1/test", UngrantedIdentityId);

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(0, commandHandler.CallCount);
    }

    [Fact]
    public async Task HandleAsync_IdentityWithRequiredPermission_DispatchesTheCommand()
    {
        var (handler, _, commandHandler, _) = BuildHandler();

        await handler.HandleAsync("GET", "/api/v1/test", GrantedIdentityId);

        Assert.Equal(1, commandHandler.CallCount);
    }

    // ------------------------------------------------------------------
    // Dispatch outcome mapping
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_CommandSucceeds_Returns200WithTheResultMessage()
    {
        var (handler, _, _, _) = BuildHandler(() => CommandResult.Success("all good"));

        var response = await handler.HandleAsync("GET", "/api/v1/test", GrantedIdentityId);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("all good", response.Body);
    }

    [Fact]
    public async Task HandleAsync_CommandReportsForeseeableFailure_Returns400()
    {
        var (handler, _, _, _) = BuildHandler(() => CommandResult.Failure("bad input"));

        var response = await handler.HandleAsync("GET", "/api/v1/test", GrantedIdentityId);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("bad input", response.Body);
    }

    [Fact]
    public async Task HandleAsync_CommandThrows_Returns500_WithNoLeakedExceptionDetail()
    {
        var (handler, _, _, _) = BuildHandler(() => throw new InvalidOperationException("a secret internal detail"));

        var response = await handler.HandleAsync("GET", "/api/v1/test", GrantedIdentityId);

        Assert.Equal(500, response.StatusCode);
        Assert.DoesNotContain("secret internal detail", response.Body);
        Assert.DoesNotContain("InvalidOperationException", response.Body);
    }

    // ------------------------------------------------------------------
    // Audit — caller identity carried in Detail, never ambient-attributed
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_AuthorizedRequest_RecordsAnAuditEntryCarryingTheCallerIdentityInDetail()
    {
        var (handler, _, _, auditRecorder) = BuildHandler();

        await handler.HandleAsync("GET", "/api/v1/test", GrantedIdentityId);

        var recorded = Assert.Single(auditRecorder.Recorded);
        Assert.Equal(ApiRequestHandler.RequestAuditAction, recorded.Action);
        Assert.Equal(GrantedIdentityId, recorded.Detail![ApiRequestHandler.CallerIdentityDetailKey]);
    }

    [Fact]
    public async Task HandleAsync_UnauthorizedRequest_RecordsNoAuditEntry()
    {
        var (handler, _, _, auditRecorder) = BuildHandler();

        await handler.HandleAsync("GET", "/api/v1/test", UngrantedIdentityId);

        Assert.Empty(auditRecorder.Recorded);
    }

    // ------------------------------------------------------------------
    // Argument validation
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_NullMethod_ThrowsArgumentNullException()
    {
        var (handler, _, _, _) = BuildHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.HandleAsync(null!, "/api/v1/test", GrantedIdentityId));
    }

    [Fact]
    public async Task HandleAsync_NullPath_ThrowsArgumentNullException()
    {
        var (handler, _, _, _) = BuildHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.HandleAsync("GET", null!, GrantedIdentityId));
    }
}
