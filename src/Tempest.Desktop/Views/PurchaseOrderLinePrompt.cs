using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.BusinessGovernance;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>Add Purchase Order Line's own collected values (`WP 21.3B`).</summary>
/// <param name="Description">What the line is.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="UnitPrice">The price of one unit.</param>
/// <param name="VatRate">This line's own VAT treatment.</param>
public sealed record PurchaseOrderLineInput(string Description, decimal Quantity, Money UnitPrice, VatRate VatRate);

/// <summary>
/// The Purchase orders area's own "Add line…" dialog (`WP 21.3B`):
/// description, quantity, unit price (in the order's own currency) and a
/// VAT rate. Initially hidden, shares the Dialog Framework's own
/// established panel styling and real modal behaviour, mirroring
/// <see cref="ExpenseEntryPrompt"/>'s own identical shape.
/// </summary>
public sealed class PurchaseOrderLinePrompt : Border
{
    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _description = new() { Watermark = "Description", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly NumericUpDown _quantity = new() { Minimum = 0.01m, Increment = 1m, Value = 1m, MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly NumericUpDown _unitPrice = new() { Minimum = 0m, Increment = 1m, MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ComboBox _vatRate = new() { MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _validation = new() { FontSize = DesignTokens.FontSizeCaption, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };

    private readonly Button _addButton = new() { Content = "Add", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private CurrencyCode _currency = CurrencyCode.Gbp;
    private TaskCompletionSource<PurchaseOrderLineInput?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrderLinePrompt"/> class, initially hidden.</summary>
    public PurchaseOrderLinePrompt()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 380;
        MaxWidth = 460;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Add purchase order line";

        foreach (var rate in Enum.GetValues<VatRate>())
            _vatRate.Items.Add(new ComboBoxItem { Content = rate.DisplayName(), Tag = rate });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_addButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_description);
        body.Children.Add(_quantity);
        body.Children.Add(_unitPrice);
        body.Children.Add(_vatRate);
        body.Children.Add(_validation);
        body.Children.Add(buttons);
        Child = body;

        _addButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_description, "Description");
        AutomationProperties.SetName(_quantity, "Quantity");
        AutomationProperties.SetName(_unitPrice, "Unit price");
        AutomationProperties.SetName(_vatRate, "VAT rate");
        AutomationProperties.SetName(_addButton, "Add");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        ToolTip.SetTip(_addButton, "Add");
        ToolTip.SetTip(_cancelButton, "Cancel");

        _addButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>Shows this dialog and returns the line entered, or <see langword="null"/> if the user cancelled.</summary>
    /// <param name="currency">The order's own currency — every unit price entered here is in this currency.</param>
    public Task<PurchaseOrderLineInput?> PromptAsync(CurrencyCode currency, CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _currency = currency;
        _title.Text = $"Add purchase order line ({currency})";
        _description.Text = string.Empty;
        _quantity.Value = 1m;
        _unitPrice.Value = 0m;
        _vatRate.SelectedIndex = 0; // OutOfScope — the model's own default, never assumed otherwise.
        _validation.IsVisible = false;

        IsVisible = true;

        _pending = new TaskCompletionSource<PurchaseOrderLineInput?>();
        return _pending.Task;
    }

    private void TryComplete()
    {
        var description = _description.Text?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            ShowValidationError("A description is required.");
            return;
        }

        if (_quantity.Value is not { } quantity || quantity <= 0m)
        {
            ShowValidationError("Quantity must be greater than zero.");
            return;
        }

        if (_vatRate.SelectedItem is not ComboBoxItem { Tag: VatRate vatRate })
        {
            ShowValidationError("A VAT rate is required.");
            return;
        }

        var unitPrice = new Money(_unitPrice.Value ?? 0m, _currency);

        Complete(new PurchaseOrderLineInput(description, quantity, unitPrice, vatRate));
    }

    private void ShowValidationError(string message)
    {
        _validation.Text = message;
        _validation.IsVisible = true;
    }

    private void Complete(PurchaseOrderLineInput? input)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(input);
    }
}
