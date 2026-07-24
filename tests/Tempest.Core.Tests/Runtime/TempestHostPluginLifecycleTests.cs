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
}
