using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Tempest.Core.Audit;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.Secrets;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U2 (Xero Technical Design §11): Customers &amp; Suppliers shows
/// each organisation's Xero link and links, creates and unlinks it — the
/// real view and <see cref="XeroContactLinkPrompt"/> over the real X2
/// <see cref="XeroContactLinker"/>, <see cref="XeroAccountingApi"/>,
/// <see cref="XeroWriteSafetyHandler"/> and <see cref="XeroRateLimiter"/>,
/// end to end against the in-process Xero simulator on a hand-moved clock.
/// No network, no sleeps; every test asserts the simulator saw no contract
/// or safety violation.
/// </summary>
public sealed class ContactLinkViewTests
{
    [AvaloniaFact]
    public async Task NotLinked_PoConfirmsTheVatMatch_LinksWithoutCreating_AndShowsXeroDetailsReadOnly()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync(vat: "GB 123 4567 89");
        var byVat = await kit.SeedContactAsync(new JsonObject
        {
            ["Name"] = "Acme Engineering Limited",
            ["TaxNumber"] = "GB123456789",
            ["Addresses"] = new JsonArray
            {
                new JsonObject { ["AddressType"] = "POBOX", ["AddressLine1"] = "Accounts Payable", ["AddressLine2"] = "PO Box 12", ["City"] = "Leeds", ["PostalCode"] = "LS1 1AA" },
            },
            ["PaymentTerms"] = new JsonObject { ["Sales"] = new JsonObject { ["Day"] = 30, ["Type"] = "DAYSAFTERBILLDATE" } },
        });
        var byName = kit.Simulator.SeedContact("Acme Engineering Ltd");
        kit.Simulator.SeedContact("Unrelated Widgets plc");
        var (window, view) = await kit.ShowAsync();

        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Not linked", StringComparison.Ordinal));
        Assert.True(Button(view, CustomersSuppliersView.LinkToXeroName).IsVisible);
        Assert.False(Button(view, CustomersSuppliersView.UnlinkFromXeroName).IsVisible);
        var mark = kit.Mark;

        Click(view, CustomersSuppliersView.LinkToXeroName);
        var prompt = view.XeroLinkPrompt!;
        await kit.WaitAsync(() => prompt.IsVisible && !prompt.IsBusy && prompt.Candidates.Count > 0);

        // Ranked: VAT number first, then the same name; the unrelated contact is not offered.
        Assert.Equal([byVat, byName], prompt.Candidates.Select(c => c.ContactId));
        Assert.Equal([XeroContactMatcher.MatchedOnVatNumber, XeroContactMatcher.MatchedOnExactName], prompt.Candidates.Select(c => c.MatchedOn));
        var rows = List(prompt, XeroContactLinkPrompt.CandidateListName).Items.OfType<ListBoxItem>().Select(AutomationProperties.GetName).ToList();
        Assert.StartsWith("Acme Engineering Limited — Same VAT number · VAT GB123456789", rows[0], StringComparison.Ordinal);
        Assert.StartsWith("Acme Engineering Ltd — Same name", rows[1], StringComparison.Ordinal);

        // Nothing is linked until the PO picks one and confirms.
        Assert.False(Button(prompt, XeroContactLinkPrompt.LinkButtonName).IsEnabled);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Empty(kit.WritesSince(mark));

        Assert.True(prompt.Select(byVat));
        Assert.True(Button(prompt, XeroContactLinkPrompt.LinkButtonName).IsEnabled);
        Click(prompt, XeroContactLinkPrompt.LinkButtonName);

        await kit.WaitAsync(() => !prompt.IsVisible && Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Acme Engineering Limited in Xero", StringComparison.Ordinal));
        Assert.Equal("Linked to Acme Engineering Limited in Xero (an existing contact you confirmed).", Text(view, CustomersSuppliersView.XeroLinkStateName));
        Assert.Equal("Billing address: Accounts Payable, PO Box 12, Leeds, LS1 1AA", Text(view, "Xero billing address"));
        Assert.Equal("VAT number: GB123456789", Text(view, "Xero VAT number"));
        Assert.Equal("Payment terms: 30 days after the invoice date", Text(view, "Xero payment terms"));
        Assert.Equal(
            $"From Xero, read at {CustomersSuppliersView.FormatReadAt(kit.Clock.GetUtcNow())}. Change these in Xero; TempestOS never sends them.",
            Text(view, CustomersSuppliersView.XeroDetailsNoteName));
        Assert.Equal("Linked 'Acme Engineering Ltd' to its Xero contact.", Text(view, CustomersSuppliersView.XeroStatusName));
        Assert.True(Button(view, CustomersSuppliersView.UnlinkFromXeroName).IsVisible);
        Assert.False(Button(view, CustomersSuppliersView.LinkToXeroName).IsVisible);

        var link = (await kit.Linker.FindLinkAsync("ACME1"))!;
        Assert.Equal((byVat, XeroContactLinker.LinkedByLinked), (link.XeroId, link.LinkedBy));

        // Linking an existing contact never creates one; the only write is
        // Q7's customer code into its empty ContactNumber — never the
        // billing details, which stay Xero's.
        var writes = kit.WritesSince(mark);
        Assert.DoesNotContain(writes, w => w.Method == HttpMethod.Put);
        var write = Assert.Single(writes);
        Assert.Equal($"Contacts/{byVat}", write.Path);
        var sent = write.JsonBody!["Contacts"]![0]!.AsObject();
        Assert.Equal(["ContactID", "ContactNumber"], sent.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal(3, kit.LiveContacts.Count);
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task NoMatchInXero_PoCreatesIt_OneContactCarryingTheCustomerCode_IsLinked()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        kit.Simulator.SeedContact("Unrelated Widgets plc");
        var (window, view) = await kit.ShowAsync();

        await view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => Button(view, CustomersSuppliersView.LinkToXeroName).IsVisible);
        Click(view, CustomersSuppliersView.LinkToXeroName);
        var prompt = view.XeroLinkPrompt!;
        await kit.WaitAsync(() => prompt.IsVisible && !prompt.IsBusy && Text(prompt, XeroContactLinkPrompt.NoMatchesName).Length > 0 && Control(prompt, XeroContactLinkPrompt.NoMatchesName).IsVisible);

        Assert.Empty(prompt.Candidates);
        Assert.False(Button(prompt, XeroContactLinkPrompt.LinkButtonName).IsEnabled);
        Assert.True(Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);
        var mark = kit.Mark;

        Click(prompt, XeroContactLinkPrompt.CreateButtonName);
        await kit.WaitAsync(() => !prompt.IsVisible && Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Brunel Fabrication Ltd in Xero", StringComparison.Ordinal));

        Assert.Equal("Linked to Brunel Fabrication Ltd in Xero (created in Xero by TempestOS).", Text(view, CustomersSuppliersView.XeroLinkStateName));
        Assert.Equal("Created 'Brunel Fabrication Ltd' in Xero and linked it.", Text(view, CustomersSuppliersView.XeroStatusName));
        Assert.Equal("Payment terms: none set in Xero", Text(view, "Xero payment terms"));

        var created = Assert.Single(kit.LiveContacts, c => c.Body["Name"]?.GetValue<string>() == "Brunel Fabrication Ltd");
        Assert.Equal("BRUNL", created.Number);
        var link = (await kit.Linker.FindLinkAsync("BRUNL"))!;
        Assert.Equal((created.Id, XeroContactLinker.LinkedByCreated), (link.XeroId, link.LinkedBy));
        Assert.Single(kit.WritesSince(mark), w => w.Method == HttpMethod.Put && w.Path == "Contacts");
        Assert.Contains(kit.Audit.Rows, r => r.Action == XeroContactLinker.AuditLinkCreated);
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task Unlink_RemovesOnlyTheLink_WritesNothingToXero_AndOffersLinkAgain()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "ACME1");
        Assert.Equal(ConnectorOutcome.Ok, (await kit.Linker.LinkExistingAsync("ACME1", contactId)).Outcome);
        var (window, view) = await kit.ShowAsync();

        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Acme Engineering Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);
        var mark = kit.Mark;

        Click(view, CustomersSuppliersView.UnlinkFromXeroName);
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Not linked", StringComparison.Ordinal));

        Assert.Equal(
            "Unlinked 'Acme Engineering Ltd' from its Xero contact. Nothing was changed in Xero; link it again before its next document goes to Xero.",
            Text(view, CustomersSuppliersView.XeroStatusName));
        Assert.True(Button(view, CustomersSuppliersView.LinkToXeroName).IsVisible);
        Assert.False(Control(view, "Xero billing address").IsEffectivelyVisible);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Empty(kit.RequestsSince(mark));
        Assert.Equal("ACTIVE", kit.Simulator.Find("Contacts", contactId)!.Status);
        var audit = Assert.Single(kit.Audit.Rows, r => r.Action == XeroContactLinker.AuditLinkUnlinked);
        Assert.Equal(contactId, audit.Detail!["contactId"]);
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task ContactArchivedInXero_RefreshSaysDocumentsAreBlocked_AndWhatToDo()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "ACME1");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        var (window, view) = await kit.ShowAsync();

        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Acme Engineering Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);

        kit.Simulator.ArchiveContactInXero(contactId);
        kit.Clock.Advance(TimeSpan.FromHours(1));
        Click(view, CustomersSuppliersView.RefreshFromXeroName);
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).Contains("archived in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);

        Assert.Equal(
            "Linked to Acme Engineering Ltd in Xero (an existing contact you confirmed). It is archived in Xero, so documents for it are blocked: restore it in Xero, or unlink it and link another.",
            Text(view, CustomersSuppliersView.XeroLinkStateName));
        Assert.Equal(
            $"From Xero, read at {CustomersSuppliersView.FormatReadAt(kit.Clock.GetUtcNow())}. Change these in Xero; TempestOS never sends them.",
            Text(view, CustomersSuppliersView.XeroDetailsNoteName));
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task DetailsAnswerForAnOrganisationNoLongerShown_IsDropped()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "ACME1");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        var (window, view) = await kit.ShowAsync();

        IDisposable? hold = kit.Simulator.HoldRequests();
        try
        {
            await view.SelectAsync("ACME1");
            await kit.WaitAsync(() => kit.Simulator.InFlight == 1);
            Assert.Equal("Reading the contact's details from Xero…", Text(view, CustomersSuppliersView.XeroStatusName));

            // The UI thread is free while Xero answers: another organisation opens.
            await view.SelectAsync("BRUNL");
            await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Not linked", StringComparison.Ordinal));

            hold.Dispose();
            hold = null;
            await kit.WaitAsync(() => kit.Simulator.InFlight == 0);
            await kit.WaitAsync(() => !view.IsXeroBusy);
        }
        finally
        {
            hold?.Dispose();
        }

        Assert.Equal("BRUNL", view.EditingRecordId);
        Assert.StartsWith("Not linked", Text(view, CustomersSuppliersView.XeroLinkStateName), StringComparison.Ordinal);
        Assert.False(Control(view, "Xero billing address").IsEffectivelyVisible);
        Assert.Equal(string.Empty, Text(view, CustomersSuppliersView.XeroStatusName));
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task XeroNotConnected_SaysSo_AndOffersNoXeroAction()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.SecretStore.RemoveAsync(XeroContactLinker.TenantIdSecretKey);
        var (window, view) = await kit.ShowAsync();

        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Xero is not connected", StringComparison.Ordinal) && !view.IsXeroBusy);

        Assert.False(Button(view, CustomersSuppliersView.LinkToXeroName).IsVisible);
        Assert.False(Button(view, CustomersSuppliersView.UnlinkFromXeroName).IsVisible);
        Assert.Empty(kit.Simulator.Requests);
        window.Close();
    }

    [AvaloniaFact]
    public async Task WithoutXero_TheViewShowsNoXeroSection()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var view = new CustomersSuppliersView(kit.Organisations, kit.Contacts);
        var window = new Window { Content = view, Width = 1200, Height = 900 };
        window.Show();
        await view.RefreshAsync();

        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => view.EditingRecordId == "ACME1");

        Assert.Null(view.XeroLinkPrompt);
        Assert.False(Control(view, CustomersSuppliersView.XeroLinkStateName).IsEffectivelyVisible);
        Assert.Empty(kit.Simulator.Requests);
        window.Close();
    }

    // ------------------------------------------------------------ helpers

    internal static Control Control(Control root, string name) =>
        root.GetLogicalDescendants().OfType<Control>().Single(c => AutomationProperties.GetName(c) == name);

    internal static string Text(Control root, string name) => ((TextBlock)Control(root, name)).Text ?? string.Empty;

    internal static Button Button(Control root, string name) =>
        root.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);

    internal static ListBox List(Control root, string name) =>
        root.GetLogicalDescendants().OfType<ListBox>().Single(b => AutomationProperties.GetName(b) == name);

    internal static void Click(Control root, string name) =>
        Button(root, name).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
}

/// <summary>
/// The U2 rig: a real <see cref="XeroContactLinker"/> → <see cref="XeroAccountingApi"/>
/// → <see cref="XeroWriteSafetyHandler"/> → <see cref="XeroRateLimiter"/> →
/// <see cref="XeroApiSimulator"/>, over real organisation and contact
/// catalogues and the B2 link store on in-memory persistence; a hand-moved
/// clock; the view hosted in a headless window.
/// </summary>
internal sealed class ContactLinkTestKit : IAsyncDisposable
{
    public const string TenantId = "tenant-u2";
    public const string AccessToken = "u2-access-token";

    private static int _seeds;
    private readonly HttpClient _client;
    private readonly SqlitePersistenceStore _store;
    private readonly string _root;

    private ContactLinkTestKit(
        XeroSimulatorClock clock, XeroApiSimulator simulator, HttpClient client, InMemorySecretStore secretStore,
        OrganisationCatalog organisations, ContactCatalog contacts, PersistenceXeroLinkStore links, RecordingAudit audit,
        XeroContactLinker linker, TestSettingsReader settings, SqlitePersistenceStore store, string root)
    {
        _store = store;
        _root = root;
        Clock = clock;
        Simulator = simulator;
        _client = client;
        SecretStore = secretStore;
        Organisations = organisations;
        Contacts = contacts;
        Links = links;
        Audit = audit;
        Linker = linker;
        Settings = settings;
    }

    public XeroSimulatorClock Clock { get; }

    public XeroApiSimulator Simulator { get; }

    public InMemorySecretStore SecretStore { get; }

    public OrganisationCatalog Organisations { get; }

    public ContactCatalog Contacts { get; }

    public PersistenceXeroLinkStore Links { get; }

    public RecordingAudit Audit { get; }

    public XeroContactLinker Linker { get; }

    public TestSettingsReader Settings { get; }

    /// <summary>A mark in the simulator's request log.</summary>
    public int Mark => Simulator.Requests.Count;

    public IReadOnlyList<XeroSimulatedRequest> RequestsSince(int mark) => [.. Simulator.Requests.Skip(mark)];

    public IReadOnlyList<XeroSimulatedRequest> WritesSince(int mark) => [.. RequestsSince(mark).Where(r => r.Method != HttpMethod.Get)];

    public IReadOnlyList<XeroSimulatedDocument> LiveContacts => [.. Simulator.All("Contacts").Where(c => c.Status != "DELETED")];

    public static async Task<ContactLinkTestKit> CreateAsync(bool isDemoCompany = true)
    {
        var clock = new XeroSimulatorClock();
        var simulator = new XeroApiSimulator(new XeroSimulatorOptions(TenantId: TenantId, AccessToken: AccessToken), clock);
        var settings = new TestSettingsReader(isDemoCompany);
        var rateLimiter = new XeroRateLimiter(clock) { InnerHandler = simulator };
        var safety = new XeroWriteSafetyHandler(() => settings, _ => Task.FromResult(false), () => null, timeProvider: clock)
        {
            InnerHandler = rateLimiter,
        };
        var client = new HttpClient(safety) { BaseAddress = XeroApiSimulator.BaseAddress };

        var secretStore = new InMemorySecretStore();
        await secretStore.SetAsync("Invoicing:Xero:AccessToken", AccessToken);
        await secretStore.SetAsync("Invoicing:Xero:RefreshToken", "u2-refresh-token");
        await secretStore.SetAsync("Invoicing:Xero:ExpiresAtUtc", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
        await secretStore.SetAsync(XeroContactLinker.TenantIdSecretKey, TenantId);
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>("Invoicing:Xero:ClientId", "client-u2")]))
            .Build();
        var profile = new OAuthProviderProfile(
            "Xero", new Uri("https://login.xero.com/identity/connect/authorize"), new Uri("https://identity.xero.com/connect/token"),
            XeroScopes.Required, new Uri("https://api.xero.com/connections"));

        // The token client never answers: a test needing a refresh fails loudly rather than reach a network.
        var authoriser = new OAuthAuthoriser(profile, configuration, secretStore, new NoBrowser(), new HttpClient(new NoNetwork()));
        var api = new XeroAccountingApi(client, authoriser, clock);

        // The catalogues need the transactional store the platform ships (`TD-158`): SQLite in a throw-away folder.
        var root = Path.Combine(Path.GetTempPath(), "tempest-u2-" + Guid.NewGuid().ToString("N"));
        var persistence = new SqlitePersistenceStore(new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, root),
                new KeyValuePair<string, string>(SqlitePersistenceStore.SynchronousConfigurationKey, SqlitePersistenceStore.RelaxedSynchronousValue),
            ]))
            .Build());
        var documents = new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor());
        var organisations = new OrganisationCatalog(documents, persistence);
        var contacts = new ContactCatalog(documents, persistence);
        var links = new PersistenceXeroLinkStore(new InMemoryPersistenceStore());
        var audit = new RecordingAudit();
        var linker = new XeroContactLinker(api, links, organisations, secretStore, audit, clock, attemptStore: new InMemoryPersistenceStore());

        return new ContactLinkTestKit(clock, simulator, client, secretStore, organisations, contacts, links, audit, linker, settings, persistence, root);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        Simulator.Dispose();
        await _store.DisposeAsync();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is harmless.
        }
    }

    /// <summary>Registers a customer (fictional) as Customers &amp; Suppliers would.</summary>
    public async Task AddOrganisationAsync(
        string reference = "ACME1", string name = "Acme Engineering Ltd", string? customerCode = "ACME1", string? vat = null)
    {
        var organisation = new Organisation
        {
            Reference = reference,
            Name = name,
            CustomerCode = customerCode,
            TaxRegistration = vat,
            Status = RelationshipStatus.Active,
        };
        organisation = organisation with { Roles = organisation.RolesFor(OrganisationTradingType.Customer) };
        await Organisations.RegisterAsync(reference, organisation, CustomersSuppliersView.Provenance);
    }

    /// <summary>A contact entered in Xero by hand, with whatever fields the test gives — sent straight to the simulator, outside TempestOS's pipeline.</summary>
    public async Task<string> SeedContactAsync(JsonObject contact)
    {
        using var raw = Simulator.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, "Contacts?summarizeErrors=true")
        {
            Content = new StringContent(new JsonObject { ["Contacts"] = new JsonArray { contact } }.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        request.Headers.Add("xero-tenant-id", TenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Idempotency-Key", $"u2-seed:{Interlocked.Increment(ref _seeds)}");

        using var response = await raw.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return body["Contacts"]![0]!["ContactID"]!.GetValue<string>();
    }

    /// <summary>The view with this kit's linker, in a shown headless window, its list loaded.</summary>
    public async Task<(Window Window, CustomersSuppliersView View)> ShowAsync()
    {
        var view = new CustomersSuppliersView(Organisations, Contacts) { XeroContacts = Linker };
        var window = new Window { Content = view, Width = 1200, Height = 900 };
        window.Show();
        await view.RefreshAsync();
        return (window, view);
    }

    /// <summary>Pumps the UI dispatcher until <paramref name="condition"/> holds.</summary>
    public Task WaitAsync(Func<bool> condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? what = null) =>
        DesktopTestHelpers.WaitUntilAsync(condition, 20, diagnostics: () => $"simulator requests: {Simulator.Requests.Count}, violations: {Simulator.Violations.Count}", what: what);

    public void AssertNoViolations() => Assert.Empty(Simulator.Violations);
}

/// <summary>An <see cref="IXeroSettingsReader"/> whose cached reading says whether this is the Demo Company (D7).</summary>
internal sealed class TestSettingsReader(bool isDemoCompany) : IXeroSettingsReader
{
    public bool IsDemoCompany { get; set; } = isDemoCompany;

    public Task<XeroSettingsReading?> ReadCachedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<XeroSettingsReading?>(Reading());

    public Task<ConnectorResult<XeroSettingsReading>> RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ConnectorResult<XeroSettingsReading>.Ok(Reading()));

    private XeroSettingsReading Reading() => new(
        XeroSettingsReading.CurrentSchemaVersion,
        ContactLinkTestKit.TenantId,
        new XeroOrganisationProfile("org-u2", "Demo Company (UK)", "Demo Company (UK)", null, null, null, null, null, "GBP", "GB", true, IsDemoCompany, []),
        [],
        [],
        DateTimeOffset.UnixEpoch);
}

/// <summary>An in-memory <see cref="ISecretStore"/>.</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private TaskCompletionSource? _hold;

    /// <summary>When set, every read throws it — a store defect, which is neither an <see cref="InvalidOperationException"/> nor a transport fault.</summary>
    public Exception? Fault { get; set; }

    /// <summary>Holds the next read until the returned gate is opened.</summary>
    public TaskCompletionSource HoldNextGet()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _hold = hold;
        return hold;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _hold, null) is { } hold)
            await hold.Task.ConfigureAwait(false);

        if (Fault is { } fault)
            throw fault;

        lock (_values)
            return _values.TryGetValue(key, out var value) ? value : null;
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        lock (_values)
            _values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_values)
            _values.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>An <see cref="IAuditRecorder"/> that keeps every row.</summary>
internal sealed class RecordingAudit : IAuditRecorder
{
    private readonly List<(string Action, IReadOnlyDictionary<string, string>? Detail)> _rows = [];

    public IReadOnlyList<(string Action, IReadOnlyDictionary<string, string>? Detail)> Rows
    {
        get
        {
            lock (_rows)
                return [.. _rows];
        }
    }

    public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default)
    {
        lock (_rows)
            _rows.Add((action, detail));
        return Task.CompletedTask;
    }
}

/// <summary>A browser launcher that is never expected to run.</summary>
internal sealed class NoBrowser : IBrowserLauncher
{
    public void Open(Uri url) => throw new InvalidOperationException("No browser in this test.");
}

/// <summary>A handler that refuses every request — no network in these tests.</summary>
internal sealed class NoNetwork : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No token endpoint in this test.");
}
