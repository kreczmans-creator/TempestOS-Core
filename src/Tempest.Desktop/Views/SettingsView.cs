using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing;
using Tempest.Core.People;
using Tempest.Core.Quotations;
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
/// <b>Persistence root stays read-only; Principal is now a real sign-in
/// (`WP 21.3B`).</b> No Work Package builds a <c>--persistence-root</c>
/// switch (`WP RC.0A`'s own future scope) — the resolved path is shown,
/// never edited. Principal is different: it once showed only the
/// configured <c>Identity:DisplayName</c>/<c>Identity:Role</c> keys
/// ("do not invent an identity switch" was `WP 19.2B`'s own boundary);
/// this Work Package invents exactly that switch, deliberately — a second
/// principal has to be able to sign in and check work the first recorded,
/// which the Evidence Check refusal (`EvidenceService.RecordCheckAsync`)
/// now names directly: "switch person first". <b>Switch person…</b> lists
/// every released person the People directory (`WP 20.10F`, or the
/// <see cref="IPeopleDirectory"/> seam standing in for it — see that
/// interface's own remarks) carries a known sign-in identity for, asks
/// the chosen one to confirm by name (no password — this platform's own
/// posture is single-user local trust, stated on the dialog itself), and
/// publishes them through <see cref="Tempest.Desktop.WorkspaceHost.SwitchPrincipal"/>
/// — the identical <see cref="CurrentPrincipalAccessor.SetCurrent"/> call
/// <see cref="Tempest.Desktop.WorkspaceHost.StartAsync"/> itself makes at
/// launch, never a second mechanism.
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
    private readonly IAccountsReadModel? _accountsReadModel;
    private readonly AccountsRefreshService? _accountsRefreshService;
    private readonly IConfigurationProvider _configuration;
    private readonly string _persistenceRootPath;
    private readonly IPeopleDirectory? _people;
    private readonly ConfirmationDialog? _confirmationDialog;
    private readonly Action<ISessionPrincipal>? _switchPrincipal;

    private readonly TextBox _persistenceRootBox = new() { IsReadOnly = true, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 320 };
    private readonly Button _openPersistenceFolder = new() { Content = "Open folder", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _displayNameOverride = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _roleOverride = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };

    // `WP 21.3B`: Switch person — lists every released person the People
    // directory carries a known sign-in identity for.
    private readonly ComboBox _switchPersonSelector = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 220 };
    private readonly Button _switchPersonButton = new() { Content = "Switch person…", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _switchPersonStatus = new() { FontSize = DesignTokens.FontSizeCaption };

    // `WP 21.3B`: Organisation identity — the consultant's own default VAT
    // rate for a new quotation line (`QuotationService.DefaultVatRateSettingKey`).
    // A minimal stand-in section: `WP 20.10G`'s own fuller "Organisation
    // identity" section (letterhead, trading details) was not in this Work
    // Package's own base and is not built here — reconciled at merge time.
    private readonly ComboBox _defaultVatRateSelector = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 220 };

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
    private readonly TextBlock _accountsReadingStatus = new() { FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.85 };
    private readonly Button _accountsRefreshButton = new() { Content = "Refresh now", MinHeight = DesignTokens.ControlSizeMedium };
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
    /// <param name="people">Where "Switch person…" reads every switchable person from (`WP 21.3B`). <see langword="null"/> leaves the action honestly unavailable.</param>
    /// <param name="confirmationDialog">The shared Dialog Framework overlay "Switch person…" confirms through (`WP 21.3B`). <see langword="null"/> leaves the action honestly unavailable.</param>
    /// <param name="switchPrincipal">Publishes the confirmed person as this session's own principal (`WP 21.3B`) — <see cref="Tempest.Desktop.WorkspaceHost.SwitchPrincipal"/>. <see langword="null"/> leaves the action honestly unavailable.</param>
    public SettingsView(
        ThemeService theme, UserSettings settings, ISettingsProvider settingsProvider, IConfigurationProvider configuration, string persistenceRootPath,
        IWorkingPatternProvider? workingPatterns = null, ICurrentPrincipalAccessor? principals = null,
        IInvoicingConnector? invoicingConnector = null, ISecretStore? secretStore = null,
        IAccountsReadModel? accountsReadModel = null, AccountsRefreshService? accountsRefreshService = null,
        IPeopleDirectory? people = null, ConfirmationDialog? confirmationDialog = null, Action<ISessionPrincipal>? switchPrincipal = null)
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
        _accountsReadModel = accountsReadModel;
        _accountsRefreshService = accountsRefreshService;
        _people = people;
        _confirmationDialog = confirmationDialog;
        _switchPrincipal = switchPrincipal;

        _themeSelector.Items.Add(new ComboBoxItem { Content = "Light", Tag = ThemeVariant.Light });
        _themeSelector.Items.Add(new ComboBoxItem { Content = "Dark", Tag = ThemeVariant.Dark });
        AutomationProperties.SetName(_themeSelector, "Theme");

        _invoicingConnectorSelector.Items.Add(new ComboBoxItem { Content = "Fake", Tag = "Fake" });
        _invoicingConnectorSelector.Items.Add(new ComboBoxItem { Content = "Xero", Tag = "Xero" });
        _invoicingConnectorSelector.Items.Add(new ComboBoxItem { Content = "QuickBooks Online", Tag = "QuickBooksOnline" });
        AutomationProperties.SetName(_invoicingConnectorSelector, "Invoicing connector");

        // `WP 21.3B`: the closed VAT vocabulary, declaration order — the
        // consultant's own default for a new quotation line.
        foreach (var rate in Enum.GetValues<VatRate>())
            _defaultVatRateSelector.Items.Add(new ComboBoxItem { Content = rate.DisplayName(), Tag = rate });
        AutomationProperties.SetName(_defaultVatRateSelector, "Default VAT rate");

        AutomationProperties.SetName(_switchPersonSelector, "Switch to");
        AutomationProperties.SetName(_switchPersonButton, "Switch person…");

        AutomationProperties.SetName(_persistenceRootBox, "Persistence root");
        AutomationProperties.SetName(_openPersistenceFolder, "Open persistence folder");
        AutomationProperties.SetName(_toastDuration, "Toast duration (seconds)");
        AutomationProperties.SetName(_workingPatternHours, "Working pattern (hours per week)");
        AutomationProperties.SetName(_invoicingClientId, "Invoicing client id");
        AutomationProperties.SetName(_invoicingClientSecret, "Invoicing client secret");
        AutomationProperties.SetName(_invoicingPollMinutes, "Invoicing poll interval (minutes)");
        AutomationProperties.SetName(_accountsRefreshButton, "Refresh accounts reading");
        AutomationProperties.SetName(_saveButton, "Save settings");
        AutomationProperties.SetName(_confirmBeforeDelete, "Confirm before deleting an object");
        AutomationProperties.SetName(_independentCheckRequired, "Independent check required");
        AutomationProperties.SetName(_invoicingAuthoriseButton, "Authorise invoicing connector");

        var persistenceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        persistenceRow.Children.Add(_persistenceRootBox);
        persistenceRow.Children.Add(_openPersistenceFolder);
        var persistence = BuildSection("Persistence root", persistenceRow);

        var principalStack = new StackPanel { Spacing = DesignTokens.SpaceXs };
        principalStack.Children.Add(_displayNameOverride);
        principalStack.Children.Add(_roleOverride);

        // `WP 21.3B`: "Switch person…" — a real sign-in, not read-only any
        // longer (this class's own remarks). Shown whenever both
        // collaborators were actually threaded through; otherwise the
        // section stays exactly the read-only pair it always was.
        if (_people is not null && _confirmationDialog is not null && _switchPrincipal is not null)
        {
            var switchRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
            switchRow.Children.Add(_switchPersonSelector);
            switchRow.Children.Add(_switchPersonButton);
            principalStack.Children.Add(switchRow);
            principalStack.Children.Add(_switchPersonStatus);
        }

        var principal = BuildSection("Principal", principalStack);

        // `WP 21.3B`: Organisation identity — a minimal stand-in for `WP
        // 20.10G`'s own fuller section (see this class's own field remarks).
        var organisationIdentity = BuildSection("Organisation identity", LabeledRow("Default VAT rate (new quotation lines)", _defaultVatRateSelector));

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

        // `WP 19.8B` (po-comments.md item 8): the accounts reading — bills,
        // subscriptions and cash, read from the accounting package — never
        // entered in Tempest and never computed here; the same "unavailable
        // since <time>" honesty convention as `DescribeAuthorisationState`,
        // above.
        var accountsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        accountsRow.Children.Add(_accountsReadingStatus);
        accountsRow.Children.Add(_accountsRefreshButton);

        var invoicingStack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        invoicingStack.Children.Add(LabeledRow("Connector", _invoicingConnectorSelector));
        invoicingStack.Children.Add(LabeledRow("Client Id", _invoicingClientId));
        invoicingStack.Children.Add(LabeledRow("Client Secret", _invoicingClientSecret));
        invoicingStack.Children.Add(invoicingAuthoriseRow);
        invoicingStack.Children.Add(LabeledRow("Poll every (minutes)", _invoicingPollMinutes));

        if (_accountsReadModel is not null)
            invoicingStack.Children.Add(accountsRow);

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
        body.Children.Add(organisationIdentity);
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
        _switchPersonButton.Classes.Add(ChromeStyles.Subtle);
        _openPersistenceFolder.Click += (_, _) => OnOpenPersistenceFolder();
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        _invoicingAuthoriseButton.Click += async (_, _) => await OnAuthoriseInvoicingAsync().ConfigureAwait(true);
        _accountsRefreshButton.Click += async (_, _) => await OnRefreshAccountsAsync().ConfigureAwait(true);
        _switchPersonButton.Click += async (_, _) => await OnSwitchPersonAsync().ConfigureAwait(true);

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

        if (_accountsReadModel is not null)
            await RefreshAccountsReadingStatusAsync().ConfigureAwait(true);

        await LoadDefaultVatRateAsync().ConfigureAwait(true);

        if (_people is not null && _confirmationDialog is not null && _switchPrincipal is not null)
            await LoadSwitchablePeopleAsync().ConfigureAwait(true);

        _savedStatus.Text = string.Empty;
    }

    /// <summary>`WP 21.3B`: reads the consultant's own configured default VAT rate — registered by <c>QuotationService</c>'s own constructor, defaulting to <see cref="VatRate.OutOfScope"/> until read back for the first time from a store where that registration has not yet run (a test host with no <c>IQuotationService</c> composed).</summary>
    private async Task LoadDefaultVatRateAsync()
    {
        VatRate current;

        try
        {
            var stored = await _settingsProvider.GetValueAsync(QuotationService.DefaultVatRateSettingKey).ConfigureAwait(true);
            current = Enum.TryParse<VatRate>(stored, out var parsed) ? parsed : VatRate.OutOfScope;
        }
        catch (SettingNotFoundException)
        {
            // No `IQuotationService` has registered the definition yet in
            // this process — reads as the model's own default rather than
            // failing the whole Settings refresh over one missing key.
            current = VatRate.OutOfScope;
        }

        foreach (var candidate in _defaultVatRateSelector.Items.OfType<ComboBoxItem>())
        {
            if (Equals(candidate.Tag, current))
                _defaultVatRateSelector.SelectedItem = candidate;
        }
    }

    /// <summary>`WP 21.3B`: every released person the People directory carries a known sign-in identity for — "Switch person…"'s own candidate list.</summary>
    private async Task LoadSwitchablePeopleAsync()
    {
        if (_people is null)
            return;

        var switchable = await _people.ListSwitchableAsync().ConfigureAwait(true);

        _switchPersonSelector.ItemsSource = switchable
            .Select(p => new ComboBoxItem { Content = p.Role is { } role ? $"{p.DisplayName} ({role})" : p.DisplayName, Tag = p })
            .ToList();
        _switchPersonSelector.SelectedIndex = switchable.Count > 0 ? 0 : -1;
        _switchPersonButton.IsEnabled = switchable.Count > 0;
        _switchPersonStatus.Text = switchable.Count == 0 ? "No released person carries a known sign-in identity yet." : string.Empty;
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

        if (_defaultVatRateSelector.SelectedItem is ComboBoxItem { Tag: VatRate selectedVatRate })
        {
            try
            {
                await _settingsProvider.SetValueAsync(QuotationService.DefaultVatRateSettingKey, selectedVatRate.ToString()).ConfigureAwait(true);
            }
            catch (SettingNotFoundException)
            {
                // As `LoadDefaultVatRateAsync` — no `IQuotationService` has
                // registered the definition in this process; nothing to
                // save it into.
            }
        }

        _savedStatus.Text = $"Saved at {DateTime.Now:HH:mm:ss}.";
        ActionCompleted?.Invoke("Settings saved.", ActionOutcome.Changed);
    }

    /// <summary>
    /// "Switch person…" (`WP 21.3B`): the chosen person confirms by name —
    /// no password, this platform's own single-user local trust posture,
    /// stated on the dialog itself — then becomes this session's own
    /// principal from this moment on.
    /// </summary>
    private async Task OnSwitchPersonAsync()
    {
        if (_people is null || _confirmationDialog is null || _switchPrincipal is null)
            return;

        if (_switchPersonSelector.SelectedItem is not ComboBoxItem { Tag: Person person } || person.IdentityId is not { } identityId)
        {
            _switchPersonStatus.Text = "Choose a person to switch to first.";
            return;
        }

        var confirmed = await _confirmationDialog
            .ConfirmAsync(
                "Switch person",
                $"Switch to {person.DisplayName}? TempestOS does not ask for a password — this platform's own posture is single-user local trust, "
                + "and every act from this point on records as theirs.",
                "Switch")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            _switchPersonStatus.Text = "Switch person was cancelled.";
            return;
        }

        _switchPrincipal(new SessionPrincipal(identityId, person.DisplayName, SessionRole.Engineer));

        _switchPersonStatus.Text = $"Now signed in as {person.DisplayName}.";
        ActionCompleted?.Invoke($"Switched to {person.DisplayName}.", ActionOutcome.Changed);

        await RefreshAsync().ConfigureAwait(true);
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

    private async Task RefreshAccountsReadingStatusAsync()
    {
        if (_accountsReadModel is null)
            return;

        var snapshot = await _accountsReadModel.ReadAsync().ConfigureAwait(true);
        _accountsReadingStatus.Text = DescribeAccountsReading(snapshot);
    }

    private async Task OnRefreshAccountsAsync()
    {
        if (_accountsRefreshService is not null)
            await _accountsRefreshService.RefreshNowAsync().ConfigureAwait(true);

        await RefreshAccountsReadingStatusAsync().ConfigureAwait(true);
        ActionCompleted?.Invoke(_accountsReadingStatus.Text ?? "Accounts reading refreshed.", ActionOutcome.Changed);
    }

    private static string DescribeAccountsReading(AccountsSnapshot snapshot) =>
        snapshot.IsAvailable
            ? $"Accounts reading: last at {snapshot.ReadAt!.Value.ToLocalTime():yyyy-MM-dd HH:mm} ({snapshot.Connector})."
            : $"Accounts reading: unavailable: {snapshot.UnavailableReason}";

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
