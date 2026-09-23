using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Editors;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The project Commercial section (`WP 19.0A`, `ADR-0150`): client and
/// rate card, each read-only with a Change/Pin action opening a real
/// picker; purchase order reference, budget, dates and project manager,
/// each editable and dispatching its own already-registered <c>project.*</c>
/// command directly. Moved verbatim from <see cref="ObjectEditorView"/>'s
/// own former <c>PopulateCommercialAsync</c> and its six sibling
/// <c>On*</c>/<c>Resolve*</c> methods (`WP 21.1B`).
/// </summary>
internal sealed class CommercialSection : IEditorSection
{
    private readonly StackPanel _clientPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _changeClientButton = new() { Content = "Change Client", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _clientStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly TextBox _purchaseOrderBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _purchaseOrderSaveButton = new() { Content = "Save Purchase Order", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _purchaseOrderStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly TextBox _budgetBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize, Watermark = "amount currency" };
    private readonly Button _budgetSaveButton = new() { Content = "Save Budget", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _budgetStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly StackPanel _rateCardPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _changeRateCardButton = new() { Content = "Pin Rate Card", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _rateCardStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly DatePicker _startDate = new() { MinHeight = DesignTokens.MinControlSize };
    private readonly DatePicker _targetDate = new() { MinHeight = DesignTokens.MinControlSize };
    private readonly Button _datesSaveButton = new() { Content = "Save Dates", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _datesStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly TextBox _projectManagerBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _useMeButton = new() { Content = "Use Me", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _projectManagerSaveButton = new() { Content = "Save Project Manager", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _projectManagerStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Commercial";

    public bool AppliesTo(IEngineeringObject? subject) =>
        _ctx?.Declarations?.For(_ctx.ObjectKind) is { } declaration && declaration.HasSection(EditorSectionKeys.Commercial) && subject is IProject;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var body = new StackPanel { Spacing = DesignTokens.SpaceMd };

        var clientGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        clientGroup.Children.Add(new TextBlock { Text = "Client", Opacity = 0.8, FontSize = DesignTokens.FontSizeBody });
        clientGroup.Children.Add(_clientPanel);
        clientGroup.Children.Add(_changeClientButton);
        clientGroup.Children.Add(_clientStatus);
        body.Children.Add(clientGroup);

        var poGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        poGroup.Children.Add(EditorSectionHelpers.LabeledRow("Purchase Order Reference", _purchaseOrderBox));
        poGroup.Children.Add(_purchaseOrderSaveButton);
        poGroup.Children.Add(_purchaseOrderStatus);
        body.Children.Add(poGroup);

        var budgetGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        budgetGroup.Children.Add(EditorSectionHelpers.LabeledRow("Budget", _budgetBox));
        budgetGroup.Children.Add(_budgetSaveButton);
        budgetGroup.Children.Add(_budgetStatus);
        body.Children.Add(budgetGroup);

        var rateCardGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        rateCardGroup.Children.Add(new TextBlock { Text = "Rate Card", Opacity = 0.8, FontSize = DesignTokens.FontSizeBody });
        rateCardGroup.Children.Add(_rateCardPanel);
        rateCardGroup.Children.Add(_changeRateCardButton);
        rateCardGroup.Children.Add(_rateCardStatus);
        body.Children.Add(rateCardGroup);

        var datesGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        datesGroup.Children.Add(EditorSectionHelpers.LabeledRow("Start Date", _startDate));
        datesGroup.Children.Add(EditorSectionHelpers.LabeledRow("Target Date", _targetDate));
        datesGroup.Children.Add(_datesSaveButton);
        datesGroup.Children.Add(_datesStatus);
        body.Children.Add(datesGroup);

        var pmGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        pmGroup.Children.Add(EditorSectionHelpers.LabeledRow("Project Manager", _projectManagerBox));
        var pmButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
        pmButtons.Children.Add(_useMeButton);
        pmButtons.Children.Add(_projectManagerSaveButton);
        pmGroup.Children.Add(pmButtons);
        pmGroup.Children.Add(_projectManagerStatus);
        body.Children.Add(pmGroup);

        _changeClientButton.Click += async (_, _) => await OnChangeClientAsync().ConfigureAwait(true);
        _changeRateCardButton.Click += async (_, _) => await OnChangeRateCardAsync().ConfigureAwait(true);
        _purchaseOrderSaveButton.Click += async (_, _) => await OnSavePurchaseOrderAsync().ConfigureAwait(true);
        _budgetSaveButton.Click += async (_, _) => await OnSaveBudgetAsync().ConfigureAwait(true);
        _datesSaveButton.Click += async (_, _) => await OnSaveDatesAsync().ConfigureAwait(true);
        _useMeButton.Click += (_, _) => _projectManagerBox.Text = _ctx.CommercialSupport?.CurrentPrincipalIdentityId() ?? _projectManagerBox.Text;
        _projectManagerSaveButton.Click += async (_, _) => await OnSaveProjectManagerAsync().ConfigureAwait(true);

        _expander = EditorSectionHelpers.BuildSection(Title, body);
        _expander.IsVisible = false;
        return _expander;
    }

    public async Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return;

        var project = (IProject)subject!;

        // `WP 19.1A-R1` disclosure #4, wired by `WP 19.2B`: the client
        // shows the organisation's own name (falling back to the bare id
        // when unresolved) and the rate card shows the card's own code and
        // name alongside the pinned revision, rather than the bare id or
        // `ReferencePin.ToString()`. Resolved asynchronously, never
        // blocking — this whole method is already awaited end-to-end.
        _clientPanel.Children.Clear();
        var clientName = project.ClientOrganisationId is { } clientId ? await ResolveClientNameAsync(clientId).ConfigureAwait(true) : null;
        _clientPanel.Children.Add(new TextBlock
        {
            Text = clientName ?? project.ClientOrganisationId ?? "(no client set)",
            Opacity = project.ClientOrganisationId is null ? 0.5 : 1.0,
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });
        _changeClientButton.IsVisible = _ctx.CommercialSupport is not null;
        _clientStatus.Text = string.Empty;

        _purchaseOrderBox.Text = project.PurchaseOrderReference ?? string.Empty;
        _purchaseOrderStatus.Text = string.Empty;

        _budgetBox.Text = project.Budget?.ToString() ?? string.Empty;
        _budgetStatus.Text = string.Empty;

        _rateCardPanel.Children.Clear();
        var rateCardDisplay = project.RateCardPin is { } pin ? await ResolveRateCardDisplayAsync(pin).ConfigureAwait(true) : null;
        _rateCardPanel.Children.Add(new TextBlock
        {
            Text = rateCardDisplay ?? (project.RateCardPin is { } unresolvedPin ? unresolvedPin.ToString() : "(no rate card pinned)"),
            Opacity = project.RateCardPin is null ? 0.5 : 1.0,
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });
        _changeRateCardButton.IsVisible = _ctx.CommercialSupport is not null;
        _rateCardStatus.Text = string.Empty;

        _startDate.SelectedDate = project.StartDate is { } start ? new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue)) : null;
        _targetDate.SelectedDate = project.TargetDate is { } targetDate ? new DateTimeOffset(targetDate.ToDateTime(TimeOnly.MinValue)) : null;
        _datesStatus.Text = string.Empty;

        _projectManagerBox.Text = project.ProjectManagerIdentityId ?? string.Empty;
        _useMeButton.IsVisible = _ctx.CommercialSupport is not null;
        _projectManagerStatus.Text = string.Empty;
    }

    /// <summary>The client organisation's own name, or <see langword="null"/> when no resolver is wired or the id does not resolve.</summary>
    private async Task<string?> ResolveClientNameAsync(string clientOrganisationId)
    {
        if (_ctx.CommercialSupport?.ResolveClientNameAsync is not { } resolve)
            return null;

        var name = await resolve(clientOrganisationId, CancellationToken.None).ConfigureAwait(true);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>The rate card's own code, name and pinned revision, formatted for display — or <see langword="null"/> when no resolver is wired or the pin does not resolve.</summary>
    private async Task<string?> ResolveRateCardDisplayAsync(ReferencePin pin)
    {
        if (_ctx.CommercialSupport?.ResolveRateCardAsync is not { } resolve)
            return null;

        var resolved = await resolve(pin, CancellationToken.None).ConfigureAwait(true);
        return resolved is { } card ? $"{card.Code} — {card.Name} (rev {pin.RevisionNumber})" : null;
    }

    private async Task OnChangeClientAsync()
    {
        if (_ctx.CommercialSupport is null)
            return;

        var picked = await _ctx.CommercialSupport.PickClientOrganisationIdAsync(CancellationToken.None).ConfigureAwait(true);
        if (picked is null)
        {
            _clientStatus.Text = "Change client was cancelled.";
            return;
        }

        var organisationId = picked.Length == 0 ? null : picked;
        var result = await _ctx.CommandDispatcher
            .DispatchAsync(new SetProjectClientCommand(_ctx.ObjectId, _ctx.ObjectKind, organisationId), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_clientStatus, result).ConfigureAwait(true);
    }

    private async Task OnChangeRateCardAsync()
    {
        if (_ctx.CommercialSupport is null)
            return;

        var picked = await _ctx.CommercialSupport.PickRateCardIdAsync(CancellationToken.None).ConfigureAwait(true);
        if (picked is null)
        {
            _rateCardStatus.Text = "Pin rate card was cancelled.";
            return;
        }

        var result = await _ctx.CommandDispatcher
            .DispatchAsync(new PinProjectRateCardCommand(_ctx.ObjectId, _ctx.ObjectKind, picked), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_rateCardStatus, result).ConfigureAwait(true);
    }

    private async Task OnSavePurchaseOrderAsync()
    {
        var result = await _ctx.CommandDispatcher
            .DispatchAsync(new SetProjectPurchaseOrderCommand(_ctx.ObjectId, _ctx.ObjectKind, EditorSectionHelpers.NullIfEmpty(_purchaseOrderBox.Text)), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_purchaseOrderStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveBudgetAsync()
    {
        var text = _budgetBox.Text?.Trim();
        Money? budget = null;

        if (!string.IsNullOrWhiteSpace(text))
        {
            if (!EditorSectionHelpers.TryParseMoney(text, out var parsed))
            {
                _budgetStatus.Text = "Budget must be \"<amount> <currency>\" (e.g. \"50000 GBP\"), or blank to clear.";
                return;
            }

            budget = parsed;
        }

        var result = await _ctx.CommandDispatcher
            .DispatchAsync(new SetProjectBudgetCommand(_ctx.ObjectId, _ctx.ObjectKind, budget), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_budgetStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveDatesAsync()
    {
        var startDate = _startDate.SelectedDate is { } start ? DateOnly.FromDateTime(start.Date) : (DateOnly?)null;
        var targetDate = _targetDate.SelectedDate is { } target ? DateOnly.FromDateTime(target.Date) : (DateOnly?)null;

        var result = await _ctx.CommandDispatcher
            .DispatchAsync(new SetProjectDatesCommand(_ctx.ObjectId, _ctx.ObjectKind, startDate, targetDate), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_datesStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveProjectManagerAsync()
    {
        var result = await _ctx.CommandDispatcher
            .DispatchAsync(new SetProjectManagerCommand(_ctx.ObjectId, _ctx.ObjectKind, EditorSectionHelpers.NullIfEmpty(_projectManagerBox.Text)), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportAsync(_projectManagerStatus, result).ConfigureAwait(true);
    }

    /// <summary>Reports a Commercial field's own write outcome — mirrors <see cref="BillOfMaterialsSection"/>'s own "refresh before the message survives" discipline.</summary>
    private async Task ReportAsync(TextBlock statusMessage, CommandResult result)
    {
        var message = result.Succeeded ? "Saved." : result.Message ?? "Save failed.";
        if (result.Succeeded)
            await _ctx.RefreshAsync().ConfigureAwait(true);
        statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.From(result.Succeeded));
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
