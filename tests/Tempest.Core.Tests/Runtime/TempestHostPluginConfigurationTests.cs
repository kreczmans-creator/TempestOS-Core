using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Plugins;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// WP 13.1A / Plugin Platform Architecture.md, "Configurable Plugins Root and
// Manifest Convention": Runtime:Plugins:RootDirectory/ManifestFileName/
// Disabled, resolved by TempestHost itself from configuration, end-to-end
// through the real Host via ITempestHostBuilder.AddConfigurationSource.
[Collection("Console output capture")]
public class TempestHostPluginConfigurationTests
{
    [Fact]
    public async Task RunAsync_RuntimePluginsRootDirectoryConfigured_IsHonouredWhenNoTestOnlyOverrideGiven()
    {
        using var temp = new TempDirectory();
        var candidateFolder = Path.Combine(temp.Path, "only-plugin");
        Directory.CreateDirectory(candidateFolder);

        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            candidateFolder, "Configured.dll", "test.configured-root", "Configured Root Plugin", "1.0.0");
        File.WriteAllText(
            Path.Combine(candidateFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: "test.configured-root", name: "Configured Root Plugin", assemblyFileName: Path.GetFileName(assemblyPath)));

        // No pluginsRootPathOverride (the 1-arg internal ctor leaves it
        // null) - the only way this plugin can be found is via configuration.
        var builder = new TempestHostBuilder(Type.EmptyTypes);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Runtime:Plugins:RootDirectory", temp.Path),
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));

        await using var host = builder.Build();
        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal("test.configured-root", entry.Id);
        Assert.Equal(PluginRegistryState.Loaded, entry.State);

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task RunAsync_TestOnlyPluginsRootPathOverride_TakesPrecedenceOverConfiguredRootDirectory()
    {
        using var overrideRoot = new TempDirectory();
        var overrideCandidateFolder = Path.Combine(overrideRoot.Path, "only-plugin");
        Directory.CreateDirectory(overrideCandidateFolder);
        var overrideAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            overrideCandidateFolder, "Override.dll", "test.override-root", "Override Root Plugin", "1.0.0");
        File.WriteAllText(
            Path.Combine(overrideCandidateFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: "test.override-root", name: "Override Root Plugin", assemblyFileName: Path.GetFileName(overrideAssembly)));

        using var configuredRoot = new TempDirectory();
        var configuredCandidateFolder = Path.Combine(configuredRoot.Path, "only-plugin");
        Directory.CreateDirectory(configuredCandidateFolder);
        var configuredAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            configuredCandidateFolder, "Configured.dll", "test.configured-root", "Configured Root Plugin", "1.0.0");
        File.WriteAllText(
            Path.Combine(configuredCandidateFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: "test.configured-root", name: "Configured Root Plugin", assemblyFileName: Path.GetFileName(configuredAssembly)));

        // pluginsRootPathOverride is explicitly overrideRoot.Path here - per
        // TempestHost's own documented precedence, this must win over the
        // configured Runtime:Plugins:RootDirectory below.
        var builder = new TempestHostBuilder(Type.EmptyTypes, overrideRoot.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Runtime:Plugins:RootDirectory", configuredRoot.Path),
        ]));

        await using var host = builder.Build();
        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal("test.override-root", entry.Id);

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task RunAsync_RuntimePluginsManifestFileNameConfigured_DiscoversCustomNamedManifest_IgnoresDefaultNamedManifest()
    {
        using var temp = new TempDirectory();

        const string customManifestFileName = "custom.manifest.json";

        var foundFolder = Path.Combine(temp.Path, "a-found-plugin");
        Directory.CreateDirectory(foundFolder);
        var foundAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            foundFolder, "Found.dll", "test.found-via-custom-name", "Found Plugin", "1.0.0");
        File.WriteAllText(
            Path.Combine(foundFolder, customManifestFileName),
            PluginManifestJsonBuilder.Build(id: "test.found-via-custom-name", name: "Found Plugin", assemblyFileName: Path.GetFileName(foundAssembly)));

        var ignoredFolder = Path.Combine(temp.Path, "b-ignored-plugin");
        Directory.CreateDirectory(ignoredFolder);

        // Written under the old default file name - with a custom name
        // configured, Plugin Discovery must never look for this file.
        File.WriteAllText(
            Path.Combine(ignoredFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: "test.ignored-default-name", name: "Ignored Plugin", assemblyFileName: "Ignored.dll"));

        var builder = new TempestHostBuilder(Type.EmptyTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Runtime:Plugins:ManifestFileName", customManifestFileName),
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));

        await using var host = builder.Build();
        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal("test.found-via-custom-name", entry.Id);
        Assert.Equal(PluginRegistryState.Loaded, entry.State);

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task RunAsync_RuntimePluginsDisabledConfigured_CommaSeparatedTrimmedEmptyEntriesRemoved_DisablesOnlyMatchingPlugins()
    {
        using var temp = new TempDirectory();

        var disabledFolder = Path.Combine(temp.Path, "a-disabled-plugin");
        Directory.CreateDirectory(disabledFolder);
        File.WriteAllText(
            Path.Combine(disabledFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: "test.disabled-via-config", name: "Disabled Plugin", assemblyFileName: "Disabled.dll"));

        var enabledFolder = Path.Combine(temp.Path, "b-enabled-plugin");
        Directory.CreateDirectory(enabledFolder);
        var enabledAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            enabledFolder, "Enabled.dll", "test.enabled-via-config", "Enabled Plugin", "1.0.0");
        File.WriteAllText(
            Path.Combine(enabledFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: "test.enabled-via-config", name: "Enabled Plugin", assemblyFileName: Path.GetFileName(enabledAssembly)));

        var builder = new TempestHostBuilder(Type.EmptyTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            // Deliberately messy: surrounding whitespace and an empty entry,
            // both of which Runtime:Plugins:Disabled's own parsing must
            // tolerate (StringSplitOptions.TrimEntries | RemoveEmptyEntries).
            new KeyValuePair<string, string>("Runtime:Plugins:Disabled", "  test.disabled-via-config ,, "),
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));

        await using var host = builder.Build();
        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

        var disabledEntry = diagnosticsProvider.Plugins.Single(e => e.Id == "test.disabled-via-config");
        Assert.Equal(PluginRegistryState.Disabled, disabledEntry.State);

        var enabledEntry = diagnosticsProvider.Plugins.Single(e => e.Id == "test.enabled-via-config");
        Assert.Equal(PluginRegistryState.Loaded, enabledEntry.State);

        await host.StopAsync();
        await runTask;
    }

    // ----------------------------------------------------------------
    // Blank configured values - WP 13.1B regression (Code Review finding
    // 2): a present-but-empty/whitespace Runtime:Plugins:RootDirectory or
    // ManifestFileName must fall back to the default convention, not be
    // treated as a literal override - the "??" precedence chain only
    // substitutes on null, so an unguarded empty string previously reached
    // PluginManifestDiscoveryService's own ArgumentException.
    // ThrowIfNullOrWhiteSpace guard, uncaught, faulting the entire Host
    // for what should be a no-op plugin-configuration mistake.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_RuntimePluginsRootDirectoryConfiguredAsEmptyString_FallsBackToDefault_HostStillReachesRunning()
    {
        using var temp = new TempDirectory();

        // No pluginsRootPathOverride, and the configured value is blank -
        // the only legitimate behaviour is falling back to the
        // AppContext.BaseDirectory-relative default convention, which in a
        // test process almost certainly has no "Plugins" folder - a
        // zero-plugin run, not a faulted Host.
        var builder = new TempestHostBuilder(Type.EmptyTypes);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Runtime:Plugins:RootDirectory", ""),
        ]));

        await using var host = builder.Build();
        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        // The defect this regresses: the Host used to reach Faulted here,
        // not Running - a blank plugin-configuration value must never take
        // down the whole platform.
        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task RunAsync_RuntimePluginsManifestFileNameConfiguredAsWhitespace_FallsBackToDefault_DiscoversDefaultNamedManifest()
    {
        using var temp = new TempDirectory();

        var candidateFolder = Path.Combine(temp.Path, "only-plugin");
        Directory.CreateDirectory(candidateFolder);
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            candidateFolder, "Default.dll", "test.default-manifest-name", "Default Manifest Name Plugin", "1.0.0");

        // Written under the real default file name - a whitespace-only
        // configured override must fall back to this exact name, not to a
        // literal "   " filename that would never match anything on disk.
        File.WriteAllText(
            Path.Combine(candidateFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginManifestJsonBuilder.Build(id: "test.default-manifest-name", name: "Default Manifest Name Plugin", assemblyFileName: Path.GetFileName(assemblyPath)));

        var builder = new TempestHostBuilder(Type.EmptyTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Runtime:Plugins:ManifestFileName", "   "),
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));

        await using var host = builder.Build();
        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal("test.default-manifest-name", entry.Id);
        Assert.Equal(PluginRegistryState.Loaded, entry.State);

        await host.StopAsync();
        await runTask;
    }
}
