using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.Evidence;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>Collects one check — the checker's name and organisation, their statement, and the outcome (`WP 18.2B`, §1).</summary>
/// <param name="CheckerName">The checker's name, as typed.</param>
/// <param name="CheckerOrganisation">The checker's organisation, as typed.</param>
/// <param name="Statement">The checker's own statement, verbatim.</param>
/// <param name="Outcome">What the checker concluded.</param>
public sealed record CheckEntryInput(string CheckerName, string CheckerOrganisation, string Statement, CheckOutcome Outcome);

/// <summary>
/// The Evidence workspace's own check entry dialog (`WP 18.2B`, §1):
/// checker name, checker organisation, a multiline statement and the
/// outcome — the client's own review, entered by hand, unless the
/// independent-check rule is on, in which case the acting principal (read
/// server-side, never asked here) stands as the checker. Initially
/// hidden, shares the Dialog Framework's own established panel styling
/// and real modal behaviour (mirrors <see cref="DeclaredFigureEntry"/>).
/// </summary>
public sealed class CheckEntry : Border
{
    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _checkerName = new() { Watermark = "Checker name", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _checkerOrganisation = new() { Watermark = "Checker organisation", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _statement = new() { Watermark = "Statement", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 90, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ComboBox _outcome = new() { MinHeight = DesignTokens.ControlSizeMedium, ItemsSource = Enum.GetValues<CheckOutcome>(), Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _validation = new() { FontSize = DesignTokens.FontSizeCaption, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };
    private readonly Button _checkButton = new() { Content = "Check", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<CheckEntryInput?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="CheckEntry"/> class, initially hidden.</summary>
    public CheckEntry()
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

        _title.Text = "Record a check";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_checkButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_checkerName);
        body.Children.Add(_checkerOrganisation);
        body.Children.Add(_statement);
        body.Children.Add(_outcome);
        body.Children.Add(_validation);
        body.Children.Add(buttons);
        Child = body;

        _checkButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);

        _checkButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>Shows this dialog and returns the check entered, or <see langword="null"/> if the user cancelled.</summary>
    public Task<CheckEntryInput?> PromptAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _checkerName.Text = string.Empty;
        _checkerOrganisation.Text = string.Empty;
        _statement.Text = string.Empty;
        _outcome.SelectedItem = CheckOutcome.Accepted;
        _validation.IsVisible = false;
        IsVisible = true;
        _checkerName.Focus();

        _pending = new TaskCompletionSource<CheckEntryInput?>();
        return _pending.Task;
    }

    private void TryComplete()
    {
        var checkerName = _checkerName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(checkerName))
        {
            ShowValidationError("A checker name is required.");
            return;
        }

        var checkerOrganisation = _checkerOrganisation.Text?.Trim();
        if (string.IsNullOrWhiteSpace(checkerOrganisation))
        {
            ShowValidationError("A checker organisation is required.");
            return;
        }

        var statement = _statement.Text;
        if (string.IsNullOrWhiteSpace(statement))
        {
            ShowValidationError("A statement is required.");
            return;
        }

        if (_outcome.SelectedItem is not CheckOutcome outcome)
        {
            ShowValidationError("An outcome is required.");
            return;
        }

        Complete(new CheckEntryInput(checkerName, checkerOrganisation, statement, outcome));
    }

    private void ShowValidationError(string message)
    {
        _validation.Text = message;
        _validation.IsVisible = true;
    }

    private void Complete(CheckEntryInput? input)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(input);
    }
}
