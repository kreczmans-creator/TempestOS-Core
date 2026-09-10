using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Invoicing;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Invoicing;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Invoicing area (`WP 19.1A` part 3, `ADR-0151`): every
/// <see cref="InvoiceRequest"/> across every live project — or, when a
/// project is open, that project's own requests alone — grouped by status;
/// <b>Send</b>, <b>Reconcile now</b> and <b>Void</b> per request, with every
/// connector outcome shown; <b>Review</b> opens the request in the Object
/// Editor. Raising a request happens from the project's own Deliverables
/// tab (<see cref="ProjectDeliverablesView"/>) — this area only ever reads
/// and acts on requests that already exist.
/// </summary>
/// <remarks>
/// <para>
/// <b>Renders from the domain, refreshed by the change feed.</b> Mirrors
/// <see cref="TimesheetWeekView"/>/<see cref="ProjectDeliverablesView"/>'s
/// own identical discipline: every request is read fresh, through one
/// coherent <see cref="EngineeringDomainContext.Repository"/> read, every
/// time <see cref="IWorkspaceChanges.Changed"/> touches
/// <see cref="InvoiceRequest.CanonicalKind"/> — there is no manual refresh
/// call site anywhere else in this class.
/// </para>
/// <para>
/// <b>Send/Reconcile/Void dispatch through <see cref="ICommandRegistry"/>,
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
    // `InvoiceRequestStatus`'s own declaration order groups
    // Voided/Unknown/Reauthorise/Unavailable differently to how a person
    // reading this area wants them: Unknown ("respond lost, reconciling")
    // is more urgent than a terminal Voided, so it is grouped earlier.
    // `Unavailable` is excluded entirely — `InvoiceRequestStatus.Unavailable`'s
    // own remarks: it is never a stored status, so no request is ever found
    // in it.
    private static readonly IReadOnlyList<InvoiceRequestStatus> StatusOrder =
    [
        InvoiceRequestStatus.Draft, InvoiceRequestStatus.Sending, InvoiceRequestStatus.Sent, InvoiceRequestStatus.Accepted,
        InvoiceRequestStatus.Rejected, InvoiceRequestStatus.Unknown, InvoiceRequestStatus.Reauthorise, InvoiceRequestStatus.Voided,
    ];

    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandRegistry _commandRegistry;
    private readonly Func<Guid?> _currentProjectId;
    private readonly Action<Guid, string> _openObject;

    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly StackPanel _groups = new() { Spacing = DesignTokens.SpaceMd };

    private IWorkspaceChanges? _workspaceChanges;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// Collects Send/Reconcile/Void's own confirmation — reuses
    /// <see cref="RibbonView.ParameterPrompt"/>'s own pattern, so all three
    /// dispatch through the already-registered
    /// <see cref="InvoicingCommandIds"/> descriptors exactly as the Ribbon
    /// and the Command Palette do. <see langword="null"/> (any test that
    /// constructs this view directly) leaves all three honestly
    /// unavailable rather than run without asking.
    /// </summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>The change feed this view reloads its own list from (`WP 18.1A`, `WP 18.9.1`).</summary>
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

        this.DetachedFromVisualTree += (_, _) => WorkspaceChanges = null;

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

    /// <summary>Reloads every invoice request in scope — every live project's own requests, or the open project's alone when one is open.</summary>
    public async Task RefreshAsync()
    {
        var scopedProjectId = _currentProjectId();

        var projects = await ProjectsInScopeAsync(scopedProjectId).ConfigureAwait(true);

        var rows = new List<RequestRow>();
        foreach (var project in projects)
        {
            var requests = (await _domainContext.Repository.ListChildrenAsync(project.Id).ConfigureAwait(true))
                .OfType<InvoiceRequest>()
                .Where(IsLive);

            var projectName = DisplayNameOf(project);
            foreach (var request in requests)
                rows.Add(new RequestRow(request, projectName));
        }

        _status.Text = rows.Count == 0
            ? (scopedProjectId is null ? "No invoice requests yet." : "No invoice requests for this project yet.")
            : $"{rows.Count} request(s) across {projects.Count} project(s).";

        _groups.Children.Clear();
        foreach (var status in StatusOrder)
        {
            var statusRows = rows
                .Where(r => r.Request.Status == status)
                .OrderByDescending(r => r.Request.SentAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(r => r.Request.DisplayName, StringComparer.Ordinal)
                .ToList();

            _groups.Children.Add(BuildGroup(status, statusRows));
        }
    }

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

    private Control BuildGroup(InvoiceRequestStatus status, IReadOnlyList<RequestRow> rows)
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(new TextBlock
        {
            Text = $"{status} ({rows.Count})",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
        });

        if (rows.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "(none)", Opacity = 0.6, FontSize = DesignTokens.FontSizeCaption });
        }
        else
        {
            foreach (var row in rows)
                panel.Children.Add(BuildRow(row));
        }

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = panel,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    private Control BuildRow(RequestRow row)
    {
        var request = row.Request;
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        rows.Children.Add(new TextBlock
        {
            Text = $"{row.ProjectName} — {request.DisplayName} — Client {request.ClientOrganisationId}"
                + (request.PurchaseOrderReference is { } po ? $" — PO {po}" : string.Empty)
                + $" — {request.Total}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        rows.Children.Add(new TextBlock
        {
            Text = $"External # {request.ExternalInvoiceNumber ?? "(none)"}"
                + $" — Issued {request.IssuedDate?.ToString("yyyy-MM-dd") ?? "(none)"}"
                + $" — Paid {request.PaidDate?.ToString("yyyy-MM-dd") ?? "(not paid)"}"
                + (request.LastError is { } error ? $" — {error}" : string.Empty),
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };

        var review = new Button { Content = "Review", MinHeight = DesignTokens.MinControlSize };
        review.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(review, $"Review {request.DisplayName}");
        review.Click += (_, _) => _openObject(request.Id, InvoiceRequest.CanonicalKind);
        actions.Children.Add(review);

        if (request.Status == InvoiceRequestStatus.Draft)
        {
            var send = new Button { Content = "Send", MinHeight = DesignTokens.MinControlSize };
            send.Classes.Add(ChromeStyles.Primary);
            AutomationProperties.SetName(send, $"Send {request.DisplayName}");
            send.Click += async (_, _) => await OnSendAsync(request.Id).ConfigureAwait(true);
            actions.Children.Add(send);
        }

        if (request.Status is InvoiceRequestStatus.Unknown or InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted)
        {
            var reconcile = new Button { Content = "Reconcile now", MinHeight = DesignTokens.MinControlSize };
            reconcile.Classes.Add(ChromeStyles.Flat);
            AutomationProperties.SetName(reconcile, $"Reconcile {request.DisplayName}");
            reconcile.Click += async (_, _) => await OnReconcileAsync(request.Id).ConfigureAwait(true);
            actions.Children.Add(reconcile);
        }

        if (request.Status is InvoiceRequestStatus.Draft or InvoiceRequestStatus.Rejected)
        {
            var voidButton = new Button { Content = "Void", MinHeight = DesignTokens.MinControlSize };
            voidButton.Classes.Add(ChromeStyles.Flat);
            AutomationProperties.SetName(voidButton, $"Void {request.DisplayName}");
            voidButton.Click += async (_, _) => await OnVoidAsync(request.Id).ConfigureAwait(true);
            actions.Children.Add(voidButton);
        }

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

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = rows,
            Tag = request.Id,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

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

    private void OnWorkspaceChanged(WorkspaceChange change)
    {
        if (!change.Entries.Any(e => e.Kind == InvoiceRequest.CanonicalKind))
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
}
