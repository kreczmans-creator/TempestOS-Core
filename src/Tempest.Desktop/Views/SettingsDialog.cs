using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Settings;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Preferences dialog (`WP 10.5B` scope: "Settings, Preferences") —
/// a real, working panel over <see cref="UserSettings"/>: appearance
/// (Theme, reusing <see cref="ThemeService"/> directly, never a second
/// theme mechanism), notifications (Toast duration), and workflow
/// (confirm-before-delete). Extended `WP 18.2A` with the Evidence
/// discipline's own governed setting — <em>Independent check
/// required</em> (<see cref="EvidenceService.IndependentCheckSettingKey"/>),
/// read and written through the same <see cref="ISettingsProvider"/> every
/// other runtime-mutable setting already uses (`ADR-0148`, decision 1:
/// built in, switched off by default). Initially hidden, shares the
/// Dialog Framework's own established panel styling.
/// </summary>
public sealed class SettingsDialog : Border
{
    private readonly ThemeService _theme;
    private readonly UserSettings _settings;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IWorkingPatternProvider? _workingPatterns;
    private readonly ICurrentPrincipalAccessor? _principals;

    private readonly ComboBox _themeSelector = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 140 };
    private readonly NumericUpDown _toastDuration = new() { Minimum = 1, Maximum = 30, Increment = 0.5m, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 100 };
    private readonly CheckBox _confirmBeforeDelete = new() { Content = "Confirm before deleting an object" };
    private readonly CheckBox _independentCheckRequired = new() { Content = "Independent check required (checker must differ from the evidence's own author)" };
    private readonly NumericUpDown _workingPatternHours = new() { Minimum = 0, Maximum = 168, Increment = 0.5m, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 100 };
    private readonly Button _saveButton = new() { Content = "Save", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<bool>? _pending;

    /// <summary>Initialises a new instance of the <see cref="SettingsDialog"/> class, initially hidden.</summary>
    /// <param name="theme">The theme service Appearance controls.</param>
    /// <param name="settings">The user settings Notifications and Workflow control.</param>
    /// <param name="settingsProvider">Every runtime-mutable setting's own read/write surface.</param>
    /// <param name="workingPatterns">
    /// Reads and lazily registers the current principal's own working
    /// pattern (`WP 19.0A`, `ADR-0150`). <see langword="null"/> omits the
    /// Timesheets section entirely — a composition root with no principal
    /// context to show it against.
    /// </param>
    /// <param name="principals">The current principal, whose own working pattern the Timesheets section edits. <see langword="null"/> omits the section, as above.</param>
    public SettingsDialog(
        ThemeService theme, UserSettings settings, ISettingsProvider settingsProvider,
        IWorkingPatternProvider? workingPatterns = null, ICurrentPrincipalAccessor? principals = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _theme = theme;
        _settings = settings;
        _settingsProvider = settingsProvider;
        _workingPatterns = workingPatterns;
        _principals = principals;

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

        // `WP 18.2A` (`ADR-0148`, decision 1): a one-person consultancy has
        // one login and enters the client's own review by hand until there
        // is a second member of staff, so this stays off by default;
        // switched on, the independence rule refuses a checker who is also
        // the evidence's own author.
        var evidence = BuildSection("Evidence", _independentCheckRequired);

        // `WP 19.0A` (`ADR-0150`): the current principal's own working
        // pattern — hours per week, the denominator utilisation (`WP 19.1B`)
        // reads. Shown only where a principal context exists to edit it.
        var timesheets = BuildSection("Timesheets", LabeledRow("Working pattern (hours/week)", _workingPatternHours));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_saveButton);

        var body = new StackPanel();
        body.Children.Add(title);
        body.Children.Add(appearance);
        body.Children.Add(notifications);
        body.Children.Add(workflow);
        body.Children.Add(evidence);

        if (_workingPatterns is not null && _principals is not null)
            body.Children.Add(timesheets);

        body.Children.Add(buttons);
        Child = body;

        _saveButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        title.FontFamily = DesignTokens.TitleFont;
        _cancelButton.Click += (_, _) => Complete(false);
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        KeyDown += OnKeyDown;

        // Real modal behaviour (`WP 16.5A`, `TD-65`) — see
        // `DialogModality`'s own remarks.
        DialogModality.Install(this);
    }

    /// <summary>
    /// <c>Escape</c> cancels — nothing this dialog changes is applied
    /// until <see cref="SaveAsync"/> actually runs, so discarding on
    /// Escape (mirroring <see cref="_cancelButton"/>) never loses an
    /// already-applied change. <c>Enter</c> needs no explicit handling —
    /// native <see cref="Button"/> behaviour, unchanged.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Complete(false);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Shows this dialog, pre-populated with the current live settings,
    /// returning <see langword="true"/> once the user Saves (and the new
    /// values are already applied/persisted), <see langword="false"/> on
    /// Cancel (nothing changed).
    /// </summary>
    /// <remarks>
    /// Every field this method sets directly is set synchronously, exactly
    /// as before `WP 18.2A` — a caller that immediately looks for a
    /// control after calling this must still find it already populated.
    /// The one setting that lives behind a real, awaited read
    /// (<see cref="EvidenceService.IndependentCheckSettingKey"/>, an
    /// <see cref="ISettingsProvider"/> value, not a <see cref="UserSettings"/>
    /// field) populates its own checkbox in the background instead,
    /// starting from the safe, off default it already shows the instant
    /// this dialog opens.
    /// </remarks>
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

        _independentCheckRequired.IsChecked = false;
        _ = LoadIndependentCheckRequiredAsync();

        _workingPatternHours.Value = WorkingPatternProvider.DefaultHoursPerWeek;
        _ = LoadWorkingPatternAsync();

        IsVisible = true;
        // The safe action gets initial focus (mirroring
        // `ConfirmationDialog`'s own identical convention) — Enter before
        // tabbing anywhere discards rather than saves.
        _cancelButton.Focus();

        _pending = new TaskCompletionSource<bool>();
        return _pending.Task;
    }

    private async Task LoadIndependentCheckRequiredAsync()
    {
        var value = await _settingsProvider.GetValueAsync(EvidenceService.IndependentCheckSettingKey).ConfigureAwait(true);
        _independentCheckRequired.IsChecked = bool.TryParse(value, out var required) && required;
    }

    /// <summary>
    /// Loads the current principal's own working pattern — registering its
    /// own setting definition lazily, at the default, if nothing has yet
    /// (`WP 19.0A`; mirrors <see cref="LoadIndependentCheckRequiredAsync"/>'s
    /// own background-read shape, starting from the safe default this
    /// dialog already shows the instant it opens).
    /// </summary>
    private async Task LoadWorkingPatternAsync()
    {
        if (_workingPatterns is null || _principals?.Current?.Identity.Id is not { } identityId)
            return;

        _workingPatternHours.Value = await _workingPatterns
            .AvailableHoursAsync(identityId, DateOnly.FromDateTime(DateTime.UtcNow))
            .ConfigureAwait(true);
    }

    private async Task SaveAsync()
    {
        if (_themeSelector.SelectedItem is ComboBoxItem { Tag: ThemeVariant selectedTheme } && selectedTheme != _theme.Current)
            await _theme.ToggleAsync().ConfigureAwait(true);

        _settings.ToastDurationSeconds = (double)(_toastDuration.Value ?? 4.5m);
        _settings.ConfirmBeforeDelete = _confirmBeforeDelete.IsChecked ?? true;
        await _settings.SaveAsync().ConfigureAwait(true);

        await _settingsProvider.SetValueAsync(
            EvidenceService.IndependentCheckSettingKey,
            (_independentCheckRequired.IsChecked ?? false) ? bool.TrueString : bool.FalseString).ConfigureAwait(true);

        if (_workingPatterns is not null && _principals?.Current?.Identity.Id is { } identityId)
        {
            await _workingPatterns.EnsureRegisteredAsync(identityId).ConfigureAwait(true);

            var hours = _workingPatternHours.Value ?? WorkingPatternProvider.DefaultHoursPerWeek;
            await _settingsProvider
                .SetValueAsync(WorkingPatternProvider.SettingKeyFor(identityId), hours.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .ConfigureAwait(true);
        }

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
