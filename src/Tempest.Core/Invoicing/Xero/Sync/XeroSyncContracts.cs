namespace Tempest.Core.Invoicing.Xero.Sync;

// ============================================================================
// `v0.24.0` Xero integration (`ADR-0162`) — the seams the sync engine (X6)
// and the per-document push handlers (X3, X4, X5) build against. Contracts
// only: the behaviour is specified in
// `docs/releases/v0.24.0/Xero Technical Design.md` §5–§7 and built by the
// tasks in §11 of that document.
// ============================================================================

/// <summary>Which kind of TempestOS record a Xero link or outbox entry is about.</summary>
public enum XeroDocumentKind
{
    /// <summary>A customer or supplier (<c>Organisation</c>) linked to a Xero contact. Key: <c>Organisation.ReferenceKey</c>.</summary>
    Contact,

    /// <summary>A quotation, copied to a Xero quote (D2). Key: the quotation's id.</summary>
    Quote,

    /// <summary>An invoice request, created as a Xero <c>ACCREC</c> draft (D3). Key: the request's id.</summary>
    Invoice,

    /// <summary>A purchase order, copied to a Xero purchase order (D5). Key: the order's id.</summary>
    PurchaseOrder,

    /// <summary>A project expense, created as a Xero <c>ACCPAY</c> draft bill (D5). Key: the expense's id.</summary>
    ExpenseBill,
}

/// <summary>
/// One TempestOS record, as the sync engine names it: its kind and its
/// stable TempestOS key (a <see cref="Guid"/> in <c>"D"</c> format for a
/// document; <c>Organisation.ReferenceKey</c> for a contact).
/// </summary>
/// <param name="Kind">What kind of record this is.</param>
/// <param name="TempestKey">The record's own stable TempestOS key — never a Xero id.</param>
public sealed record XeroDocumentRef(XeroDocumentKind Kind, string TempestKey)
{
    /// <summary>The ref for a TempestOS document identified by <paramref name="id"/>.</summary>
    public static XeroDocumentRef For(XeroDocumentKind kind, Guid id) => new(kind, id.ToString("D"));
}

/// <summary>
/// The durable link between one TempestOS record and the one Xero record it
/// was pushed to or linked with, in one Xero organisation (tenant). A link
/// is written only after Xero has confirmed the record exists; while a link
/// exists the engine never issues a create for that record again — it
/// updates, or refuses with a reason.
/// </summary>
/// <param name="SchemaVersion">The stored shape's version; <see cref="CurrentSchemaVersion"/> when written by this build. A reader accepts every earlier version and ignores unknown JSON properties.</param>
/// <param name="TenantId">The Xero organisation this link belongs to. Links never cross tenants: the Demo Company's ids are never used against the live organisation (D7).</param>
/// <param name="Document">The TempestOS record.</param>
/// <param name="XeroId">Xero's own id: <c>ContactID</c>, <c>QuoteID</c>, <c>InvoiceID</c> (also for a bill) or <c>PurchaseOrderID</c>. Never changed once written.</param>
/// <param name="XeroNumber">Xero's own number as last read (<c>QuoteNumber</c>, <c>InvoiceNumber</c>, <c>PurchaseOrderNumber</c>, <c>ContactNumber</c>); <see langword="null"/> when Xero holds none.</param>
/// <param name="LastPushedContentHash">A hash of the content last pushed (lines, dates, totals, revision), so an unchanged record is not pushed twice; <see langword="null"/> for a contact link.</param>
/// <param name="LastKnownXeroStatus">Xero's own status word as last read (for example <c>"DRAFT"</c>, <c>"AUTHORISED"</c>, <c>"PAID"</c>), verbatim.</param>
/// <param name="AttachmentFileName">The file name of the TempestOS PDF last uploaded to this Xero record; <see langword="null"/> until one is.</param>
/// <param name="AttachmentContentHash">The SHA-256 (hex) of the PDF last uploaded, so the same PDF is not uploaded twice.</param>
/// <param name="LinkedAtUtc">When the link was first written.</param>
/// <param name="LastReadAtUtc">When Xero's status was last read back; <see langword="null"/> if never.</param>
/// <param name="LinkedBy">How the link came to exist: <c>"created"</c> (TempestOS created the Xero record), <c>"linked"</c> (the Product Owner confirmed an existing Xero contact), <c>"reconciled"</c> (found by number or reference after a lost response) or <c>"imported"</c> (carried over from an <c>InvoiceRequest.ExternalId</c> written before `v0.24.0`).</param>
public sealed record XeroLink(
    int SchemaVersion,
    string TenantId,
    XeroDocumentRef Document,
    string XeroId,
    string? XeroNumber,
    string? LastPushedContentHash,
    string? LastKnownXeroStatus,
    string? AttachmentFileName,
    string? AttachmentContentHash,
    DateTimeOffset LinkedAtUtc,
    DateTimeOffset? LastReadAtUtc,
    string LinkedBy)
{
    /// <summary>The schema version this build writes.</summary>
    public const int CurrentSchemaVersion = 1;
}

/// <summary>
/// Durable store of <see cref="XeroLink"/>s, one per (tenant, document).
/// Backed by <c>IPersistenceStore</c> (collection <c>"Xero.Links"</c>), so
/// no existing record's own persisted state changes shape: a record with no
/// link reads as "not sent".
/// </summary>
public interface IXeroLinkStore
{
    /// <summary>The link for <paramref name="document"/> in <paramref name="tenantId"/>, or <see langword="null"/> when none exists.</summary>
    Task<XeroLink?> FindAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary>Every link in <paramref name="tenantId"/>, optionally only those of <paramref name="kind"/>.</summary>
    Task<IReadOnlyList<XeroLink>> ListAsync(string tenantId, XeroDocumentKind? kind = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <paramref name="link"/>, creating or replacing the link for its
    /// (tenant, document). Refuses (<see cref="InvalidOperationException"/>,
    /// a defect, never a normal outcome) a write that would change an
    /// existing link's <see cref="XeroLink.XeroId"/> — a record is linked to
    /// one Xero record for life; re-linking is an explicit
    /// <see cref="UnlinkAsync"/> first.
    /// </summary>
    Task SaveAsync(XeroLink link, CancellationToken cancellationToken = default);

    /// <summary>Removes the link for <paramref name="document"/> (the Product Owner's explicit "unlink", or a Xero record found deleted). Audited by the caller.</summary>
    Task UnlinkAsync(string tenantId, XeroDocumentRef document, CancellationToken cancellationToken = default);
}

/// <summary>One kind of write the sync engine can queue for Xero.</summary>
public enum XeroOperation
{
    /// <summary>Create the Xero quote as <c>DRAFT</c>, or update its content while Xero still holds it as <c>DRAFT</c> (X3).</summary>
    PushQuote,

    /// <summary>Set the Xero quote's status to follow TempestOS: <c>SENT</c>, <c>ACCEPTED</c> or <c>DECLINED</c> (X3, D2). <see cref="XeroOutboxEntry.Argument"/> carries the target status word.</summary>
    SetQuoteStatus,

    /// <summary>Create the <c>ACCREC</c> invoice as <c>DRAFT</c> through <c>InvoicingService.SendAsync</c> (X4, D3).</summary>
    PushInvoiceDraft,

    /// <summary>Update the Xero invoice's content while Xero still holds it as <c>DRAFT</c>; refused with the reason otherwise (X4).</summary>
    UpdateInvoiceDraft,

    /// <summary>Delete the Xero invoice while it is still <c>DRAFT</c>, because the TempestOS request was voided (X4).</summary>
    DeleteInvoiceDraft,

    /// <summary>Create or update the Xero purchase order (X5).</summary>
    PushPurchaseOrder,

    /// <summary>Delete the Xero purchase order because the TempestOS order was cancelled (X5).</summary>
    DeletePurchaseOrder,

    /// <summary>Create or update the <c>ACCPAY</c> draft bill for an expense (X5).</summary>
    PushExpenseBill,

    /// <summary>Delete the draft bill because the expense was deleted, while Xero still holds it as <c>DRAFT</c> (X5).</summary>
    DeleteExpenseBill,

    /// <summary>Upload (or replace) the TempestOS PDF, or an expense's receipt, on the linked Xero record (X3–X5; <see cref="IXeroDocumentFileSource"/>). Always queued after the create it depends on.</summary>
    UploadAttachment,
}

/// <summary>Where one <see cref="XeroOutboxEntry"/> is in its life.</summary>
public enum XeroOutboxState
{
    /// <summary>Waiting to be sent — at once, or not before <see cref="XeroOutboxEntry.NotBeforeUtc"/> (backoff or a 429 <c>Retry-After</c>).</summary>
    Pending,

    /// <summary>Claimed by the drain and being sent. Found in this state at start-up, it is treated as <see cref="Unknown"/>: reconciled by lookup before any resend.</summary>
    InFlight,

    /// <summary>Sent and confirmed by Xero. Terminal.</summary>
    Succeeded,

    /// <summary>Xero refused it (validation, a business rule) or TempestOS's own safety rules did; carries the reason. Needs a person: <see cref="IXeroOutbox.RetryAsync"/> after fixing the cause.</summary>
    Failed,

    /// <summary>The request was sent but its response was lost; the next drain looks the record up by its number or reference before anything is resent.</summary>
    Unknown,

    /// <summary>The stored Xero grant is missing, expired, revoked, or lacks a scope this operation needs; waits for the Product Owner to re-authorise, then resumes.</summary>
    WaitingForAuthorisation,

    /// <summary>A later entry for the same document and operation replaced this one before it was sent. Terminal.</summary>
    Superseded,
}

/// <summary>
/// One queued write to Xero — the unit of retry, idempotency and audit. Held
/// in <c>IPersistenceStore</c> (collection <c>"Xero.Outbox"</c>), so a write
/// made while Xero is unreachable survives a restart and is sent on the next
/// drain.
/// </summary>
/// <param name="SchemaVersion">The stored shape's version; <see cref="CurrentSchemaVersion"/> when written by this build.</param>
/// <param name="Id">This entry's own id.</param>
/// <param name="Operation">What to do.</param>
/// <param name="Document">The TempestOS record it is about.</param>
/// <param name="Argument">An operation-specific value (the target quote status, the attachment's file name); <see langword="null"/> when the operation needs none.</param>
/// <param name="IdempotencyKey">
/// The <c>Idempotency-Key</c> header value (at most 128 characters), fixed
/// when the entry is enqueued and reused verbatim on every attempt:
/// <c>tos:{kind}:{key}:{operation}:{contentHash}</c>, shortened by hashing
/// when longer. A changed record is a new entry with a new key — Xero answers
/// 400 when a key is reused with a different body.
/// </param>
/// <param name="ContentHash">A hash of the content this entry pushes, for coalescing and for "nothing changed" short-cuts.</param>
/// <param name="State">Where the entry is in its life.</param>
/// <param name="Attempts">How many times it has been sent.</param>
/// <param name="EnqueuedAtUtc">When it was queued.</param>
/// <param name="NotBeforeUtc">Not sent before this moment (backoff, or a 429's <c>Retry-After</c>); <see langword="null"/> for "at once".</param>
/// <param name="LastAttemptAtUtc">When it was last sent; <see langword="null"/> if never.</param>
/// <param name="LastError">The last refusal or failure reason, for the Failed badge; <see langword="null"/> when none.</param>
/// <param name="EnqueuedBy">The signed-in principal (or <c>"system"</c>) that caused the entry, for the audit trail.</param>
public sealed record XeroOutboxEntry(
    int SchemaVersion,
    Guid Id,
    XeroOperation Operation,
    XeroDocumentRef Document,
    string? Argument,
    string IdempotencyKey,
    string ContentHash,
    XeroOutboxState State,
    int Attempts,
    DateTimeOffset EnqueuedAtUtc,
    DateTimeOffset? NotBeforeUtc,
    DateTimeOffset? LastAttemptAtUtc,
    string? LastError,
    string EnqueuedBy)
{
    /// <summary>The schema version this build writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Xero's documented maximum <c>Idempotency-Key</c> length.</summary>
    public const int MaximumIdempotencyKeyLength = 128;

    /// <summary>
    /// `v0.24.0` F1 (additive, M1): the Xero organisation (tenant) this entry
    /// was last claimed for — sent to — so a
    /// <see cref="XeroOutboxState.Succeeded"/> entry names the organisation it
    /// succeeded in. <see langword="null"/> while it has never been sent, and
    /// for an entry written before F1. The outbox de-duplicates a new write
    /// only against open entries and entries that succeeded in the
    /// organisation connected now (design §6.1): a record sent to the Demo
    /// Company is not "already sent" to another organisation.
    /// </summary>
    public string? TenantId { get; init; }
}

/// <summary>
/// The durable queue of writes to Xero. Domain services (or the commands
/// over them) enqueue; only <see cref="IXeroSyncService.DrainAsync"/> sends.
/// </summary>
/// <remarks>
/// Ordering is per document: an entry is not sent while an earlier entry
/// for the same document is <see cref="XeroOutboxState.Pending"/>,
/// <see cref="XeroOutboxState.Unknown"/>, <see cref="XeroOutboxState.Failed"/>
/// or <see cref="XeroOutboxState.WaitingForAuthorisation"/> — a status change
/// is never sent ahead of the create it depends on. A new entry for the same
/// document and operation supersedes a still-pending one.
/// </remarks>
public interface IXeroOutbox
{
    /// <summary>
    /// Queues <paramref name="operation"/> for <paramref name="document"/>.
    /// <paramref name="contentHash"/> identifies the content to push; an
    /// entry identical to one already open, or one that succeeded in the
    /// organisation connected now (<see cref="XeroOutboxEntry.TenantId"/>),
    /// for the same content is not queued twice (the existing entry is
    /// returned).
    /// </summary>
    Task<XeroOutboxEntry> EnqueueAsync(
        XeroOperation operation, XeroDocumentRef document, string contentHash, string? argument = null,
        CancellationToken cancellationToken = default);

    /// <summary>Every entry for <paramref name="document"/>, oldest first.</summary>
    Task<IReadOnlyList<XeroOutboxEntry>> ListForDocumentAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary>Every entry in one of <paramref name="states"/> (all entries when empty), oldest first.</summary>
    Task<IReadOnlyList<XeroOutboxEntry>> ListAsync(IReadOnlyCollection<XeroOutboxState> states, CancellationToken cancellationToken = default);

    /// <summary>
    /// The Product Owner's <em>Retry</em> on a <see cref="XeroOutboxState.Failed"/>
    /// entry: back to <see cref="XeroOutboxState.Pending"/>, keeping its
    /// idempotency key. <see langword="false"/> when the entry is not Failed.
    /// </summary>
    Task<bool> RetryAsync(Guid entryId, CancellationToken cancellationToken = default);
}

/// <summary>What one push handler reports for one attempt at one <see cref="XeroOutboxEntry"/>.</summary>
public enum XeroPushOutcome
{
    /// <summary>Xero confirmed the write; the handler has saved the link.</summary>
    Succeeded,

    /// <summary>Nothing needed sending (the content and PDF already match the link). Treated as success.</summary>
    NothingToDo,

    /// <summary>Xero, or a TempestOS safety rule (D3, D4, D7), refused it with a reason. The entry becomes Failed.</summary>
    Rejected,

    /// <summary>A precondition in TempestOS is missing — most often the customer or supplier is not yet linked to a Xero contact (X2). The entry becomes Failed with that reason, and is retried automatically once the precondition is met.</summary>
    Blocked,

    /// <summary>The grant is missing, expired or lacks a scope. The entry and the whole queue wait for re-authorisation.</summary>
    Reauthorise,

    /// <summary>Xero could not be reached, answered 5xx, or answered 429. The entry stays Pending with <see cref="XeroPushResult.RetryAfter"/> honoured.</summary>
    RetryLater,

    /// <summary>The request went out but the response was lost. The entry becomes Unknown and is reconciled by lookup before any resend.</summary>
    Unknown,
}

/// <summary>The result of one push attempt.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Reason">Why, for every outcome but <see cref="XeroPushOutcome.Succeeded"/>/<see cref="XeroPushOutcome.NothingToDo"/>; an engineer-readable message (Xero's own <c>ValidationErrors</c> joined, where Xero sent them).</param>
/// <param name="RetryAfter">How long to wait before the next attempt, from a 429 <c>Retry-After</c> header; <see langword="null"/> to let the engine's own backoff decide.</param>
/// <param name="Link">The link as saved after a successful write; <see langword="null"/> otherwise.</param>
public sealed record XeroPushResult(XeroPushOutcome Outcome, string? Reason = null, TimeSpan? RetryAfter = null, XeroLink? Link = null)
{
    /// <summary>
    /// Whether this <see cref="XeroPushOutcome.Rejected"/> is X5's
    /// <em>CannotTell</em> verdict: a create whose answer was lost and cannot be
    /// recovered, so TempestOS cannot tell whether Xero holds the record
    /// (<c>XeroPurchasingOwnership.CannotTell</c>). The engine keeps it with the
    /// Failed entry and reports it on <see cref="XeroDocumentSyncStatus.CannotTell"/>.
    /// </summary>
    public bool CannotTell { get; init; }
}

/// <summary>
/// Sends one kind of <see cref="XeroOperation"/>. One implementation per
/// operation (X3: quotes, X4: invoices, X5: purchase orders and bills, the
/// attachment uploader), each in its own file, registered with the engine.
/// A handler never throws for anything a call to Xero can ordinarily produce
/// (`ADR-0151`); it never creates a Xero record when a link for the document
/// already exists in the tenant; and before resending an entry whose state
/// was <see cref="XeroOutboxState.Unknown"/> it looks the record up by its
/// number or reference first.
/// </summary>
public interface IXeroPushHandler
{
    /// <summary>The operations this handler sends.</summary>
    IReadOnlyCollection<XeroOperation> Operations { get; }

    /// <summary>Sends <paramref name="entry"/> to the Xero organisation <paramref name="tenantId"/>.</summary>
    Task<XeroPushResult> PushAsync(string tenantId, XeroOutboxEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>One write a <see cref="IXeroSyncPlanner"/> decides a record needs.</summary>
/// <param name="Operation">What to queue.</param>
/// <param name="ContentHash">A hash of the content the write carries (see <see cref="XeroOutboxEntry.ContentHash"/>).</param>
/// <param name="Argument">The operation's argument, if any (see <see cref="XeroOutboxEntry.Argument"/>).</param>
public sealed record XeroPlannedOperation(XeroOperation Operation, string ContentHash, string? Argument = null);

/// <summary>
/// Decides, from a TempestOS record's current state and its link, which
/// writes Xero still needs — desired state, not events. The engine (X6)
/// asks the planner for a record after every committed change to it
/// (<c>IWorkspaceChanges</c>) and, at start-up and on Refresh, for every
/// in-scope record — so a change committed just before a crash, or made
/// offline, is still queued. Domain services stay unaware of Xero. One
/// planner per <see cref="XeroDocumentKind"/> (X3, X4, X5), each in its own
/// file.
/// </summary>
public interface IXeroSyncPlanner
{
    /// <summary>The kind of link this planner plans for.</summary>
    XeroDocumentKind Kind { get; }

    /// <summary>The TempestOS canonical Kind it watches (for example <c>"Quotation"</c>, <c>"InvoiceRequest"</c>, <c>"PurchaseOrder"</c>, <c>"ProjectExpense"</c>).</summary>
    string CanonicalKind { get; }

    /// <summary>
    /// The writes <paramref name="objectId"/> still needs, given its
    /// <paramref name="link"/> (<see langword="null"/> when not yet linked);
    /// empty when Xero already matches, or when the record is not in scope
    /// (a draft quote, an invoice request not yet sent). Pure with respect
    /// to Xero: never a network call.
    /// </summary>
    Task<IReadOnlyList<XeroPlannedOperation>> PlanAsync(Guid objectId, XeroLink? link, CancellationToken cancellationToken = default);
}

/// <summary>The one Xero badge shown on a quote, invoice, purchase order or expense (X6).</summary>
public enum XeroSyncBadge
{
    /// <summary>Nothing has been queued or sent for this record.</summary>
    NotSent,

    /// <summary>Queued; Xero has not confirmed it yet (offline, waiting for a retry, or in flight).</summary>
    Queued,

    /// <summary>Held in Xero as a draft — the Product Owner reviews, approves and sends it in Xero (D3, D4).</summary>
    InXeroDraft,

    /// <summary>Held in Xero (a quote or purchase order copy, at the status TempestOS last set).</summary>
    InXero,

    /// <summary>Approved in Xero, not yet paid (Xero <c>AUTHORISED</c>).</summary>
    AwaitingPayment,

    /// <summary>Paid in full (Xero <c>PAID</c>), read back from Xero.</summary>
    Paid,

    /// <summary>Voided or deleted in Xero.</summary>
    Voided,

    /// <summary>The last write was refused or is blocked; <see cref="XeroSyncStatus.Reason"/> says why and <see cref="XeroSyncStatus.RetryableEntryId"/> offers Retry.</summary>
    Failed,

    /// <summary>Xero needs re-authorising before anything more is sent.</summary>
    NeedsReauthorisation,
}

/// <summary>What the X6 badge shows for one record.</summary>
/// <param name="Badge">The badge.</param>
/// <param name="Reason">Why, for <see cref="XeroSyncBadge.Failed"/>/<see cref="XeroSyncBadge.NeedsReauthorisation"/>, or a note (for example "Xero shows this quote as INVOICED"); <see langword="null"/> when none.</param>
/// <param name="XeroNumber">The number Xero holds the record under, when linked.</param>
/// <param name="XeroStatus">Xero's own status word as last read, verbatim.</param>
/// <param name="AsOfUtc">When the badge's facts were last confirmed against Xero; <see langword="null"/> when never.</param>
/// <param name="RetryableEntryId">The Failed outbox entry a Retry acts on; <see langword="null"/> when there is nothing to retry.</param>
public sealed record XeroSyncStatus(
    XeroSyncBadge Badge, string? Reason = null, string? XeroNumber = null, string? XeroStatus = null,
    DateTimeOffset? AsOfUtc = null, Guid? RetryableEntryId = null);

/// <summary>A summary of one drain.</summary>
/// <param name="Attempted">Entries sent this drain.</param>
/// <param name="Succeeded">Entries Xero confirmed (including nothing-to-do).</param>
/// <param name="Failed">Entries that became Failed.</param>
/// <param name="Deferred">Entries left Pending or Unknown for a later drain (backoff, 429, unreachable).</param>
/// <param name="PausedForAuthorisation">Whether the drain stopped because Xero needs re-authorising.</param>
/// <param name="ResumeNotBeforeUtc">When the next drain may send anything, after a 429; <see langword="null"/> when not rate-limited.</param>
public sealed record XeroDrainReport(
    int Attempted, int Succeeded, int Failed, int Deferred, bool PausedForAuthorisation, DateTimeOffset? ResumeNotBeforeUtc);

/// <summary>
/// The Xero sync engine (X6): drains the outbox one entry at a time
/// (never more than one request in flight per tenant), honours 429
/// <c>Retry-After</c> and backoff, reads statuses back, and answers the badge
/// for any record. Runs on a timer as a hosted service and on demand
/// (Refresh, and straight after an enqueue when online).
/// </summary>
public interface IXeroSyncService
{
    /// <summary>Sends every due entry, in order, until the queue is empty, a 429 or authorisation failure pauses it, or <paramref name="cancellationToken"/> is cancelled.</summary>
    Task<XeroDrainReport> DrainAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads Xero's status back for every linked record that is not terminal (batched, <c>If-Modified-Since</c>), updating links and the records' read-only status fields.</summary>
    Task ReadBackAsync(CancellationToken cancellationToken = default);

    /// <summary>The badge for <paramref name="document"/>, from the link store and outbox alone — never a network call.</summary>
    Task<XeroSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);
}
