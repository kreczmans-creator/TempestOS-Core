using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Desktop;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Invoicing;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Tasks;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Invoicing area (`WP 19.1A` part 3, `ADR-0151`; regrouped `WP
/// 19.10D` to the Product Owner's own sketch, Product Owner comment item 6
/// sheet 9): every <see cref="InvoiceRequest"/> across every live project
/// — or, when a project is open, that project's own requests alone —
/// grouped as <b>New</b>, <b>Available to invoice</b>, <b>Sent</b>,
/// <b>Outstanding / Overdue</b> and <b>Closed</b> (collapsed by default);
/// <b>Send</b>, <b>Reconcile now</b>, <b>Void</b> and <b>Raise invoice</b>
/// per row, with every connector outcome shown; <b>Review</b> opens the
/// request in the Object Editor, <b>Open completion</b> opens the
/// completion. Raising a request from a completion is also reachable here
/// now (`invoicing.raise`), alongside the project's own Deliverables tab
/// (<see cref="ProjectDeliverablesView"/>) — both dispatch the identical
/// command.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every request lands in exactly one group.</b> Draft is <b>New</b>.
/// Sending/Sent/Accepted is <b>Sent</b> unless it is also Outstanding.
/// <b>Outstanding / Overdue</b> is a Sent or Accepted request unpaid past
/// its own <see cref="InvoiceRequest.DueOn"/> (`TD-180`, `WP 20.1B`) — the
/// identical rule
/// <c>Tempest.Workspace.Tasks.TaskEquations.InvoiceFinanceItems</c> reads
/// for the Home/Engineering "Finance" tile, reused here rather than
/// re-derived — plus Reauthorise and Unknown, which always need attention.
/// Rejected and Voided are <b>Closed</b>. This partition is exhaustive and
/// disjoint over every <see cref="InvoiceRequestStatus"/> that is ever
/// actually stored (<see cref="InvoiceRequestStatus.Unavailable"/> is not;
/// its own remarks explain why).
/// </para>
/// <para>
/// <b>Available to invoice reads the same rule
/// <see cref="Tempest.Core.Invoicing.InvoicingService"/>'s own private
/// <c>ListCarriedSourcesAsync</c> enforces</b>, restated here rather than
/// exposed: a live <see cref="DeliverableCompletion"/>
/// (<see cref="DeliverableCompletion.InvoicedBy"/> still <see
/// langword="null"/>) whose own id is not a <see
/// cref="InvoiceRequestLine.SourceId"/> on any live request that is not
/// Rejected or Voided. A request carrying it, even a Draft one, excludes
/// it — exactly what stops <c>InvoicingService.RaiseFromCompletionAsync</c>
/// itself from ever raising two requests for the same completion.
/// </para>
/// <para>
/// <b>Renders from the domain, refreshed by the change feed.</b> Mirrors
/// <see cref="TimesheetWeekView"/>/<see cref="ProjectDeliverablesView"/>'s
/// own identical discipline: every request and completion is read fresh,
/// through one coherent <see cref="EngineeringDomainContext.Repository"/>
/// read, every time <see cref="IWorkspaceChanges.Changed"/> touches
/// <see cref="InvoiceRequest.CanonicalKind"/> or
/// <see cref="DeliverableCompletion.CanonicalKind"/> — there is no manual
/// refresh call site anywhere else in this class.
/// </para>
/// <para>
/// <b>Send/Reconcile/Void/Raise dispatch through <see cref="ICommandRegistry"/>,
/// not <see cref="IInvoicingService"/> directly</b> — exactly as
/// <see cref="TimesheetWeekView"/>'s own Amend/Delete do, which is what
/// lets each command's own <c>confirmationMessage</c>
/// (<see cref="InvoicingWorkspaceRegistration.Register"/>) run through
/// <see cref="ParameterPrompt"/> rather than this view inventing a second
/// confirmation mechanism.
/// </para>
/// <para>
/// <b>No blocking calls.</b> Every read and write here is
/// <see langword="await"/>ed.
/// </para>
/// </remarks>
public sealed class InvoicingView : UserControl
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandRegistry _commandRegistry;
    private readonly Func<Guid?> _currentProjectId;
    private readonly Action<Guid, string> _openObject;

    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly StackPanel _groups = new() { Spacing = DesignTokens.SpaceMd };

    private readonly WorkspaceChangesSubscription _workspaceChanges;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// Collects Send/Reconcile/Void/Raise's own confirmation — reuses
    /// <see cref="RibbonView.ParameterPrompt"/>'s own pattern, so all four
    /// dispatch through the already-registered
    /// <see cref="InvoicingCommandIds"/> descriptors exactly as the Ribbon
    /// and the Command Palette do. <see langword="null"/> (any test that
    /// constructs this view directly) leaves all four honestly
    /// unavailable rather than run without asking.
    /// </summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>The change feed this view reloads its own list from (`WP 18.1A`, `WP 18.9.1`).</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="InvoicingView"/> class.</summary>
    public InvoicingView(
        EngineeringDomainContext domainContext, ICommandRegistry commandRegistry, Func<Guid?> currentProjectId, Action<Guid, string> openObject)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(currentProjectId);
        ArgumentNullException.ThrowIfNull(openObject);

        _domainContext = domainContext;
        _commandRegistry = commandRegistry;
        _currentProjectId = currentProjectId;
        _openObject = openObject;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        var heading = new TextBlock
        {
            Text = "Invoicing",
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(heading);
        body.Children.Add(_status);
        body.Children.Add(_groups);

        AutomationProperties.SetName(this, "Invoicing");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Test-only (`WP 19.7C`, <c>WorkspaceChangesReattachTests</c>): counts every <see cref="RefreshAsync"/> call, proving a reattached view's subscription still reaches <see cref="OnWorkspaceChanged"/>.</summary>
    internal int RefreshCount { get; private set; }

    /// <summary>Reloads every invoice request and every unbilled completion in scope — every live project's own, or the open project's alone when one is open — and rebuilds the five groups.</summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;

        var scopedProjectId = _currentProjectId();
        var projects = await ProjectsInScopeAsync(scopedProjectId).ConfigureAwait(true);

        var requestRows = new List<RequestRow>();
        var completionCandidates = new List<CompletionRow>();
        var expenseCandidates = new List<ExpenseRow>();

        foreach (var project in projects)
        {
            var projectName = DisplayNameOf(project);
            var children = await _domainContext.Repository.ListChildrenAsync(project.Id).ConfigureAwait(true);

            foreach (var request in children.OfType<InvoiceRequest>().Where(IsLive))
                requestRows.Add(new RequestRow(request, projectName));

            // `WP 21.3B`: a billable, unbilled expense is "available to
            // invoice" exactly as an unbilled completion is — read here
            // directly, over the same already-fetched children, mirroring
            // how a completion is read rather than through `IExpenseService`.
            foreach (var expense in children.OfType<ProjectExpense>().Where(e => IsLive(e) && e.Billable && e.InvoicedBy is null))
                expenseCandidates.Add(new ExpenseRow(expense, projectName));

            var unbilled = children.OfType<DeliverableCompletion>().Where(c => IsLive(c) && c.InvoicedBy is null).ToList();
            if (unbilled.Count == 0)
                continue;

            var deliverablesById = (await ProjectMembership.ListProjectMembersAsync(_domainContext.Repository, project.Id, CancellationToken.None).ConfigureAwait(true))
                .OfType<Deliverable>()
                .Where(IsLive)
                .ToDictionary(d => d.Id);

            foreach (var completion in unbilled)
            {
                deliverablesById.TryGetValue(completion.DeliverableId, out var deliverable);
                completionCandidates.Add(new CompletionRow(completion, projectName, deliverable));
            }
        }

        // "Available to invoice" (Product Owner comment item 6, sheet 9):
        // a completion `InvoicingService.ListCarriedSourcesAsync` would not
        // find carried — that method is private to `Tempest.Core.Invoicing`,
        // so its own rule is restated here (class remarks) rather than
        // exposed.
        var carried = requestRows
            .Where(r => r.Request.Status is not (InvoiceRequestStatus.Rejected or InvoiceRequestStatus.Voided))
            .SelectMany(r => r.Request.Lines)
            .Select(l => l.SourceId)
            .ToHashSet();

        var availableRows = completionCandidates
            .Where(c => !carried.Contains(c.Completion.Id))
            .OrderBy(c => c.Deliverable?.DisplayName ?? c.Completion.DisplayName, StringComparer.Ordinal)
            .ThenBy(c => c.Completion.CompletedOn)
            .ToList();

        // `WP 21.3B`: an expense joins the identical group, so a project
        // whose only unbilled work right now is an expense still shows
        // something waiting for an invoice.
        var availableExpenseRows = expenseCandidates
            .Where(e => !carried.Contains(e.Expense.Id))
            .OrderByDescending(e => e.Expense.Date)
            .ThenBy(e => e.Expense.Description, StringComparer.Ordinal)
            .ToList();

        var availableCount = availableRows.Count + availableExpenseRows.Count;

        _status.Text = requestRows.Count == 0 && availableCount == 0
            ? (scopedProjectId is null ? "No invoice requests yet." : "No invoice requests for this project yet.")
            : $"{requestRows.Count} request(s), {availableCount} item(s) awaiting invoice, across {projects.Count} project(s).";

        var asOf = DateTimeOffset.UtcNow;

        var newRows = requestRows
            .Where(r => r.Request.Status == InvoiceRequestStatus.Draft)
            .OrderByDescending(r => r.Request.CreatedAt)
            .ThenBy(r => r.Request.DisplayName, StringComparer.Ordinal)
            .ToList();

        var sentRows = requestRows
            .Where(r => r.Request.Status is InvoiceRequestStatus.Sending or InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted)
            .Where(r => !IsOutstandingOrOverdue(r.Request, asOf))
            .OrderByDescending(r => r.Request.SentAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(r => r.Request.DisplayName, StringComparer.Ordinal)
            .ToList();

        var outstandingRows = requestRows
            .Where(r => IsOutstandingOrOverdue(r.Request, asOf))
            .OrderBy(r => r.Request.SentAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(r => r.Request.DisplayName, StringComparer.Ordinal)
            .ToList();

        var closedRows = requestRows
            .Where(r => r.Request.Status is InvoiceRequestStatus.Rejected or InvoiceRequestStatus.Voided)
            .OrderByDescending(r => r.Request.CreatedAt)
            .ThenBy(r => r.Request.DisplayName, StringComparer.Ordinal)
            .ToList();

        _groups.Children.Clear();
        _groups.Children.Add(BuildStandardGroup(
            "New", $"New ({newRows.Count})", "No draft requests.", newRows.Select(BuildNewRow).ToList()));
        _groups.Children.Add(BuildStandardGroup(
            "Available to invoice", $"Available to invoice ({availableCount})",
            "Nothing completed or expensed is waiting for an invoice.",
            availableRows.Select(BuildAvailableRow).Concat(availableExpenseRows.Select(BuildAvailableExpenseRow)).ToList()));
        _groups.Children.Add(BuildStandardGroup(
            "Sent", $"Sent ({sentRows.Count})", "Nothing has been sent yet.", sentRows.Select(BuildSentRow).ToList()));
        _groups.Children.Add(BuildStandardGroup(
            "Outstanding / Overdue",
            $"Outstanding / Overdue ({outstandingRows.Count}) — unpaid past its own due date (`TD-180`), or needing attention (Reauthorise, Unknown)",
            "Nothing is outstanding or overdue.",
            outstandingRows.Select(r => BuildOutstandingRow(r, asOf)).ToList()));
        _groups.Children.Add(BuildClosedGroup(
            $"Closed ({closedRows.Count})", "Nothing has been rejected or voided.", closedRows.Select(BuildClosedRow).ToList()));
    }

    /// <summary>
    /// A Sent or Accepted request unpaid past its own <see cref="InvoiceRequest.DueOn"/>
    /// (`TD-180`) — the identical rule <see cref="TaskEquations.InvoiceFinanceItems"/>
    /// reads for the Finance tile, reused rather than re-derived — or a
    /// Reauthorise or Unknown request, which always needs attention
    /// regardless of age.
    /// </summary>
    private static bool IsOutstandingOrOverdue(InvoiceRequest request, DateTimeOffset asOf) =>
        request.Status is InvoiceRequestStatus.Reauthorise or InvoiceRequestStatus.Unknown
        || (request.Status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted
            && request.PaidDate is null
            && request.DueOn is { } due
            && DateOnly.FromDateTime(asOf.UtcDateTime) > due);

    private async Task<List<IEngineeringObject>> ProjectsInScopeAsync(Guid? scopedProjectId)
    {
        if (scopedProjectId is { } id)
        {
            var project = await _domainContext.Repository.FindAsync(id).ConfigureAwait(true);
            return project is not null && IsLive(project) ? [project] : [];
        }

        return (await _domainContext.Repository.ListByKindAsync(ProjectDirectory.ProjectKind).ConfigureAwait(true))
            .Where(IsLive)
            .OrderBy(DisplayNameOf, StringComparer.Ordinal)
            .ToList();
    }

    // ================================================================
    // Group and row rendering
    // ================================================================

    private static Control BuildStandardGroup(string automationName, string headerText, string emptyText, IReadOnlyList<Control> rowControls)
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(new TextBlock
        {
            Text = headerText,
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

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

    /// <summary>Closed (Rejected, Voided) — collapsed by default, per the Product Owner's own table.</summary>
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

    private static TextBlock HeadingLine(RequestRow row)
    {
        var request = row.Request;
        return new TextBlock
        {
            Text = $"{row.ProjectName} — {request.DisplayName} — Client {request.ClientOrganisationId}"
                + (request.PurchaseOrderReference is { } po ? $" — PO {po}" : string.Empty)
                + $" — {MoneyDisplay.Format(request.Total)}"
                + $" — Terms {request.PaymentTerms.DisplayName()}"
                + (request.DueOn is { } due ? $" — Due {due:yyyy-MM-dd}" : string.Empty),
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
    }

    private static TextBlock ExternalInfoLine(InvoiceRequest request) =>
        new()
        {
            Text = $"External # {request.ExternalInvoiceNumber ?? "(none)"}"
                + $" — Sent {request.SentAtUtc?.ToString("yyyy-MM-dd") ?? "(not sent)"}"
                + $" — {request.ExternalStatus ?? "(no status read yet)"}"
                + (request.LastError is { } error ? $" — {error}" : string.Empty),
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };

    private static Border RowBorder(Control content, Guid tag)
    {
        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = content,
            Tag = tag,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    private Button ReviewButton(InvoiceRequest request)
    {
        var review = new Button { Content = "Review", MinHeight = DesignTokens.MinControlSize };
        review.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(review, $"Review {request.DisplayName}");
        review.Click += (_, _) => _openObject(request.Id, InvoiceRequest.CanonicalKind);
        return review;
    }

    private Button SendButton(InvoiceRequest request)
    {
        var send = new Button { Content = "Send", MinHeight = DesignTokens.MinControlSize };
        send.Classes.Add(ChromeStyles.Primary);
        AutomationProperties.SetName(send, $"Send {request.DisplayName}");
        send.Click += async (_, _) => await OnSendAsync(request.Id).ConfigureAwait(true);
        return send;
    }

    private Button VoidButton(InvoiceRequest request)
    {
        var voidButton = new Button { Content = "Void", MinHeight = DesignTokens.MinControlSize };
        voidButton.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(voidButton, $"Void {request.DisplayName}");
        voidButton.Click += async (_, _) => await OnVoidAsync(request.Id).ConfigureAwait(true);
        return voidButton;
    }

    private Button ReconcileButton(InvoiceRequest request)
    {
        var reconcile = new Button { Content = "Reconcile now", MinHeight = DesignTokens.MinControlSize };
        reconcile.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(reconcile, $"Reconcile {request.DisplayName}");
        reconcile.Click += async (_, _) => await OnReconcileAsync(request.Id).ConfigureAwait(true);
        return reconcile;
    }

    /// <summary>New: Draft requests — request, project, client, total, raised date; Review, Send, Void.</summary>
    private Control BuildNewRow(RequestRow row)
    {
        var request = row.Request;
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };
        rows.Children.Add(HeadingLine(row));
        rows.Children.Add(new TextBlock
        {
            Text = $"Raised {request.CreatedAt:yyyy-MM-dd}",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        actions.Children.Add(ReviewButton(request));
        actions.Children.Add(SendButton(request));
        actions.Children.Add(VoidButton(request));
        rows.Children.Add(actions);

        return RowBorder(rows, request.Id);
    }

    /// <summary>Available to invoice: an unbilled, uncarried completion — completion, project, deliverable, completed date, fixed price if any; Open completion, Raise invoice.</summary>
    private Control BuildAvailableRow(CompletionRow candidate)
    {
        var completion = candidate.Completion;
        var deliverableName = candidate.Deliverable is { } d
            ? (d.Identifier is { } id ? $"{id} — {d.DisplayName}" : d.DisplayName)
            : completion.DisplayName;

        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };
        rows.Children.Add(new TextBlock
        {
            Text = $"{candidate.ProjectName} — {deliverableName} — Completed {completion.CompletedOn:yyyy-MM-dd}"
                + (completion.FixedPriceValue is { } price ? $" — fixed price {MoneyDisplay.Format(price)}" : " — time-billed"),
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };

        var open = new Button { Content = "Open completion", MinHeight = DesignTokens.MinControlSize };
        open.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(open, $"Open the completion of {deliverableName}");
        open.Click += (_, _) => _openObject(completion.Id, DeliverableCompletion.CanonicalKind);
        actions.Children.Add(open);

        var raise = new Button { Content = "Raise invoice", MinHeight = DesignTokens.MinControlSize };
        raise.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(raise, $"Raise invoice for {deliverableName}");
        raise.Click += async (_, _) => await OnRaiseAsync(completion.Id, DeliverableCompletion.CanonicalKind).ConfigureAwait(true);
        actions.Children.Add(raise);

        rows.Children.Add(actions);

        return RowBorder(rows, completion.Id);
    }

    /// <summary>Available to invoice: a billable, unbilled, uncarried expense (`WP 21.3B`) — expense, project, date, net/VAT/gross; Open expense, Raise invoice.</summary>
    private Control BuildAvailableExpenseRow(ExpenseRow candidate)
    {
        var expense = candidate.Expense;

        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };
        rows.Children.Add(new TextBlock
        {
            Text = $"{candidate.ProjectName} — {expense.Description} — {expense.Category} — {expense.Date:yyyy-MM-dd}"
                + $" — {MoneyDisplay.Format(expense.NetAmount)} net, {MoneyDisplay.Format(expense.VatAmount)} VAT",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };

        var open = new Button { Content = "Open expense", MinHeight = DesignTokens.MinControlSize };
        open.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(open, $"Open the expense {expense.Description}");
        open.Click += (_, _) => _openObject(expense.Id, ProjectExpense.CanonicalKind);
        actions.Children.Add(open);

        var raise = new Button { Content = "Raise invoice", MinHeight = DesignTokens.MinControlSize };
        raise.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(raise, $"Raise invoice for {expense.Description}");
        raise.Click += async (_, _) => await OnRaiseAsync(expense.Id, ProjectExpense.CanonicalKind).ConfigureAwait(true);
        actions.Children.Add(raise);

        rows.Children.Add(actions);

        return RowBorder(rows, expense.Id);
    }

    /// <summary>Sent: Sending, Sent, Accepted requests not yet Outstanding — request, external number, sent date, external status; Review, Reconcile now (Sent/Accepted only).</summary>
    private Control BuildSentRow(RequestRow row)
    {
        var request = row.Request;
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };
        rows.Children.Add(HeadingLine(row));
        rows.Children.Add(ExternalInfoLine(request));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        actions.Children.Add(ReviewButton(request));
        if (request.Status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted)
            actions.Children.Add(ReconcileButton(request));
        rows.Children.Add(actions);

        return RowBorder(rows, request.Id);
    }

    /// <summary>Outstanding / Overdue: as Sent, plus days outstanding, or which attention Reauthorise/Unknown needs; Review, Reconcile now, Authorise (Settings) where Reauthorise.</summary>
    private Control BuildOutstandingRow(RequestRow row, DateTimeOffset asOf)
    {
        var request = row.Request;
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };
        rows.Children.Add(HeadingLine(row));
        rows.Children.Add(ExternalInfoLine(request));
        rows.Children.Add(new TextBlock
        {
            Text = DescribeOutstanding(request, asOf),
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        actions.Children.Add(ReviewButton(request));

        if (request.Status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted or InvoiceRequestStatus.Unknown)
            actions.Children.Add(ReconcileButton(request));

        if (request.Status == InvoiceRequestStatus.Reauthorise)
        {
            actions.Children.Add(new TextBlock
            {
                Text = "Re-authorise in Settings > Invoicing to continue.",
                FontSize = DesignTokens.FontSizeCaption,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.85,
            });
        }

        rows.Children.Add(actions);
        return RowBorder(rows, request.Id);
    }

    private static string DescribeOutstanding(InvoiceRequest request, DateTimeOffset asOf) => request.Status switch
    {
        InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted when request.DueOn is { } due =>
            $"{DateOnly.FromDateTime(asOf.UtcDateTime).DayNumber - due.DayNumber} day(s) overdue (due {due:yyyy-MM-dd}).",
        InvoiceRequestStatus.Reauthorise => "Needs attention: the connector needs re-authorising.",
        InvoiceRequestStatus.Unknown => "Needs attention: the response was lost; reconciling.",
        _ => "Needs attention.",
    };

    /// <summary>Closed: Rejected, Voided — request, reason; Review.</summary>
    private Control BuildClosedRow(RequestRow row)
    {
        var request = row.Request;
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };
        rows.Children.Add(new TextBlock
        {
            Text = $"{row.ProjectName} — {request.DisplayName} ({request.Status})",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        rows.Children.Add(new TextBlock
        {
            Text = $"Reason: {request.LastError ?? "(none recorded)"}",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        actions.Children.Add(ReviewButton(request));
        rows.Children.Add(actions);

        return RowBorder(rows, request.Id);
    }

    // ================================================================
    // Actions
    // ================================================================

    private async Task OnSendAsync(Guid requestId)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm the send here — Send is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(requestId, InvoiceRequest.CanonicalKind)]);
        var invocation = await _commandRegistry.InvokeAsync(InvoicingCommandIds.Send, context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Send is unavailable.", succeeded: false);
            return;
        }

        if (!result.Succeeded)
        {
            Report(result.Message ?? "Send failed.", succeeded: false);
            return;
        }

        // The connector's own outcome is read off the request's own,
        // freshly re-read status — never the command's own message text —
        // so the exact wording this area promises (`brief-19.1A-part3.md`
        // §2) does not depend on `InvoicingCommands.SendInvoiceCommandHandler`,
        // a file this Work Package does not own.
        await RefreshAsync().ConfigureAwait(true);

        var refreshed = await _domainContext.Repository.FindAsync(requestId).ConfigureAwait(true) as InvoiceRequest;
        Report(refreshed is null ? (result.Message ?? "Send attempted.") : DescribeSendOutcome(refreshed), succeeded: true);
    }

    private async Task OnReconcileAsync(Guid requestId)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm reconciliation here — Reconcile now is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(requestId, InvoiceRequest.CanonicalKind)]);
        var invocation = await _commandRegistry.InvokeAsync(InvoicingCommandIds.Reconcile, context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Reconcile now is unavailable.", succeeded: false);
            return;
        }

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? (result.Succeeded ? "Reconciled." : "Reconcile failed."), succeeded: result.Succeeded);
    }

    private async Task OnVoidAsync(Guid requestId)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm the void here — Void is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(requestId, InvoiceRequest.CanonicalKind)]);
        var invocation = await _commandRegistry.InvokeAsync(InvoicingCommandIds.Void, context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Void is unavailable.", succeeded: false);
            return;
        }

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? (result.Succeeded ? "Voided." : "Void failed."), succeeded: result.Succeeded);
    }

    /// <summary>
    /// Raises a new <see cref="InvoiceRequest"/> from a completion or, `WP
    /// 21.3B`, an expense (`invoicing.raise`,
    /// <see cref="IInvoicingService.RaiseFromCompletionAsync"/>/
    /// <see cref="IInvoicingService.RaiseFromExpenseAsync"/>) — the
    /// identical command <see cref="ProjectDeliverablesView"/>'s own
    /// "Raise invoice" dispatches, here through <see cref="ICommandRegistry"/>
    /// (with its own confirmation) rather than <see cref="ICommandDispatcher"/>
    /// directly, matching Send/Reconcile/Void's own shape in this view. A
    /// refusal (already invoiced, no client, no rate-card pin, nothing to
    /// bill) is shown, never swallowed.
    /// </summary>
    private async Task OnRaiseAsync(Guid sourceId, string sourceKind)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm raising an invoice here — Raise invoice is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(sourceId, sourceKind)]);
        var invocation = await _commandRegistry.InvokeAsync(InvoicingCommandIds.Raise, context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Raise invoice is unavailable.", succeeded: false);
            return;
        }

        if (!result.Succeeded)
        {
            Report(result.Message ?? "Raise invoice failed.", succeeded: false);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Invoice request raised.", succeeded: true);

        // `WP 17.9.4`: what you make opens right up.
        if (result.SubjectId is { } createdId)
            _openObject(createdId, InvoiceRequest.CanonicalKind);
    }

    private void OnWorkspaceChanged(WorkspaceChange change)
    {
        if (!change.Entries.Any(e => e.Kind is InvoiceRequest.CanonicalKind or DeliverableCompletion.CanonicalKind or ProjectExpense.CanonicalKind))
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

    /// <summary>
    /// Describes what a connector reported for a just-sent request, in the
    /// exact terms `brief-19.1A-part3.md` §2 promises — a small, deliberate
    /// duplicate of <c>InvoicingCommands.SendInvoiceCommandHandler.DescribeSendOutcome</c>'s
    /// own switch, kept here rather than in <c>Tempest.Workspace.Invoicing</c>
    /// (a file this Work Package does not own, and a file `WP 19.1B`'s own
    /// parallel work may also touch) so this area's own wording is this
    /// area's own to keep correct.
    /// </summary>
    private static string DescribeSendOutcome(InvoiceRequest request) => request.Status switch
    {
        InvoiceRequestStatus.Sent => $"Sent — external invoice number '{request.ExternalInvoiceNumber ?? request.ExternalId}'.",
        InvoiceRequestStatus.Rejected => $"Rejected: {request.LastError}",
        InvoiceRequestStatus.Reauthorise => "The connector needs re-authorising — see Settings > Invoicing to reauthorise.",
        InvoiceRequestStatus.Draft => "The connector is unavailable; the request stays Draft — try again later.",
        InvoiceRequestStatus.Unknown => "The response was lost; reconciling.",
        _ => $"Send attempted — status now {request.Status}.",
    };

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };

    private sealed record RequestRow(InvoiceRequest Request, string ProjectName);

    private sealed record CompletionRow(DeliverableCompletion Completion, string ProjectName, Deliverable? Deliverable);

    private sealed record ExpenseRow(ProjectExpense Expense, string ProjectName);
}
