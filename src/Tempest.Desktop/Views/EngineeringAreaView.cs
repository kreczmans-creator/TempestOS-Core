using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Workspace.Shell;
using Tempest.Workspace.Tasks;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Engineering module's own rail landing page (`WP 19.7A`, Product
/// Owner IA sketches item 6, sheet 8): a tree — Dashboard + Reports, Tasks
/// (Calculations, Reviews, Approvals), Modules (Mechanical; Electrical and
/// Structural are future and not shown), Reference data — with a right
/// pane over whichever node is selected.
/// </summary>
/// <remarks>
/// <para>
/// Every node embeds an already-built, already-tested view rather than
/// rendering anything new: Dashboard + Reports carries the one disclosed
/// placeholder this release allows plus the existing <see cref="ReportsView"/>;
/// Tasks filters the existing <see cref="ITasksReadModel"/> to Reviews and
/// Approvals (the read model has no "Calculations" bucket of its own to
/// filter by, so that sketched sub-heading is not shown — the kill
/// switch's own "leave it out and say so" rather than inventing a second
/// placeholder); Modules → Mechanical navigates on to
/// <see cref="ShellArea.Engineering"/>, the ribbon-and-docking surface's
/// own long-established scope-aware location, exactly as it always has
/// (see <see cref="ShellArea.EngineeringDepartment"/>'s own remarks) —
/// Electrical and Structural are the Product Owner's own sketched
/// "future" modules and are deliberately not shown; Modules → Engineering
/// Calculations embeds the existing calculation surface; Reference data
/// embeds a second <see cref="LibrariesView"/> instance (the first is
/// already parented inside <see cref="EvidenceWorkspaceView"/>, and a
/// control can only ever be parented once).
/// </para>
/// </remarks>
public sealed class EngineeringAreaView : UserControl
{
    private readonly IShellNavigator _navigator;
    private readonly ITasksReadModel _tasksReadModel;
    private readonly ReportsView _reportsView;
    private readonly EngineeringCalculationView _engineeringCalculation;
    private readonly LibrariesView _referenceData;
    private readonly Func<Task> _onEngineeringCalculationSelected;

    private readonly TreeView _tree = new() { MinWidth = 260, MaxWidth = 260 };
    private readonly ContentControl _detail = new();
    private Border? _treeHost;
    private readonly Control _dashboardPlaceholder;
    private readonly StackPanel _dashboardStack = new() { Spacing = DesignTokens.SpaceLg };
    private readonly ScrollViewer _dashboardScroll;
    private readonly StackPanel _tasksPanel = new() { Spacing = DesignTokens.SpaceLg, Margin = DesignTokens.PagePadding };
    private readonly ScrollViewer _tasksScroll;

    private readonly TreeViewItem _dashboardNode = new() { Header = "Dashboard + Reports" };
    private readonly TreeViewItem _tasksNode = new() { Header = "Tasks" };
    private readonly TreeViewItem _modulesNode = new() { Header = "Modules", IsExpanded = true };
    private readonly TreeViewItem _mechanicalNode = new() { Header = "Mechanical" };
    private readonly TreeViewItem _calculationsNode = new() { Header = "Engineering Calculations" };
    private readonly TreeViewItem _referenceDataNode = new() { Header = "Reference data" };

    private IWorkspaceChanges? _workspaceChanges;
    private bool _suppressSelection;

    /// <summary>Raised after the user asks to enter the Mechanical module, so the shell can render the ribbon-and-docking surface.</summary>
    public event Action? EngineeringRequested;

    /// <summary>The change feed the Dashboard + Reports and Tasks nodes reload from while shown.</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges;
        set
        {
            if (ReferenceEquals(_workspaceChanges, value))
                return;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed -= OnWorkspaceChanged;

            _workspaceChanges = value;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed += OnWorkspaceChanged;
        }
    }

    /// <summary>Initialises a new instance of the <see cref="EngineeringAreaView"/> class.</summary>
    public EngineeringAreaView(
        IShellNavigator navigator, ITasksReadModel tasksReadModel, ReportsView reportsView,
        EngineeringCalculationView engineeringCalculation, LibrariesView referenceData,
        Func<Task> onEngineeringCalculationSelected)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(tasksReadModel);
        ArgumentNullException.ThrowIfNull(reportsView);
        ArgumentNullException.ThrowIfNull(engineeringCalculation);
        ArgumentNullException.ThrowIfNull(referenceData);
        ArgumentNullException.ThrowIfNull(onEngineeringCalculationSelected);

        _navigator = navigator;
        _tasksReadModel = tasksReadModel;
        _reportsView = reportsView;
        _engineeringCalculation = engineeringCalculation;
        _referenceData = referenceData;
        _onEngineeringCalculationSelected = onEngineeringCalculationSelected;

        this.DetachedFromVisualTree += (_, _) => WorkspaceChanges = null;

        _dashboardPlaceholder = new EmptyStateView(
            "▤",
            "Engineering dashboard — not built yet",
            "WP 19.7B fills this in: Open tasks and Engineering reviews tiles. The Reports panel below is real today.");

        _dashboardStack.Children.Add(_dashboardPlaceholder);
        _dashboardStack.Children.Add(_reportsView);
        _dashboardScroll = new ScrollViewer { Content = _dashboardStack };
        _tasksScroll = new ScrollViewer { Content = _tasksPanel };

        _modulesNode.Items.Add(_mechanicalNode);
        _modulesNode.Items.Add(_calculationsNode);

        _tree.Items.Add(_dashboardNode);
        _tree.Items.Add(_tasksNode);
        _tree.Items.Add(_modulesNode);
        _tree.Items.Add(_referenceDataNode);

        foreach (var (node, name) in new[]
                 {
                     (_dashboardNode, "Dashboard + Reports"), (_tasksNode, "Tasks"), (_modulesNode, "Modules"),
                     (_mechanicalNode, "Mechanical"), (_calculationsNode, "Engineering Calculations"), (_referenceDataNode, "Reference data"),
                 })
            AutomationProperties.SetName(node, name);
        AutomationProperties.SetName(_tree, "Engineering tree");

        _tree.SelectionChanged += (_, _) => _ = OnSelectionChangedAsync();

        var split = new DockPanel();
        _treeHost = new Border
        {
            Child = _tree,
            Width = 260,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(0, DesignTokens.SpaceMd, 0, 0),
        };
        ThemeReactiveBrush.Bind(_treeHost, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        DockPanel.SetDock(_treeHost, Dock.Left);
        split.Children.Add(_treeHost);

        _detail.Content = _dashboardScroll;
        split.Children.Add(_detail);

        AutomationProperties.SetName(this, "Engineering");
        Content = split;
    }

    /// <summary>
    /// Selects the node named <paramref name="automationName"/>
    /// ("Dashboard + Reports", "Tasks", "Modules", "Mechanical",
    /// "Engineering Calculations" or "Reference data") — the same name a
    /// screen reader announces, and what a journey test drives the tree
    /// by.
    /// </summary>
    public void SelectNode(string automationName)
    {
        var item = new[] { _dashboardNode, _tasksNode, _modulesNode, _mechanicalNode, _calculationsNode, _referenceDataNode }
            .Single(i => string.Equals(AutomationProperties.GetName(i), automationName, StringComparison.Ordinal));
        _tree.SelectedItem = item;
    }

    /// <summary>
    /// Narrows the tree below the shell's own compact threshold, giving
    /// embedded content (Engineering Calculations, at the narrowest
    /// supported width) the room it needs — the same one threshold
    /// <c>GlobalNavigationRail</c>/<c>RibbonView</c>/<c>LibrariesView</c>
    /// already fold on.
    /// </summary>
    public void SetCompact(bool compact)
    {
        var width = compact ? 160 : 260;
        _tree.MinWidth = width;
        _tree.MaxWidth = width;
        if (_treeHost is not null)
            _treeHost.Width = width;
    }

    /// <summary>Re-reads whichever node is currently shown.</summary>
    public async Task RefreshAsync()
    {
        if (_tree.SelectedItem is null)
        {
            // Set synchronously first — `IsSelected` also fires
            // `_tree.SelectionChanged`, which re-runs the identical
            // content build fire-and-forget, but a caller awaiting this
            // method must see real content the instant it returns, not
            // only once that later task happens to complete.
            _dashboardNode.IsSelected = true;
            await _reportsView.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _dashboardScroll;
            return;
        }

        await OnSelectionChangedAsync().ConfigureAwait(true);
    }

    private async Task OnSelectionChangedAsync()
    {
        if (_suppressSelection)
            return;

        if (_tree.SelectedItem is not TreeViewItem selected)
            return;

        if (ReferenceEquals(selected, _dashboardNode))
        {
            await _reportsView.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _dashboardScroll;
            return;
        }

        if (ReferenceEquals(selected, _tasksNode))
        {
            await RenderTasksAsync().ConfigureAwait(true);
            _detail.Content = _tasksScroll;
            return;
        }

        if (ReferenceEquals(selected, _mechanicalNode))
        {
            // Real navigation away, to the ribbon-and-docking surface —
            // reset the tree's own selection first, suppressed so it does
            // not re-enter this handler: leaving Mechanical selected would
            // make the next entry into this area navigate straight back
            // out again (`RefreshAsync`'s own "something is already
            // selected, re-read it" branch) instead of showing the tree.
            _suppressSelection = true;
            _tree.SelectedItem = null;
            _suppressSelection = false;

            await _navigator.GoToEngineeringAsync().ConfigureAwait(true);
            EngineeringRequested?.Invoke();
            return;
        }

        if (ReferenceEquals(selected, _calculationsNode))
        {
            await _onEngineeringCalculationSelected().ConfigureAwait(true);
            _detail.Content = _engineeringCalculation;
            return;
        }

        if (ReferenceEquals(selected, _referenceDataNode))
        {
            await _referenceData.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _referenceData;
            return;
        }
    }

    private async Task RenderTasksAsync()
    {
        var snapshot = await _tasksReadModel.ReadAsync().ConfigureAwait(true);
        _tasksPanel.Children.Clear();

        _tasksPanel.Children.Add(PageHeading.Label("ENGINEERING · TASKS"));
        _tasksPanel.Children.Add(PageHeading.Title("Reviews and approvals"));
        _tasksPanel.Children.Add(PageHeading.Lead(
            "Evidence awaiting check or issue — the Tasks read model carries no separate Calculations bucket, so that sketched sub-heading is not shown here."));

        AddSection("Reviews", snapshot.Reviews);
        AddSection("Approvals", snapshot.Approvals);
    }

    private void AddSection(string title, IReadOnlyList<TaskItem> items)
    {
        var section = new StackPanel { Spacing = DesignTokens.SpaceSm };
        section.Children.Add(new TextBlock { Text = $"{title} ({items.Count})", FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2 });

        if (items.Count == 0)
        {
            section.Children.Add(new TextBlock { Text = "Nothing here.", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 });
        }
        else
        {
            foreach (var item in items)
                section.Children.Add(new TextBlock { Text = item.Title, FontSize = DesignTokens.FontSizeBody });
        }

        _tasksPanel.Children.Add(section);
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
