using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.ExportImport;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Notifications;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 6.7 end-to-end: ExportImportSampleModule constructor-injects
// the real, unmodified IIdentityService/ISettingsProvider/
// ICurrentPrincipalAccessor/IPermissionEvaluator/IAuditRecorder/
// INotificationDispatcher/IExportService/ImportService/ICommandDispatcher/
// ICommandRegistry, registers its two sample settings and adapters, and
// demonstrates the full integration chain (permission-gated export and
// import, multi-source round-trip through real Settings, Audit recording,
// a Notifications completion notice for each direction) driven entirely
// by the real, unmodified module pipeline - mirroring
// ReportingSampleModuleIntegrationTests' own structure.
[Collection("Console output capture")]
public class ExportImportSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        IConfigurationProvider configuration, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(ExportImportSampleModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddInstance(configuration);
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Events.RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<INotificationDispatcher, NotificationDispatcher>();
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
        services.Singleton<ISettingsProvider, SettingsProvider>();
        services.Singleton<IAuditRecorder, AuditRecorder>();
        services.Singleton<IAuditQuery, AuditQuery>();

        var exportFormat = new JsonExportFormat();
        services.AddInstance<IExportFormat>(exportFormat);
        services.Singleton<IExportService, ExportService>();

        var importService = new ImportService(exportFormat);
        services.AddInstance<IImportService>(importService);
        services.AddInstance(importService);

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    private static IConfigurationProvider EmptyConfiguration(string persistenceRootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
        ])).Build();

    private static IConfigurationProvider ConfigurationGrantingExportAndImportPermission(string persistenceRootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
            new KeyValuePair<string, string>(
                "Identity:Roles:ExportImporter:Permissions",
                $"{ExportImportSampleModule.ExportPermissionKey},{ExportImportSampleModule.ImportPermissionKey},{AuditQuery.QueryPermission.Key}"),
            new KeyValuePair<string, string>($"Identity:Principals:{ExportImportSampleModule.SampleIdentityId}:Roles", "ExportImporter"),
        ])).Build();

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time registration
    // ----------------------------------------------------------------

    [Fact]
    public void ExportImportSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(ExportImportSampleModule));

        var module = serviceProvider.GetService(typeof(ExportImportSampleModule));

        Assert.IsType<ExportImportSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_EstablishesPrincipalAndRegistersSettingsAdaptersAndCommands()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(ExportImportSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<ExportImportSampleModule>(serviceProvider.GetService(typeof(ExportImportSampleModule)));
        Assert.True(module.HasRegistered);
        Assert.NotNull(module.EstablishedPrincipal);
        Assert.Equal(ExportImportSampleModule.SampleIdentityId, module.EstablishedPrincipal!.Identity.Id);

        var settingsProvider = (ISettingsProvider)serviceProvider.GetService(typeof(ISettingsProvider));
        Assert.Equal(ExportImportSampleModule.GreetingSettingDefaultValue, await settingsProvider.GetValueAsync(ExportImportSampleModule.GreetingSettingKey));
        Assert.Equal(ExportImportSampleModule.SubtitleSettingDefaultValue, await settingsProvider.GetValueAsync(ExportImportSampleModule.SubtitleSettingKey));

        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        Assert.Contains(commandRegistry.Items, i => i.Id == ExportImportSampleModule.ExportCommandId);
        Assert.Contains(commandRegistry.Items, i => i.Id == ExportImportSampleModule.ImportCommandId);
    }

    // ----------------------------------------------------------------
    // Command: permission gating
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExportSampleDataCommand_NoPermissionGranted_ReportsDeniedByDefault()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(ExportImportSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(ExportImportSampleModule.ExportCommandId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("does not hold", result.Message);
    }

    [Fact]
    public async Task ImportSampleDataCommand_NoPermissionGranted_ReportsDeniedByDefault()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), typeof(ExportImportSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(ExportImportSampleModule.ImportCommandId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("does not hold", result.Message);
    }

    [Fact]
    public async Task ImportSampleDataCommand_NothingExportedYet_ReportsFailure()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingExportAndImportPermission(temp.Path), typeof(ExportImportSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(ExportImportSampleModule.ImportCommandId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("No artifact", result.Message);
    }

    // ----------------------------------------------------------------
    // Command: full export/import round trip through real Settings
    // ----------------------------------------------------------------

    [Fact]
    public async Task ExportThenImportSampleDataCommand_PermissionGranted_RoundTripsCustomisedSettingsThroughRealSettingsProvider()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingExportAndImportPermission(temp.Path), typeof(ExportImportSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var settingsProvider = (ISettingsProvider)serviceProvider.GetService(typeof(ISettingsProvider));
        await settingsProvider.SetValueAsync(ExportImportSampleModule.GreetingSettingKey, "Customised Greeting");
        await settingsProvider.SetValueAsync(ExportImportSampleModule.SubtitleSettingKey, "Customised Subtitle");

        var exportResult = await commandRegistry.InvokeAsync(ExportImportSampleModule.ExportCommandId, CancellationToken.None);
        Assert.True(exportResult.Succeeded);
        Assert.Contains("Exported 2 source(s)", exportResult.Message);

        await settingsProvider.SetValueAsync(ExportImportSampleModule.GreetingSettingKey, "Overwritten");
        await settingsProvider.SetValueAsync(ExportImportSampleModule.SubtitleSettingKey, "Overwritten");

        var importResult = await commandRegistry.InvokeAsync(ExportImportSampleModule.ImportCommandId, CancellationToken.None);
        Assert.True(importResult.Succeeded);

        Assert.Equal("Customised Greeting", await settingsProvider.GetValueAsync(ExportImportSampleModule.GreetingSettingKey));
        Assert.Equal("Customised Subtitle", await settingsProvider.GetValueAsync(ExportImportSampleModule.SubtitleSettingKey));
    }

    [Fact]
    public async Task ExportSampleDataCommand_PermissionGranted_RecordsAnAuditEntry()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingExportAndImportPermission(temp.Path), typeof(ExportImportSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandRegistry.InvokeAsync(ExportImportSampleModule.ExportCommandId, CancellationToken.None);

        var auditQuery = (IAuditQuery)serviceProvider.GetService(typeof(IAuditQuery));
        var records = await auditQuery.QueryAsync(new AuditQueryCriteria(
            actorId: ExportImportSampleModule.SampleIdentityId, action: ExportImportSampleModule.ExportedActionName));

        Assert.Single(records);
    }

    [Fact]
    public async Task ImportSampleDataCommand_PermissionGranted_RecordsAnAuditEntry()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingExportAndImportPermission(temp.Path), typeof(ExportImportSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandRegistry.InvokeAsync(ExportImportSampleModule.ExportCommandId, CancellationToken.None);
        await commandRegistry.InvokeAsync(ExportImportSampleModule.ImportCommandId, CancellationToken.None);

        var auditQuery = (IAuditQuery)serviceProvider.GetService(typeof(IAuditQuery));
        var records = await auditQuery.QueryAsync(new AuditQueryCriteria(
            actorId: ExportImportSampleModule.SampleIdentityId, action: ExportImportSampleModule.ImportedActionName));

        Assert.Single(records);
    }

    [Fact]
    public async Task ExportSampleDataCommand_PermissionGranted_PublishesACompletionNotification()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingExportAndImportPermission(temp.Path), typeof(ExportImportSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var notificationDispatcher = (INotificationDispatcher)serviceProvider.GetService(typeof(INotificationDispatcher));
        var observed = new List<IPlatformNotification>();
        notificationDispatcher.Subscribe(new DelegatingNotificationHandler(n => observed.Add(n)));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandRegistry.InvokeAsync(ExportImportSampleModule.ExportCommandId, CancellationToken.None);

        var notification = Assert.Single(observed);
        Assert.Equal(ExportImportSampleModule.NotificationCategory, notification.Category);
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
    public async Task RunAsync_WithExportImportSampleModule_RoundTripsThroughTheRealHost()
    {
        using var temp = new TempDirectory();
        var host = new TempestHostBuilder([typeof(ExportImportSampleModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, temp.Path),
                new KeyValuePair<string, string>(
                    "Identity:Roles:ExportImporter:Permissions",
                    $"{ExportImportSampleModule.ExportPermissionKey},{ExportImportSampleModule.ImportPermissionKey}"),
                new KeyValuePair<string, string>($"Identity:Principals:{ExportImportSampleModule.SampleIdentityId}:Roles", "ExportImporter"),
            ]))
            .Build();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            Assert.Equal(HostState.Running, host.State);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var exportResult = await registry.InvokeAsync(ExportImportSampleModule.ExportCommandId, CancellationToken.None);
            Assert.True(exportResult.Succeeded);

            var importResult = await registry.InvokeAsync(ExportImportSampleModule.ImportCommandId, CancellationToken.None);
            Assert.True(importResult.Succeeded);

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
