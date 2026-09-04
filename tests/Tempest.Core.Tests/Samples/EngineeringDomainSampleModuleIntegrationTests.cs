using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Materials;
using Tempest.Core.Modules;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 8.2C end-to-end: EngineeringDomainSampleModule constructor-injects
// the real, unmodified IIdentityService/EngineeringDomainContext/IMaterialCatalog/
// IDependencyTraversal/ICommandDispatcher/ICommandRegistry, and builds its own
// twelve-object, nine-family representative graph during initialisation -
// driven entirely by the real, unmodified module pipeline, mirroring
// RequirementsSampleModuleIntegrationTests' own structure.
[Collection("Console output capture")]
public class EngineeringDomainSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        string persistenceRootPath, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(EngineeringDomainSampleModule).Assembly])
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
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();

        var currentPrincipalAccessor = new CurrentPrincipalAccessor();
        services.AddInstance<ICurrentPrincipalAccessor>(currentPrincipalAccessor);
        services.AddInstance(currentPrincipalAccessor);
        services.Singleton<IRoleProvider, RoleProvider>();
        services.Singleton<IPermissionEvaluator, PermissionEvaluator>();
        services.Singleton<IIdentityService, IdentityService>();

        services.Singleton<IPersistenceStore, PersistenceStore>();
        services.Singleton<IEngineeringDocumentStore, EngineeringDocumentStore>();
        services.Singleton<IMaterialCatalog, MaterialCatalog>();

        services.Singleton<IEngineeringObjectRepository, InMemoryEngineeringObjectRepository>();
        services.Singleton<IEngineeringRelationshipRepository, InMemoryEngineeringRelationshipRepository>();
        services.Singleton<ILifecycleTransitionTable, LifecycleTransitionTable>();
        services.Singleton<IValidationRuleSet, ValidationRuleSet>();
        services.Singleton<IReferenceIntegrityChecker, ReferenceIntegrityChecker>();
        services.Singleton<IRelationshipDiscovery, RelationshipDiscoveryService>();
        services.Singleton<IDependencyTraversal, RelationshipDiscoveryService>();
        services.Singleton<IImpactAnalysis, RelationshipDiscoveryService>();
        services.Singleton<IEvidenceComposer, EvidenceComposer>();
        // `TD-87`/`ADR-0120` — the migration registry EngineeringObjectStateStore
        // now takes as an optional collaborator, registered exactly as
        // TempestHost registers it: the container resolves every
        // constructor parameter whether or not it has a default, so a
        // collaborator missing here is a rig that no longer stands in for
        // the real graph (same reasoning as IBinaryPersistenceStore below).
        services.Singleton<IStateMigrationRegistry, StateMigrationRegistry>();
        // `TD-87`/`ADR-0120` — EngineeringObjectStateStore's own
        // `int? targetSchemaVersion` constructor parameter, registered
        // exactly as TempestHost registers it (see that file's own
        // remarks on why AddInstance, not Singleton, is required here).
        services.AddInstance(typeof(int?), (object)(int?)EngineeringObjectStateStore.CurrentSchemaVersion);
        // `TD-85` — the durable object-state store EngineeringDomainContext
        // now takes as a collaborator, registered exactly as TempestHost
        // registers it, so this rig stays a faithful stand-in for the real
        // service graph rather than a divergent one.
        services.Singleton<IEngineeringObjectStateStore, EngineeringObjectStateStore>();
        // `TD-31` — likewise the attachment content store, for the same
        // reason: the container resolves every constructor parameter
        // whether or not it has a default, so a collaborator missing here
        // is a rig that no longer stands in for the real graph.
        services.Singleton<IBinaryPersistenceStore, PersistenceStore>();
        services.Singleton<IAttachmentContentStore, AttachmentContentStore>();
        services.Singleton<EngineeringDomainContext>();

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    [Fact]
    public void EngineeringDomainSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDomainSampleModule));

        var module = serviceProvider.GetService(typeof(EngineeringDomainSampleModule));

        Assert.IsType<EngineeringDomainSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_BuildsTheCompleteTwelveObjectGraph()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDomainSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<EngineeringDomainSampleModule>(serviceProvider.GetService(typeof(EngineeringDomainSampleModule)));

        Assert.True(module.HasRegistered);
        Assert.NotNull(module.SampleProjectId);
        Assert.NotNull(module.SampleAssemblyId);
        Assert.NotNull(module.SamplePartId);
        Assert.Equal(16, module.AllSampleObjectIds.Count);
    }

    /// <summary>
    /// `TD-37` fix (`WP 10.1B`): a second, entirely independent pipeline
    /// built against the same <paramref name="persistenceRootPath"/> —
    /// mirroring a genuine second application launch from the same working
    /// directory — must reach <see cref="ModuleState.Initialised"/> again,
    /// not <see cref="ModuleState.Failed"/> against its own
    /// <c>"SAMPLE-MAT-001"</c> material registration, and must skip the
    /// whole graph-construction sequence rather than partially duplicating
    /// it (see this module's own <c>InitialiseAsync</c> remarks).
    /// </summary>
    [Fact]
    public async Task Initialise_ASecondTimeAgainstTheSamePersistenceStore_IsIdempotentNotFailed()
    {
        using var temp = new TempDirectory();

        var (firstRuntimeManager, firstServiceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDomainSampleModule));
        var firstLifecycleManager = new ModuleLifecycleManager(firstRuntimeManager, firstServiceProvider);
        await firstLifecycleManager.InitialiseAllAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Initialised, firstLifecycleManager.GetState("tempest.samples.engineeringdomain"));

        var (secondRuntimeManager, secondServiceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDomainSampleModule));
        var secondLifecycleManager = new ModuleLifecycleManager(secondRuntimeManager, secondServiceProvider);
        await secondLifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Initialised, secondLifecycleManager.GetState("tempest.samples.engineeringdomain"));

        var secondModule = Assert.IsType<EngineeringDomainSampleModule>(secondServiceProvider.GetService(typeof(EngineeringDomainSampleModule)));
        Assert.True(secondModule.HasRegistered);

        // Honestly unset on the idempotent-skip run - no lookup-by-business-Id
        // capability exists to recover the first run's own object Ids (see
        // this module's own InitialiseAsync remarks).
        Assert.Null(secondModule.SampleAssemblyId);
        Assert.Empty(secondModule.AllSampleObjectIds);
    }

    [Fact]
    public async Task Initialise_SampleAssembly_WasRevisedThroughLifecycleTransitionsAndIsQueryableThroughTheSharedRepository()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDomainSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<EngineeringDomainSampleModule>(serviceProvider.GetService(typeof(EngineeringDomainSampleModule)));
        var repository = (IEngineeringObjectRepository)serviceProvider.GetService(typeof(IEngineeringObjectRepository));

        var found = await repository.FindAsync(module.SampleAssemblyId!.Value);

        var assembly = Assert.IsType<Assembly>(found);
        Assert.Equal(LifecycleState.Approved, assembly.Status);
        Assert.Equal(2, assembly.History.Count);
    }

    [Fact]
    public async Task Initialise_SamplePart_ReferencesARealMaterialSpecificationFromTheMaterialsFramework()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDomainSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<EngineeringDomainSampleModule>(serviceProvider.GetService(typeof(EngineeringDomainSampleModule)));
        var repository = (IEngineeringObjectRepository)serviceProvider.GetService(typeof(IEngineeringObjectRepository));
        var materialCatalog = (IMaterialCatalog)serviceProvider.GetService(typeof(IMaterialCatalog));

        var part = Assert.IsType<Part>(await repository.FindAsync(module.SamplePartId!.Value));
        var material = await materialCatalog.FindAsync(part.MaterialId!);

        Assert.NotNull(material);
        Assert.Equal("Fictional Sample Alloy", material!.Name);
    }

    [Fact]
    public async Task Initialise_RegistersTheGraphSummaryCommandDescriptor()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDomainSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Single(commandRegistry.Items, i => i.Id == EngineeringDomainSampleModule.GetGraphSummaryCommandId);
    }

    [Fact]
    public async Task GetSampleEngineeringDomainGraphSummaryCommand_Dispatched_ReportsCompositionTraversalResult()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(EngineeringDomainSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(EngineeringDomainSampleModule.GetGraphSummaryCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("16 objects total", result.Message);
    }

    [Fact]
    public async Task RunAsync_WithEngineeringDomainSampleModule_RunsThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(EngineeringDomainSampleModule)])
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
            var result = await registry.InvokeAsync(EngineeringDomainSampleModule.GetGraphSummaryCommandId, CancellationToken.None);

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
