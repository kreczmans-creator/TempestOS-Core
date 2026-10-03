using System.Globalization;
using System.Text.Json;
using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.Secrets;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>How the Xero sync engine (<see cref="XeroSyncService"/>) paces itself.</summary>
public sealed record XeroSyncOptions
{
    /// <summary>The configuration key of <see cref="SyncInterval"/>, in whole seconds (design §6.3: <c>Xero:SyncSeconds</c>).</summary>
    public const string SyncSecondsConfigurationKey = "Xero:SyncSeconds";

    /// <summary>The configuration key of <see cref="ReadBackInterval"/>, in whole minutes.</summary>
    public const string ReadBackMinutesConfigurationKey = "Xero:ReadBackMinutes";

    /// <summary>How often the engine wakes when nothing else wakes it (design §6.3: default 60 s). A saved change, a Retry and a re-authorisation wake it at once.</summary>
    public TimeSpan SyncInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>How often statuses are read back from Xero (invoices paid, quotes moved, …).</summary>
    public TimeSpan ReadBackInterval { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>How old the X1 settings reading may grow before it is read again (design §8: daily).</summary>
    public TimeSpan SettingsRefreshInterval { get; init; } = TimeSpan.FromDays(1);

    /// <summary>How often every record is planned again from its current state (the start-up scan, repeated), so nothing saved while the change feed could not be heard — before the workspace finished loading, say — waits for a restart. Local only.</summary>
    public TimeSpan FullScanInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>How often an entry Blocked on a missing precondition (a contact link, an account mapping, a PDF) is tried again when nothing else prompted it.</summary>
    public TimeSpan BlockedRetryInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// While writes wait for re-authorisation and the stored grant still reads
    /// as authorised (a 403 the grant record does not explain), how long before
    /// the engine tries once more anyway. A re-authorisation it sees (the grant
    /// turning unusable, then usable again) or is told of resumes at once.
    /// </summary>
    public TimeSpan ReauthorisationProbeInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>The most records one read-back pass reads, so it never crowds the drain out of the minute limit.</summary>
    public int ReadBackBudget { get; init; } = 25;

    /// <summary>How many inconclusive (<see cref="XeroPushOutcome.Unknown"/>) answers an entry gets before it is Failed "check Xero, then Retry or Unlink" (design §6.3: three).</summary>
    public int MaximumUnknownAnswers { get; init; } = 3;

    /// <summary>How many transport or 5xx failures in a row stop a drain (Xero looks down; the rest wait for the next wake rather than each failing in turn).</summary>
    public int MaximumConsecutiveTransientFailures { get; init; } = 3;

    /// <summary>
    /// After a drain stopped on <see cref="MaximumConsecutiveTransientFailures"/>
    /// (Xero looks down), how long before the next drain may send anything —
    /// so the entries it did not try, still due, are not tried at the hosted
    /// loop's one-second floor. Short (the first recovery backoff,
    /// <see cref="XeroBackoff.RecoveryBaseDelay"/>), so a lost create is
    /// still looked up well inside its key's lifetime. In memory only.
    /// </summary>
    public TimeSpan TransientStopPause { get; init; } = XeroBackoff.RecoveryBaseDelay;

    /// <summary>The jitter source: a number in <c>[0, 1)</c> per backoff (<see cref="XeroBackoff"/>); <see langword="null"/> for <see cref="Random.Shared"/>.</summary>
    public Func<double>? Jitter { get; init; }

    /// <summary>The defaults, with <see cref="SyncInterval"/> and <see cref="ReadBackInterval"/> read from <paramref name="configuration"/> when set to a positive whole number.</summary>
    /// <param name="configuration">The host's configuration; <see langword="null"/> for the defaults.</param>
    public static XeroSyncOptions FromConfiguration(IConfigurationProvider? configuration)
    {
        var options = new XeroSyncOptions();
        if (configuration is null)
            return options;

        if (configuration.TryGetValue(SyncSecondsConfigurationKey, out var seconds)
            && int.TryParse(seconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) && s > 0)
        {
            options = options with { SyncInterval = TimeSpan.FromSeconds(s) };
        }

        if (configuration.TryGetValue(ReadBackMinutesConfigurationKey, out var minutes)
            && int.TryParse(minutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) && m > 0)
        {
            options = options with { ReadBackInterval = TimeSpan.FromMinutes(m) };
        }

        return options;
    }
}

/// <summary>Whether Xero can be called now — the engine's way to see a re-authorisation happen.</summary>
public interface IXeroConnectionState
{
    /// <summary>The connector's current authorisation state (local, or a token refresh at most).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<ConnectorAuthorisationState> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>The <see cref="IXeroConnectionState"/> of the composed <see cref="XeroConnector"/> (its <see cref="XeroConnector.AuthorisationStateAsync"/>, which also checks the granted scopes).</summary>
public sealed class XeroConnectorConnectionState : IXeroConnectionState
{
    private readonly XeroConnector _connector;

    /// <summary>Initialises a new instance of the <see cref="XeroConnectorConnectionState"/> class.</summary>
    /// <param name="connector">The Xero connector.</param>
    public XeroConnectorConnectionState(XeroConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);
        _connector = connector;
    }

    /// <inheritdoc />
    public Task<ConnectorAuthorisationState> ReadAsync(CancellationToken cancellationToken = default) =>
        _connector.AuthorisationStateAsync(cancellationToken);
}

/// <summary>
/// One planner the engine drives: the planner, how it plans one record and
/// queues the writes, how it lists every record (the start-up scan), and
/// what must be recorded first thing at start-up.
/// </summary>
/// <param name="Planner">The planner (X3, X4 or X5).</param>
/// <param name="PlanAndEnqueue">Plans one record against its link and queues each planned write, in order; returns the entries (new or found).</param>
/// <param name="ListIds">Every record of the planner's kind.</param>
/// <param name="Prime">Run first thing at start-up (records when automatic sync began, Q8); <see langword="null"/> when none.</param>
public sealed record XeroPlannerSlot(
    IXeroSyncPlanner Planner,
    Func<Guid, CancellationToken, Task<IReadOnlyList<XeroOutboxEntry>>> PlanAndEnqueue,
    Func<CancellationToken, Task<IReadOnlyList<Guid>>> ListIds,
    Func<CancellationToken, Task>? Prime = null);

/// <summary>
/// Everything the sync engine drives, collected by type: the per-kind
/// planners and the push handlers of X3 (quotes), X4 (invoices) and X5
/// (purchase orders and bills), registered by their own hooks as their
/// concrete types (the container takes one registration per service type),
/// and dispatched here by operation <em>and</em> document kind — so an
/// <see cref="XeroOperation.UploadAttachment"/> goes to the handler of its
/// own kind of record.
/// </summary>
public sealed class XeroSyncParts
{
    private readonly ISecretStore _secrets;
    private readonly List<XeroPlannerSlot> _planners;
    private readonly Dictionary<(XeroOperation Operation, XeroDocumentKind Kind), IXeroPushHandler> _handlers = [];

    /// <summary>Initialises a new instance of the <see cref="XeroSyncParts"/> class from what the Xero registration hooks registered. Every planner and handler is optional: a kind with none is simply not synced.</summary>
    /// <param name="links">The link store (B2).</param>
    /// <param name="outbox">The outbox (B2).</param>
    /// <param name="secrets">Where the connected organisation's tenant id is kept.</param>
    /// <param name="quotePlanner">X3's planner.</param>
    /// <param name="quotes">X3's quotation source.</param>
    /// <param name="quoteHandler">X3's push handler.</param>
    /// <param name="quoteAttachments">X3's attachment handler.</param>
    /// <param name="invoicePlanner">X4's planner.</param>
    /// <param name="invoiceHandler">X4's push handler.</param>
    /// <param name="invoiceAttachments">X4's attachment handler.</param>
    /// <param name="domain">The workspace, for the list of invoice requests to scan.</param>
    /// <param name="orderPlanner">X5's purchase-order planner.</param>
    /// <param name="orders">X5's purchase-order source.</param>
    /// <param name="orderHandler">X5's purchase-order push handler.</param>
    /// <param name="orderAttachments">X5's purchase-order attachment handler.</param>
    /// <param name="expensePlanner">X5's expense-bill planner.</param>
    /// <param name="expenses">X5's expense source.</param>
    /// <param name="billHandler">X5's bill push handler.</param>
    /// <param name="billAttachments">X5's receipt handler.</param>
    /// <param name="purchasingState">X5's state (when automatic sync began).</param>
    /// <param name="creates">X5's create log (lost creates and tombstones, for the badge).</param>
    /// <param name="sendAgain">X5's <em>Send again</em>.</param>
    public XeroSyncParts(
        IXeroLinkStore links,
        IXeroOutbox outbox,
        ISecretStore secrets,
        XeroQuotePlanner? quotePlanner = null,
        IXeroQuoteSource? quotes = null,
        XeroQuotePushHandler? quoteHandler = null,
        XeroQuoteAttachmentHandler? quoteAttachments = null,
        XeroInvoicePlanner? invoicePlanner = null,
        XeroInvoicePushHandler? invoiceHandler = null,
        XeroInvoiceAttachmentPushHandler? invoiceAttachments = null,
        EngineeringDomainContext? domain = null,
        XeroPurchaseOrderPlanner? orderPlanner = null,
        IXeroPurchaseOrderSource? orders = null,
        XeroPurchaseOrderPushHandler? orderHandler = null,
        XeroPurchaseOrderAttachmentHandler? orderAttachments = null,
        XeroExpenseBillPlanner? expensePlanner = null,
        IXeroExpenseSource? expenses = null,
        XeroExpenseBillPushHandler? billHandler = null,
        XeroExpenseBillAttachmentHandler? billAttachments = null,
        XeroPurchasingSyncState? purchasingState = null,
        XeroPurchasingCreateLog? creates = null,
        XeroPurchasingSendAgain? sendAgain = null)
        : this(links, outbox, secrets, [], [], creates, sendAgain, quotes, expensePlanner)
    {
        if (quotePlanner is not null)
        {
            _planners.Add(new XeroPlannerSlot(
                quotePlanner, quotePlanner.PlanAndEnqueueAsync,
                quotes is null ? NoIds : quotes.ListIdsAsync,
                async ct => await quotePlanner.AutomaticFromAsync(ct).ConfigureAwait(false)));
        }

        if (invoicePlanner is not null)
        {
            _planners.Add(new XeroPlannerSlot(
                invoicePlanner, (id, ct) => PlanGenericAsync(invoicePlanner, id, ct),
                domain is null ? NoIds : async ct => [.. (await domain.Repository.ListByKindAsync(InvoiceRequest.CanonicalKind, ct).ConfigureAwait(false)).Select(e => e.Id)]));
        }

        Func<CancellationToken, Task>? primePurchasing = purchasingState is null ? null : async ct => await purchasingState.AutomaticFromAsync(ct).ConfigureAwait(false);
        if (orderPlanner is not null)
            _planners.Add(new XeroPlannerSlot(orderPlanner, orderPlanner.PlanAndEnqueueAsync, orders is null ? NoIds : orders.ListIdsAsync, primePurchasing));

        if (expensePlanner is not null)
            _planners.Add(new XeroPlannerSlot(expensePlanner, expensePlanner.PlanAndEnqueueAsync, expenses is null ? NoIds : expenses.ListIdsAsync, primePurchasing));

        Add(XeroDocumentKind.Quote, quoteHandler);
        Add(XeroDocumentKind.Quote, quoteAttachments);
        Add(XeroDocumentKind.Invoice, invoiceHandler);
        Add(XeroDocumentKind.Invoice, invoiceAttachments);
        Add(XeroDocumentKind.PurchaseOrder, orderHandler);
        Add(XeroDocumentKind.PurchaseOrder, orderAttachments);
        Add(XeroDocumentKind.ExpenseBill, billHandler);
        Add(XeroDocumentKind.ExpenseBill, billAttachments);
    }

    /// <summary>Test seam: explicit planners and (kind, handler) pairs.</summary>
    internal XeroSyncParts(
        IXeroLinkStore links, IXeroOutbox outbox, ISecretStore secrets, IEnumerable<XeroPlannerSlot> planners,
        IEnumerable<(XeroDocumentKind Kind, IXeroPushHandler Handler)> handlers, XeroPurchasingCreateLog? creates = null,
        XeroPurchasingSendAgain? sendAgain = null, IXeroQuoteSource? quotes = null, XeroExpenseBillPlanner? expensePlanner = null)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(planners);
        ArgumentNullException.ThrowIfNull(handlers);

        Links = links;
        Outbox = outbox;
        _secrets = secrets;
        Creates = creates;
        SendAgain = sendAgain;
        Quotes = quotes;
        ExpensePlanner = expensePlanner;
        _planners = [.. planners];
        foreach (var (kind, handler) in handlers)
            Add(kind, handler);
    }

    /// <summary>The link store.</summary>
    public IXeroLinkStore Links { get; }

    /// <summary>The outbox.</summary>
    public IXeroOutbox Outbox { get; }

    /// <summary>X5's create log; <see langword="null"/> when purchasing is not synced.</summary>
    public XeroPurchasingCreateLog? Creates { get; }

    /// <summary>X5's <em>Send again</em>; <see langword="null"/> when purchasing is not synced.</summary>
    public XeroPurchasingSendAgain? SendAgain { get; }

    /// <summary>X3's quotation source (for the drift note on a quote's badge); <see langword="null"/> when none.</summary>
    public IXeroQuoteSource? Quotes { get; }

    /// <summary>X5's expense planner (for the Q6 note on an expense's badge); <see langword="null"/> when none.</summary>
    public XeroExpenseBillPlanner? ExpensePlanner { get; }

    /// <summary>Every planner, in the order they are scanned (quotes, invoices, purchase orders, expenses).</summary>
    public IReadOnlyList<XeroPlannerSlot> Planners => _planners;

    /// <summary>The planner for records of <paramref name="kind"/>, or <see langword="null"/>.</summary>
    /// <param name="kind">The kind of record.</param>
    public XeroPlannerSlot? PlannerFor(XeroDocumentKind kind) => _planners.FirstOrDefault(p => p.Planner.Kind == kind);

    /// <summary>The planner watching the canonical Kind <paramref name="canonicalKind"/>, or <see langword="null"/>.</summary>
    /// <param name="canonicalKind">A TempestOS canonical Kind.</param>
    public XeroPlannerSlot? PlannerFor(string canonicalKind) =>
        _planners.FirstOrDefault(p => string.Equals(p.Planner.CanonicalKind, canonicalKind, StringComparison.Ordinal));

    /// <summary>The handler that sends <paramref name="operation"/> for a record of <paramref name="kind"/>, or <see langword="null"/>.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="kind">The kind of record.</param>
    public IXeroPushHandler? HandlerFor(XeroOperation operation, XeroDocumentKind kind) =>
        _handlers.TryGetValue((operation, kind), out var handler) ? handler : null;

    /// <summary>The connected Xero organisation's tenant id; <see langword="null"/> when none is connected.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<string?> ReadTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = await _secrets.GetAsync(XeroContactLinker.TenantIdSecretKey, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
    }

    private static Task<IReadOnlyList<Guid>> NoIds(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Guid>>([]);

    private void Add(XeroDocumentKind kind, IXeroPushHandler? handler)
    {
        if (handler is null)
            return;

        foreach (var operation in handler.Operations)
            _handlers[(operation, kind)] = handler;
    }

    /// <summary>Plans with a planner that has no queueing of its own (X4's): against the link in the connected tenant, each write queued in order.</summary>
    private async Task<IReadOnlyList<XeroOutboxEntry>> PlanGenericAsync(IXeroSyncPlanner planner, Guid id, CancellationToken cancellationToken)
    {
        var document = XeroDocumentRef.For(planner.Kind, id);
        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var link = tenantId is null ? null : await Links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false);

        var planned = await planner.PlanAsync(id, link, cancellationToken).ConfigureAwait(false);
        var entries = new List<XeroOutboxEntry>(planned.Count);
        foreach (var operation in planned)
            entries.Add(await Outbox.EnqueueAsync(operation.Operation, document, operation.ContentHash, operation.Argument, cancellationToken).ConfigureAwait(false));

        return entries;
    }
}

/// <summary>What the Xero badge (U3) shows for one record, with the actions it offers.</summary>
/// <param name="Status">The badge, its reason or note, Xero's number and status, and the Failed entry a Retry acts on.</param>
/// <param name="Label">The badge's words: <em>Not sent</em>, <em>Queued</em>, <em>In Xero (draft)</em>, <em>In Xero</em>, <em>Awaiting payment</em>, <em>Paid</em>, <em>Voided</em>, <em>Failed</em> or <em>Waiting for authorisation</em>.</param>
/// <param name="CanRetry">Whether <em>Retry</em> is offered (<see cref="XeroSyncService.RetryAsync"/> on <see cref="XeroSyncStatus.RetryableEntryId"/>).</param>
/// <param name="CanSendAgain">Whether <em>Send again</em> is offered (<see cref="XeroSyncService.SendAgainAsync"/>): a purchase order or bill TempestOS made in Xero was deleted (or voided) there.</param>
public sealed record XeroDocumentSyncStatus(XeroSyncStatus Status, string Label, bool CanRetry, bool CanSendAgain)
{
    /// <summary>
    /// Whether the record's Failed write is X5's <em>CannotTell</em> verdict
    /// (<see cref="XeroPushResult.CannotTell"/>): its create's answer was lost
    /// and cannot be recovered, so someone must check Xero. Typed, so the
    /// badge never matches the reason's words.
    /// </summary>
    public bool CannotTell { get; init; }
}

/// <summary>What one wake of the engine did (<see cref="XeroSyncService.RunCycleAsync"/>).</summary>
/// <param name="Planned">Outbox entries the cycle's planning queued (new ones only).</param>
/// <param name="Drain">The drain.</param>
/// <param name="ReadBack">The read-back, when one was due; <see langword="null"/> otherwise.</param>
/// <param name="SettingsRefreshed">Whether the X1 settings were read from Xero this cycle.</param>
public sealed record XeroSyncCycleReport(int Planned, XeroDrainReport Drain, XeroReadBackReport? ReadBack, bool SettingsRefreshed);

/// <summary>
/// The Xero sync engine (`v0.24.0` X6, `ADR-0162`; design §6): makes
/// everything run by itself. It plans what each saved record still needs in
/// Xero, drains the outbox one request at a time through the X3–X5 push
/// handlers, recovers any write whose answer was lost before any other work,
/// reads statuses back, keeps the X1 settings fresh, and answers the badge for
/// any record from local state alone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Planning (§6.2).</b> Every saved change to a quotation, invoice request,
/// purchase order or expense (<see cref="XeroChangeObserver"/>, off the commit
/// thread) is planned by its kind's planner; a full scan runs at start-up and
/// on Refresh, so a change saved just before a crash or while the engine was
/// not running is still queued. Planning is local: no network.
/// </para>
/// <para>
/// <b>Drain (§6.3).</b> One entry at a time, so never more than one request in
/// flight; read-back and the settings refresh share the same gate. Outcomes:
/// Succeeded/NothingToDo → Succeeded (and the record is re-planned, so the
/// writes that follow — a PDF once the record exists, a status after its
/// create — are queued at once); Rejected → Failed with the reason; Blocked →
/// Failed with the reason, tried again automatically when anything changes,
/// after a settings refresh or a re-authorisation, and every
/// <see cref="XeroSyncOptions.BlockedRetryInterval"/>; Reauthorise →
/// WaitingForAuthorisation and <b>everything pauses</b> until the grant is
/// usable again (seen through <see cref="IXeroConnectionState"/>, or told by
/// <see cref="NotifyAuthorisedAsync"/>); RetryLater with a 429
/// <c>Retry-After</c> → <b>everything pauses</b> for exactly that + 1 s (60 s
/// when absent), persisted so a restart honours it; RetryLater for a
/// transport failure or 5xx → this entry backs off
/// <c>min(30 s × 2^(n−1), 30 min)</c> ± 20 % (<see cref="XeroBackoff"/>) and
/// the drain stops after <see cref="XeroSyncOptions.MaximumConsecutiveTransientFailures"/>
/// in a row and sends nothing for <see cref="XeroSyncOptions.TransientStopPause"/>; Unknown → reconciled by its handler's lookup on the next
/// attempt, Failed after <see cref="XeroSyncOptions.MaximumUnknownAnswers"/>
/// inconclusive answers.
/// </para>
/// <para>
/// <b>Never double-sending; lost creates recovered within the key's lifetime.</b>
/// An entry is claimed (persisted InFlight) before its request goes, and one
/// found InFlight at start-up — the process stopped mid-request — becomes
/// Unknown. A create whose answer was lost, or that failed in transport after
/// it may have reached Xero, is recorded Unknown too. <b>Unknown entries are
/// claimed before any other work</b> (<see cref="IXeroOutboxDrain.ClaimNextDueAsync(IReadOnlyCollection{XeroOutboxState}, CancellationToken)"/>),
/// at start-up before the scan or the settings refresh, and are retried on a
/// short schedule (<see cref="XeroBackoff.RecoveryDelay"/>: 5 s doubling to
/// 45 s) for as long as Xero may still hold their <c>Idempotency-Key</c>
/// (<see cref="XeroPurchasingOwnership.IdempotencyKeyLifetime"/>, five minutes
/// from the first recovery) — so the handler replays the create under its own
/// key, or looks the record up, while Xero still answers a repeat with the
/// first answer, and one record results. A create not recovered in that time
/// — the process was down longer, Xero was unreachable throughout, or a day
/// limit paused everything — is never re-sent blindly: quotes and invoices are
/// still found by number; a purchase order or bill whose id was never learnt
/// is <em>CannotTell</em> (X5): Failed with the reason, nothing sent, for
/// whoever keeps the books to check Xero.
/// </para>
/// <para>
/// <b>Read-back and settings (§3, §8).</b> Statuses are read back every
/// <see cref="XeroSyncOptions.ReadBackInterval"/> (<see cref="XeroReadBack"/>),
/// and each record found changed is re-planned. The X1 settings are read on
/// connect (a tenant first seen, or a re-authorisation), daily, and before the
/// first write of a session — after any lost create has been recovered.
/// </para>
/// <para>
/// <b>Offline-safe.</b> With no organisation connected, Xero unreachable or
/// waiting for re-authorisation, writes wait in the outbox; planning and every
/// badge (<see cref="GetStatusAsync"/>, <see cref="GetDocumentStatusAsync"/>)
/// work from the link store and outbox alone. Every write's outcome and every
/// failure is audited (§6.7), never with a token.
/// </para>
/// </remarks>
public sealed class XeroSyncService : IXeroSyncService
{
    /// <summary>The <see cref="IPersistenceStore"/> collection holding the engine's own small state (a 429 pause, a re-authorisation pause, each entry's recovery and blocked marks).</summary>
    public const string StateCollection = "Xero.SyncEngine";

    /// <summary>Audit: a write was queued in the outbox (§6.7).</summary>
    public const string AuditEnqueued = "xero.outbox.enqueued";

    /// <summary>Audit: Xero confirmed a write (§6.7).</summary>
    public const string AuditSucceeded = "xero.push.succeeded";

    /// <summary>Audit: a write was refused or blocked and needs a person, or an inconclusive one gave up (§6.7).</summary>
    public const string AuditFailed = "xero.push.failed";

    /// <summary>Audit: a write was put off — Xero unreachable, 5xx, 429, or its answer not known yet.</summary>
    public const string AuditDeferred = "xero.push.deferred";

    /// <summary>Audit: a person chose Retry on a Failed write.</summary>
    public const string AuditRetried = "xero.push.retry";

    /// <summary>Audit: Xero needs re-authorising; everything waits (§6.7).</summary>
    public const string AuditReauthorisationRequired = "xero.reauthorisation.required";

    /// <summary>Audit: writes waiting for re-authorisation were resumed.</summary>
    public const string AuditResumed = "xero.sync.resumed";

    /// <summary>Audit: Xero answered 429; everything pauses until its <c>Retry-After</c>.</summary>
    public const string AuditRateLimited = "xero.sync.rate-limited";

    /// <summary>Audit: writes found in flight at start-up are being recovered (their answers may have been lost).</summary>
    public const string AuditRecovering = "xero.sync.recovering";

    /// <summary>The reason an entry that stayed inconclusive is Failed with (design §6.3).</summary>
    public const string InconclusiveReason =
        "Xero could not confirm whether it holds this record after several tries; check Xero, then Retry or Unlink.";

    private const string PausedUntilKey = "paused-until";
    private const string AuthorisationPausedAtKey = "authorisation-paused-at";
    private const string TrackPrefix = "entry/";

    private static readonly XeroOutboxState[] UnknownOnly = [XeroOutboxState.Unknown];
    private static readonly XeroOutboxState[] PendingOnly = [XeroOutboxState.Pending];

    /// <summary>The longest pause the limiter's nearly-spent-minute rule sets after a reading.</summary>
    private static readonly TimeSpan NearlySpentMinutePause = TimeSpan.FromMinutes(1);

    private readonly XeroSyncParts _parts;
    private readonly IXeroOutboxDrain _drain;
    private readonly IPersistenceStore _store;
    private readonly IXeroSettingsReader? _settings;
    private readonly IXeroConnectionState? _connection;
    private readonly XeroRateLimiter? _rateLimiter;
    private readonly XeroReadBack? _readBack;
    private readonly XeroChangeObserver? _observer;
    private readonly XeroInvoiceLinkImporter? _importer;
    private readonly IAuditRecorder? _audit;
    private readonly ILogger? _logger;
    private readonly TimeProvider _time;
    private readonly XeroSyncOptions _options;
    private readonly Func<double> _jitter;
    private readonly SemaphoreSlim _networkGate = new(1, 1);
    private readonly SemaphoreSlim _cycleGate = new(1, 1);
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private TaskCompletionSource _wake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _started;
    private bool _settingsReadThisSession;
    private DateTimeOffset? _lastSettingsAttemptUtc;
    private string? _lastTenantId;
    private bool _sawUnusableGrant = true;
    private DateTimeOffset? _lastReadBackUtc;
    private DateTimeOffset? _lastBlockedRetryUtc;
    private DateTimeOffset? _lastScanUtc;
    private long _drainHeldUntilUtcTicks;

    /// <summary>Initialises a new instance of the <see cref="XeroSyncService"/> class.</summary>
    /// <param name="parts">The planners and handlers it drives.</param>
    /// <param name="drain">The drain side of the outbox (B2).</param>
    /// <param name="store">Where the engine keeps its own small state (<see cref="StateCollection"/>).</param>
    /// <param name="settings">The X1 settings reader; <see langword="null"/> never refreshes.</param>
    /// <param name="connection">How a re-authorisation is seen; <see langword="null"/> resumes only on <see cref="NotifyAuthorisedAsync"/>.</param>
    /// <param name="rateLimiter">The client-side limiter, whose pause the drain honours before claiming; <see langword="null"/> when none.</param>
    /// <param name="readBack">The status read-back; <see langword="null"/> reads nothing back.</param>
    /// <param name="observer">The saved-change observer; <see langword="null"/> relies on the scans.</param>
    /// <param name="importer">Imports pre-v0.24 invoice links on connect; <see langword="null"/> imports nothing.</param>
    /// <param name="audit">The audit recorder; <see langword="null"/> records nothing.</param>
    /// <param name="configuration">The host's configuration (<see cref="XeroSyncOptions.FromConfiguration"/>); ignored when <paramref name="options"/> is given.</param>
    /// <param name="logger">The logger; <see langword="null"/> logs nothing.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> for the system clock.</param>
    /// <param name="options">Pacing; <see langword="null"/> reads <paramref name="configuration"/>.</param>
    public XeroSyncService(
        XeroSyncParts parts,
        IXeroOutboxDrain drain,
        IPersistenceStore store,
        IXeroSettingsReader? settings = null,
        IXeroConnectionState? connection = null,
        XeroRateLimiter? rateLimiter = null,
        XeroReadBack? readBack = null,
        XeroChangeObserver? observer = null,
        XeroInvoiceLinkImporter? importer = null,
        IAuditRecorder? audit = null,
        IConfigurationProvider? configuration = null,
        ILogger? logger = null,
        TimeProvider? timeProvider = null,
        XeroSyncOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(drain);
        ArgumentNullException.ThrowIfNull(store);

        _parts = parts;
        _drain = drain;
        _store = store;
        _settings = settings;
        _connection = connection;
        _rateLimiter = rateLimiter;
        _readBack = readBack;
        _observer = observer;
        _importer = importer;
        _audit = audit;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _options = options ?? XeroSyncOptions.FromConfiguration(configuration);
        _jitter = _options.Jitter ?? Random.Shared.NextDouble;
    }

    /// <summary>Raised after every <see cref="RunCycleAsync"/>, with what it did.</summary>
    public event Action<XeroSyncCycleReport>? CycleCompleted;

    /// <summary>The pacing in use.</summary>
    public XeroSyncOptions Options => _options;

    /// <summary>The engine's clock.</summary>
    public TimeProvider Clock => _time;

    /// <summary>Whether <see cref="StartAsync"/> has run.</summary>
    public bool IsStarted => Volatile.Read(ref _started);

    /// <summary>
    /// Start-up (§6.2, §6.3): every entry found InFlight becomes Unknown;
    /// when automatic sync began is recorded (Q8); the change observer starts
    /// listening; <b>every write whose answer may have been lost is recovered
    /// first</b>; pre-v0.24 invoice links are imported; then every record is
    /// planned (the full scan), so a change saved just before a crash is not
    /// lost. Runs once; later calls return at once.
    /// </summary>
    /// <param name="cancellationToken">Cancels start-up.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
                return;

            var now = _time.GetUtcNow();
            var recovered = await _drain.RecoverInFlightAsync(cancellationToken).ConfigureAwait(false);
            if (recovered > 0)
            {
                foreach (var entry in await _parts.Outbox.ListAsync(UnknownOnly, cancellationToken).ConfigureAwait(false))
                {
                    var track = await ReadTrackAsync(entry.Id, cancellationToken).ConfigureAwait(false);
                    if (track.RecoveringSinceUtc is null)
                        await WriteTrackAsync(entry.Id, track with { RecoveringSinceUtc = now }, cancellationToken).ConfigureAwait(false);
                }

                await AuditAsync(AuditRecovering, new Dictionary<string, string>
                {
                    ["entries"] = recovered.ToString(CultureInfo.InvariantCulture),
                    ["reason"] = "Found in flight at start-up: their answers may have been lost; each is reconciled before anything else is sent.",
                }, cancellationToken).ConfigureAwait(false);
            }

            foreach (var slot in _parts.Planners)
            {
                if (slot.Prime is { } prime)
                    await Isolated(() => prime(cancellationToken), $"recording when automatic {slot.Planner.Kind} sync began").ConfigureAwait(false);
            }

            if (_observer is not null)
            {
                _observer.ChangesQueued -= Signal;
                _observer.ChangesQueued += Signal;
                _observer.Start(_parts.Planners.Select(p => p.Planner.CanonicalKind));
            }

            // Lost answers first, before the scan, the settings refresh or any other write.
            await RecoverAsync(cancellationToken).ConfigureAwait(false);

            _started = true;
        }
        finally
        {
            _startGate.Release();
        }

        await ScanAsync(cancellationToken).ConfigureAwait(false);
        Signal();
    }

    /// <summary>Stops listening for saved changes (the hosted service's stop). Queued work stays in the outbox.</summary>
    public void StopListening()
    {
        if (_observer is null)
            return;

        _observer.ChangesQueued -= Signal;
        _observer.Stop();
    }

    /// <summary>
    /// Sends only the writes whose answers may have been lost (Unknown
    /// entries), each looked up or replayed by its handler — the start-up
    /// step that comes before any other work.
    /// </summary>
    /// <param name="cancellationToken">Cancels the drain.</param>
    public async Task<XeroDrainReport> RecoverAsync(CancellationToken cancellationToken = default)
    {
        await _networkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await DrainCoreAsync(recoveryOnly: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _networkGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<XeroDrainReport> DrainAsync(CancellationToken cancellationToken = default)
    {
        await _networkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await DrainCoreAsync(recoveryOnly: false, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _networkGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ReadBackAsync(CancellationToken cancellationToken = default) =>
        await ReadBackNowAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Reads statuses back now (<see cref="ReadBackAsync"/>), re-planning every record found changed, and says what it did; <see langword="null"/> when nothing could be read (no read-back, no organisation connected, or paused).</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    public async Task<XeroReadBackReport?> ReadBackNowAsync(CancellationToken cancellationToken = default)
    {
        if (_readBack is null)
            return null;

        XeroReadBackReport report;
        await _networkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tenantId = await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
            if (tenantId is null || await IsPausedAsync(cancellationToken).ConfigureAwait(false))
                return null;

            // Stamped before the pass, so a pass that throws is not retried
            // on every wake of the loop but on the next interval.
            _lastReadBackUtc = _time.GetUtcNow();
            report = await _readBack.ReadAsync(tenantId, _options.ReadBackBudget, cancellationToken).ConfigureAwait(false);
            await NoteRateLimiterPauseAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _networkGate.Release();
        }

        foreach (var document in report.Changed)
            await PlanDocumentAsync(document, cancellationToken).ConfigureAwait(false);

        return report;
    }

    /// <summary>
    /// One wake of the engine: plans every record the change observer saw (a
    /// full scan if it overflowed, and every <see cref="XeroSyncOptions.FullScanInterval"/>); retries Blocked entries when something
    /// changed or their interval passed; reads the X1 settings when a day old;
    /// drains; and reads statuses back when due. Runs <see cref="StartAsync"/>
    /// first if it has not run. Never throws for anything Xero or the network
    /// did; one cycle at a time.
    /// </summary>
    /// <param name="cancellationToken">Cancels the cycle.</param>
    public async Task<XeroSyncCycleReport> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!IsStarted)
            await StartAsync(cancellationToken).ConfigureAwait(false);

        XeroSyncCycleReport report;
        await _cycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var planned = 0;
            var changedSomething = false;
            if (_observer?.TakeAll() is { } batch)
            {
                if (batch.RescanNeeded)
                {
                    planned += await ScanAsync(cancellationToken).ConfigureAwait(false);
                    changedSomething = true;
                }
                else
                {
                    foreach (var change in batch.Changes)
                    {
                        if (_parts.PlannerFor(change.CanonicalKind) is { } slot)
                            planned += await PlanDocumentAsync(XeroDocumentRef.For(slot.Planner.Kind, change.ObjectId), cancellationToken).ConfigureAwait(false);
                    }

                    changedSomething = batch.Changes.Count > 0;
                }
            }

            var now = _time.GetUtcNow();
            if (_lastScanUtc is { } lastScan && now - lastScan >= _options.FullScanInterval)
                planned += await ScanAsync(cancellationToken).ConfigureAwait(false);

            if (changedSomething || _lastBlockedRetryUtc is not { } lastRetry || now - lastRetry >= _options.BlockedRetryInterval)
                await RetryBlockedAsync(cancellationToken).ConfigureAwait(false);

            var settingsRefreshed = false;
            await _networkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is not null
                    && !await IsPausedAsync(cancellationToken).ConfigureAwait(false)
                    && await SettingsAreStaleAsync(cancellationToken).ConfigureAwait(false))
                {
                    settingsRefreshed = await RefreshSettingsCoreAsync(force: false, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _networkGate.Release();
            }

            if (settingsRefreshed)
                await RetryBlockedAsync(cancellationToken).ConfigureAwait(false);

            var drain = await DrainAsync(cancellationToken).ConfigureAwait(false);

            XeroReadBackReport? readBack = null;
            now = _time.GetUtcNow();
            if (_readBack is not null && (_lastReadBackUtc is not { } lastRead || now - lastRead >= _options.ReadBackInterval))
                readBack = await ReadBackNowAsync(cancellationToken).ConfigureAwait(false);

            report = new XeroSyncCycleReport(planned, drain, readBack, settingsRefreshed);
        }
        finally
        {
            _cycleGate.Release();
        }

        CycleCompleted?.Invoke(report);
        return report;
    }

    /// <summary>
    /// The Product Owner's <em>Refresh</em>: every record planned again (the
    /// full scan), Blocked entries retried, the X1 settings read, the outbox
    /// drained and every status read back — now, not on the timer.
    /// </summary>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    public async Task<XeroSyncCycleReport> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!IsStarted)
            await StartAsync(cancellationToken).ConfigureAwait(false);

        var planned = await ScanAsync(cancellationToken).ConfigureAwait(false);
        await RetryBlockedAsync(cancellationToken).ConfigureAwait(false);

        var settingsRefreshed = false;
        await _networkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is not null && !await IsPausedAsync(cancellationToken).ConfigureAwait(false))
                settingsRefreshed = await RefreshSettingsCoreAsync(force: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _networkGate.Release();
        }

        var drain = await DrainAsync(cancellationToken).ConfigureAwait(false);
        var readBack = await ReadBackNowAsync(cancellationToken).ConfigureAwait(false);
        return new XeroSyncCycleReport(planned, drain, readBack, settingsRefreshed);
    }

    /// <summary>The start-up and Refresh scan (§6.2): every record of every planned kind planned from its current state. Local only.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>How many new outbox entries it queued.</returns>
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        _lastScanUtc = _time.GetUtcNow();
        var queued = 0;
        foreach (var slot in _parts.Planners)
        {
            IReadOnlyList<Guid> ids;
            try
            {
                ids = await slot.ListIds(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.Error($"Xero sync could not list {slot.Planner.Kind} records to scan; isolated, the rest are scanned.", ex);
                continue;
            }

            foreach (var id in ids)
                queued += await PlanDocumentAsync(XeroDocumentRef.For(slot.Planner.Kind, id), cancellationToken).ConfigureAwait(false);
        }

        return queued;
    }

    /// <summary>Plans one record now and queues what it still needs (audited per new entry). Local only; a failure is logged and isolated.</summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the planning.</param>
    /// <returns>How many new outbox entries it queued.</returns>
    public async Task<int> PlanDocumentAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (_parts.PlannerFor(document.Kind) is not { } slot || !Guid.TryParse(document.TempestKey, out var id))
            return 0;

        try
        {
            var before = (await _parts.Outbox.ListForDocumentAsync(document, cancellationToken).ConfigureAwait(false)).Select(e => e.Id).ToHashSet();
            var entries = await slot.PlanAndEnqueue(id, cancellationToken).ConfigureAwait(false);
            var queued = 0;
            foreach (var entry in entries.Where(e => !before.Contains(e.Id)))
            {
                queued++;
                await AuditAsync(AuditEnqueued, Detail(entry), cancellationToken).ConfigureAwait(false);
            }

            if (queued > 0)
                Signal();

            return queued;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Error($"Xero sync could not plan {document.Kind} {document.TempestKey}; isolated, it is planned again on the next change or scan.", ex);
            return 0;
        }
    }

    /// <summary>The Product Owner's <em>Retry</em> on a Failed entry: back to Pending (its key kept), audited, and the engine woken.</summary>
    /// <param name="entryId">The Failed entry (<see cref="XeroSyncStatus.RetryableEntryId"/>).</param>
    /// <param name="cancellationToken">Cancels the retry.</param>
    /// <returns>Whether the entry was Failed and is now queued again.</returns>
    public async Task<bool> RetryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        if (!await _parts.Outbox.RetryAsync(entryId, cancellationToken).ConfigureAwait(false))
            return false;

        var track = await ReadTrackAsync(entryId, cancellationToken).ConfigureAwait(false);
        await WriteTrackAsync(entryId, track with { Blocked = false, UnknownAnswers = 0 }, cancellationToken).ConfigureAwait(false);

        if (await _drain.FindAsync(entryId, cancellationToken).ConfigureAwait(false) is { } entry)
            await AuditAsync(AuditRetried, Detail(entry), cancellationToken).ConfigureAwait(false);

        Signal();
        return true;
    }

    /// <summary>
    /// The person's <em>Send again</em> on a purchase order or expense whose
    /// Xero record TempestOS made was deleted (or a bill voided) in Xero (X5,
    /// <see cref="XeroPurchasingSendAgain"/>): sends it as a new draft. Refused,
    /// with the reason, for anything else.
    /// </summary>
    /// <param name="document">The purchase order or expense.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<XeroPurchasingSendRequest> SendAgainAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (_parts.SendAgain is not { } sendAgain || !Guid.TryParse(document.TempestKey, out var id))
            return new XeroPurchasingSendRequest(false, [], "Send again is offered for purchase orders and expenses only.");

        var result = document.Kind switch
        {
            XeroDocumentKind.PurchaseOrder => await sendAgain.SendOrderAgainAsync(id, cancellationToken).ConfigureAwait(false),
            XeroDocumentKind.ExpenseBill => await sendAgain.SendExpenseAgainAsync(id, cancellationToken).ConfigureAwait(false),
            _ => new XeroPurchasingSendRequest(false, [], "Send again is offered for purchase orders and expenses only."),
        };

        if (result.Queued)
            Signal();

        return result;
    }

    /// <summary>
    /// Settings tells the engine the Product Owner has just authorised (or
    /// re-authorised) Xero: every entry waiting for authorisation is resumed,
    /// the X1 settings are read (on connect), Blocked entries are retried, and
    /// the engine is woken.
    /// </summary>
    /// <param name="cancellationToken">Cancels the resume.</param>
    /// <returns>How many waiting entries were resumed.</returns>
    public async Task<int> NotifyAuthorisedAsync(CancellationToken cancellationToken = default)
    {
        int resumed;
        await _networkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            resumed = await ResumeCoreAsync("authorised in Settings", cancellationToken).ConfigureAwait(false);
            if (await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is not null)
                await RefreshSettingsCoreAsync(force: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _networkGate.Release();
        }

        await RetryBlockedAsync(cancellationToken).ConfigureAwait(false);
        Signal();
        return resumed;
    }

    /// <summary>
    /// When the engine next has work it can actually do: the earliest due
    /// entry (now when one is due) and the next read-back, each held back to
    /// the end of a 429 pause; <see langword="null"/> when nothing can be done
    /// before something wakes the engine. With no organisation connected, or
    /// while writes wait for re-authorisation, nothing is due — neither the
    /// drain nor the read-back could run — so the hosted loop waits its whole
    /// <see cref="XeroSyncOptions.SyncInterval"/> (a connect or a
    /// re-authorisation wakes it at once through <see cref="NotifyAuthorisedAsync"/>)
    /// rather than waking every second to find it still cannot.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<DateTimeOffset?> NextWorkDueAtAsync(CancellationToken cancellationToken = default)
    {
        if (await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false) is null
            || await IsWaitingForAuthorisationAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var now = _time.GetUtcNow();
        DateTimeOffset? next = null;

        void Consider(DateTimeOffset at) => next = next is { } current && current <= at ? current : at;

        // Only the head of each document's queue can be sent; an entry
        // behind a Failed or waiting one is not work yet.
        var heads = (await _parts.Outbox.ListAsync([], cancellationToken).ConfigureAwait(false))
            .Where(e => e.State is not (XeroOutboxState.Succeeded or XeroOutboxState.Superseded))
            .GroupBy(e => e.Document)
            .Select(g => g.First())
            .Where(e => e.State is XeroOutboxState.Pending or XeroOutboxState.Unknown && e.SchemaVersion <= XeroOutboxEntry.CurrentSchemaVersion);
        // After a drain stopped on an outage, nothing is sent before its hold ends.
        var held = DrainHeldUntil(now) ?? now;
        foreach (var entry in heads)
        {
            var dueAt = entry.NotBeforeUtc is { } notBefore && notBefore > now ? notBefore : now;
            Consider(dueAt > held ? dueAt : held);
        }

        if (_readBack is not null)
            Consider(_lastReadBackUtc is { } last ? last + _options.ReadBackInterval : now);

        // A 429 pause (persisted, or the client-side limiter's own) holds back
        // everything that goes to Xero: nothing is due before it ends.
        var pausedUntil = await ReadTimeAsync(PausedUntilKey, cancellationToken).ConfigureAwait(false);
        if (_rateLimiter?.DelayBeforeNextCall() is { } limiterWait && limiterWait > TimeSpan.Zero
            && (pausedUntil is null || now + limiterWait > pausedUntil))
        {
            pausedUntil = now + limiterWait;
        }

        if (next is { } due && pausedUntil is { } until && until > due)
            next = until;

        return next;
    }

    /// <summary>Wakes the engine's loop (a saved change, a Retry, a re-authorisation).</summary>
    public void Signal() => Volatile.Read(ref _wake).TrySetResult();

    /// <summary>
    /// Waits until <see cref="Signal"/> is called or <paramref name="maximumWait"/>
    /// passes on the engine's clock, whichever is first — the hosted service's
    /// idle wait. A signal raised while a cycle ran is not lost: the next wait
    /// returns at once.
    /// </summary>
    /// <param name="maximumWait">The longest wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns><see langword="true"/> when woken by a signal.</returns>
    public async Task<bool> WaitForWorkAsync(TimeSpan maximumWait, CancellationToken cancellationToken = default)
    {
        var wake = Volatile.Read(ref _wake);
        if (!wake.Task.IsCompleted && maximumWait > TimeSpan.Zero)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delay = Task.Delay(maximumWait, _time, timeout.Token);
            await Task.WhenAny(wake.Task, delay).ConfigureAwait(false);
            await timeout.CancelAsync().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!wake.Task.IsCompleted)
            return false;

        Interlocked.CompareExchange(ref _wake, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), wake);
        return true;
    }

    /// <inheritdoc />
    public async Task<XeroSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        (await GetDocumentStatusAsync(document, cancellationToken).ConfigureAwait(false)).Status;

    /// <summary>
    /// The badge for <paramref name="document"/> with its words and actions
    /// (U3), from the link store, the outbox and X5's create log alone — never
    /// a network call: <em>Waiting for authorisation</em> while its writes wait
    /// for (re-)authorisation or no organisation is connected; <em>Failed</em>
    /// with the reason and Retry (and, for a purchase order or bill deleted in
    /// Xero, Send again); <em>Queued</em> while a write is pending, being sent
    /// or being reconciled; otherwise what Xero last said — <em>In Xero
    /// (draft)</em>, <em>In Xero</em>, <em>Awaiting payment</em>, <em>Paid</em>,
    /// <em>Voided</em> — or <em>Not sent</em>.
    /// </summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<XeroDocumentSyncStatus> GetDocumentStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tenantId = await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var entries = await _parts.Outbox.ListForDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        var open = entries.Where(e => e.State is not (XeroOutboxState.Succeeded or XeroOutboxState.Superseded)).ToList();
        var link = tenantId is null ? null : await _parts.Links.FindAsync(tenantId, document, cancellationToken).ConfigureAwait(false);
        var canSendAgain = tenantId is not null && link is null && await HasTombstoneAsync(tenantId, document, cancellationToken).ConfigureAwait(false);

        var number = link?.XeroNumber;
        var xeroStatus = link?.LastKnownXeroStatus;
        var asOf = link?.LastReadAtUtc ?? link?.LinkedAtUtc;

        XeroDocumentSyncStatus Make(XeroSyncBadge badge, string? reason = null, Guid? retry = null) =>
            new(new XeroSyncStatus(badge, reason, number, xeroStatus, asOf, retry), LabelFor(badge), retry is not null, canSendAgain);

        if (link is not null && PersistenceXeroLinkStore.IsFromNewerVersion(link))
            return Make(XeroSyncBadge.Failed, $"The Xero link for this record was {PersistenceXeroLinkStore.NewerVersionNote}; this build does not change it.");

        if (open.FirstOrDefault(e => e.State == XeroOutboxState.WaitingForAuthorisation) is { } waiting)
            return Make(XeroSyncBadge.NeedsReauthorisation, waiting.LastError ?? "Xero needs re-authorising before anything more is sent.");

        if (tenantId is null && open.Count > 0)
            return Make(XeroSyncBadge.NeedsReauthorisation, "No Xero organisation is connected; queued writes are sent once it is.");

        if (open.FirstOrDefault(e => e.State == XeroOutboxState.Failed) is { } failed)
        {
            var retryable = failed.SchemaVersion <= XeroOutboxEntry.CurrentSchemaVersion && Enum.IsDefined(failed.Operation)
                            && !string.Equals(failed.LastError, PersistenceXeroOutbox.UnreadableError, StringComparison.Ordinal);
            var cannotTell = (await ReadTrackAsync(failed.Id, cancellationToken).ConfigureAwait(false)).CannotTell;
            return Make(XeroSyncBadge.Failed, failed.LastError ?? "Xero refused this write.", retryable ? failed.Id : null) with { CannotTell = cannotTell };
        }

        if (open.Count > 0)
        {
            var head = open[0];
            var now = _time.GetUtcNow();
            var waitingElsewhere = head.State == XeroOutboxState.Pending && await IsWaitingForAuthorisationAsync(cancellationToken).ConfigureAwait(false);
            var reason = head.State switch
            {
                XeroOutboxState.InFlight => "Being sent to Xero.",
                XeroOutboxState.Unknown => "Checking whether Xero received it.",
                _ when waitingElsewhere => "Xero needs re-authorising before anything more is sent; this write waits in the queue.",
                _ when head.SchemaVersion > XeroOutboxEntry.CurrentSchemaVersion => $"Queued by a newer TempestOS ({PersistenceXeroLinkStore.NewerVersionNote}); this build does not send it.",
                _ when head.NotBeforeUtc is { } notBefore && notBefore > now =>
                    $"Xero could not be reached{(string.IsNullOrWhiteSpace(head.LastError) ? string.Empty : $" ({head.LastError!.Trim().TrimEnd('.')})")}; trying again at {notBefore.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} UTC.",
                _ => null,
            };
            return Make(XeroSyncBadge.Queued, reason);
        }

        if (link is not null)
        {
            var (badge, note) = await LinkedBadgeAsync(document, link, cancellationToken).ConfigureAwait(false);
            return Make(badge, note);
        }

        if (document.Kind == XeroDocumentKind.ExpenseBill && _parts.ExpensePlanner is { } expenses && Guid.TryParse(document.TempestKey, out var expenseId))
            return Make(XeroSyncBadge.NotSent, await expenses.DescribeNotPushedAsync(expenseId, cancellationToken).ConfigureAwait(false));

        return Make(XeroSyncBadge.NotSent, canSendAgain ? "The record TempestOS made in Xero was deleted there." : null);
    }

    /// <summary>The badge's words for <paramref name="badge"/>.</summary>
    /// <param name="badge">The badge.</param>
    public static string LabelFor(XeroSyncBadge badge) => badge switch
    {
        XeroSyncBadge.NotSent => "Not sent",
        XeroSyncBadge.Queued => "Queued",
        XeroSyncBadge.InXeroDraft => "In Xero (draft)",
        XeroSyncBadge.InXero => "In Xero",
        XeroSyncBadge.AwaitingPayment => "Awaiting payment",
        XeroSyncBadge.Paid => "Paid",
        XeroSyncBadge.Voided => "Voided",
        XeroSyncBadge.Failed => "Failed",
        XeroSyncBadge.NeedsReauthorisation => "Waiting for authorisation",
        _ => badge.ToString(),
    };

    // ------------------------------------------------------------------ drain

    private enum Step
    {
        Continue,
        Transient,
        StopRateLimited,
        StopAuthorisation,
    }

    /// <summary>The drain itself; the caller holds <see cref="_networkGate"/>.</summary>
    private async Task<XeroDrainReport> DrainCoreAsync(bool recoveryOnly, CancellationToken cancellationToken)
    {
        var tenantId = await _parts.ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is null)
            return new XeroDrainReport(0, 0, 0, 0, PausedForAuthorisation: true, ResumeNotBeforeUtc: null);

        if (!recoveryOnly)
            await OnTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

        if (await IsWaitingForAuthorisationAsync(cancellationToken).ConfigureAwait(false)
            && !await TryResumeAsync(refreshSettings: !recoveryOnly, cancellationToken).ConfigureAwait(false))
        {
            return new XeroDrainReport(0, 0, 0, 0, PausedForAuthorisation: true, ResumeNotBeforeUtc: null);
        }

        if (await ReadTimeAsync(PausedUntilKey, cancellationToken).ConfigureAwait(false) is { } pausedUntil && pausedUntil > _time.GetUtcNow())
            return new XeroDrainReport(0, 0, 0, 0, PausedForAuthorisation: false, ResumeNotBeforeUtc: pausedUntil);

        if (DrainHeldUntil(_time.GetUtcNow()) is not null)
            return new XeroDrainReport(0, 0, 0, 0, PausedForAuthorisation: false, ResumeNotBeforeUtc: null);

        int attempted = 0, succeeded = 0, failed = 0, deferred = 0, transientInARow = 0;
        DateTimeOffset? resume = null;
        var pausedForAuthorisation = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_rateLimiter?.DelayBeforeNextCall() is { } wait && wait > TimeSpan.Zero)
            {
                resume = _time.GetUtcNow() + wait;
                break;
            }

            var claimedFromUnknown = true;
            var entry = await _drain.ClaimNextDueAsync(UnknownOnly, cancellationToken).ConfigureAwait(false);
            if (entry is null)
            {
                if (recoveryOnly)
                    break;

                claimedFromUnknown = false;
                if (!_settingsReadThisSession && await AnyDueAsync(PendingOnly, cancellationToken).ConfigureAwait(false))
                {
                    // Before the first write of a session (§8) — after every lost answer has been recovered.
                    await RefreshSettingsCoreAsync(force: false, cancellationToken).ConfigureAwait(false);
                    if (_rateLimiter?.DelayBeforeNextCall() is { } settingsWait && settingsWait > TimeSpan.Zero)
                    {
                        resume = _time.GetUtcNow() + settingsWait;
                        break;
                    }
                }

                entry = await _drain.ClaimNextDueAsync(PendingOnly, cancellationToken).ConfigureAwait(false);
            }

            if (entry is null)
                break;

            attempted++;
            var (step, outcome) = await SendAsync(tenantId, entry, claimedFromUnknown, cancellationToken).ConfigureAwait(false);
            switch (outcome)
            {
                case XeroOutboxState.Succeeded:
                    succeeded++;
                    break;
                case XeroOutboxState.Failed:
                    failed++;
                    break;
                case XeroOutboxState.WaitingForAuthorisation:
                    break;
                default:
                    deferred++;
                    break;
            }

            transientInARow = step == Step.Transient ? transientInARow + 1 : 0;
            if (step == Step.StopAuthorisation)
            {
                pausedForAuthorisation = true;
                break;
            }

            if (step == Step.StopRateLimited)
            {
                resume = await ReadTimeAsync(PausedUntilKey, cancellationToken).ConfigureAwait(false);
                break;
            }

            if (transientInARow >= _options.MaximumConsecutiveTransientFailures)
            {
                Interlocked.Exchange(ref _drainHeldUntilUtcTicks, (_time.GetUtcNow() + _options.TransientStopPause).UtcTicks);
                break;
            }
        }

        return new XeroDrainReport(attempted, succeeded, failed, deferred, pausedForAuthorisation, resume);
    }

    /// <summary>Until when the drain holds off after stopping on an outage; <see langword="null"/> when it does not.</summary>
    private DateTimeOffset? DrainHeldUntil(DateTimeOffset now)
    {
        var ticks = Interlocked.Read(ref _drainHeldUntilUtcTicks);
        return ticks > now.UtcTicks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;
    }

    /// <summary>Sends one claimed entry and records what happened.</summary>
    private async Task<(Step Step, XeroOutboxState Outcome)> SendAsync(string tenantId, XeroOutboxEntry entry, bool claimedFromUnknown, CancellationToken cancellationToken)
    {
        var track = await ReadTrackAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        var recovering = claimedFromUnknown || track.RecoveringSinceUtc is not null;

        // What the client-side limiter looked like before this entry's
        // requests, so a pause it takes on during them can be told apart
        // from one it already had (RateLimitedUntil).
        var limiterBefore = new LimiterSnapshot(_rateLimiter?.PausedUntilUtc, _rateLimiter?.LastReading, _time.GetUtcNow());

        XeroPushResult result;
        if (_parts.HandlerFor(entry.Operation, entry.Document.Kind) is not { } handler)
        {
            result = new XeroPushResult(XeroPushOutcome.Rejected, $"This build has no Xero handler for {entry.Operation} on a {entry.Document.Kind}; nothing was sent.");
        }
        else
        {
            try
            {
                result = await handler.PushAsync(tenantId, entry, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Stopped mid-request, exactly like a crash: the entry stays
                // InFlight and is recovered (Unknown, looked up first) at the
                // next start-up.
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Error($"The Xero handler for {entry.Operation} failed unexpectedly on entry {entry.Id}; the write is reconciled before it is sent again.", ex);
                result = new XeroPushResult(XeroPushOutcome.Unknown, $"The {entry.Operation} handler failed unexpectedly: {ex.Message}");
            }
        }

        return await RecordAsync(entry, result, track, recovering, limiterBefore, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(Step Step, XeroOutboxState Outcome)> RecordAsync(
        XeroOutboxEntry entry, XeroPushResult result, EntryTrack track, bool recovering, LimiterSnapshot limiterBefore, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var detail = Detail(entry, result);

        switch (result.Outcome)
        {
            case XeroPushOutcome.Succeeded:
            case XeroPushOutcome.NothingToDo:
                await _drain.RecordOutcomeAsync(entry.Id, XeroOutboxState.Succeeded, cancellationToken: cancellationToken).ConfigureAwait(false);
                await DeleteTrackAsync(entry.Id, cancellationToken).ConfigureAwait(false);
                detail["recovered"] = recovering ? "true" : "false";
                await AuditAsync(AuditSucceeded, detail, cancellationToken).ConfigureAwait(false);

                // The writes that follow this one (a PDF once the record
                // exists, the status after its create) are planned at once.
                await PlanDocumentAsync(entry.Document, cancellationToken).ConfigureAwait(false);
                return (Step.Continue, XeroOutboxState.Succeeded);

            case XeroPushOutcome.Rejected:
            case XeroPushOutcome.Blocked:
            {
                var blocked = result.Outcome == XeroPushOutcome.Blocked;
                var reason = result.Reason ?? (blocked ? "Blocked: a precondition in TempestOS is missing." : "Xero refused this write.");
                await _drain.RecordOutcomeAsync(entry.Id, XeroOutboxState.Failed, reason, cancellationToken: cancellationToken).ConfigureAwait(false);
                var repeat = track.Blocked && blocked && string.Equals(entry.LastError, reason, StringComparison.Ordinal);
                await WriteTrackAsync(entry.Id, new EntryTrack(Blocked: blocked, CannotTell: result.CannotTell), cancellationToken).ConfigureAwait(false);
                if (!repeat)
                {
                    detail["blocked"] = blocked ? "true" : "false";
                    await AuditAsync(AuditFailed, detail, cancellationToken).ConfigureAwait(false);
                }

                return (Step.Continue, XeroOutboxState.Failed);
            }

            case XeroPushOutcome.Reauthorise:
                await _drain.RecordOutcomeAsync(entry.Id, XeroOutboxState.WaitingForAuthorisation, result.Reason, cancellationToken: cancellationToken).ConfigureAwait(false);
                await WriteTimeAsync(AuthorisationPausedAtKey, now, cancellationToken).ConfigureAwait(false);
                _sawUnusableGrant = false;
                await AuditAsync(AuditReauthorisationRequired, detail, cancellationToken).ConfigureAwait(false);
                return (Step.StopAuthorisation, XeroOutboxState.WaitingForAuthorisation);

            case XeroPushOutcome.RetryLater when RateLimitedUntil(result, now, limiterBefore) is { } until:
            {
                var asUnknown = recovering || IsCreate(entry.Operation);
                await _drain.RecordOutcomeAsync(entry.Id, asUnknown ? XeroOutboxState.Unknown : XeroOutboxState.Pending, result.Reason, until, cancellationToken).ConfigureAwait(false);
                if (asUnknown && track.RecoveringSinceUtc is null)
                    await WriteTrackAsync(entry.Id, track with { RecoveringSinceUtc = now }, cancellationToken).ConfigureAwait(false);

                await WriteTimeAsync(PausedUntilKey, until, cancellationToken).ConfigureAwait(false);
                detail["pausedUntilUtc"] = until.ToString("O", CultureInfo.InvariantCulture);
                await AuditAsync(AuditRateLimited, detail, cancellationToken).ConfigureAwait(false);
                return (Step.StopRateLimited, asUnknown ? XeroOutboxState.Unknown : XeroOutboxState.Pending);
            }

            case XeroPushOutcome.RetryLater:
            {
                // A create that failed in transport may still have reached
                // Xero: it is recovered like a lost answer — Unknown, claimed
                // first, on the short schedule while Xero holds its key.
                var asUnknown = recovering || IsCreate(entry.Operation);
                DateTimeOffset notBefore;
                if (asUnknown)
                {
                    var since = track.RecoveringSinceUtc ?? now;
                    var attempts = track.RecoveryAttempts + 1;
                    var delay = now - since < XeroPurchasingOwnership.IdempotencyKeyLifetime
                        ? XeroBackoff.RecoveryDelay(attempts, _jitter())
                        : XeroBackoff.Delay(entry.Attempts, _jitter());
                    notBefore = now + delay;
                    await WriteTrackAsync(entry.Id, track with { RecoveringSinceUtc = since, RecoveryAttempts = attempts }, cancellationToken).ConfigureAwait(false);
                    await _drain.RecordOutcomeAsync(entry.Id, XeroOutboxState.Unknown, result.Reason, notBefore, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    notBefore = now + XeroBackoff.Delay(entry.Attempts, _jitter());
                    await _drain.RecordOutcomeAsync(entry.Id, XeroOutboxState.Pending, result.Reason, notBefore, cancellationToken).ConfigureAwait(false);
                }

                detail["notBeforeUtc"] = notBefore.ToString("O", CultureInfo.InvariantCulture);
                await AuditAsync(AuditDeferred, detail, cancellationToken).ConfigureAwait(false);
                return (Step.Transient, asUnknown ? XeroOutboxState.Unknown : XeroOutboxState.Pending);
            }

            default:
            {
                var answers = track.UnknownAnswers + 1;
                if (answers >= _options.MaximumUnknownAnswers)
                {
                    var reason = string.IsNullOrWhiteSpace(result.Reason) ? InconclusiveReason : $"{InconclusiveReason} ({result.Reason.Trim().TrimEnd('.')}.)";
                    await _drain.RecordOutcomeAsync(entry.Id, XeroOutboxState.Failed, reason, cancellationToken: cancellationToken).ConfigureAwait(false);
                    await DeleteTrackAsync(entry.Id, cancellationToken).ConfigureAwait(false);
                    detail["reason"] = reason;
                    await AuditAsync(AuditFailed, detail, cancellationToken).ConfigureAwait(false);
                    return (Step.Continue, XeroOutboxState.Failed);
                }

                var since = track.RecoveringSinceUtc ?? now;
                var attempts = track.RecoveryAttempts + 1;
                var notBefore = now + XeroBackoff.RecoveryDelay(attempts, _jitter());
                await WriteTrackAsync(entry.Id, track with { RecoveringSinceUtc = since, RecoveryAttempts = attempts, UnknownAnswers = answers }, cancellationToken).ConfigureAwait(false);
                await _drain.RecordOutcomeAsync(entry.Id, XeroOutboxState.Unknown, result.Reason, notBefore, cancellationToken).ConfigureAwait(false);
                detail["notBeforeUtc"] = notBefore.ToString("O", CultureInfo.InvariantCulture);
                await AuditAsync(AuditDeferred, detail, cancellationToken).ConfigureAwait(false);
                return (Step.Continue, XeroOutboxState.Unknown);
            }
        }
    }

    /// <summary>
    /// When a RetryLater answer was a 429 (design §6.3, §6.5: <b>pause all</b>),
    /// until when everything pauses; <see langword="null"/> for a transport
    /// failure or a 5xx. A 429 is known by its <c>Retry-After</c> (+1 s; only
    /// a 429's is passed on), or — when the handler did not pass it on (X4's
    /// invoice handlers never do) or Xero sent none — by the client-side
    /// limiter: it held the request back (it was already paused before the
    /// request), or it paused on an answer seen during the request (its own
    /// <c>Retry-After</c> + 1 s, or 60 s when absent). The limiter also pauses,
    /// on <em>any</em> answer, when Xero's <c>X-MinLimit-Remaining</c> is at
    /// or below <see cref="XeroRateLimiter.MinuteRemainingFloor"/> — to the
    /// end of the minute, at most 60 s after that answer was read. A pause
    /// that rule alone explains is not a 429: the answer (a 5xx, say) is
    /// backed off as itself, and the limiter still holds every call until its
    /// pause ends, so the drain stops there anyway (§6.5). A pause the limiter
    /// says a 429 is behind (<see cref="XeroRateLimiter.PauseCameFromTooManyRequests"/>)
    /// is always a 429's, however short its <c>Retry-After</c>, so it is
    /// persisted and survives a restart.
    /// </summary>
    private DateTimeOffset? RateLimitedUntil(XeroPushResult result, DateTimeOffset now, LimiterSnapshot before)
    {
        DateTimeOffset? until = result.RetryAfter is { } retryAfter ? now + XeroBackoff.RateLimitPause(retryAfter) : null;

        if (_rateLimiter?.PausedUntilUtc is { } limiterUntil
            && (until is not null || before.PausedUntilUtc is not null || _rateLimiter.PauseCameFromTooManyRequests || !ExplainedByNearlySpentMinute(limiterUntil, before))
            && (until is null || limiterUntil > until))
        {
            until = limiterUntil;
        }

        return until;
    }

    /// <summary>
    /// Whether the limiter's pause to <paramref name="limiterUntil"/>, taken
    /// during the request, is the nearly-spent-minute rule's rather than a
    /// 429's: the reading taken during the request was at or below the floor,
    /// and the pause ends no later than that rule can set it (the oldest call
    /// of the minute + 60 s, which is never after the reading + 60 s).
    /// </summary>
    private bool ExplainedByNearlySpentMinute(DateTimeOffset limiterUntil, LimiterSnapshot before) =>
        _rateLimiter?.LastReading is { MinuteRemaining: { } remaining } reading
        && !ReferenceEquals(reading, before.Reading)
        && reading.ReadAtUtc >= before.TakenAtUtc
        && remaining <= XeroRateLimiter.MinuteRemainingFloor
        && limiterUntil <= reading.ReadAtUtc + NearlySpentMinutePause;

    /// <summary>The client-side limiter as it was before an entry's requests.</summary>
    private readonly record struct LimiterSnapshot(DateTimeOffset? PausedUntilUtc, XeroRateLimitReading? Reading, DateTimeOffset TakenAtUtc);

    /// <summary>Whether <paramref name="operation"/> creates a Xero record — a request that, if it failed in transport, may still have made one.</summary>
    private static bool IsCreate(XeroOperation operation) =>
        operation is XeroOperation.PushQuote or XeroOperation.PushInvoiceDraft or XeroOperation.PushPurchaseOrder or XeroOperation.PushExpenseBill;

    // -------------------------------------------------- pauses and connection

    private async Task<bool> IsPausedAsync(CancellationToken cancellationToken) =>
        await IsWaitingForAuthorisationAsync(cancellationToken).ConfigureAwait(false)
        || (await ReadTimeAsync(PausedUntilKey, cancellationToken).ConfigureAwait(false) is { } until && until > _time.GetUtcNow())
        || _rateLimiter?.PausedUntilUtc is not null;

    private async Task<bool> IsWaitingForAuthorisationAsync(CancellationToken cancellationToken) =>
        (await _parts.Outbox.ListAsync([XeroOutboxState.WaitingForAuthorisation], cancellationToken).ConfigureAwait(false)).Count > 0;

    /// <summary>
    /// Whether the grant is usable again, so the waiting writes resume: seen
    /// unusable since the pause (a re-authorisation happened), or the probe
    /// interval passed. On resuming, the X1 settings are read (on connect) —
    /// unless <paramref name="refreshSettings"/> is <see langword="false"/>
    /// (the start-up recovery, which comes before any other work, the
    /// settings refresh included): then they are only marked unread, so the
    /// first cycle after recovery reads them. The caller holds
    /// <see cref="_networkGate"/>.
    /// </summary>
    private async Task<bool> TryResumeAsync(bool refreshSettings, CancellationToken cancellationToken)
    {
        if (_connection is null)
            return false;

        ConnectorAuthorisationState state;
        try
        {
            state = await _connection.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Warning("Xero sync could not read the authorisation state; writes keep waiting.", ex);
            return false;
        }

        if (state.Status != ConnectorAuthorisation.Authorised)
        {
            _sawUnusableGrant = true;
            return false;
        }

        var pausedAt = await ReadTimeAsync(AuthorisationPausedAtKey, cancellationToken).ConfigureAwait(false);
        var probeDue = pausedAt is not { } at || _time.GetUtcNow() - at >= _options.ReauthorisationProbeInterval;
        if (!_sawUnusableGrant && !probeDue)
            return false;

        await ResumeCoreAsync(_sawUnusableGrant ? "re-authorised" : "trying again after the re-authorisation interval", cancellationToken).ConfigureAwait(false);
        if (refreshSettings)
        {
            await RefreshSettingsCoreAsync(force: true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _settingsReadThisSession = false;
            _lastSettingsAttemptUtc = null;
        }

        return true;
    }

    private async Task<int> ResumeCoreAsync(string why, CancellationToken cancellationToken)
    {
        var resumed = await _drain.ResumeAfterAuthorisationAsync(cancellationToken).ConfigureAwait(false);
        await _store.DeleteAsync(StateCollection, AuthorisationPausedAtKey, cancellationToken).ConfigureAwait(false);
        _sawUnusableGrant = false;
        if (resumed > 0)
        {
            await AuditAsync(AuditResumed, new Dictionary<string, string>
            {
                ["entries"] = resumed.ToString(CultureInfo.InvariantCulture),
                ["reason"] = why,
            }, cancellationToken).ConfigureAwait(false);
        }

        return resumed;
    }

    /// <summary>On connect (a tenant first seen by this session, or a different one): the X1 settings are read and pre-v0.24 invoice links imported. The caller holds <see cref="_networkGate"/>.</summary>
    private async Task OnTenantAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (string.Equals(_lastTenantId, tenantId, StringComparison.Ordinal))
            return;

        var changed = _lastTenantId is not null;
        _lastTenantId = tenantId;
        if (changed)
            _settingsReadThisSession = false;

        if (_importer is not null)
            await Isolated(() => _importer.EnsureImportedAsync(tenantId, cancellationToken), "importing pre-v0.24 invoice links").ConfigureAwait(false);
    }

    private async Task<bool> SettingsAreStaleAsync(CancellationToken cancellationToken)
    {
        if (_settings is null)
            return false;

        if (!_settingsReadThisSession)
            return true; // on connect, and before the session's first write (recovery has already run at start-up).

        var reading = await _settings.ReadCachedAsync(cancellationToken).ConfigureAwait(false);
        return reading is null || _time.GetUtcNow() - reading.ReadAtUtc >= _options.SettingsRefreshInterval;
    }

    /// <summary>Reads the X1 settings from Xero (three calls). Not more than once per sync interval unless <paramref name="force"/>d, so an unreachable Xero is not asked on every pass. The caller holds <see cref="_networkGate"/>.</summary>
    private async Task<bool> RefreshSettingsCoreAsync(bool force, CancellationToken cancellationToken)
    {
        if (_settings is null)
            return false;

        var now = _time.GetUtcNow();
        if (!force && _lastSettingsAttemptUtc is { } last && now - last < _options.SyncInterval)
            return false;

        _lastSettingsAttemptUtc = now;
        try
        {
            var result = await _settings.RefreshAsync(cancellationToken).ConfigureAwait(false);
            if (result.Outcome != ConnectorOutcome.Ok)
            {
                _logger?.Warning($"Xero settings could not be read ({result.Outcome}: {result.Reason}); the last reading is used.");
                return false;
            }

            _settingsReadThisSession = true;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Warning("Xero settings could not be read; the last reading is used.", ex);
            return false;
        }
    }

    private async Task NoteRateLimiterPauseAsync(CancellationToken cancellationToken)
    {
        if (_rateLimiter?.PausedUntilUtc is { } until)
        {
            var stored = await ReadTimeAsync(PausedUntilKey, cancellationToken).ConfigureAwait(false);
            if (stored is null || stored < until)
                await WriteTimeAsync(PausedUntilKey, until, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Puts every entry that failed Blocked on a missing precondition back to Pending, so it is tried again (its precondition may now be met).</summary>
    private async Task RetryBlockedAsync(CancellationToken cancellationToken)
    {
        _lastBlockedRetryUtc = _time.GetUtcNow();
        foreach (var entry in await _parts.Outbox.ListAsync([XeroOutboxState.Failed], cancellationToken).ConfigureAwait(false))
        {
            var track = await ReadTrackAsync(entry.Id, cancellationToken).ConfigureAwait(false);
            if (track.Blocked)
                await _parts.Outbox.RetryAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> AnyDueAsync(XeroOutboxState[] states, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        return (await _parts.Outbox.ListAsync(states, cancellationToken).ConfigureAwait(false))
            .Any(e => e.NotBeforeUtc is not { } notBefore || notBefore <= now);
    }

    private async Task<bool> HasTombstoneAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken)
    {
        if (_parts.Creates is not { } creates || document.Kind is not (XeroDocumentKind.PurchaseOrder or XeroDocumentKind.ExpenseBill))
            return false;

        return (await creates.ListSentAsync(tenantId, document, cancellationToken).ConfigureAwait(false)).Any(c => c.IsTombstone);
    }

    /// <summary>The badge for a record with no open write, from what Xero last said of it.</summary>
    private async Task<(XeroSyncBadge Badge, string? Note)> LinkedBadgeAsync(XeroDocumentRef document, XeroLink link, CancellationToken cancellationToken)
    {
        var status = string.IsNullOrWhiteSpace(link.LastKnownXeroStatus) ? null : link.LastKnownXeroStatus.Trim().ToUpperInvariant();

        // Read side only (ADR-0162): Xero's approval words are matched the
        // way InvoicingService.InterpretStatus and XeroConnector read them,
        // never written.
        var approved = status?.Contains("AUTHORIS", StringComparison.Ordinal) == true;
        var awaitingApproval = XeroConnector.IsAwaitingApproval(status);
        switch (document.Kind)
        {
            case XeroDocumentKind.Invoice:
            case XeroDocumentKind.ExpenseBill:
            {
                var bill = document.Kind == XeroDocumentKind.ExpenseBill;
                if (status == "PAID")
                    return (XeroSyncBadge.Paid, null);
                if (approved)
                    return (XeroSyncBadge.AwaitingPayment, bill ? "Approved in Xero." : null);
                if (status == "VOIDED")
                    return (XeroSyncBadge.Voided, "Voided in Xero.");
                if (status == XeroConnector.DeletedStatus)
                    return (XeroSyncBadge.Voided, "Deleted in Xero.");
                if (awaitingApproval)
                    return (XeroSyncBadge.InXeroDraft, "Awaiting approval in Xero.");
                return (XeroSyncBadge.InXeroDraft, bill ? "Draft bill in Xero." : "Draft in Xero — review and send from Xero.");
            }

            case XeroDocumentKind.PurchaseOrder:
                if (status == XeroConnector.DeletedStatus)
                    return (XeroSyncBadge.Voided, "Deleted in Xero.");
                if (approved)
                    return (XeroSyncBadge.InXero, "Approved in Xero.");
                if (status == "BILLED")
                    return (XeroSyncBadge.InXero, "Billed in Xero.");
                if (awaitingApproval)
                    return (XeroSyncBadge.InXeroDraft, "Awaiting approval in Xero.");
                return (XeroSyncBadge.InXeroDraft, null);

            case XeroDocumentKind.Quote:
            {
                string? note = null;
                if (_parts.Quotes is { } quotes && Guid.TryParse(document.TempestKey, out var quotationId)
                    && await quotes.FindAsync(quotationId, cancellationToken).ConfigureAwait(false) is { } quote)
                {
                    note = XeroQuoteMapper.DriftNote(quote, link) ?? XeroQuoteMapper.DriftNote(link.LastKnownXeroStatus, quote.Status);
                }

                return status switch
                {
                    "DELETED" => (XeroSyncBadge.Failed, note ?? "Deleted in Xero."),
                    "DRAFT" or null => (XeroSyncBadge.InXeroDraft, note),
                    _ => (XeroSyncBadge.InXero, note),
                };
            }

            default:
                return (XeroSyncBadge.InXero, null);
        }
    }

    // ------------------------------------------------------------ own state

    /// <summary>What the engine remembers of one entry beyond the outbox's own fields.</summary>
    /// <param name="RecoveringSinceUtc">When the engine started recovering it as a write whose answer may have been lost; <see langword="null"/> when it is not.</param>
    /// <param name="RecoveryAttempts">Recovery attempts so far (the short schedule).</param>
    /// <param name="UnknownAnswers">Inconclusive answers so far.</param>
    /// <param name="Blocked">Whether it is Failed because a precondition was missing (retried automatically).</param>
    /// <param name="CannotTell">Whether it is Failed on X5's <em>CannotTell</em> verdict (<see cref="XeroPushResult.CannotTell"/>).</param>
    internal sealed record EntryTrack(DateTimeOffset? RecoveringSinceUtc = null, int RecoveryAttempts = 0, int UnknownAnswers = 0, bool Blocked = false, bool CannotTell = false);

    private async Task<EntryTrack> ReadTrackAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var json = await _store.ReadAsync(StateCollection, TrackPrefix + entryId.ToString("D"), cancellationToken).ConfigureAwait(false);
        if (json is null)
            return new EntryTrack();

        try
        {
            return JsonSerializer.Deserialize<EntryTrack>(json) ?? new EntryTrack();
        }
        catch (JsonException)
        {
            return new EntryTrack();
        }
    }

    private Task WriteTrackAsync(Guid entryId, EntryTrack track, CancellationToken cancellationToken) =>
        track == new EntryTrack()
            ? DeleteTrackAsync(entryId, cancellationToken)
            : _store.WriteAsync(StateCollection, TrackPrefix + entryId.ToString("D"), JsonSerializer.Serialize(track), cancellationToken);

    private Task DeleteTrackAsync(Guid entryId, CancellationToken cancellationToken) =>
        _store.DeleteAsync(StateCollection, TrackPrefix + entryId.ToString("D"), cancellationToken);

    private async Task<DateTimeOffset?> ReadTimeAsync(string key, CancellationToken cancellationToken)
    {
        var text = await _store.ReadAsync(StateCollection, key, cancellationToken).ConfigureAwait(false);
        return text is not null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
    }

    private Task WriteTimeAsync(string key, DateTimeOffset at, CancellationToken cancellationToken) =>
        _store.WriteAsync(StateCollection, key, at.ToString("O", CultureInfo.InvariantCulture), cancellationToken);

    // ------------------------------------------------------------ audit

    private static Dictionary<string, string> Detail(XeroOutboxEntry entry, XeroPushResult? result = null)
    {
        var detail = new Dictionary<string, string>
        {
            ["document"] = entry.Document.TempestKey,
            ["kind"] = entry.Document.Kind.ToString(),
            ["operation"] = entry.Operation.ToString(),
            ["entry"] = entry.Id.ToString("D"),
            ["idempotencyKey"] = entry.IdempotencyKey,
            ["attempt"] = entry.Attempts.ToString(CultureInfo.InvariantCulture),
        };

        if (entry.Argument is { } argument)
            detail["argument"] = argument;

        if (result is not null)
        {
            detail["outcome"] = result.Outcome.ToString();
            if (!string.IsNullOrWhiteSpace(result.Reason))
                detail["reason"] = result.Reason;
            if (result.Link is { } link)
            {
                detail["xeroId"] = link.XeroId;
                if (link.XeroNumber is { } number)
                    detail["xeroNumber"] = number;
            }
        }

        return detail;
    }

    private async Task AuditAsync(string action, IReadOnlyDictionary<string, string> detail, CancellationToken cancellationToken)
    {
        if (_audit is null)
            return;

        try
        {
            await _audit.RecordAsync(action, detail, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Error($"Xero sync could not write the audit row '{action}'.", ex);
        }
    }

    private async Task Isolated(Func<Task> work, string what)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.Error($"Xero sync failed {what}; isolated, the engine carries on.", ex);
        }
    }
}

/// <summary>Answers <see cref="IXeroSyncService"/> with the one <see cref="XeroSyncService"/> singleton (the container takes one registration per service type, so the interface is forwarded rather than registered a second time).</summary>
public sealed class XeroSyncServiceForwarder : IXeroSyncService
{
    private readonly XeroSyncService _engine;

    /// <summary>Initialises a new instance of the <see cref="XeroSyncServiceForwarder"/> class.</summary>
    /// <param name="engine">The engine.</param>
    public XeroSyncServiceForwarder(XeroSyncService engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public Task<XeroDrainReport> DrainAsync(CancellationToken cancellationToken = default) => _engine.DrainAsync(cancellationToken);

    /// <inheritdoc />
    public Task ReadBackAsync(CancellationToken cancellationToken = default) => _engine.ReadBackAsync(cancellationToken);

    /// <inheritdoc />
    public Task<XeroSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        _engine.GetStatusAsync(document, cancellationToken);
}
