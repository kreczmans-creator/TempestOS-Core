using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Secrets;
using Tempest.Core.Settings;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// What the Settings → Xero section (<see cref="XeroSettingsSection"/>) works
/// with (`v0.24.0` U1). Every member is optional: a missing one hides, or
/// honestly disables, the part of the section that needs it — the same
/// "<see langword="null"/> hides the section" convention
/// <see cref="SettingsView"/> follows.
/// </summary>
public sealed class XeroSettingsSectionServices
{
    /// <summary>The X1 settings reader: the cached reading (no network) and <em>Refresh from Xero</em>.</summary>
    public IXeroSettingsReader? Reader { get; init; }

    /// <summary>Whether Xero can be called now (X6's <see cref="IXeroConnectionState"/>, over <see cref="XeroConnector.AuthorisationStateAsync"/>, which also checks the granted scopes).</summary>
    public IXeroConnectionState? Connection { get; init; }

    /// <summary>Runs the interactive sign-in for <em>Re-authorise</em>; <see langword="null"/> hides the button.</summary>
    public IAuthorisableConnector? Authoriser { get; init; }

    /// <summary>Where the granted-scope record is read from (<c>Invoicing:Xero:GrantedScopes</c>, written by <see cref="OAuthAuthoriser"/>); <see langword="null"/> shows the grant as not recorded.</summary>
    public ISecretStore? SecretStore { get; init; }

    /// <summary>The X6 outbox: the queued and failed counts; <see langword="null"/> hides the sync summary.</summary>
    public IXeroOutbox? Outbox { get; init; }

    /// <summary>The X6 engine: its <see cref="XeroSyncService.RetryAsync"/> (audited, wakes the engine), <see cref="XeroSyncService.NotifyAuthorisedAsync"/> after a re-authorisation, and its <see cref="XeroSyncService.CycleCompleted"/> (a daily settings refresh reaches documents). Optional.</summary>
    public XeroSyncService? SyncService { get; init; }

    /// <summary>The Product Owner's <em>Retry</em> on one Failed entry. <see langword="null"/> uses <see cref="SyncService"/>, else <see cref="Outbox"/>.</summary>
    public Func<Guid, CancellationToken, Task<bool>>? Retry { get; init; }

    /// <summary>
    /// <em>Sync now</em> (`v0.24.0` review-board fix M3): the engine's own
    /// Refresh — every record planned, the queue drained and every status read
    /// back from Xero now, not on the 15-minute timer. <see langword="null"/>
    /// uses <see cref="SyncService"/>'s <see cref="XeroSyncService.RefreshAsync"/>
    /// when there is one; with neither, <em>Refresh from Xero</em> reads the
    /// organisation settings alone and no <em>Sync now</em> is offered.
    /// </summary>
    public Func<CancellationToken, Task<XeroSyncCycleReport>>? SyncNow { get; init; }

    /// <summary>Run after a successful re-authorisation (the engine resumes). <see langword="null"/> uses <see cref="SyncService"/> when there is one.</summary>
    public Func<CancellationToken, Task>? AfterAuthorised { get; init; }

    /// <summary>Customers and suppliers, for the "General expenses" contact choice (Q3); <see langword="null"/> offers only the current choice.</summary>
    public IOrganisationCatalog? Organisations { get; init; }

    /// <summary>Where a change of <em>Allow live organisation</em> is audited (D7).</summary>
    public IAuditRecorder? Audit { get; init; }

    /// <summary>Settings → Organisation: documents use the company details from Xero when a reading exists (design §8).</summary>
    public OrganisationIdentitySettings? Identity { get; init; }

    /// <summary>The time zone "read at" is shown in; <see langword="null"/> for the machine's own.</summary>
    public TimeZoneInfo? TimeZone { get; init; }

    /// <summary>How long <em>Re-authorise</em> waits for the browser sign-in.</summary>
    public TimeSpan AuthorisationTimeout { get; init; } = SettingsView.AuthorisationTimeout;

    /// <summary>
    /// The Xero services the host registered, or <see langword="null"/> when
    /// Xero is not the running connector (no <see cref="IXeroSettingsReader"/>
    /// is registered) — then Settings shows no Xero section.
    /// </summary>
    /// <param name="services">The host's built container.</param>
    /// <param name="identity">Settings → Organisation, so documents use the company details from Xero.</param>
    public static XeroSettingsSectionServices? FromServices(ITempestServiceProvider services, OrganisationIdentitySettings? identity = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (TryResolve<IXeroSettingsReader>(services) is not { } reader)
            return null;

        return new XeroSettingsSectionServices
        {
            Reader = reader,
            Connection = TryResolve<IXeroConnectionState>(services),
            Authoriser = TryResolve<IInvoicingConnector>(services) as XeroConnector,
            SecretStore = TryResolve<ISecretStore>(services),
            Outbox = TryResolve<IXeroOutbox>(services),
            SyncService = TryResolve<XeroSyncService>(services),
            Organisations = TryResolve<IOrganisationCatalog>(services),
            Audit = TryResolve<IAuditRecorder>(services),
            Identity = identity,
        };
    }

    private static T? TryResolve<T>(ITempestServiceProvider services)
        where T : class
    {
        try
        {
            return services.GetService(typeof(T)) as T;
        }
        catch (ServiceNotRegisteredException)
        {
            return null;
        }
    }
}

/// <summary>
/// Settings → Xero (`v0.24.0` U1, design §8, §11; `ADR-0162`): the
/// connection (granted against required scopes, <em>Re-authorise</em>), the
/// organisation and whether it is Xero's Demo Company, the
/// <em>Allow live organisation</em> switch (D7: off by default, warned,
/// audited), <em>Refresh from Xero</em> with the company details "from Xero,
/// read at …", the tax-type override per VAT rate and side, the sales and
/// per-expense-category account codes (Q10, the Demo Company's codes by
/// default), the "General expenses" contact for bills (Q3), whether invoice
/// PDFs show on Xero's online invoice (Q5, off by default), and the sync
/// summary — queued and failed counts with <em>Retry all</em>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Offline first.</b> <see cref="RefreshAsync"/> reads only what is held
/// locally — the cached reading, Settings, the outbox, the stored grant — so
/// opening Settings never calls Xero; only <em>Refresh from Xero</em> and
/// <em>Re-authorise</em> do, and neither blocks the UI thread.
/// </para>
/// <para>
/// <b>Saved with the page.</b> As every other Settings section, the choices
/// apply when Settings → Save is pressed (<see cref="SaveAsync"/>); the
/// actions (<em>Refresh from Xero</em>, <em>Re-authorise</em>,
/// <em>Retry all</em>) act at once.
/// </para>
/// </remarks>
public sealed class XeroSettingsSection : UserControl
{
    /// <summary>
    /// The audit action recorded when <em>Allow live organisation</em> changes
    /// (D7), with <c>Subject</c>, <c>OldValue</c> and <c>NewValue</c>
    /// ("On"/"Off") — the one name the design (§6.7), the runbook (XG7) and the
    /// setup guide use (`v0.24.0` review-board fix m2). Turning the switch off
    /// writes the same action with <c>NewValue</c> "Off".
    /// </summary>
    public const string AllowLiveOrganisationChangedAction = "xero.live-organisation.allowed";

    /// <summary>The status after <em>Re-authorise</em> signed in to another organisation: its pickers were reloaded, so choices not yet saved were reset.</summary>
    public const string ReauthorisedIntoAnotherOrganisationStatus =
        "Xero re-authorised for another organisation. Its tax types and accounts were reloaded; any choices not yet saved were reset.";

    /// <summary>The status after a Save while the section had not finished loading: <em>IncludeOnline</em> and <em>Allow live organisation</em> were not saved (the stored values are kept).</summary>
    public const string SwitchesNotSavedStatus =
        "Include online and Allow live organisation were not saved: the Xero section did not finish loading. Reopen Settings and save again.";

    /// <summary>The automation name of the <em>Allow live organisation</em> switch.</summary>
    public const string AllowLiveOrganisationName = "Allow live organisation";

    /// <summary>The <em>Allow live organisation</em> switch's own wording.</summary>
    public const string AllowLiveOrganisationLabel =
        "Allow live organisation — let TempestOS write drafts to a Xero organisation that is not Xero's Demo Company.";

    /// <summary>The warning shown beneath the switch.</summary>
    public const string AllowLiveOrganisationWarning =
        "Warning: with this on, TempestOS writes draft quotes, invoices, purchase orders and bills into your real Xero books. "
        + "Leave it off until the Demo Company run-through has passed. Every change of this switch is audited.";

    /// <summary>The automation name of the <em>Refresh from Xero</em> button.</summary>
    public const string RefreshName = "Refresh from Xero";

    /// <summary>The automation name of the <em>Sync now</em> button (review-board fix M3).</summary>
    public const string SyncNowName = "Sync now with Xero";

    /// <summary>The automation name of the <em>Re-authorise</em> button.</summary>
    public const string ReauthoriseName = "Re-authorise Xero";

    /// <summary>The automation name of the <em>Retry all</em> button.</summary>
    public const string RetryAllName = "Retry all failed Xero writes";

    /// <summary>The automation name of the sales account picker.</summary>
    public const string SalesAccountName = "Xero sales account";

    /// <summary>The automation name of the "General expenses" contact picker (Q3).</summary>
    public const string GeneralExpensesContactName = "Xero General expenses contact";

    /// <summary>The automation name of the <c>IncludeOnline</c> switch (Q5).</summary>
    public const string IncludeOnlineName = "Show invoice PDFs on Xero's online invoice";

    /// <summary>The automation name of the company-details note ("Company details from Xero, read at …").</summary>
    public const string ReadingNoteName = "Company details from Xero, read at";

    private readonly ISettingsProvider _settings;
    private readonly XeroSettingsSectionServices _services;
    private readonly Func<Guid, CancellationToken, Task<bool>>? _retry;
    private readonly Func<CancellationToken, Task>? _afterAuthorised;
    private readonly Func<CancellationToken, Task<XeroSyncCycleReport>>? _syncNow;

    private readonly TextBlock _connectionStatus = Body();
    private readonly TextBlock _scopesStatus = Caption();
    private readonly Button _reauthoriseButton = new() { Content = "Re-authorise", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _organisationName = new() { FontSize = DesignTokens.FontSizeBody, FontWeight = DesignTokens.WeightLabel, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _demoCompanyFlag = Body();
    private readonly CheckBox _allowLiveOrganisation = new() { Content = new TextBlock { Text = AllowLiveOrganisationLabel, TextWrapping = TextWrapping.Wrap }, MaxWidth = 720 };
    private readonly TextBlock _allowLiveWarning = new() { Text = AllowLiveOrganisationWarning, FontSize = DesignTokens.FontSizeCaption, FontWeight = DesignTokens.WeightLabel, TextWrapping = TextWrapping.Wrap, MaxWidth = 720 };
    private readonly TextBlock _liveOrganisationStatus = Caption();
    private readonly Button _refreshButton = new() { Content = "Refresh from Xero", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _readingNote = Caption();
    private readonly TextBlock _companyDetails = Body();
    private readonly Dictionary<(VatTaxDirection Direction, VatRate Rate), ComboBox> _taxTypes = [];
    private readonly ComboBox _salesAccount = Picker();
    private readonly Dictionary<ExpenseCategory, ComboBox> _expenseAccounts = [];
    private readonly TextBlock _mappingProblems = Caption();
    private readonly ComboBox _generalExpensesContact = Picker();
    private readonly CheckBox _includeOnline = new() { Content = "Show attached invoice PDFs to the client on Xero's online invoice (off: kept internal)" };
    private readonly TextBlock _syncSummary = Body();
    private readonly Button _retryAllButton = new() { Content = "Retry all", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _syncNowButton = new() { Content = "Sync now", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _status = Caption();

    private XeroSettingsReading? _reading;

    // Set once RefreshAsync has filled the IncludeOnline and Allow-live
    // checkboxes from Settings; until then SaveAsync leaves both keys alone,
    // so an unfilled (unchecked) box never overwrites a stored "True".
    private bool _loaded;
    private bool _definitionsEnsured;
    private int _failedCount;

    /// <summary>Raised after <em>Refresh from Xero</em>, <em>Re-authorise</em> or <em>Retry all</em> completes — the Desktop's own <c>ActionCompleted</c> convention.</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Raised when the reading shown changes (loaded, or refreshed from Xero), so Settings → Organisation can say where documents take the company details from.</summary>
    public event Action<XeroCompanyDetails?>? ReadingChanged;

    /// <summary>Initialises a new instance of the <see cref="XeroSettingsSection"/> class.</summary>
    /// <param name="settings">Where every Xero choice is kept.</param>
    /// <param name="services">What the section works with (<see cref="XeroSettingsSectionServices.FromServices"/> in the application).</param>
    public XeroSettingsSection(ISettingsProvider settings, XeroSettingsSectionServices services)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(services);

        _settings = settings;
        _services = services;
        _retry = services.Retry
            ?? (services.SyncService is { } sync ? sync.RetryAsync
                : services.Outbox is { } outbox ? outbox.RetryAsync
                : null);
        _afterAuthorised = services.AfterAuthorised
            ?? (services.SyncService is { } engine ? async ct => await engine.NotifyAuthorisedAsync(ct).ConfigureAwait(false) : null);
        _syncNow = services.SyncNow ?? (services.SyncService is { } sync2 ? sync2.RefreshAsync : null);

        AutomationProperties.SetName(_connectionStatus, "Xero connection");
        AutomationProperties.SetName(_scopesStatus, "Xero scopes");
        AutomationProperties.SetName(_reauthoriseButton, ReauthoriseName);
        AutomationProperties.SetName(_organisationName, "Xero organisation");
        AutomationProperties.SetName(_demoCompanyFlag, "Xero Demo Company");
        AutomationProperties.SetName(_allowLiveOrganisation, AllowLiveOrganisationName);
        AutomationProperties.SetName(_allowLiveWarning, "Allow live organisation warning");
        AutomationProperties.SetName(_liveOrganisationStatus, "Live organisation status");
        AutomationProperties.SetName(_refreshButton, RefreshName);
        AutomationProperties.SetName(_readingNote, ReadingNoteName);
        AutomationProperties.SetName(_companyDetails, "Company details from Xero");
        AutomationProperties.SetName(_salesAccount, SalesAccountName);
        AutomationProperties.SetName(_mappingProblems, "Xero mapping problems");
        AutomationProperties.SetName(_generalExpensesContact, GeneralExpensesContactName);
        AutomationProperties.SetName(_includeOnline, IncludeOnlineName);
        AutomationProperties.SetName(_syncSummary, "Xero sync summary");
        AutomationProperties.SetName(_retryAllButton, RetryAllName);
        AutomationProperties.SetName(_syncNowButton, SyncNowName);
        ToolTip.SetTip(_syncNowButton, "Send what is queued and read every status back from Xero now, rather than on the 15-minute timer.");
        ToolTip.SetTip(_refreshButton, "Read the organisation, tax rates and accounts from Xero — and sync now: send what is queued and read every status back.");
        AutomationProperties.SetName(_status, "Xero status");
        ToolTip.SetTip(_allowLiveOrganisation, "Off (the default): TempestOS writes only to Xero's Demo Company. On: also to the organisation it is connected to. Audited.");
        ToolTip.SetTip(_includeOnline, "Q5: off by default — a PDF attached to a Xero invoice is kept internal to Xero.");

        foreach (var direction in Enum.GetValues<VatTaxDirection>())
        {
            foreach (var rate in Enum.GetValues<VatRate>())
            {
                var combo = Picker();
                AutomationProperties.SetName(combo, TaxTypeName(direction, rate));
                _taxTypes[(direction, rate)] = combo;
            }
        }

        foreach (var category in Enum.GetValues<ExpenseCategory>())
        {
            var combo = Picker();
            AutomationProperties.SetName(combo, ExpenseAccountName(category));
            _expenseAccounts[category] = combo;
        }

        _reauthoriseButton.Classes.Add(ChromeStyles.Subtle);
        _retryAllButton.Classes.Add(ChromeStyles.Subtle);
        _syncNowButton.Classes.Add(ChromeStyles.Subtle);
        _syncNowButton.Click += async (_, _) => await SyncNowAsync().ConfigureAwait(true);
        _reauthoriseButton.Click += async (_, _) => await ReauthoriseAsync().ConfigureAwait(true);
        _refreshButton.Click += async (_, _) => await RefreshFromXeroAsync().ConfigureAwait(true);
        _retryAllButton.Click += async (_, _) => await RetryAllAsync().ConfigureAwait(true);
        _allowLiveOrganisation.IsCheckedChanged += (_, _) => DescribeLiveOrganisation();

        Content = BuildLayout();
        AutomationProperties.SetName(this, "Xero settings");
    }

    /// <summary>The reading shown — the cached one, or the one <em>Refresh from Xero</em> just read; <see langword="null"/> when Xero has never been read.</summary>
    public XeroSettingsReading? Reading => _reading;

    /// <summary>The automation name of the tax-type picker for <paramref name="rate"/> on <paramref name="direction"/> lines.</summary>
    /// <param name="direction">Sales or purchases.</param>
    /// <param name="rate">The VAT rate.</param>
    public static string TaxTypeName(VatTaxDirection direction, VatRate rate) =>
        $"Xero tax type for {rate} VAT on {(direction == VatTaxDirection.Sales ? "sales" : "purchases")}";

    /// <summary>The automation name of the account picker for <paramref name="category"/> expenses.</summary>
    /// <param name="category">The expense category.</param>
    public static string ExpenseAccountName(ExpenseCategory category) => $"Xero account for {category} expenses";

    /// <summary>
    /// Re-reads everything shown from what is held locally — the cached
    /// reading, Settings, the outbox and the stored grant. Never calls Xero.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        EnsureDefinitions();

        var reading = _services.Reader is null ? null : await _services.Reader.ReadCachedAsync(cancellationToken).ConfigureAwait(true);
        await ShowReadingAsync(reading, cancellationToken).ConfigureAwait(true);

        _allowLiveOrganisation.IsChecked = await ReadBoolAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, cancellationToken).ConfigureAwait(true);
        _includeOnline.IsChecked = await ReadBoolAsync(XeroInvoiceDrafts.IncludeOnlineSettingKey, cancellationToken).ConfigureAwait(true);
        _loaded = true;
        DescribeLiveOrganisation();

        await LoadGeneralExpensesContactAsync(cancellationToken).ConfigureAwait(true);
        await RefreshConnectionAsync(cancellationToken).ConfigureAwait(true);
        await RefreshSyncSummaryAsync(cancellationToken).ConfigureAwait(true);
        _status.Text = string.Empty;
    }

    /// <summary>
    /// <em>Refresh from Xero</em>: reads the organisation, tax rates and
    /// accounts (three calls, X1), caches them, and shows them; documents use
    /// the new company details from now on. When Xero cannot be read the last
    /// reading stays, and the status says why. With the sync engine present it
    /// is also <em>Sync now</em> (review-board fix M3): the engine's Refresh
    /// runs first — the queue drained and every status read back from Xero, so
    /// a change made in Xero shows now — and its own settings read is the one
    /// shown (the three calls are not made twice).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task RefreshFromXeroAsync(CancellationToken cancellationToken = default)
    {
        if (_services.Reader is not { } reader)
            return;

        EnsureDefinitions();
        _refreshButton.IsEnabled = false;
        _status.Text = "Reading Xero…";
        try
        {
            var (synced, syncProblem) = await RunSyncAsync(cancellationToken).ConfigureAwait(true);
            var result = synced is { SettingsRefreshed: true } && await reader.ReadCachedAsync(cancellationToken).ConfigureAwait(true) is { } cached
                ? ConnectorResult<XeroSettingsReading>.Ok(cached)
                : await reader.RefreshAsync(cancellationToken).ConfigureAwait(true);
            var syncNote = SyncNote(synced, syncProblem);
            if (result.Outcome == ConnectorOutcome.Ok && result.Value is { } fresh)
            {
                await ShowReadingAsync(fresh, cancellationToken).ConfigureAwait(true);
                DescribeLiveOrganisation();
                _status.Text = $"Read {fresh.Organisation.Name} from Xero.{syncNote}";
                ActionCompleted?.Invoke(_status.Text, ActionOutcome.Changed);
            }
            else
            {
                var last = _reading is { } held
                    ? $"Showing the last reading ({XeroCompanyDetails.From(held).SourceNote(_services.TimeZone)})."
                    : "Xero has not been read yet.";
                _status.Text = $"Could not read Xero ({DescribeOutcome(result.Outcome)}{(result.Reason is { Length: > 0 } reason ? $": {reason}" : string.Empty)}). {last}{syncNote}";
                ActionCompleted?.Invoke(_status.Text, ActionOutcome.Failed);
            }

            await RefreshConnectionAsync(cancellationToken).ConfigureAwait(true);
            if (synced is not null)
                await RefreshSyncSummaryAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _refreshButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// <em>Sync now</em> (review-board fix M3): the engine's Refresh — every
    /// record planned, the queue drained, every status read back from Xero and
    /// the organisation settings read — now, not on the timer; then the sync
    /// summary and the reading are re-read. Does nothing without the engine.
    /// </summary>
    /// <param name="cancellationToken">Cancels the sync.</param>
    public async Task SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (_syncNow is null)
            return;

        _syncNowButton.IsEnabled = false;
        _status.Text = "Syncing with Xero…";
        try
        {
            var (synced, problem) = await RunSyncAsync(cancellationToken).ConfigureAwait(true);
            if (synced is { SettingsRefreshed: true } && _services.Reader is { } reader && await reader.ReadCachedAsync(cancellationToken).ConfigureAwait(true) is { } cached)
            {
                await ShowReadingAsync(cached, cancellationToken).ConfigureAwait(true);
                DescribeLiveOrganisation();
            }

            await RefreshSyncSummaryAsync(cancellationToken).ConfigureAwait(true);
            _status.Text = SyncNote(synced, problem).Trim();
            ActionCompleted?.Invoke(_status.Text, synced is { ReadBack: not null } ? ActionOutcome.Changed : ActionOutcome.Failed);
        }
        finally
        {
            _syncNowButton.IsEnabled = true;
        }
    }

    private async Task<(XeroSyncCycleReport? Report, string? Problem)> RunSyncAsync(CancellationToken cancellationToken)
    {
        if (_syncNow is null)
            return (null, null);

        try
        {
            return (await _syncNow(cancellationToken).ConfigureAwait(true), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed sync is reported in the status, never thrown out of a click handler.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return (null, ex.GetBaseException().Message);
        }
    }

    /// <summary>The words for what a <em>Sync now</em> did: writes sent and statuses read back, or why it could not read Xero.</summary>
    /// <param name="report">The engine's report; <see langword="null"/> when it did not run.</param>
    /// <param name="problem">Why it failed, when it threw.</param>
    internal static string SyncNote(XeroSyncCycleReport? report, string? problem)
    {
        if (problem is not null)
            return $" Sync with Xero did not run: {problem.TrimEnd().TrimEnd('.')}.";

        if (report is null)
            return string.Empty;

        if (report.ReadBack is not { } readBack)
        {
            return report.Drain.PausedForAuthorisation
                ? " Sync now: Xero needs re-authorising before anything more is sent or read."
                : " Sync now: statuses could not be read back from Xero now (not connected, waiting for authorisation, or asked to slow down).";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $" Synced with Xero now: {report.Drain.Succeeded} write(s) sent, {readBack.Read} status(es) read back, {readBack.Changed.Count} changed.");
    }

    /// <summary>
    /// Saves every choice: the tax types, account codes, "General expenses"
    /// contact, <c>IncludeOnline</c> and <em>Allow live organisation</em> —
    /// the last audited (<see cref="AllowLiveOrganisationChangedAction"/>,
    /// old → new) whenever it changes.
    /// </summary>
    /// <param name="cancellationToken">Cancels the writes.</param>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        EnsureDefinitions();

        foreach (var ((direction, rate), combo) in _taxTypes)
        {
            if (SelectedCode(combo) is { } code)
                await _settings.SetValueAsync(XeroTaxTypeResolver.SettingKey(direction, rate), code, cancellationToken).ConfigureAwait(true);
        }

        if (SelectedCode(_salesAccount) is { Length: > 0 } sales)
            await _settings.SetValueAsync(XeroAccountCodeMap.SalesSettingKey, sales, cancellationToken).ConfigureAwait(true);

        foreach (var (category, combo) in _expenseAccounts)
        {
            if (SelectedCode(combo) is { Length: > 0 } code)
                await _settings.SetValueAsync(XeroAccountCodeMap.ExpenseSettingKey(category), code, cancellationToken).ConfigureAwait(true);
        }

        if (SelectedCode(_generalExpensesContact) is { } contact)
            await new XeroGeneralExpensesContact(_settings).SetAsync(contact, cancellationToken).ConfigureAwait(true);

        // Until RefreshAsync has filled the two checkboxes they show "off",
        // not what is stored: writing them then would silently turn a stored
        // IncludeOnline / Allow-live "True" into "False".
        if (_loaded)
        {
            await SaveSwitchesAsync(cancellationToken).ConfigureAwait(true);
        }
        else
        {
            _status.Text = SwitchesNotSavedStatus;
            ActionCompleted?.Invoke(_status.Text, ActionOutcome.Failed);
        }

        DescribeLiveOrganisation();
        await ShowMappingProblemsAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task SaveSwitchesAsync(CancellationToken cancellationToken)
    {
        await _settings.SetValueAsync(XeroInvoiceDrafts.IncludeOnlineSettingKey, Format(_includeOnline.IsChecked == true), cancellationToken).ConfigureAwait(true);

        // D7: through the one key the write-safety handler reads, and audited
        // on every change (who, when, old → new).
        var allowLive = _allowLiveOrganisation.IsChecked == true;
        var wasAllowed = await ReadBoolAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, cancellationToken).ConfigureAwait(true);
        if (allowLive != wasAllowed)
        {
            await _settings.SetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, Format(allowLive), cancellationToken).ConfigureAwait(true);
            if (_services.Audit is { } audit)
            {
                await audit.RecordAsync(
                    AllowLiveOrganisationChangedAction,
                    new Dictionary<string, string>
                    {
                        ["Subject"] = XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey,
                        ["OldValue"] = OnOff(wasAllowed),
                        ["NewValue"] = OnOff(allowLive),
                        ["Organisation"] = _reading?.Organisation.Name ?? "not read yet",
                        ["IsDemoCompany"] = _reading is { } reading ? Format(reading.Organisation.IsDemoCompany) : "unknown",
                    },
                    cancellationToken).ConfigureAwait(true);
            }
        }
    }

    /// <summary><em>Retry all</em>: every Failed outbox entry back to Pending (each keeps its key; audited and the engine woken when the engine is present), then the summary re-read.</summary>
    /// <param name="cancellationToken">Cancels the retries.</param>
    public async Task RetryAllAsync(CancellationToken cancellationToken = default)
    {
        if (_services.Outbox is not { } outbox || _retry is null)
            return;

        _retryAllButton.IsEnabled = false;
        try
        {
            var failed = await outbox.ListAsync([XeroOutboxState.Failed], cancellationToken).ConfigureAwait(true);
            var retried = 0;
            foreach (var entry in failed)
            {
                if (await _retry(entry.Id, cancellationToken).ConfigureAwait(true))
                    retried++;
            }

            await RefreshSyncSummaryAsync(cancellationToken).ConfigureAwait(true);
            _status.Text = retried == 0
                ? "Nothing to retry."
                : string.Create(CultureInfo.InvariantCulture, $"Queued {retried} failed write(s) to Xero again.");
            ActionCompleted?.Invoke(_status.Text, retried == 0 ? ActionOutcome.NoChange : ActionOutcome.Changed);
        }
        finally
        {
            _retryAllButton.IsEnabled = _services.Outbox is not null && _retry is not null && _failedCount > 0;
        }
    }

    /// <summary><em>Re-authorise</em>: the browser sign-in at Xero (waits up to <see cref="XeroSettingsSectionServices.AuthorisationTimeout"/>); on success the engine resumes and the connection is re-read.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task ReauthoriseAsync(CancellationToken cancellationToken = default)
    {
        if (_services.Authoriser is not { } authoriser)
            return;

        _reauthoriseButton.IsEnabled = false;
        _status.Text = $"Waiting for you to sign in to Xero in your browser (up to {_services.AuthorisationTimeout.TotalMinutes:0} minutes)…";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_services.AuthorisationTimeout);
            var result = await authoriser.AuthoriseAsync(timeout.Token).ConfigureAwait(true);

            if (result.Outcome == OAuthOutcome.Ok)
            {
                if (_afterAuthorised is not null)
                    await _afterAuthorised(cancellationToken).ConfigureAwait(true);

                // The sign-in may have chosen another organisation, which has
                // no cached reading yet: re-read the cache so the section and
                // every document stop showing the previous organisation's
                // name, Demo flag and company details (falling back to
                // Settings → Organisation until Xero is read). Only then are
                // the pickers reloaded — the same organisation keeps any
                // choices not yet saved — and the person is told so.
                var reloaded = false;
                if (_services.Reader is { } reader)
                {
                    var cached = await reader.ReadCachedAsync(cancellationToken).ConfigureAwait(true);
                    if (!string.Equals(cached?.TenantId, _reading?.TenantId, StringComparison.Ordinal))
                    {
                        await ShowReadingAsync(cached, cancellationToken).ConfigureAwait(true);
                        DescribeLiveOrganisation();
                        reloaded = true;
                    }
                }

                await RefreshConnectionAsync(cancellationToken).ConfigureAwait(true);
                await RefreshSyncSummaryAsync(cancellationToken).ConfigureAwait(true);
                _status.Text = reloaded ? ReauthorisedIntoAnotherOrganisationStatus : "Xero re-authorised.";
                ActionCompleted?.Invoke(_status.Text, ActionOutcome.Changed);
                return;
            }

            _status.Text = result.Outcome == OAuthOutcome.NotConfigured
                ? "Not configured. Enter the Xero app's client id under Connector authorisation, Save, then Re-authorise."
                : $"Re-authorisation failed. {result.Reason}";
            ActionCompleted?.Invoke(_status.Text, ActionOutcome.Failed);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "No sign-in completed in time. Try Re-authorise again.";
            ActionCompleted?.Invoke(_status.Text, ActionOutcome.Failed);
        }
        finally
        {
            _reauthoriseButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Makes every document use the company details from Xero from start-up:
    /// reads the cached reading now, in the background (no network, never on
    /// the UI thread), and again whenever the sync engine refreshes Xero's
    /// settings (daily). Does nothing without a reader and Settings →
    /// Organisation.
    /// </summary>
    public void KeepDocumentIdentityCurrent()
    {
        if (_services.Reader is not { } reader || _services.Identity is not { } identity)
            return;

        _ = LoadIdentityQuietlyAsync(identity, reader);

        if (_services.SyncService is { } sync)
        {
            sync.CycleCompleted += report =>
            {
                if (report.SettingsRefreshed)
                    _ = LoadIdentityQuietlyAsync(identity, reader);
            };
        }
    }

    private static async Task LoadIdentityQuietlyAsync(OrganisationIdentitySettings identity, IXeroSettingsReader reader)
    {
        try
        {
            await Task.Run(() => identity.LoadXeroCompanyDetailsAsync(reader)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort: documents keep whatever they were using — the
            // typed Settings → Organisation values when nothing was read.
        }
    }

    private Control BuildLayout()
    {
        var stack = new StackPanel { Spacing = DesignTokens.SpaceSm };

        // ---- Connection ----
        stack.Children.Add(Heading("Connection"));
        var connectionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        connectionRow.Children.Add(_connectionStatus);
        if (_services.Authoriser is not null)
            connectionRow.Children.Add(_reauthoriseButton);
        stack.Children.Add(connectionRow);
        stack.Children.Add(_scopesStatus);

        // ---- Organisation, D7 ----
        stack.Children.Add(Heading("Organisation"));
        stack.Children.Add(_organisationName);
        stack.Children.Add(_demoCompanyFlag);
        stack.Children.Add(_allowLiveOrganisation);
        stack.Children.Add(_allowLiveWarning);
        stack.Children.Add(_liveOrganisationStatus);

        // ---- Company details (X1) ----
        stack.Children.Add(Heading("Company details"));
        var refreshRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        if (_services.Reader is not null)
            refreshRow.Children.Add(_refreshButton);
        refreshRow.Children.Add(_readingNote);
        _readingNote.VerticalAlignment = VerticalAlignment.Center;
        stack.Children.Add(refreshRow);
        stack.Children.Add(_companyDetails);

        // ---- Tax types ----
        stack.Children.Add(Heading("Tax types (per VAT rate)"));
        var taxGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,*"), ColumnSpacing = DesignTokens.SpaceMd, RowSpacing = DesignTokens.SpaceXs };
        AddGridRow(taxGrid, new TextBlock { Text = "VAT rate", FontWeight = DesignTokens.WeightLabel }, new TextBlock { Text = "Sales", FontWeight = DesignTokens.WeightLabel }, new TextBlock { Text = "Purchases", FontWeight = DesignTokens.WeightLabel });
        foreach (var rate in Enum.GetValues<VatRate>())
            AddGridRow(taxGrid, new TextBlock { Text = rate.DisplayName(), VerticalAlignment = VerticalAlignment.Center }, _taxTypes[(VatTaxDirection.Sales, rate)], _taxTypes[(VatTaxDirection.Purchases, rate)]);
        stack.Children.Add(taxGrid);

        // ---- Account codes (Q10) ----
        stack.Children.Add(Heading("Account codes"));
        stack.Children.Add(Row("Sales (quotes and invoices)", _salesAccount));
        foreach (var (category, combo) in _expenseAccounts)
            stack.Children.Add(Row($"{category} expenses (bills and purchase orders)", combo));
        stack.Children.Add(_mappingProblems);

        // ---- Bills and invoices (Q3, Q5) ----
        stack.Children.Add(Heading("Bills and invoices"));
        stack.Children.Add(Row("\"General expenses\" contact (bills for expenses with no supplier)", _generalExpensesContact));
        stack.Children.Add(_includeOnline);

        // ---- Sync (X6) ----
        if (_services.Outbox is not null)
        {
            stack.Children.Add(Heading("Sync"));
            var syncRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
            syncRow.Children.Add(_syncSummary);
            _syncSummary.VerticalAlignment = VerticalAlignment.Center;
            if (_retry is not null)
                syncRow.Children.Add(_retryAllButton);
            if (_syncNow is not null)
                syncRow.Children.Add(_syncNowButton);
            stack.Children.Add(syncRow);
        }

        stack.Children.Add(_status);
        return stack;
    }

    private void EnsureDefinitions()
    {
        if (_definitionsEnsured)
            return;

        _definitionsEnsured = true;
        XeroWriteSafetyHandler.EnsureAllowLiveOrganisationDefinition(_settings);
        XeroTaxTypeResolver.EnsureDefinitions(_settings);
        XeroAccountCodeMap.EnsureDefinitions(_settings);
        XeroGeneralExpensesContact.EnsureDefinition(_settings);
        XeroInvoiceDrafts.EnsureDefinitions(_settings);
    }

    private async Task ShowReadingAsync(XeroSettingsReading? reading, CancellationToken cancellationToken)
    {
        _reading = reading;
        var details = reading is null ? null : XeroCompanyDetails.From(reading);

        if (reading is null || details is null)
        {
            _organisationName.Text = "Organisation: not read yet.";
            _demoCompanyFlag.Text = "Demo Company: unknown until Xero is read.";
            _readingNote.Text = "Company details: not read from Xero yet — documents use Settings → Organisation.";
            _companyDetails.Text = string.Empty;
        }
        else
        {
            _organisationName.Text = $"Organisation: {reading.Organisation.Name}";
            _demoCompanyFlag.Text = reading.Organisation.IsDemoCompany
                ? "Demo Company: yes — Xero's Demo Company; TempestOS may write drafts to it."
                : "Demo Company: no — a live organisation.";
            _readingNote.Text = $"Company details {details.SourceNote(_services.TimeZone)}";
            _companyDetails.Text = DescribeCompany(details);
        }

        _services.Identity?.UseXeroReading(reading);
        ReadingChanged?.Invoke(details);

        await LoadTaxTypesAsync(reading, cancellationToken).ConfigureAwait(true);
        await LoadAccountsAsync(reading, cancellationToken).ConfigureAwait(true);
        await ShowMappingProblemsAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task LoadTaxTypesAsync(XeroSettingsReading? reading, CancellationToken cancellationToken)
    {
        foreach (var direction in Enum.GetValues<VatTaxDirection>())
        {
            var choices = reading is null ? [] : XeroTaxTypeResolver.Choices(reading, direction);
            foreach (var rate in Enum.GetValues<VatRate>())
            {
                var combo = _taxTypes[(direction, rate)];
                var stored = (await ReadStringAsync(XeroTaxTypeResolver.SettingKey(direction, rate), cancellationToken).ConfigureAwait(true)).Trim();
                VatRateTaxTypeMapping.TryMap(rate, direction, out var defaultCode);

                combo.Items.Clear();
                combo.Items.Add(new ComboBoxItem { Content = defaultCode is null ? "Default (none)" : $"Default ({defaultCode})", Tag = string.Empty });
                foreach (var choice in choices)
                {
                    combo.Items.Add(new ComboBoxItem
                    {
                        Content = string.Create(CultureInfo.InvariantCulture, $"{choice.TaxType} — {choice.Name} ({choice.EffectiveRate:0.##}%)"),
                        Tag = choice.TaxType,
                    });
                }

                // The default code (as registered, or blank) shows as "Default".
                if (defaultCode is not null && string.Equals(stored, defaultCode, StringComparison.OrdinalIgnoreCase))
                    stored = string.Empty;

                Select(combo, stored, reading is null ? stored : $"{stored} — not an active {(direction == VatTaxDirection.Sales ? "sales" : "purchases")} tax rate in Xero");
            }
        }
    }

    private async Task LoadAccountsAsync(XeroSettingsReading? reading, CancellationToken cancellationToken)
    {
        await LoadAccountAsync(_salesAccount, XeroAccountCodeMap.SalesSettingKey, XeroAccountPurpose.Sales, reading, cancellationToken).ConfigureAwait(true);
        foreach (var (category, combo) in _expenseAccounts)
            await LoadAccountAsync(combo, XeroAccountCodeMap.ExpenseSettingKey(category), XeroAccountPurpose.Expense, reading, cancellationToken).ConfigureAwait(true);
    }

    private async Task LoadAccountAsync(ComboBox combo, string key, XeroAccountPurpose purpose, XeroSettingsReading? reading, CancellationToken cancellationToken)
    {
        var stored = (await ReadStringAsync(key, cancellationToken).ConfigureAwait(true)).Trim();

        combo.Items.Clear();
        if (reading is not null)
        {
            foreach (var account in XeroAccountCodeMap.Choices(reading, purpose))
                combo.Items.Add(new ComboBoxItem { Content = $"{account.Code} — {account.Name}", Tag = account.Code });
        }

        Select(combo, stored, reading is null ? stored : $"{stored} — not an active {(purpose == XeroAccountPurpose.Sales ? "revenue" : "expense")} account in Xero");
    }

    private async Task ShowMappingProblemsAsync(CancellationToken cancellationToken)
    {
        var problems = new List<string>();

        foreach (var ((direction, rate), _) in _taxTypes)
        {
            var chosen = await ReadStringAsync(XeroTaxTypeResolver.SettingKey(direction, rate), cancellationToken).ConfigureAwait(true);
            var resolution = XeroTaxTypeResolver.Resolve(rate, direction, _reading, chosen);
            if (resolution.IsBlocked && _reading is not null)
                problems.Add($"{rate} VAT on {(direction == VatTaxDirection.Sales ? "sales" : "purchases")}: {resolution.BlockedReason}");
        }

        var sales = XeroAccountCodeMap.Check(await ReadStringAsync(XeroAccountCodeMap.SalesSettingKey, cancellationToken).ConfigureAwait(true), XeroAccountPurpose.Sales, _reading);
        if (sales.IsBlocked && _reading is not null)
            problems.Add($"Sales: {sales.BlockedReason}");

        foreach (var category in _expenseAccounts.Keys)
        {
            var expense = XeroAccountCodeMap.Check(await ReadStringAsync(XeroAccountCodeMap.ExpenseSettingKey(category), cancellationToken).ConfigureAwait(true), XeroAccountPurpose.Expense, _reading);
            if (expense.IsBlocked && _reading is not null)
                problems.Add($"{category} expenses: {expense.BlockedReason}");
        }

        _mappingProblems.Text = _reading is null
            ? "Tax types and account codes are checked against Xero once it has been read; until then every push waits."
            : problems.Count == 0
                ? "Every saved tax type and account code is active in Xero."
                : "Saved choices Xero cannot take (pushes using them are Blocked):\n" + string.Join('\n', problems.Select(p => $"• {p}"));
    }

    private async Task LoadGeneralExpensesContactAsync(CancellationToken cancellationToken)
    {
        var stored = await new XeroGeneralExpensesContact(_settings).ReadAsync(cancellationToken).ConfigureAwait(true) ?? string.Empty;

        _generalExpensesContact.Items.Clear();
        _generalExpensesContact.Items.Add(new ComboBoxItem { Content = "None — a bill for an expense with no supplier waits until one is chosen", Tag = string.Empty });

        if (_services.Organisations is { } organisations)
        {
            var all = await organisations.SearchAsync(new OrganisationQuery(), cancellationToken).ConfigureAwait(true);
            foreach (var record in all.OrderBy(r => r.Definition.Name, StringComparer.CurrentCultureIgnoreCase))
                _generalExpensesContact.Items.Add(new ComboBoxItem { Content = $"{record.Definition.Name} ({record.Definition.Reference})", Tag = record.Definition.Reference });
        }

        Select(_generalExpensesContact, stored, $"{stored} — not found in Customers & suppliers");
    }

    private async Task RefreshConnectionAsync(CancellationToken cancellationToken)
    {
        if (_services.Connection is { } connection)
        {
            try
            {
                var state = await connection.ReadAsync(cancellationToken).ConfigureAwait(true);
                var headline = state.Status switch
                {
                    ConnectorAuthorisation.Authorised => "Connected to Xero.",
                    ConnectorAuthorisation.NotAuthorised => "Not connected — Re-authorise to sign in to Xero.",
                    ConnectorAuthorisation.Expired => "Re-authorise needed.",
                    _ => "Connection unknown.",
                };
                _connectionStatus.Text = state.Detail is { Length: > 0 } detail ? $"{headline} {detail}" : headline;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _connectionStatus.Text = $"Connection unknown: {ex.Message}";
            }
        }
        else
        {
            _connectionStatus.Text = "Connection: not available in this session.";
        }

        IReadOnlyList<string>? granted = null;
        if (_services.SecretStore is { } secrets)
        {
            var raw = await secrets.GetAsync($"Invoicing:Xero:{OAuthAuthoriser.GrantedScopesKeySuffix}", cancellationToken).ConfigureAwait(true);
            granted = raw?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var missing = XeroConnector.FindMissingScopes(granted);
        _scopesStatus.Text =
            $"Required scopes: {string.Join(", ", XeroScopes.Required)}\n"
            + $"Granted: {(granted is null ? "not recorded" : granted.Count == 0 ? "none" : string.Join(", ", granted))}\n"
            + $"Missing: {(missing.Count == 0 ? "none" : string.Join(", ", missing) + " — Re-authorise to grant them")}";
    }

    private async Task RefreshSyncSummaryAsync(CancellationToken cancellationToken)
    {
        if (_services.Outbox is not { } outbox)
            return;

        var queued = await outbox.ListAsync([XeroOutboxState.Pending, XeroOutboxState.InFlight, XeroOutboxState.Unknown], cancellationToken).ConfigureAwait(true);
        var failed = await outbox.ListAsync([XeroOutboxState.Failed], cancellationToken).ConfigureAwait(true);
        var waiting = await outbox.ListAsync([XeroOutboxState.WaitingForAuthorisation], cancellationToken).ConfigureAwait(true);

        _failedCount = failed.Count;
        _syncSummary.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Sync: {queued.Count} queued · {failed.Count} failed · {waiting.Count} waiting for authorisation.");
        _retryAllButton.IsEnabled = failed.Count > 0;
    }

    private void DescribeLiveOrganisation()
    {
        var allowed = _allowLiveOrganisation.IsChecked == true;
        _liveOrganisationStatus.Text = _reading switch
        {
            null => allowed
                ? "Allowed: TempestOS will write to whichever organisation it is connected to."
                : "Off: TempestOS writes only to Xero's Demo Company.",
            { Organisation.IsDemoCompany: true } => "Connected to the Demo Company: writes are allowed whatever this switch says.",
            { } live when allowed => $"Allowed: TempestOS writes drafts to {live.Organisation.Name}, your live organisation.",
            { } live => $"Writes to {live.Organisation.Name} are blocked until this switch is on and saved.",
        };
    }

    private async Task<bool> ReadBoolAsync(string key, CancellationToken cancellationToken) =>
        bool.TryParse(await ReadStringAsync(key, cancellationToken).ConfigureAwait(true), out var value) && value;

    private async Task<string> ReadStringAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await _settings.GetValueAsync(key, cancellationToken).ConfigureAwait(true) ?? string.Empty;
        }
        catch (SettingNotFoundException)
        {
            return string.Empty;
        }
    }

    private static string DescribeCompany(XeroCompanyDetails details)
    {
        var lines = new List<string> { details.Name };
        if (!string.IsNullOrWhiteSpace(details.RegistrationNumber))
            lines.Add($"Company No. {details.RegistrationNumber}");
        if (!string.IsNullOrWhiteSpace(details.VatNumber))
            lines.Add($"VAT No. {details.VatNumber}");
        if (details.AddressLines.Count > 0)
            lines.Add(string.Join(", ", details.AddressLines));
        if (!string.IsNullOrWhiteSpace(details.Phone))
            lines.Add($"Phone {details.Phone}");
        if (!string.IsNullOrWhiteSpace(details.Website))
            lines.Add(details.Website);
        foreach (var bank in details.BankAccounts)
            lines.Add($"Bank: {bank.Name}{(string.IsNullOrWhiteSpace(bank.BankAccountNumber) ? string.Empty : $" {bank.BankAccountNumber}")}");

        return string.Join('\n', lines);
    }

    private static void Select(ComboBox combo, string code, string missingLabel)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, code, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (code.Length == 0)
        {
            combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
            return;
        }

        // Kept as chosen, so Save never silently changes it — and named for
        // what it is when Xero does not hold it.
        var kept = new ComboBoxItem { Content = missingLabel, Tag = code };
        combo.Items.Add(kept);
        combo.SelectedItem = kept;
    }

    private static string? SelectedCode(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag as string;

    private static string DescribeOutcome(ConnectorOutcome outcome) => outcome switch
    {
        ConnectorOutcome.Reauthorise => "re-authorise needed",
        ConnectorOutcome.Unavailable => "Xero unreachable",
        ConnectorOutcome.Rejected => "refused",
        _ => "no answer",
    };

    private static string Format(bool value) => value.ToString(CultureInfo.InvariantCulture);

    private static string OnOff(bool value) => value ? "On" : "Off";

    private static TextBlock Body() => new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };

    private static TextBlock Caption() => new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    private static ComboBox Picker() => new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 260 };

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontSizeBody,
        FontWeight = DesignTokens.WeightHeading,
        Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0),
    };

    private static Control Row(string label, Control value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(value, 1);
        row.Children.Add(text);
        row.Children.Add(value);
        return row;
    }

    private static void AddGridRow(Grid grid, Control first, Control second, Control third)
    {
        var row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        foreach (var (control, column) in new[] { (first, 0), (second, 1), (third, 2) })
        {
            Grid.SetRow(control, row);
            Grid.SetColumn(control, column);
            grid.Children.Add(control);
        }
    }
}
