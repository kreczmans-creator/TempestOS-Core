using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Deliverables;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Settings;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;
using Tempest.Core.Tests.Plugins;
using Tempest.Workspace;
using Tempest.Workspace.Projects;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// The Demo Company smoke journey through TempestOS's <b>production</b> Xero
/// path (`v0.24.0` review board m20): nothing here builds a Xero request
/// body. Each record goes through the same code the app runs — the X2
/// <see cref="XeroContactLinker"/>; the X3 quote planner, mapper and push
/// handler; the X4 <see cref="InvoicingService"/> bound to the real
/// <see cref="XeroConnector"/> and <see cref="XeroInvoiceDrafts"/>; the X5
/// purchase-order and expense-bill planners, mappers and handlers; and the
/// X6 <see cref="XeroSyncService"/> draining the durable outbox and reading
/// statuses back — over the <see cref="XeroLiveConnection"/> pipeline
/// (<see cref="XeroAccountingApi"/> → the real <see cref="XeroWriteSafetyHandler"/>
/// with <i>Allow the live organisation</i> hard-wired off →
/// <see cref="XeroRateLimiter"/> → the request journal → Xero or the simulator).
/// </summary>
/// <remarks>
/// <para>
/// <b>What is not production.</b> The quote, purchase order and expense are
/// handed to the planners as snapshots (the X3/X5 source seams) instead of
/// being read from a workspace, and the invoice is raised in a throw-away
/// TempestOS host in a temporary folder: the operator's own data folder is
/// never opened — only its secret store, for the Xero tokens, exactly as the
/// hand-built journey (<see cref="XeroDemoSmokeJourney"/>) does. The PDFs are
/// <see cref="XeroDemoSmokeJourney.Pdf"/>s.
/// </para>
/// <para>
/// <b>It refuses to write unless Xero reports <c>IsDemoCompany</c></b>: P01
/// reads the organisation first and stops the run before the contact step
/// otherwise; and the safety handler below every call refuses a write to any
/// other organisation regardless (D7).
/// </para>
/// <para>
/// Steps: P01 connected, Demo Company; P02 contact linked or created (X2);
/// P03 quote DRAFT with its PDF, P04 SENT then ACCEPTED (X3, D2, Q1); P05
/// invoice sent as a DRAFT with its PDF kept internal (X4, D3, D4, Q5); P06
/// purchase order DRAFT with its PDF (X5, Q2); P07 expense bill DRAFT with its
/// receipt and the recorded VAT (X5, Q3, Q4); P08 the engine's read-back
/// agrees; P09 clean-up through the production paths (void, cancel, delete)
/// unless kept; P10 nothing that reached Xero approved, emailed or wrote
/// outside the allow-list.
/// </para>
/// </remarks>
internal sealed class XeroProductionSmokeJourney : IAsyncDisposable
{
    /// <summary>The smoke organisation's TempestOS reference.</summary>
    public const string OrganisationReference = "TOSSMOKE";

    /// <summary>The VAT the smoke expense records on its net 50 (20%, Travel).</summary>
    public const decimal ExpenseVat = 10m;

    private const string Title = "Production path";

    private readonly XeroLiveConnection _connection;
    private readonly XeroDemoSmokeOptions _options;
    private readonly XeroSyncOptions _syncOptions;
    private readonly XeroDemoSmokeReport _report = new();
    private readonly TimeProvider _time;

    private readonly FakeQuoteSource _quotes = new();
    private readonly FakePurchaseOrderSource _orders = new();
    private readonly FakeExpenseSource _expenses = new();
    private readonly SmokeFiles _files = new();
    private readonly InMemorySettingsProvider _settings = new();
    private readonly RecordingAuditRecorder _audit = new();
    private readonly YieldingInMemoryPersistenceStore _store = new();

    private TempDirectory? _temp;
    private ITempestHost? _host;
    private WorkspaceManager? _manager;
    private InvoicingService? _invoicing;
    private XeroSyncService? _engine;
    private PersistenceXeroLinkStore? _links;
    private PersistenceXeroOutbox? _outbox;
    private XeroContactLinker? _linker;
    private string _tenantId = string.Empty;
    private bool _connected;

    private Guid? _quoteId;
    private Guid? _invoiceRequestId;
    private Guid? _orderId;
    private Guid? _expenseId;

    /// <summary>Initialises a new instance of the <see cref="XeroProductionSmokeJourney"/> class.</summary>
    /// <param name="connection">The production-shaped pipeline (live or over the simulator).</param>
    /// <param name="options">How the run behaves (keep, delay, clock).</param>
    /// <param name="syncOptions">The engine's options; <see langword="null"/> for the build defaults.</param>
    public XeroProductionSmokeJourney(XeroLiveConnection connection, XeroDemoSmokeOptions options, XeroSyncOptions? syncOptions = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        _connection = connection;
        _options = options;
        _syncOptions = syncOptions ?? new XeroSyncOptions();
        _time = options.Time;
        _report.Stamp = _time.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        ProjectCode = "SMOKE-" + Base36(_time.GetUtcNow().ToUnixTimeSeconds(), 6);
    }

    /// <summary>The quote number this run uses.</summary>
    public string QuoteNumber => $"SMOKE-PQ-{_report.Stamp}";

    /// <summary>
    /// The project-centric code the run's invoice is raised under
    /// (<c>SMOKE-</c> and the run's time in base 36, so each run's invoice
    /// number <c>{code}-INV-001</c> is new to Xero).
    /// </summary>
    public string ProjectCode { get; }

    /// <summary>The purchase order number this run uses.</summary>
    public string OrderNumber => $"SMOKE-PPO-{_report.Stamp}";

    /// <summary>The supplier invoice number the run's expense records (the bill's number, Q4).</summary>
    public string BillNumber => $"SMOKE-PEXP-{_report.Stamp}";

    /// <summary>The audit rows the production components wrote (the safety handler's are on the connection).</summary>
    public RecordingAuditRecorder Audit => _audit;

    /// <summary>Runs the journey; never throws for anything Xero did — a failed step is in the report.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    public async Task<XeroDemoSmokeReport> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await PrepareAsync(cancellationToken))
                return _report;

            await ComposeAsync(cancellationToken);
            await ContactAsync(cancellationToken);
            await QuoteAsync(cancellationToken);
            await InvoiceAsync(cancellationToken);
            await PurchaseOrderAsync(cancellationToken);
            await BillAsync(cancellationToken);
            await ReadBackAsync(cancellationToken);
        }
        catch (JourneyStopped)
        {
            // The failed step is already in the report; later steps depended on it.
        }
        finally
        {
            if (_connected && !_report.RefusedToWrite && _engine is not null)
            {
                await CleanUpAsync(CancellationToken.None);
                Step("P10", "Nothing that reached Xero approved, emailed or wrote outside the allow-list (D3, D4)", JournalProblems().Count == 0,
                    JournalProblems() is { Count: > 0 } problems ? string.Join("; ", problems) : $"{_connection.Journal.Requests.Count} request(s) checked");
            }
        }

        return _report;
    }

    /// <summary>Every request in the journal that a TempestOS write must never make.</summary>
    public IReadOnlyList<string> JournalProblems() => XeroDemoSmokeJourney.JournalProblems(_connection.Journal.Requests);

    // ------------------------------------------------------------------ steps

    /// <summary>P01: connected, settings read, the Demo Company guard. False when the run must not write.</summary>
    private async Task<bool> PrepareAsync(CancellationToken cancellationToken)
    {
        var access = await _connection.Authoriser.EnsureAccessTokenAsync(cancellationToken);
        if (access.Outcome != AccessTokenOutcome.Ok || string.IsNullOrEmpty(access.TenantId))
        {
            Step("P01", "Connected to Xero's Demo Company (D7)", false,
                $"{access.Outcome}{(access.Reason is null ? string.Empty : $" ({access.Reason})")}: connect the Demo Company first (the hand-built journey's S01 says how)");
            return false;
        }

        _connected = true;
        _tenantId = access.TenantId;

        var refresh = await _connection.SettingsReader.RefreshAsync(cancellationToken);
        if (refresh.Outcome != ConnectorOutcome.Ok || refresh.Value is null)
        {
            Step("P01", "Connected to Xero's Demo Company (D7)", false, $"settings read {refresh.Outcome}: {refresh.Reason}");
            _connected = false;
            return false;
        }

        var organisation = refresh.Value.Organisation;
        _report.OrganisationName = organisation.Name;
        if (!organisation.IsDemoCompany)
        {
            _report.RefusedToWrite = true;
            Step("P01", "Connected to Xero's Demo Company (D7)", false,
                $"'{organisation.Name}' is not the Demo Company - nothing was written. Connect the Demo Company and run again.");
            return false;
        }

        Step("P01", "Connected to Xero's Demo Company (D7)", true, $"'{organisation.Name}' is the Demo Company; settings read through X1");
        return true;
    }

    /// <summary>Builds the production components over the connection: stores, X2, X3, X4, X5 and the X6 engine.</summary>
    private async Task ComposeAsync(CancellationToken cancellationToken)
    {
        _temp = new TempDirectory();
        (_host, _manager) = await InvoicingTestHost.StartAsync(_temp.Path);
        InvoicingTestHost.SignIn(_host);

        var organisations = InvoicingTestHost.Organisations(_host);
        await organisations.RegisterAsync(OrganisationReference, new Organisation
        {
            Reference = OrganisationReference,
            Name = XeroDemoSmokeJourney.ContactName,
            CustomerCode = XeroDemoSmokeJourney.ContactNumber,
            Status = RelationshipStatus.Active,
            Roles = [PartyKind.Customer, PartyKind.Supplier],
            PaymentTerms = PaymentTerms.Days30,
        }, OperationsFixtures.Verified());

        var api = _connection.Api;
        var secrets = _connection.SecretStore;
        _links = new PersistenceXeroLinkStore(_store);
        _outbox = new PersistenceXeroOutbox(_store, null, _time);
        var linker = new XeroContactLinker(api, _links, organisations, secrets, _audit, _time, options: null, attemptStore: _store);
        _linker = linker;
        var taxTypes = new XeroTaxTypeResolver(_connection.SettingsReader, _settings);
        var accounts = new XeroAccountCodeMap(_connection.SettingsReader, _settings);

        var connector = new XeroConnector(_connection.ApiClient, _connection.Authoriser);
        var drafts = new XeroInvoiceDrafts(connector, linker, taxTypes, accounts, _links, _outbox, _files, _settings, _audit, _time);
        _invoicing = new InvoicingService(
            InvoicingTestHost.Domain(_host), InvoicingTestHost.RateCards(_host), InvoicingTestHost.Timesheets(_host), InvoicingTestHost.Deliverables(_host),
            connector, organisations, _time, expenses: null, draftSync: drafts);

        var quotePlanner = new XeroQuotePlanner(_quotes, _links, _outbox, _store, secrets, _files, _audit, _time);
        var purchasingState = new XeroPurchasingSyncState(_store, secrets, _audit, _time);
        var creates = new XeroPurchasingCreateLog(_store);
        var general = new XeroGeneralExpensesContact(_settings);

        var parts = new XeroSyncParts(
            _links, _outbox, secrets,
            quotePlanner, _quotes, new XeroQuotePushHandler(api, _links, _quotes, _store, linker, taxTypes, accounts, _audit, _time), new XeroQuoteAttachmentHandler(api, _links, _quotes, _files, _time),
            new XeroInvoicePlanner(_invoicing, _files), new XeroInvoicePushHandler(_invoicing, drafts), new XeroInvoiceAttachmentPushHandler(drafts), InvoicingTestHost.Domain(_host),
            new XeroPurchaseOrderPlanner(_orders, _links, _outbox, purchasingState, _files), _orders,
            new XeroPurchaseOrderPushHandler(api, _links, creates, _orders, linker, taxTypes, accounts, _audit, _time),
            new XeroPurchaseOrderAttachmentHandler(api, _links, _orders, _files, _time),
            new XeroExpenseBillPlanner(_expenses, _links, _outbox, purchasingState, _files, _orders), _expenses,
            new XeroExpenseBillPushHandler(api, _links, creates, _expenses, linker, general, taxTypes, accounts, _audit, _time),
            new XeroExpenseBillAttachmentHandler(api, _links, _files, _time),
            purchasingState, creates, new XeroPurchasingSendAgain(creates, _outbox, _links, purchasingState, _orders, _expenses, _audit, _time));

        _engine = new XeroSyncService(
            parts, _outbox, _store, _connection.SettingsReader, connection: null, _connection.RateLimiter,
            new XeroReadBack(api, _links, _invoicing, _connection.RateLimiter, _audit, _time), observer: null, importer: null, _audit,
            configuration: null, logger: null, timeProvider: _time, options: _syncOptions);

        // X6 at start-up: the first scan fixes when automatic sync began (Q8),
        // so every record this run issues afterwards is pushed by itself.
        await _engine.StartAsync(cancellationToken);
        await _options.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }

    /// <summary>P02: the smoke organisation linked to its Xero contact by the production linker (create looks the contact number up first).</summary>
    private async Task ContactAsync(CancellationToken cancellationToken)
    {
        var linked = await RetryOn429Async(() => _linker!.CreateAsync(OrganisationReference, cancellationToken), r => r.Outcome == ConnectorOutcome.Unavailable, cancellationToken);
        var passed = linked.Outcome == ConnectorOutcome.Ok && linked.Value is not null;
        Step("P02", "Contact linked or created through XeroContactLinker (X2)", passed,
            passed ? $"{linked.Value!.LinkedBy} contact {linked.Value.XeroId} (ContactNumber {XeroDemoSmokeJourney.ContactNumber})" : $"{linked.Outcome}: {linked.Reason}");
        if (!passed)
            throw new JourneyStopped();

        Record("Contact", XeroDemoSmokeJourney.ContactName, linked.Value!.XeroId, "ACTIVE", $"https://go.xero.com/Contacts/View/{linked.Value.XeroId}");
    }

    /// <summary>P03–P04: an approved, exported quote reaches Xero as a DRAFT with its PDF; Send and Accept move it on (D2, Q1).</summary>
    private async Task QuoteAsync(CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        _quoteId = id;
        var document = XeroDocumentRef.For(XeroDocumentKind.Quote, id);
        var issued = _time.GetUtcNow();
        _quotes[id] = new XeroQuoteSnapshot(
            id, QuoteNumber, QuotationStatus.Approved, 1, "TempestOS smoke quote (production path)", "SMOKE production path", OrganisationReference,
            DateOnly.FromDateTime(issued.UtcDateTime), 30, "Smoke test - not a real offer.", "GBP",
            [new XeroQuoteLine("Smoke test design review", 2m, 150m, VatRate.Standard)], issued);
        StoreFile(document, XeroDemoSmokeJourney.Pdf($"{QuoteNumber} R1"));

        var link = await SettleAsync(document, cancellationToken);
        var read = link is null ? null : await RetryOn429Async(() => _connection.Api.GetQuoteAsync(link.XeroId, cancellationToken), IsRateLimited, cancellationToken);
        var attachments = link is null ? 0 : await AttachmentCountAsync(XeroAttachableResource.Quotes, link.XeroId, cancellationToken);
        var passed = link is not null && read?.Value?.Status == "DRAFT" && attachments >= 1;
        Step("P03", "Quote planned, mapped and pushed by X3: DRAFT in Xero with its PDF (Q1)", passed,
            link is null ? await OutboxDetailAsync(document, cancellationToken) : $"{link.XeroNumber} {read?.Value?.Status ?? read?.Outcome.ToString()}; {attachments} attachment(s)");
        if (link is null)
            throw new JourneyStopped();

        Record("Quote", QuoteNumber, link.XeroId, read?.Value?.Status ?? "?", $"https://go.xero.com/Accounts/Receivable/Quotes/View/{link.XeroId}");

        _quotes[id] = _quotes[id] with { Status = QuotationStatus.Sent };
        await SettleAsync(document, cancellationToken);
        var sent = await RetryOn429Async(() => _connection.Api.GetQuoteAsync(link.XeroId, cancellationToken), IsRateLimited, cancellationToken);

        _quotes[id] = _quotes[id] with { Status = QuotationStatus.Accepted };
        await SettleAsync(document, cancellationToken);
        var accepted = await RetryOn429Async(() => _connection.Api.GetQuoteAsync(link.XeroId, cancellationToken), IsRateLimited, cancellationToken);

        Step("P04", "Quote Sent then Accepted in TempestOS: SENT then ACCEPTED in Xero (D2)", sent.Value?.Status == "SENT" && accepted.Value?.Status == "ACCEPTED",
            $"after Send {sent.Value?.Status ?? sent.Outcome.ToString()}; after Accept {accepted.Value?.Status ?? accepted.Outcome.ToString()}");
        UpdateRecordStatus(link.XeroId, accepted.Value?.Status ?? "?");
    }

    /// <summary>P05: a completed deliverable's invoice request sent through the X4 path: a DRAFT with TempestOS's number, its PDF internal (D3, D4, Q5).</summary>
    private async Task InvoiceAsync(CancellationToken cancellationToken)
    {
        var host = _host!;
        var domain = InvoicingTestHost.Domain(host);

        var rateCards = InvoicingTestHost.RateCards(host);
        var cardId = $"SMOKE-CARD-{_report.Stamp}";
        await rateCards.RegisterAsync(cardId, RateCard(cardId), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, cardId);

        var projectId = await InvoicingTestHost.CreateProjectAsync(host, ProjectCode, "TempestOS smoke (production path)");
        var commercial = InvoicingTestHost.ProjectCommercial(host);
        await commercial.PinRateCardAsync(projectId, cardId);
        await commercial.SetClientAsync(projectId, OrganisationReference);

        var milestones = new ProjectMilestoneService(domain);
        var milestone = await milestones.CreateMilestoneAsync(projectId, "MS-SMOKE", "Smoke milestone", _time.GetUtcNow().AddDays(30));
        var deliverable = await milestones.CreateDeliverableAsync(projectId, milestone.Id, "DEL-SMOKE", "Smoke deliverable");
        var completion = await InvoicingTestHost.Deliverables(host).CompleteAsync(
            deliverable.Id, projectId, DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime), fixedPriceValue: new Money(250m, CurrencyCode.Gbp));
        if (!completion.Succeeded)
        {
            Step("P05", "Invoice sent through X4: DRAFT with TempestOS's number and its PDF kept internal (D3, D4, Q5)", false, $"completion refused: {completion.Reason}");
            throw new JourneyStopped();
        }

        var request = await InvoicingTestHost.RequestRaisedByCompletionAsync(host, completion.Completion!.Id);
        _invoiceRequestId = request.Id;
        var number = InvoicingService.InvoiceNumberFor(request);
        StoreFile(XeroInvoiceDrafts.DocumentFor(request.Id), XeroDemoSmokeJourney.Pdf(number));

        var result = await _invoicing!.SendAsync(request.Id, cancellationToken);
        var after = result.Request;
        if (after?.Status != InvoiceRequestStatus.Sent || after.ExternalId is null)
        {
            // Rate-limited or unreachable: the request is queued (X6) — let the engine send it.
            await SettleAsync(XeroInvoiceDrafts.DocumentFor(request.Id), cancellationToken);
            after = (InvoiceRequest?)await domain.Repository.FindAsync(request.Id, cancellationToken);
        }
        else
        {
            await SettleAsync(XeroInvoiceDrafts.DocumentFor(request.Id), cancellationToken);
        }

        if (after?.ExternalId is not { } invoiceId)
        {
            Step("P05", "Invoice sent through X4: DRAFT with TempestOS's number and its PDF kept internal (D3, D4, Q5)", false,
                $"{result.Refusal} {result.Reason}; {await OutboxDetailAsync(XeroInvoiceDrafts.DocumentFor(request.Id), cancellationToken)}".Trim());
            throw new JourneyStopped();
        }

        var read = await RetryOn429Async(() => _connection.Api.GetInvoiceAsync(invoiceId, cancellationToken), IsRateLimited, cancellationToken);
        var files = await RetryOn429Async(() => _connection.Api.ListAttachmentsAsync(XeroAttachableResource.Invoices, invoiceId, cancellationToken), IsRateLimited, cancellationToken);
        var attachment = files.Value?.FirstOrDefault();
        var passed = read.Value?.Status == "DRAFT" && read.Value.InvoiceNumber == number && attachment is not null && attachment.IncludeOnline != true;
        Step("P05", "Invoice sent through X4: DRAFT with TempestOS's number and its PDF kept internal (D3, D4, Q5)", passed,
            $"{read.Value?.InvoiceNumber ?? number} {read.Value?.Status ?? read.Outcome.ToString()}; request {after.Status}; attachment {attachment?.FileName ?? "none"} (online {attachment?.IncludeOnline?.ToString() ?? "unset"})");
        Record("Invoice", number, invoiceId, read.Value?.Status ?? "?", $"https://go.xero.com/AccountsReceivable/View.aspx?InvoiceID={invoiceId}");
    }

    /// <summary>P06: an issued purchase order planned, mapped and pushed by X5: a DRAFT with its PDF (D5, Q2).</summary>
    private async Task PurchaseOrderAsync(CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        _orderId = id;
        var document = XeroPurchaseOrderPlanner.Ref(id);
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        _orders[id] = new XeroPurchaseOrderSnapshot(
            id, OrderNumber, PurchaseOrderStatus.Issued, ProjectCode, OrganisationReference, today, today.AddDays(14), "GBP",
            [new XeroPurchaseOrderLine("Smoke test steel plate", 2m, 40m, VatRate.Standard)]);
        StoreFile(document, XeroDemoSmokeJourney.Pdf(OrderNumber));

        var link = await SettleAsync(document, cancellationToken);
        var read = link is null ? null : await RetryOn429Async(() => _connection.Api.GetPurchaseOrderAsync(link.XeroId, cancellationToken), IsRateLimited, cancellationToken);
        var attachments = link is null ? 0 : await AttachmentCountAsync(XeroAttachableResource.PurchaseOrders, link.XeroId, cancellationToken);
        Step("P06", "Purchase order planned, mapped and pushed by X5: DRAFT in Xero with its PDF (Q2)", link is not null && read?.Value?.Status == "DRAFT" && attachments >= 1,
            link is null ? await OutboxDetailAsync(document, cancellationToken) : $"{link.XeroNumber} {read?.Value?.Status ?? read?.Outcome.ToString()}; {attachments} attachment(s)");
        if (link is not null)
            Record("Purchase order", OrderNumber, link.XeroId, read?.Value?.Status ?? "?", $"https://go.xero.com/Accounts/Payable/PurchaseOrders/View/{link.XeroId}");
    }

    /// <summary>P07: a recorded expense planned, mapped and pushed by X5: a DRAFT bill numbered by the supplier's invoice number, receipt attached, recorded VAT kept (D5, Q3, Q4).</summary>
    private async Task BillAsync(CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        _expenseId = id;
        var document = XeroExpenseBillPlanner.Ref(id);
        var now = _time.GetUtcNow();
        _expenses[id] = new XeroExpenseSnapshot(
            id, ProjectCode, DateOnly.FromDateTime(now.UtcDateTime), "Smoke test train fare", ExpenseCategory.Travel, 50m, ExpenseVat, "GBP",
            OrganisationReference, null, BillNumber, null, false, now);
        StoreFile(document, XeroDemoSmokeJourney.Receipt(BillNumber));

        var link = await SettleAsync(document, cancellationToken);
        var read = link is null ? null : await RetryOn429Async(() => _connection.Api.GetBillAsync(link.XeroId, cancellationToken), IsRateLimited, cancellationToken);
        var attachments = link is null ? 0 : await AttachmentCountAsync(XeroAttachableResource.Invoices, link.XeroId, cancellationToken);
        var bill = read?.Value;
        Step("P07", "Expense bill planned, mapped and pushed by X5: DRAFT with the supplier's number, receipt and recorded VAT (Q3, Q4)",
            link is not null && bill?.Status == "DRAFT" && bill.InvoiceNumber == BillNumber && bill.TotalTax == ExpenseVat && attachments >= 1,
            link is null ? await OutboxDetailAsync(document, cancellationToken) : $"{bill?.InvoiceNumber} {bill?.Status ?? read?.Outcome.ToString()}; VAT {bill?.TotalTax?.ToString(CultureInfo.InvariantCulture) ?? "?"}; {attachments} attachment(s)");
        if (link is not null)
            Record("Bill", BillNumber, link.XeroId, bill?.Status ?? "?", $"https://go.xero.com/AccountsPayable/View.aspx?InvoiceID={link.XeroId}");
    }

    /// <summary>P08: the engine's Refresh reads every status back, and each record's Xero status is the one the steps above saw.</summary>
    private async Task ReadBackAsync(CancellationToken cancellationToken)
    {
        await WaitOutPauseAsync(cancellationToken);
        var report = await _engine!.RefreshAsync(cancellationToken);
        for (var attempt = 0; attempt < 3 && report.ReadBack is null; attempt++)
        {
            await WaitOutPauseAsync(cancellationToken);
            report = await _engine.RefreshAsync(cancellationToken);
        }

        var expected = new List<(string Label, XeroDocumentRef Document, string Status)>();
        if (_quoteId is { } quote)
            expected.Add(("quote", XeroDocumentRef.For(XeroDocumentKind.Quote, quote), "ACCEPTED"));
        if (_invoiceRequestId is { } invoice)
            expected.Add(("invoice", XeroInvoiceDrafts.DocumentFor(invoice), "DRAFT"));
        if (_orderId is { } order)
            expected.Add(("purchase order", XeroPurchaseOrderPlanner.Ref(order), "DRAFT"));
        if (_expenseId is { } expense)
            expected.Add(("bill", XeroExpenseBillPlanner.Ref(expense), "DRAFT"));

        var seen = new List<string>();
        var passed = report.ReadBack is not null;
        foreach (var (label, document, status) in expected)
        {
            var badge = await _engine.GetStatusAsync(document, cancellationToken);
            seen.Add($"{label} {badge.Badge}/{badge.XeroStatus ?? "?"}");
            passed &= string.Equals(badge.XeroStatus, status, StringComparison.OrdinalIgnoreCase);
        }

        Step("P08", "The X6 engine's read-back (Refresh from Xero) agrees with Xero", passed,
            (report.ReadBack is null ? "no read-back ran; " : string.Empty) + string.Join("; ", seen));
    }

    /// <summary>P09: clean-up through the production paths — void the invoice (X4 deletes the Xero draft), cancel the order and delete the expense (X5) — unless kept.</summary>
    private async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        if (_options.Keep)
        {
            Step("P09", "Drafts kept for inspection (-Keep)", true, "the run's drafts are left in the Demo Company; delete them in Xero when done");
            return;
        }

        var problems = new List<string>();
        var done = 0;

        if (_invoiceRequestId is { } requestId && _invoicing is not null)
        {
            var voided = await _invoicing.VoidAsync(requestId, cancellationToken);
            for (var attempt = 0; attempt < 3 && !voided.Succeeded && _connection.RateLimiter.PausedUntilUtc is not null; attempt++)
            {
                // Xero's minute was spent: Void is the person's own action, not queued; they would try again.
                await WaitOutPauseAsync(cancellationToken);
                voided = await _invoicing.VoidAsync(requestId, cancellationToken);
            }

            if (!voided.Succeeded)
                await SettleAsync(XeroInvoiceDrafts.DocumentFor(requestId), cancellationToken);
            var link = await _links!.FindAsync(_tenantId, XeroInvoiceDrafts.DocumentFor(requestId), cancellationToken);
            if (link?.LastKnownXeroStatus == "DELETED")
            {
                done++;
                UpdateRecordStatus(link.XeroId, "DELETED");
            }
            else
            {
                problems.Add($"invoice: {voided.Reason ?? link?.LastKnownXeroStatus ?? "not linked"}");
            }
        }

        if (_orderId is { } orderId)
        {
            _orders[orderId] = _orders[orderId] with { Status = PurchaseOrderStatus.Cancelled };
            await CleanedAsync("purchase order", XeroPurchaseOrderPlanner.Ref(orderId));
        }

        if (_expenseId is { } expenseId)
        {
            _expenses[expenseId] = _expenses[expenseId] with { IsDeleted = true };
            await CleanedAsync("bill", XeroExpenseBillPlanner.Ref(expenseId));
        }

        Step("P09", "The run's drafts deleted through the production paths (the accepted quote is left)", problems.Count == 0,
            problems.Count == 0 ? $"{done} draft(s) deleted" : string.Join("; ", problems));

        async Task CleanedAsync(string label, XeroDocumentRef document)
        {
            var before = await _links!.FindAsync(_tenantId, document, cancellationToken);
            if (before is null)
                return;

            var link = await SettleAsync(document, cancellationToken);
            if (link?.LastKnownXeroStatus == "DELETED")
            {
                done++;
                UpdateRecordStatus(link.XeroId, "DELETED");
            }
            else
            {
                problems.Add($"{label}: {link?.LastKnownXeroStatus ?? await OutboxDetailAsync(document, cancellationToken)}");
            }
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Plans <paramref name="document"/> and runs engine cycles until its
    /// outbox queue is at rest (succeeded, superseded or failed), waiting on
    /// the run's clock for whatever Xero's rate limits or a back-off ask.
    /// </summary>
    private async Task<XeroLink?> SettleAsync(XeroDocumentRef document, CancellationToken cancellationToken)
    {
        var engine = _engine!;
        await engine.PlanDocumentAsync(document, cancellationToken);

        for (var cycle = 0; cycle < 40; cycle++)
        {
            await engine.RunCycleAsync(cancellationToken);
            var open = (await _outbox!.ListForDocumentAsync(document, cancellationToken))
                .Where(e => e.State is XeroOutboxState.Pending or XeroOutboxState.InFlight or XeroOutboxState.Unknown or XeroOutboxState.WaitingForAuthorisation)
                .ToList();
            if (open.Count == 0)
                break;

            var now = _time.GetUtcNow();
            var due = await engine.NextWorkDueAtAsync(cancellationToken);
            var wait = due is { } at && at > now ? at - now : TimeSpan.FromSeconds(1);
            if (wait > XeroDemoSmokeOptions.LongestRetryAfter)
                break;

            await _options.Delay(wait, cancellationToken);
            await engine.PlanDocumentAsync(document, cancellationToken);
        }

        return await _links!.FindAsync(_tenantId, document, cancellationToken);
    }

    private async Task<string> OutboxDetailAsync(XeroDocumentRef document, CancellationToken cancellationToken)
    {
        var entries = await _outbox!.ListForDocumentAsync(document, cancellationToken);
        return entries.Count == 0
            ? "nothing queued"
            : string.Join("; ", entries.Select(e => $"{e.Operation} {e.State}{(e.LastError is null ? string.Empty : $": {e.LastError}")}"));
    }

    private async Task<int> AttachmentCountAsync(XeroAttachableResource resource, string id, CancellationToken cancellationToken)
    {
        var listed = await RetryOn429Async(() => _connection.Api.ListAttachmentsAsync(resource, id, cancellationToken), IsRateLimited, cancellationToken);
        return listed.Value?.Count ?? 0;
    }

    /// <summary>Repeats a direct read (or the linker's own call) after a 429, waiting on the run's clock, up to three times.</summary>
    private async Task<T> RetryOn429Async<T>(Func<Task<T>> call, Func<T, bool> rateLimited, CancellationToken cancellationToken)
    {
        var result = await call();
        for (var attempt = 0; attempt < 3 && rateLimited(result); attempt++)
        {
            var now = _time.GetUtcNow();
            var wait = _connection.RateLimiter.PausedUntilUtc is { } until && until > now ? until - now + TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(61);
            if (wait > XeroDemoSmokeOptions.LongestRetryAfter)
                break;

            await _options.Delay(wait, cancellationToken);
            result = await call();
        }

        return result;
    }

    /// <summary>Waits on the run's clock until the rate limiter's pause (if any) has passed.</summary>
    private async Task WaitOutPauseAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (_connection.RateLimiter.PausedUntilUtc is { } until && until > now && until - now <= XeroDemoSmokeOptions.LongestRetryAfter)
            await _options.Delay(until - now + TimeSpan.FromSeconds(1), cancellationToken);
    }

    private static string Base36(long value, int length)
    {
        const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var text = new char[length];
        for (var i = length - 1; i >= 0; i--)
        {
            text[i] = digits[(int)(value % 36)];
            value /= 36;
        }

        return new string(text);
    }

    private static bool IsRateLimited<T>(XeroApiResult<T> result) => result.Outcome == ConnectorOutcome.Unavailable && result.HttpStatus == 429;

    private void StoreFile(XeroDocumentRef document, XeroDocumentFile file) => _files.Store(document, file);

    private void Step(string id, string title, bool passed, string detail) => _report.Add(new XeroSmokeStep(id, $"{Title}: {title}", passed, detail));

    private void Record(string kind, string number, string id, string status, string link) => _report.Add(new XeroSmokeRecord(kind, number, id, status, link));

    private void UpdateRecordStatus(string id, string status)
    {
        if (_report.Records.FirstOrDefault(r => r.XeroId == id) is { } record)
            _report.Add(record with { Status = status });
    }

    private static RateCard RateCard(string code) => new()
    {
        Code = code,
        Name = "Smoke rate card",
        EffectivePeriod = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2030, 12, 31)),
        Currency = CurrencyCode.Gbp,
        Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
        Entries =
        [
            new RateCardEntry("ENG-1", "Senior engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), new Money(90m, CurrencyCode.Gbp), Grade: "Senior"),
        ],
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _engine?.StopListening();
        if (_manager is not null)
            await _manager.ShutdownAsync();
        if (_host is not null)
            await _host.DisposeAsync();
        _temp?.Dispose();
    }

    /// <summary>The issued PDFs and the receipt the run holds, by record (the X6 file-source seam).</summary>
    private sealed class SmokeFiles : IXeroDocumentFileSource
    {
        private readonly Dictionary<XeroDocumentRef, XeroDocumentFile> _files = [];

        public void Store(XeroDocumentRef document, XeroDocumentFile file) => _files[document] = file;

        public Task<XeroDocumentFile?> FindAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
            Task.FromResult(_files.TryGetValue(document, out var file) ? file : null);
    }

    /// <summary>Stops the journey after a failed step that later steps depend on.</summary>
    private sealed class JourneyStopped : Exception
    {
    }
}
