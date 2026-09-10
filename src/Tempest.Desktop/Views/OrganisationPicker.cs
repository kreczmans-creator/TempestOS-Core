using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Project editor's own client picker (`WP 19.0A`, `ADR-0150`): lists
/// every registered organisation, with a text filter, and an inline
/// <b>Add organisation</b> row (name, reference) that registers a new
/// record on the spot — a project's own client, unlike a rate card, needs
/// no governance gate before it can be named
/// (<c>IProjectCommercialService.SetClientAsync</c>'s own remarks: "a
/// record id in the Organisation catalogue, never validated as a
/// structure"). Initially hidden, shares the Dialog Framework's own
/// established panel styling and real modal behaviour (mirrors
/// <see cref="SubjectPicker"/>).
/// </summary>
public sealed class OrganisationPicker : Border
{
    private readonly IOrganisationCatalog _organisations;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _filter = new() { Watermark = "Filter…", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceSm) };
    private readonly ListBox _list = new() { MaxHeight = 220 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0) };
    private readonly Button _chooseButton = new() { Content = "Choose", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _clearButton = new() { Content = "Clear", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private readonly TextBox _newReference = new() { Watermark = "Reference", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0) };
    private readonly TextBox _newName = new() { Watermark = "New organisation name", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0) };
    private readonly Button _addButton = new() { Content = "Add organisation", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _addStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private IReadOnlyList<(string RecordId, string Name)> _candidates = [];
    private TaskCompletionSource<string?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="OrganisationPicker"/> class, initially hidden.</summary>
    public OrganisationPicker(IOrganisationCatalog organisations)
    {
        ArgumentNullException.ThrowIfNull(organisations);
        _organisations = organisations;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 460;
        MaxWidth = 580;
        MaxHeight = 560;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Choose a client organisation";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_clearButton);
        buttons.Children.Add(_chooseButton);

        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        addRow.Children.Add(_newReference);
        addRow.Children.Add(_newName);
        addRow.Children.Add(_addButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_filter);
        body.Children.Add(_list);
        body.Children.Add(_status);
        body.Children.Add(new Separator { Margin = new Thickness(0, DesignTokens.SpaceMd) });
        body.Children.Add(new TextBlock { Text = "Add organisation", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody });
        body.Children.Add(addRow);
        body.Children.Add(_addStatus);
        body.Children.Add(buttons);
        Child = body;

        _chooseButton.Classes.Add(ChromeStyles.Primary);
        _clearButton.Classes.Add(ChromeStyles.Subtle);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        _addButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_filter, "Filter…");
        AutomationProperties.SetName(_list, "Organisations");
        AutomationProperties.SetName(_chooseButton, "Choose");
        AutomationProperties.SetName(_clearButton, "Clear");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        AutomationProperties.SetName(_newReference, "Reference");
        AutomationProperties.SetName(_newName, "New organisation name");
        AutomationProperties.SetName(_addButton, "Add organisation");
        ToolTip.SetTip(_chooseButton, "Choose");
        ToolTip.SetTip(_clearButton, "Clear");
        ToolTip.SetTip(_cancelButton, "Cancel");
        ToolTip.SetTip(_addButton, "Add organisation");

        _filter.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) ApplyFilter(); };
        _list.DoubleTapped += (_, _) => TryComplete();
        _chooseButton.Click += (_, _) => TryComplete();
        _clearButton.Click += (_, _) => Complete(string.Empty);
        _cancelButton.Click += (_, _) => Complete(null);
        _addButton.Click += async (_, _) => await OnAddAsync().ConfigureAwait(true);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this picker, freshly reading every registered organisation,
    /// and returns the record id chosen, an empty string for "Clear"
    /// (removes the project's own client), or <see langword="null"/> if
    /// the user cancelled.
    /// </summary>
    public async Task<string?> PickAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _filter.Text = string.Empty;
        _newReference.Text = string.Empty;
        _newName.Text = string.Empty;
        _addStatus.Text = string.Empty;
        _status.Text = "Loading…";
        IsVisible = true;

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        _filter.Focus();

        _pending = new TaskCompletionSource<string?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var all = await _organisations.ListAsync(cancellationToken).ConfigureAwait(true);
        _candidates = [.. all.Select(r => (RecordId: r.Id, r.Definition.Name)).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)];
        _status.Text = _candidates.Count == 0 ? "No organisations registered yet — add one below." : string.Empty;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var text = _filter.Text ?? string.Empty;

        var matches = string.IsNullOrWhiteSpace(text)
            ? _candidates
            : [.. _candidates.Where(c => c.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || c.RecordId.Contains(text, StringComparison.OrdinalIgnoreCase))];

        _list.ItemsSource = matches.Select(c => new ListBoxItem { Content = $"{c.Name} ({c.RecordId})", Tag = c.RecordId }).ToList();
    }

    private async Task OnAddAsync()
    {
        var reference = _newReference.Text?.Trim();
        var name = _newName.Text?.Trim();

        if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(name))
        {
            _addStatus.Text = "Both a reference and a name are required.";
            return;
        }

        try
        {
            await _organisations
                .RegisterAsync(reference, new Organisation { Reference = reference, Name = name }, ReferenceProvenance.Unknown, CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is DuplicateReferenceRecordException or ArgumentException)
        {
            _addStatus.Text = ex.Message;
            return;
        }

        await ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        _newReference.Text = string.Empty;
        _newName.Text = string.Empty;
        _addStatus.Text = $"Added '{name}'.";
    }

    private void TryComplete()
    {
        if (_list.SelectedItem is ListBoxItem { Tag: string recordId })
            Complete(recordId);
    }

    private void Complete(string? recordId)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(recordId);
    }
}
