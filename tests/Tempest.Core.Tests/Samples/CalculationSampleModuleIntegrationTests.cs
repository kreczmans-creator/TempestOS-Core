using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringData;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Materials;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 7.1D end-to-end: CalculationSampleModule constructor-injects
// the real, unmodified ICalculationEngine/ICommandDispatcher/
// ICommandRegistry, registers and executes DoubleLengthCalculationDefinition
// during its own initialisation, and demonstrates the execute command
// path - driven entirely by the real, unmodified module pipeline,
// mirroring MaterialsSampleModuleIntegrationTests.
[Collection("Console output capture")]
public class CalculationSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        string persistenceRootPath, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(CalculationSampleModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
        ])).Build();

        var services = new ServiceCollection();
        services.AddInstance<IConfigurationProvider>(configuration);
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();

        var currentPrincipalAccessor = new CurrentPrincipalAccessor();
        services.AddInstance<ICurrentPrincipalAccessor>(currentPrincipalAccessor);
        services.AddInstance(currentPrincipalAccessor);

        services.Singleton<IPersistenceStore, PersistenceStore>();
        services.Singleton<IEngineeringDocumentStore, EngineeringDocumentStore>();
        services.Singleton<IMaterialCatalog, MaterialCatalog>();
        services.Singleton<ICalculationEngine, CalculationEngine>();

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time calculation lifecycle
    // ----------------------------------------------------------------

    [Fact]
    public void CalculationSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(temp.Path, typeof(CalculationSampleModule));

        var module = serviceProvider.GetService(typeof(CalculationSampleModule));

        Assert.IsType<CalculationSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_RegistersAndExecutesTheSampleCalculation()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(CalculationSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<CalculationSampleModule>(serviceProvider.GetService(typeof(CalculationSampleModule)));
        Assert.True(module.HasRegistered);
        Assert.NotNull(module.SampleRecordId);

        var documentStore = (IEngineeringDocumentStore)serviceProvider.GetService(typeof(IEngineeringDocumentStore));
        var document = await documentStore.FindAsync(module.SampleRecordId!.Value);
        Assert.NotNull(document);
        Assert.Equal(CalculationEngine.CalculationRecordDocumentKind, document!.Kind);
    }

    // ----------------------------------------------------------------
    // Command registration and invocation
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersTheCommandDescriptor()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(CalculationSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Single(commandRegistry.Items, i => i.Id == CalculationSampleModule.ExecuteSampleCalculationCommandId);
    }

    [Fact]
    public async Task ExecuteSampleCalculationCommand_Dispatched_ExecutesTheCalculationAgain()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(CalculationSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(CalculationSampleModule.ExecuteSampleCalculationCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Executed calculation", result.Message);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithCalculationSampleModule_ExecutesThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(CalculationSampleModule)])
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

            var result = await registry.InvokeAsync(CalculationSampleModule.ExecuteSampleCalculationCommandId, CancellationToken.None);

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
}
