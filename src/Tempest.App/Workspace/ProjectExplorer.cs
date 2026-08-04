namespace Tempest.App.Workspace;

/// <summary>
/// The concrete <see cref="IProjectExplorer"/> implementation. Never calls
/// any Engineering Core service directly — every read delegates to
/// whichever <see cref="IProjectExplorerNodeProvider"/> is registered for
/// the current top-level area (`ADR-0067`). This Work Package registers
/// none (no engineering functionality) — every read returns empty, exactly
/// as an area with no registered provider is documented to.
/// </summary>
internal sealed class ProjectExplorer : IProjectExplorer
{
    private readonly NavigationService _navigationService;
    private readonly Dictionary<string, IProjectExplorerNodeProvider> _providers;

    /// <summary>Initialises a new instance of the <see cref="ProjectExplorer"/> class.</summary>
    /// <param name="navigationService">Read for the current top-level area — shared, mutable state, not copied.</param>
    /// <param name="providers">The registration table <see cref="IWorkspaceManager.RegisterExplorerArea"/> writes into — shared, mutable, not copied, so a registration made after this instance is constructed is still visible.</param>
    public ProjectExplorer(NavigationService navigationService, Dictionary<string, IProjectExplorerNodeProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(providers);

        _navigationService = navigationService;
        _providers = providers;
    }

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Title => "Project Explorer";

    /// <inheritdoc />
    public WorkspaceDockPosition DockPosition => WorkspaceDockPosition.Left;

    /// <inheritdoc />
    public bool IsVisible { get; private set; } = true;

    /// <inheritdoc />
    public Task ShowAsync(CancellationToken cancellationToken = default)
    {
        IsVisible = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HideAsync(CancellationToken cancellationToken = default)
    {
        IsVisible = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var areaId = _navigationService.CurrentAreaId;

        if (areaId is null || !_providers.TryGetValue(areaId, out var provider))
            return [];

        return await provider.GetRootNodesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var areaId = _navigationService.CurrentAreaId;

        if (areaId is null || !_providers.TryGetValue(areaId, out var provider))
            throw new ArgumentException($"'{nodeId}' is not a known node.", nameof(nodeId));

        return await provider.GetChildrenAsync(nodeId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        // Stateless read-through — GetRootNodesAsync/GetChildrenAsync always
        // delegate live to the registered provider, so there is nothing of
        // this panel's own to invalidate.
        Task.CompletedTask;
}
