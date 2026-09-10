using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Tempest.Core.Configuration;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing;
using Tempest.Core.Secrets;
using Tempest.Core.Settings;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Settings area (`WP 19.2B`): a rail destination, not a dialog —
/// every section the retired Preferences dialog held, plus two new
/// read-only sections the dialog never had room for: the persistence
/// root and the principal override. Persistence root, principal
/// override, connector authorisation, working pattern, the
/// independent-check toggle, theme, toast duration and
/// confirm-before-delete.
/// </summary>
/// <remarks>
/// <para>
/// <b>No pending Save/Cancel two-step.</b> A rail area is not a modal:
/// there is nothing to trap Tab inside and nothing an Escape key should
/// discard. Every field applies when <b>Save</b> is pressed — one real,
/// dispatched write per section, exactly as the retired dialog's own Save
/// always applied every field together — and leaving without pressing it
/// simply leaves the stored values as they were, the ordinary behaviour
/// of navigating away from an unsaved form.
/// </para>
/// <para>
/// <b>Persistence root and principal override are read-only.</b> Neither
/// this Work Package nor any Work Package before it builds a
/// <c>--persistence-root</c> switch or an identity override control (`WP
/// RC.0A`'s own future scope) — showing the resolved path and the
/// configured <c>Identity:DisplayName</c>/<c>Identity:Role</c> keys is
/// this brief's own explicit boundary: "do not invent an identity
/// switch".
/// </para>
/// </remarks>
public sealed class SettingsView : UserControl
{
    /// <summary>Where a connector's own client id is stored, through <see cref="ISecretStore"/> — never the persistence database (`WP 19.1A` part 3, `ADR-0151`; moved here from the retired <c>SettingsDialog</c>, `WP 19.2B`).</summary>
    public const string InvoicingClientIdSecretKey = "Invoicing:ClientId";

    /// <summary>Where a connector's own client secret is stored, through <see cref="ISecretStore"/> — never the persistence database.</summary>
    public const string InvoicingClientSecretSecretKey = "Invoicing:ClientSecret";

    private readonly ThemeService _theme;
    private readonly UserSettings _settings;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IWorkingPatternProvider? _workingPatterns;
    private readonly ICurrentPrincipalAccessor? _principals;
    private readonly IInvoicingConnector? _invoicingConnector;
    private readonly ISecretStore? _secretStore;
    private readonly IConfigurationProvider _configuration;
    private readonly string _persistenceRootPath;

    private readonly TextBox _persistenceRootBox = new() { IsReadOnly = true, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 320 };
    private readonly Button _openPersistenceFolder = new() { Content = "Open folder", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _displayNameOverride = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _roleOverride = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };

    private readonly ComboBox _themeSelector = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 140 };
    private readonly NumericUpDown _toastDuration = new() { Minimum = 1, Maximum = 30, Increment = 0.5m, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 100 };
    private readonly CheckBox _confirmBeforeDelete = new() { Content = "Confirm before deleting an object" };
    private readonly CheckBox _independentCheckRequired = new() { Content = "Independent check required (checker must differ from the evidence's own author)" };
    private readonly NumericUpDown _workingPatternHours = new() { Minimum = 0, Maximum = 168, Increment = 0.5m, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 100 };
    private readonly ComboBox _invoicingConnectorSelector = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 160 };
    private readonly TextBox _invoicingClientId = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 160 };
    private readonly TextBox _invoicingClientSecret = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 160, PasswordChar = '•' };
    private readonly TextBlock _invoicingAuthorisationStatus = new() { FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.85 };
    private readonly Button _invoicingAuthoriseButton = new() { Content = "Authorise", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly NumericUpDown _invoicingPollMinutes = new() { Minimum = 1, Maximum = 1440, Increment = 1, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 100 };
    private readonly Button _saveButton = new() { Content = "Save", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _savedStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private bool _invoicingSettingsRegistered;

    /// <summary>Opens <paramref name="folderPath"/> in the operating system's own file manager — real by default (<see cref="Process.Start(ProcessStartInfo)"/>), overridable by a test.</summary>
    public Action<string> OpenFolder { get; set; } = DefaultOpenFolder;

    /// <summary>Raised after Save or Authorise completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="SettingsView"/> class.</summary>
    /// <param name="persistenceRootPath">The resolved path of the persistence database, or a plain description when there is none (an in-memory store, in a test) — read-only, shown as-is.</param>
    /// <param name="configuration">Where <c>Identity:DisplayName</c> and <c>Identity:Role</c> are read from, read-only — never a second identity mechanism.</param>
    public SettingsView(
        ThemeService theme, UserSettings settings, ISettingsProvider settingsProvider, IConfigurationProvider configuration, string persistenceRootPath,
        IWorkingPatternProvider? workingPatterns = null, ICurrentPrincipalAccessor? principals = null,
        IInvoicingConnector? invoicingConnector = null, ISecretStore? secretStore = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(persistenceRootPath);
        _theme = theme;
        _settings = settings;
        _settingsProvider = settingsProvider;
        _configuration = configuration;
        _persistenceRootPath = persistenceRootPath;
        _workingPatterns = workingPatterns;
        _principals = principals;
        _invoicingConnector = invoicingConnector;
        _secretStore = secretStore;

        _themeSelector.Items.Add(new ComboBoxItem { Content = "Light", Tag = ThemeVariant.Light });
        _themeSelector.Items.Add(new ComboBoxItem { Content = "Dark", Tag = ThemeVariant.Dark });
        AutomationProperties.SetName(_themeSelector, "Theme");

        _invoicingConnectorSelector.Items.Add(new ComboBoxItem { Content = "Fake", Tag = "Fake" });
        _invoicingConnectorSelector.Items.Add(new ComboBoxItem { Content = "Xero", Tag = "Xero" });
        _invoicingConnectorSelector.Items.Add(new ComboBoxItem { Content = "QuickBooks Online", Tag = "QuickBooksOnline" });
        AutomationProperties.SetName(_invoicingConnectorSelector, "Invoicing connector");

        AutomationProperties.SetName(_persistenceRootBox, "Persistence root");
        AutomationProperties.SetName(_openPersistenceFolder, "Open persistence folder");
        AutomationProperties.SetName(_toastDuration, "Toast duration (seconds)");
        AutomationProperties.SetName(_workingPatternHours, "Working pattern (hours per week)");
        AutomationProperties.SetName(_invoicingClientId, "Invoicing client id");
        AutomationProperties.SetName(_invoicingClientSecret, "Invoicing client secret");
        AutomationProperties.SetName(_invoicingPollMinutes, "Invoicing poll interval (minutes)");
        AutomationProperties.SetName(_saveButton, "Save settings");
        AutomationProperties.SetName(_invoicingAuthoriseButton, "Authorise invoicing connector");

        var persistenceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        persistenceRow.Children.Add(_persistenceRootBox);
        persistenceRow.Children.Add(_openPersistenceFolder);
        var persistence = BuildSection("Persistence root", persistenceRow);

        var principalStack = new StackPanel { Spacing = DesignTokens.SpaceXs };
        principalStack.Children.Add(_displayNameOverride);
        principalStack.Children.Add(_roleOverride);
        var principal = BuildSection("Principal override", principalStack);

        var appearance = BuildSection("Appearance", LabeledRow("Theme", _themeSelector));
        var notifications = BuildSection("Notifications", LabeledRow("Toast duration (seconds)", _toastDuration));
        var workflow = BuildSection("Workflow", _confirmBeforeDelete);

        // `WP 18.2A` (`ADR-0148`, decision 1): unchanged from `SettingsDialog`.
        var evidence = BuildSection("Evidence", _independentCheckRequired);

        // `WP 19.0A` (`ADR-0150`): unchanged from `SettingsDialog`.
        var timesheets = BuildSection("Working pattern", LabeledRow("Hours per week", _workingPatternHours));

        // `WP 19.1A` part 3 (`ADR-0151`): unchanged from `SettingsDialog`.
        var invoicingAuthoriseRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        invoicingAuthoriseRow.Children.Add(_invoicingAuthoriseButton);
        invoicingAuthoriseRow.Children.Add(_invoicingAuthorisationStatus);

        var invoicingStack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        invoicingStack.Children.Add(LabeledRow("Connector", _invoicingConnectorSelector));
        invoicingStack.Children.Add(LabeledRow("Client Id", _invoicingClientId));
        invoicingStack.Children.Add(LabeledRow("Client Secret", _invoicingClientSecret));
        invoicingStack.Children.Add(invoicingAuthoriseRow);
        invoicingStack.Children.Add(LabeledRow("Poll every (minutes)", _invoicingPollMinutes));
        var invoicing = BuildSection("Connector authorisation", invoicingStack);

        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, VerticalAlignment = VerticalAlignment.Center };
        saveRow.Children.Add(_saveButton);
        saveRow.Children.Add(_savedStatus);

        var body = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceLg };
        body.Children.Add(PageHeading.Label("SETTINGS"));
        body.Children.Add(PageHeading.Title("Settings"));
        body.Children.Add(PageHeading.Lead("Persistence, the current principal, connector authorisation, working pattern and the platform's own working preferences."));
        body.Children.Add(persistence);
        body.Children.Add(principal);
        body.Children.Add(appearance);
        body.Children.Add(notifications);
        body.Children.Add(workflow);
        body.Children.Add(evidence);

        if (_workingPatterns is not null && _principals is not null)
            body.Children.Add(timesheets);

        if (_invoicingConnector is not null && _secretStore is not null)
            body.Children.Add(invoicing);

        body.Children.Add(saveRow);

        _saveButton.Classes.Add(ChromeStyles.Primary);
        _openPersistenceFolder.Classes.Add(ChromeStyles.Subtle);
        _openPersistenceFolder.Click += (_, _) => OnOpenPersistenceFolder();
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        _invoicingAuthoriseButton.Click += async (_, _) => await OnAuthoriseInvoicingAsync().ConfigureAwait(true);

        AutomationProperties.SetName(this, "Settings");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Re-reads every current value — settings, connector state, and the two read-only sections — the same "load when you land here" discipline every other rail area follows.</summary>
    public async Task RefreshAsync()
    {
        _persistenceRootBox.Text = _persistenceRootPath;

        _displayNameOverride.Text = _configuration.TryGetValue(SessionPrincipalSource.DisplayNameConfigurationKey, out var displayName) && !string.IsNullOrWhiteSpace(displayName)
            ? $"Display name — {displayName} (configured via {SessionPrincipalSource.DisplayNameConfigurationKey})"
            : $"Display name — not configured ({SessionPrincipalSource.DisplayNameConfigurationKey}); using the OS account name.";

        _roleOverride.Text = _configuration.TryGetValue(SessionPrincipalSource.RoleConfigurationKey, out var role) && !string.IsNullOrWhiteSpace(role)
            ? $"Role — {role} (configured via {SessionPrincipalSource.RoleConfigurationKey})"
            : $"Role — not configured ({SessionPrincipalSource.RoleConfigurationKey}); defaulting to {SessionRole.Engineer}.";

        foreach (var candidate in _themeSelector.Items.OfType<ComboBoxItem>())
        {
            if (Equals(candidate.Tag, _theme.Current))
                _themeSelector.SelectedItem = candidate;
        }

        _toastDuration.Value = (decimal)_settings.ToastDurationSeconds;
        _confirmBeforeDelete.IsChecked = _settings.ConfirmBeforeDelete;

        _independentCheckRequired.IsChecked = bool.TryParse(
            await _settingsProvider.GetValueAsync(EvidenceService.IndependentCheckSettingKey).ConfigureAwait(true), out var required) && required;

        if (_workingPatterns is not null && _principals?.Current?.Identity.Id is { } identityId)
        {
            _workingPatternHours.Value = await _workingPatterns
                .AvailableHoursAsync(identityId, DateOnly.FromDateTime(DateTime.UtcNow))
                .ConfigureAwait(true);
        }
        else
        {
            _workingPatternHours.Value = WorkingPatternProvider.DefaultHoursPerWeek;
        }

        if (_invoicingConnector is not null && _secretStore is not null)
            await LoadInvoicingSectionAsync().ConfigureAwait(true);

        _savedStatus.Text = string.Empty;
    }

    private void OnOpenPersistenceFolder()
    {
        var folder = Path.GetDirectoryName(_persistenceRootPath);
        OpenFolder(string.IsNullOrEmpty(folder) ? _persistenceRootPath : folder);
    }

    private static void DefaultOpenFolder(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
                Process.Start(new ProcessStartInfo { FileName = folderPath, UseShellExecute = true })?.Dispose();
        }
        catch
        {
            // Best-effort — no file manager reachable (a container, a
            // minimal CI image) is not this button's failure to report.
        }
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
                .SetValueAsync(WorkingPatternProvider.SettingKeyFor(identityId), hours.ToString(CultureInfo.InvariantCulture))
                .ConfigureAwait(true);
        }

        if (_invoicingConnector is not null && _secretStore is not null)
            await SaveInvoicingSectionAsync().ConfigureAwait(true);

        _savedStatus.Text = $"Saved at {DateTime.Now:HH:mm:ss}.";
        ActionCompleted?.Invoke("Settings saved.", ActionOutcome.Changed);
    }

    private async Task LoadInvoicingSectionAsync()
    {
        if (_invoicingConnector is null || _secretStore is null)
            return;

        EnsureInvoicingSettingsRegistered();

        var connectorValue = await _settingsProvider.GetValueAsync(InvoicingService.ConnectorConfigurationKey).ConfigureAwait(true);
        SelectInvoicingConnector(connectorValue);

        var storedClientId = await _secretStore.GetAsync(InvoicingClientIdSecretKey).ConfigureAwait(true);
        _invoicingClientId.Text = storedClientId ?? string.Empty;

        var hasStoredSecret = await _secretStore.GetAsync(InvoicingClientSecretSecretKey).ConfigureAwait(true) is not null;
        _invoicingClientSecret.Text = string.Empty;
        _invoicingClientSecret.Watermark = hasStoredSecret ? "(unchanged)" : string.Empty;

        var pollValue = await _settingsProvider.GetValueAsync(InvoiceReconciliationService.PollMinutesConfigurationKey).ConfigureAwait(true);
        _invoicingPollMinutes.Value = int.TryParse(pollValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? minutes
            : InvoiceReconciliationService.DefaultPollMinutes;

        await RefreshInvoicingAuthorisationStatusAsync().ConfigureAwait(true);
    }

    private void EnsureInvoicingSettingsRegistered()
    {
        if (_invoicingSettingsRegistered)
            return;

        _invoicingSettingsRegistered = true;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(
                InvoicingService.ConnectorConfigurationKey, "Invoicing — connector", "Fake"));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Registered already — by an earlier RefreshAsync this process made.
        }

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(
                InvoiceReconciliationService.PollMinutesConfigurationKey, "Invoicing — poll interval (minutes)",
                InvoiceReconciliationService.DefaultPollMinutes.ToString(CultureInfo.InvariantCulture)));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // As above.
        }
    }

    private void SelectInvoicingConnector(string value)
    {
        foreach (var candidate in _invoicingConnectorSelector.Items.OfType<ComboBoxItem>())
        {
            if (Equals(candidate.Tag, value))
            {
                _invoicingConnectorSelector.SelectedItem = candidate;
                return;
            }
        }

        _invoicingConnectorSelector.SelectedIndex = 0;
    }

    private async Task RefreshInvoicingAuthorisationStatusAsync()
    {
        if (_invoicingConnector is null)
            return;

        var state = await _invoicingConnector.AuthorisationStateAsync().ConfigureAwait(true);
        _invoicingAuthorisationStatus.Text = DescribeAuthorisationState(state);
    }

    private async Task OnAuthoriseInvoicingAsync()
    {
        await RefreshInvoicingAuthorisationStatusAsync().ConfigureAwait(true);
        ActionCompleted?.Invoke(_invoicingAuthorisationStatus.Text ?? "Authorisation checked.", ActionOutcome.NoChange);
    }

    private static string DescribeAuthorisationState(ConnectorAuthorisationState state)
    {
        var headline = state.Status switch
        {
            ConnectorAuthorisation.Authorised => "Authorised.",
            ConnectorAuthorisation.NotAuthorised => "Not authorised.",
            ConnectorAuthorisation.Expired => "Re-authorise needed.",
            _ => "Unknown.",
        };

        return state.Detail is { } detail ? $"{headline} {detail}" : headline;
    }

    private async Task SaveInvoicingSectionAsync()
    {
        if (_invoicingConnector is null || _secretStore is null)
            return;

        var connectorValue = (_invoicingConnectorSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "Fake";
        await _settingsProvider.SetValueAsync(InvoicingService.ConnectorConfigurationKey, connectorValue).ConfigureAwait(true);

        var clientId = _invoicingClientId.Text ?? string.Empty;
        if (string.IsNullOrEmpty(clientId))
            await _secretStore.RemoveAsync(InvoicingClientIdSecretKey).ConfigureAwait(true);
        else
            await _secretStore.SetAsync(InvoicingClientIdSecretKey, clientId).ConfigureAwait(true);

        if (!string.IsNullOrEmpty(_invoicingClientSecret.Text))
            await _secretStore.SetAsync(InvoicingClientSecretSecretKey, _invoicingClientSecret.Text).ConfigureAwait(true);

        var pollMinutes = (int)(_invoicingPollMinutes.Value ?? InvoiceReconciliationService.DefaultPollMinutes);
        await _settingsProvider
            .SetValueAsync(InvoiceReconciliationService.PollMinutesConfigurationKey, pollMinutes.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(true);
    }

    private static Control BuildSection(string title, Control content)
    {
        var stack = new StackPanel { Spacing = DesignTokens.SpaceXs };
        stack.Children.Add(new TextBlock { Text = title, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        stack.Children.Add(content);

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = stack,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
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
}
