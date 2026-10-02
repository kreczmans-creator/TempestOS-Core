using System.Security.Cryptography;
using System.Text;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Quotations;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Contacts;
using Tempest.Core.Tests.Invoicing.Xero.Settings;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;

/// <summary>An <see cref="IXeroQuoteSource"/> holding whatever quotations a test sets.</summary>
internal sealed class FakeQuoteSource : IXeroQuoteSource
{
    private readonly Dictionary<Guid, XeroQuoteSnapshot> _quotes = [];

    public XeroQuoteSnapshot this[Guid id]
    {
        get => _quotes[id];
        set => _quotes[id] = value;
    }

    public void Remove(Guid id) => _quotes.Remove(id);

    public Task<XeroQuoteSnapshot?> FindAsync(Guid quotationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_quotes.TryGetValue(quotationId, out var quote) ? quote : null);

    public Task<IReadOnlyList<Guid>> ListIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([.. _quotes.Keys]);
}

/// <summary>An <see cref="IXeroDocumentFileSource"/> holding the "issued PDF" a test stores, as the Desktop export would.</summary>
internal sealed class FakeDocumentFileSource : IXeroDocumentFileSource
{
    private readonly Dictionary<XeroDocumentRef, XeroDocumentFile> _files = [];

    public void Store(Guid quotationId, string text, string fileName = "quote.pdf")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        _files[XeroDocumentRef.For(XeroDocumentKind.Quote, quotationId)] =
            new XeroDocumentFile(fileName, "application/pdf", bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    public XeroDocumentFile? Find(Guid quotationId) =>
        _files.TryGetValue(XeroDocumentRef.For(XeroDocumentKind.Quote, quotationId), out var file) ? file : null;

    public Task<XeroDocumentFile?> FindAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.TryGetValue(document, out var file) ? file : null);
}

/// <summary>One attempt the test drain made: the entry as claimed and what its handler answered.</summary>
internal sealed record DrainStep(XeroOutboxEntry Entry, XeroPushResult Result);

/// <summary>
/// The X3 quote sync end to end, with no network and a hand-moved clock:
/// <see cref="XeroQuotePlanner"/> → the B2 outbox → a minimal drain (design
/// §6.3, standing in for X6's engine) → <see cref="XeroQuotePushHandler"/> /
/// <see cref="XeroQuoteAttachmentHandler"/> → <see cref="XeroAccountingApi"/> →
/// <see cref="XeroWriteSafetyHandler"/> → <see cref="XeroRateLimiter"/> → a
/// lost-response hop → <see cref="XeroApiSimulator"/>. The client
/// <c>ACME1</c> is linked (X2) to a seeded Xero contact, and the X1 reading
/// carries the UK Demo Company's tax rates and accounts.
/// </summary>
internal sealed class QuoteSyncTestKit : IDisposable
{
    public const string TenantId = XeroTestAuthoriser.TenantId;
    public const string ClientReference = "ACME1";

    private readonly HttpClient _client;

    private QuoteSyncTestKit(
        XeroApiSimulator simulator, XeroSimulatorClock clock, HttpClient client, XeroAccountingApi api, InMemorySecretStore secrets,
        YieldingInMemoryPersistenceStore store, FakeSettingsReader settings, LostResponseHandler lost, XeroContactLinker linker,
        string contactId, IXeroQuoteSource quotes, XeroQuotePlannerOptions plannerOptions)
    {
        Simulator = simulator;
        Clock = clock;
        _client = client;
        Api = api;
        Secrets = secrets;
        Store = store;
        Settings = settings;
        Lost = lost;
        Linker = linker;
        ContactId = contactId;
        Quotes = quotes;
        Links = new PersistenceXeroLinkStore(store);
        Outbox = new PersistenceXeroOutbox(store, null, clock);
        SettingsProvider = new InMemorySettingsProvider();
        Planner = new XeroQuotePlanner(quotes, Links, Outbox, store, Secrets, Files, Audit, clock, plannerOptions);
        PushHandler = new XeroQuotePushHandler(
            api, Links, quotes, linker, new XeroTaxTypeResolver(settings, SettingsProvider), new XeroAccountCodeMap(settings, SettingsProvider), Audit, clock);
        AttachmentHandler = new XeroQuoteAttachmentHandler(api, Links, quotes, Files, clock);
    }

    public XeroApiSimulator Simulator { get; }

    public XeroSimulatorClock Clock { get; }

    public XeroAccountingApi Api { get; }

    public InMemorySecretStore Secrets { get; }

    public YieldingInMemoryPersistenceStore Store { get; }

    public FakeSettingsReader Settings { get; }

    public InMemorySettingsProvider SettingsProvider { get; }

    public LostResponseHandler Lost { get; }

    public XeroContactLinker Linker { get; }

    /// <summary>The Xero <c>ContactID</c> the client <see cref="ClientReference"/> is linked to.</summary>
    public string ContactId { get; }

    public IXeroQuoteSource Quotes { get; }

    /// <summary><see cref="Quotes"/> as the settable fake, when the kit was built over one.</summary>
    public FakeQuoteSource FakeQuotes => (FakeQuoteSource)Quotes;

    public FakeDocumentFileSource Files { get; } = new();

    public RecordingAuditRecorder Audit { get; } = new();

    public PersistenceXeroLinkStore Links { get; }

    public PersistenceXeroOutbox Outbox { get; }

    public XeroQuotePlanner Planner { get; }

    public XeroQuotePushHandler PushHandler { get; }

    public XeroQuoteAttachmentHandler AttachmentHandler { get; }

    public static async Task<QuoteSyncTestKit> CreateAsync(IXeroQuoteSource? quotes = null, XeroQuotePlannerOptions? plannerOptions = null)
    {
        var clock = new XeroSimulatorClock();
        var simulator = new XeroApiSimulator(new XeroSimulatorOptions(TenantId: TenantId, AccessToken: XeroTestAuthoriser.AccessToken), clock);

        var settings = new FakeSettingsReader { Cached = UkDemoReading() };
        var lost = new LostResponseHandler { InnerHandler = simulator };
        var rateLimiter = new XeroRateLimiter(clock) { InnerHandler = lost };
        var safety = new XeroWriteSafetyHandler(() => settings, _ => Task.FromResult(false), () => new RecordingAuditRecorder(), timeProvider: clock)
        {
            InnerHandler = rateLimiter,
        };
        var client = new HttpClient(safety) { BaseAddress = XeroApiSimulator.BaseAddress };
        var (authoriser, secretStore) = await XeroTestAuthoriser.CreateAsync(TenantId);
        var api = new XeroAccountingApi(client, authoriser, clock);

        var store = new YieldingInMemoryPersistenceStore();
        var organisations = OperationsFixtures.BuildOrganisationCatalog();
        await OperationsFixtures.RegisterAsync(organisations, ClientReference, new Organisation
        {
            Reference = ClientReference,
            Name = "Acme Engineering Ltd",
            CustomerCode = ClientReference,
            Status = RelationshipStatus.Active,
            Roles = [PartyKind.Customer],
        });

        var linker = new XeroContactLinker(
            api, new PersistenceXeroLinkStore(store), organisations, secretStore, timeProvider: clock,
            options: new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });
        var contactId = simulator.SeedContact("Acme Engineering Ltd");
        var linked = await linker.LinkExistingAsync(ClientReference, contactId);
        Assert.Equal(ConnectorOutcome.Ok, linked.Outcome);

        return new QuoteSyncTestKit(
            simulator, clock, client, api, secretStore, store, settings, lost, linker, contactId,
            quotes ?? new FakeQuoteSource(), plannerOptions ?? new XeroQuotePlannerOptions { AutomaticFromUtc = DateTimeOffset.MinValue });
    }

    /// <summary>An X1 reading of the simulated UK Demo Company: its tax rates and accounts, as Xero answers them.</summary>
    public static XeroSettingsReading UkDemoReading() => FakeSettingsReader.Reading(TenantId, isDemoCompany: true) with
    {
        TaxRates = [.. SimulatorSeed.UkDemoTaxRates().Select(r => new XeroTaxRate(r.TaxType, r.Name, r.EffectiveRate, r.Status, r.CanApplyToRevenue, r.CanApplyToExpenses))],
        Accounts = [.. SimulatorSeed.UkDemoAccounts().Select(a => new XeroAccount(a.AccountId, a.Code, a.Name, a.Type, a.Class, a.Status, a.TaxType))],
    };

    /// <summary>A quotation as the planner reads it (fictional): approved R1 by default, two lines, client <see cref="ClientReference"/>.</summary>
    public static XeroQuoteSnapshot Quote(
        Guid id, QuotationStatus status = QuotationStatus.Approved, int revision = 1, string reference = "P0012-Q-001",
        IReadOnlyList<XeroQuoteLine>? lines = null, DateTimeOffset? issuedAt = null, string? client = ClientReference) => new(
            id, reference, status, revision, "Bracket redesign", "P0012 Bracket programme", client, new DateOnly(2026, 10, 2), 30,
            "Payment within 30 days.", "GBP",
            lines ??
            [
                new XeroQuoteLine("Concept design", 12m, 95m, VatRate.Standard),
                new XeroQuoteLine("Drawing pack", 1m, 1_200m, VatRate.Standard),
            ],
            issuedAt ?? new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero));

    public static XeroDocumentRef Ref(Guid quotationId) => XeroDocumentRef.For(XeroDocumentKind.Quote, quotationId);

    /// <summary>The link for <paramref name="quotationId"/> in the tenant.</summary>
    public Task<XeroLink?> LinkAsync(Guid quotationId) => Links.FindAsync(TenantId, Ref(quotationId));

    /// <summary>Plans the quotation and queues its writes (what X6 does after each commit).</summary>
    public Task<IReadOnlyList<XeroOutboxEntry>> PlanAsync(Guid quotationId) => Planner.PlanAndEnqueueAsync(quotationId);

    /// <summary>
    /// Drains the outbox as design §6.3 says, one entry at a time: Succeeded
    /// and NothingToDo → Succeeded; Rejected/Blocked → Failed; RetryLater →
    /// Pending not before the backoff or <c>Retry-After</c> (a 429 pauses the
    /// whole drain); Reauthorise → WaitingForAuthorisation, and the drain
    /// pauses; Unknown → Unknown.
    /// </summary>
    public async Task<IReadOnlyList<DrainStep>> DrainAsync(int maximumAttempts = 100)
    {
        var steps = new List<DrainStep>();
        while (steps.Count < maximumAttempts && await Outbox.ClaimNextDueAsync() is { } entry)
        {
            IXeroPushHandler handler = entry.Operation == XeroOperation.UploadAttachment ? AttachmentHandler : PushHandler;
            var result = await handler.PushAsync(TenantId, entry);
            steps.Add(new DrainStep(entry, result));

            switch (result.Outcome)
            {
                case XeroPushOutcome.Succeeded or XeroPushOutcome.NothingToDo:
                    await Outbox.RecordOutcomeAsync(entry.Id, XeroOutboxState.Succeeded);
                    break;
                case XeroPushOutcome.Rejected or XeroPushOutcome.Blocked:
                    await Outbox.RecordOutcomeAsync(entry.Id, XeroOutboxState.Failed, result.Reason);
                    break;
                case XeroPushOutcome.Reauthorise:
                    await Outbox.RecordOutcomeAsync(entry.Id, XeroOutboxState.WaitingForAuthorisation, result.Reason);
                    return steps;
                case XeroPushOutcome.RetryLater:
                    await Outbox.RecordOutcomeAsync(entry.Id, XeroOutboxState.Pending, result.Reason, Clock.GetUtcNow() + (result.RetryAfter ?? TimeSpan.FromSeconds(30)));
                    if (result.RetryAfter is not null)
                        return steps; // 429: pause everything until Retry-After.
                    break;
                default:
                    await Outbox.RecordOutcomeAsync(entry.Id, XeroOutboxState.Unknown, result.Reason);
                    break;
            }
        }

        return steps;
    }

    /// <summary>The live (not deleted) Xero quotes the simulator holds.</summary>
    public IReadOnlyList<XeroSimulatedDocument> LiveQuotes => [.. Simulator.All("Quotes").Where(q => q.Status != "DELETED")];

    /// <summary>The single Xero quote the simulator holds.</summary>
    public XeroSimulatedDocument OnlyQuote => Assert.Single(LiveQuotes);

    /// <summary>The writes TempestOS sent to <c>Quotes</c> (creates, updates, uploads).</summary>
    public IReadOnlyList<XeroSimulatedRequest> QuoteWrites =>
        [.. Simulator.Requests.Where(r => r.Method != HttpMethod.Get && r.Path.StartsWith("Quotes", StringComparison.Ordinal))];

    /// <summary>A person moves the quote's status in Xero by hand (outside TempestOS's pipeline): a status-only <c>POST Quotes/{id}</c>.</summary>
    public async Task SetStatusInXeroAsync(string quoteId, string status)
    {
        using var client = Simulator.CreateClient();
        var body = SimulatorTestKit.QuoteStatus(quoteId, ContactId, status, Simulator.Find("Quotes", quoteId)!.Number!);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"Quotes/{quoteId}?summarizeErrors=true")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Simulator.Options.AccessToken);
        request.Headers.Add("xero-tenant-id", Simulator.Options.TenantId);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Idempotency-Key", $"by-hand:{Guid.NewGuid():N}");
        using var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A person edits the quote in Xero by hand (outside TempestOS's pipeline): its body as Xero holds it,
    /// changed by <paramref name="edit"/>, sent back as <c>POST Quotes/{id}</c>.
    /// </summary>
    public async Task EditQuoteInXeroByHandAsync(string quoteId, Action<System.Text.Json.Nodes.JsonObject> edit)
    {
        var quote = Simulator.Find("Quotes", quoteId)!.Body.DeepClone().AsObject();
        edit(quote);
        using var client = Simulator.CreateClient();
        var body = new System.Text.Json.Nodes.JsonObject { ["Quotes"] = new System.Text.Json.Nodes.JsonArray { quote } };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"Quotes/{quoteId}?summarizeErrors=true")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Simulator.Options.AccessToken);
        request.Headers.Add("xero-tenant-id", Simulator.Options.TenantId);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Idempotency-Key", $"by-hand:{Guid.NewGuid():N}");
        using var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    /// <summary>A person keys a quote into Xero by hand (outside TempestOS's pipeline) for <paramref name="contactId"/>, then walks it to <paramref name="status"/>; returns its <c>QuoteID</c>.</summary>
    public async Task<string> CreateQuoteInXeroByHandAsync(string contactId, string number = "P0012-Q-001", params string[] statuses)
    {
        using (var client = Simulator.CreateClient())
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, "Quotes?summarizeErrors=true")
            {
                Content = new StringContent(SimulatorTestKit.Quote(contactId, number).ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Simulator.Options.AccessToken);
            request.Headers.Add("xero-tenant-id", Simulator.Options.TenantId);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("Idempotency-Key", $"by-hand:{Guid.NewGuid():N}");
            using var response = await client.SendAsync(request);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        var quoteId = Assert.Single(LiveQuotes, q => q.Number == number).Id;
        foreach (var status in statuses)
            await SetStatusInXeroAsync(quoteId, status);
        return quoteId;
    }

    /// <summary>Asserts the simulator saw no contract or safety violation, and nothing was ever emailed.</summary>
    public void AssertNoViolations()
    {
        Assert.Empty(Simulator.Violations);
        Assert.DoesNotContain(Simulator.Requests, r => r.Path.EndsWith("/Email", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _client.Dispose();
        Simulator.Dispose();
    }
}
