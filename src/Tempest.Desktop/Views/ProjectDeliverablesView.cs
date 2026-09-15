using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;
using Tempest.Workspace;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Views;

/// <summary>
/// The project workspace's own Deliverables tab (`WP 19.0A`, `ADR-0150`):
/// every milestone <c>Deliverable</c> of the open project with its own
/// completion state; <b>Complete</b> opens a prompt (completion date,
/// fixed-price value optional, the issued Evidence records to attach, the
/// documents) and dispatches <c>deliverable.complete</c>; a second Complete
/// on a completed deliverable shows the refusal naming the first.
/// </summary>
/// <remarks>
/// <para>
/// <b>Renders from the domain, refreshed by the change feed.</b> Mirrors
/// <see cref="EvidenceWorkspaceView"/>'s own identical discipline (`WP
/// 18.9.1`): every deliverable and completion is read fresh, through one
/// coherent <see cref="EngineeringDomainContext.Repository"/> read, every
/// time <see cref="IWorkspaceChanges.Changed"/> touches either Kind — there
/// is no manual refresh call site anywhere else in this class.
/// </para>
/// <para>
/// <b>No blocking calls.</b> Every read and write here is
/// <see langword="await"/>ed.
/// </para>
/// </remarks>
public sealed class ProjectDeliverablesView : UserControl
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;
    private readonly Func<Guid?> _currentProjectId;
    private readonly DeliverableCompletionPrompt _completionPrompt;
    private readonly Action<Guid, string> _openObject;

    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly StackPanel _list = new() { Spacing = DesignTokens.SpaceSm };
    private readonly Button _addButton = new() { Content = "Add Deliverable", MinHeight = DesignTokens.ControlSizeMedium };

    private readonly WorkspaceChangesSubscription _workspaceChanges;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// Collects Add Deliverable's own title/target date (`WP 19.5B`,
    /// `ADR-0152` §7) — reuses <c>deliverable.add</c>'s own registered
    /// <see cref="CommandBinding"/>, exactly as <see cref="InvoicingView.ParameterPrompt"/>
    /// does for Send/Reconcile/Void. <see langword="null"/> (any test that
    /// constructs this view directly) leaves Add honestly unavailable
    /// rather than run without asking.
    /// </summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>The change feed this view reloads its own list from (`WP 18.1A`, `WP 18.9.1`).</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="ProjectDeliverablesView"/> class.</summary>
    public ProjectDeliverablesView(
        EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry, Func<Guid?> currentProjectId,
        DeliverableCompletionPrompt completionPrompt, Action<Guid, string> openObject)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(currentProjectId);
        ArgumentNullException.ThrowIfNull(completionPrompt);
        ArgumentNullException.ThrowIfNull(openObject);

        _domainContext = domainContext;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
        _currentProjectId = currentProjectId;
        _completionPrompt = completionPrompt;
        _openObject = openObject;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        var heading = new TextBlock
        {
            Text = "Deliverables",
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        // `WP 19.5B` (`ADR-0152` §7, Product Owner comment item 4's second
        // half): a deliverable can now be added directly, with no
        // quotation and no milestone timeline detour.
        AutomationProperties.SetName(_addButton, "Add Deliverable");
        _addButton.Classes.Add(ChromeStyles.Primary);
        _addButton.Click += async (_, _) => await OnAddAsync().ConfigureAwait(true);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        headerRow.Children.Add(heading);
        headerRow.Children.Add(_addButton);

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(headerRow);
        body.Children.Add(_status);
        body.Children.Add(_list);

        AutomationProperties.SetName(this, "Deliverables");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Test-only (`WP 19.7C`, <c>WorkspaceChangesReattachTests</c>): counts every <see cref="RefreshAsync"/> call, proving a reattached view's subscription still reaches <see cref="OnWorkspaceChanged"/>.</summary>
    internal int RefreshCount { get; private set; }

    /// <summary>Reloads the open project's own deliverables and their completions — empty, honestly, when no project is open.</summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;

        var projectId = _currentProjectId();

        if (projectId is not { } id)
        {
            _status.Text = "Open a project to see, and complete, its deliverables.";
            _list.Children.Clear();
            return;
        }

        var members = await ProjectMembership.ListProjectMembersAsync(_domainContext.Repository, id, CancellationToken.None).ConfigureAwait(true);

        var deliverables = members
            .OfType<Deliverable>()
            .Where(d => d is not IDeletable { IsDeleted: true })
            .OrderBy(d => d.DisplayName, StringComparer.Ordinal)
            .ToList();

        var completionEntries = await _domainContext.Repository.ListChildrenAsync(id, CancellationToken.None).ConfigureAwait(true);
        var completions = await _domainContext.Repository.MaterialiseAsync<DeliverableCompletion>(
            [.. completionEntries.Where(entry => !entry.IsDeleted)], CancellationToken.None).ConfigureAwait(true);

        _status.Text = deliverables.Count == 0
            ? "No deliverables in this project yet."
            : $"{deliverables.Count} deliverable(s), {completions.Count} completed.";

        _list.Children.Clear();
        foreach (var deliverable in deliverables)
        {
            var completion = completions.FirstOrDefault(c => c.DeliverableId == deliverable.Id);
            _list.Children.Add(BuildRow(id, deliverable, completion));
        }
    }

    private Control BuildRow(Guid projectId, Deliverable deliverable, DeliverableCompletion? completion)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        rows.Children.Add(new TextBlock
        {
            Text = $"{deliverable.Identifier ?? "Deliverable"} — {deliverable.DisplayName} ({deliverable.Status})",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        rows.Children.Add(new TextBlock
        {
            Text = completion is null
                ? "Not yet completed."
                : $"Completed {completion.CompletedOn:yyyy-MM-dd} by {completion.PrincipalIdentityId}"
                  + (completion.FixedPriceValue is { } price ? $" — fixed price {price}" : " — time-billed")
                  + $" — {completion.IssuedEvidenceIds.Count} evidence record(s), {completion.DocumentIds.Count} document(s)."
                  // `WP 19.1A` part 3 (`ADR-0151`): once invoiced, this
                  // completion's own `InvoicedBy` link is set once and
                  // never cleared — shown here so its Raise button's own
                  // absence (below) is not the only sign it has already
                  // been billed.
                  + (completion.InvoicedBy is { } requestId ? $" — invoiced (request '{requestId:N}')." : string.Empty),
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };

        var complete = new Button { Content = "Complete", MinHeight = DesignTokens.MinControlSize };
        complete.Classes.Add(ChromeStyles.Primary);
        AutomationProperties.SetName(complete, $"Complete {deliverable.DisplayName}");
        complete.Click += async (_, _) => await OnCompleteAsync(projectId, deliverable.Id).ConfigureAwait(true);
        actions.Children.Add(complete);

        if (completion is not null)
        {
            var open = new Button { Content = "Open completion", MinHeight = DesignTokens.MinControlSize };
            open.Classes.Add(ChromeStyles.Flat);
            AutomationProperties.SetName(open, $"Open the completion of {deliverable.DisplayName}");
            open.Click += (_, _) => _openObject(completion.Id, DeliverableCompletion.CanonicalKind);
            actions.Children.Add(open);

            // `WP 19.1A` part 3 (`ADR-0151`): a completed deliverable gets
            // a Raise invoice action, mirroring `Complete`'s own convention
            // in this exact file exactly — always present, dispatched
            // directly (never through `ICommandRegistry`'s own
            // confirmation flow; the same `invoicing.raise` command remains
            // reachable with its own confirmation from the Ribbon or the
            // Command Palette), and a second click on an already-invoiced
            // completion shows the refusal naming the first request rather
            // than hiding the button once it can only fail — exactly how a
            // second `Complete` on an already-completed deliverable already
            // behaves here.
            var raiseInvoice = new Button { Content = "Raise invoice", MinHeight = DesignTokens.MinControlSize };
            raiseInvoice.Classes.Add(ChromeStyles.Flat);
            AutomationProperties.SetName(raiseInvoice, $"Raise invoice for {deliverable.DisplayName}");
            raiseInvoice.Click += async (_, _) => await OnRaiseInvoiceAsync(completion.Id).ConfigureAwait(true);
            actions.Children.Add(raiseInvoice);
        }

        rows.Children.Add(actions);

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = rows,
            Tag = deliverable.Id,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    /// <summary>
    /// Adds a deliverable directly to the open project, with no quotation
    /// (<c>deliverable.add</c>, `WP 19.5B`, `ADR-0152` §7) — dispatched
    /// through <see cref="ICommandRegistry.InvokeAsync(string,CommandContext,CommandParameterPrompt?,CancellationToken)"/>
    /// so the same registered title/target-date parameters this view's own
    /// header button collects here are exactly what the ribbon's
    /// Deliverables category collects for the identical command.
    /// </summary>
    private async Task OnAddAsync()
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm this here — Add Deliverable is unavailable.", succeeded: false);
            return;
        }

        var projectId = _currentProjectId();
        var context = new CommandContext([], projectId);
        var invocation = await _commandRegistry.InvokeAsync("deliverable.add", context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Add Deliverable is unavailable.", succeeded: false);
            return;
        }

        if (!result.Succeeded)
        {
            Report(result.Message ?? "Add Deliverable failed.", succeeded: false);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Deliverable added.", succeeded: true);

        // `WP 17.9.4`: what you make opens right up.
        if (result.SubjectId is { } createdId)
            _openObject(createdId, CanonicalObjectKinds.Deliverable);
    }

    private async Task OnCompleteAsync(Guid projectId, Guid deliverableId)
    {
        var input = await _completionPrompt.PromptAsync(projectId).ConfigureAwait(true);
        if (input is null)
        {
            Report("Complete was cancelled.", succeeded: false);
            return;
        }

        var command = new Tempest.Workspace.Deliverables.CompleteDeliverableCommand(
            deliverableId, DeliverableCompletion.CanonicalKind, input.CompletedOn, input.IssuedEvidenceIds, input.DocumentIds, input.FixedPriceValue);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "Complete failed.", succeeded: false);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Completed.", succeeded: true);

        // `WP 17.9.4`: what you make opens right up.
        if (result.SubjectId is { } createdId)
            _openObject(createdId, DeliverableCompletion.CanonicalKind);
    }

    /// <summary>
    /// Raises a new <see cref="Tempest.Core.Invoicing.InvoiceRequest"/> from
    /// <paramref name="completionId"/> (<c>invoicing.raise</c>,
    /// <see cref="Tempest.Core.Invoicing.IInvoicingService.RaiseFromCompletionAsync"/>) —
    /// every refusal (no client, no rate-card pin, already invoiced,
    /// nothing to bill) is shown, never swallowed.
    /// </summary>
    private async Task OnRaiseInvoiceAsync(Guid completionId)
    {
        var command = new Tempest.Workspace.Invoicing.RaiseInvoiceCommand(completionId, DeliverableCompletion.CanonicalKind);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "Raise invoice failed.", succeeded: false);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Invoice request raised.", succeeded: true);

        // `WP 17.9.4`: what you make opens right up.
        if (result.SubjectId is { } createdId)
            _openObject(createdId, Tempest.Core.Invoicing.InvoiceRequest.CanonicalKind);
    }

    private void OnWorkspaceChanged(WorkspaceChange change)
    {
        if (!change.Entries.Any(e => e.Kind == CanonicalObjectKinds.Deliverable || e.Kind == DeliverableCompletion.CanonicalKind))
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
}
