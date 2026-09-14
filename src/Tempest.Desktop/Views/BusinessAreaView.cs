using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views.Dashboards;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Business module (`WP 19.7A`, Product Owner comment items 6 and 7,
/// sheet 9): a tree — Dashboard &amp; Reports, Quotes, Invoices,
/// Timesheets, Subscriptions — with a right pane over whichever node is
/// selected. Every node embeds an already-built, already-tested view:
/// Dashboard &amp; Reports is <see cref="BusinessDashboardView"/>
/// (`WP 19.7B`), Quotes is <see cref="QuotesView"/> (`WP 19.5B`), Invoices
/// is the existing <see cref="InvoicingView"/>, Timesheets is the existing
/// <see cref="TimesheetWeekView"/>, Subscriptions is
/// <see cref="SubscriptionsView"/> over <c>IAccountsReadModel</c>
/// (`WP 19.8B`).
/// </summary>
public sealed class BusinessAreaView : UserControl
{
    private readonly QuotesView _quotes;
    private readonly InvoicingView _invoices;
    private readonly TimesheetWeekView _timesheets;
    private readonly SubscriptionsView _subscriptions;
    private readonly BusinessDashboardView _dashboard;

    private readonly TreeView _tree = new() { MinWidth = 260, MaxWidth = 260 };
    private readonly ContentControl _detail = new();
    private Border? _treeHost;

    private readonly TreeViewItem _dashboardNode = new() { Header = "Dashboard & Reports" };
    private readonly TreeViewItem _quotesNode = new() { Header = "Quotes" };
    private readonly TreeViewItem _invoicesNode = new() { Header = "Invoices" };
    private readonly TreeViewItem _timesheetsNode = new() { Header = "Timesheets" };
    private readonly TreeViewItem _subscriptionsNode = new() { Header = "Subscriptions" };

    private IWorkspaceChanges? _workspaceChanges;

    /// <summary>The change feed the Dashboard &amp; Reports node reloads from while shown (`WP 19.7B` — `WP 19.7A` left this property inert, "kept wired now so that Work Package needs no further plumbing here"; this is that plumbing).</summary>
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

    /// <summary>Initialises a new instance of the <see cref="BusinessAreaView"/> class.</summary>
    public BusinessAreaView(QuotesView quotes, InvoicingView invoices, TimesheetWeekView timesheets, SubscriptionsView subscriptions, BusinessDashboardView dashboard)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(timesheets);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(dashboard);

        _quotes = quotes;
        _invoices = invoices;
        _timesheets = timesheets;
        _subscriptions = subscriptions;
        _dashboard = dashboard;

        this.DetachedFromVisualTree += (_, _) => WorkspaceChanges = null;

        _tree.Items.Add(_dashboardNode);
        _tree.Items.Add(_quotesNode);
        _tree.Items.Add(_invoicesNode);
        _tree.Items.Add(_timesheetsNode);
        _tree.Items.Add(_subscriptionsNode);

        foreach (var (node, name) in new[]
                 {
                     (_dashboardNode, "Dashboard & Reports"), (_quotesNode, "Quotes"), (_invoicesNode, "Invoices"),
                     (_timesheetsNode, "Timesheets"), (_subscriptionsNode, "Subscriptions"),
                 })
            AutomationProperties.SetName(node, name);
        AutomationProperties.SetName(_tree, "Business tree");

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
        DockPanel.SetDock(_treeHost, Avalonia.Controls.Dock.Left);
        split.Children.Add(_treeHost);

        _detail.Content = _dashboard;
        split.Children.Add(_detail);

        AutomationProperties.SetName(this, "Business");
        Content = split;
    }

    /// <summary>
    /// Selects the node named <paramref name="automationName"/>
    /// ("Dashboard &amp; Reports", "Quotes", "Invoices", "Timesheets" or
    /// "Subscriptions") — the same name a screen reader announces, and
    /// what a journey test drives the tree by.
    /// </summary>
    public void SelectNode(string automationName)
    {
        var item = new[] { _dashboardNode, _quotesNode, _invoicesNode, _timesheetsNode, _subscriptionsNode }
            .Single(i => string.Equals(AutomationProperties.GetName(i), automationName, StringComparison.Ordinal));
        _tree.SelectedItem = item;
    }

    /// <summary>
    /// Narrows the tree below the shell's own compact threshold — the same
    /// one threshold <c>GlobalNavigationRail</c>/<c>RibbonView</c>/
    /// <c>LibrariesView</c>/<c>EngineeringAreaView</c> already fold on.
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
            _dashboardNode.IsSelected = true;
            await _dashboard.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _dashboard;
            return;
        }

        await OnSelectionChangedAsync().ConfigureAwait(true);
    }

    private async Task OnSelectionChangedAsync()
    {
        if (_tree.SelectedItem is not TreeViewItem selected)
            return;

        if (ReferenceEquals(selected, _dashboardNode))
        {
            await _dashboard.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _dashboard;
            return;
        }

        if (ReferenceEquals(selected, _quotesNode))
        {
            await _quotes.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _quotes;
            return;
        }

        if (ReferenceEquals(selected, _invoicesNode))
        {
            await _invoices.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _invoices;
            return;
        }

        if (ReferenceEquals(selected, _timesheetsNode))
        {
            await _timesheets.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _timesheets;
            return;
        }

        if (ReferenceEquals(selected, _subscriptionsNode))
        {
            await _subscriptions.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _subscriptions;
            return;
        }
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
                // Best-effort background refresh — mirrors every sibling
                // rail view's own identical "the next real entry is the
                // backstop" shape (`ReportsView.OnWorkspaceChanged`,
                // `ProjectsAreaView.OnWorkspaceChanged`).
            }
        });
}
