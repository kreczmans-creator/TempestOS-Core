using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 6.5 end-to-end: AuditSampleModule constructor-injects the
// real, unmodified IIdentityService/IAuditRecorder/IAuditQuery/
// ICommandDispatcher/ICommandRegistry, establishes its own principal,
// records an action during its own initialisation, and demonstrates both
// the permission-denied-by-default and granted query paths - driven
// entirely by the real, unmodified module pipeline, mirroring
// SettingsSampleModuleIntegrationTests/IdentitySampleModuleIntegrationTests.
[Collection("Console output capture")]
public class AuditSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        IConfigurationProvider configuration, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(AuditSampleModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddInstance(configuration);
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INavigationProvider, NavigationService>();
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
        services.Singleton<IAuditQuery, AuditQuery>();

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    private static IConfigurationProvider EmptyConfiguration(string persistenceRootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
        ])).Build();

    private static IConfigurationProvider ConfigurationGrantingQueryPermission(string persistenceRootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
            new KeyValuePair<string, string>("Identity:Roles:Auditor:Permissions", AuditQuery.QueryPermission.Key),
            new KeyValuePair<string, string>($"Identity:Principals:{AuditSampleModule.SampleIdentityId}:Roles", "Auditor"),
        ])).Build();

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time recording
    // ----------------------------------------------------------------

    [Fact]
    public void AuditSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(AuditSampleModule));

        var module = serviceProvider.GetService(typeof(AuditSampleModule));

        Assert.IsType<AuditSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_EstablishesPrincipalAndRecordsTheInitialisedAction()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingQueryPermission(temp.Path), typeof(AuditSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<AuditSampleModule>(serviceProvider.GetService(typeof(AuditSampleModule)));
        Assert.True(module.HasRegistered);

        var query = (IAuditQuery)serviceProvider.GetService(typeof(IAuditQuery));
        var records = await query.QueryAsync(new AuditQueryCriteria(actorId: AuditSampleModule.SampleIdentityId));

        Assert.Contains(records, r => r.Action == AuditSampleModule.InitialisedActionName);
    }

    // ----------------------------------------------------------------
    // Command registration and invocation
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersBothCommandDescriptors()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(AuditSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(2, commandRegistry.Items.Count);
        Assert.Contains(commandRegistry.Items, i => i.Id == AuditSampleModule.RecordSampleAuditActionCommandId);
        Assert.Contains(commandRegistry.Items, i => i.Id == AuditSampleModule.QuerySampleAuditRecordsCommandId);
    }

    [Fact]
    public async Task RecordSampleAuditActionCommand_DispatchedTwice_BothRecordedAndQueryable()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingQueryPermission(temp.Path), typeof(AuditSampleModule));
        var commandDispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandDispatcher.DispatchAsync(new RecordSampleAuditActionCommand(), CancellationToken.None);
        await commandDispatcher.DispatchAsync(new RecordSampleAuditActionCommand(), CancellationToken.None);

        var query = (IAuditQuery)serviceProvider.GetService(typeof(IAuditQuery));
        var records = await query.QueryAsync(new AuditQueryCriteria(
            actorId: AuditSampleModule.SampleIdentityId, action: AuditSampleModule.ManualActionName));

        Assert.Equal(2, records.Count);
    }

    [Fact]
    public async Task QuerySampleAuditRecordsCommand_NoPermissionGranted_ReportsDeniedByDefault()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(AuditSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(AuditSampleModule.QuerySampleAuditRecordsCommandId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Denied", result.Message);
    }

    [Fact]
    public async Task QuerySampleAuditRecordsCommand_PermissionGranted_ReportsSuccess()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingQueryPermission(temp.Path), typeof(AuditSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(AuditSampleModule.QuerySampleAuditRecordsCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Found", result.Message);
    }

    // ----------------------------------------------------------------
    // Durability: a value recorded through one pipeline is queryable by
    // a second, independent pipeline over the same underlying storage.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecordedAction_IsQueryableByAFreshPipeline_OverTheSameUnderlyingStorage()
    {
        using var temp = new TempDirectory();
        var configuration = ConfigurationGrantingQueryPermission(temp.Path);

        var (runtimeManagerOne, serviceProviderOne) = BuildPipeline(configuration, typeof(AuditSampleModule));
        var lifecycleManagerOne = new ModuleLifecycleManager(runtimeManagerOne, serviceProviderOne);
        await lifecycleManagerOne.InitialiseAllAsync(CancellationToken.None);
        var dispatcherOne = (ICommandDispatcher)serviceProviderOne.GetService(typeof(ICommandDispatcher));
        await dispatcherOne.DispatchAsync(new RecordSampleAuditActionCommand(), CancellationToken.None);

        // A second, independent pipeline - simulating a fresh process -
        // over the same root path.
        var (runtimeManagerTwo, serviceProviderTwo) = BuildPipeline(configuration, typeof(AuditSampleModule));
        var lifecycleManagerTwo = new ModuleLifecycleManager(runtimeManagerTwo, serviceProviderTwo);
        await lifecycleManagerTwo.InitialiseAllAsync(CancellationToken.None);
        var queryTwo = (IAuditQuery)serviceProviderTwo.GetService(typeof(IAuditQuery));

        var records = await queryTwo.QueryAsync(new AuditQueryCriteria(
            actorId: AuditSampleModule.SampleIdentityId, action: AuditSampleModule.ManualActionName));

        Assert.Single(records);
    }

    // ----------------------------------------------------------------
    // Long-running durability: a larger volume of records survives and
    // remains fully queryable, proving Persistence's own append-only,
    // one-file-per-key design does not lose records under sustained use.
    // ----------------------------------------------------------------

    [Fact]
    public async Task ManyRecordsOverTime_AllSurviveAndRemainQueryable()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingQueryPermission(temp.Path), typeof(AuditSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        var dispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));

        const int recordCount = 200;
        for (var i = 0; i < recordCount; i++)
            await dispatcher.DispatchAsync(new RecordSampleAuditActionCommand(), CancellationToken.None);

        var query = (IAuditQuery)serviceProvider.GetService(typeof(IAuditQuery));
        var records = await query.QueryAsync(new AuditQueryCriteria(
            actorId: AuditSampleModule.SampleIdentityId, action: AuditSampleModule.ManualActionName));

        Assert.Equal(recordCount, records.Count);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithAuditSampleModule_RecordsAndQueriesThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(AuditSampleModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, temp.Path),
                new KeyValuePair<string, string>("Identity:Roles:Auditor:Permissions", AuditQuery.QueryPermission.Key),
                new KeyValuePair<string, string>($"Identity:Principals:{AuditSampleModule.SampleIdentityId}:Roles", "Auditor"),
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

            await registry.InvokeAsync(AuditSampleModule.RecordSampleAuditActionCommandId, CancellationToken.None);
            var result = await registry.InvokeAsync(AuditSampleModule.QuerySampleAuditRecordsCommandId, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Contains("Found", result.Message);

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
