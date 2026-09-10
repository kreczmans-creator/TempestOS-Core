using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing;
using Tempest.Core.Secrets;
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
    /// <summary>Where a connector's own client id is stored, through <see cref="ISecretStore"/> — never the persistence database (`WP 19.1A` part 3, `ADR-0151`).</summary>
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
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private bool _invoicingSettingsRegistered;
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
    /// <param name="invoicingConnector">
    /// The connector <see cref="ConnectorAuthorisationState"/> is read from
    /// for the Invoicing section's own Authorise button (`WP 19.1A` part 3,
    /// `ADR-0151`). <see langword="null"/> omits the section entirely.
    /// </param>
    /// <param name="secretStore">Where the Invoicing section's own client id and secret are stored — never <paramref name="settingsProvider"/>'s own runtime-mutable settings, and never the persistence database. <see langword="null"/> omits the section, as above.</param>
    public SettingsDialog(
        ThemeService theme, UserSettings settings, ISettingsProvider settingsProvider,
        IWorkingPatternProvider? workingPatterns = null, ICurrentPrincipalAccessor? principals = null,
        IInvoicingConnector? invoicingConnector = null, ISecretStore? secretStore = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _theme = theme;
        _settings = settings;
        _settingsProvider = settingsProvider;
        _workingPatterns = workingPatterns;
        _principals = principals;
        _invoicingConnector = invoicingConnector;
        _secretStore = secretStore;

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

        // `WP 19.1A` part 3 (`ADR-0151`): connector authorisation — the
        // choice of connector (bound to `InvoicingService.ConnectorConfigurationKey`,
        // "Invoicing:Connector"), its own client id and secret (through
        // `ISecretStore`, never this dialog's own `ISettingsProvider` and
        // never the persistence database), an Authorise button behind the
        // `IInvoicingConnector.AuthorisationStateAsync` seam, and the poll
        // interval (`InvoiceReconciliationService.PollMinutesConfigurationKey`,
        // "Invoicing:PollMinutes"). Shown only where a connector and a
        // secret store exist to edit against.
        _invoicingConnectorSelector.Items.Add(new ComboBoxItem { Content = "Fake", Tag = "Fake" });
        _invoicingConnectorSelector.Items.Add(new ComboBoxItem { Content = "Xero", Tag = "Xero" });
        _invoicingConnectorSelector.Items.Add(new ComboBoxItem { Content = "QuickBooks Online", Tag = "QuickBooksOnline" });

        var invoicingAuthoriseRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        invoicingAuthoriseRow.Children.Add(_invoicingAuthoriseButton);
        invoicingAuthoriseRow.Children.Add(_invoicingAuthorisationStatus);

        var invoicingStack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        invoicingStack.Children.Add(LabeledRow("Connector", _invoicingConnectorSelector));
        invoicingStack.Children.Add(LabeledRow("Client Id", _invoicingClientId));
        invoicingStack.Children.Add(LabeledRow("Client Secret", _invoicingClientSecret));
        invoicingStack.Children.Add(invoicingAuthoriseRow);
        invoicingStack.Children.Add(LabeledRow("Poll every (minutes)", _invoicingPollMinutes));
        var invoicing = BuildSection("Invoicing", invoicingStack);

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

        if (_invoicingConnector is not null && _secretStore is not null)
            body.Children.Add(invoicing);

        body.Children.Add(buttons);
        Child = body;

        _saveButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        title.FontFamily = DesignTokens.TitleFont;
        _cancelButton.Click += (_, _) => Complete(false);
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        _invoicingAuthoriseButton.Click += async (_, _) => await OnAuthoriseInvoicingAsync().ConfigureAwait(true);
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

        _invoicingConnectorSelector.SelectedIndex = 0;
        _invoicingClientId.Text = string.Empty;
        _invoicingClientSecret.Text = string.Empty;
        _invoicingClientSecret.Watermark = string.Empty;
        _invoicingAuthorisationStatus.Text = string.Empty;
        _invoicingPollMinutes.Value = InvoiceReconciliationService.DefaultPollMinutes;
        _ = LoadInvoicingSectionAsync();

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

        if (_invoicingConnector is not null && _secretStore is not null)
            await SaveInvoicingSectionAsync().ConfigureAwait(true);

        Complete(true);
    }

    /// <summary>
    /// Loads the Invoicing section's own current values (`WP 19.1A` part 3):
    /// the connector choice and poll interval from <see cref="ISettingsProvider"/>
    /// (registering both definitions lazily, at their defaults, the same
    /// "register on first read" shape <see cref="LoadWorkingPatternAsync"/>
    /// already uses), whether a client id/secret is stored (never the
    /// secret's own value — a secret field never re-displays what it
    /// already holds), and the connector's own current authorisation state.
    /// </summary>
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

    /// <summary>
    /// Registers the Invoicing section's own two <see cref="ISettingsProvider"/>
    /// definitions, once — <see cref="InvoicingService.ConnectorConfigurationKey"/>
    /// and <see cref="InvoiceReconciliationService.PollMinutesConfigurationKey"/>.
    /// </summary>
    /// <remarks>
    /// Those two constants are <c>Tempest.Core.Configuration.IConfigurationProvider</c>
    /// keys in <c>Tempest.Core.Invoicing</c> today — read once at startup,
    /// immutable thereafter (<c>IConfigurationProvider</c>'s own remarks) —
    /// registered here, under the identical key strings, as this dialog's
    /// own runtime-mutable <see cref="ISettingsProvider"/> values instead,
    /// exactly as <see cref="WorkingPatternProvider"/> registers its own
    /// setting lazily. This Work Package's own files stop at persisting the
    /// operator's choice durably and correctly; wiring
    /// <see cref="Tempest.Core.Runtime.TempestHost"/>'s own connector
    /// selection and <see cref="InvoiceReconciliationService"/>'s own poll
    /// timer to consult this setting at startup is outside this Work
    /// Package's own "Do not touch <c>Core/Invoicing</c>" boundary — see
    /// this Work Package's own report.
    /// </remarks>
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
            // Registered already — by an earlier `ShowAsync` this process
            // made. The definition existing is what matters.
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

    /// <summary>
    /// Re-reads <see cref="IInvoicingConnector.AuthorisationStateAsync"/> and
    /// shows what it reports — the Authorise button's own action
    /// (`WP 19.1A` part 3's own brief §3): "runs the OAuth authoriser from
    /// part 2 when present". No such authoriser exists in this worktree —
    /// part 2 runs in parallel and lands its own real connectors separately
    /// — so, today, every connector's own "Authorise" reduces to this seam
    /// alone; <see cref="FakeInvoicingConnector"/>'s own default state is
    /// already <see cref="ConnectorAuthorisation.Authorised"/>, so pressing
    /// Authorise with it selected succeeds immediately, exactly as the
    /// brief describes.
    /// </summary>
    private async Task RefreshInvoicingAuthorisationStatusAsync()
    {
        if (_invoicingConnector is null)
            return;

        var state = await _invoicingConnector.AuthorisationStateAsync().ConfigureAwait(true);
        _invoicingAuthorisationStatus.Text = DescribeAuthorisationState(state);
    }

    private async Task OnAuthoriseInvoicingAsync() => await RefreshInvoicingAuthorisationStatusAsync().ConfigureAwait(true);

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

        // A blank secret field means "leave the stored secret unchanged" —
        // this dialog never re-displays a stored secret (`LoadInvoicingSectionAsync`'s
        // own remarks), so a blank field is never distinguishable from "the
        // operator did not mean to change it" and must not clear it.
        if (!string.IsNullOrEmpty(_invoicingClientSecret.Text))
            await _secretStore.SetAsync(InvoicingClientSecretSecretKey, _invoicingClientSecret.Text).ConfigureAwait(true);

        var pollMinutes = (int)(_invoicingPollMinutes.Value ?? InvoiceReconciliationService.DefaultPollMinutes);
        await _settingsProvider
            .SetValueAsync(InvoiceReconciliationService.PollMinutesConfigurationKey, pollMinutes.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(true);
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
