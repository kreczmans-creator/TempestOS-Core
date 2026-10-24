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
/// Owner IA sketches item 6, sheet 8): a tree — Dashboard, Tasks
/// (Calculations, Reviews, Approvals), Disciplines, Modules — with a
/// right pane over whichever node is selected.
/// </summary>
/// <remarks>
/// <para>
/// Every node embeds an already-built, already-tested view rather than
/// rendering anything new: Dashboard carries
/// <see cref="EngineeringDashboardView"/> (`WP 19.7B`; the
/// <see cref="ReportsView"/> list beneath it was withdrawn for the RC —
/// reports return once real client use shows what they need to be);
/// Tasks shows the existing
/// <see cref="ITasksReadModel"/>'s own Calculations, Reviews and Approvals
/// (`WP 20.1B`, `TD-181` gives the read model its own Calculations bucket
/// — the sketched sub-heading this view used to disclose as missing);
/// each Disciplines node (<see cref="EngineeringDisciplines.Disciplines"/>)
/// and each Modules node (<see cref="EngineeringDisciplines.Modules"/>,
/// `ADR-0163`) navigates to <see cref="ShellArea.Engineering"/> and shows
/// that category's command bar, Explorer and Properties *in this right
/// pane*, with the tree still beside it (`v0.24.0` Product Owner review);
/// Modules → Calculations' own children embed the calculators, the
/// register of issued sheets and the packs and templates; Modules →
/// Reference data embeds a second <see cref="LibrariesView"/> instance
/// (the first is already parented inside <see cref="EvidenceWorkspaceView"/>,
/// and a control can only ever be parented once).
/// </para>
/// </remarks>
public sealed class EngineeringAreaView : UserControl
{
    private readonly IShellNavigator _navigator;
    private readonly ITasksReadModel _tasksReadModel;
    private readonly EngineeringCalculationView _engineeringCalculation;
    private readonly LibrariesView _referenceData;
    private readonly EngineeringAssetsView _engineeringAssets;
    private readonly CalculationModulesView _calculators;
    private readonly Func<Task> _onEngineeringCalculationSelected;
    private readonly Func<Task> _onCalculatorsSelected;
    private readonly Tempest.Desktop.CalcSheets.CalcSheetView _worksheets;
    private readonly Func<Task> _onWorksheetsSelected;
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

    private readonly TreeViewItem _dashboardNode = new() { Header = "Dashboard" };
    private readonly TreeViewItem _tasksNode = new() { Header = "Tasks" };
    private readonly TreeViewItem _modulesNode = new() { Header = "Modules", IsExpanded = true };
    private readonly TreeViewItem _disciplinesNode = new() { Header = "Disciplines", IsExpanded = true };
    private readonly Dictionary<string, TreeViewItem> _disciplineNodes = new(StringComparer.Ordinal);
    private readonly TreeViewItem _calculationsNode = new() { Header = "Calculation register" };
    private readonly TreeViewItem _calculatorsNode = new() { Header = "Calculators" };
    private readonly TreeViewItem _worksheetsNode = new() { Header = "Worksheets" };
    private readonly Dictionary<string, TreeViewItem> _disciplineWorksheetNodes = new(StringComparer.Ordinal);
    private readonly TreeViewItem _assetsNode = new() { Header = "Packs and templates" };
    private readonly TreeViewItem _referenceDataNode = new() { Header = "Reference data" };

    private readonly WorkspaceChangesSubscription _workspaceChanges;
    private bool _suppressSelection;

    /// <summary>Raised after a tree node moved the shell between this landing page and the engineering surface, so the shell re-renders its module.</summary>
    public event Action? EngineeringRequested;

    /// <summary>Raised when a Disciplines node is picked, carrying its command category — the shell shows that discipline's commands and Explorer area.</summary>
    public event Action<string>? DisciplineSelected;

    /// <summary>Raised after Complete completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>The change feed the Dashboard and Tasks nodes reload from while shown.</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="EngineeringAreaView"/> class.</summary>
    /// <param name="openObjectRightUp">Opens a Tasks row's own source object right up — the same delegate every other rail area's Tasks/dashboard rows already use.</param>
    /// <param name="engineeringAssets">The merged engineering capability's own area (`WP 21.2B`; `TD-160`, `TD-165`) — Modules → Calculations → Packs and templates.</param>
    /// <param name="calculators">The Engineering Calculators (`WP 21.7B`) — Modules → Calculators: every product calculation from a generated form.</param>
    /// <param name="onCalculatorsSelected">Re-reads the calculators' catalogue and released materials on entry, the same shape as <paramref name="onEngineeringCalculationSelected"/>.</param>
    public EngineeringAreaView(
        IShellNavigator navigator, ITasksReadModel tasksReadModel,
        EngineeringCalculationView engineeringCalculation, LibrariesView referenceData,
        EngineeringDashboardView dashboard, Func<Task> onEngineeringCalculationSelected,
        ICommandDispatcher commandDispatcher, Action<Guid, string> openObjectRightUp,
        EngineeringAssetsView engineeringAssets, CalculationModulesView calculators, Func<Task> onCalculatorsSelected,
        Tempest.Desktop.CalcSheets.CalcSheetView worksheets, Func<Task> onWorksheetsSelected)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(tasksReadModel);
        ArgumentNullException.ThrowIfNull(engineeringCalculation);
        ArgumentNullException.ThrowIfNull(referenceData);
        ArgumentNullException.ThrowIfNull(dashboard);
        ArgumentNullException.ThrowIfNull(onEngineeringCalculationSelected);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);
        ArgumentNullException.ThrowIfNull(engineeringAssets);
        ArgumentNullException.ThrowIfNull(calculators);
        ArgumentNullException.ThrowIfNull(onCalculatorsSelected);
        ArgumentNullException.ThrowIfNull(worksheets);
        ArgumentNullException.ThrowIfNull(onWorksheetsSelected);

        _navigator = navigator;
        _tasksReadModel = tasksReadModel;
        _engineeringCalculation = engineeringCalculation;
        _referenceData = referenceData;
        _engineeringAssets = engineeringAssets;
        _calculators = calculators;
        _onCalculatorsSelected = onCalculatorsSelected;
        _worksheets = worksheets;
        _onWorksheetsSelected = onWorksheetsSelected;
        _dashboard = dashboard;
        _onEngineeringCalculationSelected = onEngineeringCalculationSelected;
        _commandDispatcher = commandDispatcher;
        _openObjectRightUp = openObjectRightUp;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        _dashboardStack.Children.Add(_dashboard);
        _dashboardScroll = new ScrollViewer { Content = _dashboardStack };
        _tasksScroll = new ScrollViewer { Content = _tasksPanel };

        // `ADR-0163`: Disciplines are engineering domains; Modules are the
        // shared tools beneath them. Each picks the engineering surface,
        // tagged with its command category. Calculations is the one home
        // for calculation work: its own node shows the project's
        // calculations on the surface, and its children hold the
        // calculators, the register of issued sheets, and the packs and
        // templates (what was Modules → Engineering Assets).
        foreach (var category in EngineeringDisciplines.Disciplines)
            _disciplinesNode.Items.Add(SurfaceNode(category));

        foreach (var category in EngineeringDisciplines.Modules)
        {
            var node = SurfaceNode(category);
            if (category == "Calculations")
            {
                node.IsExpanded = true;
                node.Items.Add(_calculatorsNode);
                node.Items.Add(_worksheetsNode);
                node.Items.Add(_calculationsNode);
                node.Items.Add(_assetsNode);
            }

            _modulesNode.Items.Add(node);
        }
        _modulesNode.Items.Add(_referenceDataNode);

        _tree.Items.Add(_dashboardNode);
        _tree.Items.Add(_tasksNode);
        _tree.Items.Add(_disciplinesNode);
        _tree.Items.Add(_modulesNode);

        foreach (var (node, name) in new[]
                 {
                     (_dashboardNode, "Dashboard"), (_tasksNode, "Tasks"), (_disciplinesNode, "Disciplines"), (_modulesNode, "Modules"),
                     (_calculationsNode, "Calculation register"), (_calculatorsNode, "Calculators"), (_worksheetsNode, "Worksheets"),
                     (_assetsNode, "Packs and templates"), (_referenceDataNode, "Reference data"),
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

        // The engineering surface needs the width more than the tree does:
        // the tree narrows to its longest row while the surface is shown
        // beside it, so the Explorer is not squeezed, and widens again when
        // it leaves. Not the compact width: rows there clip and scroll
        // sideways (real-shell stress finding F2).
        _detail.PropertyChanged += (_, e) =>
        {
            if (e.Property == ContentControl.ContentProperty)
                _treeColumn.SetExpandedWidth(IsShowingSurface ? SurfaceTreeWidth : CollapsibleColumn.ExpandedWidth);
        };

        AutomationProperties.SetName(this, "Engineering");
        Content = split;
    }

    /// <summary>`v0.26.0` (ADR-0165, D-11): the tag of a discipline's own Worksheets child node.</summary>
    private sealed record WorksheetsMarker(string Category);

    private TreeViewItem SurfaceNode(string category)
    {
        var node = new TreeViewItem { Header = EngineeringDisciplines.Label(category), Tag = category };
        AutomationProperties.SetName(node, EngineeringDisciplines.NavigatorName(category));
        _disciplineNodes[category] = node;

        if (Tempest.Desktop.CalcSheets.CalcSheetView.DisciplineFor(category) is not null)
        {
            var worksheets = new TreeViewItem { Header = "Worksheets", Tag = new WorksheetsMarker(category) };
            AutomationProperties.SetName(worksheets, $"{EngineeringDisciplines.Label(category)} worksheets");
            node.Items.Add(worksheets);
            _disciplineWorksheetNodes[category] = worksheets;
        }

        return node;
    }

    /// <summary>`v0.26.0`: opens the Worksheets node and selects <paramref name="templateReference"/> in it (the Ctrl+K entry per calculator).</summary>
    public async Task OpenWorksheetAsync(string templateReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateReference);

        if (_navigator.Current.Area != ShellArea.EngineeringDepartment)
            await _navigator.GoToModuleAsync(ShellArea.EngineeringDepartment).ConfigureAwait(true);

        _worksheets.FilterDiscipline(null);
        _tree.SelectedItem = _worksheetsNode;
        await _onWorksheetsSelected().ConfigureAwait(true);
        _detail.Content = _worksheets;
        _worksheets.SelectTemplate(templateReference);
    }

    /// <summary>
    /// Selects the node named <paramref name="automationName"/>
    /// ("Dashboard", "Tasks", "Disciplines", "Mechanical discipline" and
    /// its siblings, "Modules", "Requirements module" and its siblings,
    /// "Calculators", "Calculation register", "Packs and templates" or
    /// "Reference data") — the same name a
    /// screen reader announces, and what a journey test drives the tree
    /// by.
    /// </summary>
    public void SelectNode(string automationName)
    {
        var item = new[]
            {
                _dashboardNode, _tasksNode, _disciplinesNode, _modulesNode, _calculationsNode, _calculatorsNode, _assetsNode, _referenceDataNode,
            }
            .Concat(_disciplineNodes.Values)
            .Concat(_disciplineWorksheetNodes.Values)
            .Append(_worksheetsNode)
            .Single(i => string.Equals(AutomationProperties.GetName(i), automationName, StringComparison.Ordinal));
        _tree.SelectedItem = item;
    }

    /// <summary>
    /// Shows the engineering surface (command bar + docking) in the right
    /// pane, beside the tree — standalone Engineering's own render. The
    /// caller detaches <paramref name="surface"/> from wherever it was
    /// first; Avalonia refuses to reparent an attached control.
    /// </summary>
    public void ShowEngineeringSurface(Control surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _engineeringSurface = surface;
        if (!ReferenceEquals(_detail.Content, surface))
            _detail.Content = surface;
    }

    /// <summary>Frees the engineering surface from the right pane, so the shell can hand it to a project's Structure tab. A no-op when it is not here.</summary>
    public void ClearEngineeringSurface()
    {
        if (_engineeringSurface is not null && ReferenceEquals(_detail.Content, _engineeringSurface))
            _detail.Content = null;
    }

    /// <summary>The tree's width beside the engineering surface: its longest row ("Building Services / HVAC", nested) plus the column's border.</summary>
    private const double SurfaceTreeWidth = 216;

    /// <summary>
    /// Highlights <paramref name="category"/>'s Disciplines node without
    /// navigating — the shell calls it whenever the command bar's
    /// category changes (an object opened right up, say), and only while
    /// the surface is shown here.
    /// </summary>
    public void HighlightDiscipline(string category)
    {
        if (!IsShowingSurface || !_disciplineNodes.TryGetValue(category, out var node) || ReferenceEquals(_tree.SelectedItem, node))
            return;

        _suppressSelection = true;
        _tree.SelectedItem = node;
        _suppressSelection = false;
    }

    private Control? _engineeringSurface;

    private bool IsShowingSurface => _engineeringSurface is not null && ReferenceEquals(_detail.Content, _engineeringSurface);

    /// <summary>
    /// Narrows the tree below the shell's own compact threshold, giving
    /// embedded content (the calculation register, at the narrowest
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

        // While the surface is shown beside the tree it refreshes itself;
        // a background workspace change must not swap it for the dashboard.
        if (_navigator.Current.Area == ShellArea.Engineering && IsShowingSurface)
            return;

        // Arriving at this landing page with a discipline still selected
        // (the rail's Engineering entry, after working in a discipline)
        // starts again at the dashboard rather than bouncing straight back
        // into the surface.
        if (_tree.SelectedItem is TreeViewItem { Tag: string })
        {
            _suppressSelection = true;
            _tree.SelectedItem = null;
            _suppressSelection = false;
        }

        if (_tree.SelectedItem is null)
        {
            // Set synchronously first — `IsSelected` also fires
            // `_tree.SelectionChanged`, which re-runs the identical
            // content build fire-and-forget, but a caller awaiting this
            // method must see real content the instant it returns, not
            // only once that later task happens to complete.
            _dashboardNode.IsSelected = true;
            await _dashboard.RefreshAsync().ConfigureAwait(true);
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

        if (selected.Tag is WorksheetsMarker marker)
        {
            await _onWorksheetsSelected().ConfigureAwait(true);
            _worksheets.FilterDiscipline(Tempest.Desktop.CalcSheets.CalcSheetView.DisciplineFor(marker.Category));
            _detail.Content = _worksheets;
            return;
        }

        if (selected.Tag is string category)
        {
            // Into the engineering surface, beside this tree: navigate only
            // when not already there (a re-pick just switches discipline).
            if (_navigator.Current.Area != ShellArea.Engineering || !IsShowingSurface)
            {
                await _navigator.GoToEngineeringAsync().ConfigureAwait(true);
                EngineeringRequested?.Invoke();
            }

            DisciplineSelected?.Invoke(category);
            return;
        }

        // Group headers carry no content of their own.
        if (ReferenceEquals(selected, _disciplinesNode) || ReferenceEquals(selected, _modulesNode))
            return;

        // Any other node, picked while the surface is shown: back to this
        // landing page's own location first, then render the node.
        if (_navigator.Current.Area == ShellArea.Engineering)
        {
            await _navigator.GoToModuleAsync(ShellArea.EngineeringDepartment).ConfigureAwait(true);
            EngineeringRequested?.Invoke();
            return;
        }

        if (ReferenceEquals(selected, _dashboardNode))
        {
            await _dashboard.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _dashboardScroll;
            return;
        }

        if (ReferenceEquals(selected, _tasksNode))
        {
            await RenderTasksAsync().ConfigureAwait(true);
            _detail.Content = _tasksScroll;
            return;
        }

        if (ReferenceEquals(selected, _calculationsNode))
        {
            await _onEngineeringCalculationSelected().ConfigureAwait(true);
            _detail.Content = _engineeringCalculation;
            return;
        }

        if (ReferenceEquals(selected, _calculatorsNode))
        {
            await _onCalculatorsSelected().ConfigureAwait(true);
            _detail.Content = _calculators;
            return;
        }

        if (ReferenceEquals(selected, _worksheetsNode))
        {
            await _onWorksheetsSelected().ConfigureAwait(true);
            _worksheets.FilterDiscipline(null);
            _detail.Content = _worksheets;
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
