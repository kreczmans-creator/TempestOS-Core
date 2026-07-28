using System.Text.Json;

namespace Tempest.Core.Tests.Templates;

// Proves the real, shipped template.json - not a copy or a mock of it -
// parses as valid JSON and declares the shortName/sourceName/symbols the
// rest of this template's own documentation (README.md, this repository's
// Academy) promises.
public class ModuleTemplateManifestTests
{
    private static JsonDocument LoadManifest()
    {
        var path = Path.Combine(RepositoryPaths.ModuleTemplateDirectory, ".template.config", "template.json");
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    [Fact]
    public void TemplateManifest_IsValidJson()
    {
        using var document = LoadManifest();

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Fact]
    public void TemplateManifest_DeclaresTheDocumentedShortName()
    {
        using var document = LoadManifest();

        Assert.Equal("tempest-module", document.RootElement.GetProperty("shortName").GetString());
    }

    [Fact]
    public void TemplateManifest_DeclaresTheExpectedSourceName()
    {
        using var document = LoadManifest();

        Assert.Equal("TempestSampleModule", document.RootElement.GetProperty("sourceName").GetString());
    }

    [Theory]
    [InlineData("ModuleId", "TEMPEST_MODULE_ID", "your.module.id")]
    [InlineData("ModuleDisplayName", "TEMPEST_MODULE_DISPLAY_NAME", "New Module")]
    [InlineData("ModuleVersion", "TEMPEST_MODULE_VERSION", "1.0.0")]
    public void TemplateManifest_DeclaresEachDocumentedSymbol_WithItsDefaultValue(
        string symbolName, string expectedReplaces, string expectedDefault)
    {
        using var document = LoadManifest();

        var symbol = document.RootElement.GetProperty("symbols").GetProperty(symbolName);

        Assert.Equal("parameter", symbol.GetProperty("type").GetString());
        Assert.Equal(expectedReplaces, symbol.GetProperty("replaces").GetString());
        Assert.Equal(expectedDefault, symbol.GetProperty("defaultValue").GetString());
    }

    [Fact]
    public void TemplateManifest_IsNotPartOfTheMainSolution()
    {
        // Deliberate (RD-0045, src/Templates/README.md): template source
        // is copied/renamed by the templating engine, never built in
        // place - so TempestOS.slnx must not reference it.
        var slnxPath = Path.Combine(RepositoryPaths.RepositoryRoot, "src", "TempestOS.slnx");
        var slnxContent = File.ReadAllText(slnxPath);

        Assert.DoesNotContain("Templates", slnxContent);
    }
}
