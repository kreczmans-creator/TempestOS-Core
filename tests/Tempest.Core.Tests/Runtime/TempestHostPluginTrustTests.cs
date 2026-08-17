using Tempest.Core.BackgroundServices;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Modules;
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

    // ------------------------------------------------------------------
    // WP 13.9.4 trust-denial execution boundary remediation. Before this
    // fix, PluginTrustDeniedException isolated a denied plugin only from
    // PluginAssemblyLoader.LoadPlugins's own returned list and
    // PluginRegistryState.Loaded - nothing stopped the already-loaded
    // assembly (ADR-0015: cannot be undone) from being separately
    // rediscovered by Module Discovery (deliberately plugin-unaware,
    // ADR-0110) and fully lifecycle-run (InitialiseAsync/StartAsync),
    // indistinguishable from first-party code. The three tests immediately
    // below are non-vacuous against that exact defect: each drives the
    // real, unmodified TempestHost pipeline end to end and asserts the
    // denied module never reaches Module Registration (absent from
    // IDiagnosticsProvider.Modules entirely) and never runs its own
    // InitialiseAsync body (its command never reaches ICommandRegistry.Items)
    // - not merely that PluginRegistryEntry.State reads TrustDenied, which
    // the three pre-existing tests above already covered and which remained
    // true even while the bypass was open. The fourth test proves the new
    // filter does not regress a genuinely passing plugin.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_ConstructorNonCompliantDeniedPlugin_ModuleNeverReachesRegistration_CommandNeverRegisters()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "boundary-ctor-denied-plugin");
        Directory.CreateDirectory(pluginFolder);

        const string moduleId = "wp1394.ctor-denied";
        const string commandId = "wp1394.ctor-denied-command";

        // Same shape as RunAsync_PluginModuleConstructorRequiresUngrantedService,
        // above: constructor requires ICommandDispatcher/ICommandRegistry,
        // neither granted, so EnforceTrust denies on constructor
        // non-compliance. This time the downstream effect is asserted, not
        // only the registry entry.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithCommandModule(
            pluginFolder, "CtorDenied.dll", moduleId, "Ctor Denied Plugin", "1.0.0",
            commandId, "Ctor Denied Command");

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "wp1394.ctor-denied-plugin",
              "Name": "Ctor Denied Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}"
            }
            """);

        // A candidate-type override, not Type.EmptyTypes, mirroring
        // PluginPlatformEndToEndTests.cs's own established pattern - Module
        // Discovery must genuinely be capable of finding this exact type
        // (proving the boundary below actually excludes something
        // reachable), not merely fed an empty candidate set that would
        // exclude it either way, independent of this fix. Safe to preload
        // here: this module type lives in the plugin's own primary,
        // manifest-declared assembly - DiscoverModuleTypes scans that
        // assembly's own types directly, regardless of whether it was
        // already resident before Plugin Loading began (only a
        // *transitively*-pulled-in secondary assembly depends on the
        // before/after diff seeing it as newly loaded - see the
        // transitive test, below, which deliberately does NOT preload for
        // exactly this reason).
        var moduleType = LoadPluginModuleType(assemblyPath);
        var builder = new TempestHostBuilder([moduleType], temp.Path);
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

        // The execution boundary: the denied module must never reach Module
        // Registration at all - not "registered but never started," entirely
        // absent, exactly as if it had never been discovered.
        Assert.DoesNotContain(diagnosticsProvider.Modules, m => m.Descriptor.Id == moduleId);

        // Its own InitialiseAsync body (the only place it registers its
        // command) must never have run.
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        Assert.DoesNotContain(commandRegistry.Items, d => d.Id == commandId);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_CapabilityCeilingExceededDeniedPlugin_ModuleNeverReachesRegistration_CommandNeverRegisters()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "boundary-ceiling-denied-plugin");
        Directory.CreateDirectory(pluginFolder);

        const string moduleId = "wp1394.ceiling-denied";
        const string commandId = "wp1394.ceiling-denied-command";

        // This denial reason is the more important of the two to prove here:
        // before WP 13.9.4's EnforceTrust reordering, FindIneligibleCapability
        // threw before DiscoverModuleTypes ever ran for this exact case, so
        // NO module Type or transitive assembly was ever identified for a
        // capability-denied plugin - there was no data anywhere a filter
        // could have keyed on, even in principle.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithCommandModule(
            pluginFolder, "CeilingDenied.dll", moduleId, "Ceiling Denied Plugin", "1.0.0",
            commandId, "Ceiling Denied Command");

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "wp1394.ceiling-denied-plugin",
              "Name": "Ceiling Denied Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}",
              "RequestedCapabilities": [ "plugin.di.register" ]
            }
            """);

        var moduleType = LoadPluginModuleType(assemblyPath);
        var builder = new TempestHostBuilder([moduleType], temp.Path);
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
        Assert.Contains("plugin.di.register", entry.Detail!, StringComparison.Ordinal);

        Assert.DoesNotContain(diagnosticsProvider.Modules, m => m.Descriptor.Id == moduleId);

        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        Assert.DoesNotContain(commandRegistry.Items, d => d.Id == commandId);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_CapabilityCeilingExceededDeniedPlugin_DualModuleAndHostedServiceType_ExcludedFromBothRegistrationPipelines()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "boundary-dual-denied-plugin");
        Directory.CreateDirectory(pluginFolder);

        const string typeId = "wp1394.dual-denied";

        // WP 13.9.4's own Adversarial Review found this the most severe
        // variant: a single Type implementing BOTH IModule and
        // IHostedService, correctly excluded from Module Registration by
        // the first pass of this fix, yet still fully reachable through the
        // wholly independent Hosted Service discovery/registration pipeline
        // (HostedServiceDiscoveryService/IHostedServiceManager) - neither
        // ReflectionFrameworkDiscoveryService/RuntimeModuleManager nor
        // HostedServiceDiscoveryService/IHostedServiceManager is trust-aware,
        // and each is a wholly separate pipeline from Module Discovery/
        // Registration. A wholly compliant, parameterless constructor, so
        // denial comes purely from the capability-ceiling check - the exact
        // path that previously recorded ZERO discovered-type data at all,
        // for either interface.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildDualModuleAndHostedServiceAssembly(
            pluginFolder, "DualDenied.dll", typeId, "Dual Denied Type", "1.0.0");

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "wp1394.dual-denied-plugin",
              "Name": "Dual Denied Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}",
              "RequestedCapabilities": [ "plugin.di.register" ]
            }
            """);

        // Both discovery candidate overrides must include this exact Type -
        // Module Discovery AND Hosted Service Discovery each need to
        // genuinely be capable of finding it, proving the boundary excludes
        // something reachable through both pipelines, not merely fed empty
        // candidate sets that would exclude it either way.
        var dualType = LoadPluginModuleType(assemblyPath);
        var builder = new TempestHostBuilder([dualType], temp.Path, [dualType]);
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
        Assert.Contains("plugin.di.register", entry.Detail!, StringComparison.Ordinal);

        // Excluded from Module Registration (the WP 13.9.4 boundary's first,
        // original scope).
        Assert.DoesNotContain(diagnosticsProvider.Modules, m => m.Descriptor.Id == typeId);

        // Excluded from Hosted Service Registration too (the sibling
        // pipeline WP 13.9.4's own Adversarial Review found still open) -
        // keyed on ServiceType, since HostedServiceStatus carries no IModule
        // Id at all.
        Assert.DoesNotContain(diagnosticsProvider.HostedServices, h => h.ServiceType == dualType);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    // The transitive, secondary-assembly variant of this scenario is proven
    // at the PluginAssemblyLoader level instead of here -
    // PluginAssemblyLoaderMultiAssemblyTrustTests.cs's own
    // EnforceTrust_SecondUndeclaredAssembly_EvilModuleWithForbiddenConstructor_IsDenied_RecordsDeniedModuleType
    // - since a real, unrestricted AppDomain-wide Module Discovery scan
    // (the only way to let a transitively, lazily-loaded secondary assembly
    // be found without defeating DiscoverModuleTypes's own before/after
    // diff by preloading it) is not safe to drive from this shared test
    // process: it also discovers every other test class's own
    // dynamically-built IModule fixtures, several of which are deliberately
    // malformed for unrelated scenarios and fault the host. That test
    // proves DiscoverModuleTypes/PluginDeniedTypeRegistry correctly
    // attribute and record the transitive type; the tests above prove
    // TempestHost's own Module Registration filter correctly excludes
    // whatever this registry records - together, the same guarantee this
    // file's other tests prove end-to-end for the single-assembly case.

    [Fact]
    public async Task RunAsync_LegitimatePluginWithNoRequestedCapabilities_ModuleReachesRunning_NotExcludedByExecutionBoundaryFilter()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "boundary-legitimate-plugin");
        Directory.CreateDirectory(pluginFolder);

        const string moduleId = "wp1394.legitimate";

        // A wholly compliant module (baseline-only constructor, zero
        // requested capabilities) - proves the new Module Registration
        // filter (deniedModuleTypeRegistry.IsDenied) does not accidentally
        // exclude a passing plugin's own module. Non-vacuous the other
        // direction: this test would fail if the filter were ever inverted
        // or over-broadened.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            pluginFolder, "Legitimate.dll", moduleId, "Legitimate Plugin", "1.0.0");

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "wp1394.legitimate-plugin",
              "Name": "Legitimate Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}"
            }
            """);

        var moduleType = LoadPluginModuleType(assemblyPath);
        var builder = new TempestHostBuilder([moduleType], temp.Path);
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
        Assert.Equal(PluginRegistryState.Loaded, entry.State);

        var moduleStatus = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == moduleId);
        Assert.Equal(ModuleState.Running, moduleStatus.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    // ------------------------------------------------------------------
    // WP 13.9.6 Module Discovery Trust Boundary Remediation. Before this
    // fix, ReflectionFrameworkDiscoveryService.CreateDescriptor called
    // Activator.CreateInstance(type) for ANY unattributed IModule type
    // during Module Discovery - unconditionally, including a denied
    // plugin's own module type, and strictly before either WP 13.9.4 filter
    // above is ever consulted. The three tests below are non-vacuous
    // against that exact defect: each proves a denied plugin's own
    // unattributed module constructor never actually executes (via
    // ConstructorExecutionProbe, an observable side effect independent of
    // registration state), not merely that the type is later excluded from
    // Module Registration (already covered above). The second test also
    // proves the more severe, related consequence is closed: a denied,
    // unattributed module with NO public parameterless constructor
    // previously threw an uncaught ModuleDiscoveryException inside the
    // Discovery loop, faulting the whole Host.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CapabilityCeilingExceededDeniedPlugin_UnattributedProbeModule_ConstructorNeverRuns()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "boundary-discovery-ceiling-denied-plugin");
        Directory.CreateDirectory(pluginFolder);

        const string moduleId = "wp1396.ceiling-denied-probe";
        var probeId = Guid.NewGuid().ToString("N");

        // No [ModuleMetadataAttribute] - CreateDescriptor would previously
        // have to construct this type via Activator.CreateInstance to read
        // its Id/Name/Version, purely to build the discovery descriptor,
        // regardless of the plugin's own trust outcome.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildUnattributedPluginModuleWithConstructorProbe(
            pluginFolder, "CeilingDeniedProbe.dll", moduleId, "Ceiling Denied Probe Plugin", "1.0.0", probeId);

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "wp1396.ceiling-denied-probe-plugin",
              "Name": "Ceiling Denied Probe Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}",
              "RequestedCapabilities": [ "plugin.di.register" ]
            }
            """);

        var moduleType = LoadPluginModuleType(assemblyPath);
        var builder = new TempestHostBuilder([moduleType], temp.Path);
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

        Assert.DoesNotContain(diagnosticsProvider.Modules, m => m.Descriptor.Id == moduleId);

        // The load-bearing assertion: the module's own constructor - and
        // therefore Activator.CreateInstance - never ran at all, not merely
        // that its descriptor never reached Module Registration.
        Assert.Equal(0, ConstructorExecutionProbe.GetInvocationCount(probeId));

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_ConstructorNonCompliantDeniedPlugin_UnattributedModuleWithNoParameterlessConstructor_NeverConstructedAndHostStaysRunning()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "boundary-discovery-ctor-denied-plugin");
        Directory.CreateDirectory(pluginFolder);

        const string moduleId = "wp1396.ctor-denied-probe";

        // Deliberately NOT BuildUnattributedPluginModuleWithConstructorProbe:
        // HasCompliantConstructor accepts a type if ANY of its public
        // constructors is compliant, and a parameterless constructor is
        // always trivially compliant - so a type that ALSO has a
        // probe-calling parameterless constructor would be ACCEPTED, not
        // denied, defeating this exact scenario (see that builder's own
        // remarks). The correct shape for a genuinely constructor-
        // non-compliant, unattributed module is
        // BuildPluginAssemblyWithConstructorParameters's own existing
        // output: a type with NO parameterless constructor at all (so it
        // can never be compliant-by-default) and no [ModuleMetadataAttribute]
        // (so CreateDescriptor would have to construct it). Before this
        // fix, CreateDescriptor's own explicit "no parameterless
        // constructor" guard would throw ModuleDiscoveryException the
        // moment Module Discovery reached this type - uncaught inside the
        // Discovery loop, Host-fatal. This test proves that guard is never
        // even reached: the type is excluded before CreateDescriptor is
        // ever called for it, so no exception of any kind is thrown, and
        // the Host reaches Running normally, not Faulted.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildPluginAssemblyWithConstructorParameters(
            pluginFolder, "CtorDeniedProbe.dll", moduleId, "Ctor Denied Probe Plugin", "1.0.0",
            [typeof(ICommandDispatcher), typeof(ICommandRegistry)]);

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "wp1396.ctor-denied-probe-plugin",
              "Name": "Ctor Denied Probe Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}"
            }
            """);

        var moduleType = LoadPluginModuleType(assemblyPath);
        var builder = new TempestHostBuilder([moduleType], temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        // Not Faulted - the previously-uncaught ModuleDiscoveryException/
        // Host-fatal crash this fix also closes.
        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.Contains("constructor", entry.Detail!, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(diagnosticsProvider.Modules, m => m.Descriptor.Id == moduleId);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_LegitimateUnattributedProbeModule_NoRequestedCapabilities_ConstructorRunsExactlyOnce()
    {
        using var temp = new TempDirectory();
        var pluginFolder = Path.Combine(temp.Path, "boundary-discovery-legitimate-plugin");
        Directory.CreateDirectory(pluginFolder);

        const string moduleId = "wp1396.legitimate-probe";
        var probeId = Guid.NewGuid().ToString("N");

        // A wholly compliant, unattributed module (baseline-only
        // constructor, zero requested capabilities) - proves the new
        // isTypeExcluded predicate does not accidentally exclude, or
        // otherwise interfere with, a genuinely passing plugin's own
        // unattributed module construction.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildUnattributedPluginModuleWithConstructorProbe(
            pluginFolder, "LegitimateProbe.dll", moduleId, "Legitimate Probe Plugin", "1.0.0", probeId);

        File.WriteAllText(
            Path.Combine(pluginFolder, PluginManifestDiscoveryService.ManifestFileName),
            $$"""
            {
              "Id": "wp1396.legitimate-probe-plugin",
              "Name": "Legitimate Probe Plugin",
              "Version": "1.0.0",
              "MinimumPlatformVersion": "0.1.0",
              "AssemblyFileName": "{{Path.GetFileName(assemblyPath)}}"
            }
            """);

        var moduleType = LoadPluginModuleType(assemblyPath);
        var builder = new TempestHostBuilder([moduleType], temp.Path);
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
        Assert.Equal(PluginRegistryState.Loaded, entry.State);

        var moduleStatus = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == moduleId);
        Assert.Equal(ModuleState.Running, moduleStatus.State);

        // Proves the fix does NOT over-exclude legitimate construction:
        // Module Discovery's own CreateDescriptor still constructed this
        // type exactly once, reading its Id/Name/Version, exactly as it
        // always has for an unattributed, trusted module.
        Assert.Equal(1, ConstructorExecutionProbe.GetInvocationCount(probeId));

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    /// <summary>
    /// Loads <paramref name="assemblyPath"/> and returns its sole concrete
    /// <see cref="IModule"/> implementer - mirrors
    /// <c>PluginPlatformEndToEndTests.LoadPluginModuleType</c>'s own
    /// identical helper exactly, so a specific dynamically-built plugin
    /// module's <see cref="Type"/> can be fed to <see cref="TempestHostBuilder"/>'s
    /// own discovery-candidate-types test seam, isolated from every other
    /// <see cref="IModule"/> fixture defined elsewhere in this test
    /// assembly.
    /// </summary>
    private static Type LoadPluginModuleType(string assemblyPath) =>
        System.Reflection.Assembly.LoadFrom(assemblyPath).GetTypes()
            .Single(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
}
