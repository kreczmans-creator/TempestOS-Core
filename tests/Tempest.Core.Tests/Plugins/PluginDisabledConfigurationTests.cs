using Tempest.Core.Logging;
using Tempest.Core.Plugins;
using Tempest.Core.Versioning;
using static Tempest.Core.Tests.Plugins.PluginManifestJsonBuilder;

namespace Tempest.Core.Tests.Plugins;

// WP 13.1A: Runtime:Plugins:Disabled - PluginManifestDiscoveryService's own
// disabledPluginIds constructor parameter (the Host-level comma-separated
// configuration parsing itself is covered separately, at Host level, in
// TempestHostPluginConfigurationTests).
public class PluginDisabledConfigurationTests
{
    private static readonly IPlatformVersionProvider DefaultVersionProvider =
        new FakePlatformVersionProvider(new Version(1, 0, 0));

    [Fact]
    public void DiscoverManifests_PluginIdInDisabledList_IsIsolatedAsDisabled_LoggedAtInformation()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "disabled-plugin");
        WriteManifest(folder, Build(id: "test.disabled", assemblyFileName: "Disabled.dll"));

        var logger = new RecordingLevelLogger();
        var registry = new PluginRegistry();
        var service = new PluginManifestDiscoveryService(
            temp.Path, DefaultVersionProvider, logger, disabledPluginIds: ["test.disabled"], registryRecorder: registry);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("test.disabled", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => (e.Level == LogLevel.Warning || e.Level == LogLevel.Error) && e.Message.Contains("test.disabled", StringComparison.Ordinal));

        var entry = Assert.Single(registry.Entries);
        Assert.Equal("test.disabled", entry.Id);
        Assert.Equal(PluginRegistryState.Disabled, entry.State);
    }

    [Fact]
    public void DiscoverManifests_DisabledPluginWithBrokenMinimumPlatformVersion_IsDisabled_NotFlaggedAsInvalidManifest()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "disabled-broken-version-plugin");

        // Would ordinarily throw InvalidPluginManifestException (Warning) -
        // the disabled short-circuit must pre-empt that check entirely.
        WriteManifest(folder, Build(id: "test.disabled-broken-version", minimumPlatformVersion: "not-a-version", assemblyFileName: "Plugin.dll"));

        var logger = new RecordingLevelLogger();
        var registry = new PluginRegistry();
        var service = new PluginManifestDiscoveryService(
            temp.Path, DefaultVersionProvider, logger, disabledPluginIds: ["test.disabled-broken-version"], registryRecorder: registry);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);

        var entry = Assert.Single(registry.Entries);
        Assert.Equal(PluginRegistryState.Disabled, entry.State);
    }

    [Fact]
    public void DiscoverManifests_DisabledPluginWithUnsatisfiableDependency_IsDisabled_NotFlaggedAsDependencyUnmet()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "disabled-unsatisfiable-dependency-plugin");

        // Would ordinarily be individually valid, then isolated during
        // dependency graph resolution as MissingPluginDependencyException
        // (Warning) - the disabled short-circuit must remove it before it
        // ever becomes an accepted candidate, so graph resolution never
        // even sees it.
        WriteManifest(folder, Build(
            id: "test.disabled-unsatisfiable-dependency",
            assemblyFileName: "Plugin.dll",
            dependencies: [DependencyFragment.On("test.does-not-exist", "1.0.0")]));

        var logger = new RecordingLevelLogger();
        var registry = new PluginRegistry();
        var service = new PluginManifestDiscoveryService(
            temp.Path, DefaultVersionProvider, logger, disabledPluginIds: ["test.disabled-unsatisfiable-dependency"], registryRecorder: registry);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);

        var entry = Assert.Single(registry.Entries);
        Assert.Equal(PluginRegistryState.Disabled, entry.State);
    }

    [Fact]
    public void DiscoverManifests_PluginIdNotInDisabledList_IsUnaffected()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "enabled-plugin");
        WriteManifest(folder, Build(id: "test.enabled", assemblyFileName: "Enabled.dll"));

        var service = new PluginManifestDiscoveryService(
            temp.Path, DefaultVersionProvider, disabledPluginIds: ["some.other.id"], allowUnsignedLoad: true);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.enabled", manifest.Id);
    }

    [Fact]
    public void DiscoverManifests_NoDisabledListConfigured_BehavesExactlyAsBeforeThisWorkPackage()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "unconfigured-plugin");
        WriteManifest(folder, Build(id: "test.unconfigured", assemblyFileName: "Unconfigured.dll"));

        // disabledPluginIds deliberately omitted (defaults to null).
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, allowUnsignedLoad: true);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.unconfigured", manifest.Id);
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
