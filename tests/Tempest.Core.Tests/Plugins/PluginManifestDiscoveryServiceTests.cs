using Tempest.Core.Logging;
using Tempest.Core.Plugins;
using Tempest.Core.Versioning;

namespace Tempest.Core.Tests.Plugins;

public class PluginManifestDiscoveryServiceTests
{
    private static readonly IPlatformVersionProvider DefaultVersionProvider =
        new FakePlatformVersionProvider(new Version(1, 0, 0));

    // ----------------------------------------------------------------
    // Successful discovery
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_ValidManifest_ReturnsExpectedManifest()
    {
        using var temp = new TempDirectory();
        var pluginFolder = CreateCandidateFolder(temp.Path, "sample-plugin");
        WriteManifest(pluginFolder, ValidManifestJson("test.sample", "Sample Plugin", "1.0.0", "0.1.0", "Sample.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.sample", manifest.Id);
        Assert.Equal("Sample Plugin", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal(new Version(0, 1, 0), manifest.MinimumPlatformVersion);
        Assert.Equal("Sample.dll", manifest.AssemblyFileName);
        Assert.Equal(Path.GetFullPath(Path.Combine(pluginFolder, "Sample.dll")), manifest.AssemblyPath);
    }

    // ----------------------------------------------------------------
    // Malformed JSON - isolated, Warning (ADR-0025, category 2)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_MalformedJson_IsIsolated_ExcludedAndLoggedAsWarning()
    {
        using var temp = new TempDirectory();
        var pluginFolder = CreateCandidateFolder(temp.Path, "broken-plugin");
        WriteManifest(pluginFolder, "{ this is not valid json");

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    // ----------------------------------------------------------------
    // Missing required fields - isolated, Warning (ADR-0025, category 2)
    // ----------------------------------------------------------------

    [Theory]
    [InlineData("Id")]
    [InlineData("Name")]
    [InlineData("Version")]
    [InlineData("MinimumPlatformVersion")]
    [InlineData("AssemblyFileName")]
    public void DiscoverManifests_MissingRequiredField_IsIsolated_ExcludedAndLoggedAsWarning(string missingField)
    {
        using var temp = new TempDirectory();
        var pluginFolder = CreateCandidateFolder(temp.Path, "incomplete-plugin");
        WriteManifest(pluginFolder, ManifestJsonMissingField(missingField));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(missingField, StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------
    // Security baseline (WP 5.0S): AssemblyFileName path containment -
    // isolated, Warning. A manifest is untrusted input; an absolute path or
    // a "../" escape must not be allowed to resolve outside the candidate
    // folder that declared it.
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_AssemblyFileNameEscapesCandidateFolder_IsIsolated_ExcludedAndLoggedAsWarning()
    {
        using var temp = new TempDirectory();
        var pluginFolder = CreateCandidateFolder(temp.Path, "escaping-plugin");
        WriteManifest(pluginFolder, ValidManifestJson("test.escape", "Escaping Plugin", "1.0.0", "0.1.0", "../../outside.dll"));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("outside its own candidate folder", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoverManifests_AssemblyFileNameIsAbsolutePathOutsideFolder_IsIsolated_ExcludedAndLoggedAsWarning()
    {
        using var temp = new TempDirectory();
        var pluginFolder = CreateCandidateFolder(temp.Path, "absolute-path-plugin");

        using var outsideTemp = new TempDirectory();
        var outsideAssembly = Path.Combine(outsideTemp.Path, "Outside.dll");
        var jsonEscapedOutsideAssembly = outsideAssembly.Replace("\\", "\\\\", StringComparison.Ordinal);

        WriteManifest(pluginFolder, ValidManifestJson("test.absolute", "Absolute Path Plugin", "1.0.0", "0.1.0", jsonEscapedOutsideAssembly));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("outside its own candidate folder", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------
    // Unparseable MinimumPlatformVersion - isolated, Warning
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_UnparseableMinimumPlatformVersion_IsIsolated_ExcludedAndLoggedAsWarning()
    {
        using var temp = new TempDirectory();
        var pluginFolder = CreateCandidateFolder(temp.Path, "bad-version-plugin");
        WriteManifest(pluginFolder, ValidManifestJson("test.badversion", "Bad Version Plugin", "1.0.0", "not-a-version", "Plugin.dll"));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    // ----------------------------------------------------------------
    // Incompatible MinimumPlatformVersion - isolated, Information (ADR-0025, category 4)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_IncompatiblePlatformVersion_IsIsolated_ExcludedAndLoggedAsInformation()
    {
        using var temp = new TempDirectory();
        var pluginFolder = CreateCandidateFolder(temp.Path, "future-plugin");
        WriteManifest(pluginFolder, ValidManifestJson("test.future", "Future Plugin", "1.0.0", "9.9.9", "Plugin.dll"));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, new FakePlatformVersionProvider(new Version(1, 0, 0)), logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("test.future", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void DiscoverManifests_CompatiblePlatformVersion_IsAccepted()
    {
        using var temp = new TempDirectory();
        var pluginFolder = CreateCandidateFolder(temp.Path, "compatible-plugin");
        WriteManifest(pluginFolder, ValidManifestJson("test.compatible", "Compatible Plugin", "1.0.0", "1.0.0", "Plugin.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, new FakePlatformVersionProvider(new Version(1, 0, 0)));

        var result = service.DiscoverManifests();

        Assert.Single(result);
    }

    // ----------------------------------------------------------------
    // Duplicate plugin identity - first (by folder-name order) wins (ADR-0025, category 3)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_DuplicatePluginId_FirstFolderWins_SecondIsolatedAsWarning()
    {
        using var temp = new TempDirectory();
        var first = CreateCandidateFolder(temp.Path, "a-plugin");
        var second = CreateCandidateFolder(temp.Path, "b-plugin");

        WriteManifest(first, ValidManifestJson("dup.id", "First Plugin", "1.0.0", "0.1.0", "First.dll"));
        WriteManifest(second, ValidManifestJson("dup.id", "Second Plugin", "1.0.0", "0.1.0", "Second.dll"));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("First Plugin", manifest.Name);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("dup.id", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------
    // Deterministic ordering (ADR-0026)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_OrdersCandidatesOrdinallyByFolderName_RegardlessOfCreationOrder()
    {
        using var temp = new TempDirectory();

        var zeta = CreateCandidateFolder(temp.Path, "zeta-plugin");
        var alpha = CreateCandidateFolder(temp.Path, "alpha-plugin");
        var mike = CreateCandidateFolder(temp.Path, "mike-plugin");

        WriteManifest(zeta, ValidManifestJson("test.zeta", "Zeta", "1.0.0", "0.1.0", "Z.dll"));
        WriteManifest(alpha, ValidManifestJson("test.alpha", "Alpha", "1.0.0", "0.1.0", "A.dll"));
        WriteManifest(mike, ValidManifestJson("test.mike", "Mike", "1.0.0", "0.1.0", "M.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        Assert.Equal(["test.alpha", "test.mike", "test.zeta"], result.Select(m => m.Id));
    }

    // ----------------------------------------------------------------
    // Absent / empty plugins directory - not a failure (ADR-0025, category 1)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_AbsentPluginsDirectory_ReturnsEmptyList_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        var nonExistentRoot = Path.Combine(temp.Path, "does-not-exist");

        var service = new PluginManifestDiscoveryService(nonExistentRoot, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
    }

    [Fact]
    public void DiscoverManifests_EmptyPluginsDirectory_ReturnsEmptyList()
    {
        using var temp = new TempDirectory();

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
    }

    // ----------------------------------------------------------------
    // Candidate folder missing a manifest file - isolated, Warning (ADR-0025, category 1)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_CandidateFolderMissingManifestFile_IsIsolated_ExcludedAndLoggedAsWarning()
    {
        using var temp = new TempDirectory();
        CreateCandidateFolder(temp.Path, "empty-plugin");

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    // ----------------------------------------------------------------
    // Isolation does not affect sibling candidates - the batch continues
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_OneCandidateFails_OthersAreStillDiscovered()
    {
        using var temp = new TempDirectory();
        var broken = CreateCandidateFolder(temp.Path, "a-broken-plugin");
        var healthy = CreateCandidateFolder(temp.Path, "b-healthy-plugin");

        WriteManifest(broken, "{ not valid json");
        WriteManifest(healthy, ValidManifestJson("test.healthy", "Healthy", "1.0.0", "0.1.0", "Healthy.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("test.healthy", manifest.Id);
    }

    // ----------------------------------------------------------------
    // A genuine, unattributable defect (not a plugin-scoped failure)
    // propagates uncaught - ADR-0025 category 11 / ADR-0026's Host-fatal carve-out.
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_UnexpectedNonPluginException_PropagatesUncaught()
    {
        using var temp = new TempDirectory();
        var pluginFolder = CreateCandidateFolder(temp.Path, "plugin-a");
        WriteManifest(pluginFolder, ValidManifestJson("test.a", "Plugin A", "1.0.0", "0.1.0", "A.dll"));

        // A platform version provider that throws is a genuine defect in the
        // Host's own supporting infrastructure, not attributable to any
        // specific plugin - it must not be caught by the per-candidate
        // catch (PluginException), which only isolates plugin-scoped failures.
        var service = new PluginManifestDiscoveryService(temp.Path, new ThrowingPlatformVersionProvider());

        Assert.Throws<InvalidOperationException>(() => service.DiscoverManifests());
    }

    // ----------------------------------------------------------------
    // Internal seam: the core algorithm trusts the order it is given
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_InternalSeam_ProcessesGivenCandidatesInGivenOrder()
    {
        using var temp = new TempDirectory();
        var first = CreateCandidateFolder(temp.Path, "z-folder");
        var second = CreateCandidateFolder(temp.Path, "a-folder");

        WriteManifest(first, ValidManifestJson("test.z", "Z", "1.0.0", "0.1.0", "Z.dll"));
        WriteManifest(second, ValidManifestJson("test.a", "A", "1.0.0", "0.1.0", "A.dll"));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        // Deliberately out of alphabetical order - the internal seam must not
        // re-sort; sorting is the public overload's own responsibility.
        var result = service.DiscoverManifests([first, second]);

        Assert.Equal(["test.z", "test.a"], result.Select(m => m.Id));
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

    private static string ValidManifestJson(string id, string name, string version, string minimumPlatformVersion, string assemblyFileName) =>
        $$"""
        {
          "Id": "{{id}}",
          "Name": "{{name}}",
          "Version": "{{version}}",
          "MinimumPlatformVersion": "{{minimumPlatformVersion}}",
          "AssemblyFileName": "{{assemblyFileName}}"
        }
        """;

    private static string ManifestJsonMissingField(string missingField)
    {
        var fields = new Dictionary<string, string>
        {
            ["Id"] = "test.incomplete",
            ["Name"] = "Incomplete Plugin",
            ["Version"] = "1.0.0",
            ["MinimumPlatformVersion"] = "0.1.0",
            ["AssemblyFileName"] = "Plugin.dll",
        };

        fields.Remove(missingField);

        var lines = fields.Select(kvp => $"  \"{kvp.Key}\": \"{kvp.Value}\"");
        return "{\n" + string.Join(",\n", lines) + "\n}";
    }

    private sealed class ThrowingPlatformVersionProvider : IPlatformVersionProvider
    {
        public PlatformVersion Version => throw new InvalidOperationException("Simulated platform version resolution failure.");
    }
}
