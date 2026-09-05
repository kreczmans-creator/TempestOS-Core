using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringData;
using Tempest.Core.Events;
using Tempest.Core.ExportImport;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Persistence;
using Tempest.Core.Reporting;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 7.3A end-to-end: RequirementsSampleModule constructor-injects
// the real, unmodified IIdentityService/IRequirementsService/
// IEngineeringDocumentStore/IVerificationService/ICurrentPrincipalAccessor/
// IPermissionEvaluator/IAuditRecorder/IReportingService/ImportService/
// ICommandDispatcher/ICommandRegistry, creates a sample requirement and
// walks it through revision, lifecycle, grouping, collection, allocation,
// and verification during its own initialisation, and demonstrates two
// command paths (permission-gated evidence read, denied by default;
// report generation) - driven entirely by the real, unmodified module
// pipeline, mirroring ExportImportSampleModuleIntegrationTests' own
// structure.
[Collection("Console output capture")]
public class RequirementsSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        string persistenceRootPath, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(RequirementsSampleModule).Assembly])
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
        services.Singleton<IAuditRecorder, AuditRecorder>();
        services.Singleton<IEngineeringDocumentStore, EngineeringDocumentStore>();
        services.Singleton<IVerificationService, VerificationService>();
        services.Singleton<IRequirementsService, RequirementsService>();
        services.Singleton<IReportingService, ReportingService>();

        var exportFormat = new JsonExportFormat();
        services.AddInstance<IExportFormat>(exportFormat);
        var importService = new ImportService(exportFormat);
        services.AddInstance<IImportService>(importService);
        services.AddInstance(importService);

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time lifecycle
    // ----------------------------------------------------------------

    [Fact]
    public void RequirementsSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));

        var module = serviceProvider.GetService(typeof(RequirementsSampleModule));

        Assert.IsType<RequirementsSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_CreatesRequirementGroupCollectionAllocationAndVerification()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<RequirementsSampleModule>(serviceProvider.GetService(typeof(RequirementsSampleModule)));
        Assert.True(module.HasRegistered);
        Assert.NotNull(module.SampleRequirementId);
        Assert.NotNull(module.SampleGroupId);
        Assert.NotNull(module.SampleCollectionId);

        var requirementsService = (IRequirementsService)serviceProvider.GetService(typeof(IRequirementsService));
        var requirement = await requirementsService.FindAsync(module.SampleRequirementId!.Value);

        Assert.NotNull(requirement);
        Assert.Equal(RequirementStatus.Allocated, requirement!.Status);
        Assert.Equal(5, requirement.RevisionNumber); // create + one ReviseAsync + three SetStatusAsync calls (Reviewed, Approved, Allocated)
    }

    /// <summary>
    /// `TD-37` fix (`WP 10.1B`): <see cref="IRequirementsService"/>'s own
    /// <c>Identifier</c> index is durable (`ADR-0058`), so a second,
    /// entirely independent pipeline built against the same
    /// <paramref name="persistenceRootPath"/> — mirroring a genuine second
    /// application launch from the same working directory — must reach
    /// <see cref="ModuleState.Initialised"/> again, not
    /// <see cref="ModuleState.Failed"/>, and must reuse the
    /// already-created <c>"SAMPLE-REQ-001"</c> rather than repeating the
    /// full create/revise/status/group/collection/allocate/verify
    /// sequence.
    /// </summary>
    [Fact]
    public async Task Initialise_ASecondTimeAgainstTheSamePersistenceStore_IsIdempotentNotFailed()
    {
        using var temp = new TempDirectory();

        var (firstRuntimeManager, firstServiceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));
        var firstLifecycleManager = new ModuleLifecycleManager(firstRuntimeManager, firstServiceProvider);
        await firstLifecycleManager.InitialiseAllAsync(CancellationToken.None);
        Assert.Equal(ModuleState.Initialised, firstLifecycleManager.GetState("tempest.samples.requirements"));

        var firstModule = Assert.IsType<RequirementsSampleModule>(firstServiceProvider.GetService(typeof(RequirementsSampleModule)));
        var firstRequirementId = firstModule.SampleRequirementId;

        var (secondRuntimeManager, secondServiceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));
        var secondLifecycleManager = new ModuleLifecycleManager(secondRuntimeManager, secondServiceProvider);
        await secondLifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Initialised, secondLifecycleManager.GetState("tempest.samples.requirements"));

        var secondModule = Assert.IsType<RequirementsSampleModule>(secondServiceProvider.GetService(typeof(RequirementsSampleModule)));
        Assert.True(secondModule.HasRegistered);
        Assert.Equal(firstRequirementId, secondModule.SampleRequirementId);
    }

    [Fact]
    public async Task Initialise_RecordsAVerification_ReadableThroughVerificationServiceDirectly()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<RequirementsSampleModule>(serviceProvider.GetService(typeof(RequirementsSampleModule)));
        var accessor = (CurrentPrincipalAccessor)serviceProvider.GetService(typeof(CurrentPrincipalAccessor));
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("verifier", "Verifier"), [VerificationService.ReadPermission]));

        var verificationService = (IVerificationService)serviceProvider.GetService(typeof(IVerificationService));
        var history = await verificationService.GetVerificationHistoryAsync(module.SampleRequirementId!.Value);

        var record = Assert.Single(history);
        Assert.Equal(VerificationOutcome.Pass, record.Outcome);
    }

    // ----------------------------------------------------------------
    // Command registration and invocation
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersBothCommandDescriptors()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Single(commandRegistry.Items, i => i.Id == RequirementsSampleModule.GetSampleRequirementEvidenceCommandId);
        Assert.Single(commandRegistry.Items, i => i.Id == RequirementsSampleModule.GenerateSampleRequirementReportCommandId);
    }

    [Fact]
    public async Task GetSampleRequirementEvidenceCommand_Dispatched_DeniedByDefault()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(RequirementsSampleModule.GetSampleRequirementEvidenceCommandId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Denied", result.Message);
    }

    [Fact]
    public async Task GetSampleRequirementEvidenceCommand_Dispatched_WithGrantedPermission_Succeeds()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var accessor = (CurrentPrincipalAccessor)serviceProvider.GetService(typeof(CurrentPrincipalAccessor));
        accessor.SetCurrent(new PlatformPrincipal(
            new PlatformIdentity("reader", "Reader"),
            [new Permission(RequirementsSampleModule.ReadPermissionKey), VerificationService.ReadPermission]));

        var result = await commandRegistry.InvokeAsync(RequirementsSampleModule.GetSampleRequirementEvidenceCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Found 1 verification record", result.Message);
    }

    [Fact]
    public async Task GenerateSampleRequirementReportCommand_Dispatched_GeneratesReport()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(RequirementsSampleModule.GenerateSampleRequirementReportCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Report generated", result.Message);
    }

    // ----------------------------------------------------------------
    // Export/Import integration
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersExportAdapter_ImportRoundTripsANewRequirement()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(temp.Path, typeof(RequirementsSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<RequirementsSampleModule>(serviceProvider.GetService(typeof(RequirementsSampleModule)));
        var requirementsService = (IRequirementsService)serviceProvider.GetService(typeof(IRequirementsService));
        var exportFormat = (IExportFormat)serviceProvider.GetService(typeof(IExportFormat));
        var importService = (ImportService)serviceProvider.GetService(typeof(ImportService));

        var adapter = new RequirementExportAdapter(requirementsService, RequirementsSampleModule.ExportAdapterKind, module.SampleRequirementId!.Value);
        var exportService = new ExportService(exportFormat);

        using var artifactStream = new MemoryStream();
        await exportService.ExportAsync(artifactStream, [adapter], CancellationToken.None);

        artifactStream.Position = 0;
        var beforeCount = (await requirementsService.ListAsync()).Count;

        await importService.ImportAsync(artifactStream, CancellationToken.None);

        var afterCount = (await requirementsService.ListAsync()).Count;
        Assert.Equal(beforeCount + 1, afterCount);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithRequirementsSampleModule_RunsThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(RequirementsSampleModule)])
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

            var result = await registry.InvokeAsync(RequirementsSampleModule.GetSampleRequirementEvidenceCommandId, CancellationToken.None);

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
