using System.Security.Cryptography;
using System.Text;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Contacts;
using Tempest.Core.Tests.Invoicing.Xero.Settings;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;

/// <summary>An <see cref="IXeroPurchaseOrderSource"/> holding whatever purchase orders a test sets.</summary>
internal sealed class FakePurchaseOrderSource : IXeroPurchaseOrderSource
{
    private readonly Dictionary<Guid, XeroPurchaseOrderSnapshot> _orders = [];

    public XeroPurchaseOrderSnapshot this[Guid id]
    {
        get => _orders[id];
        set => _orders[id] = value;
    }

    /// <summary>The order is no longer readable at all (purged, or its store unreadable).</summary>
    public void Remove(Guid id) => _orders.Remove(id);

    public Task<XeroPurchaseOrderSnapshot?> FindAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_orders.TryGetValue(purchaseOrderId, out var order) ? order : null);

    public Task<IReadOnlyList<Guid>> ListIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([.. _orders.Keys]);
}

/// <summary>An <see cref="IXeroExpenseSource"/> holding whatever expenses a test sets.</summary>
internal sealed class FakeExpenseSource : IXeroExpenseSource
{
    private readonly Dictionary<Guid, XeroExpenseSnapshot> _expenses = [];

    public XeroExpenseSnapshot this[Guid id]
    {
        get => _expenses[id];
        set => _expenses[id] = value;
    }

    /// <summary>The expense is no longer readable at all (purged, or its store unreadable).</summary>
    public void Remove(Guid id) => _expenses.Remove(id);

    public Task<XeroExpenseSnapshot?> FindAsync(Guid expenseId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_expenses.TryGetValue(expenseId, out var expense) ? expense : null);

    public Task<IReadOnlyList<Guid>> ListIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([.. _expenses.Keys]);
}

/// <summary>An <see cref="IXeroDocumentFileSource"/> holding the issued PDF or receipt a test stores, as the Desktop export or the receipt attachment would.</summary>
internal sealed class FakePurchasingFileSource : IXeroDocumentFileSource
{
    private readonly Dictionary<XeroDocumentRef, XeroDocumentFile> _files = [];

    public XeroDocumentFile Store(XeroDocumentRef document, string text, string fileName, string contentType = "application/pdf")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var file = new XeroDocumentFile(fileName, contentType, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        _files[document] = file;
        return file;
    }

    public Task<XeroDocumentFile?> FindAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.TryGetValue(document, out var file) ? file : null);
}

/// <summary>
/// Loses Xero's answer to the next <see cref="LoseNewWrites"/> writes whose
/// <c>Idempotency-Key</c> it has not seen before — a new create's answer —
/// while a replay of an earlier key (the handlers' recovery) goes through.
/// </summary>
internal sealed class NewWriteLossHandler : DelegatingHandler
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    public int LoseNewWrites { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isNew = request.Method != HttpMethod.Get
                    && request.Headers.TryGetValues("Idempotency-Key", out var keys)
                    && _seen.Add(keys.First());
        var response = await base.SendAsync(request, cancellationToken);
        if (isNew && LoseNewWrites > 0)
        {
            LoseNewWrites--;
            response.Dispose();
            throw new HttpRequestException("Simulated: the answer to a new write was lost after Xero committed it.");
        }

        return response;
    }
}

/// <summary>One attempt the test drain made: the entry as claimed and what its handler answered.</summary>
internal sealed record PurchasingDrainStep(XeroOutboxEntry Entry, XeroPushResult Result);

/// <summary>
/// The X5 purchase-order and expense-bill sync end to end, with no network
/// and a hand-moved clock: the planners → the B2 outbox → a minimal drain
/// (design §6.3, standing in for X6's engine) → the push and attachment
/// handlers → <see cref="XeroAccountingApi"/> → <see cref="XeroWriteSafetyHandler"/>
/// → <see cref="XeroRateLimiter"/> → a lost-response hop →
/// <see cref="XeroApiSimulator"/>. The supplier <see cref="SupplierReference"/>
/// and the "General expenses" organisation <see cref="GeneralReference"/>
/// are linked (X2) to seeded Xero contacts; <see cref="UnlinkedReference"/>
/// is not. The X1 reading carries the UK Demo Company's tax rates and
/// accounts.
/// </summary>
internal sealed class PurchasingSyncTestKit : IDisposable
{
    public const string TenantId = XeroTestAuthoriser.TenantId;
    public const string SupplierReference = "STEEL1";
    public const string GeneralReference = "GENEX";
    public const string UnlinkedReference = "NEWCO";

    private readonly HttpClient _client;

    private PurchasingSyncTestKit(
        XeroApiSimulator simulator, XeroSimulatorClock clock, HttpClient client, XeroAccountingApi api, InMemorySecretStore secrets,
        YieldingInMemoryPersistenceStore store, FakeSettingsReader settings, LostResponseHandler lost, XeroContactLinker linker,
        string supplierContactId, string generalContactId, IXeroPurchaseOrderSource orders, IXeroExpenseSource expenses, XeroPurchasingPlannerOptions options)
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
        SupplierContactId = supplierContactId;
        GeneralContactId = generalContactId;
        Orders = orders;
        Expenses = expenses;
        Links = new PersistenceXeroLinkStore(store);
        Outbox = new PersistenceXeroOutbox(store, null, clock);
        SettingsProvider = new InMemorySettingsProvider();
        GeneralContact = new XeroGeneralExpensesContact(SettingsProvider);
        State = new XeroPurchasingSyncState(store, secrets, Audit, clock, options);
        var taxTypes = new XeroTaxTypeResolver(settings, SettingsProvider);
        var accounts = new XeroAccountCodeMap(settings, SettingsProvider);
        OrderPlanner = new XeroPurchaseOrderPlanner(orders, Links, Outbox, State, Files);
        ExpensePlanner = new XeroExpenseBillPlanner(expenses, Links, Outbox, State, Files, orders);
        Creates = new XeroPurchasingCreateLog(store);
        OrderHandler = new XeroPurchaseOrderPushHandler(api, Links, Creates, orders, linker, taxTypes, accounts, Audit, clock);
        BillHandler = new XeroExpenseBillPushHandler(api, Links, Creates, expenses, linker, GeneralContact, taxTypes, accounts, Audit, clock);
        OrderAttachments = new XeroPurchaseOrderAttachmentHandler(api, Links, orders, Files, clock);
        BillAttachments = new XeroExpenseBillAttachmentHandler(api, Links, Files, clock);
    }

    public XeroApiSimulator Simulator { get; }

    /// <summary>How many requests the kit's own set-up (linking the contacts) sent.</summary>
    public int SetupRequestCount { get; private set; }

    /// <summary>The requests the simulator received after the kit's set-up — what the code under test sent.</summary>
    public IReadOnlyList<XeroSimulatedRequest> Requests => [.. Simulator.Requests.Skip(SetupRequestCount)];

    public XeroSimulatorClock Clock { get; }

    public XeroAccountingApi Api { get; }

    public InMemorySecretStore Secrets { get; }

    public YieldingInMemoryPersistenceStore Store { get; }

    public FakeSettingsReader Settings { get; }

    public InMemorySettingsProvider SettingsProvider { get; }

    public LostResponseHandler Lost { get; }

    /// <summary>Loses the answer to a new write only (a replay of an earlier key goes through).</summary>
    public NewWriteLossHandler LostNew { get; private init; } = new();

    public XeroContactLinker Linker { get; }

    /// <summary>The Xero <c>ContactID</c> the supplier <see cref="SupplierReference"/> is linked to.</summary>
    public string SupplierContactId { get; }

    /// <summary>The Xero <c>ContactID</c> the "General expenses" organisation <see cref="GeneralReference"/> is linked to.</summary>
    public string GeneralContactId { get; }

    public IXeroPurchaseOrderSource Orders { get; }

    public IXeroExpenseSource Expenses { get; }

    public FakePurchaseOrderSource FakeOrders => (FakePurchaseOrderSource)Orders;

    public FakeExpenseSource FakeExpenses => (FakeExpenseSource)Expenses;

    public FakePurchasingFileSource Files { get; } = new();

    public RecordingAuditRecorder Audit { get; } = new();

    public PersistenceXeroLinkStore Links { get; }

    public PersistenceXeroOutbox Outbox { get; }

    public XeroGeneralExpensesContact GeneralContact { get; }

    /// <summary>The handlers' log of creates sent to Xero.</summary>
    public XeroPurchasingCreateLog Creates { get; }

    public XeroPurchasingSyncState State { get; }

    public XeroPurchaseOrderPlanner OrderPlanner { get; }

    public XeroExpenseBillPlanner ExpensePlanner { get; }

    public XeroPurchaseOrderPushHandler OrderHandler { get; }

    public XeroExpenseBillPushHandler BillHandler { get; }

    public XeroPurchaseOrderAttachmentHandler OrderAttachments { get; }

    public XeroExpenseBillAttachmentHandler BillAttachments { get; }

    public static async Task<PurchasingSyncTestKit> CreateAsync(
        IXeroPurchaseOrderSource? orders = null, IXeroExpenseSource? expenses = null, XeroPurchasingPlannerOptions? options = null,
        IOrganisationCatalog? organisations = null, bool chooseGeneralContact = true)
    {
        var clock = new XeroSimulatorClock();
        var simulator = new XeroApiSimulator(new XeroSimulatorOptions(TenantId: TenantId, AccessToken: XeroTestAuthoriser.AccessToken), clock);

        var settings = new FakeSettingsReader { Cached = UkDemoReading() };
        var lostNew = new NewWriteLossHandler { InnerHandler = simulator };
        var lost = new LostResponseHandler { InnerHandler = lostNew };
        var rateLimiter = new XeroRateLimiter(clock) { InnerHandler = lost };
        var safety = new XeroWriteSafetyHandler(() => settings, _ => Task.FromResult(false), () => new RecordingAuditRecorder(), timeProvider: clock)
        {
            InnerHandler = rateLimiter,
        };
        var client = new HttpClient(safety) { BaseAddress = XeroApiSimulator.BaseAddress };
        var (authoriser, secretStore) = await XeroTestAuthoriser.CreateAsync(TenantId);
        var api = new XeroAccountingApi(client, authoriser, clock);

        var store = new YieldingInMemoryPersistenceStore();
        var catalog = organisations ?? OperationsFixtures.BuildOrganisationCatalog();
        await RegisterOrganisationAsync(catalog, SupplierReference, "Northern Steel Ltd", PartyKind.Supplier);
        await RegisterOrganisationAsync(catalog, GeneralReference, "Director expenses", PartyKind.Supplier);
        await RegisterOrganisationAsync(catalog, UnlinkedReference, "Newco Fixings Ltd", PartyKind.Supplier);

        var linker = new XeroContactLinker(
            api, new PersistenceXeroLinkStore(store), catalog, secretStore, timeProvider: clock,
            options: new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });
        var supplierContactId = simulator.SeedContact("Northern Steel Ltd");
        var generalContactId = simulator.SeedContact("Director expenses");
        Assert.Equal(ConnectorOutcome.Ok, (await linker.LinkExistingAsync(SupplierReference, supplierContactId)).Outcome);
        Assert.Equal(ConnectorOutcome.Ok, (await linker.LinkExistingAsync(GeneralReference, generalContactId)).Outcome);

        var kit = new PurchasingSyncTestKit(
            simulator, clock, client, api, secretStore, store, settings, lost, linker, supplierContactId, generalContactId,
            orders ?? new FakePurchaseOrderSource(), expenses ?? new FakeExpenseSource(),
            options ?? new XeroPurchasingPlannerOptions { AutomaticFromUtc = DateTimeOffset.MinValue })
        {
            LostNew = lostNew,
        };

        if (chooseGeneralContact)
            await kit.GeneralContact.SetAsync(GeneralReference);

        kit.SetupRequestCount = simulator.Requests.Count;

        return kit;
    }

    private static async Task RegisterOrganisationAsync(IOrganisationCatalog catalog, string reference, string name, PartyKind role)
    {
        if (await catalog.FindByReferenceAsync(reference) is not null)
            return;

        await OperationsFixtures.RegisterAsync((OrganisationCatalog)catalog, reference, new Organisation
        {
            Reference = reference,
            Name = name,
            CustomerCode = reference,
            Status = RelationshipStatus.Active,
            Roles = [role],
        });
    }

    /// <summary>An X1 reading of the simulated UK Demo Company: its tax rates and accounts, as Xero answers them.</summary>
    public static XeroSettingsReading UkDemoReading() => FakeSettingsReader.Reading(TenantId, isDemoCompany: true) with
    {
        TaxRates = [.. SimulatorSeed.UkDemoTaxRates().Select(r => new XeroTaxRate(r.TaxType, r.Name, r.EffectiveRate, r.Status, r.CanApplyToRevenue, r.CanApplyToExpenses))],
        Accounts = [.. SimulatorSeed.UkDemoAccounts().Select(a => new XeroAccount(a.AccountId, a.Code, a.Name, a.Type, a.Class, a.Status, a.TaxType))],
    };

    /// <summary>A purchase order as the planner reads it (fictional): issued, two lines, supplier <see cref="SupplierReference"/>.</summary>
    public static XeroPurchaseOrderSnapshot Order(
        Guid id, PurchaseOrderStatus status = PurchaseOrderStatus.Issued, string reference = "PO-2026-001", string? supplier = SupplierReference,
        DateOnly? issued = null, IReadOnlyList<XeroPurchaseOrderLine>? lines = null) => new(
            id, reference, status, "P0012", supplier,
            status == PurchaseOrderStatus.Draft ? null : issued ?? new DateOnly(2026, 10, 2),
            new DateOnly(2026, 10, 16), "GBP",
            lines ??
            [
                new XeroPurchaseOrderLine("Steel plate 10 mm", 10m, 50m, VatRate.Standard),
                new XeroPurchaseOrderLine("Freight", 1m, 75m, VatRate.Zero),
            ]);

    /// <summary>An expense as the planner reads it (fictional): a £100 + £20 VAT train fare, no supplier.</summary>
    public static XeroExpenseSnapshot Expense(
        Guid id, string? supplier = null, string? supplierInvoiceNumber = null, decimal net = 100m, decimal vat = 20m,
        ExpenseCategory category = ExpenseCategory.Travel, string description = "Train to Sheffield", Guid? sourcePurchaseOrderId = null,
        bool deleted = false, DateTimeOffset? recordedAt = null) => new(
            id, "P0012", new DateOnly(2026, 10, 1), description, category, net, vat, "GBP",
            supplier, null, supplierInvoiceNumber, sourcePurchaseOrderId, deleted, recordedAt ?? new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));

    public static XeroDocumentRef OrderRef(Guid id) => XeroPurchaseOrderPlanner.Ref(id);

    public static XeroDocumentRef ExpenseRef(Guid id) => XeroExpenseBillPlanner.Ref(id);

    public Task<XeroLink?> OrderLinkAsync(Guid id) => Links.FindAsync(TenantId, OrderRef(id));

    public Task<XeroLink?> ExpenseLinkAsync(Guid id) => Links.FindAsync(TenantId, ExpenseRef(id));

    public Task<IReadOnlyList<XeroOutboxEntry>> PlanOrderAsync(Guid id) => OrderPlanner.PlanAndEnqueueAsync(id);

    public Task<IReadOnlyList<XeroOutboxEntry>> PlanExpenseAsync(Guid id) => ExpensePlanner.PlanAndEnqueueAsync(id);

    /// <summary>
    /// Drains the outbox as design §6.3 says, one entry at a time, handing
    /// each to the handler for its operation and document kind:
    /// Succeeded/NothingToDo → Succeeded; Rejected/Blocked → Failed;
    /// RetryLater → Pending after the backoff (a 429 pauses the drain);
    /// Reauthorise → WaitingForAuthorisation, and the drain pauses;
    /// Unknown → Unknown.
    /// </summary>
    public async Task<IReadOnlyList<PurchasingDrainStep>> DrainAsync(int maximumAttempts = 100)
    {
        var steps = new List<PurchasingDrainStep>();
        while (steps.Count < maximumAttempts && await Outbox.ClaimNextDueAsync() is { } entry)
        {
            IXeroPushHandler handler = (entry.Operation, entry.Document.Kind) switch
            {
                (XeroOperation.UploadAttachment, XeroDocumentKind.PurchaseOrder) => OrderAttachments,
                (XeroOperation.UploadAttachment, _) => BillAttachments,
                (_, XeroDocumentKind.PurchaseOrder) => OrderHandler,
                _ => BillHandler,
            };
            Assert.Contains(entry.Operation, handler.Operations);

            var result = await handler.PushAsync(TenantId, entry);
            steps.Add(new PurchasingDrainStep(entry, result));

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
                        return steps;
                    break;
                default:
                    await Outbox.RecordOutcomeAsync(entry.Id, XeroOutboxState.Unknown, result.Reason);
                    break;
            }
        }

        return steps;
    }

    /// <summary>The live (not deleted) Xero purchase orders the simulator holds.</summary>
    public IReadOnlyList<XeroSimulatedDocument> LiveOrders => [.. Simulator.All("PurchaseOrders").Where(o => o.Status != "DELETED")];

    /// <summary>The bills (<c>ACCPAY</c>) the simulator holds, every status.</summary>
    public IReadOnlyList<XeroSimulatedDocument> Bills =>
        [.. Simulator.All("Invoices").Where(i => i.Body["Type"]?.GetValue<string>() == "ACCPAY")];

    /// <summary>The live (not deleted or voided) bills the simulator holds.</summary>
    public IReadOnlyList<XeroSimulatedDocument> LiveBills => [.. Bills.Where(b => b.Status is not ("DELETED" or "VOIDED"))];

    /// <summary>The writes TempestOS sent to <paramref name="resource"/>.</summary>
    public IReadOnlyList<XeroSimulatedRequest> WritesTo(string resource) =>
        [.. Simulator.Requests.Where(r => r.Method != HttpMethod.Get && r.Path.StartsWith(resource, StringComparison.Ordinal))];

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
