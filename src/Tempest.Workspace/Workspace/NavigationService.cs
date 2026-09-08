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
    private const int MaxRecentItems = 10;

    private readonly INavigationProvider _navigationProvider;
    private readonly Dictionary<string, IWorkspaceViewFactory> _viewFactories;
    private readonly WorkspaceContext _context;
    private readonly List<IWorkspaceView> _openViews = [];
    private readonly List<NavigationHistoryEntry> _history = [];
    private readonly List<RecentNavigationItem> _recentItems = [];

    private int _historyPosition = -1;
    private bool _suppressHistory;

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

    /// <summary>
    /// Gets every area switch and object open/jump, oldest first — the
    /// back/forward stack <see cref="GoBackAsync"/>/<see cref="GoForwardAsync"/>
    /// traverse. Not one of the twelve `WP8.0B Workspace Contracts.md`
    /// interfaces; a genuine, disclosed implementation-phase addition
    /// (`WP8.0C Navigation Maps.md` §4).
    /// </summary>
    public IReadOnlyList<NavigationHistoryEntry> History => _history;

    /// <summary>Gets a value indicating whether <see cref="GoBackAsync"/> would move.</summary>
    public bool CanGoBack => _historyPosition > 0;

    /// <summary>Gets a value indicating whether <see cref="GoForwardAsync"/> would move.</summary>
    public bool CanGoForward => _historyPosition >= 0 && _historyPosition < _history.Count - 1;

    /// <summary>
    /// Gets the most-recently-opened or jumped-to objects, most recent
    /// first, capped at <see cref="MaxRecentItems"/> — the Workspace's own
    /// "recent items" surface (`WP8.0C Navigation Maps.md` §5). Global, not
    /// per-project — a disclosed simplification (`WP8.1B Implementation
    /// Report.md`).
    /// </summary>
    public IReadOnlyList<RecentNavigationItem> RecentItems => _recentItems;

    /// <inheritdoc />
    public IReadOnlyList<NavigationItem> Areas => _navigationProvider.Items;

    /// <inheritdoc />
    public async Task SwitchAreaAsync(string areaId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);

        await _navigationProvider.Navigate(areaId, cancellationToken).ConfigureAwait(false);

        CurrentAreaId = areaId;

        if (!_suppressHistory)
        {
            var title = _navigationProvider.Items.FirstOrDefault(item => item.Id == areaId)?.Title ?? areaId;
            RecordHistory(new NavigationHistoryEntry(areaId, null, null, title));
        }
    }

    /// <inheritdoc />
    public async Task<IWorkspaceView> OpenAsync(Guid objectId, string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        var existing = _openViews.FirstOrDefault(v => v.ObjectId == objectId && v.ObjectKind == kind);
        IWorkspaceView view;

        if (existing is not null)
        {
            SetActive(existing);
            view = existing;
        }
        else
        {
            if (!_viewFactories.TryGetValue(kind, out var factory))
                throw new WorkspaceViewFactoryNotFoundException(kind);

            view = factory.Create(objectId, _context);
            _openViews.Add(view);
            SetActive(view);
        }

        if (!_suppressHistory)
        {
            RecordHistory(new NavigationHistoryEntry(null, objectId, kind, view.Title));
            RecordRecentItem(objectId, kind, view.Title);
        }

        return view;
    }

    /// <inheritdoc />
    public async Task<IWorkspaceView> JumpToAsync(Guid targetObjectId, string targetKind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        if (!_viewFactories.TryGetValue(targetKind, out var factory))
            throw new WorkspaceViewFactoryNotFoundException(targetKind);

        var view = factory.Create(targetObjectId, _context);
        _openViews.Add(view);
        SetActive(view);

        if (!_suppressHistory)
        {
            RecordHistory(new NavigationHistoryEntry(null, targetObjectId, targetKind, view.Title));
            RecordRecentItem(targetObjectId, targetKind, view.Title);
        }

        return view;
    }

    /// <summary>
    /// Moves one step back in <see cref="History"/>, re-applying that
    /// entry (an area switch or an object open) without pushing a new
    /// history entry. Returns <see langword="false"/> if already at the
    /// oldest entry.
    /// </summary>
    public Task<bool> GoBackAsync(CancellationToken cancellationToken = default)
    {
        if (_historyPosition <= 0)
            return Task.FromResult(false);

        _historyPosition--;
        return ReplayAsync(_history[_historyPosition], cancellationToken);
    }

    /// <summary>
    /// Moves one step forward in <see cref="History"/>. Returns
    /// <see langword="false"/> if already at the newest entry.
    /// </summary>
    public Task<bool> GoForwardAsync(CancellationToken cancellationToken = default)
    {
        if (_historyPosition < 0 || _historyPosition >= _history.Count - 1)
            return Task.FromResult(false);

        _historyPosition++;
        return ReplayAsync(_history[_historyPosition], cancellationToken);
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

    private void RecordHistory(NavigationHistoryEntry entry)
    {
        if (_historyPosition < _history.Count - 1)
            _history.RemoveRange(_historyPosition + 1, _history.Count - _historyPosition - 1);

        _history.Add(entry);
        _historyPosition = _history.Count - 1;
    }

    private void RecordRecentItem(Guid objectId, string kind, string title)
    {
        _recentItems.RemoveAll(item => item.ObjectId == objectId);
        _recentItems.Insert(0, new RecentNavigationItem(objectId, kind, title, DateTimeOffset.UtcNow));

        if (_recentItems.Count > MaxRecentItems)
            _recentItems.RemoveAt(_recentItems.Count - 1);
    }

    private async Task<bool> ReplayAsync(NavigationHistoryEntry entry, CancellationToken cancellationToken)
    {
        _suppressHistory = true;
        try
        {
            if (entry.ObjectId is { } objectId && entry.ObjectKind is { } kind)
                await OpenAsync(objectId, kind, cancellationToken).ConfigureAwait(false);
            else if (entry.AreaId is { } areaId)
                await SwitchAreaAsync(areaId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _suppressHistory = false;
        }

        return true;
    }
}
