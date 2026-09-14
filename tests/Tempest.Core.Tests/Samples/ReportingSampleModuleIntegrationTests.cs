using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Notifications;
using Tempest.Core.Persistence;
using Tempest.Core.Reporting;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

using Tempest.Core.Tests.Runtime;
namespace Tempest.Core.Tests.Samples;

// Proves WP 6.0 end-to-end (updated for WP 17.2A/ADR-0146):
// ReportingSampleModule constructor-injects the real, unmodified
// CurrentPrincipalAccessor/IReportingService/ISettingsProvider/
// ICurrentPrincipalAccessor/IPermissionEvaluator/IAuditRecorder/
// INotificationDispatcher/ICommandDispatcher/ICommandRegistry, registers
// its report definition and renderer, and demonstrates the full
// integration chain (generation gated by a permission that is now part of
// every session principal's fixed ApplicationPermissions.LocalSession set
// and so granted unconditionally, Settings-customised content, Audit
// recording, a Notifications completion notice) driven entirely by the
// real, unmodified module pipeline - mirroring
// AuditSampleModuleIntegrationTests/NotificationSampleModuleIntegrationTests'
// own structure.
public class ReportingSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        IConfigurationProvider configuration, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(ReportingSampleModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddInstance(configuration);
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<IReportingService, ReportingService>();
        services.Singleton<INotificationDispatcher, NotificationDispatcher>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();

        var currentPrincipalAccessor = new CurrentPrincipalAccessor();
        services.AddInstance<ICurrentPrincipalAccessor>(currentPrincipalAccessor);
        services.AddInstance(currentPrincipalAccessor);
        services.Singleton<IPermissionEvaluator, PermissionEvaluator>();

        // One store instance under all three shapes, as `TempestHost`
        // registers it (`ADR-0144`). The query shape is required since
        // `ADR-0145`: EngineeringDomainContext commits through it, and
        // AuditQuery answers a by-object lookup with a key prefix listing.
        var persistenceStore = new SqlitePersistenceStore(configuration);
        services.AddInstance<IPersistenceStore>(persistenceStore);
        services.AddInstance<IBinaryPersistenceStore>(persistenceStore);
        services.AddInstance<IQueryablePersistenceStore>(persistenceStore);
        services.Singleton<ISettingsProvider, SettingsProvider>();
        services.Singleton<IAuditRecorder, AuditRecorder>();
        services.Singleton<IAuditQuery, AuditQuery>();

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    private static IConfigurationProvider EmptyConfiguration(string persistenceRootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, persistenceRootPath),
        ])).Build();

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time registration
    // ----------------------------------------------------------------

    [Fact]
    public void ReportingSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(ReportingSampleModule));

        var module = serviceProvider.GetService(typeof(ReportingSampleModule));

        Assert.IsType<ReportingSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_EstablishesPrincipalAndRegistersDefinitionSettingAndCommand()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(ReportingSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<ReportingSampleModule>(serviceProvider.GetService(typeof(ReportingSampleModule)));
        Assert.True(module.HasRegistered);
        Assert.NotNull(module.EstablishedPrincipal);
        Assert.Equal(ReportingSampleModule.SampleIdentityId, module.EstablishedPrincipal!.Identity.Id);

        var reportingService = (IReportingService)serviceProvider.GetService(typeof(IReportingService));
        Assert.Contains(reportingService.RegisteredDefinitions, d => d.Id == SampleSummaryReportDefinition.ReportId);

        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        Assert.Contains(commandRegistry.Items, i => i.Id == ReportingSampleModule.GenerateSampleReportCommandId);
    }

    // ----------------------------------------------------------------
    // Report generation through the real ReportingService
    // ----------------------------------------------------------------

    [Fact]
    public async Task GeneratedReport_UsesTheGreetingSettingsCurrentValue()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(ReportingSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var settingsProvider = (ISettingsProvider)serviceProvider.GetService(typeof(ISettingsProvider));
        await settingsProvider.SetValueAsync(SampleSummaryReportRenderer.GreetingSettingKey, "Custom Greeting");

        var reportingService = (IReportingService)serviceProvider.GetService(typeof(IReportingService));
        var result = await reportingService.GenerateAsync(SampleSummaryReportDefinition.ReportId, new ReportRequest(new Dictionary<string, string>()));

        var text = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Custom Greeting", text);
    }

    // ----------------------------------------------------------------
    // Command: permission gating, Audit recording, Notifications
    //
    // `WP 17.2A` (ADR-0146): GenerateReportPermissionKey is part of every
    // session principal's fixed ApplicationPermissions.LocalSession set,
    // so the command is granted unconditionally - there is no longer a
    // configuration-driven grant mechanism, and so no denied-by-default
    // path to prove here.
    // ----------------------------------------------------------------

    [Fact]
    public async Task GenerateSampleReportCommand_PermissionGranted_ReportsSuccess()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            EmptyConfiguration(temp.Path), typeof(ReportingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(ReportingSampleModule.GenerateSampleReportCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Generated report", result.Message);
    }

    [Fact]
    public async Task GenerateSampleReportCommand_PermissionGranted_RecordsAnAuditEntry()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            EmptyConfiguration(temp.Path), typeof(ReportingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandRegistry.InvokeAsync(ReportingSampleModule.GenerateSampleReportCommandId, CancellationToken.None);

        var auditQuery = (IAuditQuery)serviceProvider.GetService(typeof(IAuditQuery));
        var records = await auditQuery.QueryAsync(new AuditQueryCriteria(
            actorId: ReportingSampleModule.SampleIdentityId, action: ReportingSampleModule.ReportGeneratedActionName));

        Assert.Single(records);
    }

    [Fact]
    public async Task GenerateSampleReportCommand_PermissionGranted_PublishesACompletionNotification()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            EmptyConfiguration(temp.Path), typeof(ReportingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var notificationDispatcher = (INotificationDispatcher)serviceProvider.GetService(typeof(INotificationDispatcher));
        var observed = new List<IPlatformNotification>();
        notificationDispatcher.Subscribe(new DelegatingNotificationHandler(n => observed.Add(n)));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandRegistry.InvokeAsync(ReportingSampleModule.GenerateSampleReportCommandId, CancellationToken.None);

        var notification = Assert.Single(observed);
        Assert.Equal(ReportingSampleModule.ReportGeneratedNotificationCategory, notification.Category);
        Assert.Equal(NotificationSeverity.Success, notification.Severity);
    }

    private sealed class DelegatingNotificationHandler : INotificationHandler<IPlatformNotification>
    {
        private readonly Action<IPlatformNotification> _onHandle;

        public DelegatingNotificationHandler(Action<IPlatformNotification> onHandle) => _onHandle = onHandle;

        public Task HandleAsync(IPlatformNotification notification, CancellationToken cancellationToken)
        {
            _onHandle(notification);
            return Task.CompletedTask;
        }
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithReportingSampleModule_GeneratesThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(ReportingSampleModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, temp.Path),
            ]))
            .Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        Assert.Equal(HostState.Running, host.State);

        var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

        var result = await registry.InvokeAsync(ReportingSampleModule.GenerateSampleReportCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("Generated report", result.Message);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }
}
