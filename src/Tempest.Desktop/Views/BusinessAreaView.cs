using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Business module (`WP 19.7A`, Product Owner comment items 6 and 7,
/// sheet 9): a tree — Dashboard &amp; Reports, Quotes, Invoices,
/// Timesheets, Subscriptions — with a right pane over whichever node is
/// selected. Every node embeds an already-built, already-tested view:
/// Quotes is <see cref="QuotesView"/> (`WP 19.5B`), Invoices is the
/// existing <see cref="InvoicingView"/>, Timesheets is the existing
/// <see cref="TimesheetWeekView"/>, Subscriptions is the new
/// <see cref="SubscriptionsView"/> over <c>IAccountsReadModel</c>
/// (`WP 19.8B`) — only Dashboard &amp; Reports is new content, and it is
/// the one disclosed placeholder this release allows.
/// </summary>
public sealed class BusinessAreaView : UserControl
{
    private readonly QuotesView _quotes;
    private readonly InvoicingView _invoices;
    private readonly TimesheetWeekView _timesheets;
    private readonly SubscriptionsView _subscriptions;

    private readonly TreeView _tree = new() { MinWidth = 260, MaxWidth = 260 };
    private readonly ContentControl _detail = new();
    private readonly Control _dashboardPlaceholder;

    private readonly TreeViewItem _dashboardNode = new() { Header = "Dashboard & Reports" };
    private readonly TreeViewItem _quotesNode = new() { Header = "Quotes" };
    private readonly TreeViewItem _invoicesNode = new() { Header = "Invoices" };
    private readonly TreeViewItem _timesheetsNode = new() { Header = "Timesheets" };
    private readonly TreeViewItem _subscriptionsNode = new() { Header = "Subscriptions" };

    private IWorkspaceChanges? _workspaceChanges;

    /// <summary>The change feed the Dashboard node would reload from once <c>WP 19.7B</c> gives it real content — kept wired now so that Work Package needs no further plumbing here.</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges;
        set => _workspaceChanges = value;
    }

    /// <summary>Initialises a new instance of the <see cref="BusinessAreaView"/> class.</summary>
    public BusinessAreaView(QuotesView quotes, InvoicingView invoices, TimesheetWeekView timesheets, SubscriptionsView subscriptions)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(timesheets);
        ArgumentNullException.ThrowIfNull(subscriptions);

        _quotes = quotes;
        _invoices = invoices;
        _timesheets = timesheets;
        _subscriptions = subscriptions;

        _dashboardPlaceholder = new EmptyStateView(
            "◈",
            "Business dashboard — not built yet",
            "WP 19.7B fills this in: Invoiced/Overdue/Due 30/Due 90 tiles, accounts receivable and payable panels, quotes to chase, and a cash flow chart. Use the tree on the left for Quotes, Invoices, Timesheets and Subscriptions today.");

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
        var treeHost = new Border
        {
            Child = _tree,
            Width = 260,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(0, DesignTokens.SpaceMd, 0, 0),
        };
        ThemeReactiveBrush.Bind(treeHost, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        DockPanel.SetDock(treeHost, Avalonia.Controls.Dock.Left);
        split.Children.Add(treeHost);

        _detail.Content = _dashboardPlaceholder;
        split.Children.Add(_detail);

        AutomationProperties.SetName(this, "Business");
        Content = split;
    }

    /// <summary>Re-reads whichever node is currently shown.</summary>
    public async Task RefreshAsync()
    {
        if (_tree.SelectedItem is null)
        {
            _dashboardNode.IsSelected = true;
            _detail.Content = _dashboardPlaceholder;
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
            _detail.Content = _dashboardPlaceholder;
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
}
