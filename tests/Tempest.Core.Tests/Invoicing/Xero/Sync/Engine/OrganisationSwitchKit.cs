using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Events;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Quotations;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Settings;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;
using Tempest.Core.Tests.Plugins;
using Legacy = Tempest.Core.Invoicing.Xero.Sync.XeroInvoiceLinkImporter.LegacyInvoiceLink;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>Routes each request to the simulated organisation its <c>xero-tenant-id</c> names — two Xero organisations behind one client, as one Xero app sees them.</summary>
internal sealed class TenantRouter : HttpMessageHandler
{
    private readonly Dictionary<string, HttpMessageInvoker> _routes = new(StringComparer.Ordinal);

    public void Add(string tenantId, HttpMessageHandler organisation) => _routes[tenantId] = new HttpMessageInvoker(organisation, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tenantId = request.Headers.TryGetValues("xero-tenant-id", out var values) ? values.FirstOrDefault() : null;
        return tenantId is not null && _routes.TryGetValue(tenantId, out var organisation)
            ? organisation.SendAsync(request, cancellationToken)
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { RequestMessage = request });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var invoker in _routes.Values)
                invoker.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// `v0.24.0` F1 (review board M1): the full X6 engine over <b>two</b>
/// simulated Xero organisations — the Demo Company the Product Owner tests
/// on, and a second organisation the workspace is later connected to — with
/// no configured automatic-sync start (Q8 as shipped: per organisation), the
/// real outbox, link store, planners, handlers, X1 settings reader and the
/// pre-v0.24 invoice-link importer (checking existence through the real
/// client). One client, one safety handler, one rate limiter; a
/// <see cref="TenantRouter"/> sends each request to the organisation its
/// tenant header names. Connecting the second organisation is what Settings
/// does: the stored tenant id changes, the engine is told
/// (<see cref="XeroSyncService.NotifyAuthorisedAsync"/>), and contacts are
/// linked there.
/// </summary>
internal sealed class OrganisationSwitchKit : IDisposable
{
    public const string DemoTenant = "tenant-demo-company";
    public const string SecondTenant = "tenant-second-company";
    public const string ClientReference = "ACME1";
    public const string SupplierReference = "STEEL1";
    public const string GeneralReference = "GENEX";

    /// <summary>A supplier linked only in the second organisation: its Demo-era expense waits, Blocked, while the Demo Company is connected.</summary>
    public const string LateSupplierReference = "NEWCO";

    private readonly HttpClient _client;
    private readonly TempDirectory _temp;

    private OrganisationSwitchKit(TempDirectory temp, XeroSimulatorClock clock, XeroApiSimulator demo, XeroApiSimulator second, HttpClient client,
        XeroAccountingApi api, InMemorySecretStore secrets, XeroRateLimiter rateLimiter, XeroSettingsReader reader, OrganisationCatalog catalog)
    {
        _temp = temp;
        _client = client;
        Clock = clock;
        DemoXero = demo;
        SecondXero = second;
        Api = api;
        Secrets = secrets;
        Reader = reader;

        Store = new YieldingInMemoryPersistenceStore();
        Links = new PersistenceXeroLinkStore(Store);
        Outbox = new PersistenceXeroOutbox(Store, null, clock, secrets);
        Linker = new XeroContactLinker(api, Links, catalog, secrets, timeProvider: clock, options: new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });
        var taxTypes = new XeroTaxTypeResolver(reader, SettingsProvider);
        var accounts = new XeroAccountCodeMap(reader, SettingsProvider);

        // Q8 as shipped: no configured start — per organisation.
        QuotePlanner = new XeroQuotePlanner(Quotes, Links, Outbox, Store, secrets, Files, Audit, clock);
        var quoteHandler = new XeroQuotePushHandler(api, Links, Quotes, Store, Linker, taxTypes, accounts, Audit, clock);
        var quoteAttachments = new XeroQuoteAttachmentHandler(api, Links, Quotes, Files, clock);

        PurchasingState = new XeroPurchasingSyncState(Store, secrets, Audit, clock);
        var creates = new XeroPurchasingCreateLog(Store);
        GeneralContact = new XeroGeneralExpensesContact(SettingsProvider);
        OrderPlanner = new XeroPurchaseOrderPlanner(Orders, Links, Outbox, PurchasingState, Files);
        ExpensePlanner = new XeroExpenseBillPlanner(Expenses, Links, Outbox, PurchasingState, Files, Orders);
        var orderHandler = new XeroPurchaseOrderPushHandler(api, Links, creates, Orders, Linker, taxTypes, accounts, Audit, clock);
        var billHandler = new XeroExpenseBillPushHandler(api, Links, creates, Expenses, Linker, GeneralContact, taxTypes, accounts, Audit, clock);
        var orderAttachments = new XeroPurchaseOrderAttachmentHandler(api, Links, Orders, Files, clock);
        var billAttachments = new XeroExpenseBillAttachmentHandler(api, Links, Files, clock);
        var sendAgain = new XeroPurchasingSendAgain(creates, Outbox, Links, PurchasingState, Orders, Expenses, Audit, clock);

        Parts = new XeroSyncParts(
            Links, Outbox, secrets,
            QuotePlanner, Quotes, quoteHandler, quoteAttachments,
            orderPlanner: OrderPlanner, orders: Orders, orderHandler: orderHandler, orderAttachments: orderAttachments,
            expensePlanner: ExpensePlanner, expenses: Expenses, billHandler: billHandler, billAttachments: billAttachments,
            purchasingState: PurchasingState, creates: creates, sendAgain: sendAgain);
        ReadBack = new XeroReadBack(api, Links, null, rateLimiter, Audit, clock);
        RateLimiter = rateLimiter;
        Importer = new XeroInvoiceLinkImporter(
            _ => Task.FromResult<IReadOnlyList<Legacy>>([.. InvoiceRequests]), Links, clock, Store, XeroInvoiceLinkImporter.CheckWith(api));
        Engine = NewEngine();
    }

    public XeroSimulatorClock Clock { get; }

    /// <summary>The Demo Company (organisation one).</summary>
    public XeroApiSimulator DemoXero { get; }

    /// <summary>The organisation connected later (organisation two).</summary>
    public XeroApiSimulator SecondXero { get; }

    public XeroAccountingApi Api { get; }

    public InMemorySecretStore Secrets { get; }

    public XeroSettingsReader Reader { get; }

    public XeroRateLimiter RateLimiter { get; }

    public YieldingInMemoryPersistenceStore Store { get; }

    public PersistenceXeroLinkStore Links { get; }

    public PersistenceXeroOutbox Outbox { get; }

    public XeroContactLinker Linker { get; }

    public InMemorySettingsProvider SettingsProvider { get; } = new();

    public RecordingAuditRecorder Audit { get; } = new();

    public EngineFiles Files { get; } = new();

    public FakeQuoteSource Quotes { get; } = new();

    public FakePurchaseOrderSource Orders { get; } = new();

    public FakeExpenseSource Expenses { get; } = new();

    public WorkspaceChangeFeed Changes { get; } = new();

    public FakeConnectionState Connection { get; } = new();

    public XeroQuotePlanner QuotePlanner { get; }

    public XeroPurchasingSyncState PurchasingState { get; }

    public XeroGeneralExpensesContact GeneralContact { get; }

    public XeroPurchaseOrderPlanner OrderPlanner { get; }

    public XeroExpenseBillPlanner ExpensePlanner { get; }

    public XeroSyncParts Parts { get; }

    public XeroReadBack ReadBack { get; }

    /// <summary>The invoice requests the importer reads (pre-v0.24 sends, and v0.24 ones).</summary>
    public List<Legacy> InvoiceRequests { get; } = [];

    public XeroInvoiceLinkImporter Importer { get; }

    public XeroSyncService Engine { get; private set; }

    public XeroChangeObserver Observer { get; private set; } = null!;

    public static async Task<OrganisationSwitchKit> CreateAsync()
    {
        var temp = new TempDirectory();
        var clock = new XeroSimulatorClock();
        var demo = new XeroApiSimulator(new XeroSimulatorOptions(TenantId: DemoTenant, AccessToken: XeroTestAuthoriser.AccessToken, OrganisationName: "Demo Company (UK)"), clock);
        var second = new XeroApiSimulator(new XeroSimulatorOptions(TenantId: SecondTenant, AccessToken: XeroTestAuthoriser.AccessToken, OrganisationName: "Second Company (UK)"), clock);

        var router = new TenantRouter();
        router.Add(DemoTenant, demo);
        router.Add(SecondTenant, second);

        XeroSettingsReader? reader = null;
        var rateLimiter = new XeroRateLimiter(clock) { InnerHandler = router };
        var safety = new XeroWriteSafetyHandler(() => reader, _ => Task.FromResult(false), () => new RecordingAuditRecorder(), timeProvider: clock)
        {
            InnerHandler = rateLimiter,
        };
        var client = new HttpClient(safety) { BaseAddress = XeroApiSimulator.BaseAddress };
        var (authoriser, secrets) = await XeroTestAuthoriser.CreateAsync(DemoTenant);
        var api = new XeroAccountingApi(client, authoriser, clock);
        reader = new XeroSettingsReader(api, new FileXeroSettingsCache(Path.Combine(temp.Path, "accounts")), secrets, null, clock);

        var catalog = OperationsFixtures.BuildOrganisationCatalog();
        await RegisterAsync(catalog, ClientReference, "Acme Engineering Ltd", PartyKind.Customer);
        await RegisterAsync(catalog, SupplierReference, "Northern Steel Ltd", PartyKind.Supplier);
        await RegisterAsync(catalog, GeneralReference, "Director expenses", PartyKind.Supplier);
        await RegisterAsync(catalog, LateSupplierReference, "Newco Fixings Ltd", PartyKind.Supplier);

        var kit = new OrganisationSwitchKit(temp, clock, demo, second, client, api, secrets, rateLimiter, reader, catalog);
        await kit.GeneralContact.SetAsync(GeneralReference);

        // The Demo Company: the client, the supplier and "General expenses" are linked; Newco is not.
        await kit.LinkAsync(demo, ClientReference, "Acme Engineering Ltd");
        await kit.LinkAsync(demo, SupplierReference, "Northern Steel Ltd");
        await kit.LinkAsync(demo, GeneralReference, "Director expenses");
        return kit;
    }

    private static async Task RegisterAsync(OrganisationCatalog catalog, string reference, string name, PartyKind role) =>
        await OperationsFixtures.RegisterAsync(catalog, reference, new Organisation
        {
            Reference = reference,
            Name = name,
            CustomerCode = reference,
            Status = RelationshipStatus.Active,
            Roles = [role],
        });

    /// <summary>Seeds <paramref name="name"/> as a contact in <paramref name="organisation"/> and links <paramref name="reference"/> to it there (the connected organisation must be <paramref name="organisation"/>).</summary>
    public async Task<string> LinkAsync(XeroApiSimulator organisation, string reference, string name)
    {
        var contactId = organisation.SeedContact(name);
        Assert.Equal(ConnectorOutcome.Ok, (await Linker.LinkExistingAsync(reference, contactId)).Outcome);
        return contactId;
    }

    /// <summary>
    /// What Settings does when the Product Owner re-authorises into the second
    /// organisation: the stored tenant id changes, the engine is told, and the
    /// contacts are linked there — Newco too, so the Demo-era expense waiting
    /// on it could now be sent, if anything still queued it.
    /// </summary>
    public async Task ConnectSecondOrganisationAsync()
    {
        await Secrets.SetAsync(XeroContactLinker.TenantIdSecretKey, SecondTenant);
        await Engine.NotifyAuthorisedAsync();

        await LinkAsync(SecondXero, ClientReference, "Acme Engineering Ltd");
        await LinkAsync(SecondXero, SupplierReference, "Northern Steel Ltd");
        await LinkAsync(SecondXero, GeneralReference, "Director expenses");
        await LinkAsync(SecondXero, LateSupplierReference, "Newco Fixings Ltd");
    }

    public XeroSyncService NewEngine()
    {
        Observer = new XeroChangeObserver(Changes);
        return new XeroSyncService(
            Parts, Outbox, Store, Reader, Connection, RateLimiter, ReadBack, Observer, Importer, Audit,
            configuration: null, logger: null, timeProvider: Clock, options: new XeroSyncOptions { Jitter = () => 0.5 });
    }

    /// <summary>TempestOS stops and starts again: a new engine over the same durable stores.</summary>
    public XeroSyncService Restart()
    {
        Engine.StopListening();
        Engine = NewEngine();
        return Engine;
    }

    public void Saved(string canonicalKind, Guid id, WorkspaceChangeType type = WorkspaceChangeType.Updated) =>
        Changes.Publish(new WorkspaceChange(1, [new WorkspaceChangeEntry(id, canonicalKind, type)]));

    public async Task SettleAsync(int maximumCycles = 30)
    {
        for (var i = 0; i < maximumCycles; i++)
        {
            var report = await Engine.RunCycleAsync();
            if (report.Drain.Attempted == 0 && report.Planned == 0)
                return;
        }
    }

    // ------------------------------------------------------------ records

    public static XeroDocumentRef QuoteRef(Guid id) => XeroDocumentRef.For(XeroDocumentKind.Quote, id);

    public static XeroDocumentRef OrderRef(Guid id) => XeroPurchaseOrderPlanner.Ref(id);

    public static XeroDocumentRef ExpenseRef(Guid id) => XeroExpenseBillPlanner.Ref(id);

    /// <summary>A quotation approved now and exported (its PDF held); the workspace announces the save.</summary>
    public Guid ExportQuote(string reference)
    {
        var id = Guid.NewGuid();
        Quotes[id] = QuoteSyncTestKit.Quote(id, QuotationStatus.Approved, reference: reference, issuedAt: Clock.GetUtcNow(), client: ClientReference);
        Files.Store(QuoteRef(id), $"{reference} R1 sheet", "quote.pdf");
        Saved(Quotation.CanonicalKind, id);
        return id;
    }

    /// <summary>A purchase order issued today.</summary>
    public Guid IssueOrder(string reference)
    {
        var id = Guid.NewGuid();
        Orders[id] = PurchasingSyncTestKit.Order(id, reference: reference, supplier: SupplierReference, issued: DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime));
        Files.Store(OrderRef(id), $"{reference} sheet", $"{reference}.pdf");
        Saved(PurchaseOrder.CanonicalKind, id, WorkspaceChangeType.StatusChanged);
        return id;
    }

    /// <summary>An expense recorded now (a £100 + £20 VAT train fare; billed to "General expenses" unless <paramref name="supplier"/> is given).</summary>
    public Guid RecordExpense(string? supplier = null, string description = "Train to Sheffield")
    {
        var id = Guid.NewGuid();
        Expenses[id] = PurchasingSyncTestKit.Expense(id, supplier, supplierInvoiceNumber: supplier is null ? null : $"SUP-{id.ToString("N")[..6]}", description: description, recordedAt: Clock.GetUtcNow());
        Files.Store(ExpenseRef(id), "receipt", "receipt.jpg", "image/jpeg");
        Saved(Tempest.Core.Expenses.ProjectExpense.CanonicalKind, id, WorkspaceChangeType.Created);
        return id;
    }

    // ------------------------------------------------------------ Xero side

    public static IReadOnlyList<XeroSimulatedDocument> QuotesIn(XeroApiSimulator organisation) =>
        [.. organisation.All("Quotes").Where(q => q.Status != "DELETED")];

    public static IReadOnlyList<XeroSimulatedDocument> OrdersIn(XeroApiSimulator organisation) =>
        [.. organisation.All("PurchaseOrders").Where(o => o.Status != "DELETED")];

    public static IReadOnlyList<XeroSimulatedDocument> BillsIn(XeroApiSimulator organisation) =>
        [.. organisation.All("Invoices").Where(i => i.Body["Type"]?.GetValue<string>() == "ACCPAY" && i.Status != "DELETED")];

    /// <summary>Every write <paramref name="organisation"/> received.</summary>
    public static IReadOnlyList<XeroSimulatedRequest> WritesTo(XeroApiSimulator organisation) =>
        [.. organisation.Requests.Where(r => r.Method != HttpMethod.Get)];

    /// <summary>A sales invoice keyed straight into <paramref name="organisation"/> (as a pre-v0.24 send, or a v0.24 send, left there): its <c>InvoiceID</c>.</summary>
    public static async Task<string> KeyInvoiceAsync(XeroApiSimulator organisation, string number)
    {
        var contactId = organisation.SeedContact($"Customer for {number}");
        using var client = organisation.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, "Invoices")
        {
            Content = new StringContent(SimulatorTestKit.Invoice(contactId, number).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", organisation.Options.AccessToken);
        request.Headers.Add("xero-tenant-id", organisation.Options.TenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Idempotency-Key", $"tos:test:keyed:{number}");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return json["Invoices"]![0]!["InvoiceID"]!.GetValue<string>();
    }

    /// <summary>Asserts neither organisation saw a contract or safety violation, nothing was emailed, and no audit row carries the token.</summary>
    public void AssertNoViolations()
    {
        Assert.Empty(DemoXero.Violations);
        Assert.Empty(SecondXero.Violations);
        Assert.DoesNotContain(DemoXero.Requests.Concat(SecondXero.Requests), r => r.Path.EndsWith("/Email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Audit.Rows, r => r.Detail?.Values.Any(v => v.Contains(XeroTestAuthoriser.AccessToken, StringComparison.Ordinal)) == true);
    }

    public void Dispose()
    {
        Engine.StopListening();
        _client.Dispose();
        DemoXero.Dispose();
        SecondXero.Dispose();
        _temp.Dispose();
    }
}
