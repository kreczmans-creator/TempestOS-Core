using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Plugins;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// ADR-0111/ADR-0112, WP 13.2A: PluginRegistryState.TrustDenied (category 17)
// and the category-16 unsigned-load-not-allowed default, both observed
// end-to-end through a real TempestHost run and IDiagnosticsProvider.Plugins
// - the same DI-public projection a module would use. Neither path was
// exercised anywhere in the existing Runtime test suite.
[Collection("Console output capture")]
public class TempestHostPluginTrustTests
{
    [Fact]
    public async Task RunAsync_PluginModuleConstructorRequiresUngrantedService_IsRecordedTrustDenied_HostStillReachesRunning()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "trust-denied-plugin");
        Directory.CreateDirectory(pluginFolder);

        // Requires ICommandDispatcher/ICommandRegistry - neither in the fixed
        // always-allowed baseline - and the manifest below requests no
        // plugin.services.resolve:* capability for either.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithCommandModule(
            pluginFolder, "TrustDenied.dll", "host-test.trust-denied", "Trust Denied Plugin", "1.0.0",
            "host-test.command", "Host Test Command");

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "host-test.trust-denied",
              "Name": "Trust Denied Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}"
            }
            """);

        var builder = new TempestHostBuilder(Type.EmptyTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal("host-test.trust-denied", entry.Id);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.NotNull(entry.Detail);
        Assert.NotEmpty(entry.Detail!);

        // Distinguishable from the OTHER TrustDenied reason (a requested
        // capability outside the trust tier's ceiling, asserted below) -
        // this is the constructor-non-compliance reason specifically, and
        // an operator reading IDiagnosticsProvider.Plugins must be able to
        // tell the two apart from Detail text alone.
        Assert.Contains("constructor", entry.Detail!, StringComparison.OrdinalIgnoreCase);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_PluginRequestsCapabilityOutsideUnsignedLocalCeiling_IsRecordedTrustDenied_HostStillReachesRunning()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "ceiling-exceeded-plugin");
        Directory.CreateDirectory(pluginFolder);

        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            pluginFolder, "CeilingExceeded.dll", "host-test.ceiling-exceeded", "Ceiling Exceeded Plugin", "1.0.0");

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "host-test.ceiling-exceeded",
              "Name": "Ceiling Exceeded Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}",
              "RequestedCapabilities": [ "plugin.di.register" ]
            }
            """);

        var builder = new TempestHostBuilder(Type.EmptyTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);

        // Distinguishable from the OTHER TrustDenied reason (a
        // constructor-non-compliant module type, asserted above) - this is
        // the capability-ineligibility reason specifically, and names the
        // actual offending capability key.
        Assert.NotNull(entry.Detail);
        Assert.Contains("plugin.di.register", entry.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain("constructor", entry.Detail!, StringComparison.OrdinalIgnoreCase);

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task RunAsync_UnsignedPlugin_AllowUnsignedLoadNotConfigured_IsIsolated_HostStillReachesRunning()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "unsigned-plugin");
        Directory.CreateDirectory(pluginFolder);

        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            pluginFolder, "Unsigned.dll", "host-test.unsigned-default", "Unsigned Default Plugin", "1.0.0");

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "host-test.unsigned-default",
              "Name": "Unsigned Default Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}"
            }
            """);

        // Deliberately no Plugins:AllowUnsignedLoad configuration at all -
        // the safe, fail-closed default (ADR-0112, category 16).
        var host = new TempestHostBuilder(Type.EmptyTypes, temp.Path).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal("host-test.unsigned-default", entry.Id);
        Assert.NotEqual(PluginRegistryState.Loaded, entry.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }
}
