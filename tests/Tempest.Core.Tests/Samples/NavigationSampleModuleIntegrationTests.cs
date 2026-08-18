using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Plugins;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Events;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;
using Tempest.Validation.FaultInjection;

namespace Tempest.Core.Tests.Samples;

// Proves WP 5.0B end-to-end: NavigationSampleModule (and its companion
// fixtures) constructor-inject the real, unmodified INavigationProvider
// (WP 5.0B) and register/unregister real NavigationItems through it, driven
// entirely by the real, unmodified module pipeline (Discovery, Registration,
// Dependency Injection, ModuleLifecycleManager) - exactly the same
// composition ClockModuleEventIntegrationTests already proves for the Event
// Bus. Nothing here is a mock or a test double standing in for a real
// platform service, except a level-recording ILogger used only to observe
// log output.
//
// Shares the "Console output capture" collection with every other test class
// that redirects Console.Out, for the same reason
// ClockModuleEventIntegrationTests does.
[Collection("Console output capture")]
public class NavigationSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        params Type[] moduleTypes)
    {
        // includeFaultInjectionModules: true - several tests below pass
        // DuplicateNavigationModule (Tempest.Validation.FaultInjection)
        // explicitly as a candidate type; ADR-0102's default-exclusion
        // filter would otherwise silently drop it even from an explicit
        // candidate list naming it directly.
        var descriptors = new ReflectionFrameworkDiscoveryService(
                [typeof(NavigationSampleModule).Assembly], includeFaultInjectionModules: true)
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection of INavigationProvider
    // ----------------------------------------------------------------

    [Fact]
    public void NavigationSampleModule_ResolvedThroughRealPipeline_ReceivesAFunctioningNavigationProvider()
    {
        var (_, serviceProvider) = BuildPipeline(typeof(NavigationSampleModule));

        var module = Assert.IsType<NavigationSampleModule>(serviceProvider.GetService(typeof(NavigationSampleModule)));

        // No exception resolving it proves the constructor-injected
        // INavigationProvider was supplied; that it is genuinely functioning
        // is proven by the registration tests below.
        Assert.NotNull(module);
    }

    // ----------------------------------------------------------------
    // Lifecycle: Initialise -> registration -> Running -> Dispose -> removal
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersTheModulesNavigationItem()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(NavigationSampleModule));
        var navigationProvider = (INavigationProvider)serviceProvider.GetService(typeof(INavigationProvider));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var item = Assert.Single(navigationProvider.Items);
        Assert.Equal(NavigationSampleModule.NavigationItemId, item.Id);
    }

    [Fact]
    public async Task FullLifecycle_InitialiseThroughDispose_NoOrphanedNavigationEntryRemainsAfterDisposal()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(NavigationSampleModule));
        var navigationProvider = (INavigationProvider)serviceProvider.GetService(typeof(INavigationProvider));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        Assert.Single(navigationProvider.Items);

        await lifecycleManager.StartAllAsync(CancellationToken.None);
        Assert.Single(navigationProvider.Items);

        await lifecycleManager.StopAllAsync(CancellationToken.None);
        Assert.Single(navigationProvider.Items);

        await lifecycleManager.DisposeAllAsync(CancellationToken.None);
        Assert.Empty(navigationProvider.Items);

        var module = Assert.IsType<NavigationSampleModule>(
            serviceProvider.GetService(typeof(NavigationSampleModule)));
        Assert.False(module.HasRegistered);
    }

    [Fact]
    public async Task FullLifecycle_RepeatedAcrossFreshInstances_ConsistentlyLeavesNoOrphanedEntry()
    {
        for (var i = 0; i < 3; i++)
        {
            var (runtimeManager, serviceProvider) = BuildPipeline(typeof(NavigationSampleModule));
            var navigationProvider = (INavigationProvider)serviceProvider.GetService(typeof(INavigationProvider));

            var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
            await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
            await lifecycleManager.StartAllAsync(CancellationToken.None);
            await lifecycleManager.StopAllAsync(CancellationToken.None);

            Assert.Single(navigationProvider.Items);

            await lifecycleManager.DisposeAllAsync(CancellationToken.None);

            Assert.Empty(navigationProvider.Items);
        }
    }

    // ----------------------------------------------------------------
    // Multiple, independent modules contributing navigation
    // ----------------------------------------------------------------

    [Fact]
    public async Task MultipleModules_EachContributesItsOwnItem_WithoutCollision()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(
            typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var navigationProvider = (INavigationProvider)serviceProvider.GetService(typeof(INavigationProvider));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(2, navigationProvider.Items.Count);
        Assert.Contains(navigationProvider.Items, item => item.Id == NavigationSampleModule.NavigationItemId);
        Assert.Contains(navigationProvider.Items, item => item.Id == SecondaryNavigationSampleModule.NavigationItemId);

        // The ungrouped item sorts before the grouped ("Admin") one.
        Assert.Equal(
            [NavigationSampleModule.NavigationItemId, SecondaryNavigationSampleModule.NavigationItemId],
            navigationProvider.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task MultipleModules_BothRemoveTheirOwnItemOnDisposal_NoneOrphaned()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(
            typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var navigationProvider = (INavigationProvider)serviceProvider.GetService(typeof(INavigationProvider));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);
        await lifecycleManager.StopAllAsync(CancellationToken.None);
        await lifecycleManager.DisposeAllAsync(CancellationToken.None);

        Assert.Empty(navigationProvider.Items);
    }

    // ----------------------------------------------------------------
    // Duplicate ID: isolated by the existing, unmodified ModuleLifecycleManager
    // (ADR-0013), exactly as ADR-0032 states no new Host failure policy is
    // needed for Navigation.
    // ----------------------------------------------------------------

    [Fact]
    public async Task DuplicateNavigationId_FailsOnlyTheOffendingModule_TheOriginalRegistrationSurvives()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(
            typeof(NavigationSampleModule), typeof(DuplicateNavigationModule));
        var navigationProvider = (INavigationProvider)serviceProvider.GetService(typeof(INavigationProvider));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        // Exactly one item is registered - the original, successful
        // registration - never overwritten or duplicated by the failing module.
        var item = Assert.Single(navigationProvider.Items);
        Assert.Equal("Home", item.Title);

        Assert.Equal(ModuleState.Initialised, lifecycleManager.GetState("tempest.samples.navigation"));

        Assert.Equal(
            ModuleState.Failed,
            lifecycleManager.GetState("tempest.validation.faultinjection.navigation-duplicate"));

        var failure = lifecycleManager.Modules.Single(
            status => status.Descriptor.Id == "tempest.validation.faultinjection.navigation-duplicate");
        Assert.IsType<DuplicateNavigationItemException>(failure.FailureReason);
    }

    [Fact]
    public async Task DuplicateNavigationId_DoesNotPreventUnrelatedModulesFromInitialising()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(
            typeof(NavigationSampleModule),
            typeof(DuplicateNavigationModule),
            typeof(SecondaryNavigationSampleModule));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        // "tempest.samples.navigation.secondary" sorts before the duplicate's
        // own "tempest.validation.faultinjection.navigation-duplicate" ('s'
        // < 'v' ordinally), so secondary initialises before the duplicate is
        // even attempted. Either order, the point stands: the duplicate's
        // failure does not stop the batch.
        Assert.Equal(
            ModuleState.Initialised,
            lifecycleManager.GetState("tempest.samples.navigation.secondary"));
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithNavigationSampleModule_RegistersAndLogsThroughTheRealHost()
    {
        var host = new TempestHostBuilder([typeof(NavigationSampleModule)]).Build();
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            Assert.Equal(HostState.Running, host.State);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Stopped, host.State);

        var output = writer.ToString();
        Assert.Contains(
            $"Navigation item '{NavigationSampleModule.NavigationItemId}' registered.", output);
    }

    // ----------------------------------------------------------------
    // Plugin compatibility: a module contributed by a plugin-loaded assembly
    // registers navigation through the identical path an ordinarily-
    // discovered module uses - no plugin-specific navigation mechanism of
    // any kind. Mirrors
    // PluginAssemblyLoaderTests.LoadPlugins_LoadedAssembly_IsVisibleToUnchangedModuleDiscovery's
    // own "prove the existing mechanism needs no change" methodology.
    // ----------------------------------------------------------------

    [Fact]
    public async Task PluginLoadedModule_RegistersNavigationItem_ThroughTheIdenticalPathAnOrdinaryModuleUses()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithNavigationModule(
            temp.Path,
            "NavigationPlugin.dll",
            "test.plugin.navigation",
            "Navigation Plugin",
            "1.0.0",
            "test.plugin.navigation.page",
            "Plugin Page");

        // ADR-0111: the dynamically-built module's constructor injects
        // INavigationProvider, which is not in the fixed always-allowed
        // baseline (ILogger/IConfigurationProvider/IDiagnosticsProvider),
        // so this plugin must explicitly request (and, at FirstParty tier,
        // is eligible to be granted) a plugin.services.resolve:* capability
        // naming it.
        var manifest = new PluginManifest(
            "test.plugin.navigation", "Navigation Plugin", "1.0.0",
            new Version(0, 1, 0), Path.GetFileName(assemblyPath), assemblyPath,
            PluginTrustTier.FirstParty,
            requestedCapabilities: [PluginCapability.ServiceResolve(typeof(INavigationProvider).FullName!)]);

        var loader = new PluginAssemblyLoader();
        var loadedAssemblies = loader.LoadPlugins([manifest]);
        var loadedAssembly = Assert.Single(loadedAssemblies);

        // The exact same, completely unchanged discovery service the Host
        // itself uses - scoped to just the newly-loaded plugin assembly.
        var descriptors = new ReflectionFrameworkDiscoveryService([loadedAssembly]).DiscoverModules();
        var descriptor = Assert.Single(descriptors);
        Assert.Equal("test.plugin.navigation", descriptor.Id);

        var runtimeManager = new RuntimeModuleManager();
        runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));
        var serviceProvider = new TempestServiceProvider(services);

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var navigationProvider = (INavigationProvider)serviceProvider.GetService(typeof(INavigationProvider));
        var item = Assert.Single(navigationProvider.Items);
        Assert.Equal("test.plugin.navigation.page", item.Id);
        Assert.Equal("Plugin Page", item.Title);
    }
}
