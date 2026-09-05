using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Dialog Framework's own single-field text input overlay
/// (`WP 10.5B` scope: "Create Object, Rename, Duplicate" — every one of
/// these ultimately just needs one name/text value from the user).
/// Initially hidden, shares the identical panel styling
/// (<see cref="ApplicationPalette.PanelBackgroundBrushKey"/>/
/// <see cref="ApplicationPalette.PanelBorderBrushKey"/>,
/// <see cref="DesignTokens.DialogCornerRadius"/>/<see cref="DesignTokens.DialogPadding"/>)
/// <see cref="ConfirmationDialog"/> (`WP 10.5A`) already established —
/// "a common framework with consistent styling," realised by every
/// dialog sharing the same tokens and resource keys, not a shared base
/// class (each dialog's own layout is genuinely different enough — one
/// text field vs. a title/message/two-buttons — that forcing a common
/// base class would cost more than it would save; see `WP10.5B
/// Architecture Review.md` §2).
/// </summary>
public sealed class InputDialog : Border
{
    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBlock _label = new() { FontSize = DesignTokens.FontSizeBody, Opacity = 0.8, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceXs) };
    private readonly TextBox _input = new() { MinHeight = DesignTokens.ControlSizeMedium };
    // A real severity row (glyph + colour, `ObjectEditorView.BuildSeverityRow`
    // — the same reusable row `PropertyInspectorView`'s own Validation
    // section already shares), not a bare-coloured string: colour alone is
    // never this platform's feedback vocabulary.
    private readonly ContentControl _validationSlot = new() { Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };
    private readonly Button _okButton = new() { Content = "OK", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<string?>? _pending;
    private Func<string, string?>? _validate;

    /// <summary>Initialises a new instance of the <see cref="InputDialog"/> class, initially hidden.</summary>
    public InputDialog()
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
        body.Children.Add(_input);
        body.Children.Add(_validationSlot);
        body.Children.Add(buttons);
        Child = body;

        _okButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        _title.FontFamily = DesignTokens.TitleFont;
        _title.FontSize = DesignTokens.FontSizeTitle;
        _cancelButton.Click += (_, _) => Complete(null);
        _okButton.Click += (_, _) => TryComplete();
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                TryComplete();
            else if (e.Key == Key.Escape)
                Complete(null);
        };

        // Real modal behaviour (`WP 16.5A`, `TD-65`) — see
        // `DialogModality`'s own remarks.
        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this dialog, returning the entered text if the user confirms
    /// (never null/blank — validated), or <see langword="null"/> if they
    /// cancel. <paramref name="validate"/>, if given, returns a non-null
    /// error message for an invalid value (shown inline, OK stays
    /// clickable — re-validated on every attempt, never silently
    /// blocked); only one confirmation may be pending at a time, mirroring
    /// <see cref="ConfirmationDialog.ConfirmAsync"/>'s own identical
    /// "a second call cancels the first" discipline.
    /// </summary>
    public Task<string?> PromptAsync(string title, string label, string initialValue = "", Func<string, string?>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(initialValue);

        _pending?.TrySetResult(null);

        _title.Text = title;
        _label.Text = label;
        _input.Text = initialValue;
        _validate = validate;
        _validationSlot.IsVisible = false;
        _validationSlot.Content = null;
        IsVisible = true;
        _input.Focus();
        _input.SelectAll();

        _pending = new TaskCompletionSource<string?>();
        return _pending.Task;
    }

    private void TryComplete()
    {
        var value = _input.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            ShowValidationError("A value is required.");
            return;
        }

        var error = _validate?.Invoke(value);
        if (error is not null)
        {
            ShowValidationError(error);
            return;
        }

        Complete(value);
    }

    private void ShowValidationError(string message)
    {
        _validationSlot.Content = ObjectEditorView.BuildSeverityRow(FeedbackSeverity.Error, message);
        _validationSlot.IsVisible = true;
    }

    private void Complete(string? result)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(result);
    }
}
