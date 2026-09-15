using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing;
using Tempest.Core.Persistence;
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
    private readonly IAccountsReadModel? _accountsReadModel;
    private readonly AccountsRefreshService? _accountsRefreshService;
    private readonly IConfigurationProvider _configuration;
    private readonly string _persistenceRootPath;

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

    /// <summary>Relaunches the application and ends this process — real by default, overridable by a test so a restore's own journey test does not actually exit the test host.</summary>
    public Action RestartProcess { get; set; } = DefaultRestartProcess;

    /// <summary>Raised after Save or Authorise completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="SettingsView"/> class.</summary>
    /// <param name="persistenceRootPath">The resolved path of the persistence database, or a plain description when there is none (an in-memory store, in a test) — read-only, shown as-is.</param>
    /// <param name="configuration">Where <c>Identity:DisplayName</c> and <c>Identity:Role</c> are read from, read-only — never a second identity mechanism.</param>
    public SettingsView(
        ThemeService theme, UserSettings settings, ISettingsProvider settingsProvider, IConfigurationProvider configuration, string persistenceRootPath,
        IWorkingPatternProvider? workingPatterns = null, ICurrentPrincipalAccessor? principals = null,
        IInvoicingConnector? invoicingConnector = null, ISecretStore? secretStore = null,
        IAccountsReadModel? accountsReadModel = null, AccountsRefreshService? accountsRefreshService = null,
        string? persistenceDatabasePath = null, IAuditRecorder? auditRecorder = null,
        IProjectContext? projectContext = null, Func<Task>? prepareForRestartAsync = null,
        IUpdateService? updateService = null, UpdateAvailability? updateAvailability = null,
        IFilePicker? filePicker = null)
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
        _persistenceDatabasePath = persistenceDatabasePath;
        _auditRecorder = auditRecorder;
        _projectContext = projectContext;
        _prepareForRestartAsync = prepareForRestartAsync;
        _updateService = updateService;
        _updateAvailability = updateAvailability;
        _filePicker = filePicker;

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
        AutomationProperties.SetName(_accountsRefreshButton, "Refresh accounts reading");
        AutomationProperties.SetName(_saveButton, "Save settings");
        AutomationProperties.SetName(_confirmBeforeDelete, "Confirm before deleting an object");
        AutomationProperties.SetName(_independentCheckRequired, "Independent check required");
        AutomationProperties.SetName(_invoicingAuthoriseButton, "Authorise invoicing connector");
        AutomationProperties.SetName(_backUpNowButton, "Back up now");
        AutomationProperties.SetName(_restoreButton, "Restore from backup");
        AutomationProperties.SetName(_openBackupsFolderButton, "Open backups folder");
        AutomationProperties.SetName(_checkForUpdatesOnLaunch, "Check for updates automatically on launch");
        AutomationProperties.SetName(_checkForUpdatesNowButton, "Check for updates now");
        AutomationProperties.SetName(_applyUpdateButton, "Apply update");

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

        if (_persistenceDatabasePath is not null && _filePicker is not null)
            body.Children.Add(backup);

        body.Children.Add(principal);
        body.Children.Add(appearance);
        body.Children.Add(notifications);
        body.Children.Add(workflow);
        body.Children.Add(evidence);

        if (_workingPatterns is not null && _principals is not null)
            body.Children.Add(timesheets);

        if (_invoicingConnector is not null && _secretStore is not null)
            body.Children.Add(invoicing);

        if (_updateService is not null)
            body.Children.Add(updates);

        body.Children.Add(saveRow);

        _saveButton.Classes.Add(ChromeStyles.Primary);
        _openPersistenceFolder.Classes.Add(ChromeStyles.Subtle);
        _openBackupsFolderButton.Classes.Add(ChromeStyles.Subtle);
        _openPersistenceFolder.Click += (_, _) => OnOpenPersistenceFolder();
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        _invoicingAuthoriseButton.Click += async (_, _) => await OnAuthoriseInvoicingAsync().ConfigureAwait(true);
        _accountsRefreshButton.Click += async (_, _) => await OnRefreshAccountsAsync().ConfigureAwait(true);
        _backUpNowButton.Click += async (_, _) => await OnBackUpNowAsync().ConfigureAwait(true);
        _restoreButton.Click += async (_, _) => await OnRestoreAsync().ConfigureAwait(true);
        _openBackupsFolderButton.Click += (_, _) => OnOpenBackupsFolder();
        _checkForUpdatesNowButton.Click += async (_, _) => await OnCheckForUpdatesNowAsync().ConfigureAwait(true);
        _applyUpdateButton.Click += async (_, _) => await OnApplyUpdateAsync().ConfigureAwait(true);

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

        if (_persistenceDatabasePath is not null)
            _backupStatus.Text = string.Empty;

        _checkForUpdatesOnLaunch.IsChecked = _settings.CheckForUpdatesOnLaunch;

        if (_updateService is not null)
            RefreshUpdateStatus();

        _savedStatus.Text = string.Empty;
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
