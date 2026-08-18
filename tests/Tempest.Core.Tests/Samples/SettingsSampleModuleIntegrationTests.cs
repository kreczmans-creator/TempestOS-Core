using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 6.4 end-to-end: SettingsSampleModule constructor-injects the
// real, unmodified ISettingsProvider/IEventBus/ICommandDispatcher/
// ICommandRegistry, registers a setting definition and subscribes to
// ISettingsChangedEvent, driven entirely by the real, unmodified module
// pipeline - exactly the same composition
// IdentitySampleModuleIntegrationTests already proves for Identity &
// Permissions. Nothing here is a mock or a test double standing in for a
// real platform service, except a level-recording ILogger used only to
// observe log output.
[Collection("Console output capture")]
public class SettingsSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        string persistenceRootPath, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(SettingsSampleModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
        ])).Build();

        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<IConfigurationProvider>(configuration);
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();
        services.Singleton<IPersistenceStore, PersistenceStore>();
        services.Singleton<ISettingsProvider, SettingsProvider>();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection
    // ----------------------------------------------------------------

    [Fact]
    public void SettingsSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(temp.Path, typeof(SettingsSampleModule));

        var module = serviceProvider.GetService(typeof(SettingsSampleModule));

        Assert.IsType<SettingsSampleModule>(module);
    }

    // ----------------------------------------------------------------
    // Initialise-time registration
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersTheSampleSettingDefinition()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(SettingsSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<SettingsSampleModule>(serviceProvider.GetService(typeof(SettingsSampleModule)));
        Assert.True(module.HasRegistered);

        var settingsProvider = (ISettingsProvider)serviceProvider.GetService(typeof(ISettingsProvider));
        var value = await settingsProvider.GetValueAsync(SettingsSampleModule.SampleSettingKey);
        Assert.Equal(SettingsSampleModule.SampleSettingDefaultValue, value);
    }

    [Fact]
    public async Task Initialise_RegistersBothCommandDescriptors()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(SettingsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(2, commandRegistry.Items.Count);
        Assert.Contains(commandRegistry.Items, i => i.Id == SettingsSampleModule.GetSampleSettingCommandId);
        Assert.Contains(commandRegistry.Items, i => i.Id == SettingsSampleModule.SetSampleSettingCommandId);
    }

    // ----------------------------------------------------------------
    // Command-driven read/write, through the real ICommandRegistry
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetSampleSettingCommand_InvokedThroughTheRegistry_ReportsTheDefaultValue()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(SettingsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(SettingsSampleModule.GetSampleSettingCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(SettingsSampleModule.SampleSettingDefaultValue, result.Message);
    }

    [Fact]
    public async Task SetSampleSettingCommand_ThenGetSampleSettingCommand_ReportsTheNewValue()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(SettingsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var commandDispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandDispatcher.DispatchAsync(new SetSampleSettingCommand("Bonjour, TempestOS!"), CancellationToken.None);
        var result = await commandRegistry.InvokeAsync(SettingsSampleModule.GetSampleSettingCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Bonjour, TempestOS!", result.Message);
    }

    // ----------------------------------------------------------------
    // Event Bus integration: the module's own subscription observes its
    // own write, proving ISettingsChangedEvent dispatch works correctly
    // for an interface event type, not merely a sealed concrete one.
    // ----------------------------------------------------------------

    [Fact]
    public async Task SetSampleSettingCommand_Dispatched_IsObservedByTheModulesOwnSubscription()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(SettingsSampleModule));
        var commandDispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandDispatcher.DispatchAsync(new SetSampleSettingCommand("observed-value"), CancellationToken.None);

        var module = Assert.IsType<SettingsSampleModule>(serviceProvider.GetService(typeof(SettingsSampleModule)));
        var change = Assert.Single(module.ObservedChanges);
        Assert.Equal(SettingsSampleModule.SampleSettingKey, change.Key);
        Assert.Equal("observed-value", change.NewValue);
    }

    // ----------------------------------------------------------------
    // Durability: a value written through one pipeline is visible to a
    // second, independent pipeline over the same underlying storage -
    // proving Persistence's own "survives an ordinary process restart"
    // requirement end-to-end, through Settings.
    // ----------------------------------------------------------------

    [Fact]
    public async Task ValueWritten_IsVisibleToAFreshSettingsProvider_OverTheSameUnderlyingStorage()
    {
        using var temp = new TempDirectory();
        var (runtimeManagerOne, serviceProviderOne) = BuildPipeline(temp.Path, typeof(SettingsSampleModule));
        var lifecycleManagerOne = new ModuleLifecycleManager(runtimeManagerOne, serviceProviderOne);
        await lifecycleManagerOne.InitialiseAllAsync(CancellationToken.None);
        var commandDispatcherOne = (ICommandDispatcher)serviceProviderOne.GetService(typeof(ICommandDispatcher));
        await commandDispatcherOne.DispatchAsync(new SetSampleSettingCommand("persisted-across-restart"), CancellationToken.None);

        // A second, independent pipeline - simulating a fresh process -
        // over the same root path.
        var (runtimeManagerTwo, serviceProviderTwo) = BuildPipeline(temp.Path, typeof(SettingsSampleModule));
        var lifecycleManagerTwo = new ModuleLifecycleManager(runtimeManagerTwo, serviceProviderTwo);
        await lifecycleManagerTwo.InitialiseAllAsync(CancellationToken.None);
        var commandRegistryTwo = (ICommandRegistry)serviceProviderTwo.GetService(typeof(ICommandRegistry));

        var result = await commandRegistryTwo.InvokeAsync(SettingsSampleModule.GetSampleSettingCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("persisted-across-restart", result.Message);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithSettingsSampleModule_RegistersAndRoundTripsThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(SettingsSampleModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, temp.Path),
            ]))
            .Build();
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
            var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            await dispatcher.DispatchAsync(new SetSampleSettingCommand("via-real-host"), CancellationToken.None);
            var result = await registry.InvokeAsync(SettingsSampleModule.GetSampleSettingCommandId, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal("via-real-host", result.Message);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Stopped, host.State);
    }
}
