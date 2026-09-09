using Tempest.Workspace.Composition;
using Tempest.Workspace.Engineering;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Shell;
using Tempest.Workspace.Calculations;
using Tempest.Workspace;
using Tempest.Core.Bearings;
using Tempest.Core.Commands;
using Tempest.Core.Calculations;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.Configuration;
using Tempest.Core.Constants;
using Tempest.Core.Audit;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Evidence;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Core.Standards;

namespace Tempest.Desktop;

/// <summary>
/// Owns the one running <see cref="ITempestHost"/>/<see cref="WorkspaceManager"/>
/// pair for the lifetime of the desktop application — the graphical
/// presentation layer's own equivalent of what <c>Program.cs</c>'s own
/// top-level statements do for the console
/// (<see cref="Tempest.Workspace.WorkspaceShell"/>).
/// Composes through <see cref="EngineeringWorkspaceComposer"/>, shared with
/// the console entry point, so the same six real Engineering Disciplines
/// load identically in both presentation layers (`WP 10.0B`'s own explicit
/// "must all load without behavioural change" requirement).
/// </summary>
public sealed class WorkspaceHost : IAsyncDisposable
{
    private readonly string? _persistenceRootPathOverride;
    private readonly ISessionPrincipalSource? _sessionPrincipalsOverride;
    private readonly IReadOnlyList<string>? _commandLineArgs;

    private ITempestHost? _host;
    private WorkspaceManager? _manager;

    /// <summary>Gets the running <see cref="IWorkspace"/>, or <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IWorkspace? Workspace { get; private set; }

    /// <summary>Gets the owning <see cref="WorkspaceManager"/> — exposed so a graphical presentation layer can reach the public <see cref="WorkspaceManager.StatusBar"/> (`WP 17.2B`), the one Workspace facet with no dedicated `WP8.0A UI Architecture.md` §1 contract.</summary>
    public WorkspaceManager? Manager => _manager;

    /// <summary>Gets the running Host's own DI container — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public ITempestServiceProvider? Services => _host?.Services;

    /// <summary>
    /// Gets the Calculations discipline's own template registry (`WP 10.7A`
    /// — Feature Completion) — captured from
    /// <see cref="EngineeringWorkspaceComposer.RegisterEngineeringDisciplines"/>'s
    /// own return value, previously discarded and unreachable anywhere
    /// outside that method's own local scope. The Object Editor's own real
    /// Calculations Execute/Recalculate section is its first real
    /// consumer. <see langword="null"/> before <see cref="StartAsync"/>
    /// completes.
    /// </summary>
    public CalculationTemplateRegistry? CalculationTemplates { get; private set; }

    /// <summary>Initialises a new instance of the <see cref="WorkspaceHost"/> class.</summary>
    /// <param name="persistenceRootPathOverride">
    /// A specific <see cref="Tempest.Core.Persistence.IPersistenceStore"/> root
    /// path to use instead of the conventional, working-directory-relative
    /// default (`ADR-0144`'s own <c>SqlitePersistenceStore.DefaultRootPath</c>) —
    /// <see langword="null"/> (the default, used by the real running
    /// application) leaves production behaviour completely unchanged.
    /// Exists solely so test code can isolate its own persisted state per
    /// test-assembly run (see <c>WorkspacePersistenceCollection</c>), rather
    /// than sharing the real, durable, cross-launch store every ordinary
    /// user relies on — the same isolation <c>Tempest.Core.Tests</c> has
    /// applied to every <see cref="Tempest.Core.Runtime.ITempestHostBuilder"/>
    /// construction since `WP 7.3A`, only now extended to
    /// <see cref="Tempest.Workspace.Composition.EngineeringWorkspaceComposer"/>'s
    /// own callers (`WP 10.1B`, `TD-37`).
    /// </param>
    /// <param name="sessionPrincipals">
    /// Where this session's own principal comes from (`TD-103`). Defaults
    /// to <see cref="SessionPrincipalSource"/> — one local desktop session,
    /// no authentication, constructed once the Host's own configuration is
    /// available so <c>Identity:DisplayName</c>/<c>Identity:Role</c> are
    /// honoured — and is injectable so a test can supply a stub rather than
    /// inherit the build agent's own OS account, and so Administration can
    /// supply a different source later without this class changing.
    /// </param>
    /// <param name="commandLineArgs">
    /// The process's own command-line arguments (Avalonia's own
    /// <c>IClassicDesktopStyleApplicationLifetime.Args</c>, itself
    /// <c>Program.Main(string[] args)</c>, unchanged), or
    /// <see langword="null"/> (the default) to contribute none — reaches
    /// the Host's default configuration source (`WP 17.2A`, ADR-0146).
    /// </param>
    public WorkspaceHost(
        string? persistenceRootPathOverride = null,
        ISessionPrincipalSource? sessionPrincipals = null,
        IReadOnlyList<string>? commandLineArgs = null)
    {
        _persistenceRootPathOverride = persistenceRootPathOverride;
        _sessionPrincipalsOverride = sessionPrincipals;
        _commandLineArgs = commandLineArgs;
    }

    /// <summary>
    /// Builds the Host, starts the Workspace (loading any persisted session
    /// state — `ADR-0064`, unchanged), and registers all six real
    /// Engineering Disciplines.
    /// </summary>
    /// <exception cref="InvalidOperationException">Already started.</exception>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is not null)
            throw new InvalidOperationException("This WorkspaceHost has already been started.");

        IReadOnlyList<IConfigurationSource>? configurationSources = _persistenceRootPathOverride is null
            ? null
            :
            [
                new MemoryConfigurationSource(
                [
                    new KeyValuePair<string, string>(
                        Tempest.Core.Persistence.SqlitePersistenceStore.RootPathConfigurationKey,
                        _persistenceRootPathOverride),
                ]),
            ];

        var (host, manager) = EngineeringWorkspaceComposer.Build(configurationSources, _commandLineArgs);
        _host = host;
        _manager = manager;

        // `TD-26` fixed at its own source, `WP 10.1B`: WorkspaceManager.StartAsync
        // itself now waits for IDiagnosticsProvider.HostState == HostState.Running
        // before returning (see its own remarks), so the bounded poll `WP 10.0B`/
        // `WP 10.1A` each applied one layer up, here, is no longer needed — removed
        // rather than kept as redundant defence-in-depth, since a second,
        // independent "is it really ready" check masks exactly the kind of
        // single-source-of-truth gap this Work Package exists to close.
        Workspace = await manager.StartAsync(cancellationToken).ConfigureAwait(false);

        // `WP 18.2B` (part 1): stateless and dependency-free, so it is
        // simply constructed here over nothing, the same `ADR-0103` shape
        // as every other Desktop-side collaborator. Built *before*
        // `RegisterEngineeringDisciplines` (part 2) so the Evidence
        // discipline's own Issue command handler can be wired to a real
        // renderer at registration time, rather than an
        // `IIssueSheetRenderer` this class would otherwise have no way to
        // hand it after the fact — `Tempest.Workspace`'s own composer has
        // no DI container to add a late instance to.
        IssueSheetRenderer = new Tempest.Desktop.IssueSheets.IssueSheetRenderer();

        CalculationTemplates = EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host, IssueSheetRenderer);

        // ---- The Product Spine (`TD-84`) ----------------------------
        // Module -> Project -> Workspace. Composed here, after the
        // disciplines have registered, because the project directory
        // reads the same engineering domain they populate. Built with
        // `new` over already-resolved Platform Services, exactly as
        // every other Desktop-side collaborator is (`ADR-0103`).
        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        var principalAccessor = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
        var eventBus = (IEventBus)host.Services!.GetService(typeof(IEventBus));
        var settingsProvider = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));

        // `TD-103`: the principal boundary. Authorship, audit attribution
        // and every permission check read the accessor this sets, and
        // until now nothing in the product ever set it — only sample
        // modules did, during their own initialisation, so what a real
        // launch could do depended on which sample happened to run last.
        // Established here, after module initialisation, so the product's
        // own answer is the one that stands rather than a sample's; the
        // source is the boundary, and Administration can replace it later
        // without the engineering domain knowing.
        //
        // `WP 17.2A` (ADR-0146): the default source is constructed here,
        // not in this class's own constructor, specifically so it can read
        // `Identity:DisplayName`/`Identity:Role` from the Host's own
        // now-built configuration — the identity id itself is still always
        // read from the OS, never from configuration.
        var configuration = (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider));
        var sessionPrincipals = _sessionPrincipalsOverride ?? new SessionPrincipalSource(configuration);
        SessionPrincipal = sessionPrincipals.Resolve();
        if (principalAccessor is CurrentPrincipalAccessor accessor)
        {
            // Published unconditionally, null included. Publishing only a
            // non-null answer would leave whatever a module happened to
            // establish during its own initialisation standing as the
            // session's principal — which is the `TD-103` defect itself,
            // not a safe fallback: a session that genuinely has no
            // principal must report none, not inherit a sample's.
            accessor.SetCurrent(SessionPrincipal);
        }

        // `TD-85`. Bring back every engineering object a previous run
        // persisted — projects, and everything inside them — before
        // anything reads the object graph. Without this the shell would
        // open on an empty repository and silently start a new object
        // graph over the user's own still-persisted work.
        RehydrationResult = await EngineeringWorkspaceComposer
            .RehydrateEngineeringObjectsAsync(host, cancellationToken)
            .ConfigureAwait(false);

        ProjectDirectory = new ProjectDirectory(domainContext);
        var hostLogger = (Tempest.Core.Logging.ILogger)host.Services!.GetService(typeof(Tempest.Core.Logging.ILogger));
        var projectContext = new ProjectContext(ProjectDirectory, eventBus, settingsProvider, hostLogger);
        ProjectContext = projectContext;
        var shellNavigator = new ShellNavigator(projectContext, eventBus, settingsProvider, hostLogger);
        ShellNavigator = shellNavigator;

        // The Engineering Workspace's own scope (`TD-89`) — project or
        // standalone — derived from navigation state and the real object
        // graph, never cached and never inferred by a view.
        EngineeringScope = new EngineeringScope(shellNavigator, projectContext, domainContext);

        // The two project-area read models. Both compose services that
        // already exist and hold no state of their own, so they are
        // constructed here beside the scope rather than registered as
        // Platform Services — the identical `ADR-0103` shape every other
        // Desktop-side collaborator uses.
        ProjectDocuments = new ProjectDocumentRegister(domainContext);
        ProjectTasks = new ProjectTaskRegister(domainContext);
        ProjectTaskWorkflow = new ProjectTaskService(domainContext);
        ProjectGovernance = new ProjectGovernanceRegister(domainContext);
        ProjectGovernanceWorkflow = new ProjectGovernanceService(domainContext);
        ProjectMilestones = new ProjectMilestoneRegister(domainContext);
        ProjectMilestoneWorkflow = new ProjectMilestoneService(domainContext);
        ProjectRequirements = new ProjectRequirementRegister(
            (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService)), domainContext);

        // The two reference-data read models, constructed the same way and
        // for the same reason: both compose governed catalogues that
        // already exist and hold no state of their own. They are what lets
        // the application see the populated reference libraries and trace
        // an engineering result back to the revisions it stood on, without
        // any surface reaching past the catalogues to do it.
        ReferenceLibraries = new ReferenceLibraryRegister(
            (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog)),
            (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog)),
            (IConstantCatalog)host.Services!.GetService(typeof(IConstantCatalog)),
            (IFastenerCatalog)host.Services!.GetService(typeof(IFastenerCatalog)),
            (IBearingCatalog)host.Services!.GetService(typeof(IBearingCatalog)),
            (IProcessCatalog)host.Services!.GetService(typeof(IProcessCatalog)));

        // The bracket section check's governed entry point. Constructed the
        // same way as the read models: it composes the Materials Library and
        // the calculation engine, both already registered, and holds no
        // state of its own. This is the whole of the application surface the
        // first calculation needs — the engineer selects a material, supplies
        // the geometry and load, and gets a result or a refusal.
        BracketCheck = new GovernedBracketCheckService(
            (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog)),
            (ICalculationEngine)host.Services!.GetService(typeof(ICalculationEngine)));

        BracketEngineeringRecords = new BracketEngineeringRecordService(
            (ICalculationPackCatalog)host.Services!.GetService(typeof(ICalculationPackCatalog)),
            (IVerificationArtefactCatalog)host.Services!.GetService(typeof(IVerificationArtefactCatalog)));

        ReferenceReview = new ReferenceReviewService(
            (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor)),
            logger: hostLogger,
            auditRecorder: (Tempest.Core.Audit.IAuditRecorder)host.Services!.GetService(typeof(Tempest.Core.Audit.IAuditRecorder)),
            permissions: (Tempest.Core.Identity.IPermissionEvaluator)host.Services!.GetService(typeof(Tempest.Core.Identity.IPermissionEvaluator)));

        // `WP 18.2A` (`ADR-0148`). The Evidence workspace's own governed
        // service, audit query and the five governed libraries it cites —
        // already-registered Platform Services, resolved here the same
        // `ADR-0103` way as every other collaborator on this class.
        EvidenceService = (IEvidenceService)host.Services!.GetService(typeof(IEvidenceService));
        AuditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
        Materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));
        Fasteners = (IFastenerCatalog)host.Services!.GetService(typeof(IFastenerCatalog));
        Bearings = (IBearingCatalog)host.Services!.GetService(typeof(IBearingCatalog));
        Standards = (IStandardCatalog)host.Services!.GetService(typeof(IStandardCatalog));
        Constants = (IConstantCatalog)host.Services!.GetService(typeof(IConstantCatalog));

        // The Engineering Calculation surface's own read model. It composes
        // the four governed acts a calculation journey needs - populate,
        // review and release, check, recover - and owns none of them: the
        // arithmetic stays in the definition, the lifecycle in the review
        // service, the population in the seeder. It exists so the Desktop
        // view renders finished answers and decides nothing, the same
        // discipline ProjectRequirementRegister already follows.
        BracketCalculations = new BracketCalculationWorkbench(
            (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog)),
            (ReferenceSeedService)host.Services!.GetService(typeof(ReferenceSeedService)),
            ReferenceReview,
            BracketCheck,
            (ICalculationEngine)host.Services!.GetService(typeof(ICalculationEngine)),
            (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider)),
            (IVerificationArtefactCatalog)host.Services!.GetService(typeof(IVerificationArtefactCatalog)),
            // The workspace's governed index of named calculations. It adds
            // no concept: a named calculation is the platform's own
            // `Calculation` Domain object, renamed through the rename
            // command CalculationsWorkspaceRegistration already registered,
            // retired through the status command it already registered, and
            // organised by the IHasParent membership every discipline
            // already uses. Constructed here over already-resolved
            // services, the same ADR-0103 shape as every collaborator above.
            new EngineeringCalculationRegister(
                domainContext,
                (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher)),
                projectContext));

        EngineeringTrace = new EngineeringTraceRegister(
            (ICalculationPackCatalog)host.Services!.GetService(typeof(ICalculationPackCatalog)),
            (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog)),
            (ITemplateCatalog)host.Services!.GetService(typeof(ITemplateCatalog)));

        // Recover where the user was, and which project they were in.
        // Order matters: the navigator's own restore opens the project,
        // so loading the context first would be redundant work, not a
        // second source of truth.
        await ShellNavigator.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the principal this session is operating as (`TD-103`), or
    /// <see langword="null"/> when none could be established.
    /// </summary>
    /// <remarks>
    /// Read from <see cref="ISessionPrincipalSource"/> at start-up and
    /// published into <see cref="ICurrentPrincipalAccessor"/>, which is
    /// what every consumer actually reads. Exposed here so a test can
    /// assert the boundary did its job, not as a second source of truth.
    /// </remarks>
    public ISessionPrincipal? SessionPrincipal { get; private set; }

    /// <summary>Gets what startup rehydration recovered (`TD-85`) — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public EngineeringRehydrationResult? RehydrationResult { get; private set; }

    /// <summary>Gets the project catalogue (`TD-84`) — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IProjectDirectory? ProjectDirectory { get; private set; }

    /// <summary>Gets the current-project context (`TD-84`) — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IProjectContext? ProjectContext { get; private set; }

    /// <summary>Gets the shell navigator (`TD-84`) — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IShellNavigator? ShellNavigator { get; private set; }

    /// <summary>Gets the Engineering Workspace's own current scope (`TD-89`) — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IEngineeringScope? EngineeringScope { get; private set; }

    /// <summary>Gets the open project's own document and drawing register — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IProjectDocumentRegister? ProjectDocuments { get; private set; }

    /// <summary>Gets the open project's own requirements register — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IProjectRequirementRegister? ProjectRequirements { get; private set; }

    /// <summary>Gets the project task register — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IProjectTaskRegister? ProjectTasks { get; private set; }

    /// <summary>Gets the task workflow service — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IProjectTaskService? ProjectTaskWorkflow { get; private set; }

    /// <summary>The project's own risk, issue and decision register.</summary>
    public IProjectGovernanceRegister? ProjectGovernance { get; private set; }

    /// <summary>The risk, issue and decision lifecycles, as the Project Workspace performs them.</summary>
    public IProjectGovernanceService? ProjectGovernanceWorkflow { get; private set; }

    /// <summary>The project's own milestone register.</summary>
    public IProjectMilestoneRegister? ProjectMilestones { get; private set; }

    /// <summary>
    /// Gets what reference data the platform holds and whether it may be
    /// relied on — <see langword="null"/> before <see cref="StartAsync"/>
    /// completes.
    /// </summary>
    public IReferenceLibraryRegister? ReferenceLibraries { get; private set; }

    /// <summary>
    /// Gets the read model answering "where did this engineering result
    /// come from?" — <see langword="null"/> before <see cref="StartAsync"/>
    /// completes.
    /// </summary>
    public IEngineeringTraceRegister? EngineeringTrace { get; private set; }

    /// <summary>
    /// Gets the governed bracket section check — <see langword="null"/>
    /// before <see cref="StartAsync"/> completes.
    /// </summary>
    public GovernedBracketCheckService? BracketCheck { get; private set; }

    /// <summary>
    /// Gets the service that writes an executed bracket check into its
    /// calculation pack and verification artefact — <see langword="null"/>
    /// before <see cref="StartAsync"/> completes.
    /// </summary>
    public BracketEngineeringRecordService? BracketEngineeringRecords { get; private set; }

    /// <summary>
    /// Gets the governed reference review and release act —
    /// <see langword="null"/> before <see cref="StartAsync"/> completes.
    /// </summary>
    /// <remarks>
    /// It takes the reviewer from the session's own principal, so a review
    /// performed through the application is attributable to whoever is
    /// signed in and to nobody else.
    /// </remarks>
    public ReferenceReviewService? ReferenceReview { get; private set; }

    /// <summary>
    /// Gets the Evidence discipline's own governed service (`ADR-0148`,
    /// `WP 18.0A`) — record, revise, cite, declare a figure, check and
    /// issue. <see langword="null"/> before <see cref="StartAsync"/>
    /// completes.
    /// </summary>
    public IEvidenceService? EvidenceService { get; private set; }

    /// <summary>Gets the platform's own audit query (`WP 18.2A`) — Evidence's own Audit section reads through this. <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IAuditQuery? AuditQuery { get; private set; }

    /// <summary>Gets the Materials Library (`WP 18.2A`) — the Evidence workspace's own Libraries tab reads the five governed libraries directly, rather than through <see cref="ReferenceLibraries"/>'s own six-library, no-source-citation summary. <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IMaterialCatalog? Materials { get; private set; }

    /// <summary>Gets the Fastener Library (`WP 18.2A`). <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IFastenerCatalog? Fasteners { get; private set; }

    /// <summary>Gets the Bearing Library (`WP 18.2A`). <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IBearingCatalog? Bearings { get; private set; }

    /// <summary>Gets the Standards Library (`WP 18.2A`). <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IStandardCatalog? Standards { get; private set; }

    /// <summary>Gets the Constants Library (`WP 18.2A`). <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IConstantCatalog? Constants { get; private set; }

    /// <summary>
    /// Gets the Engineering Calculation surface's own read model -
    /// <see langword="null"/> before <see cref="StartAsync"/> completes.
    /// </summary>
    /// <remarks>
    /// This is what the <c>EngineeringCalculation</c> shell area renders.
    /// It reaches the governed services this host already composes and adds
    /// no rule of its own.
    /// </remarks>
    public BracketCalculationWorkbench? BracketCalculations { get; private set; }

    /// <summary>Setting milestones and deliverables, as the Project Workspace performs it.</summary>
    public IProjectMilestoneService? ProjectMilestoneWorkflow { get; private set; }

    /// <summary>Gets the issue sheet PDF renderer (`WP 18.2B`) — <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public Tempest.Workspace.Evidence.IIssueSheetRenderer? IssueSheetRenderer { get; private set; }

    /// <summary>Persists current session state (`ADR-0064`, unchanged) and shuts the Workspace down — called from the main window's own Closing handler (Window Lifecycle).</summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is null)
            return;

        // Persist the product spine alongside the Workspace's own state,
        // so reopening recovers the project and location the user left
        // (`TD-84`).
        if (ProjectContext is not null)
            await ProjectContext.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (ShellNavigator is not null)
            await ShellNavigator.SaveAsync(cancellationToken).ConfigureAwait(false);

        await _manager.ShutdownAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_manager is not null)
            await _manager.DisposeAsync().ConfigureAwait(false);
    }
}
