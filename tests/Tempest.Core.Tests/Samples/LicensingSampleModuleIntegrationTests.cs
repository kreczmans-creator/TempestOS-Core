using System.Net;
using Tempest.Core.Api;
using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Licensing;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Notifications;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 6.6 end-to-end: LicensingSampleModule constructor-injects the
// real, unmodified IIdentityService/ISettingsProvider/
// ICurrentPrincipalAccessor/IPermissionEvaluator/ILicenseProvider/
// IAuditRecorder/INotificationDispatcher/IApiEndpointRegistry/
// ICommandDispatcher/ICommandRegistry, registers its setting, command,
// and route, and demonstrates the full integration chain (permission-
// gated capability evaluation, Settings-customised content on success,
// Audit recording, a Notifications completion notice, and a real HTTP
// round trip through the REST API) driven entirely by the real,
// unmodified module and Host pipeline - mirroring
// ReportingSampleModuleIntegrationTests'/ApiSampleModuleIntegrationTests'
// own structure.
[Collection("Console output capture")]
public class LicensingSampleModuleIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        IConfigurationProvider configuration, ILicense license, params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(LicensingSampleModule).Assembly])
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

        services.AddInstance<ILicenseProvider>(new LicenseProvider(license));

        services.Singleton<IPersistenceStore, PersistenceStore>();
        services.Singleton<ISettingsProvider, SettingsProvider>();
        services.Singleton<IAuditRecorder, AuditRecorder>();
        services.Singleton<IAuditQuery, AuditQuery>();
        services.Singleton<IApiEndpointRegistry, ApiEndpointRegistry>();

        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    private static ILicense UnlicensedLicense() => new License(LicenseValidator.UnlicensedLicenseeName, null, []);

    private static ILicense LicenseGranting(string capability) =>
        new License("Acme Corp", null, [capability]);

    private static IConfigurationProvider EmptyConfiguration(string persistenceRootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
        ])).Build();

    private static IConfigurationProvider ConfigurationGrantingCheckPermission(string persistenceRootPath) =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
            new KeyValuePair<string, string>(
                "Identity:Roles:CapabilityChecker:Permissions",
                $"{LicensingSampleModule.CapabilityCheckPermissionKey},{AuditQuery.QueryPermission.Key}"),
            new KeyValuePair<string, string>($"Identity:Principals:{LicensingSampleModule.SampleIdentityId}:Roles", "CapabilityChecker"),
        ])).Build();

    // ----------------------------------------------------------------
    // Constructor injection and initialise-time registration
    // ----------------------------------------------------------------

    [Fact]
    public void LicensingSampleModule_ResolvedThroughRealPipeline_ReceivesFunctioningCollaborators()
    {
        using var temp = new TempDirectory();
        var (_, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), UnlicensedLicense(), typeof(LicensingSampleModule));

        var module = serviceProvider.GetService(typeof(LicensingSampleModule));

        Assert.IsType<LicensingSampleModule>(module);
    }

    [Fact]
    public async Task Initialise_EstablishesPrincipalAndRegistersSettingCommandAndRoute()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), UnlicensedLicense(), typeof(LicensingSampleModule));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var module = Assert.IsType<LicensingSampleModule>(serviceProvider.GetService(typeof(LicensingSampleModule)));
        Assert.True(module.HasRegistered);
        Assert.NotNull(module.EstablishedPrincipal);
        Assert.Equal(LicensingSampleModule.SampleIdentityId, module.EstablishedPrincipal!.Identity.Id);

        var settingsProvider = (ISettingsProvider)serviceProvider.GetService(typeof(ISettingsProvider));
        Assert.Equal(LicensingSampleModule.PremiumMessageSettingDefaultValue, await settingsProvider.GetValueAsync(LicensingSampleModule.PremiumMessageSettingKey));

        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        Assert.Contains(commandRegistry.Items, i => i.Id == LicensingSampleModule.CheckCapabilityCommandId);

        var endpointRegistry = (IApiEndpointRegistry)serviceProvider.GetService(typeof(IApiEndpointRegistry));
        Assert.Contains(endpointRegistry.Routes, r => r.Path == LicensingSampleModule.CheckCapabilityRoutePath);
    }

    // ----------------------------------------------------------------
    // Command: permission gating, capability evaluation
    // ----------------------------------------------------------------

    [Fact]
    public async Task CheckSampleCapabilityCommand_NoPermissionGranted_ReportsDeniedByDefault()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(EmptyConfiguration(temp.Path), LicenseGranting(LicensingSampleModule.SampleCapabilityKey), typeof(LicensingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(LicensingSampleModule.CheckCapabilityCommandId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("does not hold", result.Message);
    }

    [Fact]
    public async Task CheckSampleCapabilityCommand_PermissionGranted_UnlicensedCapability_ReportsNotLicensed()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(ConfigurationGrantingCheckPermission(temp.Path), UnlicensedLicense(), typeof(LicensingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var result = await commandRegistry.InvokeAsync(LicensingSampleModule.CheckCapabilityCommandId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("is not enabled", result.Message);
        Assert.Contains(LicenseValidator.UnlicensedLicenseeName, result.Message);
    }

    [Fact]
    public async Task CheckSampleCapabilityCommand_PermissionGranted_LicensedCapability_ReportsSuccessWithSettingsMessage()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingCheckPermission(temp.Path), LicenseGranting(LicensingSampleModule.SampleCapabilityKey), typeof(LicensingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        var settingsProvider = (ISettingsProvider)serviceProvider.GetService(typeof(ISettingsProvider));
        await settingsProvider.SetValueAsync(LicensingSampleModule.PremiumMessageSettingKey, "Custom Premium Message");

        var result = await commandRegistry.InvokeAsync(LicensingSampleModule.CheckCapabilityCommandId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Custom Premium Message", result.Message);
    }

    [Fact]
    public async Task CheckSampleCapabilityCommand_LicensedCapability_RecordsAGrantedAuditEntry()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingCheckPermission(temp.Path), LicenseGranting(LicensingSampleModule.SampleCapabilityKey), typeof(LicensingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandRegistry.InvokeAsync(LicensingSampleModule.CheckCapabilityCommandId, CancellationToken.None);

        var auditQuery = (IAuditQuery)serviceProvider.GetService(typeof(IAuditQuery));
        var records = await auditQuery.QueryAsync(new AuditQueryCriteria(
            actorId: LicensingSampleModule.SampleIdentityId, action: LicensingSampleModule.CapabilityGrantedActionName));

        Assert.Single(records);
    }

    [Fact]
    public async Task CheckSampleCapabilityCommand_UnlicensedCapability_RecordsADeniedAuditEntry()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(ConfigurationGrantingCheckPermission(temp.Path), UnlicensedLicense(), typeof(LicensingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandRegistry.InvokeAsync(LicensingSampleModule.CheckCapabilityCommandId, CancellationToken.None);

        var auditQuery = (IAuditQuery)serviceProvider.GetService(typeof(IAuditQuery));
        var records = await auditQuery.QueryAsync(new AuditQueryCriteria(
            actorId: LicensingSampleModule.SampleIdentityId, action: LicensingSampleModule.CapabilityDeniedActionName));

        Assert.Single(records);
    }

    [Fact]
    public async Task CheckSampleCapabilityCommand_LicensedCapability_PublishesASuccessNotification()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(
            ConfigurationGrantingCheckPermission(temp.Path), LicenseGranting(LicensingSampleModule.SampleCapabilityKey), typeof(LicensingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var notificationDispatcher = (INotificationDispatcher)serviceProvider.GetService(typeof(INotificationDispatcher));
        var observed = new List<IPlatformNotification>();
        notificationDispatcher.Subscribe(new DelegatingNotificationHandler(n => observed.Add(n)));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandRegistry.InvokeAsync(LicensingSampleModule.CheckCapabilityCommandId, CancellationToken.None);

        var notification = Assert.Single(observed);
        Assert.Equal(LicensingSampleModule.NotificationCategory, notification.Category);
        Assert.Equal(NotificationSeverity.Success, notification.Severity);
    }

    [Fact]
    public async Task CheckSampleCapabilityCommand_UnlicensedCapability_PublishesAWarningNotification()
    {
        using var temp = new TempDirectory();
        var (runtimeManager, serviceProvider) = BuildPipeline(ConfigurationGrantingCheckPermission(temp.Path), UnlicensedLicense(), typeof(LicensingSampleModule));
        var commandRegistry = (ICommandRegistry)serviceProvider.GetService(typeof(ICommandRegistry));
        var notificationDispatcher = (INotificationDispatcher)serviceProvider.GetService(typeof(INotificationDispatcher));
        var observed = new List<IPlatformNotification>();
        notificationDispatcher.Subscribe(new DelegatingNotificationHandler(n => observed.Add(n)));
        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        await commandRegistry.InvokeAsync(LicensingSampleModule.CheckCapabilityCommandId, CancellationToken.None);

        var notification = Assert.Single(observed);
        Assert.Equal(LicensingSampleModule.NotificationCategory, notification.Category);
        Assert.Equal(NotificationSeverity.Warning, notification.Severity);
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
    // End-to-end execution through the real, unmodified Host and a real
    // HTTP round trip through the REST API
    // ----------------------------------------------------------------

    private static async Task<(RestApiHostedService HostedService, ITempestHost Host)> StartHostAsync(
        string persistenceRootPath, string? licenseFilePath, IEnumerable<string>? grantedPermissions = null)
    {
        var configurationEntries = new List<KeyValuePair<string, string>>
        {
            new(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
            new(RestApiHostedService.PortConfigurationKey, "0"),
        };

        var permissions = grantedPermissions?.ToList();
        if (permissions is { Count: > 0 })
        {
            configurationEntries.Add(new("Identity:Roles:CapabilityChecker:Permissions", string.Join(',', permissions)));
            configurationEntries.Add(new($"Identity:Principals:{LicensingSampleModule.SampleIdentityId}:Roles", "CapabilityChecker"));
        }

        var host = new TempestHostBuilder(
                discoveryCandidateTypesOverride: [typeof(LicensingSampleModule)],
                pluginsRootPathOverride: null,
                hostedServiceCandidateTypesOverride: [typeof(RestApiHostedService)],
                licenseFilePathOverride: licenseFilePath)
            .AddConfigurationSource(new MemoryConfigurationSource(configurationEntries))
            .Build();

        _ = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        var hostedService = (RestApiHostedService)host.Services!.GetService(typeof(RestApiHostedService));

        while (hostedService.BoundPort is null)
            await Task.Delay(5);

        return (hostedService, host);
    }

    [Fact]
    public async Task PostToMappedRoute_LicensedCapabilityAndGrantedPermission_Returns200()
    {
        using var temp = new TempDirectory();
        using var licenseDirectory = new TempDirectory();
        var licensePath = Path.Combine(licenseDirectory.Path, "license.json");
        File.WriteAllText(licensePath, $$"""{"LicenseeName":"Acme Corp","EnabledCapabilities":["{{LicensingSampleModule.SampleCapabilityKey}}"]}""");
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());
            var (hostedService, host) = await StartHostAsync(temp.Path, licensePath, [LicensingSampleModule.CapabilityCheckPermissionKey]);

            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{hostedService.BoundPort}/") };
            var request = new HttpRequestMessage(HttpMethod.Post, LicensingSampleModule.CheckCapabilityRoutePath);
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, LicensingSampleModule.SampleIdentityId);

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(LicensingSampleModule.PremiumMessageSettingDefaultValue, body);

            await host.StopAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task PostToMappedRoute_NoLicenseFile_Returns400_CapabilityNotEnabled()
    {
        using var temp = new TempDirectory();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());
            var (hostedService, host) = await StartHostAsync(temp.Path, licenseFilePath: null, [LicensingSampleModule.CapabilityCheckPermissionKey]);

            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{hostedService.BoundPort}/") };
            var request = new HttpRequestMessage(HttpMethod.Post, LicensingSampleModule.CheckCapabilityRoutePath);
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, LicensingSampleModule.SampleIdentityId);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            await host.StopAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task PostToMappedRoute_PermissionNotGranted_Returns403()
    {
        using var temp = new TempDirectory();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());
            var (hostedService, host) = await StartHostAsync(temp.Path, licenseFilePath: null, grantedPermissions: null);

            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{hostedService.BoundPort}/") };
            var request = new HttpRequestMessage(HttpMethod.Post, LicensingSampleModule.CheckCapabilityRoutePath);
            request.Headers.Add(ApiRequestHandler.IdentityHeaderName, LicensingSampleModule.SampleIdentityId);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            await host.StopAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
