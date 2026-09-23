using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.PurchaseOrders;
using Tempest.Desktop;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Projects;
using Tempest.Workspace.PurchaseOrders;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Purchase orders area (`WP 21.3B`): every <see cref="PurchaseOrder"/>
/// across every live project — or, when a project is open, that project's
/// own orders alone — grouped <b>New</b> (Draft), <b>Issued</b>,
/// <b>Received</b>, <b>Closed</b> (Closed and Cancelled together, collapsed
/// by default). <b>New Purchase Order…</b> picks a project and an optional
/// supplier and opens a Draft with it; each Draft row offers <b>Add
/// line…</b> and <b>Issue</b> (once it carries a line); an Issued row
/// offers <b>Receive</b>; a Received row offers <b>Record as expenses</b>
/// (once, per order) and <b>Close</b>; a Draft or Issued row offers
/// <b>Cancel</b>. <b>Open</b> opens the order in the generic Object
/// Editor. Mirrors <see cref="InvoicingView"/>'s own grouped-list shape.
/// </summary>
/// <remarks>
/// <b>Renders from the domain, refreshed by the change feed.</b> Every
/// order is read fresh, through one coherent
/// <see cref="EngineeringDomainContext.Repository"/> read, every time
/// <see cref="IWorkspaceChanges.Changed"/> touches
/// <see cref="PurchaseOrder.CanonicalKind"/> — there is no manual refresh
/// call site anywhere else in this class.
/// </remarks>
public sealed class PurchaseOrdersView : UserControl
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;
    private readonly Func<Guid?> _currentProjectId;
    private readonly ProjectPicker _projectPicker;
    private readonly InputDialog _inputDialog;
    private readonly PurchaseOrderLinePrompt _linePrompt;
    private readonly Action<Guid, string> _openObject;

    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly StackPanel _groups = new() { Spacing = DesignTokens.SpaceMd };
    private readonly Button _newOrderButton = new() { Content = "New Purchase Order…", MinHeight = DesignTokens.ControlSizeMedium };

    private readonly WorkspaceChangesSubscription _workspaceChanges;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Collects Issue/Receive/Close/Cancel/Record-as-expenses' own confirmation.</summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>The change feed this view reloads its own list from.</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrdersView"/> class.</summary>
    public PurchaseOrdersView(
        EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry,
        Func<Guid?> currentProjectId, ProjectPicker projectPicker, InputDialog inputDialog, PurchaseOrderLinePrompt linePrompt,
        Action<Guid, string> openObject)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(currentProjectId);
        ArgumentNullException.ThrowIfNull(projectPicker);
        ArgumentNullException.ThrowIfNull(inputDialog);
        ArgumentNullException.ThrowIfNull(linePrompt);
        ArgumentNullException.ThrowIfNull(openObject);

        _domainContext = domainContext;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
        _currentProjectId = currentProjectId;
        _projectPicker = projectPicker;
        _inputDialog = inputDialog;
        _linePrompt = linePrompt;
        _openObject = openObject;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        var heading = new TextBlock
        {
            Text = "Purchase orders",
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        AutomationProperties.SetName(_newOrderButton, "New Purchase Order…");
        _newOrderButton.Classes.Add(ChromeStyles.Primary);
        _newOrderButton.Click += async (_, _) => await OnNewOrderAsync().ConfigureAwait(true);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        headerRow.Children.Add(heading);
        headerRow.Children.Add(_newOrderButton);

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(headerRow);
        body.Children.Add(_status);
        body.Children.Add(_groups);

        AutomationProperties.SetName(this, "Purchase orders");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Test-only: counts every <see cref="RefreshAsync"/> call, proving a reattached view's subscription still reaches <see cref="OnWorkspaceChanged"/>.</summary>
    internal int RefreshCount { get; private set; }

    /// <summary>Reloads every purchase order in scope — every live project's own, or the open project's alone when one is open.</summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;

        var scopedProjectId = _currentProjectId();
        var projects = await ProjectsInScopeAsync(scopedProjectId).ConfigureAwait(true);

        var rows = new List<OrderRow>();
        foreach (var project in projects)
        {
            // `WP 21.5B`: list results are index rows — live rows filtered on the row, then materialised.
            var orderEntries = (await _domainContext.Repository.ListChildrenAsync(project.Id).ConfigureAwait(true))
                .Where(entry => !entry.IsDeleted)
                .ToList();
            var orders = (await _domainContext.Repository.MaterialiseAsync<PurchaseOrder>(orderEntries).ConfigureAwait(true))
                .Where(IsLive);

            var projectName = DisplayNameOf(project);
            foreach (var order in orders)
                rows.Add(new OrderRow(order, project.Id, projectName));
        }

        _status.Text = rows.Count == 0
            ? (scopedProjectId is null ? "No purchase orders yet." : "No purchase orders for this project yet.")
            : $"{rows.Count} purchase order(s) across {projects.Count} project(s).";

        var draft = rows.Where(r => r.Order.Status == PurchaseOrderStatus.Draft).OrderByDescending(r => r.Order.Reference, StringComparer.Ordinal).ToList();
        var issued = rows.Where(r => r.Order.Status == PurchaseOrderStatus.Issued).OrderByDescending(r => r.Order.IssuedDate).ToList();
        var received = rows.Where(r => r.Order.Status == PurchaseOrderStatus.Received).OrderByDescending(r => r.Order.Reference, StringComparer.Ordinal).ToList();
        var closed = rows.Where(r => r.Order.Status is PurchaseOrderStatus.Closed or PurchaseOrderStatus.Cancelled)
            .OrderByDescending(r => r.Order.Reference, StringComparer.Ordinal).ToList();

        _groups.Children.Clear();
        _groups.Children.Add(BuildStandardGroup("New", $"New ({draft.Count})", "No draft purchase orders.", draft.Select(BuildRow).ToList()));
        _groups.Children.Add(BuildStandardGroup("Issued", $"Issued ({issued.Count})", "Nothing issued yet.", issued.Select(BuildRow).ToList()));
        _groups.Children.Add(BuildStandardGroup("Received", $"Received ({received.Count})", "Nothing received yet.", received.Select(BuildRow).ToList()));
        _groups.Children.Add(BuildClosedGroup($"Closed ({closed.Count})", "Nothing closed or cancelled.", closed.Select(BuildRow).ToList()));
    }

    private async Task<List<IEngineeringObject>> ProjectsInScopeAsync(Guid? scopedProjectId)
    {
        if (scopedProjectId is { } id)
        {
            var project = await _domainContext.Repository.FindAsync(id).ConfigureAwait(true);
            return project is not null && IsLive(project) ? [project] : [];
        }

        var projectEntries = (await _domainContext.Repository.ListByKindAsync(ProjectDirectory.ProjectKind).ConfigureAwait(true))
            .Where(entry => !entry.IsDeleted)
            .ToList();
        return (await _domainContext.Repository.MaterialiseAsync<IEngineeringObject>(projectEntries).ConfigureAwait(true))
            .Where(IsLive)
            .OrderBy(DisplayNameOf, StringComparer.Ordinal)
            .ToList();
    }

    private static Control BuildStandardGroup(string automationName, string headerText, string emptyText, IReadOnlyList<Control> rowControls)
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(new TextBlock { Text = headerText, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        if (rowControls.Count == 0)
            panel.Children.Add(new TextBlock { Text = emptyText, Opacity = 0.6, FontSize = DesignTokens.FontSizeCaption });
        else
            foreach (var row in rowControls)
                panel.Children.Add(row);

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = panel,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        AutomationProperties.SetName(border, automationName);
        return border;
    }

    /// <summary>Closed (Closed, Cancelled) — collapsed by default, mirroring <see cref="InvoicingView"/>'s own identical Closed group.</summary>
    private static Control BuildClosedGroup(string headerText, string emptyText, IReadOnlyList<Control> rowControls)
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        if (rowControls.Count == 0)
            panel.Children.Add(new TextBlock { Text = emptyText, Opacity = 0.6, FontSize = DesignTokens.FontSizeCaption });
        else
            foreach (var row in rowControls)
                panel.Children.Add(row);

        var expander = new Expander { Header = headerText, IsExpanded = false, Padding = DesignTokens.PanelPadding, Content = panel };
        AutomationProperties.SetName(expander, "Closed");
        return expander;
    }

    private Control BuildRow(OrderRow row)
    {
        var order = row.Order;
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        rows.Children.Add(new TextBlock
        {
            Text = $"{order.Reference} — {row.ProjectName} — Supplier {order.SupplierOrganisationId ?? "(none)"}"
                + $" — {MoneyDisplay.Format(order.GrossTotal)} gross ({order.Lines.Count} line(s))",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        rows.Children.Add(new TextBlock
        {
            Text = $"Issued {order.IssuedDate?.ToString("yyyy-MM-dd") ?? "(not yet issued)"}"
                + $" — Expected {order.ExpectedDelivery?.ToString("yyyy-MM-dd") ?? "(not stated)"}"
                + (order.Status == PurchaseOrderStatus.Received ? $" — Expenses recorded: {order.ExpensesRecorded}" : string.Empty),
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };

        var open = new Button { Content = "Open", MinHeight = DesignTokens.MinControlSize };
        open.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(open, $"Open {order.Reference}");
        open.Click += (_, _) => _openObject(order.Id, PurchaseOrder.CanonicalKind);
        actions.Children.Add(open);

        if (order.Status == PurchaseOrderStatus.Draft)
        {
            var addLine = new Button { Content = "Add line…", MinHeight = DesignTokens.MinControlSize };
            addLine.Classes.Add(ChromeStyles.Flat);
            AutomationProperties.SetName(addLine, $"Add line to {order.Reference}");
            addLine.Click += async (_, _) => await OnAddLineAsync(order.Id, order.Currency).ConfigureAwait(true);
            actions.Children.Add(addLine);

            if (order.Lines.Count > 0)
                actions.Children.Add(BuildActionButton("Issue", $"Issue {order.Reference}", PurchaseOrderCommandIds.Issue, order.Id));

            actions.Children.Add(BuildActionButton("Cancel", $"Cancel {order.Reference}", PurchaseOrderCommandIds.Cancel, order.Id));
        }
        else if (order.Status == PurchaseOrderStatus.Issued)
        {
            actions.Children.Add(BuildActionButton("Receive", $"Receive {order.Reference}", PurchaseOrderCommandIds.Receive, order.Id));
            actions.Children.Add(BuildActionButton("Cancel", $"Cancel {order.Reference}", PurchaseOrderCommandIds.Cancel, order.Id));
        }
        else if (order.Status == PurchaseOrderStatus.Received)
        {
            if (!order.ExpensesRecorded)
            {
                actions.Children.Add(BuildActionButton(
                    "Record as expenses", $"Record lines of {order.Reference} as expenses", PurchaseOrderCommandIds.RecordAsExpenses, order.Id));
            }

            actions.Children.Add(BuildActionButton("Close", $"Close {order.Reference}", PurchaseOrderCommandIds.Close, order.Id));
        }

        rows.Children.Add(actions);

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = rows,
            Tag = order.Id,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    private Button BuildActionButton(string label, string automationName, string commandId, Guid orderId)
    {
        var button = new Button { Content = label, MinHeight = DesignTokens.MinControlSize };
        button.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(button, automationName);
        button.Click += async (_, _) => await OnActionAsync(commandId, orderId).ConfigureAwait(true);
        return button;
    }

    private async Task OnActionAsync(string commandId, Guid orderId)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm this action here — it is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(orderId, PurchaseOrder.CanonicalKind)]);
        var invocation = await _commandRegistry.InvokeAsync(commandId, context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "That action is unavailable.", succeeded: false);
            return;
        }

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? (result.Succeeded ? "Done." : "The action failed."), succeeded: result.Succeeded);
    }

    private async Task OnAddLineAsync(Guid orderId, Core.BusinessGovernance.CurrencyCode currency)
    {
        var input = await _linePrompt.PromptAsync(currency).ConfigureAwait(true);
        if (input is null)
        {
            Report("Add line was cancelled.", succeeded: false);
            return;
        }

        var command = new AddPurchaseOrderLineCommand(orderId, PurchaseOrder.CanonicalKind, input.Description, input.Quantity, input.UnitPrice, input.VatRate);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? (result.Succeeded ? "Line added." : "The line was refused."), succeeded: result.Succeeded);
    }

    private async Task OnNewOrderAsync()
    {
        var projectId = await _projectPicker.PickAsync().ConfigureAwait(true);
        if (projectId is not { } id)
        {
            Report("New Purchase Order was cancelled.", succeeded: false);
            return;
        }

        // The supplier is a tag, never validated (exactly `Quotation.ClientOrganisationId`'s
        // own rule) — a plain text prompt, not a catalogue picker, matches
        // what the model actually stores.
        var supplier = await _inputDialog
            .PromptAsync("New Purchase Order", "Supplier (organisation id — blank if unknown)", allowBlank: true)
            .ConfigureAwait(true);

        if (supplier is null)
        {
            Report("New Purchase Order was cancelled.", succeeded: false);
            return;
        }

        var command = new CreatePurchaseOrderCommand(id, reference: null, string.IsNullOrWhiteSpace(supplier) ? null : supplier, expectedDelivery: null);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "The purchase order could not be raised.", succeeded: false);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Purchase order raised.", succeeded: true);

        // `WP 17.9.4`: what you make opens right up.
        if (result.SubjectId is { } createdId)
            _openObject(createdId, PurchaseOrder.CanonicalKind);
    }

    private void OnWorkspaceChanged(WorkspaceChange change)
    {
        if (!change.Entries.Any(e => e.Kind == PurchaseOrder.CanonicalKind))
            return;

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ActionCompleted?.Invoke($"Refresh failed: {ex.Message}", ActionOutcome.Failed);
            }
        });
    }

    private void Report(string message, bool succeeded)
    {
        _status.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(succeeded));
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };

    private sealed record OrderRow(PurchaseOrder Order, Guid ProjectId, string ProjectName);
}
