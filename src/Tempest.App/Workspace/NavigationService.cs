using Tempest.Core.Navigation;

namespace Tempest.App.Workspace;

/// <summary>
/// The concrete <see cref="INavigationService"/> implementation — the
/// "Navigation host" this Work Package's own controlling instruction names,
/// and the owner of the Document Area's own open-view tracking (the "View
/// manager" duty), neither of which is a separately named public contract
/// among the twelve `WP8.0B Workspace Contracts.md` approved.
/// </summary>
internal sealed class NavigationService : INavigationService
{
    private readonly INavigationProvider _navigationProvider;
    private readonly Dictionary<string, IWorkspaceViewFactory> _viewFactories;
    private readonly WorkspaceContext _context;
    private readonly List<IWorkspaceView> _openViews = [];

    /// <summary>Initialises a new instance of the <see cref="NavigationService"/> class.</summary>
    /// <param name="navigationProvider">The existing Platform Service every top-level area comes from.</param>
    /// <param name="viewFactories">The registration table <see cref="IWorkspaceManager.RegisterView"/> writes into — shared, mutable, not copied.</param>
    /// <param name="context">The ambient Workspace context this service updates as the active view changes.</param>
    public NavigationService(INavigationProvider navigationProvider, Dictionary<string, IWorkspaceViewFactory> viewFactories, WorkspaceContext context)
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);
        ArgumentNullException.ThrowIfNull(viewFactories);
        ArgumentNullException.ThrowIfNull(context);

        _navigationProvider = navigationProvider;
        _viewFactories = viewFactories;
        _context = context;
    }

    /// <summary>Gets the top-level area <see cref="NavigationItem.Id"/> the Project Explorer is currently scoped to, or <see langword="null"/> before any <see cref="SwitchAreaAsync"/> call.</summary>
    public string? CurrentAreaId { get; private set; }

    /// <summary>Gets every view currently open, in tab order.</summary>
    public IReadOnlyList<IWorkspaceView> OpenViews => _openViews;

    /// <summary>Gets the currently active (focused) view, or <see langword="null"/> if none is open.</summary>
    public IWorkspaceView? ActiveView { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<NavigationItem> Areas => _navigationProvider.Items;

    /// <inheritdoc />
    public async Task SwitchAreaAsync(string areaId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);

        await _navigationProvider.Navigate(areaId, cancellationToken).ConfigureAwait(false);

        CurrentAreaId = areaId;
    }

    /// <inheritdoc />
    public Task<IWorkspaceView> OpenAsync(Guid objectId, string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        var existing = _openViews.FirstOrDefault(v => v.ObjectId == objectId && v.ObjectKind == kind);

        if (existing is not null)
        {
            SetActive(existing);
            return Task.FromResult(existing);
        }

        if (!_viewFactories.TryGetValue(kind, out var factory))
            throw new WorkspaceViewFactoryNotFoundException(kind);

        var view = factory.Create(objectId, _context);
        _openViews.Add(view);
        SetActive(view);

        return Task.FromResult(view);
    }

    /// <inheritdoc />
    public Task<IWorkspaceView> JumpToAsync(Guid targetObjectId, string targetKind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        if (!_viewFactories.TryGetValue(targetKind, out var factory))
            throw new WorkspaceViewFactoryNotFoundException(targetKind);

        var view = factory.Create(targetObjectId, _context);
        _openViews.Add(view);
        SetActive(view);

        return Task.FromResult(view);
    }

    /// <inheritdoc />
    public Task CloseAsync(Guid viewId, CancellationToken cancellationToken = default)
    {
        var view = _openViews.FirstOrDefault(v => v.Id == viewId);

        if (view is null)
            return Task.CompletedTask;

        _openViews.Remove(view);

        if (ActiveView?.Id == viewId)
            SetActive(_openViews.Count > 0 ? _openViews[^1] : null);

        return Task.CompletedTask;
    }

    private void SetActive(IWorkspaceView? view)
    {
        ActiveView = view;
        _context.ActiveViewId = view?.Id;
    }
}
