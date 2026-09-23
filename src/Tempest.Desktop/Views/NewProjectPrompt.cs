using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>What <see cref="NewProjectPrompt.PromptAsync"/> collects: the project's own name, its commercial identity, and whether to open a quotation with it.</summary>
/// <param name="Name">The project's own name.</param>
/// <param name="OpenQuotation">Whether to create and open a Draft quotation with the project (`WP 19.5B`, `ADR-0152`).</param>
/// <param name="ClientOrganisationId">The client's own record id in the Organisation catalogue (`WP 20.10A`, Product Owner finding D2). <see langword="null"/> when no client was chosen.</param>
/// <param name="RateCardId">The Released rate card's own record id to pin (`WP 20.10A`, Product Owner finding D12). <see langword="null"/> for "None" — the project is created with no pin, and cannot have time recorded against it until one is pinned.</param>
/// <param name="PurchaseOrderReference">The client's own purchase-order reference. <see langword="null"/> when left blank.</param>
public sealed record NewProjectPromptResult(
    string Name, bool OpenQuotation, string? ClientOrganisationId, string? RateCardId, string? PurchaseOrderReference);

/// <summary>
/// The New Project prompt (`WP 19.5B`, `ADR-0152`, Product Owner comment
/// item 4: "a quote is opened with the project"; `WP 20.10A`, Product Owner
/// findings D1/D2/D12: "Lets add a 'New Project' button on the home page",
/// "Need the ability to assign projects to a customer — 'existing customer'
/// becomes a pick from list or an 'add new customer'", and "project drop
/// down doesnt populate [because] doesn't allow recording of time at all")
/// — the project's own name, its client, its rate card, an optional
/// purchase-order reference, plus a checked-by-default "Open a quotation
/// for this project" option.
/// </summary>
/// <remarks>
/// <para>
/// A dedicated dialog rather than an extension of <see cref="InputDialog"/>
/// (`WP 10.5B` scope's own single-field text input, reused by roughly
/// seventy Create/Rename/Duplicate prompts across this platform): widening
/// its shared return shape to carry these values would touch every one of
/// those call sites for a feature only this one needs. Initially hidden,
/// shares the Dialog Framework's own established panel styling and real
/// modal behaviour (mirrors <see cref="InputDialog"/>).
/// </para>
/// <para>
/// <b>Client (`WP 20.10A`, D2).</b> A drop-down of every registered
/// organisation, plus a trailing "Add organisation…" entry that opens the
/// real <see cref="OrganisationPicker"/> — reused exactly as
/// <see cref="Editors.ObjectEditorView"/>'s own Commercial section reuses
/// it, never a second organisation form. Picking a record there (existing,
/// or freshly added) selects it here; cancelling or clearing leaves no
/// client chosen, which is a valid outcome — a project may be created with
/// no client and one set later, from the Details tab.
/// </para>
/// <para>
/// <b>Rate card (`WP 20.10A`, D12).</b> A drop-down of Released rate cards
/// only — an unreleased card is never offered, mirroring
/// <see cref="RateCardPicker"/>'s own identical "released records only"
/// shape — defaulting to the most recently released one (by
/// <c>EffectivePeriod.From</c>, descending). "None" is always selectable;
/// choosing it states the consequence inline, since
/// <see cref="TimesheetEntryPrompt"/>'s own Project drop-down lists every
/// open project regardless of pin (`WP 20.10A`, D12) but cannot resolve a
/// rate for one with none pinned (`ADR-0150`).
/// </para>
/// <para>
/// <b>Creation writes each field through the identical
/// <c>Tempest.Core.Projects.IProjectCommercialService</c> acts
/// <see cref="Editors.ObjectEditorView"/>'s own Commercial section already
/// dispatches through</b> — <c>MainWindow.PromptForNewProjectAsync</c>'s own
/// remarks — one transaction each, exactly as the editor does. This prompt
/// only collects the values; it performs no write itself.
/// </para>
/// </remarks>
public sealed class NewProjectPrompt : Border
{
    /// <summary>The client drop-down's own trailing sentinel — a unique reference, never a record id a catalogue could coincidentally register.</summary>
    private static readonly object AddOrganisationTag = new();

    private readonly IOrganisationCatalog _organisations;
    private readonly IRateCardCatalog _rateCards;
    private readonly OrganisationPicker _organisationPicker;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBlock _label = new() { FontSize = DesignTokens.FontSizeBody, Opacity = 0.8, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceXs) };
    private readonly TextBox _nameBox = new() { MinHeight = DesignTokens.ControlSizeMedium };

    private readonly TextBlock _clientLabel = FieldLabel("Client");
    private readonly ComboBox _client = new() { MinHeight = DesignTokens.ControlSizeMedium, HorizontalAlignment = HorizontalAlignment.Stretch };

    private readonly TextBlock _rateCardLabel = FieldLabel("Rate card");
    private readonly ComboBox _rateCard = new() { MinHeight = DesignTokens.ControlSizeMedium, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ContentControl _rateCardConsequence = new() { Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };

    private readonly TextBlock _purchaseOrderLabel = FieldLabel("PO reference");
    private readonly TextBox _purchaseOrderBox = new() { MinHeight = DesignTokens.ControlSizeMedium, Watermark = "Optional" };

    private readonly CheckBox _openQuotationBox = new() { Content = "Open a quotation for this project", IsChecked = true, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ContentControl _validationSlot = new() { Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };
    private readonly Button _okButton = new() { Content = "OK", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private bool _suppressClientSelection;
    private TaskCompletionSource<NewProjectPromptResult?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="NewProjectPrompt"/> class, initially hidden.</summary>
    public NewProjectPrompt(IOrganisationCatalog organisations, IRateCardCatalog rateCards, OrganisationPicker organisationPicker)
    {
        ArgumentNullException.ThrowIfNull(organisations);
        ArgumentNullException.ThrowIfNull(rateCards);
        ArgumentNullException.ThrowIfNull(organisationPicker);
        _organisations = organisations;
        _rateCards = rateCards;
        _organisationPicker = organisationPicker;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 420;
        MaxWidth = 520;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_okButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_label);
        body.Children.Add(_nameBox);
        body.Children.Add(_clientLabel);
        body.Children.Add(_client);
        body.Children.Add(_rateCardLabel);
        body.Children.Add(_rateCard);
        body.Children.Add(_rateCardConsequence);
        body.Children.Add(_purchaseOrderLabel);
        body.Children.Add(_purchaseOrderBox);
        body.Children.Add(_openQuotationBox);
        body.Children.Add(_validationSlot);
        body.Children.Add(buttons);
        Child = body;

        _okButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_okButton, "OK");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        AutomationProperties.SetName(_nameBox, "Name");
        AutomationProperties.SetName(_client, "Client");
        AutomationProperties.SetName(_rateCard, "Rate card");
        AutomationProperties.SetName(_purchaseOrderBox, "PO reference");
        AutomationProperties.SetName(_openQuotationBox, "Open a quotation for this project");
        ToolTip.SetTip(_okButton, "OK");
        ToolTip.SetTip(_cancelButton, "Cancel");
        _title.FontFamily = DesignTokens.TitleFont;
        _title.FontSize = DesignTokens.FontSizeTitle;
        _cancelButton.Click += (_, _) => Complete(null);
        _okButton.Click += (_, _) => TryComplete();
        _nameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                TryComplete();
            else if (e.Key == Key.Escape)
                Complete(null);
        };
        _client.SelectionChanged += async (_, _) => await OnClientSelectionChangedAsync().ConfigureAwait(true);
        _rateCard.SelectionChanged += (_, _) => UpdateRateCardConsequence();

        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this dialog, returning the collected values if the user
    /// confirms, or <see langword="null"/> if they cancel.
    /// </summary>
    public async Task<NewProjectPromptResult?> PromptAsync(string title, string label)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(label);

        _pending?.TrySetResult(null);

        _title.Text = title;
        _label.Text = label;
        _nameBox.Text = string.Empty;
        _purchaseOrderBox.Text = string.Empty;
        _openQuotationBox.IsChecked = true;
        _validationSlot.IsVisible = false;
        _validationSlot.Content = null;

        await ReloadOrganisationsAsync(selectRecordId: null).ConfigureAwait(true);
        await ReloadRateCardsAsync().ConfigureAwait(true);

        IsVisible = true;
        _nameBox.Focus();

        _pending = new TaskCompletionSource<NewProjectPromptResult?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    /// <summary>Re-reads every registered organisation into the Client drop-down, selecting <paramref name="selectRecordId"/> when given.</summary>
    private async Task ReloadOrganisationsAsync(string? selectRecordId)
    {
        var all = await _organisations.ListAsync().ConfigureAwait(true);

        _suppressClientSelection = true;
        try
        {
            var items = all
                .OrderBy(r => r.Definition.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => new ComboBoxItem { Content = $"{r.Definition.Name} ({r.Id})", Tag = r.Id })
                .ToList();
            items.Add(new ComboBoxItem { Content = "Add organisation…", Tag = AddOrganisationTag });

            _client.ItemsSource = items;
            _client.SelectedItem = selectRecordId is null
                ? null
                : items.FirstOrDefault(i => i is ComboBoxItem { Tag: string id } && id == selectRecordId);
        }
        finally
        {
            _suppressClientSelection = false;
        }
    }

    /// <summary>Re-reads every Released rate card into the Rate card drop-down, defaulting to the most recently released one — "None" if there are none.</summary>
    private async Task ReloadRateCardsAsync()
    {
        var all = await _rateCards.ListAsync().ConfigureAwait(true);
        var released = all
            .Where(r => r.ValidationState == ReferenceValidationState.Released)
            .OrderByDescending(r => r.Definition.EffectivePeriod.From)
            .ToList();

        var items = new List<ComboBoxItem> { new() { Content = "None", Tag = null } };
        items.AddRange(released.Select(r => new ComboBoxItem
        {
            Content = $"{r.Id} — {r.Definition.Name} — {r.Definition.Currency} — {r.Definition.EffectivePeriod}",
            Tag = r.Id,
        }));

        _rateCard.ItemsSource = items;
        _rateCard.SelectedIndex = released.Count > 0 ? 1 : 0;
        UpdateRateCardConsequence();
    }

    /// <summary>Selecting "Add organisation…" opens the real <see cref="OrganisationPicker"/> in place — reused, never a second organisation form (`WP 20.10A`, D2).</summary>
    private async Task OnClientSelectionChangedAsync()
    {
        if (_suppressClientSelection)
            return;

        if (_client.SelectedItem is not ComboBoxItem { Tag: { } tag } || !ReferenceEquals(tag, AddOrganisationTag))
            return;

        var picked = await _organisationPicker.PickAsync(CancellationToken.None).ConfigureAwait(true);

        // `null` (cancelled) and `""` (Clear) both mean "no client chosen"
        // here — there is no prior selection on a project that does not
        // exist yet to revert to, and leaving the client unset is a valid
        // outcome (it can be set later, from the Details tab).
        await ReloadOrganisationsAsync(string.IsNullOrEmpty(picked) ? null : picked).ConfigureAwait(true);
    }

    private void UpdateRateCardConsequence()
    {
        var noneSelected = _rateCard.SelectedItem is not ComboBoxItem { Tag: string };
        if (noneSelected)
        {
            _rateCardConsequence.Content = ObjectEditorView.BuildSeverityRow(
                FeedbackSeverity.Warning, "Time cannot be recorded against this project until a rate card is pinned.");
        }

        _rateCardConsequence.IsVisible = noneSelected;
    }

    private void TryComplete()
    {
        var value = _nameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            ShowValidationError("A name is required.");
            return;
        }

        if (value.Length > 200)
        {
            ShowValidationError("Name is too long (200 characters max).");
            return;
        }

        // A sentinel still selected (the picker was cancelled mid-flight,
        // or Enter was pressed before the reload above completed) reads as
        // no client chosen — never as the sentinel's own literal text.
        var clientOrganisationId = _client.SelectedItem is ComboBoxItem { Tag: string clientId } ? clientId : null;
        var rateCardId = _rateCard.SelectedItem is ComboBoxItem { Tag: string cardId } ? cardId : null;
        var purchaseOrderReference = _purchaseOrderBox.Text?.Trim();
        if (string.IsNullOrEmpty(purchaseOrderReference))
            purchaseOrderReference = null;

        Complete(new NewProjectPromptResult(
            value, _openQuotationBox.IsChecked ?? false, clientOrganisationId, rateCardId, purchaseOrderReference));
    }

    private void ShowValidationError(string message)
    {
        _validationSlot.Content = ObjectEditorView.BuildSeverityRow(FeedbackSeverity.Error, message);
        _validationSlot.IsVisible = true;
    }

    private void Complete(NewProjectPromptResult? result)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(result);
    }

    private static TextBlock FieldLabel(string text) =>
        new() { Text = text, FontSize = DesignTokens.FontSizeBody, Opacity = 0.8, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceXs) };
}
