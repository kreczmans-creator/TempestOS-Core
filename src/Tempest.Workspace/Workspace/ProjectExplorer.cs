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
    private readonly List<ProjectExplorerNode> _currentPath = [];

    private string? _pathAreaId;

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

    /// <summary>
    /// This panel's own stable, well-known identifier — fixed, not
    /// per-instance-random, so a persisted <see cref="WorkspacePanelPlacement"/>
    /// (`ADR-0064`) written by one process genuinely identifies the same
    /// logical panel when read back by a later, independent process.
    /// </summary>
    /// <remarks>
    /// <b>Disclosed, found-and-fixed defect (`WP 10.0B`):</b> this field
    /// was originally <c>Guid.NewGuid()</c>, generated fresh on every
    /// <see cref="WorkspaceManager.StartAsync"/> call — meaning a genuine
    /// cross-process restart could never correctly restore a persisted
    /// placement for this panel, since the freshly-constructed instance's
    /// own <see cref="Id"/> could never match whatever Id an earlier
    /// process had persisted. Never observed before `WP 10.0B`'s own
    /// desktop implementation, since no prior test or console usage
    /// exercised two genuinely independent <see cref="WorkspaceManager"/>
    /// instances against the same persisted session state — see
    /// `WP10.0B Technical Debt Review.md`.
    /// </remarks>
    public static readonly Guid WellKnownId = new("8f6c1a2e-2b1e-4b1a-9c3d-1f2a3b4c5d6e");

    /// <inheritdoc />
    public Guid Id { get; } = WellKnownId;

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

    /// <summary>
    /// Gets the current area's own drill-down path, root first — the
    /// Workspace's own breadcrumb trail (`WP8.0C Navigation Maps.md` §3).
    /// Empty if the user has not drilled into any node yet, or resets
    /// automatically the moment the current area changes. Not one of the
    /// twelve `WP8.0B Workspace Contracts.md` interfaces — a genuine,
    /// disclosed implementation-phase addition.
    /// </summary>
    public IReadOnlyList<ProjectExplorerNode> CurrentPath
    {
        get
        {
            EnsurePathMatchesCurrentArea();
            return _currentPath;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        EnsurePathMatchesCurrentArea();

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

    /// <summary>
    /// Drills into <paramref name="node"/>, extending <see cref="CurrentPath"/>,
    /// and returns its own children — a no-op tree read if
    /// <paramref name="node"/> has none. The Shell's own "open a non-leaf
    /// node" gesture.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> EnterAsync(ProjectExplorerNode node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        EnsurePathMatchesCurrentArea();
        _currentPath.Add(node);

        return node.HasChildren
            ? await GetChildrenAsync(node.Id, cancellationToken).ConfigureAwait(false)
            : [];
    }

    /// <summary>
    /// Moves one level up <see cref="CurrentPath"/> and returns the new
    /// current level's own nodes — the root nodes if <see cref="CurrentPath"/>
    /// is now empty. A no-op returning the root nodes if already at the root.
    /// </summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> ExitAsync(CancellationToken cancellationToken = default)
    {
        EnsurePathMatchesCurrentArea();

        if (_currentPath.Count == 0)
            return await GetRootNodesAsync(cancellationToken).ConfigureAwait(false);

        _currentPath.RemoveAt(_currentPath.Count - 1);

        return _currentPath.Count == 0
            ? await GetRootNodesAsync(cancellationToken).ConfigureAwait(false)
            : await GetChildrenAsync(_currentPath[^1].Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns every node, anywhere in the current area's own tree, whose
    /// <see cref="ProjectExplorerNode.Title"/> contains <paramref name="text"/>
    /// (ordinal, case-insensitive) — the Project Explorer's own filter/search
    /// surface (`WP8.0C Interaction Specification.md` §1,
    /// `WP8.0C Screen Catalogue.md` §14). Walks the whole tree via the
    /// registered provider; empty if none is registered for the current
    /// area, or if nothing matches.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty, or whitespace.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> FilterAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var matches = new List<ProjectExplorerNode>();
        var roots = await GetRootNodesAsync(cancellationToken).ConfigureAwait(false);
        await CollectMatchesAsync(roots, text, matches, cancellationToken).ConfigureAwait(false);

        return matches;
    }

    /// <inheritdoc />
    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        // Stateless read-through — GetRootNodesAsync/GetChildrenAsync always
        // delegate live to the registered provider, so there is nothing of
        // this panel's own to invalidate.
        Task.CompletedTask;

    private async Task CollectMatchesAsync(IReadOnlyList<ProjectExplorerNode> nodes, string text, List<ProjectExplorerNode> matches, CancellationToken cancellationToken)
    {
        foreach (var node in nodes)
        {
            if (node.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
                matches.Add(node);

            if (node.HasChildren)
            {
                var children = await GetChildrenAsync(node.Id, cancellationToken).ConfigureAwait(false);
                await CollectMatchesAsync(children, text, matches, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void EnsurePathMatchesCurrentArea()
    {
        var areaId = _navigationService.CurrentAreaId;

        if (_pathAreaId == areaId)
            return;

        _pathAreaId = areaId;
        _currentPath.Clear();
    }
}
