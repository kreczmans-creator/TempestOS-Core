using Tempest.Core.Commands;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Plugins;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Events;
using Tempest.Core.Tests.Logging;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Runtime;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 5.1B end-to-end: CommandSampleModule constructor-injects the
// real, unmodified ICommandDispatcher/ICommandRegistry (WP 5.1B) and
// registers real command handlers/descriptors through them, driven entirely
// by the real, unmodified module pipeline - exactly the same composition
// NavigationSampleModuleIntegrationTests already proves for Navigation.
// Nothing here is a mock or a test double standing in for a real platform
// service, except a level-recording ILogger used only to observe log output.
//
// One test here builds a dynamically-emitted plugin assembly via
// DynamicPluginAssemblyBuilder (System.Reflection.Emit's
// PersistedAssemblyBuilder) - not safe to run concurrently with another
// such build elsewhere in the process, so this class shares
// [Collection("Dynamic plugin assembly emission")] with every other class
// in this assembly that calls DynamicPluginAssemblyBuilder.
[Collection("Dynamic plugin assembly emission")]
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
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
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
        var sink = new RecordingLogSink();
        var host = new TempestHostBuilder([typeof(CommandSampleModule)])
            .AddLogSink(sink).WithIsolatedPersistenceRoot()
            .Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        Assert.Equal(HostState.Running, host.State);

        var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        var result = await registry.InvokeAsync(CommandSampleModule.IncrementCounterCommandId, CancellationToken.None);
        Assert.True(result.Succeeded);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);

        var messages = sink.Entries.Select(entry => entry.Message).ToList();
        Assert.Contains(
            messages,
            message => message.Contains($"Command descriptor registered: '{CommandSampleModule.IncrementCounterCommandId}'"));
    }

    // The plugin-compatibility test formerly here (a module contributed by
    // a plugin-loaded assembly registering command handlers/descriptors
    // through the identical path an ordinarily-discovered module uses) was
    // frozen by ADR-0146 (WP 17.2A) along with plugin assembly loading and
    // trust tiers - see src/Frozen/README.md.
}
