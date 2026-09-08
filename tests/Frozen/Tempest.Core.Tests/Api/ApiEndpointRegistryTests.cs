using Tempest.Core.Api;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Tests.Events;

namespace Tempest.Core.Tests.Api;

public class ApiEndpointRegistryTests
{
    private static readonly Permission SamplePermission = new("sample.permission");

    [Fact]
    public void MapCommand_ThenRoutes_ContainsTheMappedRoute()
    {
        var registry = new ApiEndpointRegistry();

        registry.MapCommand("GET", "/api/v1/sample", "sample.command", SamplePermission);

        var route = Assert.Single(registry.Routes);
        Assert.Equal("GET", route.Method);
        Assert.Equal("/api/v1/sample", route.Path);
        Assert.Equal("sample.command", route.CommandId);
        Assert.Equal(SamplePermission, route.RequiredPermission);
    }

    [Fact]
    public void Routes_NoRegistrations_IsEmpty() =>
        Assert.Empty(new ApiEndpointRegistry().Routes);

    [Fact]
    public void MapCommand_DuplicateMethodAndPath_ThrowsDuplicateApiRouteException()
    {
        var registry = new ApiEndpointRegistry();
        registry.MapCommand("GET", "/api/v1/sample", "sample.command", SamplePermission);

        var exception = Assert.Throws<DuplicateApiRouteException>(() =>
            registry.MapCommand("GET", "/api/v1/sample", "other.command", SamplePermission));

        Assert.Equal("GET", exception.Method);
        Assert.Equal("/api/v1/sample", exception.Path);
    }

    [Fact]
    public void MapCommand_DuplicateCheckIsCaseInsensitiveOnMethod()
    {
        var registry = new ApiEndpointRegistry();
        registry.MapCommand("GET", "/api/v1/sample", "sample.command", SamplePermission);

        Assert.Throws<DuplicateApiRouteException>(() =>
            registry.MapCommand("get", "/api/v1/sample", "other.command", SamplePermission));
    }

    [Fact]
    public void MapCommand_SamePathDifferentMethod_IsAllowed()
    {
        var registry = new ApiEndpointRegistry();
        registry.MapCommand("GET", "/api/v1/sample", "sample.get", SamplePermission);
        registry.MapCommand("POST", "/api/v1/sample", "sample.post", SamplePermission);

        Assert.Equal(2, registry.Routes.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapCommand_NullEmptyOrWhitespaceMethod_ThrowsArgumentException(string? method) =>
        Assert.Throws<ArgumentException>(() => new ApiEndpointRegistry().MapCommand(method!, "/api/v1/sample", "sample.command", SamplePermission));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapCommand_NullEmptyOrWhitespacePath_ThrowsArgumentException(string? path) =>
        Assert.Throws<ArgumentException>(() => new ApiEndpointRegistry().MapCommand("GET", path!, "sample.command", SamplePermission));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MapCommand_NullEmptyOrWhitespaceCommandId_ThrowsArgumentException(string? commandId) =>
        Assert.Throws<ArgumentException>(() => new ApiEndpointRegistry().MapCommand("GET", "/api/v1/sample", commandId!, SamplePermission));

    // ------------------------------------------------------------------
    // Platform Service registration (ADR-0047: an ordinary singleton, no
    // Composition Root treatment needed)
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesIApiEndpointRegistryToApiEndpointRegistry()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<IApiEndpointRegistry, ApiEndpointRegistry>();
        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService(typeof(IApiEndpointRegistry));

        Assert.IsType<ApiEndpointRegistry>(resolved);
    }

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesTheSameInstanceEveryTime()
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<IApiEndpointRegistry, ApiEndpointRegistry>();
        var provider = new TempestServiceProvider(services);

        var first = provider.GetService(typeof(IApiEndpointRegistry));
        var second = provider.GetService(typeof(IApiEndpointRegistry));

        Assert.Same(first, second);
    }
}
