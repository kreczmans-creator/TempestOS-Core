using Tempest.Core.Logging;
using Tempest.Core.Plugins;
using Tempest.Core.Versioning;

namespace Tempest.Core.Tests.Plugins;

// WP 13.1A / ADR-0107: manifest v2 field parsing (Dependencies,
// RequestedCapabilities, Publisher, Signature) - shape-only. Nothing here
// asserts trust/capability enforcement of any kind: RequestedCapabilities/
// Signature are proven only to be parsed and stored verbatim, matching this
// Work Package's own explicit scope boundary.
public class PluginManifestV2FieldsTests
{
    private static readonly IPlatformVersionProvider DefaultVersionProvider =
        new FakePlatformVersionProvider(new Version(1, 0, 0));

    // ----------------------------------------------------------------
    // v2 fields parse correctly onto the resulting PluginManifest
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_ManifestDeclaresV2Fields_ParsesOntoResultingManifest()
    {
        using var temp = new TempDirectory();

        var targetFolder = CreateCandidateFolder(temp.Path, "a-target");
        WriteManifest(targetFolder, PluginManifestJsonBuilder.Build(
            id: "test.target", name: "Target", version: "2.0.0", assemblyFileName: "Target.dll"));

        var dependentFolder = CreateCandidateFolder(temp.Path, "b-dependent");
        WriteManifest(dependentFolder, PluginManifestJsonBuilder.Build(
            id: "test.dependent",
            name: "Dependent",
            version: "1.0.0",
            assemblyFileName: "Dependent.dll",
            dependencies: [PluginManifestJsonBuilder.DependencyFragment.On("test.target", "1.0.0", "3.0.0")],
            requestedCapabilities: ["cap.alpha", "cap.beta"],
            publisher: "Acme Plugins Ltd.",
            signature: "base64-looking-opaque-blob=="));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        Assert.Equal(2, result.Count);
        var dependent = result.Single(m => m.Id == "test.dependent");

        var dependency = Assert.Single(dependent.Dependencies);
        Assert.Equal("test.target", dependency.Id);
        Assert.Equal(new Version(1, 0, 0), dependency.MinimumVersion);
        Assert.Equal(new Version(3, 0, 0), dependency.MaximumVersion);

        Assert.Equal(["cap.alpha", "cap.beta"], dependent.RequestedCapabilities);
        Assert.Equal("Acme Plugins Ltd.", dependent.Publisher);
        Assert.Equal("base64-looking-opaque-blob==", dependent.Signature);
    }

    [Fact]
    public void DiscoverManifests_ManifestWithoutV2Fields_ParsesEmptyDependenciesAndCapabilitiesAndNullPublisherSignature()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "v1-shaped-plugin");

        // Deliberately v1-shaped: no Dependencies/RequestedCapabilities/
        // Publisher/Signature keys at all in the JSON.
        WriteManifest(folder, PluginManifestJsonBuilder.Build(id: "test.v1", name: "V1 Shaped", assemblyFileName: "V1.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.NotNull(manifest.Dependencies);
        Assert.Empty(manifest.Dependencies);
        Assert.NotNull(manifest.RequestedCapabilities);
        Assert.Empty(manifest.RequestedCapabilities);
        Assert.Null(manifest.Publisher);
        Assert.Null(manifest.Signature);
    }

    // ----------------------------------------------------------------
    // Malformed dependency entries - isolated exactly like every other
    // required-field violation (InvalidPluginManifestException, Warning).
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(null, "1.0.0", null)] // missing Id
    [InlineData("", "1.0.0", null)] // blank Id
    [InlineData("dep.target", null, null)] // missing MinimumVersion
    [InlineData("dep.target", "", null)] // blank MinimumVersion
    [InlineData("dep.target", "not-a-version", null)] // unparseable MinimumVersion
    [InlineData("dep.target", "1.0.0", "not-a-version")] // unparseable MaximumVersion
    public void DiscoverManifests_MalformedDependencyEntry_IsIsolated_ExcludedAndLoggedAsWarning(
        string? dependencyId, string? dependencyMinimumVersion, string? dependencyMaximumVersion)
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "malformed-dependency-plugin");
        WriteManifest(folder, PluginManifestJsonBuilder.Build(
            id: "test.malformed-dependency",
            dependencies: [new PluginManifestJsonBuilder.DependencyFragment(dependencyId, dependencyMinimumVersion, dependencyMaximumVersion)]));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void DiscoverManifests_DependencyMaximumVersionBelowMinimumVersion_IsIsolated_ExcludedAndLoggedAsWarning()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "inverted-range-plugin");
        WriteManifest(folder, PluginManifestJsonBuilder.Build(
            id: "test.inverted-range",
            dependencies: [PluginManifestJsonBuilder.DependencyFragment.On("dep.target", "3.0.0", "1.0.0")]));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning &&
            e.Message.Contains("dep.target", StringComparison.Ordinal) &&
            e.Message.Contains("MaximumVersion", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static string CreateCandidateFolder(string root, string folderName)
    {
        var path = Path.Combine(root, folderName);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteManifest(string candidateFolder, string json) =>
        File.WriteAllText(Path.Combine(candidateFolder, PluginManifestDiscoveryService.ManifestFileName), json);
}
