using Tempest.Core.Api;
using Tempest.Core.Commands;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Api;

// Proves ApiQueryRegistry (ADR-0114) - the late-bound query-and-action
// registry: GET/POST method assignment by construction, exact-key
// first-registration-wins duplicate rejection (the ApiEndpointRegistry
// convention), case-insensitive request-time lookup, and registration
// being legal at any time (its whole reason to exist - routes registered
// after the hosted service has started must still resolve).
public class ApiQueryRegistryTests
{
    private static readonly Permission ReadPermission = new("companion.read");

    private static Task<string> EmptyQuery(CancellationToken cancellationToken) => Task.FromResult("{}");

    private static Task<CommandResult> NoopAction(string? body, CancellationToken cancellationToken) =>
        Task.FromResult(CommandResult.Success("ok"));

    [Fact]
    public void MapQuery_RegistersAGetRoute()
    {
        var registry = new ApiQueryRegistry();

        registry.MapQuery("/api/v1/companion/cockpit", ReadPermission, EmptyQuery);

        var route = Assert.Single(registry.Routes);
        Assert.Equal("GET", route.Method);
        Assert.Equal("/api/v1/companion/cockpit", route.Path);
        Assert.NotNull(route.Query);
        Assert.Null(route.Action);
    }

    [Fact]
    public void MapAction_RegistersAPostRoute()
    {
        var registry = new ApiQueryRegistry();

        registry.MapAction("/api/v1/companion/actions/x", ReadPermission, NoopAction);

        var route = Assert.Single(registry.Routes);
        Assert.Equal("POST", route.Method);
        Assert.Null(route.Query);
        Assert.NotNull(route.Action);
    }

    [Fact]
    public void MapQuery_DuplicatePath_Throws()
    {
        var registry = new ApiQueryRegistry();
        registry.MapQuery("/api/v1/q", ReadPermission, EmptyQuery);

        var exception = Assert.Throws<DuplicateApiRouteException>(() => registry.MapQuery("/API/V1/Q", ReadPermission, EmptyQuery));

        Assert.Equal("GET", exception.Method);
    }

    [Fact]
    public void MapQuery_And_MapAction_SamePath_DoNotCollide()
    {
        // GET and POST are distinct route keys - a query and an action may
        // share a path, exactly as HTTP allows.
        var registry = new ApiQueryRegistry();

        registry.MapQuery("/api/v1/q", ReadPermission, EmptyQuery);
        registry.MapAction("/api/v1/q", ReadPermission, NoopAction);

        Assert.Equal(2, registry.Routes.Count);
    }

    [Fact]
    public void Find_MatchesCaseInsensitively()
    {
        var registry = new ApiQueryRegistry();
        registry.MapQuery("/api/v1/companion/cockpit", ReadPermission, EmptyQuery);

        Assert.NotNull(registry.Find("get", "/API/v1/Companion/COCKPIT"));
        Assert.Null(registry.Find("POST", "/api/v1/companion/cockpit"));
        Assert.Null(registry.Find("GET", "/api/v1/companion/other"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapQuery_BlankPath_Throws(string? path)
    {
        var registry = new ApiQueryRegistry();

        Assert.Throws<ArgumentException>(() => registry.MapQuery(path!, ReadPermission, EmptyQuery));
    }

    [Fact]
    public void MapQuery_NullDelegateOrPermission_Throws()
    {
        var registry = new ApiQueryRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.MapQuery("/api/v1/q", null!, EmptyQuery));
        Assert.Throws<ArgumentNullException>(() => registry.MapQuery("/api/v1/q", ReadPermission, null!));
        Assert.Throws<ArgumentNullException>(() => registry.MapAction("/api/v1/q", ReadPermission, null!));
    }
}
