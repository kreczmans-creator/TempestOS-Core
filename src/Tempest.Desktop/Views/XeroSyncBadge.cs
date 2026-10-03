using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

// ============================================================================
// `v0.24.0` U3 (`ADR-0162`; design §4, §6, §11 U3): the one Xero badge shown
// on a quote, invoice, purchase order or expense, with its actions, and the
// small helpers the issuing views use to keep the rendered PDF on the record
// (so X6's `IXeroDocumentFileSource` can upload exactly what was exported).
// Read-only over X6: every badge comes from the link store and the outbox
// alone (`XeroSyncService.GetDocumentStatusAsync`) — never a network call, so
// a badge renders offline and never blocks the UI thread.
// ============================================================================

/// <summary>What one badge action (Retry, Send again, Send to Xero) did, in the words shown to the person.</summary>
/// <param name="Succeeded">Whether the action queued anything.</param>
/// <param name="Message">What to show: the confirmation, or why nothing was queued.</param>
public sealed record XeroBadgeActionResult(bool Succeeded, string Message);

/// <summary>
/// Where a <see cref="XeroSyncBadgeControl"/> reads its badge from and sends
/// its actions to (`v0.24.0` U3). The real shell uses
/// <see cref="XeroSyncServiceBadgeSource"/> over X6's engine; headless tests
/// use a fake.
/// </summary>
public interface IXeroBadgeSource
{
    /// <summary>
    /// Raised, on any thread, whenever a badge may have changed (an engine
    /// cycle finished, or an action was taken); badges reload themselves on
    /// the UI thread.
    /// </summary>
    event Action? Changed;

    /// <summary>The badge, its words and actions for <paramref name="document"/> — from local state alone, never a network call.</summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<XeroDocumentSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary><em>Retry</em> on the Failed outbox entry <paramref name="entryId"/> (<see cref="IXeroOutbox.RetryAsync"/>, through the engine so it is audited and woken).</summary>
    /// <param name="entryId">The Failed entry (<see cref="XeroSyncStatus.RetryableEntryId"/>).</param>
    /// <param name="cancellationToken">Cancels the retry.</param>
    Task<XeroBadgeActionResult> RetryAsync(Guid entryId, CancellationToken cancellationToken = default);

    /// <summary><em>Send again</em> on a purchase order or expense whose Xero record TempestOS made was deleted there (X5, <see cref="XeroPurchasingSendAgain"/>).</summary>
    /// <param name="document">The purchase order or expense.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<XeroBadgeActionResult> SendAgainAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary><em>Send to Xero</em> on a record raised before Xero sync began (Q8): records the person's request (audited) and queues its writes.</summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<XeroBadgeActionResult> SendToXeroAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary>Whether <em>Send to Xero</em> exists for records of <paramref name="kind"/> (X3 quotes; X5 purchase orders and expenses). X4 exposes none for invoices: an invoice goes to Xero through its own <em>Send</em>.</summary>
    /// <param name="kind">The kind of record.</param>
    bool CanSendToXero(XeroDocumentKind kind);

    /// <summary>
    /// Whether <paramref name="document"/>, reading <em>Not sent</em>, needs
    /// the person's explicit <em>Send to Xero</em> (Q8) — as its kind's
    /// planner decides: raised before Xero sync began and not opted in. A
    /// record that will sync on its own, or one the planner refuses (an
    /// expense recorded from a purchase order, Q6), does not. Local state
    /// only, never a network call.
    /// </summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<bool> NeedsSendToXeroAsync(XeroDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary>
    /// How the badge asks the person before a deliberate action (Unlink from
    /// Xero, link by Xero number) and the Invoicing area before a send that
    /// might bill twice (`v0.24.0` review-board fixes M5, m14, m15).
    /// <see langword="null"/> (the default) offers none of those actions: they
    /// never run without asking.
    /// </summary>
    XeroBadgePrompts? Prompts => null;

    /// <summary>Whether <em>Check Xero now</em> is offered (review-board fix M3).</summary>
    bool CanCheckNow => false;

    /// <summary>
    /// <em>Check Xero now</em>: sends what is queued and reads statuses back from
    /// Xero now, rather than on the 15-minute timer, so a change made in Xero
    /// shows on demand (review-board fix M3).
    /// </summary>
    /// <param name="document">The record whose badge was used.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    Task<XeroBadgeActionResult> CheckNowAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult(new XeroBadgeActionResult(false, "Checking Xero now is not offered here."));

    /// <summary>Whether <em>Unlink from Xero</em> is offered for <paramref name="document"/>: linked, and its Xero copy last read as deleted or voided (review-board fix M5). Local state only.</summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<bool> CanUnlinkAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <summary><em>Unlink from Xero</em> (<see cref="XeroDocumentLinkActions.UnlinkAsync"/>): audited; nothing is sent.</summary>
    /// <param name="document">The record.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    Task<XeroBadgeActionResult> UnlinkAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult(new XeroBadgeActionResult(false, "Unlinking is not offered here."));

    /// <summary>Whether a <em>Can't tell</em> purchase order or bill can be looked up and linked by its Xero number (review-board fix m15).</summary>
    bool CanLinkByNumber => false;

    /// <summary>Looks <paramref name="number"/> up in Xero for <paramref name="document"/> — read only (<see cref="XeroDocumentLinkActions.FindByNumberAsync"/>).</summary>
    /// <param name="document">The purchase order or bill.</param>
    /// <param name="number">The number the person found it under in Xero.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    Task<XeroNumberLookup> FindByNumberAsync(XeroDocumentRef document, string number, CancellationToken cancellationToken = default) =>
        Task.FromResult(new XeroNumberLookup(null, "Linking by Xero number is not offered here."));

    /// <summary>Links the record the person confirmed (<see cref="XeroDocumentLinkActions.LinkByNumberAsync"/>), audited.</summary>
    /// <param name="match">What the lookup found, as confirmed.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    Task<XeroBadgeActionResult> LinkByNumberAsync(XeroNumberMatch match, CancellationToken cancellationToken = default) =>
        Task.FromResult(new XeroBadgeActionResult(false, "Linking by Xero number is not offered here."));
}

/// <summary>
/// How Xero's deliberate actions ask the person (`v0.24.0` review-board fixes
/// M5, m14, m15): a yes/no confirmation and a one-line text answer. The shell
/// supplies its own dialogs; tests supply fakes.
/// </summary>
/// <param name="Confirm">Asks a yes/no question: title, message and the confirm button's words; <see langword="true"/> to go ahead.</param>
/// <param name="Ask">Asks for one line of text: title and label; <see langword="null"/> when the person cancelled.</param>
public sealed record XeroBadgePrompts(Func<string, string, string, Task<bool>> Confirm, Func<string, string, Task<string?>> Ask);

/// <summary>
/// The <see cref="IXeroBadgeSource"/> over X6's <see cref="XeroSyncService"/>
/// and the X3/X5 planners' own <em>Send to Xero</em> (Q8). Every action
/// wakes the engine, so what was queued is sent on its next drain.
/// </summary>
public sealed class XeroSyncServiceBadgeSource : IXeroBadgeSource
{
    private readonly XeroSyncService _engine;
    private readonly XeroQuotePlanner? _quotes;
    private readonly XeroPurchaseOrderPlanner? _orders;
    private readonly XeroExpenseBillPlanner? _expenses;
    private readonly IXeroQuoteSource? _quoteSource;
    private readonly IXeroPurchaseOrderSource? _orderSource;
    private readonly XeroDocumentLinkActions? _links;

    /// <summary>Initialises a new instance of the <see cref="XeroSyncServiceBadgeSource"/> class.</summary>
    /// <param name="engine">X6's engine: badges, Retry and Send again.</param>
    /// <param name="quotes">X3's planner, for a quotation's <em>Send to Xero</em>; <see langword="null"/> offers none.</param>
    /// <param name="orders">X5's purchase-order planner, for <em>Send to Xero</em>; <see langword="null"/> offers none.</param>
    /// <param name="expenses">X5's expense-bill planner, for <em>Send to Xero</em>; <see langword="null"/> offers none.</param>
    /// <param name="quoteSource">X3's quotation reader, so <see cref="NeedsSendToXeroAsync"/> asks the planner whether a quotation is synced automatically (<see cref="XeroQuotePlanner.IsAutomaticAsync"/>); <see langword="null"/> falls back to whether the planner would plan anything.</param>
    /// <param name="orderSource">X5's purchase-order reader, so a cancelled order is never offered <em>Send to Xero</em>; <see langword="null"/> relies on the planner alone.</param>
    /// <param name="links">The person's link actions (Unlink from Xero, Send again after an unlink, link by Xero number); <see langword="null"/> offers none of them.</param>
    public XeroSyncServiceBadgeSource(
        XeroSyncService engine, XeroQuotePlanner? quotes = null, XeroPurchaseOrderPlanner? orders = null, XeroExpenseBillPlanner? expenses = null,
        IXeroQuoteSource? quoteSource = null, IXeroPurchaseOrderSource? orderSource = null, XeroDocumentLinkActions? links = null)
    {
        ArgumentNullException.ThrowIfNull(engine);

        _engine = engine;
        _quotes = quotes;
        _orders = orders;
        _expenses = expenses;
        _quoteSource = quoteSource;
        _orderSource = orderSource;
        _links = links;
        _engine.CycleCompleted += _ => Changed?.Invoke();
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>How deliberate actions ask the person; set by the shell (its confirmation and input dialogs). <see langword="null"/> offers none of them.</summary>
    public XeroBadgePrompts? Prompts { get; set; }

    /// <inheritdoc />
    public bool CanCheckNow => true;

    /// <inheritdoc />
    public bool CanLinkByNumber => _links is { CanLookUpInXero: true };

    /// <summary>
    /// The source over the Xero services the host registered, or
    /// <see langword="null"/> when Xero is not the configured connector (no
    /// engine is registered) — the views then show no Xero badge at all.
    /// </summary>
    /// <param name="services">The host's service provider.</param>
    public static XeroSyncServiceBadgeSource? TryCreate(ITempestServiceProvider? services)
    {
        if (services is null || TryResolve<XeroSyncService>(services) is not { } engine)
            return null;

        return new XeroSyncServiceBadgeSource(
            engine, TryResolve<XeroQuotePlanner>(services), TryResolve<XeroPurchaseOrderPlanner>(services), TryResolve<XeroExpenseBillPlanner>(services),
            TryResolve<IXeroQuoteSource>(services), TryResolve<IXeroPurchaseOrderSource>(services), TryResolve<XeroDocumentLinkActions>(services));
    }

    /// <inheritdoc />
    /// <remarks>
    /// A record the person unlinked from Xero (its copy was deleted there)
    /// offers <em>Send again</em> — except an invoice, which is billed again
    /// by raising a new request.
    /// </remarks>
    public async Task<XeroDocumentSyncStatus> GetStatusAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        var status = await _engine.GetDocumentStatusAsync(document, cancellationToken).ConfigureAwait(false);
        if (_links is null || status.Status.Badge != XeroSyncBadge.NotSent || !await _links.WasUnlinkedAsync(document, cancellationToken).ConfigureAwait(false))
            return status;

        return document.Kind == XeroDocumentKind.Invoice
            ? status with { Status = status.Status with { Reason = UnlinkedInvoiceNote } }
            : status with { Status = status.Status with { Reason = UnlinkedNote }, CanSendAgain = true };
    }

    /// <summary>The note on a record unlinked from Xero by the person.</summary>
    public const string UnlinkedNote = "Unlinked from Xero (its Xero copy was deleted there). Choose Send again to send it to Xero as a new draft.";

    /// <summary>The note on an invoice unlinked from Xero by the person.</summary>
    public const string UnlinkedInvoiceNote = "Unlinked from Xero (its Xero copy was deleted there). Raise a new invoice request to bill this work again.";

    /// <inheritdoc />
    public async Task<XeroBadgeActionResult> CheckNowAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        var drain = await _engine.DrainAsync(cancellationToken).ConfigureAwait(false);
        var readBack = await _engine.ReadBackNowAsync(cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();

        if (readBack is null)
        {
            return new XeroBadgeActionResult(
                false,
                drain.PausedForAuthorisation
                    ? "Xero could not be checked: it needs re-authorising (Settings → Xero → Re-authorise)."
                    : "Xero could not be checked now: no organisation is connected, or Xero is waiting for authorisation or asked TempestOS to slow down. Try again shortly.");
        }

        return new XeroBadgeActionResult(
            true,
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Checked Xero: {readBack.Read} record(s) read back, {readBack.Changed.Count} changed")
            + (drain.Attempted > 0 ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"; {drain.Succeeded} of {drain.Attempted} queued write(s) sent.") : "."));
    }

    /// <inheritdoc />
    public async Task<bool> CanUnlinkAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        _links is not null && await _links.CanUnlinkAsync(document, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<XeroBadgeActionResult> UnlinkAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        if (_links is null)
            return new XeroBadgeActionResult(false, "Unlinking is not offered here.");

        var result = await _links.UnlinkAsync(document, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
        return new XeroBadgeActionResult(result.Done, result.Message);
    }

    /// <inheritdoc />
    public Task<XeroNumberLookup> FindByNumberAsync(XeroDocumentRef document, string number, CancellationToken cancellationToken = default) =>
        _links is null
            ? Task.FromResult(new XeroNumberLookup(null, "Linking by Xero number is not offered here."))
            : _links.FindByNumberAsync(document, number, cancellationToken);

    /// <inheritdoc />
    public async Task<XeroBadgeActionResult> LinkByNumberAsync(XeroNumberMatch match, CancellationToken cancellationToken = default)
    {
        if (_links is null)
            return new XeroBadgeActionResult(false, "Linking by Xero number is not offered here.");

        var result = await _links.LinkByNumberAsync(match, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
        return new XeroBadgeActionResult(result.Done, result.Message);
    }

    /// <inheritdoc />
    public async Task<XeroBadgeActionResult> RetryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var retried = await _engine.RetryAsync(entryId, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
        return retried
            ? new XeroBadgeActionResult(true, "Queued for Xero again.")
            : new XeroBadgeActionResult(false, "Nothing to retry: that write is no longer failed.");
    }

    /// <inheritdoc />
    public async Task<XeroBadgeActionResult> SendAgainAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        // A record the person unlinked is sent again through the link actions
        // (a new draft, a new key); anything else is X5's own Send again.
        if (_links is not null && await _links.WasUnlinkedAsync(document, cancellationToken).ConfigureAwait(false))
        {
            var again = await _links.SendAgainAsync(document, cancellationToken).ConfigureAwait(false);
            Changed?.Invoke();
            return new XeroBadgeActionResult(again.Done, again.Message);
        }

        var result = await _engine.SendAgainAsync(document, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();
        return result.Queued
            ? new XeroBadgeActionResult(true, "Queued to be sent to Xero again as a new draft.")
            : new XeroBadgeActionResult(false, result.Reason ?? "Nothing was queued.");
    }

    /// <inheritdoc />
    public async Task<XeroBadgeActionResult> SendToXeroAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!Guid.TryParse(document.TempestKey, out var id))
            return new XeroBadgeActionResult(false, "This record cannot be sent to Xero.");

        (bool Queued, string? Reason) result = document.Kind switch
        {
            XeroDocumentKind.Quote when _quotes is not null => From(await _quotes.SendToXeroAsync(id, cancellationToken).ConfigureAwait(false)),
            XeroDocumentKind.PurchaseOrder when _orders is not null => From(await _orders.SendToXeroAsync(id, cancellationToken).ConfigureAwait(false)),
            XeroDocumentKind.ExpenseBill when _expenses is not null => From(await _expenses.SendToXeroAsync(id, cancellationToken).ConfigureAwait(false)),
            _ => (false, "Send to Xero is not offered for this record."),
        };

        if (result.Queued)
            _engine.Signal();
        Changed?.Invoke();

        return result.Queued
            ? new XeroBadgeActionResult(true, "Queued for Xero.")
            : new XeroBadgeActionResult(false, result.Reason ?? "Nothing was queued.");
    }

    /// <inheritdoc />
    public bool CanSendToXero(XeroDocumentKind kind) => kind switch
    {
        XeroDocumentKind.Quote => _quotes is not null,
        XeroDocumentKind.PurchaseOrder => _orders is not null,
        XeroDocumentKind.ExpenseBill => _expenses is not null,
        _ => false,
    };

    /// <inheritdoc />
    /// <remarks>
    /// Asks each kind's own planner (Q8), with no link — a record reading
    /// <em>Not sent</em> has none: a quotation needs it unless it was issued
    /// after automatic sync began (<see cref="XeroQuotePlanner.IsAutomaticAsync"/>);
    /// a purchase order or expense needs it only while its planner plans
    /// nothing for it (not automatic, not opted in) — never for a cancelled
    /// order, nor for an expense the planner deliberately does not push
    /// (<see cref="XeroExpenseBillPlanner.DescribeNotPushedAsync"/>, Q6).
    /// A planner that cannot answer offers nothing.
    /// </remarks>
    public async Task<bool> NeedsSendToXeroAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!Guid.TryParse(document.TempestKey, out var id))
            return false;

        switch (document.Kind)
        {
            case XeroDocumentKind.Quote when _quotes is not null:
                if (_quoteSource is not null)
                {
                    return await _quoteSource.FindAsync(id, cancellationToken).ConfigureAwait(false) is { IsIssued: true } quote
                           && !await _quotes.IsAutomaticAsync(quote, cancellationToken).ConfigureAwait(false);
                }

                return (await _quotes.PlanAsync(id, null, cancellationToken).ConfigureAwait(false)).Count == 0;

            case XeroDocumentKind.PurchaseOrder when _orders is not null:
                if (_orderSource is not null
                    && await _orderSource.FindAsync(id, cancellationToken).ConfigureAwait(false) is not { WasIssued: true, Status: not Tempest.Core.PurchaseOrders.PurchaseOrderStatus.Cancelled })
                {
                    return false;
                }

                return (await _orders.PlanAsync(id, null, cancellationToken).ConfigureAwait(false)).Count == 0;

            case XeroDocumentKind.ExpenseBill when _expenses is not null:
                return await _expenses.DescribeNotPushedAsync(id, cancellationToken).ConfigureAwait(false) is null
                       && (await _expenses.PlanAsync(id, null, cancellationToken).ConfigureAwait(false)).Count == 0;

            default:
                return false;
        }
    }

    private static (bool, string?) From(XeroQuoteSendRequest request) => (request.Queued, request.Reason);

    private static (bool, string?) From(XeroPurchasingSendRequest request) => (request.Queued, request.Reason);

    private static T? TryResolve<T>(ITempestServiceProvider services) where T : class
    {
        try
        {
            return services.GetService(typeof(T)) as T;
        }
        catch (ServiceResolutionException)
        {
            return null;
        }
#pragma warning disable CA1031 // Badges are advisory: a Xero service that cannot be built leaves the views without badges, never the shell without views.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}

/// <summary>How a badge is coloured: by what the person needs to do about it.</summary>
public enum XeroBadgeTone
{
    /// <summary>Nothing has happened yet, or nothing needs doing (Not sent, Voided).</summary>
    Neutral,

    /// <summary>On its way, or waiting in Xero for the person (Queued, a draft in Xero).</summary>
    Pending,

    /// <summary>Done (in Xero past draft, Paid).</summary>
    Good,

    /// <summary>Needs the person (Failed, Can't tell, Deleted in Xero, Waiting for authorisation).</summary>
    Attention,
}

/// <summary>A badge's words: its short text, any detail (the reason or Xero's note) and its tone.</summary>
/// <param name="Text">The badge's own words, for example <em>Queued</em> or <em>Draft in Xero — review and send from Xero</em>.</param>
/// <param name="Detail">The reason or note under it; <see langword="null"/> when none.</param>
/// <param name="Tone">How it is coloured.</param>
public sealed record XeroBadgePresentation(string Text, string? Detail, XeroBadgeTone Tone);

/// <summary>
/// Turns X6's <see cref="XeroDocumentSyncStatus"/> into the words the badge
/// shows (`v0.24.0` U3, design §4): one place, so every view says the same.
/// </summary>
public static class XeroBadgeText
{
    /// <summary>Nothing queued or sent.</summary>
    public const string NotSent = "Not sent";

    /// <summary>Queued; Xero has not confirmed it yet.</summary>
    public const string Queued = "Queued";

    /// <summary>A quote, purchase order or bill held as a draft in Xero.</summary>
    public const string InXeroDraft = "In Xero (draft)";

    /// <summary>An invoice held as a draft in Xero (D3, D4): TempestOS never approves or sends it.</summary>
    public const string InvoiceDraft = "Draft in Xero — review and send from Xero";

    /// <summary>A quote or purchase order held in Xero past draft.</summary>
    public const string InXero = "In Xero";

    /// <summary>A quote Xero holds as <c>SENT</c>.</summary>
    public const string QuoteSent = "Sent in Xero";

    /// <summary>A quote Xero holds as <c>ACCEPTED</c>.</summary>
    public const string QuoteAccepted = "Accepted in Xero";

    /// <summary>A quote Xero holds as <c>DECLINED</c>.</summary>
    public const string QuoteDeclined = "Declined in Xero";

    /// <summary>A quote invoiced in Xero (raising it from TempestOS too would bill twice).</summary>
    public const string QuoteInvoiced = "Invoiced in Xero";

    /// <summary>Approved in Xero, not yet paid.</summary>
    public const string AwaitingPayment = "Awaiting payment";

    /// <summary>Paid in full, read back from Xero.</summary>
    public const string Paid = "Paid";

    /// <summary>Voided in Xero.</summary>
    public const string Voided = "Voided";

    /// <summary>Deleted in Xero.</summary>
    public const string Deleted = "Deleted in Xero";

    /// <summary>The last write was refused or blocked.</summary>
    public const string Failed = "Failed";

    /// <summary>A purchase order or bill whose create's answer was lost and cannot be recovered (X5 <em>CannotTell</em>): someone must check Xero.</summary>
    public const string CannotTell = "Can't tell";

    /// <summary>Xero needs (re-)authorising before anything more is sent.</summary>
    public const string WaitingForAuthorisation = "Waiting for authorisation";

    /// <summary>A write waiting on something missing in TempestOS (review-board fix n5) — sent by itself once it is put right.</summary>
    public const string Waiting = "Waiting";

    /// <summary>The start of the words for a write waiting for its customer or supplier to be linked to a Xero contact (review-board fix n5): <c>"Waiting: link Acme Ltd"</c>.</summary>
    public const string WaitingForContactPrefix = "Waiting: link ";

    /// <summary>
    /// The customer or supplier a Blocked write waits for, as the X2 linker
    /// names it (<c>'{reference}' is not linked to a Xero contact yet</c>);
    /// <see langword="null"/> when the reason is something else.
    /// </summary>
    /// <param name="reason">The Blocked write's reason.</param>
    public static string? ContactAwaitingLink(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            reason, "'(?<name>[^']+)' is not linked to a Xero contact", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups["name"].Value.Trim() : null;
    }

    /// <summary>The badge's words for <paramref name="status"/> on a record of <paramref name="kind"/>.</summary>
    /// <param name="kind">The kind of record.</param>
    /// <param name="status">X6's badge for it.</param>
    public static XeroBadgePresentation Describe(XeroDocumentKind kind, XeroDocumentSyncStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var reason = string.IsNullOrWhiteSpace(status.Status.Reason) ? null : status.Status.Reason.Trim();
        var xeroStatus = status.Status.XeroStatus?.Trim().ToUpperInvariant();

        // Deleted in Xero is read from typed facts, never the reason's words:
        // Xero's own status word as last read (DELETED), or X5's tombstone —
        // a purchase order or bill TempestOS made, deleted there — which is
        // exactly when X6 offers Send again.
        var deletedInXero = xeroStatus == XeroConnectorDeletedStatus || status.CanSendAgain;

        XeroBadgePresentation Make(string text, XeroBadgeTone tone) =>
            new(text, reason is not null && string.Equals(reason.TrimEnd('.'), text, StringComparison.OrdinalIgnoreCase) ? null : reason, tone);

        switch (status.Status.Badge)
        {
            case XeroSyncBadge.NotSent:
                return status.CanSendAgain ? Make(Deleted, XeroBadgeTone.Attention) : Make(NotSent, XeroBadgeTone.Neutral);

            case XeroSyncBadge.Queued:
                return Make(Queued, XeroBadgeTone.Pending);

            case XeroSyncBadge.InXeroDraft:
                return Make(kind == XeroDocumentKind.Invoice ? InvoiceDraft : InXeroDraft, XeroBadgeTone.Pending);

            case XeroSyncBadge.InXero:
                if (kind == XeroDocumentKind.Quote)
                {
                    var text = xeroStatus switch
                    {
                        "SENT" => QuoteSent,
                        "ACCEPTED" => QuoteAccepted,
                        "DECLINED" => QuoteDeclined,
                        "INVOICED" => QuoteInvoiced,
                        _ => InXero,
                    };
                    return Make(text, xeroStatus == "INVOICED" ? XeroBadgeTone.Attention : XeroBadgeTone.Good);
                }

                return Make(InXero, XeroBadgeTone.Good);

            case XeroSyncBadge.AwaitingPayment:
                return Make(AwaitingPayment, XeroBadgeTone.Pending);

            case XeroSyncBadge.Paid:
                return Make(Paid, XeroBadgeTone.Good);

            case XeroSyncBadge.Voided:
                return deletedInXero ? Make(Deleted, XeroBadgeTone.Neutral) : Make(Voided, XeroBadgeTone.Neutral);

            case XeroSyncBadge.Failed:
                if (status.CannotTell)
                    return Make(CannotTell, XeroBadgeTone.Attention);
                if (status.Blocked)
                {
                    // n5: waiting on something in TempestOS (most often a
                    // contact link), sent by itself once it is put right — not
                    // a red failure.
                    return ContactAwaitingLink(reason) is { } contact
                        ? Make(WaitingForContactPrefix + contact, XeroBadgeTone.Pending)
                        : Make(Waiting, XeroBadgeTone.Pending);
                }

                if (deletedInXero)
                    return Make(Deleted, XeroBadgeTone.Attention);
                return Make(Failed, XeroBadgeTone.Attention);

            case XeroSyncBadge.NeedsReauthorisation:
                return Make(WaitingForAuthorisation, XeroBadgeTone.Attention);

            default:
                return Make(status.Label, XeroBadgeTone.Neutral);
        }
    }

    /// <summary>Xero's status word for a record deleted there (<see cref="Tempest.Core.Invoicing.Xero.XeroConnector.DeletedStatus"/>).</summary>
    private const string XeroConnectorDeletedStatus = Tempest.Core.Invoicing.Xero.XeroConnector.DeletedStatus;
}

/// <summary>
/// The Xero badge for one quote, invoice, purchase order or expense
/// (`v0.24.0` U3, design §11): X6's status in words
/// (<see cref="XeroBadgeText"/>), its reason or note, and the actions it
/// offers — <b>Retry</b> on a failed write, <b>Send again</b> on a purchase
/// order or bill deleted in Xero (X5), and <b>Send to Xero</b> on a record
/// raised before Xero sync began (Q8); and (`v0.24.0` review-board fixes)
/// <b>Check Xero now</b> (M3), <b>Unlink from Xero</b> on a record whose Xero
/// copy was deleted there (M5), and, on a <em>Can't tell</em> purchase order or
/// bill, the bookkeeper guidance and <b>I found it in Xero — link by Xero
/// number</b> instead of Retry (m15) — each deliberate action confirmed first. Reads local state only, asynchronously
/// (<see cref="LoadAsync"/>), and reloads itself whenever its source says
/// something changed while it is on screen.
/// </summary>
public sealed class XeroSyncBadgeControl : Border
{
    private readonly IXeroBadgeSource _source;
    private readonly TextBlock _label = new() { FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false };
    private readonly Button _retry = new() { Content = "Retry", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _sendAgain = new() { Content = "Send again", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _sendToXero = new() { Content = "Send to Xero", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _checkNow = new() { Content = CheckNowText, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _unlink = new() { Content = XeroDocumentLinkActions.UnlinkActionName, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _linkByNumber = new() { Content = XeroDocumentLinkActions.LinkByNumberActionName, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _guidance = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false };
    private readonly TextBlock _checkedAt = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7, IsVisible = false };
    private readonly Border _badgeBorder;
    private readonly bool _offerSendToXero;
    private int _loadVersion;
    private bool _busy;

    /// <summary>Initialises a new instance of the <see cref="XeroSyncBadgeControl"/> class; call <see cref="LoadAsync"/> to fill it.</summary>
    /// <param name="source">Where the badge is read from and its actions go.</param>
    /// <param name="document">The record.</param>
    /// <param name="reference">The record's own number or name, for the automation names (<c>"Xero status for Q-001"</c>).</param>
    /// <param name="offerSendToXero">Whether this record could be sent to Xero on demand (Q8): it is issued (a quote approved or sent, a purchase order issued) or recorded (an expense). <em>Send to Xero</em> is then offered while the badge reads <em>Not sent</em> and the source says the record needs it (<see cref="IXeroBadgeSource.NeedsSendToXeroAsync"/>).</param>
    public XeroSyncBadgeControl(IXeroBadgeSource source, XeroDocumentRef document, string reference, bool offerSendToXero = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        _source = source;
        Document = document;
        Reference = reference;
        _offerSendToXero = offerSendToXero && source.CanSendToXero(document.Kind);

        _label.Text = "Xero: …";
        AutomationProperties.SetName(this, $"Xero status for {reference}");
        AutomationProperties.SetName(_label, $"Xero status for {reference}");
        AutomationProperties.SetName(_detail, $"Xero status detail for {reference}");
        AutomationProperties.SetName(_retry, $"Retry Xero for {reference}");
        AutomationProperties.SetName(_sendAgain, $"Send {reference} to Xero again");
        AutomationProperties.SetName(_sendToXero, $"Send {reference} to Xero");
        AutomationProperties.SetName(_checkNow, $"Check Xero now for {reference}");
        AutomationProperties.SetName(_unlink, $"Unlink {reference} from Xero");
        AutomationProperties.SetName(_linkByNumber, $"Link {reference} by Xero number");
        AutomationProperties.SetName(_guidance, $"Xero guidance for {reference}");
        AutomationProperties.SetName(_checkedAt, $"Xero checked at for {reference}");
        ToolTip.SetTip(_checkNow, "Send what is queued and read statuses back from Xero now, not on the 15-minute timer");
        ToolTip.SetTip(_unlink, "Its Xero copy was deleted there: forget that copy (audited). Nothing is sent; Send again afterwards sends it as a new draft");
        ToolTip.SetTip(_linkByNumber, "You found it in Xero: enter its Xero number, check what TempestOS found, and link it (audited)");
        ToolTip.SetTip(_retry, "Queue the failed write for Xero again");
        ToolTip.SetTip(_sendAgain, "Send it to Xero again as a new draft (the deleted one stays deleted)");
        ToolTip.SetTip(_sendToXero, "Raised before Xero sync began: send it to Xero now");
        _retry.Classes.Add(ChromeStyles.Flat);
        _sendAgain.Classes.Add(ChromeStyles.Flat);
        _sendToXero.Classes.Add(ChromeStyles.Flat);
        _checkNow.Classes.Add(ChromeStyles.Flat);
        _unlink.Classes.Add(ChromeStyles.Flat);
        _linkByNumber.Classes.Add(ChromeStyles.Flat);
        _guidance.Text = XeroDocumentLinkActions.BookkeeperGuidance;

        _retry.Click += async (_, _) => await RunAsync(ct => Status?.Status.RetryableEntryId is { } id
            ? _source.RetryAsync(id, ct)
            : Task.FromResult(new XeroBadgeActionResult(false, "Nothing to retry."))).ConfigureAwait(true);
        _sendAgain.Click += async (_, _) => await RunAsync(ct => _source.SendAgainAsync(Document, ct)).ConfigureAwait(true);
        _sendToXero.Click += async (_, _) => await RunAsync(ct => _source.SendToXeroAsync(Document, ct)).ConfigureAwait(true);
        _checkNow.Click += async (_, _) => await RunAsync(ct => _source.CheckNowAsync(Document, ct)).ConfigureAwait(true);
        _unlink.Click += async (_, _) => await RunAsync(UnlinkAsync).ConfigureAwait(true);
        _linkByNumber.Click += async (_, _) => await RunAsync(LinkByNumberAsync).ConfigureAwait(true);

        _badgeBorder = new Border
        {
            Child = _label,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(DesignTokens.BadgeCornerRadius),
            Padding = new Thickness(6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyTone(XeroBadgeTone.Neutral);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        row.Children.Add(_badgeBorder);
        row.Children.Add(_retry);
        row.Children.Add(_sendAgain);
        row.Children.Add(_sendToXero);
        row.Children.Add(_linkByNumber);
        row.Children.Add(_unlink);
        row.Children.Add(_checkNow);

        var body = new StackPanel { Spacing = DesignTokens.SpaceXs };
        body.Children.Add(row);
        body.Children.Add(_detail);
        body.Children.Add(_guidance);
        body.Children.Add(_checkedAt);
        Child = body;

        AttachedToVisualTree += (_, _) => _source.Changed += OnSourceChanged;
        DetachedFromVisualTree += (_, _) => _source.Changed -= OnSourceChanged;
    }

    /// <summary>The words on the <em>Check Xero now</em> button (review-board fix M3).</summary>
    public const string CheckNowText = "Check Xero now";

    /// <summary>The time zone "checked with Xero at" is shown in (review-board fix n6); the machine's own unless a test pins one.</summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

    /// <summary>Raised after an action completes, with what to show — forwarded by each view to its own <c>ActionCompleted</c>.</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>The record this badge is for.</summary>
    public XeroDocumentRef Document { get; }

    /// <summary>The record's own number or name.</summary>
    public string Reference { get; }

    /// <summary>X6's badge as last loaded; <see langword="null"/> before the first load.</summary>
    public XeroDocumentSyncStatus? Status { get; private set; }

    /// <summary>The badge's words as last shown; <see langword="null"/> before the first load.</summary>
    public XeroBadgePresentation? Presentation { get; private set; }

    /// <summary>The badge's text as shown (<c>"Xero: Queued"</c>).</summary>
    public string Text => _label.Text ?? string.Empty;

    /// <summary>Whether <em>Retry</em> is offered.</summary>
    public bool OffersRetry => _retry.IsVisible;

    /// <summary>Whether <em>Send again</em> is offered.</summary>
    public bool OffersSendAgain => _sendAgain.IsVisible;

    /// <summary>Whether <em>Send to Xero</em> is offered.</summary>
    public bool OffersSendToXero => _sendToXero.IsVisible;

    /// <summary>Whether <em>Check Xero now</em> is offered.</summary>
    public bool OffersCheckNow => _checkNow.IsVisible;

    /// <summary>Whether <em>Unlink from Xero</em> is offered.</summary>
    public bool OffersUnlink => _unlink.IsVisible;

    /// <summary>Whether <em>I found it in Xero — link by Xero number</em> is offered.</summary>
    public bool OffersLinkByNumber => _linkByNumber.IsVisible;

    /// <summary>The bookkeeper guidance shown on a <em>Can't tell</em> badge; <see langword="null"/> when none is shown.</summary>
    public string? Guidance => _guidance.IsVisible ? _guidance.Text : null;

    /// <summary>When the badge's facts were last confirmed with Xero, in local time (<c>"Checked with Xero 02 Oct 2026 10:00"</c>); <see langword="null"/> when never.</summary>
    public string? CheckedAtText => _checkedAt.IsVisible ? _checkedAt.Text : null;

    /// <summary>Reads the badge (local state only) and shows it. Never throws for a failed read: the badge says it could not be read.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        XeroDocumentSyncStatus status;
        var offerSendToXero = false;
        var offerUnlink = false;
        try
        {
            status = await _source.GetStatusAsync(Document, cancellationToken).ConfigureAwait(true);

            // M5: Unlink from Xero only for a record whose Xero copy was last
            // read as deleted or voided — and only where the person can be asked.
            if (_source.Prompts is not null)
                offerUnlink = await _source.CanUnlinkAsync(Document, cancellationToken).ConfigureAwait(true);

            // Send to Xero (Q8) only where the record's planner says it needs
            // the person's opt-in — never for one that will sync on its own,
            // nor one the planner refuses (Q6).
            if (_offerSendToXero && status is { Status.Badge: XeroSyncBadge.NotSent, CanSendAgain: false })
                offerSendToXero = await NeedsSendToXeroAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
#pragma warning disable CA1031 // A badge is advisory: a failed local read is shown on the badge, never thrown into the view.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (version == Volatile.Read(ref _loadVersion))
                Show(null, new XeroBadgePresentation("Status unavailable", ex.GetBaseException().Message, XeroBadgeTone.Attention));
            return;
        }

        // A newer load started while this one read: it wins.
        if (version != Volatile.Read(ref _loadVersion))
            return;

        Show(status, XeroBadgeText.Describe(Document.Kind, status), offerSendToXero, offerUnlink);
    }

    private async Task<bool> NeedsSendToXeroAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _source.NeedsSendToXeroAsync(Document, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Advisory: a planner that cannot answer offers no Send to Xero, and the badge still shows.
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }

    private void Show(XeroDocumentSyncStatus? status, XeroBadgePresentation presentation, bool offerSendToXero = false, bool offerUnlink = false)
    {
        Status = status;
        Presentation = presentation;

        _label.Text = $"Xero: {presentation.Text}";
        AutomationProperties.SetHelpText(_label, presentation.Detail);
        ToolTip.SetTip(_label, presentation.Detail);
        _detail.Text = presentation.Detail;
        _detail.IsVisible = presentation.Detail is not null;
        ApplyTone(presentation.Tone);

        // m15: a Can't tell write is never retried (Retry would do nothing);
        // the person checks Xero instead, and links what they find.
        var cannotTell = status?.CannotTell == true;
        _retry.IsVisible = status?.CanRetry == true && !cannotTell;
        _sendAgain.IsVisible = status?.CanSendAgain == true;
        _sendToXero.IsVisible = offerSendToXero && status is { Status.Badge: XeroSyncBadge.NotSent, CanSendAgain: false };
        _linkByNumber.IsVisible = cannotTell && _source.CanLinkByNumber && _source.Prompts is not null;
        _guidance.IsVisible = cannotTell;
        _unlink.IsVisible = offerUnlink;
        _checkNow.IsVisible = _source.CanCheckNow && status is not null && status.Status.Badge != XeroSyncBadge.NotSent;

        // n6: when Xero last confirmed the facts, in local time.
        if (status?.Status.AsOfUtc is { } asOf)
        {
            _checkedAt.Text = $"Checked with Xero {TimeZoneInfo.ConvertTime(asOf, TimeZone).ToString("dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";
            _checkedAt.IsVisible = true;
        }
        else
        {
            _checkedAt.IsVisible = false;
        }

        SetButtonsEnabled(!_busy);
    }

    private async Task<XeroBadgeActionResult> UnlinkAsync(CancellationToken cancellationToken)
    {
        if (_source.Prompts is not { } prompts)
            return new XeroBadgeActionResult(false, "Nothing can ask you to confirm the unlink here.");

        var confirmed = await prompts.Confirm(
            "Unlink from Xero?",
            $"Xero shows that its copy of {Reference} was deleted (or voided) there. Unlinking makes TempestOS forget that copy; "
            + "it sends nothing now. Choose Send again afterwards to send it to Xero as a new draft. This is recorded in the audit log.",
            "Unlink").ConfigureAwait(true);
        return confirmed
            ? await _source.UnlinkAsync(Document, cancellationToken).ConfigureAwait(true)
            : new XeroBadgeActionResult(false, "Nothing was unlinked.");
    }

    private async Task<XeroBadgeActionResult> LinkByNumberAsync(CancellationToken cancellationToken)
    {
        if (_source.Prompts is not { } prompts)
            return new XeroBadgeActionResult(false, "Nothing can ask you for the Xero number here.");

        var number = await prompts.Ask("Link by Xero number", $"The number Xero shows for {Reference}").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(number))
            return new XeroBadgeActionResult(false, "Nothing was linked.");

        var lookup = await _source.FindByNumberAsync(Document, number.Trim(), cancellationToken).ConfigureAwait(true);
        if (lookup.Match is not { } match)
            return new XeroBadgeActionResult(false, lookup.Reason ?? "Nothing was found in Xero; nothing was linked.");

        var confirmed = await prompts.Confirm(
            "Link to this Xero record?",
            $"Xero holds {match.Describe()}. Link {Reference} to it? TempestOS then treats it as this record's copy in Xero and never creates another. "
            + "This is recorded in the audit log.",
            "Link").ConfigureAwait(true);
        return confirmed
            ? await _source.LinkByNumberAsync(match, cancellationToken).ConfigureAwait(true)
            : new XeroBadgeActionResult(false, "Nothing was linked.");
    }

    private void ApplyTone(XeroBadgeTone tone)
    {
        var brushKey = tone switch
        {
            XeroBadgeTone.Good => ApplicationPalette.HealthTextHealthyBrushKey,
            XeroBadgeTone.Attention => ApplicationPalette.HealthTextBlockedBrushKey,
            XeroBadgeTone.Pending => ApplicationPalette.HealthTextAttentionBrushKey,
            _ => ApplicationPalette.HealthTextUnknownBrushKey,
        };
        ThemeReactiveBrush.Bind(_label, TextBlock.ForegroundProperty, brushKey);
        ThemeReactiveBrush.Bind(_badgeBorder, BorderBrushProperty, brushKey);
    }

    private async Task RunAsync(Func<CancellationToken, Task<XeroBadgeActionResult>> action)
    {
        if (_busy)
            return;

        _busy = true;
        SetButtonsEnabled(false);
        XeroBadgeActionResult result;
        try
        {
            result = await action(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Reported on screen like every other failed action, never thrown out of a click handler.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            result = new XeroBadgeActionResult(false, $"Xero: {ex.GetBaseException().Message}");
        }
        finally
        {
            _busy = false;
        }

        await LoadAsync().ConfigureAwait(true);
        ActionCompleted?.Invoke(result.Message, result.Succeeded ? ActionOutcome.Changed : ActionOutcome.Failed);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _retry.IsEnabled = enabled;
        _sendAgain.IsEnabled = enabled;
        _sendToXero.IsEnabled = enabled;
        _checkNow.IsEnabled = enabled;
        _unlink.IsEnabled = enabled;
        _linkByNumber.IsEnabled = enabled;
    }

    private void OnSourceChanged() => Dispatcher.UIThread.Post(async () => await LoadAsync().ConfigureAwait(true));
}

/// <summary>
/// Keeps the PDF a person issued on the record it was issued for (`v0.24.0`
/// U3; design §1, §3 "the same number and the same PDF"): the bytes saved by
/// an Export or an Issue are attached to the quotation, invoice request or
/// purchase order, so X6's <see cref="IXeroDocumentFileSource"/> uploads
/// exactly what was exported. File naming and folders are the caller's,
/// unchanged.
/// </summary>
public static class XeroIssuedPdf
{
    /// <summary>The MIME type issued documents are attached as.</summary>
    public const string ContentType = "application/pdf";

    /// <summary>
    /// Whether <paramref name="quotation"/> has been issued — an approved
    /// revision, or sent and answered — so its exported sheet is the one Xero
    /// should carry. A draft or in-review sheet is never attached: it would
    /// otherwise count as "exported" the moment the quotation is approved
    /// (X3's planner), and Xero would get the draft's PDF.
    /// </summary>
    /// <param name="quotation">The quotation.</param>
    public static bool IsIssued(Tempest.Core.Quotations.Quotation quotation)
    {
        ArgumentNullException.ThrowIfNull(quotation);

        return (quotation.Status == Tempest.Core.Quotations.QuotationStatus.Approved && quotation.RevisionNumber > 0)
               || quotation.Status is Tempest.Core.Quotations.QuotationStatus.Sent
                   or Tempest.Core.Quotations.QuotationStatus.Accepted
                   or Tempest.Core.Quotations.QuotationStatus.Declined;
    }

    /// <summary>
    /// Whether an invoice request in <paramref name="status"/> was sent to
    /// the accounting system, so its exported document is the one its Xero
    /// draft should carry: <see cref="InvoiceRequestStatus.Sent"/>,
    /// <see cref="InvoiceRequestStatus.Accepted"/>, or
    /// <see cref="InvoiceRequestStatus.Unknown"/> (sent, its answer lost — it
    /// may well be in Xero). Never a draft, a request still
    /// <see cref="InvoiceRequestStatus.Sending"/>, one waiting to
    /// <see cref="InvoiceRequestStatus.Reauthorise"/> (it never reached Xero),
    /// a rejected or a voided one.
    /// </summary>
    /// <param name="status">The request's status.</param>
    public static bool IsSent(InvoiceRequestStatus status) =>
        status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted or InvoiceRequestStatus.Unknown;

    /// <summary>
    /// Attaches <paramref name="bytes"/> to <paramref name="record"/> under
    /// <paramref name="fileName"/> — unless the newest PDF already attached
    /// says the same: byte-for-byte the same, or, given
    /// <paramref name="renderAt"/>, the same document rendered at the moment
    /// that PDF says it was generated. Every render stamps the time it was
    /// made (the footer's "Generated … UTC" and the PDF's creation date), so
    /// two exports of an unchanged record never match byte-for-byte; asking
    /// the renderer for this export's document at the kept PDF's own time
    /// and finding the kept bytes proves the content unchanged, so an
    /// unchanged re-export adds nothing and queues no second upload to Xero.
    /// Never throws for an attach the platform refuses (no content store, too
    /// large, an archived project, the content store failing to write): the
    /// export itself already succeeded.
    /// </summary>
    /// <param name="record">The issued record.</param>
    /// <param name="fileName">The attachment's name (the exported file's own name).</param>
    /// <param name="bytes">The exact bytes saved.</param>
    /// <param name="renderAt">Renders this export's document as if generated at the given moment — the same model and renderer, only <c>GeneratedAtUtc</c> changed; <see langword="null"/> compares bytes alone.</param>
    /// <param name="cancellationToken">Cancels the attach.</param>
    /// <returns>Whether the PDF is now on the record (newly attached, or already there).</returns>
    public static async Task<bool> AttachAsync(
        IHasAttachments record, string fileName, ReadOnlyMemory<byte> bytes, Func<DateTimeOffset, ReadOnlyMemory<byte>>? renderAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        try
        {
            var attachments = await record.GetAttachmentsAsync(cancellationToken).ConfigureAwait(true);
            var newestPdf = attachments.LastOrDefault(a =>
                string.Equals(a.ContentType, ContentType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetExtension(a.FileName), ".pdf", StringComparison.OrdinalIgnoreCase));
            if (newestPdf is not null)
            {
                var existing = await record.ReadAttachmentContentAsync(newestPdf.Id, cancellationToken).ConfigureAwait(true);
                if (existing.IsAvailable && SameDocument(existing.Bytes, bytes.Span, renderAt))
                    return true;
            }

            await record.AttachContentAsync(fileName, ContentType, bytes, cancellationToken).ConfigureAwait(true);
            return true;
        }
        catch (InvalidOperationException)
        {
            // No attachment content store (a Core-only host), or the record refuses changes.
            return false;
        }
        catch (EngineeringDomainException)
        {
            // Too large, or a domain rule refused the change (an archived project, say).
            return false;
        }
        catch (IOException)
        {
            // The content store could not write (a full disk, say): the exported file is saved; only the kept copy is missing.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // The content store's folder refused the write.
            return false;
        }
    }

    /// <summary>
    /// The moment a PDF this application rendered says it was generated —
    /// its document information <c>/CreationDate</c>, which every renderer
    /// sets from its model's <c>GeneratedAtUtc</c> — to the second, read as
    /// UTC (the renderers pass a UTC time, so the written fields are UTC
    /// whatever offset suffix follows them). <see langword="null"/> when it
    /// carries none this can read.
    /// </summary>
    /// <param name="pdf">The PDF's bytes.</param>
    public static DateTimeOffset? GeneratedAtOf(ReadOnlySpan<byte> pdf)
    {
        var at = pdf.IndexOf("/CreationDate"u8);
        if (at < 0)
            return null;

        var rest = pdf[(at + "/CreationDate"u8.Length)..];
        var open = rest.IndexOf("(D:"u8);
        if (open is < 0 or > 4)
            return null;

        rest = rest[(open + 3)..];
        if (rest.Length < 14)
            return null;

        Span<int> parts = stackalloc int[6];
        ReadOnlySpan<int> widths = [4, 2, 2, 2, 2, 2];
        var offset = 0;
        for (var i = 0; i < widths.Length; i++)
        {
            var value = 0;
            for (var j = 0; j < widths[i]; j++)
            {
                var c = rest[offset++];
                if (c is < (byte)'0' or > (byte)'9')
                    return null;
                value = (value * 10) + (c - '0');
            }

            parts[i] = value;
        }

        try
        {
            return new DateTimeOffset(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool SameDocument(ReadOnlySpan<byte> kept, ReadOnlySpan<byte> exported, Func<DateTimeOffset, ReadOnlyMemory<byte>>? renderAt)
    {
        if (kept.SequenceEqual(exported))
            return true;

        if (renderAt is null || GeneratedAtOf(kept) is not { } keptAt)
            return false;

        return kept.SequenceEqual(renderAt(keptAt).Span);
    }
}

/// <summary>
/// Wraps a document renderer and keeps the bytes of its last render, so a
/// caller exporting through <see cref="DocumentExporter"/> can attach exactly
/// the bytes that were saved (`v0.24.0` U3) without changing the exporter or
/// the file it names.
/// </summary>
/// <typeparam name="TModel">The renderer's model.</typeparam>
public sealed class CapturingDocumentRenderer<TModel> : IDocumentRenderer<TModel>
{
    private readonly IDocumentRenderer<TModel> _inner;

    /// <summary>Initialises a new instance of the <see cref="CapturingDocumentRenderer{TModel}"/> class.</summary>
    /// <param name="inner">The renderer to run.</param>
    public CapturingDocumentRenderer(IDocumentRenderer<TModel> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    public string DocumentType => _inner.DocumentType;

    /// <inheritdoc />
    public string TemplateName => _inner.TemplateName;

    /// <summary>The bytes of the last <see cref="Render"/>; <see langword="null"/> before any.</summary>
    public ReadOnlyMemory<byte>? LastRender { get; private set; }

    /// <summary>The identity the last <see cref="Render"/> was asked for (<see langword="null"/>: the renderer's own).</summary>
    public OrganisationIdentity? LastIdentity { get; private set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(TModel model, OrganisationIdentity? identity = null)
    {
        var bytes = _inner.Render(model, identity);
        LastRender = bytes;
        LastIdentity = identity;
        return bytes;
    }

    /// <summary>Renders <paramref name="model"/> with the wrapped renderer and the last render's identity, without recording it — for <see cref="XeroIssuedPdf.AttachAsync"/>'s re-render at a kept PDF's own time.</summary>
    /// <param name="model">The model.</param>
    public ReadOnlyMemory<byte> RenderAgain(TModel model) => _inner.Render(model, LastIdentity);
}
