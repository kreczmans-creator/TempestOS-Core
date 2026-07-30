using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringData;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 7.1E end-to-end: VerificationSampleModule constructor-injects
// the real, unmodified IEngineeringDocumentStore/IVerificationService/
// ICommandDispatcher/ICommandRegistry, creates a sample subject document
// and records a verification against it during its own initialisation,
// and demonstrates the permission-gated history-read command path -
// driven entirely by the real, unmodified module pipeline, mirroring
// CalculationSampleModuleIntegrationTests.
[Collection("Console output capture")]
public class VerificationSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        string persistenceRootPath, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(VerificationSampleModule).Assembly])
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
        services.Singleton<IPermissionEvaluator, PermissionEvaluator>();

        services.Singleton<IPersistenceStore, PersistenceStore>();
        services.Singleton<IEngineeringDocumentStore, EngineeringDocumentStore>();
        services.Singleton<IVerificationService, VerificationService>();

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time verification lifecycle
    // ----------------------------------------------------------------

    [Fact]
    public void VerificationSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(temp.Path, typeof(VerificationSampleModule));

        var module = serviceProvider.GetService(typeof(VerificationSampleModule));

        Assert.IsType<VerificationSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_CreatesSubjectDocumentAndRecordsVerification()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(VerificationSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<VerificationSampleModule>(serviceProvider.GetService(typeof(VerificationSampleModule)));
        Assert.True(module.HasRegistered);
        Assert.NotNull(module.SampleSubjectDocumentId);
        Assert.NotNull(module.SampleVerificationRecordId);

        var documentStore = (IEngineeringDocumentStore)serviceProvider.GetService(typeof(IEngineeringDocumentStore));
        var document = await documentStore.FindAsync(module.SampleVerificationRecordId!.Value);
        Assert.NotNull(document);
        Assert.Equal(VerificationService.VerificationRecordDocumentKind, document!.Kind);
    }

    // ----------------------------------------------------------------
    // Command registration and invocation
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersTheCommandDescriptor()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(VerificationSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Single(commandRegistry.Items, i => i.Id == VerificationSampleModule.GetSampleVerificationHistoryCommandId);
    }

    [Fact]
    public async Task GetSampleVerificationHistoryCommand_Dispatched_DeniedByDefault()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(VerificationSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(VerificationSampleModule.GetSampleVerificationHistoryCommandId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Denied", result.Message);
    }

    [Fact]
    public async Task GetSampleVerificationHistoryCommand_Dispatched_WithGrantedPermission_Succeeds()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(VerificationSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var accessor = (CurrentPrincipalAccessor)serviceProvider.GetService(typeof(CurrentPrincipalAccessor));
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("verifier", "Verifier"), [VerificationService.ReadPermission]));

        var result = await commandRegistry.InvokeAsync(VerificationSampleModule.GetSampleVerificationHistoryCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Found 1 verification record", result.Message);
    }

    // ----------------------------------------------------------------
    // Durability: a verification recorded through one pipeline is
    // readable by a second, independent pipeline over the same
    // underlying storage.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecordedVerification_IsReadableByAFreshPipeline_OverTheSameUnderlyingStorage()
    {
        using var temp = new TempDirectory();

        var (runtimeManagerOne, serviceProviderOne) = BuildPipeline(temp.Path, typeof(VerificationSampleModule));
        var lifecycleManagerOne = new ModuleLifecycleManager(runtimeManagerOne, serviceProviderOne);
        await lifecycleManagerOne.InitialiseAllAsync(CancellationToken.None);
        var moduleOne = Assert.IsType<VerificationSampleModule>(serviceProviderOne.GetService(typeof(VerificationSampleModule)));

        var persistenceStoreTwo = new PersistenceStore(new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, temp.Path),
        ])).Build());
        var accessorTwo = new CurrentPrincipalAccessor();
        var documentStoreTwo = new EngineeringDocumentStore(persistenceStoreTwo, accessorTwo);
        var serviceTwo = new VerificationService(documentStoreTwo, accessorTwo, new PermissionEvaluator());
        accessorTwo.SetCurrent(new PlatformPrincipal(new PlatformIdentity("verifier", "Verifier"), [VerificationService.ReadPermission]));

        var history = await serviceTwo.GetVerificationHistoryAsync(moduleOne.SampleSubjectDocumentId!.Value);

        var record = Assert.Single(history);
        Assert.Equal(VerificationOutcome.Pass, record.Outcome);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithVerificationSampleModule_RecordsThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(VerificationSampleModule)])
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

            var result = await registry.InvokeAsync(VerificationSampleModule.GetSampleVerificationHistoryCommandId, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Contains("Denied", result.Message);

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
