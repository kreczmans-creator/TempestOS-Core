using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Expenses;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Expenses;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Views;

/// <summary>
/// The project workspace's own Details tab (`WP 20.10A`, Product Owner
/// findings D2/D12/T1) — the project's own identity, and its Commercial
/// section: client, purchase order, budget, rate card, dates and project
/// manager, reachable directly from the project workspace rather than only
/// through the generic Object Editor, which no project tab ever embedded
/// (`T1`: "Commercial ... This doesnt exist at all. Not seen anywhere and
/// cannot navigate to it anywhere").
/// </summary>
/// <remarks>
/// <para>
/// <b>The kill switch this Work Package's own brief names.</b> Hosting
/// <see cref="ObjectEditorView"/> itself inside a project tab would fight
/// this workspace's own layout: that control is built, by
/// <see cref="ObjectEditorView.TryCreate"/>, for one fixed object id at a
/// time, and the Details tab has to follow whichever project is currently
/// open, rebuilding the whole generic editor (Identity, Content, BOM,
/// Requirements, Calculations, Verification, Attachments, Validation,
/// Relationships, Invoice, Quotation Lines, Evidence — every section
/// besides Commercial, all hidden for a Project by its own declaration
/// gate, but still constructed) on every single project switch. This view
/// is the dedicated small surface the brief's own kill switch allows
/// instead: the same fields, over the identical
/// <see cref="ProjectCommercialEditorSupport"/> collaborator and the
/// identical already-registered <c>project.*</c> commands
/// <see cref="ObjectEditorView"/>'s own Commercial section already
/// dispatches through (<c>SetProjectClientCommand</c>,
/// <c>SetProjectPurchaseOrderCommand</c>, <c>SetProjectBudgetCommand</c>,
/// <c>PinProjectRateCardCommand</c>, <c>SetProjectDatesCommand</c>,
/// <c>SetProjectManagerCommand</c>) — so a write here and a write through
/// the generic editor are the same act, one transaction with an audit row,
/// through <see cref="IProjectCommercialService"/>. Change client and Pin
/// rate card open the identical <c>OrganisationPicker</c>/<c>RateCardPicker</c>
/// overlays the generic editor's own Change/Pin buttons open — reused, not
/// rebuilt.
/// </para>
/// <para>
/// Name and identifier are shown read-only: renaming a project is a
/// separate capability (<c>IWorkspaceManager.RenameObjectAsync</c>) this
/// Work Package's own findings never asked for, so it is left out rather
/// than added unasked.
/// </para>
/// </remarks>
public sealed class ProjectDetailsView : UserControl
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly Func<Guid?> _currentProjectId;
    private readonly ProjectCommercialEditorSupport _commercialSupport;
    private readonly IExpenseService? _expenseService;
    private readonly ExpenseEntryPrompt? _recordExpensePrompt;
    private readonly Action<Guid, string>? _openObject;

    private readonly TextBlock _name = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeTitle, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _identity = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 };

    private readonly TextBlock _clientText = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly Button _changeClientButton = new() { Content = "Change client…", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _clientStatus = new() { FontSize = DesignTokens.FontSizeCaption };

    private readonly TextBox _purchaseOrderBox = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _purchaseOrderSaveButton = new() { Content = "Save", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _purchaseOrderStatus = new() { FontSize = DesignTokens.FontSizeCaption };

    private readonly TextBox _budgetBox = new() { MinHeight = DesignTokens.ControlSizeMedium, Watermark = "<amount> <currency>, e.g. 50000 GBP" };
    private readonly Button _budgetSaveButton = new() { Content = "Save", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _budgetStatus = new() { FontSize = DesignTokens.FontSizeCaption };

    private readonly TextBlock _rateCardText = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly Button _changeRateCardButton = new() { Content = "Pin rate card…", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _rateCardStatus = new() { FontSize = DesignTokens.FontSizeCaption };

    private readonly DatePicker _startDate = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly DatePicker _targetDate = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _datesSaveButton = new() { Content = "Save", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _datesStatus = new() { FontSize = DesignTokens.FontSizeCaption };

    private readonly TextBox _projectManagerBox = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _useMeButton = new() { Content = "Use Me", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _projectManagerSaveButton = new() { Content = "Save", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _projectManagerStatus = new() { FontSize = DesignTokens.FontSizeCaption };

    // `WP 21.3B`: recorded here as well as from Business → Timesheets — the
    // brief's own two call sites for "Record expense…".
    private readonly TextBlock _expensesText = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly Button _recordExpenseButton = new() { Content = "Record expense…", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _expensesStatus = new() { FontSize = DesignTokens.FontSizeCaption };

    private readonly TextBlock _noProject = new() { FontSize = DesignTokens.FontSizeBody, Opacity = 0.7, IsVisible = false };
    private readonly StackPanel _body = new() { Spacing = DesignTokens.SpaceLg };

    private readonly WorkspaceChangesSubscription _workspaceChanges;

    private Guid? _projectId;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention.</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>The change feed this view re-reads the open project's own commercial fields from — mirrors every sibling project tab built externally (Deliverables, Quote, Evidence).</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Test-only (mirrors <c>WorkspaceChangesReattachTests</c>'s own convention): counts every <see cref="RefreshAsync"/> call.</summary>
    internal int RefreshCount { get; private set; }

    /// <summary>Initialises a new instance of the <see cref="ProjectDetailsView"/> class.</summary>
    /// <param name="expenseService">Reads the open project's own expense total for the summary line (`WP 21.3B`).</param>
    /// <param name="recordExpensePrompt">Collects "Record expense…"'s own values. <see langword="null"/> leaves the button honestly unavailable, mirroring every optional collaborator's identical convention across this platform's Desktop views.</param>
    /// <param name="openObject">Opens the recorded expense right up (`WP 17.9.4`). <see langword="null"/> leaves it merely recorded, not opened.</param>
    public ProjectDetailsView(
        EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher, Func<Guid?> currentProjectId,
        ProjectCommercialEditorSupport commercialSupport, IExpenseService? expenseService = null,
        ExpenseEntryPrompt? recordExpensePrompt = null, Action<Guid, string>? openObject = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(currentProjectId);
        ArgumentNullException.ThrowIfNull(commercialSupport);

        _domainContext = domainContext;
        _commandDispatcher = commandDispatcher;
        _currentProjectId = currentProjectId;
        _commercialSupport = commercialSupport;
        _expenseService = expenseService;
        _recordExpensePrompt = recordExpensePrompt;
        _openObject = openObject;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        AutomationProperties.SetName(_changeClientButton, "Change client…");
        AutomationProperties.SetName(_purchaseOrderBox, "Purchase order reference");
        AutomationProperties.SetName(_purchaseOrderSaveButton, "Save purchase order");
        AutomationProperties.SetName(_budgetBox, "Budget");
        AutomationProperties.SetName(_budgetSaveButton, "Save budget");
        AutomationProperties.SetName(_changeRateCardButton, "Pin rate card…");
        AutomationProperties.SetName(_startDate, "Start date");
        AutomationProperties.SetName(_targetDate, "Target date");
        AutomationProperties.SetName(_datesSaveButton, "Save dates");
        AutomationProperties.SetName(_projectManagerBox, "Project manager");
        AutomationProperties.SetName(_useMeButton, "Use Me");
        AutomationProperties.SetName(_projectManagerSaveButton, "Save project manager");
        AutomationProperties.SetName(_recordExpenseButton, "Record expense…");

        _changeClientButton.Classes.Add(ChromeStyles.Subtle);
        _purchaseOrderSaveButton.Classes.Add(ChromeStyles.Subtle);
        _budgetSaveButton.Classes.Add(ChromeStyles.Subtle);
        _changeRateCardButton.Classes.Add(ChromeStyles.Subtle);
        _datesSaveButton.Classes.Add(ChromeStyles.Subtle);
        _useMeButton.Classes.Add(ChromeStyles.Subtle);
        _projectManagerSaveButton.Classes.Add(ChromeStyles.Subtle);
        _recordExpenseButton.Classes.Add(ChromeStyles.Subtle);

        _changeClientButton.Click += async (_, _) => await OnChangeClientAsync().ConfigureAwait(true);
        _purchaseOrderSaveButton.Click += async (_, _) => await OnSavePurchaseOrderAsync().ConfigureAwait(true);
        _budgetSaveButton.Click += async (_, _) => await OnSaveBudgetAsync().ConfigureAwait(true);
        _changeRateCardButton.Click += async (_, _) => await OnChangeRateCardAsync().ConfigureAwait(true);
        _datesSaveButton.Click += async (_, _) => await OnSaveDatesAsync().ConfigureAwait(true);
        _useMeButton.Click += (_, _) => _projectManagerBox.Text = _commercialSupport.CurrentPrincipalIdentityId() ?? _projectManagerBox.Text;
        _projectManagerSaveButton.Click += async (_, _) => await OnSaveProjectManagerAsync().ConfigureAwait(true);
        _recordExpenseButton.Click += async (_, _) => await OnRecordExpenseAsync().ConfigureAwait(true);

        foreach (var status in new[] { _clientStatus, _purchaseOrderStatus, _budgetStatus, _rateCardStatus, _datesStatus, _projectManagerStatus, _expensesStatus })
            ThemeReactiveBrush.Bind(status, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);

        _body.Children.Add(Section("Client", Row(_clientText, _changeClientButton), _clientStatus));
        _body.Children.Add(Section("Purchase order reference", Row(_purchaseOrderBox, _purchaseOrderSaveButton), _purchaseOrderStatus));
        _body.Children.Add(Section("Budget", Row(_budgetBox, _budgetSaveButton), _budgetStatus));
        _body.Children.Add(Section("Rate card", Row(_rateCardText, _changeRateCardButton), _rateCardStatus));
        _body.Children.Add(Section("Expenses", Row(_expensesText, _recordExpenseButton), _expensesStatus));

        var datesRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        datesRow.Children.Add(_startDate);
        datesRow.Children.Add(_targetDate);
        datesRow.Children.Add(_datesSaveButton);
        _body.Children.Add(Section("Start / target dates", datesRow, _datesStatus));

        var pmRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        pmRow.Children.Add(_projectManagerBox);
        pmRow.Children.Add(_useMeButton);
        pmRow.Children.Add(_projectManagerSaveButton);
        _body.Children.Add(Section("Project manager", pmRow, _projectManagerStatus));

        var page = new StackPanel { Spacing = DesignTokens.SpaceLg, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        page.Children.Add(PageHeading.Label("DETAILS"));
        page.Children.Add(_name);
        page.Children.Add(_identity);
        page.Children.Add(_noProject);
        page.Children.Add(_body);

        AutomationProperties.SetName(this, "Details");
        Content = new ScrollViewer { Content = page };
    }

    private static Control Row(Control primary, Control action)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(primary);
        row.Children.Add(action);
        return row;
    }

    private static StackPanel Section(string title, Control content, Control status)
    {
        var section = new StackPanel { Spacing = DesignTokens.SpaceXs };
        section.Children.Add(new TextBlock { Text = title, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody });
        section.Children.Add(content);
        section.Children.Add(status);
        return section;
    }

    /// <summary>Re-reads the open project's own identity and Commercial fields — empty, honestly, when no project is open.</summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;

        if (_currentProjectId() is not { } projectId
            || await _domainContext.Repository.FindAsync(projectId).ConfigureAwait(true) is not IProject project)
        {
            _projectId = null;
            _name.Text = string.Empty;
            _identity.Text = string.Empty;
            _noProject.Text = "No project open.";
            _noProject.IsVisible = true;
            _body.IsVisible = false;
            return;
        }

        _projectId = projectId;
        _noProject.IsVisible = false;
        _body.IsVisible = true;

        _name.Text = project.DisplayName;
        _identity.Text = project.Identifier is { } identifier ? $"{identifier}  ·  {project.Kind}" : project.Kind;

        var clientName = project.ClientOrganisationId is { } clientId ? await ResolveClientNameAsync(clientId).ConfigureAwait(true) : null;
        _clientText.Text = clientName ?? project.ClientOrganisationId ?? "(no client set)";
        _clientText.Opacity = project.ClientOrganisationId is null ? 0.5 : 1.0;
        _clientStatus.Text = string.Empty;

        _purchaseOrderBox.Text = project.PurchaseOrderReference ?? string.Empty;
        _purchaseOrderStatus.Text = string.Empty;

        _budgetBox.Text = project.Budget?.ToString() ?? string.Empty;
        _budgetStatus.Text = string.Empty;

        var rateCardDisplay = project.RateCardPin is { } pin ? await ResolveRateCardDisplayAsync(pin).ConfigureAwait(true) : null;
        _rateCardText.Text = rateCardDisplay ?? (project.RateCardPin is { } unresolvedPin ? unresolvedPin.ToString() : "(no rate card pinned)");
        _rateCardText.Opacity = project.RateCardPin is null ? 0.5 : 1.0;
        _rateCardStatus.Text = string.Empty;

        _startDate.SelectedDate = project.StartDate is { } start ? new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue)) : null;
        _targetDate.SelectedDate = project.TargetDate is { } target ? new DateTimeOffset(target.ToDateTime(TimeOnly.MinValue)) : null;
        _datesStatus.Text = string.Empty;

        _projectManagerBox.Text = project.ProjectManagerIdentityId ?? string.Empty;
        _projectManagerStatus.Text = string.Empty;

        await RefreshExpensesSummaryAsync(projectId).ConfigureAwait(true);
    }

    /// <summary>`WP 21.3B`: a plain count and unbilled total — the project's own expenses are browsed in full through the Project Explorer's own Expenses area, not re-listed here.</summary>
    private async Task RefreshExpensesSummaryAsync(Guid projectId)
    {
        if (_expenseService is null)
        {
            _expensesText.Text = "(expense recording is unavailable here)";
            _expensesText.Opacity = 0.5;
            return;
        }

        var all = await _expenseService.ListForProjectAsync(projectId).ConfigureAwait(true);
        var unbilled = all.Where(e => e.Billable && e.InvoicedBy is null).ToList();

        _expensesText.Text = all.Count == 0
            ? "No expenses recorded."
            : $"{all.Count} expense(s) recorded — {unbilled.Count} billable and not yet invoiced.";
        _expensesText.Opacity = all.Count == 0 ? 0.5 : 1.0;
        _expensesStatus.Text = string.Empty;
    }

    private async Task<string?> ResolveClientNameAsync(string clientOrganisationId)
    {
        if (_commercialSupport.ResolveClientNameAsync is not { } resolve)
            return null;

        var name = await resolve(clientOrganisationId, CancellationToken.None).ConfigureAwait(true);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private async Task<string?> ResolveRateCardDisplayAsync(ReferencePin pin)
    {
        if (_commercialSupport.ResolveRateCardAsync is not { } resolve)
            return null;

        var resolved = await resolve(pin, CancellationToken.None).ConfigureAwait(true);
        return resolved is { } card ? $"{card.Code} — {card.Name} (rev {pin.RevisionNumber})" : null;
    }

    private async Task OnChangeClientAsync()
    {
        if (_projectId is not { } projectId)
            return;

        var picked = await _commercialSupport.PickClientOrganisationIdAsync(CancellationToken.None).ConfigureAwait(true);
        if (picked is null)
        {
            _clientStatus.Text = "Change client was cancelled.";
            return;
        }

        var organisationId = picked.Length == 0 ? null : picked;
        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectClientCommand(projectId, MechanicalObjectFactoryRegistry.Project, organisationId), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_clientStatus, result).ConfigureAwait(true);
    }

    private async Task OnChangeRateCardAsync()
    {
        if (_projectId is not { } projectId)
            return;

        var picked = await _commercialSupport.PickRateCardIdAsync(CancellationToken.None).ConfigureAwait(true);
        if (picked is null)
        {
            _rateCardStatus.Text = "Pin rate card was cancelled.";
            return;
        }

        var result = await _commandDispatcher
            .DispatchAsync(new PinProjectRateCardCommand(projectId, MechanicalObjectFactoryRegistry.Project, picked), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_rateCardStatus, result).ConfigureAwait(true);
    }

    private async Task OnSavePurchaseOrderAsync()
    {
        if (_projectId is not { } projectId)
            return;

        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectPurchaseOrderCommand(projectId, MechanicalObjectFactoryRegistry.Project, NullIfEmpty(_purchaseOrderBox.Text)), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_purchaseOrderStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveBudgetAsync()
    {
        if (_projectId is not { } projectId)
            return;

        var text = _budgetBox.Text?.Trim();
        Money? budget = null;

        if (!string.IsNullOrWhiteSpace(text))
        {
            if (!TryParseMoney(text, out var parsed))
            {
                _budgetStatus.Text = "Budget must be \"<amount> <currency>\" (e.g. \"50000 GBP\"), or blank to clear.";
                return;
            }

            budget = parsed;
        }

        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectBudgetCommand(projectId, MechanicalObjectFactoryRegistry.Project, budget), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_budgetStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveDatesAsync()
    {
        if (_projectId is not { } projectId)
            return;

        var startDate = _startDate.SelectedDate is { } start ? DateOnly.FromDateTime(start.Date) : (DateOnly?)null;
        var targetDate = _targetDate.SelectedDate is { } target ? DateOnly.FromDateTime(target.Date) : (DateOnly?)null;

        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectDatesCommand(projectId, MechanicalObjectFactoryRegistry.Project, startDate, targetDate), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_datesStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveProjectManagerAsync()
    {
        if (_projectId is not { } projectId)
            return;

        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectManagerCommand(projectId, MechanicalObjectFactoryRegistry.Project, NullIfEmpty(_projectManagerBox.Text)), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_projectManagerStatus, result).ConfigureAwait(true);
    }

    /// <summary>"Record expense…" (`WP 21.3B`) — the project's own second call site, beside Business → Timesheets' identical one (<see cref="Views.TimesheetWeekView"/>). Pre-selects this project in the prompt's own Project drop-down.</summary>
    private async Task OnRecordExpenseAsync()
    {
        if (_projectId is not { } projectId)
            return;

        if (_recordExpensePrompt is null)
        {
            _expensesStatus.Text = "Nothing can collect the expense here — Record expense is unavailable.";
            return;
        }

        var input = await _recordExpensePrompt.PromptAsync(projectId).ConfigureAwait(true);
        if (input is null)
        {
            _expensesStatus.Text = "Record expense was cancelled.";
            return;
        }

        var command = new Tempest.Workspace.Expenses.RecordExpenseCommand(
            input.ProjectId, input.Date, input.Description, input.Category, input.NetAmount, input.VatAmount, input.Billable);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Expense recorded." : result.Message ?? "Record expense failed.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        _expensesStatus.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));

        // `WP 17.9.4`: what you make opens right up.
        if (result.Succeeded && result.SubjectId is { } createdId)
            _openObject?.Invoke(createdId, ProjectExpense.CanonicalKind);
    }

    private async Task ReportAsync(TextBlock statusMessage, CommandResult result)
    {
        var message = result.Succeeded ? "Saved." : result.Message ?? "Save failed.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        statusMessage.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    private static string? NullIfEmpty(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool TryParseMoney(string value, out Money money)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 && decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            try
            {
                money = new Money(amount, new CurrencyCode(parts[1]));
                return true;
            }
            catch (ArgumentException)
            {
                // Falls through to the failure return below.
            }
        }

        money = default;
        return false;
    }

    private void OnWorkspaceChanged(WorkspaceChange change) =>
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Best-effort background refresh — mirrors every sibling
                // project tab's own identical "the next real entry is the
                // backstop" shape.
            }
        });
}
