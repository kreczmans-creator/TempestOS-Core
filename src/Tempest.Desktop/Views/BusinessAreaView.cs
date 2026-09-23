using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views.Dashboards;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Business module (`WP 19.7A`, Product Owner comment items 6 and 7,
/// sheet 9): a tree — Dashboard &amp; Reports, Quotes, Invoices, Purchase
/// orders, Timesheets, Subscriptions — with a right pane over whichever
/// node is selected. Every node embeds an already-built, already-tested
/// view: Dashboard &amp; Reports is <see cref="BusinessDashboardView"/>
/// (`WP 19.7B`), Quotes is <see cref="QuotesView"/> (`WP 19.5B`), Invoices
/// is the existing <see cref="InvoicingView"/>, Purchase orders is
/// <see cref="PurchaseOrdersView"/> (`WP 21.3B`), Timesheets is the
/// existing <see cref="TimesheetWeekView"/>, Subscriptions is
/// <see cref="SubscriptionsView"/> over <c>IAccountsReadModel</c>
/// (`WP 19.8B`).
/// </summary>
public sealed class BusinessAreaView : UserControl
{
    private readonly QuotesView _quotes;
    private readonly InvoicingView _invoices;
    private readonly PurchaseOrdersView _purchaseOrders;
    private readonly TimesheetWeekView _timesheets;
    private readonly SubscriptionsView _subscriptions;
    private readonly BusinessDashboardView _dashboard;

    private readonly TreeView _tree = new();
    private readonly ContentControl _detail = new();
    private readonly CollapsibleColumn _treeColumn;

    private readonly TreeViewItem _dashboardNode = new() { Header = "Dashboard & Reports" };
    private readonly TreeViewItem _quotesNode = new() { Header = "Quotes" };
    private readonly TreeViewItem _invoicesNode = new() { Header = "Invoices" };
    private readonly TreeViewItem _purchaseOrdersNode = new() { Header = "Purchase orders" };
    private readonly TreeViewItem _timesheetsNode = new() { Header = "Timesheets" };
    private readonly TreeViewItem _subscriptionsNode = new() { Header = "Subscriptions" };

    private readonly WorkspaceChangesSubscription _workspaceChanges;

    /// <summary>The change feed the Dashboard &amp; Reports node reloads from while shown (`WP 19.7B` — `WP 19.7A` left this property inert, "kept wired now so that Work Package needs no further plumbing here"; this is that plumbing).</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="BusinessAreaView"/> class.</summary>
    public BusinessAreaView(
        QuotesView quotes, InvoicingView invoices, PurchaseOrdersView purchaseOrders, TimesheetWeekView timesheets,
        SubscriptionsView subscriptions, BusinessDashboardView dashboard)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(purchaseOrders);
        ArgumentNullException.ThrowIfNull(timesheets);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(dashboard);

        _quotes = quotes;
        _invoices = invoices;
        _purchaseOrders = purchaseOrders;
        _timesheets = timesheets;
        _subscriptions = subscriptions;
        _dashboard = dashboard;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        _tree.Items.Add(_dashboardNode);
        _tree.Items.Add(_quotesNode);
        _tree.Items.Add(_invoicesNode);
        _tree.Items.Add(_purchaseOrdersNode);
        _tree.Items.Add(_timesheetsNode);
        _tree.Items.Add(_subscriptionsNode);

        foreach (var (node, name) in new[]
                 {
                     (_dashboardNode, "Dashboard & Reports"), (_quotesNode, "Quotes"), (_invoicesNode, "Invoices"),
                     (_purchaseOrdersNode, "Purchase orders"), (_timesheetsNode, "Timesheets"), (_subscriptionsNode, "Subscriptions"),
                 })
            AutomationProperties.SetName(node, name);
        AutomationProperties.SetName(_tree, "Business tree");

        _tree.SelectionChanged += (_, _) => _ = OnSelectionChangedAsync();

        var split = new DockPanel();
        _treeColumn = new CollapsibleColumn("Business", _tree);
        DockPanel.SetDock(_treeColumn, Avalonia.Controls.Dock.Left);
        split.Children.Add(_treeColumn);

        _detail.Content = _dashboard;
        split.Children.Add(_detail);

        AutomationProperties.SetName(this, "Business");
        Content = split;
    }

    /// <summary>
    /// Selects the node named <paramref name="automationName"/>
    /// ("Dashboard &amp; Reports", "Quotes", "Invoices", "Purchase
    /// orders", "Timesheets" or "Subscriptions") — the same name a screen
    /// reader announces, and what a journey test drives the tree by.
    /// </summary>
    public void SelectNode(string automationName)
    {
        var item = new[] { _dashboardNode, _quotesNode, _invoicesNode, _purchaseOrdersNode, _timesheetsNode, _subscriptionsNode }
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

    /// <summary>Re-reads whichever node is currently shown.</summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;

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

        if (ReferenceEquals(selected, _purchaseOrdersNode))
        {
            await _purchaseOrders.RefreshAsync().ConfigureAwait(true);
            _detail.Content = _purchaseOrders;
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
