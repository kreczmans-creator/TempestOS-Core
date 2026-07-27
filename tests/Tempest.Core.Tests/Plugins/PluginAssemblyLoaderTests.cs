using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Plugins;

public class PluginAssemblyLoaderTests
{
    // ----------------------------------------------------------------
    // Successful load
    // ----------------------------------------------------------------

    [Fact]
    public void LoadPlugins_ValidAssembly_LoadsSuccessfully()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Valid.dll", "test.valid", "Valid Plugin", "1.0.0");

        var manifest = CreateManifest("test.valid", assemblyPath);
        var loader = new PluginAssemblyLoader();

        var result = loader.LoadPlugins([manifest]);

        var assembly = Assert.Single(result);
        Assert.StartsWith("Valid-", assembly.GetName().Name, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // Missing assembly - isolated, Error (ADR-0025, category 5)
    // ----------------------------------------------------------------

    [Fact]
    public void LoadPlugins_MissingAssembly_IsIsolated_ExcludedAndLoggedAsError()
    {
        using var temp = new TempDirectory();
        var manifest = CreateManifest("test.missing", Path.Combine(temp.Path, "DoesNotExist.dll"));

        var logger = new RecordingLevelLogger();
        var loader = new PluginAssemblyLoader(logger);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("test.missing", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------
    // Assembly load failure (corrupt file) - isolated, Error (ADR-0025, category 6)
    // ----------------------------------------------------------------

    [Fact]
    public void LoadPlugins_CorruptAssembly_IsIsolated_ExcludedAndLoggedAsError()
    {
        using var temp = new TempDirectory();
        var corruptPath = DynamicPluginAssemblyBuilder.WriteCorruptAssemblyFile(temp.Path, "Corrupt.dll");
        var manifest = CreateManifest("test.corrupt", corruptPath);

        var logger = new RecordingLevelLogger();
        var loader = new PluginAssemblyLoader(logger);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("test.corrupt", StringComparison.Ordinal));
    }

    // ----------------------------------------------------------------
    // Isolation does not affect sibling plugins - the batch continues
    // ----------------------------------------------------------------

    [Fact]
    public void LoadPlugins_SomeFail_OthersStillLoad()
    {
        using var temp = new TempDirectory();

        var missingManifest = CreateManifest("test.missing", Path.Combine(temp.Path, "Missing.dll"));

        var corruptPath = DynamicPluginAssemblyBuilder.WriteCorruptAssemblyFile(temp.Path, "Corrupt.dll");
        var corruptManifest = CreateManifest("test.corrupt", corruptPath);

        var validPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Valid.dll", "test.valid", "Valid Plugin", "1.0.0");
        var validManifest = CreateManifest("test.valid", validPath);

        var loader = new PluginAssemblyLoader();

        var result = loader.LoadPlugins([missingManifest, corruptManifest, validManifest]);

        var assembly = Assert.Single(result);
        Assert.StartsWith("Valid-", assembly.GetName().Name, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------
    // Assembly visibility to Module Discovery (Responsibilities Matrix,
    // Plugin Manifest Architecture.md - Module Discovery is unchanged and
    // requires no cooperation: AppDomain.CurrentDomain.GetAssemblies()
    // already sees an assembly loaded by any means).
    // ----------------------------------------------------------------

    [Fact]
    public void LoadPlugins_LoadedAssembly_IsVisibleToUnchangedModuleDiscovery()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Visible.dll", "test.visible", "Visible Plugin", "2.5.1");

        var manifest = CreateManifest("test.visible", assemblyPath);
        var loader = new PluginAssemblyLoader();

        var loadedAssemblies = loader.LoadPlugins([manifest]);
        var loadedAssembly = Assert.Single(loadedAssemblies);

        // The exact same, completely unchanged discovery service the Host
        // itself uses - scoped to just the newly-loaded assembly, proving
        // discovery finds the plugin's module without any plugin-aware
        // change to ReflectionFrameworkDiscoveryService.
        var discovery = new ReflectionFrameworkDiscoveryService([loadedAssembly]);
        var descriptors = discovery.DiscoverModules();

        var descriptor = Assert.Single(descriptors);
        Assert.Equal("test.visible", descriptor.Id);
        Assert.Equal("Visible Plugin", descriptor.Name);
        Assert.Equal("2.5.1", descriptor.Version);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static PluginManifest CreateManifest(string id, string assemblyPath) =>
        new(id, $"{id} name", "1.0.0", new Version(0, 1, 0), Path.GetFileName(assemblyPath), assemblyPath);
}
