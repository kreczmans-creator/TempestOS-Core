using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Shell;
using Tempest.Workspace.Tasks;
using Tempest.Core.Commands;
using Tempest.Core.Events;
using Tempest.Desktop;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views.Dashboards;
using Tempest.Desktop.Views.EngineeringAssets;

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
/// rendering anything new: Dashboard + Reports carries
/// <see cref="EngineeringDashboardView"/> (`WP 19.7B`) plus the existing
/// <see cref="ReportsView"/>; Tasks shows the existing
/// <see cref="ITasksReadModel"/>'s own Calculations, Reviews and Approvals
/// (`WP 20.1B`, `TD-181` gives the read model its own Calculations bucket
/// — the sketched sub-heading this view used to disclose as missing);
/// Modules → Mechanical navigates on to
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
    private readonly EngineeringAssetsView _engineeringAssets;
    private readonly Func<Task> _onEngineeringCalculationSelected;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly Action<Guid, string> _openObjectRightUp;

    private readonly EngineeringDashboardView _dashboard;

    private readonly TreeView _tree = new();
    private readonly ContentControl _detail = new();
    private readonly CollapsibleColumn _treeColumn;
    private readonly StackPanel _dashboardStack = new() { Spacing = DesignTokens.SpaceLg };
    private readonly ScrollViewer _dashboardScroll;
    private readonly StackPanel _tasksPanel = new() { Spacing = DesignTokens.SpaceLg, Margin = DesignTokens.PagePadding };
    private readonly ScrollViewer _tasksScroll;

    private readonly TreeViewItem _dashboardNode = new() { Header = "Dashboard + Reports" };
    private readonly TreeViewItem _tasksNode = new() { Header = "Tasks" };
    private readonly TreeViewItem _modulesNode = new() { Header = "Modules", IsExpanded = true };
    private readonly TreeViewItem _mechanicalNode = new() { Header = "Mechanical" };
    private readonly TreeViewItem _calculationsNode = new() { Header = "Engineering Calculations" };
    private readonly TreeViewItem _assetsNode = new() { Header = "Engineering Assets" };
    private readonly TreeViewItem _referenceDataNode = new() { Header = "Reference data" };

    private readonly WorkspaceChangesSubscription _workspaceChanges;
    private bool _suppressSelection;

    /// <summary>Raised after the user asks to enter the Mechanical module, so the shell can render the ribbon-and-docking surface.</summary>
    public event Action? EngineeringRequested;

    /// <summary>Raised after Complete completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>The change feed the Dashboard + Reports and Tasks nodes reload from while shown.</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="EngineeringAreaView"/> class.</summary>
    /// <param name="openObjectRightUp">Opens a Tasks row's own source object right up — the same delegate every other rail area's Tasks/dashboard rows already use.</param>
    /// <param name="engineeringAssets">The merged engineering capability's own area (`WP 21.2B`; `TD-160`, `TD-165`) — Modules → Engineering Assets.</param>
    public EngineeringAreaView(
        IShellNavigator navigator, ITasksReadModel tasksReadModel, ReportsView reportsView,
        EngineeringCalculationView engineeringCalculation, LibrariesView referenceData,
        EngineeringDashboardView dashboard, Func<Task> onEngineeringCalculationSelected,
        ICommandDispatcher commandDispatcher, Action<Guid, string> openObjectRightUp,
        EngineeringAssetsView engineeringAssets)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(tasksReadModel);
        ArgumentNullException.ThrowIfNull(reportsView);
        ArgumentNullException.ThrowIfNull(engineeringCalculation);
        ArgumentNullException.ThrowIfNull(referenceData);
        ArgumentNullException.ThrowIfNull(dashboard);
        ArgumentNullException.ThrowIfNull(onEngineeringCalculationSelected);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);
        ArgumentNullException.ThrowIfNull(engineeringAssets);

        _navigator = navigator;
        _tasksReadModel = tasksReadModel;
        _reportsView = reportsView;
        _engineeringCalculation = engineeringCalculation;
        _referenceData = referenceData;
        _engineeringAssets = engineeringAssets;
        _dashboard = dashboard;
        _onEngineeringCalculationSelected = onEngineeringCalculationSelected;
        _commandDispatcher = commandDispatcher;
        _openObjectRightUp = openObjectRightUp;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        _dashboardStack.Children.Add(_dashboard);
        _dashboardStack.Children.Add(_reportsView);
        _dashboardScroll = new ScrollViewer { Content = _dashboardStack };
        _tasksScroll = new ScrollViewer { Content = _tasksPanel };

        _modulesNode.Items.Add(_mechanicalNode);
        _modulesNode.Items.Add(_calculationsNode);
        _modulesNode.Items.Add(_assetsNode);

        _tree.Items.Add(_dashboardNode);
        _tree.Items.Add(_tasksNode);
        _tree.Items.Add(_modulesNode);
        _tree.Items.Add(_referenceDataNode);

        foreach (var (node, name) in new[]
                 {
                     (_dashboardNode, "Dashboard + Reports"), (_tasksNode, "Tasks"), (_modulesNode, "Modules"),
                     (_mechanicalNode, "Mechanical"), (_calculationsNode, "Engineering Calculations"),
                     (_assetsNode, "Engineering Assets"), (_referenceDataNode, "Reference data"),
                 })
            AutomationProperties.SetName(node, name);
        AutomationProperties.SetName(_tree, "Engineering tree");

        _tree.SelectionChanged += (_, _) => _ = OnSelectionChangedAsync();

        var split = new DockPanel();
        _treeColumn = new CollapsibleColumn("Engineering", _tree);
        DockPanel.SetDock(_treeColumn, Dock.Left);
        split.Children.Add(_treeColumn);

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
        var item = new[]
            {
                _dashboardNode, _tasksNode, _modulesNode, _mechanicalNode, _calculationsNode, _assetsNode, _referenceDataNode,
            }
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
        _treeColumn.SetCompact(compact);
        _engineeringAssets.SetCompact(compact);
    }

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

    /// <summary>Re-reads whichever node is currently shown.</summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;

        if (_tree.SelectedItem is null)
        {
            // Set synchronously first — `IsSelected` also fires
            // `_tree.SelectionChanged`, which re-runs the identical
            // content build fire-and-forget, but a caller awaiting this
            // method must see real content the instant it returns, not
            // only once that later task happens to complete.
            _dashboardNode.IsSelected = true;
            await Task.WhenAll(_dashboard.RefreshAsync(), _reportsView.RefreshAsync()).ConfigureAwait(true);
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
            await Task.WhenAll(_dashboard.RefreshAsync(), _reportsView.RefreshAsync()).ConfigureAwait(true);
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

        if (ReferenceEquals(selected, _assetsNode))
        {
            await _engineeringAssets.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _engineeringAssets;
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
        _tasksPanel.Children.Add(PageHeading.Title("Calculations, reviews and approvals"));

        AddCalculationsSection(snapshot.Calculations);
        AddSection("Reviews", snapshot.Reviews);
        AddSection("Approvals", snapshot.Approvals);
    }

    /// <summary>`WP 20.1B` (`TD-181`): every open Calculation, each row opening it right up and offering Complete.</summary>
    private void AddCalculationsSection(IReadOnlyList<TaskItem> items)
    {
        var section = new StackPanel { Spacing = DesignTokens.SpaceSm };
        section.Children.Add(new TextBlock { Text = $"Calculations ({items.Count})", FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2 });

        if (items.Count == 0)
        {
            section.Children.Add(new TextBlock { Text = "Nothing here.", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 });
        }
        else
        {
            foreach (var item in items)
                section.Children.Add(CalculationRow(item));
        }

        _tasksPanel.Children.Add(section);
    }

    private Control CalculationRow(TaskItem item)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        row.Children.Add(new TextBlock { Text = item.Title, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center });

        var open = new Button { Content = "Open", MinHeight = DesignTokens.ControlSizeSmall };
        open.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(open, $"Open {item.Title}");
        open.Click += (_, _) => _openObjectRightUp(item.ObjectId, item.Kind);
        row.Children.Add(open);

        var complete = new Button { Content = "Complete", MinHeight = DesignTokens.ControlSizeSmall };
        complete.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(complete, $"Complete {item.Title}");
        complete.Click += async (_, _) => await OnCompleteAsync(item).ConfigureAwait(true);
        row.Children.Add(complete);

        return row;
    }

    private async Task OnCompleteAsync(TaskItem item)
    {
        var result = await _commandDispatcher
            .DispatchAsync(new CompleteCalculationCommand(item.ObjectId, item.Kind), CancellationToken.None)
            .ConfigureAwait(true);

        ActionCompleted?.Invoke(result.Message ?? "Complete failed.", ActionOutcome.From(result.Succeeded));

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
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
