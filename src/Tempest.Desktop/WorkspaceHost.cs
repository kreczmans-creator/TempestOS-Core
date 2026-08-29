using Tempest.App.Composition;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Settings;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Runtime;

namespace Tempest.Desktop;

/// <summary>
/// Owns the one running <see cref="ITempestHost"/>/<see cref="WorkspaceManager"/>
/// pair for the lifetime of the desktop application — the graphical
/// presentation layer's own equivalent of what <c>Program.cs</c>'s own
/// top-level statements do for the console
/// (<see cref="Tempest.App.Workspace.WorkspaceShell"/>).
/// Composes through <see cref="EngineeringWorkspaceComposer"/>, shared with
/// the console entry point, so the same six real Engineering Disciplines
/// load identically in both presentation layers (`WP 10.0B`'s own explicit
/// "must all load without behavioural change" requirement).
/// </summary>
public sealed class WorkspaceHost : IAsyncDisposable
{
    private readonly string? _persistenceRootPathOverride;
    private readonly ISessionPrincipalSource _sessionPrincipals;

    private ITempestHost? _host;
    private WorkspaceManager? _manager;

    /// <summary>Gets the running <see cref="IWorkspace"/>, or <see langword="null"/> before <see cref="StartAsync"/> completes.</summary>
    public IWorkspace? Workspace { get; private set; }

    /// <summary>Gets the owning <see cref="WorkspaceManager"/> — exposed so a graphical presentation layer can reach <see cref="WorkspaceManager.StatusBar"/> (internal, `InternalsVisibleTo`), the one Workspace facet with no dedicated public contract (`WP8.0A UI Architecture.md` §1).</summary>
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
    /// default (`ADR-0041`'s own <c>PersistenceStore.DefaultRootPath</c>) —
    /// <see langword="null"/> (the default, used by the real running
    /// application) leaves production behaviour completely unchanged.
    /// Exists solely so test code can isolate its own persisted state per
    /// test-assembly run (see <c>WorkspacePersistenceCollection</c>), rather
    /// than sharing the real, durable, cross-launch store every ordinary
    /// user relies on — the same isolation <c>Tempest.Core.Tests</c> has
    /// applied to every <see cref="Tempest.Core.Runtime.ITempestHostBuilder"/>
    /// construction since `WP 7.3A`, only now extended to
    /// <see cref="Tempest.App.Composition.EngineeringWorkspaceComposer"/>'s
    /// own callers (`WP 10.1B`, `TD-37`).
    /// </param>
    /// <param name="sessionPrincipals">
    /// Where this session's own principal comes from (`TD-103`). Defaults
    /// to <see cref="LocalSessionPrincipalSource"/> — one local desktop
    /// user, no authentication — and is injectable so a test can state the
    /// account rather than inherit the build agent's, and so
    /// Administration can supply a different source later without this
    /// class changing.
    /// </param>
    public WorkspaceHost(string? persistenceRootPathOverride = null, ISessionPrincipalSource? sessionPrincipals = null)
    {
        _persistenceRootPathOverride = persistenceRootPathOverride;
        _sessionPrincipals = sessionPrincipals ?? new LocalSessionPrincipalSource();
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
                        Tempest.Core.Persistence.PersistenceStore.RootPathConfigurationKey,
                        _persistenceRootPathOverride),
                ]),
            ];

        var (host, manager) = EngineeringWorkspaceComposer.Build(configurationSources);
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

        CalculationTemplates = EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);

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
        SessionPrincipal = _sessionPrincipals.Resolve();
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
        var projectContext = new ProjectContext(ProjectDirectory, eventBus, settingsProvider);
        ProjectContext = projectContext;
        var shellNavigator = new ShellNavigator(projectContext, eventBus, settingsProvider);
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
        ProjectRequirements = new ProjectRequirementRegister(
            (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService)), domainContext);

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
    public IPrincipal? SessionPrincipal { get; private set; }

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
