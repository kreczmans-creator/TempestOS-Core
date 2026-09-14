using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Shell;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Projects;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views.Dashboards;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Projects module (`WP 19.7A`, Product Owner IA sketches items 6):
/// a tree — Dashboard + Reports, Open, Closed (under 90 days), Archive (90
/// days and over) — with a right pane over whichever node is selected.
/// Selecting a project leaf opens its workspace exactly as the retired
/// standalone Projects rail button always did; the three groups reuse the
/// one <see cref="ProjectBrowserView"/> instance the composer already
/// builds, filtered to the selected group's own project ids
/// (<see cref="ProjectArchival"/>).
/// </summary>
/// <remarks>
/// <see cref="ProjectSummary"/>/<see cref="IProjectDirectory"/> carry no
/// <c>ClosedOn</c> (that fact lives only on the real
/// <see cref="Tempest.Core.EngineeringDomain.Project"/> domain object, `WP
/// 19.5C`) — grouping reads the domain directly through
/// <see cref="EngineeringDomainContext"/>, the same "sibling reader" shape
/// <c>ProjectStatusReadModel</c>/<c>TasksReadModelService</c> already use,
/// rather than changing that read model (out of this Work Package's own
/// scope).
/// </remarks>
public sealed class ProjectsAreaView : UserControl
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ProjectBrowserView _projectBrowser;
    private readonly ProjectsDashboardView _dashboard;
    private readonly TimeProvider _time;

    private readonly TreeView _tree = new();
    private readonly ContentControl _detail = new();
    private readonly CollapsibleColumn _treeColumn;

    private readonly TreeViewItem _dashboardNode = new() { Header = "Dashboard + Reports" };
    private readonly TreeViewItem _openNode = new() { Header = "Open", IsExpanded = true };
    private readonly TreeViewItem _closedNode = new() { Header = "Closed (under 90 days)" };
    private readonly TreeViewItem _archiveNode = new() { Header = "Archive (90 days and over)" };

    private readonly WorkspaceChangesSubscription _workspaceChanges;
    private bool _suppressSelection;

    /// <summary>The change feed this view re-groups every project from.</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="ProjectsAreaView"/> class.</summary>
    /// <param name="domainContext">Reads every project's real <c>ClosedOn</c>/<c>Held</c> facts for grouping.</param>
    /// <param name="projectBrowser">The single, already-composed project catalogue — reused, filtered, for each group.</param>
    /// <param name="dashboard">The "Dashboard + Reports" node's own real content (`WP 19.7B`).</param>
    /// <param name="timeProvider">The clock the 90-day Archive rule reads "as of". <see langword="null"/> is <see cref="TimeProvider.System"/>.</param>
    public ProjectsAreaView(EngineeringDomainContext domainContext, ProjectBrowserView projectBrowser, ProjectsDashboardView dashboard, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(projectBrowser);
        ArgumentNullException.ThrowIfNull(dashboard);

        _domainContext = domainContext;
        _projectBrowser = projectBrowser;
        _dashboard = dashboard;
        _dashboard.OpenProjectRequestedAsync += id => OpenProjectRequestedAsync?.Invoke(id) ?? Task.CompletedTask;
        _projectBrowser.ProjectCreated += OnProjectCreated;
        _time = timeProvider ?? TimeProvider.System;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        _tree.Items.Add(_dashboardNode);
        _tree.Items.Add(_openNode);
        _tree.Items.Add(_closedNode);
        _tree.Items.Add(_archiveNode);

        AutomationProperties.SetName(_tree, "Projects tree");
        AutomationProperties.SetName(_dashboardNode, "Dashboard + Reports");
        AutomationProperties.SetName(_openNode, "Open");
        AutomationProperties.SetName(_closedNode, "Closed");
        AutomationProperties.SetName(_archiveNode, "Archive");

        _tree.SelectionChanged += (_, _) => _ = OnSelectionChangedAsync();

        var split = new DockPanel();
        _treeColumn = new CollapsibleColumn("Projects", _tree);
        DockPanel.SetDock(_treeColumn, Dock.Left);
        split.Children.Add(_treeColumn);

        _detail.Margin = DesignTokens.PagePadding;
        split.Children.Add(_detail);

        AutomationProperties.SetName(this, "Projects");
        Content = split;
    }

    /// <summary>
    /// Selects the root node named <paramref name="automationName"/>
    /// ("Dashboard + Reports", "Open", "Closed" or "Archive") — the same
    /// name a screen reader announces, and what a journey test drives the
    /// tree by, exactly as <c>ProjectWorkspaceView.SyncSelectedArea</c>
    /// selects its own <c>TabControl</c> programmatically.
    /// </summary>
    public void SelectNode(string automationName)
    {
        var item = new[] { _dashboardNode, _openNode, _closedNode, _archiveNode }
            .Single(i => string.Equals(AutomationProperties.GetName(i), automationName, StringComparison.Ordinal));
        _tree.SelectedItem = item;
    }

    /// <summary>
    /// Narrows the tree below the shell's own compact threshold — the same
    /// one threshold <c>GlobalNavigationRail</c>/<c>RibbonView</c>/
    /// <c>LibrariesView</c>/<c>EngineeringAreaView</c> already fold on.
    /// </summary>
    public void SetCompact(bool compact) => _treeColumn.SetCompact(compact);

    /// <summary>Gets whether the tree column is currently manually collapsed to its own strip (`WP 19.10O`).</summary>
    public bool IsTreeCollapsed => _treeColumn.IsCollapsed;

    /// <summary>Collapses the tree column to its own strip, or restores it (`WP 19.10O`).</summary>
    public void SetTreeCollapsed(bool collapsed) => _treeColumn.SetCollapsed(collapsed);

    /// <summary>Raised after <see cref="SetTreeCollapsed"/> changes the tree column's own collapsed state — the caller's own cue to persist it.</summary>
    public event Action<bool>? TreeCollapsedChanged
    {
        add => _treeColumn.CollapsedChanged += value;
        remove => _treeColumn.CollapsedChanged -= value;
    }

    /// <summary>Test-only (`WP 19.7C`, <c>WorkspaceChangesReattachTests</c>): counts every <see cref="RefreshAsync"/> call, proving a reattached view's subscription still reaches <see cref="OnWorkspaceChanged"/>.</summary>
    internal int RefreshCount { get; private set; }

    /// <summary>Re-reads every project and rebuilds each group's own children.</summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;

        var everyProject = await _domainContext.Repository
            .ListByKindAsync(MechanicalObjectFactoryRegistry.Project)
            .ConfigureAwait(true);

        var asOf = _time.GetUtcNow();
        var open = new List<Tempest.Core.EngineeringDomain.Project>();
        var closed = new List<Tempest.Core.EngineeringDomain.Project>();
        var archive = new List<Tempest.Core.EngineeringDomain.Project>();

        foreach (var candidate in everyProject)
        {
            if (candidate is not Tempest.Core.EngineeringDomain.Project project || project is IDeletable { IsDeleted: true })
                continue;

            switch (ProjectArchival.ListingGroupOf(project, asOf))
            {
                case ProjectListingGroup.Open: open.Add(project); break;
                case ProjectListingGroup.Closed: closed.Add(project); break;
                default: archive.Add(project); break;
            }
        }

        Populate(_openNode, open);
        Populate(_closedNode, closed);
        Populate(_archiveNode, archive);

        _openNode.Header = $"Open ({open.Count})";
        _closedNode.Header = $"Closed (under 90 days) ({closed.Count})";
        _archiveNode.Header = $"Archive (90 days and over) ({archive.Count})";

        if (_tree.SelectedItem is null)
        {
            _suppressSelection = true;
            _dashboardNode.IsSelected = true;
            _suppressSelection = false;
            await _dashboard.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _dashboard;
            return;
        }

        // A group (Open/Closed/Archive) was already selected on a
        // previous entry — re-read its own content too, the same "load
        // when you land here" discipline every other area follows,
        // rather than leaving whatever it last showed on screen stale.
        await OnSelectionChangedAsync().ConfigureAwait(true);
    }

    private static void Populate(TreeViewItem node, IReadOnlyList<Tempest.Core.EngineeringDomain.Project> projects)
    {
        node.Items.Clear();

        foreach (var project in projects.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var label = string.IsNullOrWhiteSpace(project.Identifier) ? project.DisplayName : $"{project.Identifier} {project.DisplayName}";
            var leaf = new TreeViewItem { Header = label, Tag = project.Id };
            AutomationProperties.SetName(leaf, label);
            node.Items.Add(leaf);
        }
    }

    private async Task OnSelectionChangedAsync()
    {
        if (_suppressSelection)
            return;

        if (_tree.SelectedItem is not TreeViewItem selected)
            return;

        if (ReferenceEquals(selected, _dashboardNode))
        {
            await _dashboard.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _dashboard;
            return;
        }

        if (selected.Tag is Guid projectId)
        {
            if (OpenProjectRequestedAsync is { } handler)
                await handler(projectId).ConfigureAwait(true);
            return;
        }

        // A group header (Open/Closed/Archive) — reuse the single
        // ProjectBrowserView instance, filtered to this group's own
        // project ids.
        var ids = selected.Items.OfType<TreeViewItem>().Select(i => (Guid)i.Tag!).ToHashSet();
        _projectBrowser.SetVisibleProjects(ids);
        await _projectBrowser.RefreshAsync().ConfigureAwait(true);
        _detail.Content = _projectBrowser;
    }

    /// <summary>Raised when the user selects a project leaf — the shell opens it.</summary>
    public event Func<Guid, Task>? OpenProjectRequestedAsync;

    /// <summary>
    /// The shared <see cref="ProjectBrowserView"/> just created a project
    /// (`WP 19.10Q`) — extends the Open group's own visible set
    /// synchronously, so <see cref="ProjectBrowserView.RefreshAsync"/>'s
    /// very next call (already under way, as part of that same create
    /// path) agrees with reality immediately, rather than waiting for
    /// <see cref="OnWorkspaceChanged"/>'s later, fire-and-forget reaction
    /// to the change feed. A project just created is open by definition,
    /// so only the Open node's own set is ever extended here — Closed and
    /// Archive keep showing whatever they already did.
    /// </summary>
    private void OnProjectCreated(Guid projectId)
    {
        if (!ReferenceEquals(_tree.SelectedItem, _openNode))
            return;

        var ids = _openNode.Items.OfType<TreeViewItem>().Select(i => (Guid)i.Tag!).ToHashSet();
        ids.Add(projectId);
        _projectBrowser.SetVisibleProjects(ids);
    }

    private void OnWorkspaceChanged(WorkspaceChange change) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Best-effort background refresh — mirrors ReportsView's
                // own identical "the next real entry is the backstop"
                // shape (`OnWorkspaceChanged`'s own remarks there).
            }
        });
}
