using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>What <see cref="NewProjectPrompt.PromptAsync"/> collects: the project's own name, and whether to open a quotation with it.</summary>
/// <param name="Name">The project's own name.</param>
/// <param name="OpenQuotation">Whether to create and open a Draft quotation with the project (`WP 19.5B`, `ADR-0152`).</param>
public sealed record NewProjectPromptResult(string Name, bool OpenQuotation);

/// <summary>
/// The New Project prompt (`WP 19.5B`, `ADR-0152`, Product Owner comment
/// item 4: "a quote is opened with the project") — the project's own name,
/// plus a checked-by-default "Open a quotation for this project" option.
/// </summary>
/// <remarks>
/// A dedicated dialog rather than an extension of <see cref="InputDialog"/>
/// (`WP 10.5B` scope's own single-field text input, reused by roughly
/// seventy Create/Rename/Duplicate prompts across this platform): widening
/// its shared return shape to carry a second, optional value would touch
/// every one of those call sites for a feature only this one needs.
/// Initially hidden, shares the Dialog Framework's own established panel
/// styling and real modal behaviour (mirrors <see cref="InputDialog"/>).
/// </remarks>
public sealed class NewProjectPrompt : Border
{
    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBlock _label = new() { FontSize = DesignTokens.FontSizeBody, Opacity = 0.8, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceXs) };
    private readonly TextBox _nameBox = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly CheckBox _openQuotationBox = new() { Content = "Open a quotation for this project", IsChecked = true, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ContentControl _validationSlot = new() { Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };
    private readonly Button _okButton = new() { Content = "OK", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<NewProjectPromptResult?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="NewProjectPrompt"/> class, initially hidden.</summary>
    public NewProjectPrompt()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 360;
        MaxWidth = 440;
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
        body.Children.Add(_openQuotationBox);
        body.Children.Add(_validationSlot);
        body.Children.Add(buttons);
        Child = body;

        _okButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_okButton, "OK");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        AutomationProperties.SetName(_nameBox, "Name");
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

        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this dialog, returning the name and the "open a quotation"
    /// choice if the user confirms, or <see langword="null"/> if they
    /// cancel.
    /// </summary>
    public Task<NewProjectPromptResult?> PromptAsync(string title, string label)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(label);

        _pending?.TrySetResult(null);

        _title.Text = title;
        _label.Text = label;
        _nameBox.Text = string.Empty;
        _openQuotationBox.IsChecked = true;
        _validationSlot.IsVisible = false;
        _validationSlot.Content = null;
        IsVisible = true;
        _nameBox.Focus();

        _pending = new TaskCompletionSource<NewProjectPromptResult?>();
        return _pending.Task;
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

        Complete(new NewProjectPromptResult(value, _openQuotationBox.IsChecked ?? false));
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
}
