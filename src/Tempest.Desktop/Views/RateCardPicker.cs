using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Project editor's own rate-card picker (`WP 19.0A`, `ADR-0150`):
/// lists <b>Released cards only</b>, with a text filter — an unreleased
/// card is never offered here at all, which is the surface half of the
/// refusal <c>PinProjectRateCardCommand</c>'s own handler already enforces
/// the substrate half of, mirroring <see cref="CitationPicker"/>'s own
/// identical "released records only" shape and reason. Initially hidden,
/// shares the Dialog Framework's own established panel styling and real
/// modal behaviour.
/// </summary>
public sealed class RateCardPicker : Border
{
    private readonly IRateCardCatalog _rateCards;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _filter = new() { Watermark = "Filter…", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceSm) };
    private readonly ListBox _list = new() { MaxHeight = 320 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0) };
    private readonly Button _chooseButton = new() { Content = "Pin", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private IReadOnlyList<(string RecordId, string Description)> _released = [];
    private TaskCompletionSource<string?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="RateCardPicker"/> class, initially hidden.</summary>
    public RateCardPicker(IRateCardCatalog rateCards)
    {
        ArgumentNullException.ThrowIfNull(rateCards);
        _rateCards = rateCards;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 480;
        MaxWidth = 620;
        MaxHeight = 520;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Pin a Released rate card";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_chooseButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_filter);
        body.Children.Add(_list);
        body.Children.Add(_status);
        body.Children.Add(buttons);
        Child = body;

        _chooseButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_filter, "Filter…");
        AutomationProperties.SetName(_list, "Released rate cards");
        AutomationProperties.SetName(_chooseButton, "Pin");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        ToolTip.SetTip(_chooseButton, "Pin");
        ToolTip.SetTip(_cancelButton, "Cancel");

        _filter.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) ApplyFilter(); };
        _list.DoubleTapped += (_, _) => TryComplete();
        _chooseButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this picker, freshly reading every Released rate card, and
    /// returns the record id chosen, or <see langword="null"/> if the user
    /// cancelled.
    /// </summary>
    public async Task<string?> PickAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _filter.Text = string.Empty;
        _status.Text = "Loading…";
        IsVisible = true;

        var all = await _rateCards.ListAsync(cancellationToken).ConfigureAwait(true);
        _released =
        [
            .. all
                .Where(r => r.ValidationState == ReferenceValidationState.Released)
                .Select(r => (RecordId: r.Id, Description: $"{r.Definition.Name} — {r.Definition.Currency} — {r.Definition.EffectivePeriod}"))
                .OrderBy(c => c.RecordId, StringComparer.OrdinalIgnoreCase),
        ];
        _status.Text = _released.Count == 0 ? "No Released rate cards are available to pin." : string.Empty;
        ApplyFilter();
        _filter.Focus();

        _pending = new TaskCompletionSource<string?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private void ApplyFilter()
    {
        var text = _filter.Text ?? string.Empty;

        var matches = string.IsNullOrWhiteSpace(text)
            ? _released
            : [.. _released.Where(r => r.RecordId.Contains(text, StringComparison.OrdinalIgnoreCase) || r.Description.Contains(text, StringComparison.OrdinalIgnoreCase))];

        _list.ItemsSource = matches.Select(r => new ListBoxItem { Content = $"{r.RecordId} — {r.Description}", Tag = r.RecordId }).ToList();
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
