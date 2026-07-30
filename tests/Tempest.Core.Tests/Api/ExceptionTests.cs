using Tempest.Core.Api;

namespace Tempest.Core.Tests.Api;

public class ExceptionTests
{
    [Fact]
    public void ApiException_MessageConstructor_SetsMessage()
    {
        var exception = new ApiException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void DuplicateApiRouteException_IsAnApiException()
    {
        var exception = new DuplicateApiRouteException("GET", "/api/v1/sample");

        Assert.IsAssignableFrom<ApiException>(exception);
        Assert.Equal("GET", exception.Method);
        Assert.Equal("/api/v1/sample", exception.Path);
        Assert.Contains("GET", exception.Message);
        Assert.Contains("/api/v1/sample", exception.Message);
    }
}
