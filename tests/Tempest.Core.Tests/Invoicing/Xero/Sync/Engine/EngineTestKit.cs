using System.Security.Cryptography;
using System.Text;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Events;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
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

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>An <see cref="IXeroDocumentFileSource"/> holding whatever issued PDF or receipt a test stores for any record.</summary>
internal sealed class EngineFiles : IXeroDocumentFileSource
{
    private readonly Dictionary<XeroDocumentRef, XeroDocumentFile> _files = [];

    public XeroDocumentFile Store(XeroDocumentRef document, string text, string fileName = "document.pdf", string contentType = "application/pdf")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var file = new XeroDocumentFile(fileName, contentType, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        _files[document] = file;
        return file;
    }

    public Task<XeroDocumentFile?> FindAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.TryGetValue(document, out var file) ? file : null);
}

/// <summary>An <see cref="IXeroConnectionState"/> a test sets.</summary>
internal sealed class FakeConnectionState : IXeroConnectionState
{
    public ConnectorAuthorisation Status { get; set; } = ConnectorAuthorisation.Authorised;

    public int Reads { get; private set; }

    public Task<ConnectorAuthorisationState> ReadAsync(CancellationToken cancellationToken = default)
    {
        Reads++;
        return Task.FromResult(new ConnectorAuthorisationState(Status));
    }
}

/// <summary>
/// Between the client pipeline and the simulator: can lose Xero's answer to
/// the next new writes (an <c>Idempotency-Key</c> not seen before), or
/// <em>crash</em> the process straight after Xero committed a new write — the
/// answer never arrives and the drain is cut off mid-request, exactly as if
/// TempestOS stopped then (the entry stays InFlight).
/// </summary>
internal sealed class EngineFaultHop : DelegatingHandler
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>How many answers to new writes to lose (the request reached Xero; a transport failure is reported).</summary>
    public int LoseNewWrites { get; set; }

    /// <summary>When set, the next new write whose path contains <c>PathContains</c> is committed by Xero and then the "process" is cancelled mid-request.</summary>
    public (string PathContains, CancellationTokenSource Process)? CrashOnNewWrite { get; set; }

    /// <summary>When set, every 429 reaches the client without its <c>Retry-After</c> (Xero may omit it; design §6.5: then 60 s).</summary>
    public bool StripRetryAfter { get; set; }

    /// <summary>When set, every answer carries this <c>X-MinLimit-Remaining</c> (a minute nearly spent by another client of the same app).</summary>
    public int? MinuteRemaining { get; set; }

    /// <summary>The paths of the writes the hop cut off by a crash.</summary>
    public List<string> Crashed { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isNew = request.Method != HttpMethod.Get
                    && request.Headers.TryGetValues("Idempotency-Key", out var keys)
                    && _seen.Add(keys.First());
        var response = await base.SendAsync(request, cancellationToken);
        if (StripRetryAfter && response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            response.Headers.RetryAfter = null;

        if (MinuteRemaining is { } remaining)
        {
            response.Headers.Remove("X-MinLimit-Remaining");
            response.Headers.TryAddWithoutValidation("X-MinLimit-Remaining", remaining.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (isNew && CrashOnNewWrite is { } crash && request.RequestUri!.AbsolutePath.Contains(crash.PathContains, StringComparison.Ordinal))
        {
            CrashOnNewWrite = null;
            Crashed.Add(request.RequestUri.AbsolutePath);
            response.Dispose();
            await crash.Process.CancelAsync();
            throw new OperationCanceledException("Simulated: TempestOS stopped after Xero committed the write.", crash.Process.Token);
        }

        if (isNew && LoseNewWrites > 0)
        {
            LoseNewWrites--;
            response.Dispose();
            throw new HttpRequestException("Simulated: the answer to a new write was lost after Xero committed it.");
        }

        return response;
    }
}

/// <summary>
/// The X6 engine end to end over the simulator, with no network and a
/// hand-moved clock: quotes (X3) and purchase orders and bills (X5) planned,
/// queued, drained and read back by <see cref="XeroSyncService"/> through
/// <see cref="XeroAccountingApi"/> → <see cref="XeroWriteSafetyHandler"/> →
/// <see cref="XeroRateLimiter"/> → <see cref="EngineFaultHop"/> →
/// <see cref="XeroApiSimulator"/>. The X1 settings are the real reader over
/// the simulator (cached in a temp folder). The client <see cref="ClientReference"/>,
/// the supplier <see cref="SupplierReference"/> and the "General expenses"
/// organisation <see cref="GeneralReference"/> are linked to seeded Xero
/// contacts; <see cref="UnlinkedReference"/> is not. A "restart" is
/// <see cref="NewEngine"/>: a fresh engine over the same durable stores.
/// </summary>
internal sealed class EngineTestKit : IDisposable
{
    public const string TenantId = XeroTestAuthoriser.TenantId;
    public const string ClientReference = "ACME1";
    public const string SupplierReference = "STEEL1";
    public const string GeneralReference = "GENEX";
    public const string UnlinkedReference = "NEWCO";

    private readonly HttpClient _client;
    private readonly TempDirectory _temp;

    private EngineTestKit(
        TempDirectory temp, XeroApiSimulator simulator, XeroSimulatorClock clock, HttpClient client, XeroAccountingApi api, InMemorySecretStore secrets,
        XeroRateLimiter rateLimiter, EngineFaultHop hop, XeroSettingsReader reader, YieldingInMemoryPersistenceStore store, XeroContactLinker linker,
        PersistenceXeroLinkStore links, XeroSyncOptions options)
    {
        _temp = temp;
        Simulator = simulator;
        Clock = clock;
        _client = client;
        Api = api;
        Secrets = secrets;
        RateLimiter = rateLimiter;
        Hop = hop;
        Reader = reader;
        Store = store;
        Linker = linker;
        Links = links;
        Options = options;
        Outbox = new PersistenceXeroOutbox(store, null, clock);
        SettingsProvider = new InMemorySettingsProvider();
        var taxTypes = new XeroTaxTypeResolver(reader, SettingsProvider);
        var accounts = new XeroAccountCodeMap(reader, SettingsProvider);

        QuotePlanner = new XeroQuotePlanner(Quotes, Links, Outbox, store, secrets, Files, Audit, clock, new XeroQuotePlannerOptions { AutomaticFromUtc = DateTimeOffset.MinValue });
        QuoteHandler = new XeroQuotePushHandler(api, Links, Quotes, store, linker, taxTypes, accounts, Audit, clock);
        QuoteAttachments = new XeroQuoteAttachmentHandler(api, Links, Quotes, Files, clock);

        PurchasingState = new XeroPurchasingSyncState(store, secrets, Audit, clock, new XeroPurchasingPlannerOptions { AutomaticFromUtc = DateTimeOffset.MinValue });
        Creates = new XeroPurchasingCreateLog(store);
        GeneralContact = new XeroGeneralExpensesContact(SettingsProvider);
        OrderPlanner = new XeroPurchaseOrderPlanner(Orders, Links, Outbox, PurchasingState, Files);
        ExpensePlanner = new XeroExpenseBillPlanner(Expenses, Links, Outbox, PurchasingState, Files, Orders);
        OrderHandler = new XeroPurchaseOrderPushHandler(api, Links, Creates, Orders, linker, taxTypes, accounts, Audit, clock);
        BillHandler = new XeroExpenseBillPushHandler(api, Links, Creates, Expenses, linker, GeneralContact, taxTypes, accounts, Audit, clock);
        OrderAttachments = new XeroPurchaseOrderAttachmentHandler(api, Links, Orders, Files, clock);
        BillAttachments = new XeroExpenseBillAttachmentHandler(api, Links, Files, clock);
        SendAgain = new XeroPurchasingSendAgain(Creates, Outbox, Links, PurchasingState, Orders, Expenses, Audit, clock);

        Parts = new XeroSyncParts(
            Links, Outbox, secrets,
            QuotePlanner, Quotes, QuoteHandler, QuoteAttachments,
            orderPlanner: OrderPlanner, orders: Orders, orderHandler: OrderHandler, orderAttachments: OrderAttachments,
            expensePlanner: ExpensePlanner, expenses: Expenses, billHandler: BillHandler, billAttachments: BillAttachments,
            purchasingState: PurchasingState, creates: Creates, sendAgain: SendAgain);
        ReadBack = new XeroReadBack(api, Links, null, rateLimiter, Audit, clock);
        Engine = NewEngine();
    }

    public XeroApiSimulator Simulator { get; }

    public XeroSimulatorClock Clock { get; }

    public XeroAccountingApi Api { get; }

    public InMemorySecretStore Secrets { get; }

    public XeroRateLimiter RateLimiter { get; }

    public EngineFaultHop Hop { get; }

    public XeroSettingsReader Reader { get; }

    public YieldingInMemoryPersistenceStore Store { get; }

    public XeroContactLinker Linker { get; }

    public PersistenceXeroLinkStore Links { get; }

    public PersistenceXeroOutbox Outbox { get; }

    public InMemorySettingsProvider SettingsProvider { get; }

    public XeroSyncOptions Options { get; }

    public RecordingAuditRecorder Audit { get; } = new();

    public EngineFiles Files { get; } = new();

    public FakeQuoteSource Quotes { get; } = new();

    public FakePurchaseOrderSource Orders { get; } = new();

    public FakeExpenseSource Expenses { get; } = new();

    public FakeConnectionState Connection { get; } = new();

    public WorkspaceChangeFeed Changes { get; } = new();

    public XeroQuotePlanner QuotePlanner { get; }

    public XeroQuotePushHandler QuoteHandler { get; }

    public XeroQuoteAttachmentHandler QuoteAttachments { get; }

    public XeroPurchasingSyncState PurchasingState { get; }

    public XeroPurchasingCreateLog Creates { get; }

    public XeroGeneralExpensesContact GeneralContact { get; }

    public XeroPurchaseOrderPlanner OrderPlanner { get; }

    public XeroExpenseBillPlanner ExpensePlanner { get; }

    public XeroPurchaseOrderPushHandler OrderHandler { get; }

    public XeroExpenseBillPushHandler BillHandler { get; }

    public XeroPurchaseOrderAttachmentHandler OrderAttachments { get; }

    public XeroExpenseBillAttachmentHandler BillAttachments { get; }

    public XeroPurchasingSendAgain SendAgain { get; }

    public XeroSyncParts Parts { get; }

    public XeroReadBack ReadBack { get; }

    /// <summary>The engine of the current "process"; <see cref="Restart"/> replaces it.</summary>
    public XeroSyncService Engine { get; private set; }

    /// <summary>The change observer of the current engine.</summary>
    public XeroChangeObserver Observer { get; private set; } = null!;

    /// <summary>How many requests the kit's own set-up sent.</summary>
    public int SetupRequestCount { get; private set; }

    /// <summary>The requests the code under test sent (after set-up).</summary>
    public IReadOnlyList<XeroSimulatedRequest> Requests => [.. Simulator.Requests.Skip(SetupRequestCount)];

    /// <summary>The Xero <c>ContactID</c>s seeded and linked during set-up.</summary>
    public string ClientContactId { get; private set; } = string.Empty;

    public string SupplierContactId { get; private set; } = string.Empty;

    public string GeneralContactId { get; private set; } = string.Empty;

    public static async Task<EngineTestKit> CreateAsync(XeroSyncOptions? options = null)
    {
        var temp = new TempDirectory();
        var clock = new XeroSimulatorClock();
        var simulator = new XeroApiSimulator(new XeroSimulatorOptions(TenantId: TenantId, AccessToken: XeroTestAuthoriser.AccessToken), clock);

        XeroSettingsReader? reader = null;
        var hop = new EngineFaultHop { InnerHandler = simulator };
        var rateLimiter = new XeroRateLimiter(clock) { InnerHandler = hop };
        var safety = new XeroWriteSafetyHandler(() => reader, _ => Task.FromResult(false), () => new RecordingAuditRecorder(), timeProvider: clock)
        {
            InnerHandler = rateLimiter,
        };
        var client = new HttpClient(safety) { BaseAddress = XeroApiSimulator.BaseAddress };
        var (authoriser, secrets) = await XeroTestAuthoriser.CreateAsync(TenantId);
        var api = new XeroAccountingApi(client, authoriser, clock);
        reader = new XeroSettingsReader(api, new FileXeroSettingsCache(Path.Combine(temp.Path, "accounts")), secrets, null, clock);

        var store = new YieldingInMemoryPersistenceStore();
        var links = new PersistenceXeroLinkStore(store);
        var catalog = OperationsFixtures.BuildOrganisationCatalog();
        await RegisterAsync(catalog, ClientReference, "Acme Engineering Ltd", PartyKind.Customer);
        await RegisterAsync(catalog, SupplierReference, "Northern Steel Ltd", PartyKind.Supplier);
        await RegisterAsync(catalog, GeneralReference, "Director expenses", PartyKind.Supplier);
        await RegisterAsync(catalog, UnlinkedReference, "Newco Fixings Ltd", PartyKind.Supplier);

        var linker = new XeroContactLinker(
            api, links, catalog, secrets, timeProvider: clock, options: new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });

        var kit = new EngineTestKit(
            temp, simulator, clock, client, api, secrets, rateLimiter, hop, reader, store, linker, links,
            options ?? new XeroSyncOptions { Jitter = () => 0.5 });

        kit.ClientContactId = simulator.SeedContact("Acme Engineering Ltd");
        kit.SupplierContactId = simulator.SeedContact("Northern Steel Ltd");
        kit.GeneralContactId = simulator.SeedContact("Director expenses");
        Assert.Equal(ConnectorOutcome.Ok, (await linker.LinkExistingAsync(ClientReference, kit.ClientContactId)).Outcome);
        Assert.Equal(ConnectorOutcome.Ok, (await linker.LinkExistingAsync(SupplierReference, kit.SupplierContactId)).Outcome);
        Assert.Equal(ConnectorOutcome.Ok, (await linker.LinkExistingAsync(GeneralReference, kit.GeneralContactId)).Outcome);
        await kit.GeneralContact.SetAsync(GeneralReference);

        kit.SetupRequestCount = simulator.Requests.Count;
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

    /// <summary>A fresh engine (and change observer) over the kit's durable stores and pipeline: TempestOS restarted.</summary>
    public XeroSyncService NewEngine()
    {
        Observer = new XeroChangeObserver(Changes);
        return new XeroSyncService(
            Parts, Outbox, Store, Reader, Connection, RateLimiter, ReadBack, Observer, importer: null, Audit,
            configuration: null, logger: null, timeProvider: Clock, options: Options);
    }

    /// <summary>The process stops (its observer unsubscribes) and starts again: a new engine; nothing in memory survives.</summary>
    public XeroSyncService Restart()
    {
        Engine.StopListening();
        Engine = NewEngine();
        return Engine;
    }

    /// <summary>A saved change, as the workspace announces it after a commit.</summary>
    public void Saved(string canonicalKind, Guid id, WorkspaceChangeType type = WorkspaceChangeType.Updated) =>
        Changes.Publish(new WorkspaceChange(1, [new WorkspaceChangeEntry(id, canonicalKind, type)]));

    /// <summary>Runs engine cycles until one sends nothing and nothing is due now (at most <paramref name="maximumCycles"/>).</summary>
    public async Task<IReadOnlyList<XeroSyncCycleReport>> SettleAsync(int maximumCycles = 20)
    {
        var reports = new List<XeroSyncCycleReport>();
        for (var i = 0; i < maximumCycles; i++)
        {
            var report = await Engine.RunCycleAsync();
            reports.Add(report);
            if (report.Drain.Attempted == 0 && report.Planned == 0)
                break;
        }

        return reports;
    }

    // ------------------------------------------------------------ records

    public static XeroDocumentRef QuoteRef(Guid id) => XeroDocumentRef.For(XeroDocumentKind.Quote, id);

    public static XeroDocumentRef OrderRef(Guid id) => XeroPurchaseOrderPlanner.Ref(id);

    public static XeroDocumentRef ExpenseRef(Guid id) => XeroExpenseBillPlanner.Ref(id);

    /// <summary>A quotation (fictional): approved R1 by default, client <see cref="ClientReference"/>.</summary>
    public static XeroQuoteSnapshot Quote(Guid id, QuotationStatus status = QuotationStatus.Approved, string reference = "P0012-Q-001") =>
        QuoteSyncTestKit.Quote(id, status, reference: reference, client: ClientReference);

    /// <summary>A purchase order (fictional): issued, two lines, supplier <see cref="SupplierReference"/>.</summary>
    public static XeroPurchaseOrderSnapshot Order(Guid id, PurchaseOrderStatus status = PurchaseOrderStatus.Issued, string reference = "PO-2026-001", string? supplier = SupplierReference) =>
        PurchasingSyncTestKit.Order(id, status, reference, supplier);

    /// <summary>An expense (fictional): a £100 + £20 VAT train fare, billed to the "General expenses" contact.</summary>
    public static XeroExpenseSnapshot Expense(Guid id, string? supplier = null, bool deleted = false) =>
        PurchasingSyncTestKit.Expense(id, supplier, deleted: deleted);

    /// <summary>Approves and exports a quotation: it exists in TempestOS with its PDF, and the workspace announces the save.</summary>
    public Guid ExportQuote(string reference = "P0012-Q-001")
    {
        var id = Guid.NewGuid();
        Quotes[id] = Quote(id, reference: reference);
        Files.Store(QuoteRef(id), $"{reference} R1 sheet", "quote.pdf");
        Saved(Quotation.CanonicalKind, id);
        return id;
    }

    public void SetQuoteStatus(Guid id, QuotationStatus status)
    {
        Quotes[id] = Quotes[id] with { Status = status };
        Saved(Quotation.CanonicalKind, id, WorkspaceChangeType.StatusChanged);
    }

    public Guid IssueOrder(string reference = "PO-2026-001", string? supplier = SupplierReference)
    {
        var id = Guid.NewGuid();
        Orders[id] = Order(id, reference: reference, supplier: supplier);
        Files.Store(OrderRef(id), $"{reference} sheet", $"{reference}.pdf");
        Saved(PurchaseOrder.CanonicalKind, id, WorkspaceChangeType.StatusChanged);
        return id;
    }

    public Guid RecordExpense(string? supplier = null)
    {
        var id = Guid.NewGuid();
        Expenses[id] = Expense(id, supplier);
        Files.Store(ExpenseRef(id), "receipt", "receipt.jpg", "image/jpeg");
        Saved(Tempest.Core.Expenses.ProjectExpense.CanonicalKind, id, WorkspaceChangeType.Created);
        return id;
    }

    // ------------------------------------------------------------ Xero side

    public IReadOnlyList<XeroSimulatedDocument> LiveQuotes => [.. Simulator.All("Quotes").Where(q => q.Status != "DELETED")];

    public IReadOnlyList<XeroSimulatedDocument> LiveOrders => [.. Simulator.All("PurchaseOrders").Where(o => o.Status != "DELETED")];

    public IReadOnlyList<XeroSimulatedDocument> Bills => [.. Simulator.All("Invoices").Where(i => i.Body["Type"]?.GetValue<string>() == "ACCPAY")];

    public IReadOnlyList<XeroSimulatedRequest> WritesTo(string resource) =>
        [.. Requests.Where(r => r.Method != HttpMethod.Get && r.Path.StartsWith(resource, StringComparison.Ordinal))];

    public Task<XeroLink?> LinkAsync(XeroDocumentRef document) => Links.FindAsync(TenantId, document);

    /// <summary>Every audit row written with <paramref name="action"/>.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>?> AuditRows(string action) => [.. Audit.Rows.Where(r => r.Action == action).Select(r => r.Detail)];

    /// <summary>Asserts the simulator saw no contract or safety violation, nothing was emailed, and no audit row carries the token.</summary>
    public void AssertNoViolations()
    {
        Assert.Empty(Simulator.Violations);
        Assert.DoesNotContain(Simulator.Requests, r => r.Path.EndsWith("/Email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Audit.Rows, r => r.Detail?.Values.Any(v => v.Contains(XeroTestAuthoriser.AccessToken, StringComparison.Ordinal)) == true);
    }

    public void Dispose()
    {
        Engine.StopListening();
        _client.Dispose();
        Simulator.Dispose();
        _temp.Dispose();
    }
}
