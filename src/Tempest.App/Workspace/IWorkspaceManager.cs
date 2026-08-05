namespace Tempest.App.Workspace;

/// <summary>
/// Creates and owns the lifecycle of the one running <see cref="IWorkspace"/>
/// instance — the Workspace's own equivalent of
/// <see cref="Tempest.Core.Runtime.ITempestHostBuilder"/>/
/// <see cref="Tempest.Core.Runtime.ITempestHost"/>, and the Workspace's own
/// registration point for the extensibility mechanisms that resolve
/// `ADR-0067`.
/// </summary>
public interface IWorkspaceManager
{
    /// <summary>Gets the current running Workspace, or <see langword="null"/> before <see cref="StartAsync"/>.</summary>
    IWorkspace? Current { get; }

    /// <summary>Assembles and starts the Workspace — the composition-root entry point.</summary>
    Task<IWorkspace> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists current state (<see cref="IWorkspaceState.SaveAsync"/>) and shuts the Workspace down.</summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the one <see cref="IWorkspaceViewFactory"/> responsible for
    /// presenting an engineering object of <paramref name="kind"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A factory is already registered for <paramref name="kind"/>.</exception>
    void RegisterView(string kind, IWorkspaceViewFactory factory);

    /// <summary>
    /// Registers the one <see cref="IProjectExplorerNodeProvider"/> responsible
    /// for populating the Project Explorer's own tree for <paramref name="kind"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A provider is already registered for <paramref name="kind"/>.</exception>
    void RegisterExplorerArea(string kind, IProjectExplorerNodeProvider provider);

    /// <summary>
    /// Registers the one <see cref="IPropertyFacetProvider"/> responsible for
    /// supplying the Property Inspector's own real facets for <paramref name="kind"/>.
    /// A genuine, disclosed `WP 9.0A` addition to this frozen `WP8.0B`
    /// contract — additive only (a new member, no existing member changed),
    /// applying the same Kind-keyed registration principle
    /// <see cref="RegisterView"/>/<see cref="RegisterExplorerArea"/> already
    /// establish (`ADR-0067`) a third time.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A provider is already registered for <paramref name="kind"/>.</exception>
    void RegisterFacetProvider(string kind, IPropertyFacetProvider provider);
}
