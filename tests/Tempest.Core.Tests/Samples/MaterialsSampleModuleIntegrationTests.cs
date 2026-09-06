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

// Proves WP 7.1C end-to-end: MaterialsSampleModule constructor-injects the
// real, unmodified IMaterialCatalog/ICommandDispatcher/ICommandRegistry,
// registers and revises a fictional material during its own
// initialisation, and demonstrates both the register and revise command
// paths - driven entirely by the real, unmodified module pipeline,
// mirroring EngineeringDataSampleModuleIntegrationTests.
[Collection("Console output capture")]
public class MaterialsSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        string persistenceRootPath, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(MaterialsSampleModule).Assembly])
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

        var currentPrincipalAccessor = new CurrentPrincipalAccessor();
        services.AddInstance<ICurrentPrincipalAccessor>(currentPrincipalAccessor);
        services.AddInstance(currentPrincipalAccessor);

        services.Singleton<IPersistenceStore, PersistenceStore>();
        services.Singleton<IEngineeringDocumentStore, EngineeringDocumentStore>();
        services.Singleton<IMaterialCatalog, MaterialCatalog>();

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time material lifecycle
    // ----------------------------------------------------------------

    [Fact]
    public void MaterialsSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(temp.Path, typeof(MaterialsSampleModule));

        var module = serviceProvider.GetService(typeof(MaterialsSampleModule));

        Assert.IsType<MaterialsSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_RegistersAndRevisesASampleMaterial()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(MaterialsSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<MaterialsSampleModule>(serviceProvider.GetService(typeof(MaterialsSampleModule)));
        Assert.True(module.HasRegistered);
        Assert.Equal(MaterialsSampleModule.SampleMaterialId, module.RegisteredMaterialId);

        var catalog = (IMaterialCatalog)serviceProvider.GetService(typeof(IMaterialCatalog));
        var material = await catalog.FindAsync(MaterialsSampleModule.SampleMaterialId);

        Assert.NotNull(material);
        Assert.Equal(2, material!.RevisionNumber);
        var yieldStrength = (Quantity<Pressure>)material.Definition.Properties["YieldStrength"].Value;
        Assert.Equal(105.0, yieldStrength.Value);
    }

    /// <summary>
    /// `TD-37` fix (`WP 10.1B`): <see cref="IMaterialCatalog"/>'s own
    /// <c>materialId</c> index is durable (`ADR-0055`), so a second,
    /// entirely independent pipeline built against the same
    /// <paramref name="persistenceRootPath"/> — mirroring a genuine second
    /// application launch from the same working directory — must reach
    /// <see cref="ModuleState.Initialised"/> again, not
    /// <see cref="ModuleState.Failed"/> with a
    /// <see cref="DuplicateMaterialException"/>, and must still populate
    /// <see cref="MaterialsSampleModule.RegisteredMaterialId"/> from the
    /// already-registered material rather than re-registering it.
    /// </summary>
    [Fact]
    public async Task Initialise_ASecondTimeAgainstTheSamePersistenceStore_IsIdempotentNotFailed()
    {
        using var temp = new TempDirectory();

        var (firstRuntimeManager, firstServiceProvider) = BuildPipeline(temp.Path, typeof(MaterialsSampleModule));
        var firstLifecycleManager = new ModuleLifecycleManager(firstRuntimeManager, firstServiceProvider);
        await firstLifecycleManager.InitialiseAllAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Initialised, firstLifecycleManager.GetState("tempest.samples.materials"));

        var (secondRuntimeManager, secondServiceProvider) = BuildPipeline(temp.Path, typeof(MaterialsSampleModule));
        var secondLifecycleManager = new ModuleLifecycleManager(secondRuntimeManager, secondServiceProvider);
        await secondLifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Initialised, secondLifecycleManager.GetState("tempest.samples.materials"));

        var secondModule = Assert.IsType<MaterialsSampleModule>(secondServiceProvider.GetService(typeof(MaterialsSampleModule)));
        Assert.True(secondModule.HasRegistered);
        Assert.Equal(MaterialsSampleModule.SampleMaterialId, secondModule.RegisteredMaterialId);
    }

    // ----------------------------------------------------------------
    // Command registration and invocation
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersBothCommandDescriptors()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(MaterialsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(2, commandRegistry.Items.Count);
        Assert.Contains(commandRegistry.Items, i => i.Id == MaterialsSampleModule.RegisterSampleMaterialCommandId);
        Assert.Contains(commandRegistry.Items, i => i.Id == MaterialsSampleModule.ReviseSampleMaterialCommandId);
    }

    [Fact]
    public async Task RegisterSampleMaterialCommand_Dispatched_RegistersANewMaterial()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(MaterialsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(MaterialsSampleModule.RegisterSampleMaterialCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Registered material", result.Message);
    }

    [Fact]
    public async Task ReviseSampleMaterialCommand_Dispatched_RevisesTheModulesOwnMaterial()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(MaterialsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(MaterialsSampleModule.ReviseSampleMaterialCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);

        var catalog = (IMaterialCatalog)serviceProvider.GetService(typeof(IMaterialCatalog));
        var found = await catalog.FindAsync(MaterialsSampleModule.SampleMaterialId);
        Assert.Equal(3, found!.RevisionNumber);
    }

    // ----------------------------------------------------------------
    // Durability: a material registered through one pipeline is readable
    // by a second, independent pipeline over the same underlying storage.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RegisteredMaterial_IsReadableByAFreshPipeline_OverTheSameUnderlyingStorage()
    {
        using var temp = new TempDirectory();

        var (runtimeManagerOne, serviceProviderOne) = BuildPipeline(temp.Path, typeof(MaterialsSampleModule));
        var lifecycleManagerOne = new ModuleLifecycleManager(runtimeManagerOne, serviceProviderOne);
        await lifecycleManagerOne.InitialiseAllAsync(CancellationToken.None);

        var persistenceStoreTwo = new PersistenceStore(new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, temp.Path),
        ])).Build());
        var catalogTwo = new MaterialCatalog(
            new EngineeringDocumentStore(persistenceStoreTwo, new CurrentPrincipalAccessor()),
            persistenceStoreTwo);

        var found = await catalogTwo.FindAsync(MaterialsSampleModule.SampleMaterialId);

        Assert.NotNull(found);
        Assert.Equal(2, found!.RevisionNumber);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithMaterialsSampleModule_RegistersAndRevisesThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(MaterialsSampleModule)])
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

            var result = await registry.InvokeAsync(MaterialsSampleModule.RegisterSampleMaterialCommandId, CancellationToken.None);

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
