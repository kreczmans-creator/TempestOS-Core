using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Dialog Framework's own single-button information/warning/error
/// overlay (`WP 10.5B` scope: "Warning dialogs, Error dialogs,
/// Information dialogs, About") — a severity glyph/colour
/// (<see cref="SeverityColors"/>, reused, not reinvented), a message,
/// and an optional collapsible details section (`WP 10.5B` scope:
/// "unexpected exceptions... recovery suggestions... diagnostics").
/// Initially hidden, shares <see cref="ConfirmationDialog"/>/
/// <see cref="InputDialog"/>'s own identical panel styling.
/// </summary>
public sealed class MessageDialog : Border
{
    private readonly TextBlock _icon = new() { FontSize = DesignTokens.IconSizeSmall, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0) };
    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBlock _message = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly Expander _details = new() { Header = "Details", IsVisible = false, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };
    private readonly TextBlock _detailsText = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8 };
    private readonly Button _okButton = new() { Content = "OK", MinHeight = DesignTokens.ControlSizeMedium, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };

    private TaskCompletionSource<bool>? _pending;

    /// <summary>Initialises a new instance of the <see cref="MessageDialog"/> class, initially hidden.</summary>
    public MessageDialog()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 360;
        MaxWidth = 480;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _details.Content = _detailsText;

        var headerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(_icon, 0);
        Grid.SetColumn(_title, 1);
        headerRow.Children.Add(_icon);
        headerRow.Children.Add(_title);

        var body = new StackPanel();
        body.Children.Add(headerRow);
        body.Children.Add(_message);
        body.Children.Add(_details);
        body.Children.Add(_okButton);
        Child = body;

        _okButton.Classes.Add(ChromeStyles.Primary);
        _title.FontFamily = DesignTokens.TitleFont;
        _title.FontSize = DesignTokens.FontSizeTitle;
        _okButton.Click += (_, _) => Complete();
    }

    /// <summary>Shows this dialog with <paramref name="severity"/>'s own glyph/colour, returning once the user acknowledges it (OK). Never blocks indefinitely — always resolves on a real user click.</summary>
    public Task ShowAsync(FeedbackSeverity severity, string title, string message, string? details = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        _pending?.TrySetResult(true);

        _icon.Text = SeverityColors.Glyph(severity);
        _icon.Foreground = SeverityColors.Resolve(severity);
        _title.Text = title;
        _message.Text = message;

        _details.IsVisible = details is not null;
        _detailsText.Text = details ?? string.Empty;

        IsVisible = true;

        _pending = new TaskCompletionSource<bool>();
        return _pending.Task;
    }

    private void Complete()
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(true);
    }
}
