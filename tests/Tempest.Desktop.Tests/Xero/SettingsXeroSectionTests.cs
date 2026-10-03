using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.ReferenceData;
using Tempest.Core.Secrets;
using Tempest.Core.Settings;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U1 (design §8, §11; `ADR-0162`), headless: Settings → Xero over
/// the real Settings, audit and Customers &amp; Suppliers of a
/// <see cref="WorkspaceHost"/>, and fakes of the §12 Xero interfaces
/// (<see cref="IXeroSettingsReader"/>, <see cref="IXeroOutbox"/>,
/// <see cref="IXeroConnectionState"/>). Offline shows the last reading
/// without calling Xero; the D7 switch is off by default, warned and audited;
/// every picker saves to the key the X1/X5 resolvers read.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SettingsXeroSectionTests
{
    [AvaloniaFact]
    public async Task Offline_TheSectionShowsTheLastReading_WithoutCallingXero_AndDocumentsUseIt()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        await using var fixture = await SectionFixture.StartAsync(reader);

        Assert.Equal(0, reader.RefreshCalls);
        Assert.Equal("Company details from Xero, read at 2 Oct 2026 10:00", Text(fixture.Section, XeroSettingsSection.ReadingNoteName));
        Assert.Equal("Organisation: Demo Company (UK)", Text(fixture.Section, "Xero organisation"));
        Assert.StartsWith("Demo Company: yes", Text(fixture.Section, "Xero Demo Company"), StringComparison.Ordinal);
        var details = Text(fixture.Section, "Company details from Xero");
        Assert.Contains("Demo Company (UK) Limited", details, StringComparison.Ordinal);
        Assert.Contains("VAT No. GB 123 4567 89", details, StringComparison.Ordinal);
        Assert.Contains("Company No. 01234567", details, StringComparison.Ordinal);

        // Documents now print Xero's details.
        var identity = fixture.Identity.ToIdentity();
        Assert.Equal("Demo Company (UK) Limited", identity.LegalName);
        Assert.Equal("GB 123 4567 89", identity.VatNumber);
        Assert.Contains("VAT No. GB 123 4567 89", identity.FooterLeft(), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task NoReadingYet_TheSectionSaysSo_AndDocumentsKeepTheTypedOrganisation()
    {
        var reader = new FakeXeroSettingsReader();
        await using var fixture = await SectionFixture.StartAsync(reader);

        Assert.Equal("Organisation: not read yet.", Text(fixture.Section, "Xero organisation"));
        Assert.Contains("not read from Xero yet", Text(fixture.Section, XeroSettingsSection.ReadingNoteName), StringComparison.Ordinal);
        Assert.Null(fixture.Identity.XeroCompanyDetails);
        Assert.Equal(fixture.Identity.ToSettingsIdentity(), fixture.Identity.ToIdentity());
        Assert.Contains("checked against Xero once it has been read", Text(fixture.Section, "Xero mapping problems"), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task AllowLiveOrganisation_IsOffByDefault_Warned_AndEveryChangeIsAudited()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Live() };
        await using var fixture = await SectionFixture.StartAsync(reader);

        var allowLive = CheckBox(fixture.Section, XeroSettingsSection.AllowLiveOrganisationName);
        Assert.False(allowLive.IsChecked);
        Assert.Equal(XeroSettingsSection.AllowLiveOrganisationWarning, Text(fixture.Section, "Allow live organisation warning"));
        Assert.StartsWith("Demo Company: no", Text(fixture.Section, "Xero Demo Company"), StringComparison.Ordinal);
        Assert.Equal("Writes to Tempest Live Ltd are blocked until this switch is on and saved.", Text(fixture.Section, "Live organisation status"));

        allowLive.IsChecked = true;
        await fixture.Section.SaveAsync();
        Assert.Equal("True", await fixture.Settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));
        Assert.Contains("your live organisation", Text(fixture.Section, "Live organisation status"), StringComparison.Ordinal);

        // A save with no change records nothing more.
        await fixture.Section.SaveAsync();

        allowLive.IsChecked = false;
        await fixture.Section.SaveAsync();
        Assert.Equal("False", await fixture.Settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));

        var audit = await fixture.Resolve<IAuditQuery>().QueryAsync(new AuditQueryCriteria(action: XeroSettingsSection.AllowLiveOrganisationChangedAction));
        var rows = audit.OrderBy(r => r.OccurredAt).ToList();
        Assert.Equal(["Off→On", "On→Off"], rows.Select(r => $"{r.Detail["OldValue"]}→{r.Detail["NewValue"]}").ToArray());
        Assert.All(rows, r =>
        {
            Assert.Equal(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, r.Detail["Subject"]);
            Assert.Equal("Tempest Live Ltd", r.Detail["Organisation"]);
            Assert.Equal("False", r.Detail["IsDemoCompany"]);
        });
    }

    [AvaloniaFact]
    public async Task RefreshFromXero_ReadsShowsAndFeedsDocuments_AndAFailureKeepsTheLastReading()
    {
        var reader = new FakeXeroSettingsReader();
        await using var fixture = await SectionFixture.StartAsync(reader);
        var outcomes = new List<(string Message, ActionOutcome Outcome)>();
        fixture.Section.ActionCompleted += (message, outcome) => outcomes.Add((message, outcome));

        // Not blocking: while Xero is being read the UI stays live and the
        // button cannot be pressed twice.
        var pending = new TaskCompletionSource<ConnectorResult<XeroSettingsReading>>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.NextRefresh = pending.Task;
        Click(fixture.Section, XeroSettingsSection.RefreshName);
        Assert.False(Button(fixture.Section, XeroSettingsSection.RefreshName).IsEnabled);
        Assert.Equal("Reading Xero…", Text(fixture.Section, "Xero status"));

        var fresh = XeroTestReadings.Demo();
        reader.Cached = fresh;
        pending.SetResult(ConnectorResult<XeroSettingsReading>.Ok(fresh));
        await UntilAsync(() => Button(fixture.Section, XeroSettingsSection.RefreshName).IsEnabled);

        Assert.Equal(1, reader.RefreshCalls);
        Assert.Equal("Organisation: Demo Company (UK)", Text(fixture.Section, "Xero organisation"));
        Assert.Equal("Company details from Xero, read at 2 Oct 2026 10:00", Text(fixture.Section, XeroSettingsSection.ReadingNoteName));
        Assert.Equal("Demo Company (UK) Limited", fixture.Identity.ToIdentity().LegalName);
        Assert.Equal(("Read Demo Company (UK) from Xero.", ActionOutcome.Changed), outcomes[^1]);

        // Offline now: the reading stays, and the status says why.
        reader.NextRefresh = Task.FromResult(ConnectorResult<XeroSettingsReading>.Unavailable("no network"));
        await fixture.Section.RefreshFromXeroAsync();
        Assert.Equal(
            "Could not read Xero (Xero unreachable: no network). Showing the last reading (from Xero, read at 2 Oct 2026 10:00).",
            Text(fixture.Section, "Xero status"));
        Assert.Equal("Organisation: Demo Company (UK)", Text(fixture.Section, "Xero organisation"));
        Assert.Equal("Demo Company (UK) Limited", fixture.Identity.ToIdentity().LegalName);
        Assert.Equal(ActionOutcome.Failed, outcomes[^1].Outcome);
    }

    [AvaloniaFact]
    public async Task TaxTypeAndAccountPickers_OfferXerosActiveChoices_AndSaveWhatTheResolversRead()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        await using var fixture = await SectionFixture.StartAsync(reader);

        // Tax types: the default first, then only active rates for the side.
        var salesStandard = ComboBox(fixture.Section, XeroSettingsSection.TaxTypeName(VatTaxDirection.Sales, VatRate.Standard));
        var tags = salesStandard.Items.OfType<ComboBoxItem>().Select(i => (string)i.Tag!).ToList();
        Assert.Equal(string.Empty, tags[0]);
        Assert.Equal("Default (OUTPUT2)", salesStandard.Items.OfType<ComboBoxItem>().First().Content);
        Assert.Contains("OUTPUT2", tags);
        Assert.Contains("SPECIALOUT", tags);
        Assert.DoesNotContain("INPUT2", tags);       // not applicable to sales
        Assert.DoesNotContain("OLDOUTPUT", tags);    // archived
        Assert.Equal(0, salesStandard.SelectedIndex);

        // Accounts: Q10 defaults (the Demo Company's codes) pre-selected; only active revenue accounts offered.
        var sales = ComboBox(fixture.Section, XeroSettingsSection.SalesAccountName);
        Assert.Equal("200", ((ComboBoxItem)sales.SelectedItem!).Tag);
        Assert.Equal(["200", "260"], sales.Items.OfType<ComboBoxItem>().Select(i => (string)i.Tag!).ToArray());
        Assert.Equal("493", ((ComboBoxItem)ComboBox(fixture.Section, XeroSettingsSection.ExpenseAccountName(ExpenseCategory.Travel)).SelectedItem!).Tag);
        Assert.Equal("412", ((ComboBoxItem)ComboBox(fixture.Section, XeroSettingsSection.ExpenseAccountName(ExpenseCategory.Subcontract)).SelectedItem!).Tag);
        Assert.Equal("Every saved tax type and account code is active in Xero.", Text(fixture.Section, "Xero mapping problems"));

        Choose(salesStandard, "SPECIALOUT");
        Choose(sales, "260");
        Choose(ComboBox(fixture.Section, XeroSettingsSection.ExpenseAccountName(ExpenseCategory.Materials)), "493");
        await fixture.Section.SaveAsync();

        Assert.Equal("SPECIALOUT", (await new XeroTaxTypeResolver(reader, fixture.Settings).ResolveAsync(VatRate.Standard, VatTaxDirection.Sales)).Code);
        Assert.Equal("INPUT2", (await new XeroTaxTypeResolver(reader, fixture.Settings).ResolveAsync(VatRate.Standard, VatTaxDirection.Purchases)).Code);
        var codes = new XeroAccountCodeMap(reader, fixture.Settings);
        Assert.Equal("260", (await codes.ResolveSalesAsync()).Code);
        Assert.Equal("493", (await codes.ResolveExpenseAsync(ExpenseCategory.Materials)).Code);
        Assert.Equal("412", (await codes.ResolveExpenseAsync(ExpenseCategory.Subcontract)).Code);

        // Choosing "Default" again clears the override.
        Choose(salesStandard, string.Empty);
        await fixture.Section.SaveAsync();
        Assert.Equal("OUTPUT2", (await new XeroTaxTypeResolver(reader, fixture.Settings).ResolveAsync(VatRate.Standard, VatTaxDirection.Sales)).Code);
    }

    [AvaloniaFact]
    public async Task ASavedCodeXeroDoesNotHold_IsKeptAsChosen_AndNamedAsAProblem()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        await using var fixture = await SectionFixture.StartAsync(reader, async settings =>
        {
            XeroAccountCodeMap.EnsureDefinitions(settings);
            await settings.SetValueAsync(XeroAccountCodeMap.SalesSettingKey, "999");
        });

        var sales = ComboBox(fixture.Section, XeroSettingsSection.SalesAccountName);
        var selected = (ComboBoxItem)sales.SelectedItem!;
        Assert.Equal("999", selected.Tag);
        Assert.Equal("999 — not an active revenue account in Xero", selected.Content);
        Assert.Contains("Sales: Xero has no account 999", Text(fixture.Section, "Xero mapping problems"), StringComparison.Ordinal);

        // Saving without touching it never silently changes it.
        await fixture.Section.SaveAsync();
        Assert.Equal("999", await fixture.Settings.GetValueAsync(XeroAccountCodeMap.SalesSettingKey));
    }

    [AvaloniaFact]
    public async Task GeneralExpensesContact_AndIncludeOnline_SaveWhereTheBillAndInvoiceHandlersReadThem()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        await using var fixture = await SectionFixture.StartAsync(reader, organisations: async catalog =>
        {
            await catalog.RegisterAsync("DIRCT", new Organisation { Reference = "DIRCT", Name = "Director (reimbursements)", Roles = [PartyKind.Supplier] }, ReferenceProvenance.Unknown);
            await catalog.RegisterAsync("STEEL", new Organisation { Reference = "STEEL", Name = "Steel Supplies Ltd", Roles = [PartyKind.Supplier] }, ReferenceProvenance.Unknown);
        });

        var contact = ComboBox(fixture.Section, XeroSettingsSection.GeneralExpensesContactName);
        Assert.Equal(0, contact.SelectedIndex);
        Assert.Equal([string.Empty, "DIRCT", "STEEL"], contact.Items.OfType<ComboBoxItem>().Select(i => (string)i.Tag!).ToArray());

        var includeOnline = CheckBox(fixture.Section, XeroSettingsSection.IncludeOnlineName);
        Assert.False(includeOnline.IsChecked);

        Choose(contact, "DIRCT");
        includeOnline.IsChecked = true;
        await fixture.Section.SaveAsync();

        Assert.Equal("DIRCT", await new XeroGeneralExpensesContact(fixture.Settings).ReadAsync());
        Assert.Equal("True", await fixture.Settings.GetValueAsync(XeroInvoiceDrafts.IncludeOnlineSettingKey));

        await fixture.Section.RefreshAsync();
        Assert.Equal("DIRCT", ((ComboBoxItem)contact.SelectedItem!).Tag);
        Assert.True(includeOnline.IsChecked);
    }

    [AvaloniaFact]
    public async Task SyncSummary_CountsQueuedFailedAndWaiting_AndRetryAllRequeuesEveryFailedEntry()
    {
        var outbox = new FakeXeroOutbox();
        outbox.Add(XeroOutboxState.Pending);
        outbox.Add(XeroOutboxState.Unknown);
        outbox.Add(XeroOutboxState.Failed);
        outbox.Add(XeroOutboxState.Failed);
        outbox.Add(XeroOutboxState.WaitingForAuthorisation);
        outbox.Add(XeroOutboxState.Succeeded);
        // Retry is the engine's in the application; the outbox's own retry is
        // never a fallback (verifier F3 round 3, defect 3), so it is injected here.
        await using var fixture = await SectionFixture.StartAsync(
            new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() },
            outbox: outbox,
            services: s => new XeroSettingsSectionServices
            {
                Reader = s.Reader, Identity = s.Identity, Outbox = s.Outbox, Organisations = s.Organisations, Audit = s.Audit, TimeZone = s.TimeZone,
                Retry = outbox.RetryAsync,
            });

        Assert.Equal("Sync: 2 queued · 2 failed · 1 waiting for authorisation.", Text(fixture.Section, "Xero sync summary"));
        Assert.True(Button(fixture.Section, XeroSettingsSection.RetryAllName).IsEnabled);

        Click(fixture.Section, XeroSettingsSection.RetryAllName);
        await UntilAsync(() => Text(fixture.Section, "Xero sync summary").Contains("0 failed", StringComparison.Ordinal));

        Assert.Equal("Sync: 4 queued · 0 failed · 1 waiting for authorisation.", Text(fixture.Section, "Xero sync summary"));
        Assert.Equal(2, outbox.Retried.Count);
        Assert.False(Button(fixture.Section, XeroSettingsSection.RetryAllName).IsEnabled);
        Assert.Equal("Queued 2 failed write(s) to Xero again.", Text(fixture.Section, "Xero status"));
    }

    [AvaloniaFact]
    public async Task Connection_ShowsGrantedAgainstRequiredScopes_AndReauthoriseResumesTheEngine()
    {
        var secrets = new InMemorySecretStore();
        await secrets.SetAsync($"Invoicing:Xero:{OAuthAuthoriser.GrantedScopesKeySuffix}", "accounting.invoices offline_access");
        var connection = new FakeConnectionState(new ConnectorAuthorisationState(ConnectorAuthorisation.Expired, "Xero needs re-authorising to allow: accounting.contacts"));
        var authoriser = new FakeAuthoriser();
        var resumed = 0;
        await using var fixture = await SectionFixture.StartAsync(
            new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() },
            services: s => new XeroSettingsSectionServices
            {
                Reader = s.Reader,
                Identity = s.Identity,
                Audit = s.Audit,
                Organisations = s.Organisations,
                TimeZone = s.TimeZone,
                Connection = connection,
                Authoriser = authoriser,
                SecretStore = secrets,
                AfterAuthorised = _ =>
                {
                    resumed++;
                    return Task.CompletedTask;
                },
            });

        Assert.Equal("Re-authorise needed. Xero needs re-authorising to allow: accounting.contacts", Text(fixture.Section, "Xero connection"));
        var scopes = Text(fixture.Section, "Xero scopes");
        Assert.Contains($"Required scopes: {string.Join(", ", XeroScopes.Required)}", scopes, StringComparison.Ordinal);
        Assert.Contains("Granted: accounting.invoices, offline_access", scopes, StringComparison.Ordinal);
        Assert.Contains($"Missing: {string.Join(", ", XeroConnector.FindMissingScopes(["accounting.invoices", "offline_access"]))}", scopes, StringComparison.Ordinal);

        authoriser.OnAuthorise = () =>
        {
            connection.State = new ConnectorAuthorisationState(ConnectorAuthorisation.Authorised);
            secrets.SetAsync($"Invoicing:Xero:{OAuthAuthoriser.GrantedScopesKeySuffix}", string.Join(' ', XeroScopes.Required)).GetAwaiter().GetResult();
        };
        Click(fixture.Section, XeroSettingsSection.ReauthoriseName);
        await UntilAsync(() => Text(fixture.Section, "Xero status") == "Xero re-authorised.");

        Assert.Equal(1, authoriser.Calls);
        Assert.Equal(1, resumed);
        Assert.Equal("Connected to Xero.", Text(fixture.Section, "Xero connection"));
        Assert.Contains("Missing: none", Text(fixture.Section, "Xero scopes"), StringComparison.Ordinal);

        authoriser.Refusal = "access_denied";
        await fixture.Section.ReauthoriseAsync();
        Assert.Equal("Re-authorisation failed. access_denied", Text(fixture.Section, "Xero status"));
        Assert.Equal(1, resumed);
    }

    [AvaloniaFact]
    public async Task EveryInteractiveControl_InTheXeroSection_HasAnAutomationName()
    {
        var outbox = new FakeXeroOutbox();
        outbox.Add(XeroOutboxState.Failed);
        await using var fixture = await SectionFixture.StartAsync(
            new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() },
            outbox: outbox,
            services: s => new XeroSettingsSectionServices
            {
                Reader = s.Reader,
                Identity = s.Identity,
                Outbox = s.Outbox,
                Organisations = s.Organisations,
                Connection = new FakeConnectionState(new ConnectorAuthorisationState(ConnectorAuthorisation.Authorised)),
                Authoriser = new FakeAuthoriser(),
            });

        var unnamed = fixture.Section.GetLogicalDescendants()
            .Where(c => c is Avalonia.Controls.Button or Avalonia.Controls.ComboBox or Avalonia.Controls.CheckBox or TextBox or ListBox)
            .Where(c => string.IsNullOrWhiteSpace(AutomationProperties.GetName((Control)c)))
            .Select(c => c.GetType().Name)
            .ToList();
        Assert.Empty(unnamed);

        // Every picker is there: 2 sides × every VAT rate, sales, every expense category, the contact.
        var combos = fixture.Section.GetLogicalDescendants().OfType<ComboBox>().Count();
        Assert.Equal((2 * Enum.GetValues<VatRate>().Length) + 1 + Enum.GetValues<ExpenseCategory>().Length + 1, combos);
    }

    // ------------------------------------------------------------------

    internal static string Text(Control root, string automationName) =>
        root.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == automationName).Text ?? string.Empty;

    internal static Button Button(Control root, string automationName) =>
        root.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == automationName);

    internal static CheckBox CheckBox(Control root, string automationName) =>
        root.GetLogicalDescendants().OfType<CheckBox>().Single(c => AutomationProperties.GetName(c) == automationName);

    internal static ComboBox ComboBox(Control root, string automationName) =>
        root.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == automationName);

    internal static void Click(Control root, string automationName)
    {
        Button(root, automationName).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    internal static void Choose(ComboBox combo, string tag)
    {
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(i => Equals(i.Tag, tag));
        Dispatcher.UIThread.RunJobs();
    }

    internal static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(10);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
                return;
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            if (condition())
                return;
            await Task.Delay(5);
        }

        Assert.Fail("The condition was not met within ten (scaled) seconds.");
    }
}

/// <summary>A started <see cref="WorkspaceHost"/>, Settings → Organisation and a Xero section in a window — disposed after each test.</summary>
internal sealed class SectionFixture : IAsyncDisposable
{
    private readonly WorkspaceHost _host;
    private readonly Window _window;

    private SectionFixture(WorkspaceHost host, Window window, XeroSettingsSection section, OrganisationIdentitySettings identity, ISettingsProvider settings)
    {
        _host = host;
        _window = window;
        Section = section;
        Identity = identity;
        Settings = settings;
    }

    public XeroSettingsSection Section { get; }

    public OrganisationIdentitySettings Identity { get; }

    public ISettingsProvider Settings { get; }

    public T Resolve<T>() where T : class => (T)_host.Services!.GetService(typeof(T));

    public static async Task<SectionFixture> StartAsync(
        FakeXeroSettingsReader reader,
        Func<ISettingsProvider, Task>? seed = null,
        Func<IOrganisationCatalog, Task>? organisations = null,
        FakeXeroOutbox? outbox = null,
        Func<XeroSettingsSectionServices, XeroSettingsSectionServices>? services = null)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var settings = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));
        var catalog = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));
        if (seed is not null)
            await seed(settings);
        if (organisations is not null)
            await organisations(catalog);

        var identity = new OrganisationIdentitySettings(settings);
        var baseline = new XeroSettingsSectionServices
        {
            Reader = reader,
            Identity = identity,
            Outbox = outbox,
            Organisations = catalog,
            Audit = (IAuditRecorder)host.Services!.GetService(typeof(IAuditRecorder)),
            TimeZone = TimeZoneInfo.Utc,
        };
        var section = new XeroSettingsSection(settings, services?.Invoke(baseline) ?? baseline);
        var window = new Window { Width = 1200, Height = 900, Content = section };
        window.Show();
        await section.RefreshAsync();
        Dispatcher.UIThread.RunJobs();
        return new SectionFixture(host, window, section, identity, settings);
    }

    public async ValueTask DisposeAsync()
    {
        _window.Close();
        Dispatcher.UIThread.RunJobs();
        await _host.ShutdownAsync();
        await _host.DisposeAsync();
    }
}

/// <summary>Readings shaped like Xero's UK Demo Company, and a live organisation.</summary>
internal static class XeroTestReadings
{
    public static readonly DateTimeOffset ReadAt = new(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);

    public static XeroSettingsReading Demo() => Build("Demo Company (UK)", "Demo Company (UK) Limited", isDemo: true);

    public static XeroSettingsReading Live() => Build("Tempest Live Ltd", "Tempest Live Ltd", isDemo: false);

    private static XeroSettingsReading Build(string name, string legalName, bool isDemo) => new(
        XeroSettingsReading.CurrentSchemaVersion,
        "tenant-1",
        new XeroOrganisationProfile(
            "org-1", name, legalName, "GB 123 4567 89", "01234567",
            new XeroAddress("STREET", ["23 Main Street", "Central City"], "Marineville", "Oxfordshire", "OX1 1AA", "United Kingdom"),
            "01234 567890", "www.demo.example", "GBP", "GB", PaysTax: true, IsDemoCompany: isDemo,
            [new XeroBankAccount("Business Bank Account", "12-34-56 12345678", "GBP")]),
        [
            new("OUTPUT2", "20% (VAT on Income)", 20m, "ACTIVE", true, false),
            new("INPUT2", "20% (VAT on Expenses)", 20m, "ACTIVE", false, true),
            new("RROUTPUT", "5% (VAT on Income)", 5m, "ACTIVE", true, false),
            new("RRINPUT", "5% (VAT on Expenses)", 5m, "ACTIVE", false, true),
            new("ZERORATEDOUTPUT", "Zero Rated Income", 0m, "ACTIVE", true, false),
            new("ZERORATEDINPUT", "Zero Rated Expenses", 0m, "ACTIVE", false, true),
            new("EXEMPTOUTPUT", "Exempt Income", 0m, "ACTIVE", true, false),
            new("EXEMPTINPUT", "Exempt Expenses", 0m, "ACTIVE", false, true),
            new("NONE", "No VAT", 0m, "ACTIVE", true, true),
            new("SPECIALOUT", "Special income", 20m, "ACTIVE", true, false),
            new("OLDOUTPUT", "Old income rate", 17.5m, "ARCHIVED", true, false),
        ],
        [
            new("a-200", "200", "Sales", "REVENUE", "REVENUE", "ACTIVE", "OUTPUT2"),
            new("a-260", "260", "Other Revenue", "REVENUE", "REVENUE", "ACTIVE", "OUTPUT2"),
            new("a-270", "270", "Interest Income", "REVENUE", "REVENUE", "ARCHIVED", "NONE"),
            new("a-412", "412", "Consulting & Accounting", "EXPENSE", "EXPENSE", "ACTIVE", "INPUT2"),
            new("a-429", "429", "General Expenses", "EXPENSE", "EXPENSE", "ACTIVE", "INPUT2"),
            new("a-493", "493", "Travel - National", "EXPENSE", "EXPENSE", "ACTIVE", "INPUT2"),
            new("a-090", "090", "Business Bank Account", "BANK", "ASSET", "ACTIVE", null),
        ],
        ReadAt);
}

/// <summary>An <see cref="IXeroSettingsReader"/> fake: a cached reading, and the next refresh's answer.</summary>
internal sealed class FakeXeroSettingsReader : IXeroSettingsReader
{
    public XeroSettingsReading? Cached { get; set; }

    public Task<ConnectorResult<XeroSettingsReading>>? NextRefresh { get; set; }

    public int RefreshCalls { get; private set; }

    public Task<XeroSettingsReading?> ReadCachedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Cached);

    public Task<ConnectorResult<XeroSettingsReading>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        RefreshCalls++;
        return NextRefresh ?? Task.FromResult(ConnectorResult<XeroSettingsReading>.Unavailable("offline"));
    }
}

/// <summary>An <see cref="IXeroOutbox"/> fake holding entries in given states; Retry turns Failed into Pending, as the real outbox does.</summary>
internal sealed class FakeXeroOutbox : IXeroOutbox
{
    private readonly List<XeroOutboxEntry> _entries = [];

    public List<Guid> Retried { get; } = [];

    public void Add(XeroOutboxState state) => _entries.Add(new XeroOutboxEntry(
        XeroOutboxEntry.CurrentSchemaVersion, Guid.NewGuid(), XeroOperation.PushInvoiceDraft,
        XeroDocumentRef.For(XeroDocumentKind.Invoice, Guid.NewGuid()), null, $"tos:key:{_entries.Count}", "hash",
        state, 1, XeroTestReadings.ReadAt, null, null, state == XeroOutboxState.Failed ? "Xero refused it" : null, "system"));

    public Task<XeroOutboxEntry> EnqueueAsync(XeroOperation operation, XeroDocumentRef document, string contentHash, string? argument = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Settings never enqueues.");

    public Task<IReadOnlyList<XeroOutboxEntry>> ListForDocumentAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<XeroOutboxEntry>>([.. _entries.Where(e => e.Document == document)]);

    public Task<IReadOnlyList<XeroOutboxEntry>> ListAsync(IReadOnlyCollection<XeroOutboxState> states, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<XeroOutboxEntry>>([.. _entries.Where(e => states.Count == 0 || states.Contains(e.State))]);

    public Task<bool> RetryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var index = _entries.FindIndex(e => e.Id == entryId && e.State == XeroOutboxState.Failed);
        if (index < 0)
            return Task.FromResult(false);

        _entries[index] = _entries[index] with { State = XeroOutboxState.Pending };
        Retried.Add(entryId);
        return Task.FromResult(true);
    }
}

/// <summary>An <see cref="IXeroConnectionState"/> fake.</summary>
internal sealed class FakeConnectionState(ConnectorAuthorisationState state) : IXeroConnectionState
{
    public ConnectorAuthorisationState State { get; set; } = state;

    public Task<ConnectorAuthorisationState> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(State);
}

/// <summary>An <see cref="IAuthorisableConnector"/> fake that records the sign-in.</summary>
internal sealed class FakeAuthoriser : IAuthorisableConnector
{
    public int Calls { get; private set; }

    public string? Refusal { get; set; }

    public Action? OnAuthorise { get; set; }

    public Task<OAuthResult> AuthoriseAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Refusal is not null)
            return Task.FromResult(OAuthResult.Failed(Refusal));

        OnAuthorise?.Invoke();
        return Task.FromResult(OAuthResult.Ok());
    }
}

/// <summary>An in-memory <see cref="ISecretStore"/>.</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }
}
