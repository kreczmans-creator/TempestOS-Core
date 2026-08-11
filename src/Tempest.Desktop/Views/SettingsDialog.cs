using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Preferences dialog (`WP 10.5B` scope: "Settings, Preferences") —
/// a real, working panel over <see cref="UserSettings"/>: appearance
/// (Theme, reusing <see cref="ThemeService"/> directly, never a second
/// theme mechanism), notifications (Toast duration), and workflow
/// (confirm-before-delete). Initially hidden, shares the Dialog
/// Framework's own established panel styling.
/// </summary>
public sealed class SettingsDialog : Border
{
    private readonly ThemeService _theme;
    private readonly UserSettings _settings;

    private readonly ComboBox _themeSelector = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 140 };
    private readonly NumericUpDown _toastDuration = new() { Minimum = 1, Maximum = 30, Increment = 0.5m, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 100 };
    private readonly CheckBox _confirmBeforeDelete = new() { Content = "Confirm before deleting an object" };
    private readonly Button _saveButton = new() { Content = "Save", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<bool>? _pending;

    /// <summary>Initialises a new instance of the <see cref="SettingsDialog"/> class, initially hidden.</summary>
    public SettingsDialog(ThemeService theme, UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settings);
        _theme = theme;
        _settings = settings;

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

        _themeSelector.Items.Add(new ComboBoxItem { Content = "Light", Tag = ThemeVariant.Light });
        _themeSelector.Items.Add(new ComboBoxItem { Content = "Dark", Tag = ThemeVariant.Dark });

        var title = new TextBlock { Text = "Preferences", FontSize = DesignTokens.FontSizeTitle, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceMd) };

        var appearance = BuildSection("Appearance", LabeledRow("Theme", _themeSelector));
        var notifications = BuildSection("Notifications", LabeledRow("Toast duration (seconds)", _toastDuration));
        var workflow = BuildSection("Workflow", _confirmBeforeDelete);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_saveButton);

        var body = new StackPanel();
        body.Children.Add(title);
        body.Children.Add(appearance);
        body.Children.Add(notifications);
        body.Children.Add(workflow);
        body.Children.Add(buttons);
        Child = body;

        _cancelButton.Click += (_, _) => Complete(false);
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
    }

    /// <summary>Shows this dialog, pre-populated with the current live settings, returning <see langword="true"/> once the user Saves (and the new values are already applied/persisted), <see langword="false"/> on Cancel (nothing changed).</summary>
    public Task<bool> ShowAsync()
    {
        _pending?.TrySetResult(false);

        foreach (var candidate in _themeSelector.Items.OfType<ComboBoxItem>())
        {
            if (Equals(candidate.Tag, _theme.Current))
                _themeSelector.SelectedItem = candidate;
        }

        _toastDuration.Value = (decimal)_settings.ToastDurationSeconds;
        _confirmBeforeDelete.IsChecked = _settings.ConfirmBeforeDelete;

        IsVisible = true;

        _pending = new TaskCompletionSource<bool>();
        return _pending.Task;
    }

    private async Task SaveAsync()
    {
        if (_themeSelector.SelectedItem is ComboBoxItem { Tag: ThemeVariant selectedTheme } && selectedTheme != _theme.Current)
            await _theme.ToggleAsync().ConfigureAwait(true);

        _settings.ToastDurationSeconds = (double)(_toastDuration.Value ?? 4.5m);
        _settings.ConfirmBeforeDelete = _confirmBeforeDelete.IsChecked ?? true;
        await _settings.SaveAsync().ConfigureAwait(true);

        Complete(true);
    }

    private static Control BuildSection(string title, Control content)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceMd) };
        stack.Children.Add(new TextBlock { Text = title, FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXs) });
        stack.Children.Add(content);
        return stack;
    }

    private static Control LabeledRow(string label, Control valueControl)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(valueControl, 1);
        row.Children.Add(text);
        row.Children.Add(valueControl);
        return row;
    }

    private void Complete(bool result)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(result);
    }
}
