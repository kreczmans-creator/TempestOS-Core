using Tempest.Core.Audit;
using Tempest.Core.BackgroundServices;
using Tempest.Core.Bearings;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.Components;
using Tempest.Core.Constants;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.BusinessOperations.Finance;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Events;
using Tempest.Core.Fasteners;
using Tempest.Core.ExportImport;
using Tempest.Core.Identity;
using Tempest.Core.Input;
using Tempest.Core.Logging;
using Tempest.Core.Macros;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Notifications;
using Tempest.Core.Persistence;
using Tempest.Core.Plugins;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.Reporting;
using Tempest.Core.Requirements;
using Tempest.Core.Settings;
using Tempest.Core.Standards;
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
/// <b>Frozen by ADR-0146 (<c>WP 17.2A</c>).</b> This class used to construct
/// and hold three Host-owned trust collaborators — a plugin trust store, a
/// component-principal registry, and a current-component accessor — wiring
/// them into Plugin Discovery, Plugin Loading, and
/// <see cref="Modules.ModuleLifecycleManager"/>/<see cref="BackgroundServices.HostedServiceManager"/>'s
/// own construction; it ran a licence-validation phase (ADR-0050) ahead of
/// even the logger, aborting startup Host-fatally on an invalid licence file;
/// and it registered the REST API's own <c>IApiEndpointRegistry</c> singleton
/// (ADR-0047). Signing, trust tiers, capability enforcement, plugin assembly
/// loading, licensing and the inbound REST API are all frozen at
/// <c>src/Frozen/</c> — see that folder's own <c>README.md</c>. Plugin
/// Discovery (Phase 3.1) stays live: the Host still discovers and records
/// what is in the plugin drop folder, and goes no further.
/// </para>
/// </remarks>
public sealed class TempestHost : ITempestHost
{
    private readonly object _gate = new();
    private readonly IReadOnlyList<IConfigurationSource> _configurationSources;
    private readonly IEnumerable<Type>? _discoveryCandidateTypesOverride;
    private readonly string? _pluginsRootPathOverride;
    private readonly IEnumerable<Type>? _hostedServiceCandidateTypesOverride;
    private readonly bool _includeFaultInjectionModules;
    private readonly IReadOnlyList<ILogSink> _additionalLogSinks;
    private readonly CancellationTokenSource _shutdownRequested = new();
    private readonly CancellationTokenSource _stopEscalation = new();
    private readonly TaskCompletionSource _runCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HostState _state = HostState.Created;
    private ILogger? _logger;
    private IRuntimeModuleManager? _moduleManager;
    private IModuleLifecycleManager? _lifecycleManager;
    private IHostedServiceManager? _hostedServiceManager;
    private ITempestServiceProvider? _services;

    /// <summary>
    /// Every already-constructed service instance this Host registered, in
    /// registration order — what the Service Disposal phase disposes, in
    /// reverse (`TD-03`, `WP 17.1A`). See <see cref="DisposeRegisteredServiceInstancesAsync"/>.
    /// </summary>
    private IReadOnlyList<object>? _registeredServiceInstances;
    private bool _serviceInstancesDisposed;

    internal TempestHost(
        IReadOnlyList<IConfigurationSource> configurationSources,
        IEnumerable<Type>? discoveryCandidateTypesOverride,
        string? pluginsRootPathOverride,
        IEnumerable<Type>? hostedServiceCandidateTypesOverride,
        bool includeFaultInjectionModules = false,
        IReadOnlyList<ILogSink>? additionalLogSinks = null)
    {
        _configurationSources = configurationSources;
        _discoveryCandidateTypesOverride = discoveryCandidateTypesOverride;
        _pluginsRootPathOverride = pluginsRootPathOverride;
        _hostedServiceCandidateTypesOverride = hostedServiceCandidateTypesOverride;
        _includeFaultInjectionModules = includeFaultInjectionModules;
        _additionalLogSinks = additionalLogSinks ?? [];
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

        // `WP 17.2A` (ADR-0146): a durable, rotated file sink under the
        // persistence root's own `logs/` folder is registered by default,
        // alongside the console sink - resolved here, ahead of Persistence
        // itself (below), from the identical `Persistence:RootPath`
        // configuration key/default `PersistenceStore`/`SqlitePersistenceStore`
        // each independently resolve, so the log directory and the
        // database directory always share one root without this class
        // taking a dependency on either concrete store type.
        var persistenceRootPath = configuration.TryGetValue(Persistence.PersistenceStore.RootPathConfigurationKey, out var configuredRootPath)
            && !string.IsNullOrWhiteSpace(configuredRootPath)
            ? configuredRootPath
            : Persistence.PersistenceStore.DefaultRootPath;

        var rollingFileSink = new RollingFileLogSink(Path.Combine(persistenceRootPath, "logs"));

        // The console sink is included only when a console is genuinely
        // attached and useful to write to. `Console.IsOutputRedirected`
        // reports `true` both for a stream genuinely redirected to a file
        // or pipe, and - because the underlying handle is invalid - for a
        // `WinExe` with no console allocated at all (`Tempest.Desktop`),
        // so `!IsOutputRedirected` alone already excludes the Desktop;
        // `Environment.UserInteractive` (false for a Windows Service or a
        // `CreateNoWindow`-launched batch process) is the second half of
        // "genuinely attached", so this Host never spends a write on a
        // console nobody can see either way.
        var consoleAttached = !Console.IsOutputRedirected && Environment.UserInteractive;

        List<ILogSink> sinks = [rollingFileSink, .. _additionalLogSinks];

        if (consoleAttached)
            sinks.Insert(0, new ConsoleLogSink());

        ILogSink sink = sinks.Count > 1 ? new CompositeLogSink(sinks) : sinks[0];
        ILoggerFactory loggerFactory = new LoggerFactory(configuration, sink);
        var logger = loggerFactory.CreateLogger(LoggingServiceCollectionExtensions.DefaultLoggerCategory);
        _logger = logger;

        // `WP 17.2A`: forwards any future library code's own
        // `Microsoft.Extensions.Logging.ILoggerFactory` dependency (an
        // accounting connector's `HttpClient` diagnostics in `v0.19.0`,
        // SQLite's own logging hooks) into this exact same sink pipeline,
        // category for category - registered below, alongside the rest of
        // Platform Services.
        Microsoft.Extensions.Logging.ILoggerFactory microsoftLoggerFactory = new TempestLoggerProvider(loggerFactory);

        logger.Information("Host lifecycle phase completed: Configuration Built.");
        logger.Information("Host lifecycle phase completed: Logging Built.");

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

        // Plugin Platform Architecture.md, "Plugin Registry": Host-owned,
        // constructed immediately before Plugin Discovery ever runs, so
        // Discovery can record every candidate's outcome into it as it
        // goes. Never added to the DI ServiceCollection (ADR-0017's own
        // Host-owned-collaborator boundary) — only IDiagnosticsProvider.Plugins,
        // the DI-public read-only projection, ever reaches a module.
        var pluginRegistry = new PluginRegistry();

        // Frozen by ADR-0146 (WP 17.2A): this is now the whole of the
        // plugin platform the Host still runs. Discovery reads, validates,
        // version-checks, dependency-resolves and records what is in the
        // plugin drop folder; nothing is signed, verified, trust-tiered,
        // loaded or scoped. See src/Frozen/README.md.
        var pluginDiscoveryService = new PluginManifestDiscoveryService(
            pluginsRootPath, platformVersionProvider, logger, manifestFileName, disabledPluginIds, pluginRegistry);

        var pluginManifests = pluginDiscoveryService.DiscoverManifests();
        logger.Information($"Host lifecycle phase completed: Plugin Discovery. {pluginManifests.Count} plugin(s) discovered.");

        runToken.ThrowIfCancellationRequested();

        var discovery = new ReflectionFrameworkDiscoveryService(
            logger, includeFaultInjectionModules: _includeFaultInjectionModules);

        var descriptors = _discoveryCandidateTypesOverride is not null
            ? discovery.DiscoverModules(_discoveryCandidateTypesOverride)
            : discovery.DiscoverModules();

        logger.Information("Host lifecycle phase completed: Module Discovery.");

        runToken.ThrowIfCancellationRequested();

        var moduleManager = new RuntimeModuleManager(logger);

        foreach (var descriptor in descriptors)
            moduleManager.Register(descriptor);

        _moduleManager = moduleManager;
        logger.Information("Host lifecycle phase completed: Module Registration.");

        runToken.ThrowIfCancellationRequested();

        var hostedServiceDiscovery = new HostedServiceDiscoveryService(logger);

        var hostedServiceTypes = _hostedServiceCandidateTypesOverride is not null
            ? hostedServiceDiscovery.DiscoverHostedServiceTypes(_hostedServiceCandidateTypesOverride)
            : hostedServiceDiscovery.DiscoverHostedServiceTypes();

        var services = new ServiceCollection(logger);
        services.AddInstance(configuration);
        services.AddInstance(sink);
        services.AddInstance(loggerFactory);
        services.AddInstance(logger);
        services.AddInstance(microsoftLoggerFactory);

        // Registered under its own concrete type, distinct from `sink`
        // above (which may be the `CompositeLogSink` wrapping it, not
        // itself `IDisposable`) - the Service Disposal phase (`TD-03`,
        // `WP 17.1A`) walks every `AddInstance`-registered instance, so
        // this is what makes the rolling file sink's own writer close, and
        // its swallowed-error count reported, on Host shutdown.
        services.AddInstance(rollingFileSink);
        services.AddInstance(platformVersionProvider);
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
        // two service-type keys - so a caller needing write access (the
        // presentation layer's own SessionPrincipalSource boundary,
        // `WP 17.2A`) and every ordinary consumer (which resolves only the
        // read-only interface) share the exact same object, never two
        // independently-constructed ones. See CurrentPrincipalAccessor's
        // own remarks.
        //
        // `WP 17.2A` (ADR-0146): IRoleProvider/RoleProvider and
        // IIdentityService/IdentityService are deleted, not merely
        // unregistered - Identity collapses to one session principal
        // (SessionPrincipalSource, established directly on the concrete
        // CurrentPrincipalAccessor by the presentation layer, never
        // resolved through a Host-registered identity service).
        var currentPrincipalAccessor = new CurrentPrincipalAccessor();
        services.AddInstance<ICurrentPrincipalAccessor>(currentPrincipalAccessor);
        services.AddInstance(currentPrincipalAccessor);

        // `WP 17.9.1`: identity ids are stored; names are shown. One directory
        // over the same accessor, so every surface describes a principal the
        // same way.
        services.AddInstance<IPrincipalDirectory>(new PrincipalDirectory(currentPrincipalAccessor));
        services.Singleton<IPermissionEvaluator, PermissionEvaluator>();

        // ADR-0041/ADR-0144: Persistence is established here, as part of
        // Settings' own scope, ahead of Settings' own registration so the
        // container can resolve IPersistenceStore for SettingsProvider's
        // constructor.
        //
        // ADR-0144 changed two things about these lines. The backend is now
        // SQLite by default, selected by `Persistence:Backend`
        // (`files` still selects the file-per-key store for exactly one
        // release). And ONE instance is constructed here and registered
        // under all three store shapes, rather than three Singleton<>
        // registrations that would each have constructed their own - which
        // is what the two `Singleton<..., PersistenceStore>()` lines this
        // replaces actually did: the text store and the byte store were two
        // objects over one directory tree, harmless for a file store and
        // impossible for one holding an exclusive lock on one database
        // file. The dual-registration shape is ADR-0044's own, already used
        // here for CurrentPrincipalAccessor and ImportService.
        var persistenceStore = CreatePersistenceStore(configuration, logger);
        services.AddInstance(typeof(IPersistenceStore), persistenceStore);
        services.AddInstance(typeof(IBinaryPersistenceStore), persistenceStore);
        services.AddInstance(typeof(IQueryablePersistenceStore), persistenceStore);
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

        // TD-85: the durable half of the Engineering Domain. Registered
        // before EngineeringDomainContext, which takes it as a
        // collaborator. Built on the same IPersistenceStore
        // IEngineeringDocumentStore already uses (ADR-0053) - one
        // persistence authority, split by concern (the document owns
        // identity, Kind and revisions; this owns the object state a
        // document was never designed to carry), never a second one.
        services.Singleton<IEngineeringObjectStateStore, EngineeringObjectStateStore>();

        // TD-31: the durable bytes of an attached file. Built on the same
        // single persistence store, in its byte shape
        // (IBinaryPersistenceStore), with its own collection. The metadata
        // stays on the object; only the content lives here, so rehydrating
        // a whole object graph never loads a file.
        //
        // IBinaryPersistenceStore itself is registered up with the rest of
        // Persistence (ADR-0144) - it used to be registered here, a second
        // time and as a second instance, which is now both unnecessary and
        // impossible.
        services.Singleton<IAttachmentContentStore, AttachmentContentStore>();

        // ADR-0145: there is no write-intent marker here any more. The
        // marker, its interface and the reconciliation sweep that read it
        // existed because an attachment's bytes and the object state
        // naming them were two writes with a window between them. They are
        // now one write in one transaction, so the state the sweep
        // repaired is unreachable and the sweep is deleted rather than
        // left registered against a failure that cannot occur.

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
        // `Group A` (P01): the Standards Library is registered before every
        // other reference library because they all cite it. Its narrow
        // IStandardResolver seam is registered through a forwarder rather
        // than by mapping StandardCatalog to two service types, which would
        // construct two catalogues over one store, each with its own write
        // locks - see StandardCatalogResolver's own remarks.
        // The population seam. One service, registered alongside the
        // libraries it writes into, because seeding is an ordinary write
        // through the ordinary catalogues and needs nothing else: no
        // pipeline, no staging store, no second persistence mechanism. It
        // is registered but never invoked from here — the host does not
        // seed itself at start-up, because deciding when a library gets
        // populated is a governance choice and not a side effect of
        // booting.
        services.Singleton<ReferenceSeedService>();

        services.Singleton<IStandardCatalog, StandardCatalog>();
        services.Singleton<IStandardResolver, StandardCatalogResolver>();
        services.Singleton<IStandardValidationService, StandardValidationService>();

        services.Singleton<IMaterialCatalog, MaterialCatalog>();
        services.Singleton<IMaterialValidationService, MaterialValidationService>();

        // ADR-0124: the Bearing Library is the same thin, typed index over
        // the Engineering Data Model (Kind = "BearingReference") that
        // Materials is, plus a direct IPersistenceStore dependency of its
        // own for two indexes - bearingId and manufacturer-part-number -
        // for the identical reason (IEngineeringDocumentStore has no
        // lookup-by-arbitrary-string and no enumerate-by-Kind). Registered
        // after Materials, whose catalogue its validation service takes as
        // an optional collaborator when confirming that a bearing's own
        // material references resolve.
        services.Singleton<IBearingCatalog, BearingCatalog>();
        services.Singleton<IBearingValidationService, BearingValidationService>();

        // `Group A` (P01): the four remaining reference libraries, each the
        // same thin, typed index over the Engineering Data Model that
        // Materials and Bearings are, over the shared
        // ReferenceDataCatalog<T> base (`ADR-0126`). Registered after
        // Materials and Standards, whose catalogue and resolver their
        // validation services take as optional collaborators when
        // confirming that material and standard references resolve; the
        // container supplies each optional parameter from the container
        // where it is registered, and leaves it null where it is not.
        services.Singleton<IFastenerCatalog, FastenerCatalog>();
        services.Singleton<IFastenerValidationService, FastenerValidationService>();

        services.Singleton<IComponentCatalog, ComponentCatalog>();
        services.Singleton<IComponentValidationService, ComponentValidationService>();

        // The released-constant seam is forwarded to the single registered
        // catalogue for the same reason IStandardResolver is - see
        // ConstantCatalogReleasedSource's own remarks. It is the only way a
        // future calculation capability should reach a constant: it hands
        // back nothing at all until a record is Released.
        services.Singleton<IConstantCatalog, ConstantCatalog>();
        services.Singleton<IReleasedConstantSource, ConstantCatalogReleasedSource>();
        services.Singleton<IConstantValidationService, ConstantValidationService>();

        services.Singleton<IProcessCatalog, ProcessCatalog>();
        services.Singleton<IProcessValidationService, ProcessValidationService>();

        // `Group B` (P02, the engineering-reasoning layer) was frozen to
        // `src/Frozen/Tempest.Core.EngineeringIntelligence` by `WP 18.0C`
        // (`D-028`): unreachable from any shipped surface. See
        // `src/Frozen/README.md`.

        // `Group C` (P07): business governance. Rate cards are authored,
        // evidenced, approved, revisioned and superseded records, so they
        // sit on the same shared ReferenceDataCatalog<T> base as `P01`
        // rather than growing a third lifecycle (`ADR-0129`).
        //
        // WP 18.0C (D-028): Contracts, Risk, Assets (IP/data), Finance
        // (Assumption/Scenario/Control), Development (Opportunity/
        // Pipeline), Operating and Pricing.PricingService were frozen to
        // `src/Frozen/Tempest.Core.BusinessGovernance` — unreachable from
        // any shipped surface. See `src/Frozen/README.md`.
        services.Singleton<IRateCardCatalog, RateCardCatalog>();
        services.Singleton<IRateCardValidationService, RateCardValidationService>();

        // `Group D` (P03, CommercialIntelligence) was frozen to
        // `src/Frozen/Tempest.Core.CommercialIntelligence` by `WP 18.0C`
        // (`D-028`): unreachable from any shipped surface. See
        // `src/Frozen/README.md`.

        // `Group E` (P05): engineering assets. Templates, calculation
        // packs, verification artefacts, design review packs and technical
        // documentation are authored, evidenced, reviewed, revisioned and
        // superseded records like every other library, and sit on the same
        // shared ReferenceDataCatalog<T> base (`ADR-0136`).
        //
        // Registered after `P01`, `P03` and `P07`, all of which `P05`
        // references and none of which it duplicates: `E2` links the
        // platform's own calculation records rather than recomputing them,
        // `E3` references `Tempest.Core.Requirements` rather than copying a
        // requirement, and `E5` points at `EngineeringData` documents
        // rather than storing content a second time.
        services.Singleton<ITemplateCatalog, TemplateCatalog>();
        services.Singleton<ITemplateValidationService, TemplateValidationService>();

        services.Singleton<ICalculationPackCatalog, CalculationPackCatalog>();
        services.Singleton<ICalculationPackValidationService, CalculationPackValidationService>();

        services.Singleton<IVerificationArtefactCatalog, VerificationArtefactCatalog>();
        services.Singleton<IVerificationArtefactValidationService, VerificationArtefactValidationService>();
        services.Singleton<IVerificationTraceService, VerificationTraceService>();

        services.Singleton<IDesignReviewCatalog, DesignReviewCatalog>();
        services.Singleton<IDesignReviewValidationService, DesignReviewValidationService>();

        services.Singleton<ITechnicalDocumentCatalog, TechnicalDocumentCatalog>();
        services.Singleton<ITechnicalDocumentValidationService, TechnicalDocumentValidationService>();

        // `Group F` (P06, Knowledge) was frozen to
        // `src/Frozen/Tempest.Core.Knowledge` by `WP 18.0C` (`D-028`):
        // unreachable from any shipped surface. See `src/Frozen/README.md`.

        // `P04`: Business OS. The operational layer over the reference and
        // governance programmes — organisations and contacts behind `P07`'s
        // opportunities, and budgets the spend is measured against
        // (`ADR-0142`).
        //
        // WP 18.0C (D-028): Interaction, FinancialEntry, Purchasing,
        // Quality and Records were frozen to
        // `src/Frozen/Tempest.Core.BusinessOperations` — unreachable from
        // any shipped surface. See `src/Frozen/README.md`.
        services.Singleton<IOrganisationCatalog, OrganisationCatalog>();
        services.Singleton<IContactCatalog, ContactCatalog>();
        services.Singleton<IOrganisationValidationService, OrganisationValidationService>();

        services.Singleton<IBudgetCatalog, BudgetCatalog>();

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

        // TD-67/TD-97 reconciliation services (`WP 16.4B`). Registered as
        // ordinary Platform Services so that the reconcile/repair path
        // TD-67 named as absent genuinely exists and is reachable, rather
        // than sitting in the assembly with no way to reach it. Each is
        // explicit `DetectAsync`/`SweepAsync` only: nothing here, and
        // nothing in the startup phase table, ever invokes a sweep on its
        // own - this platform does not repair a user's data behind their
        // back. No command or user-facing surface invokes them either;
        // adding one would be product capability, which `v0.16.0` is
        // scoped out of.
        services.Singleton<IRequirementsReconciliationService, RequirementsReconciliationService>();
        services.Singleton<IMaterialCatalogReconciliationService, MaterialCatalogReconciliationService>();

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

        // `TD-03`/`WP 17.1A`: the Service Disposal phase's own subject.
        // Captured from the descriptors rather than accumulated by hand so
        // that a future AddInstance registration is covered by having been
        // registered, not by somebody having remembered. Reference-distinct
        // because one instance registered under three service types (the
        // persistence store, ADR-0144) must be disposed once, not three
        // times.
        lock (_gate)
        {
            _services = serviceProvider;
            _registeredServiceInstances = services.Descriptors
                .Select(descriptor => descriptor.ExistingInstance)
                .Where(instance => instance is not null)
                .Select(instance => instance!)
                .Distinct(ReferenceEqualityComparer.Instance)
                .ToList();
        }

        logger.Information("Host lifecycle phase completed: Dependency Injection Built.");

        runToken.ThrowIfCancellationRequested();

        var lifecycleManager = new ModuleLifecycleManager(moduleManager, serviceProvider, logger);
        _lifecycleManager = lifecycleManager;

        // `TD-159`: the product's own five engineering calculations, put
        // into the engine before the first module initialises.
        //
        // `TD-75` phase 1 moved these definitions out of `Tempest.Samples`
        // and into `Tempest.Core.Calculations` because they are product
        // content, but it moved only the declarations. The registrations
        // stayed in the sample module, which neither `Tempest.App` nor
        // `Tempest.Desktop` references — so a shipped Desktop run offered
        // five Calculation Templates in the Object Editor and threw
        // `CalculationDefinitionNotFoundException` on executing any of
        // them. Both test projects DO reference the sample assembly, so
        // every test passed against a composition no user ever ran.
        //
        // Registered here rather than in `CalculationsWorkspaceRegistration`
        // (the discipline's own composition root, and the natural home)
        // because that runs after `manager.StartAsync()` returns, and a
        // module may legitimately execute a calculation while initialising.
        // Here, the catalogue is present before anyone can ask for it.
        ProductCalculationCatalogue.RegisterAll((ICalculationEngine)serviceProvider.GetService(typeof(ICalculationEngine)));
        logger.Information($"Product calculation catalogue registered: {ProductCalculationCatalogue.CalculationIds.Count} calculations.");

        await lifecycleManager.InitialiseAllAsync(runToken).ConfigureAwait(false);
        await lifecycleManager.StartAllAsync(runToken).ConfigureAwait(false);
        logger.Information("Host lifecycle phase completed: Module Initialisation.");

        runToken.ThrowIfCancellationRequested();

        var hostedServiceManager = new HostedServiceManager(hostedServiceTypes, serviceProvider, logger);
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

        // Service Disposal (`TD-03`, `WP 17.1A`): every service registered
        // as an already-constructed instance that implements
        // IAsyncDisposable/IDisposable, in reverse registration order. It
        // was a no-op until ADR-0144 gave the platform its first genuinely
        // disposable service - a persistence store holding a database file
        // and a cross-process lock, which the next Host on the same root
        // cannot open until this one has let go. Idempotent, so the path
        // that already stopped cleanly does not dispose twice.
        await DisposeRegisteredServiceInstancesAsync().ConfigureAwait(false);

        lock (_gate)
            _state = HostState.Disposed;

        _logger?.Information("Host -> Disposed.");

        _shutdownRequested.Dispose();
        _stopEscalation.Dispose();
    }

    /// <summary>
    /// Builds the one persistence store this Host registers under all
    /// three store shapes, honouring <c>Persistence:Backend</c>
    /// (`ADR-0144`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>sqlite</c> — the default, and the only value a shipped
    /// installation should ever use — gives
    /// <see cref="SqlitePersistenceStore"/>. <c>files</c> gives the
    /// file-per-key <see cref="PersistenceStore"/>, retained for exactly
    /// one release so that a site which hits an unforeseen SQLite problem
    /// in <c>v0.17.0</c> has somewhere to stand while it is fixed; it is
    /// deleted in <c>v0.18.0</c> and nothing new may be built on it.
    /// </para>
    /// <para>
    /// An unrecognised value is a Host-fatal configuration error rather
    /// than a silent fall back to the default: a deployment that asked for
    /// a backend and got a different one would be writing its data
    /// somewhere its operator did not choose (`ADR-0013`).
    /// </para>
    /// </remarks>
    private static object CreatePersistenceStore(IConfigurationProvider configuration, ILogger logger)
    {
        var backend = configuration.TryGetValue(SqlitePersistenceStore.BackendConfigurationKey, out var configured)
            && !string.IsNullOrWhiteSpace(configured)
            ? configured.Trim()
            : SqlitePersistenceStore.SqliteBackendValue;

        if (string.Equals(backend, SqlitePersistenceStore.SqliteBackendValue, StringComparison.OrdinalIgnoreCase))
        {
            var store = new SqlitePersistenceStore(configuration, logger);
            logger.Information(
                $"Persistence backend: SQLite (ADR-0144), database '{store.DatabasePath}', " +
                $"schema version {SqlitePersistenceStore.SchemaVersion}.");
            return store;
        }

        if (string.Equals(backend, SqlitePersistenceStore.FileBackendValue, StringComparison.OrdinalIgnoreCase))
        {
            var store = new PersistenceStore(configuration, logger);
            logger.Warning(
                $"Persistence backend: the file-per-key store, root '{store.RootPath}', selected by " +
                $"'{SqlitePersistenceStore.BackendConfigurationKey}'. This backend does not fsync a write, " +
                "cannot answer a query without scanning a directory, and cannot make two writes land together. " +
                "It is retained for one release only and is deleted in v0.18.0 (ADR-0144).");
            return store;
        }

        throw new PersistenceStoreUnavailableException(
            $"'{SqlitePersistenceStore.BackendConfigurationKey}' is configured as '{backend}', which is not a " +
            $"persistence backend this build has. Valid values are '{SqlitePersistenceStore.SqliteBackendValue}' " +
            $"(the default) and '{SqlitePersistenceStore.FileBackendValue}'.");
    }

    /// <summary>
    /// The Service Disposal lifecycle phase, for the services the Host
    /// registered as already-constructed instances (`TD-03`, `WP 17.1A`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Disposes every registered instance implementing
    /// <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/>, in
    /// <b>reverse registration order</b> — the order a composition root
    /// must use, because a service registered later may have been handed a
    /// service registered earlier and must stop using it first. Async
    /// disposal is preferred where a type offers both.
    /// </para>
    /// <para>
    /// This closes `TD-03` for instance registrations, which is where the
    /// platform's disposable services actually are: the persistence store
    /// (`ADR-0144`) is registered this way, and it holds a database file
    /// and a cross-process lock that a second Host on the same root cannot
    /// take until this one lets go. It does <b>not</b> close `TD-03` for
    /// container-constructed singletons; <c>TempestServiceProvider</c>
    /// keeps no disposal list of what it built, and giving it one is a
    /// change to the container rather than to the Host. That remains open
    /// and is deliberately not claimed here.
    /// </para>
    /// <para>
    /// Idempotent, and never allowed to fail shutdown: a failing dispose is
    /// logged and the remaining instances are still disposed
    /// (`FOUNDATION.md` principle 5).
    /// </para>
    /// </remarks>
    private async Task DisposeRegisteredServiceInstancesAsync()
    {
        IReadOnlyList<object>? instances;

        lock (_gate)
        {
            if (_serviceInstancesDisposed)
                return;

            _serviceInstancesDisposed = true;
            instances = _registeredServiceInstances;
        }

        if (instances is null)
            return;

        for (var i = instances.Count - 1; i >= 0; i--)
        {
            var instance = instances[i];

            try
            {
                switch (instance)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning(
                    $"Service Disposal: '{instance.GetType().FullName}' threw while being disposed. Shutdown " +
                    "continues; the remaining services are still disposed.",
                    ex);
            }
        }
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

        // Service Disposal - see the remarks on DisposeRegisteredServiceInstancesAsync.
        await DisposeRegisteredServiceInstancesAsync().ConfigureAwait(false);
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
