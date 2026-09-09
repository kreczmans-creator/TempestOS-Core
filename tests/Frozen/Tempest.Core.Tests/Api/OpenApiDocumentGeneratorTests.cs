using System.Text.Json;
using Tempest.Core.Api;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Api;

public class OpenApiDocumentGeneratorTests
{
    [Fact]
    public void Generate_NoRoutes_ProducesAValidEmptyDocument()
    {
        var json = OpenApiDocumentGenerator.Generate([]);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("3.0.3", document.RootElement.GetProperty("openapi").GetString());
        Assert.Empty(document.RootElement.GetProperty("paths").EnumerateObject());
    }

    [Fact]
    public void Generate_OneRoute_DescribesItsMethodAndPath()
    {
        var routes = new[] { new ApiRouteDescriptor("GET", "/api/v1/sample", "sample.command", new Permission("sample.permission")) };

        var json = OpenApiDocumentGenerator.Generate(routes);

        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/sample", out var pathItem));
        Assert.True(pathItem.TryGetProperty("get", out var operation));
        Assert.Contains("sample.command", operation.GetProperty("summary").GetString());
    }

    [Fact]
    public void Generate_MultipleMethodsOnTheSamePath_DescribesBothUnderOnePathItem()
    {
        var routes = new[]
        {
            new ApiRouteDescriptor("GET", "/api/v1/sample", "sample.get", new Permission("sample.permission")),
            new ApiRouteDescriptor("POST", "/api/v1/sample", "sample.post", new Permission("sample.permission")),
        };

        var json = OpenApiDocumentGenerator.Generate(routes);

        using var document = JsonDocument.Parse(json);
        var pathItem = document.RootElement.GetProperty("paths").GetProperty("/api/v1/sample");
        Assert.True(pathItem.TryGetProperty("get", out _));
        Assert.True(pathItem.TryGetProperty("post", out _));
    }

    [Fact]
    public void Generate_NullRoutes_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => OpenApiDocumentGenerator.Generate(null!));
}
