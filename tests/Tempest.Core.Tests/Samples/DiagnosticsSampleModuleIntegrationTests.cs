using Tempest.Core.BackgroundServices;
using Tempest.Core.Commands;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Diagnostics;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Plugins;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 5.2 end-to-end: DiagnosticsSampleModule constructor-injects the
// real, unmodified IDiagnosticsProvider/ICommandDispatcher/ICommandRegistry
// and registers its command through them, driven entirely by the real,
// unmodified module pipeline - exactly the same composition
// CommandSampleModuleIntegrationTests already proves for the Command
// Framework. Nothing here is a mock or a test double standing in for a real
// platform service, except a level-recording ILogger used only to observe
// log output.
[Collection("Console output capture")]
public class DiagnosticsSampleModuleIntegrationTests
{
    // Mirrors TempestHost's own composition exactly: DiagnosticsProvider is
    // built with Func<T> accessors closing over locals that are not yet
    // assigned at registration time, then AddInstance-registered (ADR-0039)
    // - never a container-constructed singleton.
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider, Action<IModuleLifecycleManager> AttachLifecycleManager) BuildPipeline(
        params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(DiagnosticsSampleModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        IModuleLifecycleManager? lifecycleManager = null;

        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();
        services.AddInstance<IDiagnosticsProvider>(new DiagnosticsProvider(
            () => HostState.Running,
            () => lifecycleManager,
            () => null,
            new PluginRegistry()));
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider, manager => lifecycleManager = manager);
    }

    // ----------------------------------------------------------------
    // Constructor injection
    // ----------------------------------------------------------------

    [Fact]
    public void DiagnosticsSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        var (_, serviceProvider, _) = BuildPipeline(typeof(DiagnosticsSampleModule));

        var module = Assert.IsType<DiagnosticsSampleModule>(serviceProvider.GetService(typeof(DiagnosticsSampleModule)));

        Assert.NotNull(module);
    }

    // ----------------------------------------------------------------
    // Initialise-time observation, including the genuine, disclosed
    // "hosted services not yet available" finding.
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_ObservesLiveHostStateAndModuleCount_ThroughTheRealDiagnosticsProvider()
    {
        var (runtimeManager, serviceProvider, attachLifecycleManager) = BuildPipeline(typeof(DiagnosticsSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        attachLifecycleManager(lifecycleManager);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<DiagnosticsSampleModule>(serviceProvider.GetService(typeof(DiagnosticsSampleModule)));
        Assert.Equal(HostState.Running, module.ObservedHostStateDuringInitialise);
        Assert.Equal(1, module.ObservedModuleCountDuringInitialise);
        Assert.True(module.HasRegistered);
    }

    [Fact]
    public async Task Initialise_ObservesZeroHostedServices_BecauseHostedServiceManagerDoesNotExistYetAtThisPhase()
    {
        var (runtimeManager, serviceProvider, attachLifecycleManager) = BuildPipeline(typeof(DiagnosticsSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        attachLifecycleManager(lifecycleManager);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<DiagnosticsSampleModule>(serviceProvider.GetService(typeof(DiagnosticsSampleModule)));

        // This is not a bug: IHostedServiceManager is constructed only
        // after Module Initialisation completes (ADR-0029/ADR-0030's
        // frozen phase table), so it genuinely cannot exist yet here.
        Assert.Equal(0, module.ObservedHostedServiceCountDuringInitialise);
    }

    // ----------------------------------------------------------------
    // Command registration and invocation, through both the real
    // ICommandDispatcher and the real ICommandRegistry.
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersTheDiagnosticsSummaryCommandDescriptor()
    {
        var (runtimeManager, serviceProvider, attachLifecycleManager) = BuildPipeline(typeof(DiagnosticsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        attachLifecycleManager(lifecycleManager);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var item = Assert.Single(commandRegistry.Items);
        Assert.Equal(DiagnosticsSampleModule.GetDiagnosticsSummaryCommandId, item.Id);
    }

    [Fact]
    public async Task GetDiagnosticsSummaryCommand_InvokedThroughTheRegistry_SucceedsAndReportsLiveHostState()
    {
        var (runtimeManager, serviceProvider, attachLifecycleManager) = BuildPipeline(typeof(DiagnosticsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        attachLifecycleManager(lifecycleManager);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(
            DiagnosticsSampleModule.GetDiagnosticsSummaryCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Host: Running", result.Message);
        Assert.Contains("Modules: 1 tracked", result.Message);
    }

    [Fact]
    public async Task GetDiagnosticsSummaryCommand_DispatchedDirectly_Succeeds()
    {
        var (runtimeManager, serviceProvider, attachLifecycleManager) = BuildPipeline(typeof(DiagnosticsSampleModule));
        var commandDispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        attachLifecycleManager(lifecycleManager);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandDispatcher.DispatchAsync(new GetDiagnosticsSummaryCommand(), CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithDiagnosticsSampleModule_RegistersAndReportsThroughTheRealHost()
    {
        var host = new TempestHostBuilder([typeof(DiagnosticsSampleModule)]).Build();
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            Assert.Equal(HostState.Running, host.State);

            var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
            Assert.Equal(HostState.Running, diagnosticsProvider.HostState);
            Assert.NotEmpty(diagnosticsProvider.Modules);

            // By the time RunAsync has reached Running, Hosted Services
            // Started (Phase 10.1) has already completed, so - unlike
            // during the module's own Initialise - HostedServices now
            // legitimately reflects live data (empty here only because
            // this Host has no hosted services of its own).
            Assert.NotNull(diagnosticsProvider.HostedServices);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var result = await registry.InvokeAsync(
                DiagnosticsSampleModule.GetDiagnosticsSummaryCommandId, CancellationToken.None);
            Assert.True(result.Succeeded);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Stopped, host.State);
    }

    // ----------------------------------------------------------------
    // Plugin compatibility: IDiagnosticsProvider reports on a
    // plugin-loaded module's lifecycle state through the identical
    // ModuleLifecycleManager snapshot a built-in module's state is read
    // from - proving the Acceptance Criteria's "a consumer can query
    // every module's state" holds regardless of a module's origin, with
    // no plugin-specific Diagnostics mechanism of any kind.
    // ----------------------------------------------------------------

    [Fact]
    public async Task DiagnosticsProvider_ReportsAPluginLoadedModulesLifecycleState_ThroughTheSameSnapshotABuiltInModuleUses()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithCommandModule(
            temp.Path,
            "DiagnosticsPlugin.dll",
            "test.plugin.diagnostics",
            "Diagnostics Plugin",
            "1.0.0",
            "test.plugin.diagnostics.increment",
            "Plugin Increment");

        // ADR-0111: the dynamically-built module's constructor injects
        // ICommandDispatcher/ICommandRegistry - neither is in the fixed
        // always-allowed baseline (ILogger/IConfigurationProvider/
        // IDiagnosticsProvider), so this plugin must explicitly request
        // (and, at FirstParty tier, is eligible to be granted) a
        // plugin.services.resolve:* capability naming each.
        var manifest = new PluginManifest(
            "test.plugin.diagnostics", "Diagnostics Plugin", "1.0.0",
            new Version(0, 1, 0), Path.GetFileName(assemblyPath), assemblyPath,
            [],
            [
                PluginCapability.ServiceResolve(typeof(ICommandDispatcher).FullName!),
                PluginCapability.ServiceResolve(typeof(ICommandRegistry).FullName!),
            ],
            null, null, PluginTrustTier.FirstParty);

        var loader = new PluginAssemblyLoader();
        var loadedAssemblies = loader.LoadPlugins([manifest]);
        var loadedAssembly = Assert.Single(loadedAssemblies);

        var descriptors = new ReflectionFrameworkDiscoveryService([loadedAssembly]).DiscoverModules();
        var descriptor = Assert.Single(descriptors);

        var runtimeManager = new RuntimeModuleManager();
        runtimeManager.Register(descriptor);

        IModuleLifecycleManager? lifecycleManager = null;

        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();
        services.AddInstance<IDiagnosticsProvider>(new DiagnosticsProvider(
            () => HostState.Running, () => lifecycleManager, () => null, new PluginRegistry()));
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));
        var serviceProvider = new TempestServiceProvider(services);

        lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var diagnosticsProvider = (IDiagnosticsProvider)serviceProvider.GetService(typeof(IDiagnosticsProvider));
        var status = Assert.Single(diagnosticsProvider.Modules);
        Assert.Equal("test.plugin.diagnostics", status.Descriptor.Id);
        Assert.Equal(ModuleState.Initialised, status.State);
    }
}
