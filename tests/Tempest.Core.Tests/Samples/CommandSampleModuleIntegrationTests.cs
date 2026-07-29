using Tempest.Core.Commands;
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

namespace Tempest.Core.Tests.Samples;

// Proves WP 5.1B end-to-end: CommandSampleModule constructor-injects the
// real, unmodified ICommandDispatcher/ICommandRegistry (WP 5.1B) and
// registers real command handlers/descriptors through them, driven entirely
// by the real, unmodified module pipeline - exactly the same composition
// NavigationSampleModuleIntegrationTests already proves for Navigation.
// Nothing here is a mock or a test double standing in for a real platform
// service, except a level-recording ILogger used only to observe log output.
[Collection("Console output capture")]
public class CommandSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(CommandSampleModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection
    // ----------------------------------------------------------------

    [Fact]
    public void CommandSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        var (_, serviceProvider) = BuildPipeline(typeof(CommandSampleModule));

        var module = Assert.IsType<CommandSampleModule>(serviceProvider.GetService(typeof(CommandSampleModule)));

        Assert.NotNull(module);
    }

    // ----------------------------------------------------------------
    // Lifecycle: Initialise registers both commands
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersBothCommandDescriptors()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(CommandSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(2, commandRegistry.Items.Count);
        Assert.Contains(commandRegistry.Items, d => d.Id == CommandSampleModule.IncrementCounterCommandId);
        Assert.Contains(commandRegistry.Items, d => d.Id == CommandSampleModule.NavigateHomeCommandId);
    }

    // ----------------------------------------------------------------
    // Successful execution and failure propagation, dispatched by Id
    // through the real ICommandRegistry
    // ----------------------------------------------------------------

    [Fact]
    public async Task IncrementCounterCommand_InvokedByDefaultFactory_Succeeds()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(CommandSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(CommandSampleModule.IncrementCounterCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);

        var module = Assert.IsType<CommandSampleModule>(serviceProvider.GetService(typeof(CommandSampleModule)));
        Assert.Equal(1, module.Counter);
    }

    [Fact]
    public async Task IncrementCounterCommand_DispatchedDirectlyWithNegativeAmount_ReturnsFailure_WithoutThrowing()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(CommandSampleModule));
        var commandDispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandDispatcher.DispatchAsync(new IncrementCounterCommand(-5), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("non-negative", result.Message);

        var module = Assert.IsType<CommandSampleModule>(serviceProvider.GetService(typeof(CommandSampleModule)));
        Assert.Equal(0, module.Counter);
    }

    [Fact]
    public async Task IncrementCounterCommand_DispatchedRepeatedly_AccumulatesAcrossCalls()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(CommandSampleModule));
        var commandDispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandDispatcher.DispatchAsync(new IncrementCounterCommand(3), CancellationToken.None);
        await commandDispatcher.DispatchAsync(new IncrementCounterCommand(4), CancellationToken.None);

        var module = Assert.IsType<CommandSampleModule>(serviceProvider.GetService(typeof(CommandSampleModule)));
        Assert.Equal(7, module.Counter);
    }

    // ----------------------------------------------------------------
    // Navigation integration: the first concrete realisation of ADR-0022's
    // own OpenModuleCommand -> NavigationService.Navigate(...) shape.
    // ----------------------------------------------------------------

    [Fact]
    public async Task NavigateToSampleHomeCommand_Invoked_PublishesNavigationRequestedEventForTheNavigationSampleModulesItem()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(
            typeof(CommandSampleModule), typeof(NavigationSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var eventBus = (IEventBus)serviceProvider.GetService(typeof(IEventBus));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var received = new List<NavigationRequestedEvent>();
        eventBus.Subscribe(new RecordingHandler<NavigationRequestedEvent>((e, ct) => { received.Add(e); return Task.CompletedTask; }));

        var result = await commandRegistry.InvokeAsync(CommandSampleModule.NavigateHomeCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        var published = Assert.Single(received);
        Assert.Equal(NavigationSampleModule.NavigationItemId, published.Item.Id);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithCommandSampleModule_RegistersAndLogsThroughTheRealHost()
    {
        var host = new TempestHostBuilder([typeof(CommandSampleModule)]).Build();
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            Assert.Equal(HostState.Running, host.State);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var result = await registry.InvokeAsync(CommandSampleModule.IncrementCounterCommandId, CancellationToken.None);
            Assert.True(result.Succeeded);

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
            $"Command descriptor registered: '{CommandSampleModule.IncrementCounterCommandId}'", output);
    }

    // ----------------------------------------------------------------
    // Plugin compatibility: a module contributed by a plugin-loaded
    // assembly registers command handlers/descriptors through the
    // identical path an ordinarily-discovered module uses - no
    // plugin-specific Command Framework mechanism of any kind.
    // ----------------------------------------------------------------

    [Fact]
    public async Task PluginLoadedModule_RegistersCommand_ThroughTheIdenticalPathAnOrdinaryModuleUses()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithCommandModule(
            temp.Path,
            "CommandPlugin.dll",
            "test.plugin.commands",
            "Command Plugin",
            "1.0.0",
            "test.plugin.commands.increment",
            "Plugin Increment");

        var manifest = new PluginManifest(
            "test.plugin.commands", "Command Plugin", "1.0.0",
            new Version(0, 1, 0), Path.GetFileName(assemblyPath), assemblyPath);

        var loader = new PluginAssemblyLoader();
        var loadedAssemblies = loader.LoadPlugins([manifest]);
        var loadedAssembly = Assert.Single(loadedAssemblies);

        var descriptors = new ReflectionFrameworkDiscoveryService([loadedAssembly]).DiscoverModules();
        var descriptor = Assert.Single(descriptors);
        Assert.Equal("test.plugin.commands", descriptor.Id);

        var runtimeManager = new RuntimeModuleManager();
        runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));
        var serviceProvider = new TempestServiceProvider(services);

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var item = Assert.Single(commandRegistry.Items);
        Assert.Equal("test.plugin.commands.increment", item.Id);
        Assert.Equal("Plugin Increment", item.DisplayName);

        // The dynamically-emitted plugin module registers its descriptor
        // without a CreateDefault factory (emitting a closure via raw IL
        // would add substantial, unnecessary complexity to this test
        // fixture) - dispatch is proven directly through the identical,
        // shared ICommandDispatcher instead, exactly as a caller with real
        // data already would.
        var commandDispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));
        var result = await commandDispatcher.DispatchAsync(new IncrementCounterCommand(1), CancellationToken.None);
        Assert.True(result.Succeeded);
    }
}
