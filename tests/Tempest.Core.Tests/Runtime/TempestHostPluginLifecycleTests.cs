using Tempest.Core.Diagnostics;
using Tempest.Core.Plugins;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Host-level integration tests for ADR-0026's Plugin Discovery (3.1) / Plugin
// Loading (3.2) phases. discoveryCandidateTypesOverride is always fixed here
// (Type.EmptyTypes) so Module Discovery does not perform a real AppDomain
// scan - the test assembly contains many internal-visibility-only IModule
// fixtures (see HostTestFixtures.cs, ModuleFixtures.cs) that
// ReflectionFrameworkDiscoveryService, running from a different assembly
// with no InternalsVisibleTo back into the test assembly, cannot construct
// via reflection; a real scan would fault on those, unrelated to plugins
// entirely. "Assembly visibility to Module Discovery" is proven precisely,
// without that hazard, in PluginAssemblyLoaderTests instead.
[Collection("Console output capture")]
public class TempestHostPluginLifecycleTests
{
    [Fact]
    public async Task RunAsync_LogsPluginDiscoveryAndLoadingBeforeModuleDiscovery()
    {
        using var temp = new TempDirectory();

        var host = new TempestHostBuilder(Type.EmptyTypes, temp.Path).Build();
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            var runTask = host.RunAsync();
            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();

        var pluginDiscoveryIndex = output.IndexOf("Host lifecycle phase completed: Plugin Discovery", StringComparison.Ordinal);
        var pluginLoadingIndex = output.IndexOf("Host lifecycle phase completed: Plugin Loading", StringComparison.Ordinal);
        var moduleDiscoveryIndex = output.IndexOf("Host lifecycle phase completed: Module Discovery", StringComparison.Ordinal);

        Assert.True(pluginDiscoveryIndex >= 0, "Plugin Discovery phase was not logged.");
        Assert.True(pluginLoadingIndex > pluginDiscoveryIndex, "Plugin Loading did not follow Plugin Discovery.");
        Assert.True(moduleDiscoveryIndex > pluginLoadingIndex, "Module Discovery did not follow Plugin Loading.");

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_WithNoPluginsDirectory_StillReachesRunning()
    {
        using var temp = new TempDirectory();
        var missingPluginsRoot = Path.Combine(temp.Path, "does-not-exist");

        var host = new TempestHostBuilder(Type.EmptyTypes, missingPluginsRoot).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_IsolatedPluginFailure_DoesNotFaultTheHost_StartupContinues()
    {
        using var temp = new TempDirectory();
        var brokenPluginFolder = Path.Combine(temp.Path, "broken-plugin");
        Directory.CreateDirectory(brokenPluginFolder);
        File.WriteAllText(
            Path.Combine(brokenPluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            "{ not valid json");

        var host = new TempestHostBuilder(Type.EmptyTypes, temp.Path).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_MultipleIsolatedPluginFailures_AllIsolated_HostStillReachesRunning()
    {
        using var temp = new TempDirectory();

        var malformed = Path.Combine(temp.Path, "a-malformed-plugin");
        Directory.CreateDirectory(malformed);
        File.WriteAllText(Path.Combine(malformed, PluginManifestDiscoveryService.ManifestFileName), "{ not valid json");

        var missingAssembly = Path.Combine(temp.Path, "b-missing-assembly-plugin");
        Directory.CreateDirectory(missingAssembly);
        File.WriteAllText(
            Path.Combine(missingAssembly, PluginManifestDiscoveryService.ManifestFileName),
            """
            {
              "Id": "host-test.missing-assembly",
              "Name": "Missing Assembly Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "DoesNotExist.dll"
            }
            """);

        var host = new TempestHostBuilder(Type.EmptyTypes, temp.Path).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    // ----------------------------------------------------------------
    // IDiagnosticsProvider.Plugins end-to-end (WP 13.1A / ADR-0108): a real
    // TempestHost run's own Plugin Registry, observed through the same
    // DI-public projection a module would use.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_ValidPlugin_ReportsLoadedInDiagnosticsProviderPlugins_WithCorrectIdNameVersion()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "valid-plugin");
        Directory.CreateDirectory(pluginFolder);

        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            pluginFolder, "Valid.dll", "host-test.valid", "Valid Diagnostics Plugin", "3.2.1");
        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "host-test.valid",
              "Name": "Valid Diagnostics Plugin",
              "Version": "3.2.1",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}"
            }
            """);

        var host = new TempestHostBuilder(Type.EmptyTypes, temp.Path).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal("host-test.valid", entry.Id);
        Assert.Equal("Valid Diagnostics Plugin", entry.Name);
        Assert.Equal("3.2.1", entry.Version);
        Assert.Equal(PluginRegistryState.Loaded, entry.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_IsolatedPlugin_ReportsCorrectStateAndDetailInDiagnosticsProviderPlugins_HostStillReachesRunning()
    {
        using var temp = new TempDirectory();
        var brokenPluginFolder = Path.Combine(temp.Path, "broken-plugin");
        Directory.CreateDirectory(brokenPluginFolder);
        File.WriteAllText(
            Path.Combine(brokenPluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            "{ not valid json");

        var host = new TempestHostBuilder(Type.EmptyTypes, temp.Path).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal(PluginRegistryState.Failed, entry.State);
        Assert.NotNull(entry.Detail);
        Assert.NotEmpty(entry.Detail!);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    // ----------------------------------------------------------------
    // Failure isolation at Host level for the new ADR-0107 categories -
    // built from real candidate folders, mirroring
    // RunAsync_IsolatedPluginFailure_DoesNotFaultTheHost_StartupContinues's
    // own shape exactly, for missing-dependency and circular-dependency.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_MissingDependencyPlugin_IsIsolated_HostStillReachesRunning_ReportsDependencyUnmet()
    {
        using var temp = new TempDirectory();

        var dependentFolder = Path.Combine(temp.Path, "a-dependent-plugin");
        Directory.CreateDirectory(dependentFolder);
        File.WriteAllText(
            Path.Combine(dependentFolder, PluginManifestDiscoveryService.ManifestFileName),
            """
            {
              "Id": "host-test.dependent",
              "Name": "Dependent Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "Dependent.dll",
              "Dependencies": [ { "Id": "host-test.does-not-exist", "MinimumVersion": "1.0.0" } ]
            }
            """);

        var siblingFolder = Path.Combine(temp.Path, "b-sibling-plugin");
        Directory.CreateDirectory(siblingFolder);
        var siblingAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            siblingFolder, "Sibling.dll", "host-test.sibling", "Sibling Plugin", "1.0.0");
        File.WriteAllText(
            Path.Combine(siblingFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "host-test.sibling",
              "Name": "Sibling Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(siblingAssembly)}}"
            }
            """);

        var host = new TempestHostBuilder(Type.EmptyTypes, temp.Path).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

        var dependentEntry = diagnosticsProvider.Plugins.Single(e => e.Id == "host-test.dependent");
        Assert.Equal(PluginRegistryState.DependencyUnmet, dependentEntry.State);

        var siblingEntry = diagnosticsProvider.Plugins.Single(e => e.Id == "host-test.sibling");
        Assert.Equal(PluginRegistryState.Loaded, siblingEntry.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_CircularDependencyPlugins_AreIsolated_HostStillReachesRunning_ReportsDependencyUnmet()
    {
        using var temp = new TempDirectory();

        var aFolder = Path.Combine(temp.Path, "a-cycle-plugin");
        Directory.CreateDirectory(aFolder);
        File.WriteAllText(
            Path.Combine(aFolder, PluginManifestDiscoveryService.ManifestFileName),
            """
            {
              "Id": "host-test.cycle-a",
              "Name": "Cycle Plugin A",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "A.dll",
              "Dependencies": [ { "Id": "host-test.cycle-b", "MinimumVersion": "1.0.0" } ]
            }
            """);

        var bFolder = Path.Combine(temp.Path, "b-cycle-plugin");
        Directory.CreateDirectory(bFolder);
        File.WriteAllText(
            Path.Combine(bFolder, PluginManifestDiscoveryService.ManifestFileName),
            """
            {
              "Id": "host-test.cycle-b",
              "Name": "Cycle Plugin B",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "B.dll",
              "Dependencies": [ { "Id": "host-test.cycle-a", "MinimumVersion": "1.0.0" } ]
            }
            """);

        var host = new TempestHostBuilder(Type.EmptyTypes, temp.Path).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

        var aEntry = diagnosticsProvider.Plugins.Single(e => e.Id == "host-test.cycle-a");
        Assert.Equal(PluginRegistryState.DependencyUnmet, aEntry.State);

        var bEntry = diagnosticsProvider.Plugins.Single(e => e.Id == "host-test.cycle-b");
        Assert.Equal(PluginRegistryState.DependencyUnmet, bEntry.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }
}
