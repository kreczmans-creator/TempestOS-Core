using Tempest.Core.Api;
using Tempest.Core.Audit;
using Tempest.Core.BackgroundServices;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.ExportImport;
using Tempest.Core.Identity;
using Tempest.Core.Input;
using Tempest.Core.Licensing;
using Tempest.Core.Logging;
using Tempest.Core.Macros;
using Tempest.Core.Materials;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Notifications;
using Tempest.Core.Persistence;
using Tempest.Core.Plugins;
using Tempest.Core.Reporting;
using Tempest.Core.Requirements;
using Tempest.Core.Settings;
using Tempest.Core.Verification;
using Tempest.Core.Versioning;

namespace Tempest.Core.Runtime;

/// <summary>
/// The concrete <see cref="ITempestHost"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Constructed only by <see cref="TempestHostBuilder"/> — the constructor is
/// <see langword="internal"/>, so no other component can construct the
/// runtime. Configuration, Logging, Discovery, Registration, and Dependency
/// Injection are all constructed directly by this class, in that order,
/// during <see cref="RunAsync"/> — see <c>Runtime Host Architecture.md</c>'s
/// "Relationship to Existing Services" section. Discovery, Registration, and
/// Lifecycle are held as private fields and never registered into the
/// dependency injection container (ADR-0017): a module has no path back into
/// the machinery orchestrating it.
/// </para>
/// <para>
/// <b>Startup cancellation and shutdown requests</b> (ADR-0014) are observed
/// through a single linked token for implementation simplicity — ADR-0014
/// explicitly permits satisfying both signals through one underlying trigger
/// without merging the concepts (see its Positive consequences) — but remain
/// two distinct triggers: the caller's own <see cref="CancellationToken"/>
/// passed to <see cref="RunAsync"/>, and an internal signal raised by
/// <see cref="StopAsync"/>. Both are handled identically once observed during
/// <see cref="HostState.Starting"/> (ADR-0018): control passes to the same
/// controlled-shutdown procedure used by a graceful, post-<see cref="HostState.Running"/>
/// stop. <see cref="RunAsync"/> only rethrows <see cref="OperationCanceledException"/>
/// when the caller's own token was the trigger — a shutdown requested via
/// <see cref="StopAsync"/> is a deliberate, successful stop, and
/// <see cref="RunAsync"/> completes normally for it, matching the established
/// .NET generic-host convention for exactly this scenario.
/// </para>
/// <para>
/// <b>Disposal is always an explicit, separate call</b> (ADR-0019):
/// <see cref="RunAsync"/> never disposes the host automatically, whether it
/// ends at <see cref="HostState.Stopped"/> or <see cref="HostState.Faulted"/>.
/// <see cref="DisposeAsync"/> is idempotent — safe to call more than once,
/// including once the host is already <see cref="HostState.Disposed"/> —
/// matching the standard <see cref="IAsyncDisposable"/> convention.
/// </para>
/// <para>
/// <b>Plugin trust and capability enforcement</b> (ADR-0110, ADR-0111,
/// ADR-0112, WP 13.2A): this class constructs and holds every new
/// Host-owned trust collaborator — <see cref="Plugins.PluginTrustStore"/>,
/// <see cref="Plugins.PluginComponentPrincipalRegistry"/>, and
/// <see cref="Identity.CurrentComponentAccessor"/> — and wires them into
/// <see cref="Plugins.PluginManifestDiscoveryService"/>,
/// <see cref="Plugins.PluginAssemblyLoader"/>, and
/// <see cref="Modules.ModuleLifecycleManager"/>'s own construction, alongside
/// the already-existing <see cref="Identity.IPermissionEvaluator"/>. None of
/// the three new collaborators is ever added to the DI
/// <see cref="DependencyInjection.ServiceCollection"/> (ADR-0017), mirroring
/// <see cref="Plugins.PluginRegistry"/>'s own established boundary.
/// </para>
/// </remarks>
public sealed class TempestHost : ITempestHost
{
    private readonly object _gate = new();
    private readonly IReadOnlyList<IConfigurationSource> _configurationSources;
    private readonly IEnumerable<Type>? _discoveryCandidateTypesOverride;
    private readonly string? _pluginsRootPathOverride;
    private readonly IEnumerable<Type>? _hostedServiceCandidateTypesOverride;
    private readonly string? _licenseFilePathOverride;
    private readonly bool _includeFaultInjectionModules;
    private readonly CancellationTokenSource _shutdownRequested = new();
    private readonly CancellationTokenSource _stopEscalation = new();
    private readonly TaskCompletionSource _runCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HostState _state = HostState.Created;
    private ILogger? _logger;
    private IRuntimeModuleManager? _moduleManager;
    private IModuleLifecycleManager? _lifecycleManager;
    private IHostedServiceManager? _hostedServiceManager;
    private ITempestServiceProvider? _services;

    internal TempestHost(
        IReadOnlyList<IConfigurationSource> configurationSources,
        IEnumerable<Type>? discoveryCandidateTypesOverride,
        string? pluginsRootPathOverride,
        IEnumerable<Type>? hostedServiceCandidateTypesOverride,
        string? licenseFilePathOverride,
        bool includeFaultInjectionModules = false)
    {
        _configurationSources = configurationSources;
        _discoveryCandidateTypesOverride = discoveryCandidateTypesOverride;
        _pluginsRootPathOverride = pluginsRootPathOverride;
        _hostedServiceCandidateTypesOverride = hostedServiceCandidateTypesOverride;
        _licenseFilePathOverride = licenseFilePathOverride;
        _includeFaultInjectionModules = includeFaultInjectionModules;
    }

    /// <inheritdoc />
    public HostState State
    {
        get
        {
            lock (_gate)
                return _state;
        }
    }

    /// <inheritdoc />
    public ITempestServiceProvider? Services
    {
        get
        {
            lock (_gate)
                return _services;
        }
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        EnterStarting();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownRequested.Token);
        var runToken = linkedCts.Token;

        try
        {
            await ExecuteStartupPhasesAsync(runToken).ConfigureAwait(false);

            EnterRunning();

            // Always throws: the only way out of Running is a cancellation,
            // either the caller's own token or an internal shutdown request.
            await AwaitShutdownSignalAsync(runToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var shutdownFault = await StopInternalAsync().ConfigureAwait(false);

            // A critical hosted service's StopAsync failure (ADR-0021/0029)
            // is Host-fatal and takes priority over the cancellation itself -
            // the Host ended Faulted, not Stopped, so RunAsync must reflect
            // that rather than completing as if shutdown succeeded.
            if (shutdownFault is not null)
                throw shutdownFault;

            // The caller's own token, as opposed to an internal shutdown
            // request raised via StopAsync(), is the only trigger this method
            // treats as a cancellation to propagate — a StopAsync() shutdown
            // is a deliberate, successful stop (ADR-0013, ADR-0018: never a
            // fault), and RunAsync completes normally for it, matching the
            // established .NET generic-host convention for this scenario.
            if (cancellationToken.IsCancellationRequested)
                throw;
        }
        catch (Exception ex)
        {
            EnterFaulted(ex);
            throw;
        }
        finally
        {
            _runCompletion.TrySetResult();
        }
    }

    private async Task ExecuteStartupPhasesAsync(CancellationToken runToken)
    {
        runToken.ThrowIfCancellationRequested();

        var configurationBuilder = new ConfigurationBuilder();

        foreach (var source in _configurationSources)
            configurationBuilder.AddSource(source);

        var configuration = configurationBuilder.Build();

        // ADR-0050: License validation runs here, before the DI container
        // (and even the logger) exists - Configuration's own value is
        // irrelevant to this check, since Licensing never reads
        // IConfigurationProvider itself (a fixed, documented file-path
        // convention, mirroring Plugin Manifest's own fixed convention).
        // An invalid license aborts startup immediately, Host-fatal, per
        // ADR-0013's existing platform-service-failure classification,
        // applied here without modification. A missing license file is
        // not itself invalid - it resolves to a valid, unrestricted-but-
        // uncapable default (see LicenseValidator's own remarks, and
        // ADR-0050's own resolution of Risk Register.md's R5).
        ILicenseValidator licenseValidator = _licenseFilePathOverride is not null
            ? new LicenseValidator(_licenseFilePathOverride)
            : new LicenseValidator();

        var licenseValidationResult = licenseValidator.Validate();

        if (!licenseValidationResult.IsValid)
        {
            Console.Error.WriteLine($"License validation failed: {licenseValidationResult.FailureReason}");
            throw new LicenseValidationException(licenseValidationResult.FailureReason!);
        }

        var currentLicense = licenseValidationResult.License!;

        ILogSink sink = new ConsoleLogSink();
        ILoggerFactory loggerFactory = new LoggerFactory(configuration, sink);
        var logger = loggerFactory.CreateLogger(LoggingServiceCollectionExtensions.DefaultLoggerCategory);
        _logger = logger;

        logger.Information("Host lifecycle phase completed: Configuration Built.");
        logger.Information("Host lifecycle phase completed: Logging Built.");
        logger.Information(
            $"Host lifecycle phase completed: License Validated. Licensee: '{currentLicense.LicenseeName}', " +
            $"{currentLicense.EnabledCapabilities.Count} capability(ies) enabled.");

        runToken.ThrowIfCancellationRequested();

        // ADR-0026: PlatformVersionProvider's construction moves here, ahead
        // of Plugin Discovery, since the MinimumPlatformVersion compatibility
        // check (ADR-0025, category 4) needs it. Its DI registration remains
        // at Platform Services Registered, below - construction and
        // registration are separable concerns, and only construction needed
        // to move.
        IPlatformVersionProvider platformVersionProvider = new PlatformVersionProvider(logger);

        // Plugin Platform Architecture.md, "Configurable Plugins Root and
        // Manifest Convention": Runtime:Plugins:RootDirectory/
        // ManifestFileName/Disabled are all optional configuration
        // overrides, resolved here since `configuration` is already built
        // and in scope. _pluginsRootPathOverride (the existing test-only
        // constructor field) takes precedence over configuration, exactly
        // as it already did before this override existed, preserving every
        // existing test's own determinism unchanged.
        // A configured-but-blank value (an empty string, or one that is only
        // whitespace) is treated as absent, not as a present-but-empty
        // override - PluginManifestDiscoveryService's own constructor
        // guards (ArgumentException.ThrowIfNullOrWhiteSpace) would otherwise
        // turn a single blank configuration entry (an empty environment
        // variable, a blank JSON field) into an uncaught exception here,
        // faulting the entire Host - not merely isolating one plugin -
        // exactly the kind of platform-wide failure a plugin-scoped
        // configuration mistake must never cause.
        var pluginsRootPath = _pluginsRootPathOverride
            ?? (configuration.TryGetValue("Runtime:Plugins:RootDirectory", out var configuredRoot) && !string.IsNullOrWhiteSpace(configuredRoot) ? configuredRoot : null)
            ?? Path.Combine(AppContext.BaseDirectory, "Plugins");

        var manifestFileName = configuration.TryGetValue("Runtime:Plugins:ManifestFileName", out var configuredManifestFileName) && !string.IsNullOrWhiteSpace(configuredManifestFileName)
            ? configuredManifestFileName
            : PluginManifestDiscoveryService.ManifestFileName;

        IReadOnlyCollection<string>? disabledPluginIds = configuration.TryGetValue("Runtime:Plugins:Disabled", out var configuredDisabled) && configuredDisabled is not null
            ? configuredDisabled.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : null;

        // ADR-0112: the operator's own explicit opt-in for Unsigned-Local
        // plugins to load at all. Absent or unparseable resolves to false -
        // ADR-0112's own table names this the safe default (fail closed,
        // mirroring ADR-0043's identical fail-closed precedent for an
        // unrecognised identity).
        var allowUnsignedLoad = configuration.TryGetValue("Plugins:AllowUnsignedLoad", out var rawAllowUnsigned)
            && bool.TryParse(rawAllowUnsigned, out var parsedAllowUnsigned)
            && parsedAllowUnsigned;

        // Plugin Platform Architecture.md, "Plugin Registry": Host-owned,
        // constructed immediately before Plugin Discovery ever runs, so
        // both Discovery and Loading can record every candidate's outcome
        // into it as they go. Never added to the DI ServiceCollection
        // (ADR-0017's own Host-owned-collaborator boundary, applied to a
        // fourth collaborator) — only IDiagnosticsProvider.Plugins, the
        // DI-public read-only projection, ever reaches a module.
        var pluginRegistry = new PluginRegistry();

        // ADR-0112: the local, flat-file trust store (TrustedPublishers/,
        // fixed convention relative to AppContext.BaseDirectory) a signed
        // candidate's PublisherCertificateThumbprint is resolved against.
        // Host-owned, alongside pluginRegistry, for the identical reason.
        var pluginTrustStore = new PluginTrustStore(logger);

        // ADR-0111: the small, Host-owned registry mapping a discovered
        // IModule Type back to the plugin's own component principal that
        // owns it - written once, by PluginAssemblyLoader, for every plugin
        // whose two static trust checks both pass; read later by the
        // componentScopeProvider closure passed to ModuleLifecycleManager,
        // below. Never added to the DI ServiceCollection, for the same
        // ADR-0017 reason as pluginRegistry/pluginTrustStore.
        var componentPrincipalRegistry = new PluginComponentPrincipalRegistry();

        // WP 13.9.4: the small, Host-owned registry recording every
        // discovered IModule or IHostedService Type belonging to a plugin
        // PluginAssemblyLoader denies trust to - written once, by
        // PluginAssemblyLoader, for every plugin either static trust check
        // rejects; read twice, below, by Module Registration's own filter
        // AND Hosted Service Registration's own filter - closing the gap
        // where a denied plugin's already-loaded assembly (ADR-0015: that
        // step cannot be undone) could still be separately rediscovered and
        // fully lifecycle-run/started by Module Discovery or Hosted Service
        // Discovery (both deliberately plugin-unaware, ADR-0110). One
        // registry covers both pipelines - a single Type can implement both
        // IModule and IHostedService, and denial must exclude it from
        // whichever pipeline(s) would otherwise have found it. Never added
        // to the DI ServiceCollection, for the identical ADR-0017 reason as
        // componentPrincipalRegistry.
        var deniedTypeRegistry = new PluginDeniedTypeRegistry();

        // ADR-0111: the second, component-scoped identity axis, distinct
        // from CurrentPrincipalAccessor's own user-scoped one (constructed
        // below, at Platform Services Registered). Constructed here, ahead
        // of Plugin Discovery, mirroring CurrentPrincipalAccessor's own
        // early-construction convention - EventBus's own construction
        // (Platform Services Registered, Phase 6, later) is what actually
        // needs it; Plugin Discovery/Loading do not read it directly.
        var currentComponentAccessor = new CurrentComponentAccessor();

        var pluginDiscoveryService = new PluginManifestDiscoveryService(
            pluginsRootPath, platformVersionProvider, logger, manifestFileName, disabledPluginIds, pluginRegistry,
            pluginTrustStore, allowUnsignedLoad);

        var pluginManifests = pluginDiscoveryService.DiscoverManifests();
        logger.Information($"Host lifecycle phase completed: Plugin Discovery. {pluginManifests.Count} plugin(s) eligible.");

        runToken.ThrowIfCancellationRequested();

        // ADR-0110/ADR-0111: componentPrincipalRegistry is passed as the
        // IPluginComponentPrincipalRecorder write side only - the loader
        // records a trust-checked plugin's own component principal against
        // each of its discovered IModule types here. WP 13.9.4:
        // deniedTypeRegistry is passed as the IPluginDeniedTypeRecorder write
        // side - the loader records every discovered IModule and
        // IHostedService type belonging to a denied plugin here; Module
        // Discovery's and Hosted Service Discovery's own scans below remain
        // entirely unchanged and still plugin-unaware (ADR-0110) - only
        // Module Registration and Hosted Service Registration, further
        // below, are filtered against what this registry records.
        IPluginAssemblyLoader pluginAssemblyLoader = new PluginAssemblyLoader(
            logger, pluginRegistry, componentPrincipalRegistry, deniedTypeRegistry);
        var loadedPluginAssemblies = pluginAssemblyLoader.LoadPlugins(pluginManifests);
        logger.Information($"Host lifecycle phase completed: Plugin Loading. {loadedPluginAssemblies.Count} plugin assembly(ies) loaded.");

        runToken.ThrowIfCancellationRequested();

        // WP 13.9.6: isTypeExcluded is wired to deniedTypeRegistry.IsDenied,
        // already fully populated by Plugin Loading, above - closing the
        // trust boundary gap the WP 13.9.4 filters below could not: an
        // unattributed IModule type belonging to a denied plugin was
        // previously still constructed via Activator.CreateInstance inside
        // CreateDescriptor, during Module Discovery itself, strictly before
        // either filter below is ever consulted (a genuine, live constructor
        // execution for a denied plugin's code), and - if that same type also
        // lacked a public parameterless constructor - threw an uncaught
        // ModuleDiscoveryException that faulted the whole Host. Both are
        // closed by this one predicate; ReflectionFrameworkDiscoveryService
        // itself gains no plugin awareness (ADR-0110) - see its own remarks.
        var discovery = new ReflectionFrameworkDiscoveryService(
            logger, includeFaultInjectionModules: _includeFaultInjectionModules, isTypeExcluded: deniedTypeRegistry.IsDenied);

        var descriptors = _discoveryCandidateTypesOverride is not null
            ? discovery.DiscoverModules(_discoveryCandidateTypesOverride)
            : discovery.DiscoverModules();

        logger.Information("Host lifecycle phase completed: Module Discovery.");

        runToken.ThrowIfCancellationRequested();

        var moduleManager = new RuntimeModuleManager(logger);

        // WP 13.9.4: the trust-denial execution boundary. A descriptor whose
        // ModuleType was recorded by deniedTypeRegistry belongs to a plugin
        // PluginAssemblyLoader already denied trust - its assembly remains
        // resident in the process (ADR-0015: load cannot be undone) and
        // Module Discovery, immediately above, is deliberately plugin-unaware
        // (ADR-0110) and so still found it - but it must never reach Module
        // Registration, and therefore never InitialiseAsync/StartAsync, and
        // therefore never Command/Navigation/Event registration (all only
        // reachable from inside a running module body). Hosted Service
        // Registration, further below, is filtered identically -
        // ReflectionFrameworkDiscoveryService, RuntimeModuleManager,
        // ModuleLifecycleManager, HostedServiceDiscoveryService, and
        // IHostedServiceManager themselves gain no trust awareness at all -
        // these two filters are the only new logic, living entirely in this
        // orchestration method, exactly where componentScopeProvider (below)
        // already threads plugin-relevant data through otherwise fully
        // generic machinery.
        var deniedCount = 0;

        foreach (var descriptor in descriptors)
        {
            if (deniedTypeRegistry.IsDenied(descriptor.ModuleType))
            {
                deniedCount++;
                logger.Warning(
                    $"Module '{descriptor.ModuleType.FullName}' excluded from Module Registration: " +
                    "its own plugin was denied trust (ADR-0110/ADR-0111/WP 13.9.4).");
                continue;
            }

            moduleManager.Register(descriptor);
        }

        if (deniedCount > 0)
            logger.Warning($"{deniedCount} module(s) excluded from Module Registration due to plugin trust denial.");

        _moduleManager = moduleManager;
        logger.Information("Host lifecycle phase completed: Module Registration.");

        runToken.ThrowIfCancellationRequested();

        var hostedServiceDiscovery = new HostedServiceDiscoveryService(logger);

        var discoveredHostedServiceTypes = _hostedServiceCandidateTypesOverride is not null
            ? hostedServiceDiscovery.DiscoverHostedServiceTypes(_hostedServiceCandidateTypesOverride)
            : hostedServiceDiscovery.DiscoverHostedServiceTypes();

        // WP 13.9.4: the identical trust-denial execution boundary applied
        // to Module Registration, above, applied here to Hosted Service
        // Registration - a second, wholly independent discovery/registration
        // pipeline (HostedServiceDiscoveryService/IHostedServiceManager) a
        // denied plugin's already-loaded assembly could otherwise still
        // reach, even for a type that ALSO implements IModule and was
        // already correctly excluded above - deniedTypeRegistry is keyed on
        // Type alone, covering both pipelines from the one recording pass.
        var hostedServiceTypes = new List<Type>();
        var deniedHostedServiceCount = 0;

        foreach (var hostedServiceType in discoveredHostedServiceTypes)
        {
            if (deniedTypeRegistry.IsDenied(hostedServiceType))
            {
                deniedHostedServiceCount++;
                logger.Warning(
                    $"Hosted service '{hostedServiceType.FullName}' excluded from Hosted Service Registration: " +
                    "its own plugin was denied trust (ADR-0110/ADR-0111/WP 13.9.4).");
                continue;
            }

            hostedServiceTypes.Add(hostedServiceType);
        }

        if (deniedHostedServiceCount > 0)
        {
            logger.Warning(
                $"{deniedHostedServiceCount} hosted service(s) excluded from Hosted Service Registration due to " +
                "plugin trust denial.");
        }

        var services = new ServiceCollection(logger);
        services.AddInstance(configuration);
        services.AddInstance(sink);
        services.AddInstance(loggerFactory);
        services.AddInstance(logger);
        services.AddInstance(platformVersionProvider);
        // ADR-0110/ADR-0111: EventBus, NavigationService, CommandHandlerTable,
        // and CommandRegistry each gained new, optional, trailing constructor
        // parameters (a component-scope accessor and/or IPermissionEvaluator)
        // for the trust-ordered registration rule and capability-gated
        // publish/register checks. No change is needed at these registration
        // lines themselves - see currentComponentAccessor's own dual
        // registration, below, and its remarks on lazy constructor-parameter
        // resolution.
        services.Singleton<IEventBus, EventBus>();
        services.Singleton<IReportingService, ReportingService>();
        services.Singleton<INotificationDispatcher, NotificationDispatcher>();
        services.Singleton<INavigationProvider, NavigationService>();
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();

        // ADR-0044: CurrentPrincipalAccessor is constructed directly, once,
        // and registered under both its own concrete type and
        // ICurrentPrincipalAccessor - the same already-built instance under
        // two service-type keys - so IdentityService (which needs write
        // access via the concrete type) and every ordinary consumer
        // (which resolves only the read-only interface) share the exact
        // same object, never two independently-constructed ones. See
        // CurrentPrincipalAccessor's own remarks.
        var currentPrincipalAccessor = new CurrentPrincipalAccessor();
        services.AddInstance<ICurrentPrincipalAccessor>(currentPrincipalAccessor);
        services.AddInstance(currentPrincipalAccessor);
        services.Singleton<IRoleProvider, RoleProvider>();
        services.Singleton<IPermissionEvaluator, PermissionEvaluator>();
        services.Singleton<IIdentityService, IdentityService>();

        // ADR-0111: currentComponentAccessor was already constructed above,
        // ahead of Plugin Discovery - registered here, under both its own
        // concrete type (EventBus's own constructor needs the concrete type
        // specifically, to call BeginScope) and ICurrentComponentAccessor
        // (NavigationService/CommandRegistry/CommandHandlerTable only ever
        // need the read-only interface), mirroring currentPrincipalAccessor's
        // own dual-registration pattern immediately above. IPermissionEvaluator
        // is already registered above (WP 6.1) - NavigationService,
        // CommandRegistry, CommandHandlerTable, and EventBus (registered
        // below) each resolve it, and currentComponentAccessor, through
        // their own new, optional constructor parameters automatically:
        // TempestServiceProvider resolves every constructor parameter type
        // lazily, at first resolution, not at Singleton<> registration time
        // (see ServiceCollection.cs/TempestServiceProvider.cs) - so no
        // change is needed at any of those types' own Singleton<> lines
        // below beyond what construction-time resolution already provides.
        services.AddInstance<ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);

        // ADR-0050: Licensing's ILicenseProvider wraps the already-
        // validated license from before Phase 1 - registered via
        // AddInstance, never container-constructed, exactly like
        // IPlatformVersionProvider and IDiagnosticsProvider below.
        ILicenseProvider licenseProvider = new LicenseProvider(currentLicense);
        services.AddInstance(licenseProvider);

        // ADR-0041: Persistence is established here, as part of Settings'
        // own scope, ahead of Settings' own registration so the container
        // can resolve IPersistenceStore for SettingsProvider's constructor.
        services.Singleton<IPersistenceStore, PersistenceStore>();
        services.Singleton<ISettingsProvider, SettingsProvider>();

        // WP 10.6A / ADR-0098 / ADR-0100: the Macro foundation and the
        // External Controller/Input Binding abstraction — both ordinary
        // Platform Services, registered here alongside the rest of the
        // Command Framework's own supporting cast. MacroManager needs
        // ISettingsProvider (just registered above) and ICommandRegistry
        // (registered earlier in this method); neither is constructed
        // eagerly here, so registration order only needs to precede first
        // resolution, not construction.
        services.Singleton<IMacroManager, MacroManager>();
        services.Singleton<IInputBindingRegistry, InputBindingRouter>();

        // ADR-0041/ADR-0045: Audit reuses the same IPersistenceStore
        // Settings established, rather than introducing a second
        // storage mechanism - registered after Persistence and Identity
        // & Permissions, both of which it depends on.
        services.Singleton<IAuditRecorder, AuditRecorder>();
        services.Singleton<IAuditQuery, AuditQuery>();

        // ADR-0047: the REST API's own hosted-service scaffold is
        // registered separately, via hosted service discovery, below -
        // IApiEndpointRegistry itself is an ordinary Phase 6 singleton,
        // resolvable by any module wanting to map a route during its own
        // initialisation, before the hosted service itself ever starts
        // listening.
        services.Singleton<IApiEndpointRegistry, ApiEndpointRegistry>();

        // ADR-0051: Export/Import reads from whatever service owns the
        // exported data (Settings, Reporting) via that service's own
        // public interface, never IPersistenceStore directly - registered
        // last among the ordinary Phase 6 singletons, needing nothing but
        // Dependency Injection itself.
        //
        // ImportService is constructed directly, once, and registered
        // under both its own concrete type and IImportService - the same
        // already-built instance under two service-type keys - mirroring
        // ADR-0044's own dual-registration precedent for
        // CurrentPrincipalAccessor: a module needing RegisterImportable
        // resolves the concrete type, while every ordinary consumer
        // resolves only the read-only IImportService interface, both
        // against the exact same object.
        var exportFormat = new JsonExportFormat();
        services.AddInstance<IExportFormat>(exportFormat);
        services.Singleton<IExportService, ExportService>();

        var importService = new ImportService(exportFormat, logger);
        services.AddInstance<IImportService>(importService);
        services.AddInstance(importService);

        // ADR-0053: the Engineering Data Model is built directly on the
        // same IPersistenceStore Settings/Audit already established,
        // rather than introducing a second storage mechanism - registered
        // after Persistence and Identity & Permissions, both of which it
        // depends on, mirroring Audit's own placement rationale.
        services.Singleton<IEngineeringDocumentStore, EngineeringDocumentStore>();

        // WP 8.2C: the Engineering Domain's own shared services sit between
        // the Engineering Data Model and every discipline framework
        // (WP8.2B Dependency Rules.md §1/§5) - registered directly after
        // IEngineeringDocumentStore, which every one of them ultimately
        // depends on (Repository/RelationshipRepository do not - they are
        // a new, purely in-memory index, never a competing storage
        // mechanism). No discipline-specific Kind or business rule is
        // registered here - this is the shared vocabulary layer only.
        services.Singleton<IEngineeringObjectRepository, InMemoryEngineeringObjectRepository>();
        services.Singleton<IEngineeringRelationshipRepository, InMemoryEngineeringRelationshipRepository>();
        services.Singleton<ILifecycleTransitionTable, LifecycleTransitionTable>();
        services.Singleton<IValidationRuleSet, ValidationRuleSet>();
        services.Singleton<IReferenceIntegrityChecker, ReferenceIntegrityChecker>();

        // RelationshipDiscoveryService realises all three digital-thread
        // interfaces (it is stateless, delegating only to the two
        // repositories above, themselves the real singletons) - registered
        // once per interface rather than dual-registered as one shared
        // instance, since no shared mutable state exists for callers to
        // observe diverging.
        services.Singleton<IRelationshipDiscovery, RelationshipDiscoveryService>();
        services.Singleton<IDependencyTraversal, RelationshipDiscoveryService>();
        services.Singleton<IImpactAnalysis, RelationshipDiscoveryService>();
        services.Singleton<IEvidenceComposer, EvidenceComposer>();

        // TD-87/ADR-0120: the migration chain(s) EngineeringObjectStateStore's
        // own read path walks. Registered before that store, which takes
        // it as an optional collaborator - empty until a Kind's own
        // declaring class registers a migration onto it, the same
        // "empty is a legal, zero-cost default" shape
        // IEngineeringObjectRehydratorRegistry already has.
        services.Singleton<IStateMigrationRegistry, StateMigrationRegistry>();

        // TD-87/ADR-0120: EngineeringObjectStateStore's own optional
        // `int? targetSchemaVersion` constructor parameter (see that
        // class's own remarks) has no default-parameter-value support in
        // this container - every constructor parameter, whatever its C#
        // default, is resolved through this same registry (see
        // TempestServiceProvider's own "Dependency resolution" remarks).
        // AddInstance is this container's own established answer for "a
        // value the container cannot construct on its own"; registering
        // it here, unconditionally CurrentSchemaVersion, keeps this call
        // producing exactly the store's own default target, unchanged.
        services.AddInstance(typeof(int?), (object)(int?)EngineeringObjectStateStore.CurrentSchemaVersion);

        // TD-85: the durable half of the Engineering Domain. Registered
        // before EngineeringDomainContext, which takes it as a
        // collaborator. Built on the same IPersistenceStore
        // IEngineeringDocumentStore already uses (ADR-0053) - one
        // persistence authority, split by concern (the document owns
        // identity, Kind and revisions; this owns the object state a
        // document was never designed to carry), never a second one.
        services.Singleton<IEngineeringObjectStateStore, EngineeringObjectStateStore>();

        // TD-31: the durable bytes of an attached file. Registered here for
        // the same reason and on the same terms as the state store above -
        // the same single persistence store, in its byte shape
        // (IBinaryPersistenceStore), with its own collection. The metadata
        // stays on the object; only the content lives here, so rehydrating
        // a whole object graph never loads a file.
        services.Singleton<IBinaryPersistenceStore, PersistenceStore>();
        services.Singleton<IAttachmentContentStore, AttachmentContentStore>();

        // TD-85: the Kind-to-type map startup rehydration resolves through.
        // Empty until each Kind's own declaring class registers it -
        // nothing here declares a Kind of its own (ADR-0105).
        services.Singleton<IEngineeringObjectRehydratorRegistry, EngineeringObjectRehydratorRegistry>();

        // The shared collaborator bundle every canonical object's own
        // EngineeringObjectFactory<T> needs - constructed here so a
        // composition root (a future discipline module, or the sample
        // module below) can resolve one instance rather than assembling
        // seven collaborators by hand.
        services.Singleton<EngineeringDomainContext>();

        // TD-85: rebuilds the live object graph from the two stores above
        // at startup. Registered after EngineeringDomainContext, which it
        // reads through; it stores nothing of its own.
        services.Singleton<EngineeringObjectRehydrationService>();

        // ADR-0055: Materials is a thin, typed index over the Engineering
        // Data Model (Kind = "MaterialSpecification"), plus a direct
        // IPersistenceStore dependency of its own for the materialId
        // index IEngineeringDocumentStore's own contract has no lookup-by-
        // arbitrary-string capability to provide - registered after both,
        // which it depends on.
        services.Singleton<IMaterialCatalog, MaterialCatalog>();

        // ADR-0056: every calculation execution is durably recorded as an
        // Engineering Data Model document (Kind = "CalculationRecord"),
        // mirroring Materials' own reuse of IEngineeringDocumentStore -
        // registered after it, which it depends on. No direct
        // IPersistenceStore dependency is needed here, unlike Materials:
        // each execution always creates a brand new document, never
        // looked up later by a caller-chosen key.
        services.Singleton<ICalculationEngine, CalculationEngine>();

        // ADR-0057: verification history is queried through the
        // Engineering Data Model's own existing LinkAsync/
        // GetReferencesAsync mechanism, not a new index - registered
        // after Engineering Data and Identity & Permissions, both of
        // which it depends on. Read access is permission-gated,
        // mirroring IAuditQuery's own established pattern.
        services.Singleton<IVerificationService, VerificationService>();

        // ADR-0058: the Requirements Engine is a thin, typed index over
        // the Engineering Data Model (Kind = "Requirement" and two
        // sibling kinds), plus a direct IPersistenceStore dependency of
        // its own for its identifier index, mirroring Materials' own
        // materialId index - registered after Engineering Data and
        // Verification, both of which it depends on (Verification for
        // its own GetEvidenceAsync aggregation).
        services.Singleton<IRequirementsService, RequirementsService>();

        // WP 9.1A: Requirements-specific validation - a thin read-only
        // service over IRequirementsService itself, registered directly
        // after it, which it depends on. Reuses EngineeringDomain's own
        // IValidationResult/IValidationDiagnostic for its result shape only
        // - IValidationRule itself is scoped to IEngineeringObject, which
        // no Requirements type implements (ADR-0084).
        services.Singleton<IRequirementValidationService, RequirementValidationService>();

        // Composition Root pattern (ADR-0009), like Configuration/Logging/
        // PlatformVersionProvider above: DiagnosticsProvider needs references
        // to _lifecycleManager/_hostedServiceManager, both Host-owned and
        // never added to this container (ADR-0017), and neither constructed
        // yet at this point in the phase table - so it is built here,
        // directly, with Func<T> accessors closing over this instance's own
        // fields, and registered as an already-constructed instance rather
        // than a container-constructed singleton. See ADR-0039.
        IDiagnosticsProvider diagnosticsProvider = new DiagnosticsProvider(
            () => State,
            () => { lock (_gate) return _lifecycleManager; },
            () => { lock (_gate) return _hostedServiceManager; },
            pluginRegistry);
        services.AddInstance(diagnosticsProvider);

        services.AddDiscoveredModules(moduleManager.GetAll().Select(module => module.Descriptor));
        services.AddDiscoveredHostedServices(hostedServiceTypes);
        logger.Information(
            $"Host lifecycle phase completed: Platform Services Registered. " +
            $"{hostedServiceTypes.Count} hosted service(s) discovered.");

        runToken.ThrowIfCancellationRequested();

        ITempestServiceProvider serviceProvider = new TempestServiceProvider(services, logger);

        lock (_gate)
            _services = serviceProvider;

        logger.Information("Host lifecycle phase completed: Dependency Injection Built.");

        runToken.ThrowIfCancellationRequested();

        // ADR-0111: given a module Id, resolves the owning plugin's own
        // component principal (if any - null for a genuine first-party
        // module, or for a plugin whose types never made it past trust
        // enforcement in PluginAssemblyLoader) and pushes it onto
        // currentComponentAccessor for the duration of one lifecycle call.
        // A linear scan per call is acceptable here - module counts are
        // small, this is not a hot path comparable to per-request REST
        // handling.
        Func<string, IDisposable?> componentScopeProvider = moduleId =>
        {
            var descriptor = descriptors.FirstOrDefault(d => d.Id == moduleId);

            if (descriptor is null)
                return null;

            var principal = componentPrincipalRegistry.GetPrincipalFor(descriptor.ModuleType);

            return principal is not null ? currentComponentAccessor.BeginScope(principal) : null;
        };

        var lifecycleManager = new ModuleLifecycleManager(moduleManager, serviceProvider, logger, componentScopeProvider);
        _lifecycleManager = lifecycleManager;

        await lifecycleManager.InitialiseAllAsync(runToken).ConfigureAwait(false);
        await lifecycleManager.StartAllAsync(runToken).ConfigureAwait(false);
        logger.Information("Host lifecycle phase completed: Module Initialisation.");

        runToken.ThrowIfCancellationRequested();

        // WP 13.10B / TD-51: the identical component-scope mechanism
        // componentScopeProvider (above) already gives ModuleLifecycleManager,
        // extended to HostedServiceManager - a plugin's own hosted service
        // previously ran with no ambient component principal at all (null,
        // treated as First-Party), even when the plugin genuinely passed
        // trust enforcement. Hosted services are natively Type-keyed (no
        // string Id concept exists for one), so this closure takes the
        // service's own Type directly - no moduleId-to-descriptor lookup
        // step is needed, unlike componentScopeProvider above.
        // componentPrincipalRegistry is now populated for hosted-service
        // types too (PluginAssemblyLoader.EnforceTrust, WP 13.10B) - null
        // here for a genuine first-party hosted service, or for a plugin's
        // hosted service whose own types never made it past trust
        // enforcement, identically to the module case.
        Func<Type, IDisposable?> hostedServiceComponentScopeProvider = serviceType =>
        {
            var principal = componentPrincipalRegistry.GetPrincipalFor(serviceType);

            return principal is not null ? currentComponentAccessor.BeginScope(principal) : null;
        };

        var hostedServiceManager = new HostedServiceManager(
            hostedServiceTypes, serviceProvider, logger, hostedServiceComponentScopeProvider);
        _hostedServiceManager = hostedServiceManager;

        await hostedServiceManager.StartAllAsync(runToken).ConfigureAwait(false);
        logger.Information("Host lifecycle phase completed: Hosted Services Started.");
    }

    private static async Task AwaitShutdownSignalAsync(CancellationToken runToken)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using (runToken.Register(() => signal.TrySetResult()))
        {
            await signal.Task.ConfigureAwait(false);
        }

        runToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        bool alreadyTerminal;

        lock (_gate)
        {
            if (_state == HostState.Created)
                throw new InvalidHostStateTransitionException(_state, "Stop");

            if (_state == HostState.Disposed)
                throw new InvalidHostStateTransitionException(_state, "Stop");

            alreadyTerminal = _state is HostState.Stopped or HostState.Faulted;
        }

        if (alreadyTerminal)
            return;

        try
        {
            if (!_shutdownRequested.IsCancellationRequested)
                _shutdownRequested.Cancel();
            else if (!_stopEscalation.IsCancellationRequested)
                _stopEscalation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The host finished disposing between the state check above and
            // this call - nothing further to signal.
            return;
        }

        await _runCompletion.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        HostState stateAtEntry;

        lock (_gate)
        {
            stateAtEntry = _state;

            if (stateAtEntry == HostState.Disposed)
                return;
        }

        if (stateAtEntry is HostState.Starting or HostState.Running or HostState.Stopping)
        {
            try
            {
                if (!_shutdownRequested.IsCancellationRequested)
                    _shutdownRequested.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            await _runCompletion.Task.ConfigureAwait(false);

            lock (_gate)
                stateAtEntry = _state;
        }

        if (stateAtEntry != HostState.Stopped && _hostedServiceManager is not null)
        {
            try
            {
                await _hostedServiceManager.StopAllAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // A second, critical hosted-service failure discovered during
                // post-fault teardown itself must not prevent the Host from
                // still reaching Disposed - the original fault is already
                // recorded; this is logged, not rethrown (FOUNDATION.md
                // principle 5: cleanup is always guaranteed, never
                // conditional on how far execution got).
                _logger?.Critical(
                    "A critical hosted service failed to stop during post-fault teardown.",
                    ex);
            }
        }

        if (stateAtEntry != HostState.Stopped && _lifecycleManager is not null)
            await _lifecycleManager.DisposeAllAsync(CancellationToken.None).ConfigureAwait(false);

        // Service Disposal: no-op today - Configuration, Logging, and the DI
        // container implement no IDisposable/IAsyncDisposable (see
        // Failure Behaviour.md and the WP 2.7 Architectural Debt Assessment).

        lock (_gate)
            _state = HostState.Disposed;

        _logger?.Information("Host -> Disposed.");

        _shutdownRequested.Dispose();
        _stopEscalation.Dispose();
    }

    private void EnterStarting()
    {
        lock (_gate)
        {
            if (_state != HostState.Created)
                throw new InvalidHostStateTransitionException(_state, "Run");

            _state = HostState.Starting;
        }
    }

    private void EnterRunning()
    {
        lock (_gate)
            _state = HostState.Running;

        _logger?.Information("Host -> Running.");
    }

    private void EnterFaulted(Exception exception)
    {
        lock (_gate)
            _state = HostState.Faulted;

        _logger?.Critical("Host -> Faulted.", exception);
    }

    private async Task<Exception?> StopInternalAsync()
    {
        lock (_gate)
            _state = HostState.Stopping;

        _logger?.Information("Host -> Stopping.");

        Exception? criticalHostedServiceFault = null;

        if (_hostedServiceManager is not null)
        {
            try
            {
                await _hostedServiceManager.StopAllAsync(_stopEscalation.Token).ConfigureAwait(false);
                _logger?.Information("Host lifecycle phase completed: Hosted Services Stopped.");
            }
            catch (OperationCanceledException)
            {
                _logger?.Information(
                    "Hosted service stop sequence escalated before every service finished " +
                    "stopping; proceeding directly to module disposal.");
            }
            catch (Exception ex)
            {
                // A critical hosted service's StopAsync failure is Host-fatal
                // (ADR-0021/ADR-0029) - recorded here, but the remainder of
                // shutdown (module stop/dispose, service disposal) is still
                // attempted, per ADR-0004/ADR-0019's cleanup guarantee.
                criticalHostedServiceFault = ex;
                _logger?.Critical(
                    "A critical hosted service failed to stop; the Host will fault once " +
                    "shutdown completes.",
                    ex);
            }
        }

        if (_lifecycleManager is not null)
        {
            try
            {
                await _lifecycleManager.StopAllAsync(_stopEscalation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.Information(
                    "Module stop sequence escalated before every module finished stopping; " +
                    "proceeding directly to disposal.");
            }

            _logger?.Information("Host lifecycle phase completed: Module Disposal (Stop).");

            await _lifecycleManager.DisposeAllAsync(CancellationToken.None).ConfigureAwait(false);
            _logger?.Information("Host lifecycle phase completed: Module Disposal (Dispose).");
        }

        // Service Disposal: no-op today - see the remarks on DisposeAsync above.
        _logger?.Information("Host lifecycle phase completed: Service Disposal.");

        _logger?.Information("Shutdown complete.");

        if (criticalHostedServiceFault is not null)
        {
            EnterFaulted(criticalHostedServiceFault);
            return criticalHostedServiceFault;
        }

        lock (_gate)
            _state = HostState.Stopped;

        _logger?.Information("Host -> Stopped.");

        return null;
    }
}
