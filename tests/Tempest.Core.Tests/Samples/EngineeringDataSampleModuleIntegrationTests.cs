using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Events;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 7.1A end-to-end: EngineeringDataSampleModule constructor-
// injects the real, unmodified IEngineeringDocumentStore/
// ICommandDispatcher/ICommandRegistry, creates/revises/links documents
// during its own initialisation, and demonstrates both the create and
// revise command paths - driven entirely by the real, unmodified module
// pipeline, mirroring AuditSampleModuleIntegrationTests.
[Collection("Console output capture")]
public class EngineeringDataSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        string persistenceRootPath, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(EngineeringDataSampleModule).Assembly])
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

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time document lifecycle
    // ----------------------------------------------------------------

    [Fact]
    public void EngineeringDataSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDataSampleModule));

        var module = serviceProvider.GetService(typeof(EngineeringDataSampleModule));

        Assert.IsType<EngineeringDataSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_CreatesRevisesAndLinksASampleDocument()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDataSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<EngineeringDataSampleModule>(serviceProvider.GetService(typeof(EngineeringDataSampleModule)));
        Assert.True(module.HasRegistered);
        Assert.NotNull(module.SampleDocumentId);

        var store = (IEngineeringDocumentStore)serviceProvider.GetService(typeof(IEngineeringDocumentStore));
        var history = await store.GetRevisionHistoryAsync(module.SampleDocumentId!.Value);
        Assert.Equal(2, history.Count);
        Assert.Equal("Initial sample content.", history[0].Content);
        Assert.Equal("Revised sample content.", history[1].Content);

        var references = await store.GetReferencesAsync(module.SampleDocumentId.Value);
        var reference = Assert.Single(references);
        Assert.Equal(EngineeringDataSampleModule.SampleRelationshipKind, reference.RelationshipKind);
    }

    // ----------------------------------------------------------------
    // Command registration and invocation
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersBothCommandDescriptors()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDataSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(2, commandRegistry.Items.Count);
        Assert.Contains(commandRegistry.Items, i => i.Id == EngineeringDataSampleModule.CreateSampleDocumentCommandId);
        Assert.Contains(commandRegistry.Items, i => i.Id == EngineeringDataSampleModule.ReviseSampleDocumentCommandId);
    }

    [Fact]
    public async Task CreateSampleDocumentCommand_Dispatched_CreatesANewDocument()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDataSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(EngineeringDataSampleModule.CreateSampleDocumentCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Created document", result.Message);
    }

    [Fact]
    public async Task ReviseSampleDocumentCommand_Dispatched_RevisesTheModulesOwnDocument()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDataSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        var module = Assert.IsType<EngineeringDataSampleModule>(serviceProvider.GetService(typeof(EngineeringDataSampleModule)));

        var result = await commandRegistry.InvokeAsync(EngineeringDataSampleModule.ReviseSampleDocumentCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);

        var store = (IEngineeringDocumentStore)serviceProvider.GetService(typeof(IEngineeringDocumentStore));
        var found = await store.FindAsync(module.SampleDocumentId!.Value);
        Assert.Equal(3, found!.CurrentRevisionNumber);
    }

    // ----------------------------------------------------------------
    // Durability: a document created through one pipeline is readable by
    // a second, independent pipeline over the same underlying storage.
    // ----------------------------------------------------------------

    [Fact]
    public async Task CreatedDocument_IsReadableByAFreshPipeline_OverTheSameUnderlyingStorage()
    {
        using var temp = new TempDirectory();

        var (runtimeManagerOne, serviceProviderOne) = BuildPipeline(temp.Path, typeof(EngineeringDataSampleModule));
        var lifecycleManagerOne = new ModuleLifecycleManager(runtimeManagerOne, serviceProviderOne);
        await lifecycleManagerOne.InitialiseAllAsync(CancellationToken.None);
        var moduleOne = Assert.IsType<EngineeringDataSampleModule>(serviceProviderOne.GetService(typeof(EngineeringDataSampleModule)));

        // A second, independent pipeline - simulating a fresh process -
        // over the same root path.
        var storeTwo = new EngineeringDocumentStore(
            new PersistenceStore(new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, temp.Path),
            ])).Build()),
            new CurrentPrincipalAccessor());

        var found = await storeTwo.FindAsync(moduleOne.SampleDocumentId!.Value);

        Assert.NotNull(found);
        Assert.Equal(2, found!.CurrentRevisionNumber);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithEngineeringDataSampleModule_CreatesAndRevisesThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(EngineeringDataSampleModule)])
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

            var result = await registry.InvokeAsync(EngineeringDataSampleModule.CreateSampleDocumentCommandId, CancellationToken.None);

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
