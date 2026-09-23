using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing;
using Tempest.Core.Persistence;
using Tempest.Core.People;
using Tempest.Core.Quotations;
using Tempest.Core.Secrets;
using Tempest.Core.Settings;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Startup;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Files;
using Tempest.Workspace.Projects;

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
    /// <summary>
    /// The legacy, provider-less key this view stored a client id under from
    /// `WP 19.1A` part 3 until `WP 21.6P` — a key <see cref="Tempest.Core.Invoicing.OAuth.OAuthAuthoriser"/>
    /// never read (it resolves <c>Invoicing:&lt;Provider&gt;:ClientId</c>,
    /// `ADR-0151`), so a client id typed here never reached the authoriser.
    /// Still read as a fallback when the provider's own key is empty, and
    /// migrated onto the provider's key on Save, so a value the Product
    /// Owner already typed is not lost.
    /// </summary>
    public const string InvoicingClientIdSecretKey = "Invoicing:ClientId";

    /// <summary>The legacy, provider-less client-secret key — see <see cref="InvoicingClientIdSecretKey"/>.</summary>
    public const string InvoicingClientSecretSecretKey = "Invoicing:ClientSecret";

    /// <summary>The <see cref="ISecretStore"/> key the authoriser reads a provider's client id from — <c>Invoicing:&lt;Provider&gt;:ClientId</c> (`ADR-0151`), never the persistence database.</summary>
    public static string ClientIdSecretKeyFor(string provider) => $"Invoicing:{provider}:ClientId";

    /// <summary>The <see cref="ISecretStore"/> key the authoriser reads a provider's client secret from — <c>Invoicing:&lt;Provider&gt;:ClientSecret</c>.</summary>
    public static string ClientSecretSecretKeyFor(string provider) => $"Invoicing:{provider}:ClientSecret";

    /// <summary>How long <em>Authorise</em> waits for the operator to finish signing in at the provider before giving up (`WP 21.6P`).</summary>
    public static readonly TimeSpan AuthorisationTimeout = TimeSpan.FromMinutes(5);

    private bool _loadingInvoicingSection;

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
    private readonly OrganisationIdentitySettings? _organisationIdentity;

    // `WP 21.5A` (`WP RC.0A` scope item 4): Settings → Data's own backup
    // and restore, and Settings → Updates. Both sections are entirely
    // optional collaborators — `null` hides the section, exactly as
    // `_workingPatterns`/`_invoicingConnector` already do above — so a test
    // (or a build with an in-memory store, which has no database file to
    // back up at all) simply sees neither.
    private readonly string? _persistenceDatabasePath;
    private readonly IAuditRecorder? _auditRecorder;
    private readonly IProjectContext? _projectContext;
    private readonly Func<Task>? _prepareForRestartAsync;
    private readonly IUpdateService? _updateService;
    private readonly UpdateAvailability? _updateAvailability;
    private readonly IFilePicker? _filePicker;
    private readonly BackupService _backupService = new();
    private readonly IPeopleDirectory? _people;
    private readonly ConfirmationDialog? _confirmationDialog;
    private readonly Action<ISessionPrincipal>? _switchPrincipal;

    private readonly TextBox _persistenceRootBox = new() { IsReadOnly = true, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 320 };
    private readonly Button _openPersistenceFolder = new() { Content = "Open folder", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _displayNameOverride = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _roleOverride = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };

    private readonly Button _backUpNowButton = new() { Content = "Back up now…", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _restoreButton = new() { Content = "Restore from backup…", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _openBackupsFolderButton = new() { Content = "Open backups folder", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _backupStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, TextWrapping = TextWrapping.Wrap };

    private readonly CheckBox _checkForUpdatesOnLaunch = new() { Content = "Check for updates automatically on launch" };
    private readonly Button _checkForUpdatesNowButton = new() { Content = "Check now", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _applyUpdateButton = new() { MinHeight = DesignTokens.ControlSizeMedium, IsVisible = false };
    private readonly TextBlock _updateStatus = new() { FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.85 };
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

    // `WP 20.10G` (PO finding D4): Settings → Organisation identity — the
    // fields every exported document's footer reads at render time
    // (`Documents.DocumentTemplate`), pre-filled with the Tempest defaults.
    private readonly TextBox _orgLegalName = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 260 };
    private readonly TextBox _orgCompanyNumber = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 160 };
    private readonly TextBox _orgWebsite = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 220 };
    private readonly TextBox _orgAddressLine1 = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 260 };
    private readonly TextBox _orgAddressLine2 = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 260 };
    private readonly TextBox _orgEmail = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 220 };
    private readonly TextBox _orgPhone = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 180 };

    // `WP 21.2A`: the invoice document's own "Payment details" section —
    // read at render time exactly as the six fields above already are.
    private readonly TextBox _orgBankSortCode = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 120 };
    private readonly TextBox _orgBankAccountNumber = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 160 };
    private readonly TextBox _orgBankAccountName = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 220 };
    private readonly TextBox _orgBankIban = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 220 };

    private bool _invoicingSettingsRegistered;

    /// <summary>Opens <paramref name="folderPath"/> in the operating system's own file manager — real by default (<see cref="Process.Start(ProcessStartInfo)"/>), overridable by a test.</summary>
    public Action<string> OpenFolder { get; set; } = DefaultOpenFolder;

    /// <summary>Relaunches the application and ends this process — real by default, overridable by a test so a restore's own journey test does not actually exit the test host.</summary>
    public Action RestartProcess { get; set; } = DefaultRestartProcess;

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
        OrganisationIdentitySettings? organisationIdentity = null,
        string? persistenceDatabasePath = null, IAuditRecorder? auditRecorder = null,
        IProjectContext? projectContext = null, Func<Task>? prepareForRestartAsync = null,
        IUpdateService? updateService = null, UpdateAvailability? updateAvailability = null,
        IFilePicker? filePicker = null,
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
        _organisationIdentity = organisationIdentity;
        _persistenceDatabasePath = persistenceDatabasePath;
        _auditRecorder = auditRecorder;
        _projectContext = projectContext;
        _prepareForRestartAsync = prepareForRestartAsync;
        _updateService = updateService;
        _updateAvailability = updateAvailability;
        _filePicker = filePicker;
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
        _invoicingConnectorSelector.SelectionChanged += async (_, _) =>
        {
            // `WP 21.6P`: each provider keeps its own client id and secret;
            // switching the selector shows the chosen provider's own.
            if (!_loadingInvoicingSection && _invoicingConnector is not null && _secretStore is not null)
                await LoadCredentialFieldsAsync(SelectedInvoicingProvider()).ConfigureAwait(true);
        };

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
        AutomationProperties.SetName(_orgLegalName, "Organisation legal name");
        AutomationProperties.SetName(_orgCompanyNumber, "Organisation company number");
        AutomationProperties.SetName(_orgWebsite, "Organisation website");
        AutomationProperties.SetName(_orgAddressLine1, "Organisation address line 1");
        AutomationProperties.SetName(_orgAddressLine2, "Organisation address line 2");
        AutomationProperties.SetName(_orgEmail, "Organisation email");
        AutomationProperties.SetName(_orgPhone, "Organisation phone");
        AutomationProperties.SetName(_backUpNowButton, "Back up now");
        AutomationProperties.SetName(_restoreButton, "Restore from backup");
        AutomationProperties.SetName(_openBackupsFolderButton, "Open backups folder");
        AutomationProperties.SetName(_checkForUpdatesOnLaunch, "Check for updates automatically on launch");
        AutomationProperties.SetName(_checkForUpdatesNowButton, "Check for updates now");
        AutomationProperties.SetName(_applyUpdateButton, "Apply update");
        AutomationProperties.SetName(_orgBankSortCode, "Organisation bank sort code");
        AutomationProperties.SetName(_orgBankAccountNumber, "Organisation bank account number");
        AutomationProperties.SetName(_orgBankAccountName, "Organisation bank account name");
        AutomationProperties.SetName(_orgBankIban, "Organisation bank IBAN");

        var persistenceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        persistenceRow.Children.Add(_persistenceRootBox);
        persistenceRow.Children.Add(_openPersistenceFolder);
        var persistence = BuildSection("Persistence root", persistenceRow);

        // `WP 21.5A` (`WP RC.0A` scope item 4). Shown only when there is a
        // real database file to act on — an in-memory test store has none.
        var backupRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        backupRow.Children.Add(_backUpNowButton);
        backupRow.Children.Add(_restoreButton);
        backupRow.Children.Add(_openBackupsFolderButton);
        var backupStack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        backupStack.Children.Add(backupRow);
        backupStack.Children.Add(_backupStatus);
        var backup = BuildSection("Backup and restore", backupStack);

        // `WP 21.5A` (`WP RC.0A` scope item 1). Shown only when this run
        // has an update service at all — never for `dotnet run`/a plain
        // `bin/` exe/the plain zip, none of which construct one (`WP
        // 21.5A`'s brief: "off by default... so nothing phones home
        // unasked" — the section itself is absent, not merely disabled,
        // for every run shape that could never act on it anyway).
        var updateRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        updateRow.Children.Add(_checkForUpdatesNowButton);
        updateRow.Children.Add(_applyUpdateButton);
        updateRow.Children.Add(_updateStatus);
        var updateStack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        updateStack.Children.Add(_checkForUpdatesOnLaunch);
        updateStack.Children.Add(updateRow);
        var updates = BuildSection("Updates", updateStack);

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
        var vatDefaults = BuildSection("VAT", LabeledRow("Default VAT rate (new quotation lines)", _defaultVatRateSelector));

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

        // `WP 20.10G` (PO finding D4): the fields every exported
        // document's footer shows — `DocumentTemplate.AppendFooters`
        // reads `OrganisationIdentitySettings.ToIdentity()` at render
        // time, never a constant baked into a renderer.
        var orgStack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        orgStack.Children.Add(LabeledRow("Legal name", _orgLegalName));
        orgStack.Children.Add(LabeledRow("Company number", _orgCompanyNumber));
        orgStack.Children.Add(LabeledRow("Website", _orgWebsite));
        orgStack.Children.Add(LabeledRow("Address line 1", _orgAddressLine1));
        orgStack.Children.Add(LabeledRow("Address line 2", _orgAddressLine2));
        orgStack.Children.Add(LabeledRow("Email", _orgEmail));
        orgStack.Children.Add(LabeledRow("Phone", _orgPhone));
        var organisation = BuildSection("Organisation identity", orgStack);

        // `WP 21.2A`, scope item 2: the invoice renderer's own "Payment
        // details" section — a separate BuildSection, not folded into
        // "Organisation identity" above, since a bank detail is never part
        // of a document's footer (`DocumentTemplate.AppendFooters` reads
        // only the seven identity fields) and deserves its own visible
        // grouping instead of six fields silently gaining four unrelated
        // neighbours.
        var bankStack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        bankStack.Children.Add(LabeledRow("Sort code", _orgBankSortCode));
        bankStack.Children.Add(LabeledRow("Account number", _orgBankAccountNumber));
        bankStack.Children.Add(LabeledRow("Account name", _orgBankAccountName));
        bankStack.Children.Add(LabeledRow("IBAN", _orgBankIban));
        var bankDetails = BuildSection("Bank details (invoice payment details)", bankStack);

        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, VerticalAlignment = VerticalAlignment.Center };
        saveRow.Children.Add(_saveButton);
        saveRow.Children.Add(_savedStatus);

        var body = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceLg };
        body.Children.Add(PageHeading.Label("SETTINGS"));
        body.Children.Add(PageHeading.Title("Settings"));
        body.Children.Add(PageHeading.Lead("Persistence, the current principal, connector authorisation, working pattern and the platform's own working preferences."));
        body.Children.Add(persistence);

        if (_persistenceDatabasePath is not null && _filePicker is not null)
            body.Children.Add(backup);

        body.Children.Add(principal);
        body.Children.Add(vatDefaults);
        body.Children.Add(appearance);
        body.Children.Add(notifications);
        body.Children.Add(workflow);
        body.Children.Add(evidence);

        if (_workingPatterns is not null && _principals is not null)
            body.Children.Add(timesheets);

        if (_invoicingConnector is not null && _secretStore is not null)
            body.Children.Add(invoicing);

        if (_organisationIdentity is not null)
        {
            body.Children.Add(organisation);
            body.Children.Add(bankDetails);
        }

        if (_updateService is not null)
            body.Children.Add(updates);

        body.Children.Add(saveRow);

        _saveButton.Classes.Add(ChromeStyles.Primary);
        _openPersistenceFolder.Classes.Add(ChromeStyles.Subtle);
        _openBackupsFolderButton.Classes.Add(ChromeStyles.Subtle);
        _switchPersonButton.Classes.Add(ChromeStyles.Subtle);
        _openPersistenceFolder.Click += (_, _) => OnOpenPersistenceFolder();
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        _invoicingAuthoriseButton.Click += async (_, _) => await OnAuthoriseInvoicingAsync().ConfigureAwait(true);
        _accountsRefreshButton.Click += async (_, _) => await OnRefreshAccountsAsync().ConfigureAwait(true);
        _backUpNowButton.Click += async (_, _) => await OnBackUpNowAsync().ConfigureAwait(true);
        _restoreButton.Click += async (_, _) => await OnRestoreAsync().ConfigureAwait(true);
        _openBackupsFolderButton.Click += (_, _) => OnOpenBackupsFolder();
        _checkForUpdatesNowButton.Click += async (_, _) => await OnCheckForUpdatesNowAsync().ConfigureAwait(true);
        _applyUpdateButton.Click += async (_, _) => await OnApplyUpdateAsync().ConfigureAwait(true);
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

        if (_organisationIdentity is not null)
        {
            _orgLegalName.Text = _organisationIdentity.LegalName;
            _orgCompanyNumber.Text = _organisationIdentity.CompanyNumber;
            _orgWebsite.Text = _organisationIdentity.Website;
            _orgAddressLine1.Text = _organisationIdentity.AddressLine1;
            _orgAddressLine2.Text = _organisationIdentity.AddressLine2;
            _orgEmail.Text = _organisationIdentity.Email;
            _orgPhone.Text = _organisationIdentity.Phone;
            _orgBankSortCode.Text = _organisationIdentity.BankSortCode;
            _orgBankAccountNumber.Text = _organisationIdentity.BankAccountNumber;
            _orgBankAccountName.Text = _organisationIdentity.BankAccountName;
            _orgBankIban.Text = _organisationIdentity.BankIban;
        }
        if (_persistenceDatabasePath is not null)
            _backupStatus.Text = string.Empty;

        _checkForUpdatesOnLaunch.IsChecked = _settings.CheckForUpdatesOnLaunch;

        if (_updateService is not null)
            RefreshUpdateStatus();
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

    private void OnOpenBackupsFolder()
    {
        if (_persistenceDatabasePath is null)
            return;

        var databaseDirectory = Path.GetDirectoryName(_persistenceDatabasePath);
        if (string.IsNullOrEmpty(databaseDirectory))
            return;

        var backupsFolder = Path.Combine(databaseDirectory, BackupService.BackupsFolderName);
        Directory.CreateDirectory(backupsFolder);
        OpenFolder(backupsFolder);
    }

    /// <summary>"Back up now…" — a Save picker, the online backup API, and read-back verification (`WP 21.5A`). Audited when an <see cref="IAuditRecorder"/> is available.</summary>
    private async Task OnBackUpNowAsync()
    {
        if (_persistenceDatabasePath is null || _filePicker is null)
            return;

        var destination = await _filePicker.PickSavePathAsync(new SavePickerRequest(
            "Back up TempestOS data", BackupService.BuildManualBackupFileName(DateTimeOffset.UtcNow))).ConfigureAwait(true);
        if (destination is null)
            return;

        try
        {
            var outcome = await _backupService.BackUpNowAsync(_persistenceDatabasePath, destination).ConfigureAwait(true);

            if (_auditRecorder is not null)
            {
                await _auditRecorder.RecordAsync(
                    "Data.BackedUp",
                    new Dictionary<string, string> { ["BackupFile"] = outcome.BackupPath }).ConfigureAwait(true);
            }

            _backupStatus.Text = $"Backed up to '{outcome.BackupPath}' ({outcome.TableCount} table(s)), verified.";
            ActionCompleted?.Invoke(_backupStatus.Text, ActionOutcome.NoChange);
        }
        catch (BackupVerificationException ex)
        {
            _backupStatus.Text = $"Backup failed: {ex.Message}";
            ActionCompleted?.Invoke(_backupStatus.Text, ActionOutcome.Failed);
        }
    }

    /// <summary>
    /// "Restore from backup…" — refused while a project is open; otherwise
    /// an Open picker, an audit row (recorded before the live store closes
    /// — see this method's own remarks below), the store shut down
    /// (<see cref="_prepareForRestartAsync"/>), the current database moved
    /// aside and the backup copied in (<see cref="BackupService.RestoreFromBackup"/>),
    /// and the application restarted (<see cref="RestartProcess"/>) —
    /// `WP 21.5A` (`WP RC.0A` scope item 4).
    /// </summary>
    /// <remarks>
    /// The audit row is written into the database being replaced, not the
    /// one being restored: it is recorded through the current session's own
    /// <see cref="IAuditRecorder"/>, before that session's store closes,
    /// which means it survives as the last entry of whatever database ends
    /// up moved aside to <c>tempest-replaced-…</c> — a permanent record
    /// that this operator restored a backup, from where, and when. The
    /// restored database keeps its own separate history, frozen at the
    /// moment the backup was taken.
    /// </remarks>
    private async Task OnRestoreAsync()
    {
        if (_persistenceDatabasePath is null || _prepareForRestartAsync is null || _filePicker is null)
            return;

        if (_projectContext?.HasProject == true)
        {
            _backupStatus.Text = "Close the open project before restoring a backup.";
            ActionCompleted?.Invoke(_backupStatus.Text, ActionOutcome.Failed);
            return;
        }

        var backupFilePath = await PickBackupFileAsync().ConfigureAwait(true);
        if (backupFilePath is null)
            return;

        if (_auditRecorder is not null)
        {
            await _auditRecorder.RecordAsync(
                "Data.RestoredFromBackup",
                new Dictionary<string, string> { ["BackupFile"] = backupFilePath }).ConfigureAwait(true);
        }

        await _prepareForRestartAsync().ConfigureAwait(true);

        var databaseDirectory = Path.GetDirectoryName(_persistenceDatabasePath)!;
        var replacedPath = Path.Combine(databaseDirectory, BackupService.BuildReplacedDatabaseFileName(DateTimeOffset.UtcNow));
        BackupService.RestoreFromBackup(_persistenceDatabasePath, backupFilePath, replacedPath);

        RestartProcess();
    }

    /// <summary>
    /// Asks <see cref="_filePicker"/> for a backup file's own content and
    /// writes it to a fresh temporary file, returning that file's path —
    /// <see cref="IFilePicker"/>'s own contract (`WP 18.2A`) hands back
    /// bytes, not a path, which is what makes it the one picker abstraction
    /// in this project a headless test can substitute
    /// (<c>evidenceFilePickerOverride</c>); <see cref="BackupService.RestoreFromBackup"/>
    /// needs a path, so this bridges the two rather than inventing a
    /// second, untestable picker seam. <see langword="null"/> means the
    /// user cancelled, or no picker was supplied at all.
    /// </summary>
    private async Task<string?> PickBackupFileAsync()
    {
        if (_filePicker is null)
            return null;

        var picked = await _filePicker.PickFilesAsync(new FilePickerRequest(
            "Restore TempestOS data from a backup", AllowMultiple: false, FileTypeDescription: "SQLite database", Extensions: ["db"])).ConfigureAwait(true);

        if (picked is not [{ } file, ..])
            return null;

        var tempPath = Path.Combine(Path.GetTempPath(), $"tempestos-restore-{Guid.NewGuid():N}.db");
        var bytes = await file.ReadAsync().ConfigureAwait(true);
        await File.WriteAllBytesAsync(tempPath, bytes.ToArray()).ConfigureAwait(true);
        return tempPath;
    }

    /// <summary>
    /// Shows whatever <see cref="_updateAvailability"/> already knows,
    /// without itself checking the feed — "Check now" (<see cref="OnCheckForUpdatesNowAsync"/>)
    /// is the explicit, operator-initiated check; simply opening Settings
    /// never makes a network call of its own.
    /// </summary>
    private void RefreshUpdateStatus()
    {
        if (_updateService is null)
            return;

        if (!_updateService.IsInstalled)
        {
            _updateStatus.Text = "Not installed via the installer — updates unavailable.";
            _applyUpdateButton.IsVisible = false;
            return;
        }

        if (_updateAvailability?.AvailableVersion is { } version)
        {
            _updateStatus.Text = $"Update to {version} is available.";
            _applyUpdateButton.Content = $"Update to {version}";
            _applyUpdateButton.IsVisible = true;
        }
        else
        {
            _updateStatus.Text = "No update checked yet.";
            _applyUpdateButton.IsVisible = false;
        }
    }

    private async Task OnCheckForUpdatesNowAsync()
    {
        if (_updateService is null)
            return;

        _updateStatus.Text = "Checking…";
        var version = await _updateService.CheckForUpdateAsync().ConfigureAwait(true);

        if (_updateAvailability is not null)
            _updateAvailability.AvailableVersion = version;

        RefreshUpdateStatus();

        ActionCompleted?.Invoke(
            version is { } v ? $"Update to {v} is available." : "No update available.",
            ActionOutcome.NoChange);
    }

    private async Task OnApplyUpdateAsync()
    {
        if (_updateService is null)
            return;

        _updateStatus.Text = "Downloading update…";
        _applyUpdateButton.IsEnabled = false;
        try
        {
            // `ApplyUpdatesAndRestart` ends this process itself once the
            // download completes — control normally never returns here.
            await _updateService.DownloadAndApplyUpdateAsync().ConfigureAwait(true);
        }
        finally
        {
            _applyUpdateButton.IsEnabled = true;
        }
    }

    private static void DefaultRestartProcess()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(executablePath))
                Process.Start(new ProcessStartInfo { FileName = executablePath, UseShellExecute = true })?.Dispose();
        }
        catch
        {
            // Best-effort, as `DefaultOpenFolder` below — a restart that
            // could not relaunch still leaves the operator with a restored
            // database on disk; they can start TempestOS again by hand.
        }
        finally
        {
            Environment.Exit(0);
        }
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
        _settings.CheckForUpdatesOnLaunch = _checkForUpdatesOnLaunch.IsChecked ?? false;
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

        if (_organisationIdentity is not null)
        {
            _organisationIdentity.LegalName = _orgLegalName.Text ?? string.Empty;
            _organisationIdentity.CompanyNumber = _orgCompanyNumber.Text ?? string.Empty;
            _organisationIdentity.Website = _orgWebsite.Text ?? string.Empty;
            _organisationIdentity.AddressLine1 = _orgAddressLine1.Text ?? string.Empty;
            _organisationIdentity.AddressLine2 = _orgAddressLine2.Text ?? string.Empty;
            _organisationIdentity.Email = _orgEmail.Text ?? string.Empty;
            _organisationIdentity.Phone = _orgPhone.Text ?? string.Empty;
            _organisationIdentity.BankSortCode = _orgBankSortCode.Text ?? string.Empty;
            _organisationIdentity.BankAccountNumber = _orgBankAccountNumber.Text ?? string.Empty;
            _organisationIdentity.BankAccountName = _orgBankAccountName.Text ?? string.Empty;
            _organisationIdentity.BankIban = _orgBankIban.Text ?? string.Empty;
            await _organisationIdentity.SaveAsync().ConfigureAwait(true);
        }

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
        _loadingInvoicingSection = true;
        try
        {
            SelectInvoicingConnector(connectorValue);
        }
        finally
        {
            _loadingInvoicingSection = false;
        }

        await LoadCredentialFieldsAsync(SelectedInvoicingProvider()).ConfigureAwait(true);

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

    /// <summary>
    /// <em>Authorise</em> (`WP 21.6P`). Until this Work Package the button
    /// only re-read the stored state — <see cref="Tempest.Core.Invoicing.OAuth.OAuthAuthoriser.AuthoriseAsync"/>
    /// had no caller in the product, so a real provider could never be
    /// signed in to from the running application. Now: the section is
    /// saved first (so the client id in the box is the one the authoriser
    /// reads), and if the running connector is a real provider
    /// (<see cref="IAuthorisableConnector"/>) that is not currently
    /// authorised, the interactive sign-in runs — the system browser opens
    /// on the provider's consent page and this view waits, up to
    /// <see cref="AuthorisationTimeout"/>, for the loopback redirect. The
    /// Fake connector, and an already-authorised provider, only refresh
    /// the state as before. Every outcome is shown in words; nothing
    /// throws past this method.
    /// </summary>
    private async Task OnAuthoriseInvoicingAsync()
    {
        if (_invoicingConnector is null)
            return;

        if (_secretStore is not null)
            await SaveInvoicingSectionAsync().ConfigureAwait(true);

        var selectedProvider = SelectedInvoicingProvider();
        if (!ProviderMatchesRunningConnector(selectedProvider))
        {
            // The saved choice differs from what this process is running —
            // whatever this process runs (the Fake connector on a fresh
            // install, most often). SaveInvoicingSectionAsync has already
            // written the restart wording into the status; refreshing the
            // *running* connector's state here would overwrite it with the
            // Fake's own "Authorised." and mislead the operator into
            // thinking Xero was authorised — found by driving the real
            // application (the overnight acceptance campaign, 2026-09-15).
            ActionCompleted?.Invoke(_invoicingAuthorisationStatus.Text ?? "Restart required.", ActionOutcome.NoChange);
            return;
        }

        if (_invoicingConnector is IAuthorisableConnector authorisable)
        {
            var before = await _invoicingConnector.AuthorisationStateAsync().ConfigureAwait(true);
            if (before.Status != ConnectorAuthorisation.Authorised)
            {
                var outcome = await RunInteractiveAuthorisationAsync(authorisable).ConfigureAwait(true);
                if (outcome is not null)
                {
                    ActionCompleted?.Invoke(outcome, ActionOutcome.Failed);
                    return;
                }
            }
        }

        await RefreshInvoicingAuthorisationStatusAsync().ConfigureAwait(true);
        ActionCompleted?.Invoke(_invoicingAuthorisationStatus.Text ?? "Authorisation checked.", ActionOutcome.NoChange);
    }

    /// <summary>Runs the browser round trip; <see langword="null"/> on success, otherwise the failure text already shown in the status.</summary>
    private async Task<string?> RunInteractiveAuthorisationAsync(IAuthorisableConnector authorisable)
    {
        _invoicingAuthoriseButton.IsEnabled = false;
        _invoicingAuthorisationStatus.Text =
            $"Waiting for you to sign in to {_invoicingConnector!.Name} in your browser (up to {AuthorisationTimeout.TotalMinutes:0} minutes)…";

        try
        {
            using var timeout = new CancellationTokenSource(AuthorisationTimeout);
            var result = await authorisable.AuthoriseAsync(timeout.Token).ConfigureAwait(true);

            if (result.Outcome == Tempest.Core.Invoicing.OAuth.OAuthOutcome.Ok)
                return null;

            var failure = result.Outcome == Tempest.Core.Invoicing.OAuth.OAuthOutcome.NotConfigured
                ? $"Not configured. Enter the {_invoicingConnector.Name} app's client id (and its secret, if it has one), Save, then Authorise."
                : $"Authorisation failed. {result.Reason}";
            _invoicingAuthorisationStatus.Text = failure;
            return failure;
        }
        catch (OperationCanceledException)
        {
            var failure = $"No sign-in completed within {AuthorisationTimeout.TotalMinutes:0} minutes. Try Authorise again.";
            _invoicingAuthorisationStatus.Text = failure;
            return failure;
        }
        finally
        {
            _invoicingAuthoriseButton.IsEnabled = true;
        }
    }

    private string SelectedInvoicingProvider() =>
        (_invoicingConnectorSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "Fake";

    /// <summary>Whether the selector names the connector this process is running — the selector's tags carry no spaces ("QuickBooksOnline"), the connector's <see cref="IInvoicingConnector.Name"/> may ("QuickBooks Online").</summary>
    private bool ProviderMatchesRunningConnector(string provider) =>
        _invoicingConnector is not null
        && string.Equals(
            provider.Replace(" ", string.Empty, StringComparison.Ordinal),
            _invoicingConnector.Name.Replace(" ", string.Empty, StringComparison.Ordinal),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Shows the chosen provider's own stored client id and whether a secret is held — the provider key first, the legacy provider-less key as the fallback (`WP 21.6P`).</summary>
    private async Task LoadCredentialFieldsAsync(string provider)
    {
        if (_secretStore is null)
            return;

        var storedClientId = await _secretStore.GetAsync(ClientIdSecretKeyFor(provider)).ConfigureAwait(true)
            ?? await _secretStore.GetAsync(InvoicingClientIdSecretKey).ConfigureAwait(true);
        _invoicingClientId.Text = storedClientId ?? string.Empty;

        var hasStoredSecret = await _secretStore.GetAsync(ClientSecretSecretKeyFor(provider)).ConfigureAwait(true) is not null
            || await _secretStore.GetAsync(InvoicingClientSecretSecretKey).ConfigureAwait(true) is not null;
        _invoicingClientSecret.Text = string.Empty;
        _invoicingClientSecret.Watermark = hasStoredSecret ? "(unchanged)" : string.Empty;
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

        // `WP 21.6P`: stored under the provider's own key — the one
        // `OAuthAuthoriser` actually reads (`Invoicing:<Provider>:ClientId`,
        // `ADR-0151`). The legacy provider-less keys are removed once the
        // value is safely under the provider's key, so nothing is read from
        // them again.
        var clientId = _invoicingClientId.Text ?? string.Empty;
        if (string.IsNullOrEmpty(clientId))
            await _secretStore.RemoveAsync(ClientIdSecretKeyFor(connectorValue)).ConfigureAwait(true);
        else
            await _secretStore.SetAsync(ClientIdSecretKeyFor(connectorValue), clientId).ConfigureAwait(true);

        if (!string.IsNullOrEmpty(_invoicingClientSecret.Text))
        {
            await _secretStore.SetAsync(ClientSecretSecretKeyFor(connectorValue), _invoicingClientSecret.Text).ConfigureAwait(true);
        }
        else if (await _secretStore.GetAsync(ClientSecretSecretKeyFor(connectorValue)).ConfigureAwait(true) is null
                 && await _secretStore.GetAsync(InvoicingClientSecretSecretKey).ConfigureAwait(true) is { } legacySecret)
        {
            await _secretStore.SetAsync(ClientSecretSecretKeyFor(connectorValue), legacySecret).ConfigureAwait(true);
        }

        await _secretStore.RemoveAsync(InvoicingClientIdSecretKey).ConfigureAwait(true);
        await _secretStore.RemoveAsync(InvoicingClientSecretSecretKey).ConfigureAwait(true);
        _invoicingClientSecret.Text = string.Empty;
        _invoicingClientSecret.Watermark = await _secretStore.GetAsync(ClientSecretSecretKeyFor(connectorValue)).ConfigureAwait(true) is not null
            ? "(unchanged)"
            : string.Empty;

        if (!ProviderMatchesRunningConnector(connectorValue))
        {
            _invoicingAuthorisationStatus.Text =
                $"Saved. Restart TempestOS to use {connectorValue} — this session is running the {_invoicingConnector.Name} connector.";
        }

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
