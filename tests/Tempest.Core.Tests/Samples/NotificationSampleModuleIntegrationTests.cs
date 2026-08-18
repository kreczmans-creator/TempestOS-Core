using Tempest.Core.Commands;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Notifications;
using Tempest.Core.Runtime;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 6.2 end-to-end: NotificationSampleModule constructor-injects
// the real, unmodified INotificationDispatcher/ICommandDispatcher/
// ICommandRegistry, subscribes to IPlatformNotification during its own
// initialisation, and demonstrates both a manually dispatched publish and
// a full-Host run in which the module observes
// NotificationSampleHostedService's own "started" notification -
// "Background notifications" proven concretely, not merely asserted -
// mirroring AuditSampleModuleIntegrationTests/SettingsSampleModuleIntegrationTests'
// own structure. Unlike Audit/Settings, Notifications has no Persistence
// or Identity dependency, so this pipeline needs neither a TempDirectory
// nor any principal/permission configuration.
[Collection("Console output capture")]
public class NotificationSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(NotificationSampleModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new Tempest.Core.Tests.Notifications.RecordingLevelLogger());
        services.Singleton<INotificationDispatcher, NotificationDispatcher>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time subscription
    // ----------------------------------------------------------------

    [Fact]
    public void NotificationSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        var (_, serviceProvider) = BuildPipeline(typeof(NotificationSampleModule));

        var module = serviceProvider.GetService(typeof(NotificationSampleModule));

        Assert.IsType<NotificationSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_SubscribesAndRegistersTheCommand()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(NotificationSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<NotificationSampleModule>(serviceProvider.GetService(typeof(NotificationSampleModule)));
        Assert.True(module.HasRegistered);
    }

    [Fact]
    public async Task Initialise_RegistersItsCommandDescriptor()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(NotificationSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Single(commandRegistry.Items);
        Assert.Contains(commandRegistry.Items, i => i.Id == NotificationSampleModule.PublishSampleNotificationCommandId);
    }

    // ----------------------------------------------------------------
    // Publish/subscribe round trip through the real dispatcher
    // ----------------------------------------------------------------

    [Fact]
    public async Task SubscribedModule_ObservesANotificationPublishedDirectlyThroughTheDispatcher()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(NotificationSampleModule));
        var dispatcher = (INotificationDispatcher)serviceProvider.GetService(typeof(INotificationDispatcher));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await dispatcher.PublishAsync<IPlatformNotification>(new PlatformNotification("Direct", NotificationSeverity.Information, "direct publish"));

        var module = Assert.IsType<NotificationSampleModule>(serviceProvider.GetService(typeof(NotificationSampleModule)));
        Assert.Contains(module.ObservedNotifications, n => n.Message == "direct publish");
    }

    [Fact]
    public async Task PublishSampleNotificationCommand_DispatchedTwice_BothObservedByTheModuleItself()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(NotificationSampleModule));
        var commandDispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandDispatcher.DispatchAsync(new PublishSampleNotificationCommand(NotificationSeverity.Success, "first"), CancellationToken.None);
        await commandDispatcher.DispatchAsync(new PublishSampleNotificationCommand(NotificationSeverity.Success, "second"), CancellationToken.None);

        var module = Assert.IsType<NotificationSampleModule>(serviceProvider.GetService(typeof(NotificationSampleModule)));
        Assert.Contains(module.ObservedNotifications, n => n.Message == "first");
        Assert.Contains(module.ObservedNotifications, n => n.Message == "second");
    }

    [Fact]
    public async Task PublishSampleNotificationCommand_Dispatched_ReportsSuccess()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(NotificationSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(NotificationSampleModule.PublishSampleNotificationCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host, proving
    // "Background notifications": NotificationSampleModule subscribes
    // during Module Initialisation (Phase 8), which completes before
    // NotificationSampleHostedService's own StartAsync (Phase 8.1) - so
    // the module reliably observes the hosted service's "started"
    // notification when both run together in the same Host.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithNotificationSampleModuleAndHostedService_ModuleObservesTheHostedServicesStartedNotification()
    {
        var host = new TempestHostBuilder(
                discoveryCandidateTypesOverride: [typeof(NotificationSampleModule)],
                pluginsRootPathOverride: null,
                hostedServiceCandidateTypesOverride: [typeof(NotificationSampleHostedService)])
            .Build();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            Assert.Equal(HostState.Running, host.State);

            var module = Assert.IsType<NotificationSampleModule>(host.Services!.GetService(typeof(NotificationSampleModule)));
            Assert.Contains(module.ObservedNotifications, n =>
                n.Category == NotificationSampleHostedService.Category && n.Message == NotificationSampleHostedService.StartedMessage);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_WithNotificationSampleModule_PublishSampleNotificationCommandInvokableThroughTheRealHost()
    {
        var host = new TempestHostBuilder([typeof(NotificationSampleModule)]).Build();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var result = await registry.InvokeAsync(NotificationSampleModule.PublishSampleNotificationCommandId, CancellationToken.None);

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
