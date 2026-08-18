using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Runtime;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 6.1 end-to-end: IdentitySampleModule constructor-injects the
// real, unmodified IIdentityService/ICurrentPrincipalAccessor/
// IPermissionEvaluator/ICommandDispatcher/ICommandRegistry and establishes
// a principal through them, driven entirely by the real, unmodified module
// pipeline - exactly the same composition
// DiagnosticsSampleModuleIntegrationTests already proves for Diagnostics.
// Nothing here is a mock or a test double standing in for a real platform
// service, except a level-recording ILogger used only to observe log
// output.
[Collection("Console output capture")]
public class IdentitySampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        IConfigurationProvider configuration, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(IdentitySampleModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
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

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    private static IConfigurationProvider EmptyConfiguration() =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([])).Build();

    private static IConfigurationProvider ConfigurationGrantingSamplePermission() =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Identity:Roles:SampleReader:Permissions", IdentitySampleModule.SamplePermissionKey),
            new KeyValuePair<string, string>($"Identity:Principals:{IdentitySampleModule.SampleIdentityId}:Roles", "SampleReader"),
        ])).Build();

    // ----------------------------------------------------------------
    // Constructor injection
    // ----------------------------------------------------------------

    [Fact]
    public void IdentitySampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        var (_, serviceProvider) = BuildPipeline(EmptyConfiguration(), typeof(IdentitySampleModule));

        var module = serviceProvider.GetService(typeof(IdentitySampleModule));

        Assert.IsType<IdentitySampleModule>(module);
    }

    // ----------------------------------------------------------------
    // Initialise-time establishment of the current principal
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_EstablishesTheSampleIdentityAsCurrentPrincipal()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(), typeof(IdentitySampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<IdentitySampleModule>(serviceProvider.GetService(typeof(IdentitySampleModule)));
        Assert.NotNull(module.EstablishedPrincipal);
        Assert.Equal(IdentitySampleModule.SampleIdentityId, module.EstablishedPrincipal!.Identity.Id);
        Assert.True(module.HasRegistered);

        var accessor = (ICurrentPrincipalAccessor)serviceProvider.GetService(typeof(ICurrentPrincipalAccessor));
        Assert.Same(module.EstablishedPrincipal, accessor.Current);
    }

    // ----------------------------------------------------------------
    // Command registration and invocation - both the fail-closed default
    // and the granted-permission path, against the same, unmodified module.
    // ----------------------------------------------------------------

    [Fact]
    public async Task Initialise_RegistersTheCheckSamplePermissionCommandDescriptor()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(), typeof(IdentitySampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var item = Assert.Single(commandRegistry.Items);
        Assert.Equal(IdentitySampleModule.CheckSamplePermissionCommandId, item.Id);
    }

    [Fact]
    public async Task CheckSamplePermissionCommand_NoRoleConfigured_ReportsDeniedByDefault()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(), typeof(IdentitySampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(
            IdentitySampleModule.CheckSamplePermissionCommandId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(IdentitySampleModule.SampleIdentityId, result.Message);
    }

    [Fact]
    public async Task CheckSamplePermissionCommand_RoleGrantingPermissionConfigured_ReportsSuccess()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingSamplePermission(), typeof(IdentitySampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(
            IdentitySampleModule.CheckSamplePermissionCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(IdentitySampleModule.SampleIdentityId, result.Message);
    }

    [Fact]
    public async Task CheckSamplePermissionCommand_DispatchedDirectly_Succeeds()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingSamplePermission(), typeof(IdentitySampleModule));
        var commandDispatcher = (ICommandDispatcher)serviceProvider.GetService(typeof(ICommandDispatcher));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandDispatcher.DispatchAsync(new CheckSamplePermissionCommand(), CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithIdentitySampleModuleAndGrantingConfiguration_EstablishesAndAuthorizesThroughTheRealHost()
    {
        var host = new TempestHostBuilder([typeof(IdentitySampleModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>("Identity:Roles:SampleReader:Permissions", IdentitySampleModule.SamplePermissionKey),
                new KeyValuePair<string, string>($"Identity:Principals:{IdentitySampleModule.SampleIdentityId}:Roles", "SampleReader"),
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

            var accessor = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
            Assert.Equal(IdentitySampleModule.SampleIdentityId, accessor.Current!.Identity.Id);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var result = await registry.InvokeAsync(
                IdentitySampleModule.CheckSamplePermissionCommandId, CancellationToken.None);
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
