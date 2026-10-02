using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Quotations;

namespace Tempest.Core.Invoicing;

/// <summary>One of Tempest's own sent invoice requests, as the receivable side of <see cref="AccountsSnapshot"/> sees it (`WP 19.8B`, scope §3).</summary>
/// <param name="RequestId">The <see cref="InvoiceRequest.Id"/> this figure comes from.</param>
/// <param name="ClientOrganisationId">The client this invoice was raised against.</param>
/// <param name="IssuedDate">When the invoice was issued, as the connector reported it (<see cref="InvoiceRequest.IssuedDate"/>).</param>
/// <param name="DueDate">The request's own <see cref="InvoiceRequest.DueOn"/> (`TD-180`, `WP 20.1B`) — falls back to <paramref name="IssuedDate"/> itself only for a request whose <see cref="InvoiceRequest.SentAtUtc"/> was never recorded (a pre-existing, latent gap on the "Sent, response lost" path, outside this Work Package's own scope), so this figure is never left unset for a request this list already includes.</param>
/// <param name="Amount">The request's own total.</param>
public sealed record ReceivableInvoice(Guid RequestId, string ClientOrganisationId, DateOnly IssuedDate, DateOnly DueDate, Money Amount);

/// <summary>One week of <see cref="AccountsSnapshot.CashFlowSeries"/> — receivable expected in, payable expected out, and the running cash position that results (`WP 19.8B`, scope §3).</summary>
/// <param name="WeekStart">The first day of this week, counting from the snapshot's own <see cref="AccountsSnapshot.AsOf"/> date.</param>
/// <param name="ReceivableIn">The sum of every receivable (`WP 19.8B` scope §3's own Tempest-side figures) whose own due date falls in this week.</param>
/// <param name="PayableOut">The sum of every bill and repeating bill whose own due date falls in this week.</param>
/// <param name="ClosingCash">The running cash position at the end of this week — the previous week's own closing figure (or the cash position's own total, for the first week), plus <paramref name="ReceivableIn"/>, minus <paramref name="PayableOut"/>. A projection, not a forecast this platform stands behind: nothing beyond a bill's or a repeating bill's own next occurrence is assumed (`AccountsSnapshot`'s own remarks).</param>
public sealed record CashFlowWeek(DateOnly WeekStart, Money ReceivableIn, Money PayableOut, Money ClosingCash);

/// <summary>
/// The coherent read the Business dashboard (`WP 19.7B`) will call — tiles,
/// the receivable and payable lists, the cash position and a 12-week
/// cash-flow projection, or an honest <see cref="IsAvailable"/> <c>false</c>
/// when there has never been a successful <see cref="AccountsReading"/>
/// (`WP 19.8B`, po-comments.md item 8, scope §3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Receivable is Tempest's own state; payable and cash are the cached
/// reading's.</b> <see cref="InvoicedTotal"/>, <see cref="OverdueTotal"/>,
/// <see cref="Due30Total"/>, <see cref="Due90Total"/> and
/// <see cref="Receivable"/> are computed fresh, every call, from every
/// <see cref="InvoiceRequest"/> whose own <see cref="InvoiceRequest.Status"/>
/// is <see cref="InvoiceRequestStatus.Sent"/> or
/// <see cref="InvoiceRequestStatus.Accepted"/> and carries an
/// <see cref="InvoiceRequest.IssuedDate"/> — this platform's own durable,
/// transactional state, the same source <c>KpiSnapshotService</c> reads.
/// Everything payable (<see cref="BillsDueWithin30"/>,
/// <see cref="SubscriptionsDueWithin60"/>, <see cref="Cash"/>) comes
/// straight from the last <see cref="AccountsReading"/> the accounting
/// package answered — never a fresh connector call from inside this read
/// (`AccountsRefreshService`'s own remarks explain why a network call has
/// no place here).
/// </para>
/// <para>
/// <b><see cref="InvoicedTotal"/> is every matching request; the three due
/// buckets are a due-date split of the same set.</b> A request due more
/// than 90 days out still counts in <see cref="InvoicedTotal"/> (the
/// Product Owner's own "Invoiced" tile — everything currently owed,
/// regardless of when) but appears in none of
/// <see cref="OverdueTotal"/>/<see cref="Due30Total"/>/<see cref="Due90Total"/>,
/// so those three do not necessarily sum to the first.
/// </para>
/// <para>
/// <b>The cash-flow series is deliberately simple, not a forecast.</b>
/// Each of the 12 weeks sums whichever bills and repeating bills'
/// <em>next</em> due date falls inside it — a repeating bill's own future
/// occurrences beyond that single next date are never projected forward,
/// matching the brief's own words, "a simple cash-flow series", not a
/// recurring-schedule simulator.
/// </para>
/// </remarks>
public sealed class AccountsSnapshot
{
    private AccountsSnapshot(
        bool isAvailable, string? unavailableReason, DateTimeOffset? unavailableSince,
        DateTimeOffset? readAt, string? connector, DateOnly asOf,
        Money invoicedTotal, Money overdueTotal, Money due30Total, Money due90Total,
        IReadOnlyList<ReceivableInvoice> overdue, IReadOnlyList<ReceivableInvoice> due30, IReadOnlyList<ReceivableInvoice> due90,
        IReadOnlyList<BillDue> billsDueWithin30, IReadOnlyList<CategorisedRepeatingBill> subscriptionsDueWithin60,
        IReadOnlyDictionary<AccountsCategory, Money> subscriptionTotalsByCategory,
        IReadOnlyList<CashAccountBalance> cash, Money totalCash, IReadOnlyList<CashFlowWeek> cashFlowSeries)
    {
        IsAvailable = isAvailable;
        UnavailableReason = unavailableReason;
        UnavailableSince = unavailableSince;
        ReadAt = readAt;
        Connector = connector;
        AsOf = asOf;
        InvoicedTotal = invoicedTotal;
        OverdueTotal = overdueTotal;
        Due30Total = due30Total;
        Due90Total = due90Total;
        Overdue = overdue;
        Due30 = due30;
        Due90 = due90;
        BillsDueWithin30 = billsDueWithin30;
        SubscriptionsDueWithin60 = subscriptionsDueWithin60;
        SubscriptionTotalsByCategory = subscriptionTotalsByCategory;
        Cash = cash;
        TotalCash = totalCash;
        CashFlowSeries = cashFlowSeries;
    }

    /// <summary><see langword="false"/> when there has never been a successful <see cref="AccountsReading"/> — every other member on this instance is a zeroed placeholder in that case; read <see cref="UnavailableReason"/>/<see cref="UnavailableSince"/> instead.</summary>
    public bool IsAvailable { get; }

    /// <summary>Why there is no reading, when <see cref="IsAvailable"/> is <see langword="false"/> — the most recent refresh failure's own reason, or "No accounts reading yet." before the very first refresh attempt.</summary>
    public string? UnavailableReason { get; }

    /// <summary>When the failure named by <see cref="UnavailableReason"/> happened, when known.</summary>
    public DateTimeOffset? UnavailableSince { get; }

    /// <summary>When the underlying <see cref="AccountsReading"/> was read, when <see cref="IsAvailable"/> is <see langword="true"/>.</summary>
    public DateTimeOffset? ReadAt { get; }

    /// <summary>Which connector produced the underlying <see cref="AccountsReading"/>, when <see cref="IsAvailable"/> is <see langword="true"/>.</summary>
    public string? Connector { get; }

    /// <summary>The date every due-date figure on this snapshot is computed relative to.</summary>
    public DateOnly AsOf { get; }

    /// <summary>The total of every matching sent/accepted invoice request, regardless of its own due date.</summary>
    public Money InvoicedTotal { get; }

    /// <summary>The total of every matching request whose own due date has already passed.</summary>
    public Money OverdueTotal { get; }

    /// <summary>The total of every matching request due within the next 30 days (not already overdue).</summary>
    public Money Due30Total { get; }

    /// <summary>The total of every matching request due more than 30, but within 90, days out.</summary>
    public Money Due90Total { get; }

    /// <summary>Every overdue receivable, individually.</summary>
    public IReadOnlyList<ReceivableInvoice> Overdue { get; }

    /// <summary>Every receivable due within 30 days, individually.</summary>
    public IReadOnlyList<ReceivableInvoice> Due30 { get; }

    /// <summary>Every receivable due within 31-90 days, individually.</summary>
    public IReadOnlyList<ReceivableInvoice> Due90 { get; }

    /// <summary>Every loaded bill due within 30 days (including any already overdue).</summary>
    public IReadOnlyList<BillDue> BillsDueWithin30 { get; }

    /// <summary>Every subscription whose own next occurrence is due within 60 days (including any already overdue), each already categorised.</summary>
    public IReadOnlyList<CategorisedRepeatingBill> SubscriptionsDueWithin60 { get; }

    /// <summary>The total of <see cref="SubscriptionsDueWithin60"/>, grouped by <see cref="AccountsCategory"/> — Hardware/Software/Premises/Other always present, zero when a category has nothing due in the window.</summary>
    public IReadOnlyDictionary<AccountsCategory, Money> SubscriptionTotalsByCategory { get; }

    /// <summary>Every bank account's own balance, individually.</summary>
    public IReadOnlyList<CashAccountBalance> Cash { get; }

    /// <summary>The sum of <see cref="Cash"/>.</summary>
    public Money TotalCash { get; }

    /// <summary>Twelve weeks of projected cash flow, starting from <see cref="AsOf"/>.</summary>
    public IReadOnlyList<CashFlowWeek> CashFlowSeries { get; }

    /// <summary>
    /// Builds the honest-unavailable state — no reading has ever been
    /// saved. Every <see cref="Money"/>-typed member is a meaningless zero
    /// in this platform's own fixture currency (GBP) — <see cref="Money"/>
    /// itself has no way to state "no currency" (its own constructor
    /// refuses an unspecified one), so this placeholder exists purely to
    /// keep every other member non-nullable; a caller must check
    /// <see cref="IsAvailable"/> before reading any of them, exactly as
    /// every other "honest unavailability" figure on this platform (for
    /// example <c>DaysSalesOutstandingResult</c>) already requires.
    /// </summary>
    public static AccountsSnapshot Unavailable(string reason, DateTimeOffset? since, DateOnly asOf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var zero = Money.Zero(CurrencyCode.Gbp);

        return new AccountsSnapshot(
            false, reason, since, null, null, asOf,
            zero, zero, zero, zero,
            [], [], [],
            [], [], EmptyCategoryTotals(CurrencyCode.Gbp),
            [], zero, []);
    }

    /// <summary>Computes a snapshot from <paramref name="reading"/> and Tempest's own <paramref name="receivable"/> list, as of <paramref name="asOf"/>.</summary>
    /// <param name="cashFlowWeeks">How many weeks the cash-flow series covers — 12 by <see cref="AccountsReadModel"/>'s own default, exposed here so a test can hand-check a shorter series.</param>
    public static AccountsSnapshot From(AccountsReading reading, IReadOnlyList<ReceivableInvoice> receivable, DateOnly asOf, int cashFlowWeeks = 12)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(receivable);

        var currency = ResolveReportingCurrency(reading, receivable);

        var overdue = receivable.Where(r => r.DueDate < asOf).ToList();
        var due30 = receivable.Where(r => r.DueDate >= asOf && r.DueDate <= asOf.AddDays(30)).ToList();
        var due90 = receivable.Where(r => r.DueDate > asOf.AddDays(30) && r.DueDate <= asOf.AddDays(90)).ToList();

        var invoicedTotal = Money.Sum(receivable.Select(r => r.Amount), currency);
        var overdueTotal = Money.Sum(overdue.Select(r => r.Amount), currency);
        var due30Total = Money.Sum(due30.Select(r => r.Amount), currency);
        var due90Total = Money.Sum(due90.Select(r => r.Amount), currency);

        var billsDueWithin30 = reading.Bills.Where(b => b.Due <= asOf.AddDays(30)).ToList();
        var subscriptionsDueWithin60 = reading.RepeatingBills.Where(b => b.Bill.NextDue <= asOf.AddDays(60)).ToList();

        var totalsByCategory = EmptyCategoryTotals(currency);
        foreach (var subscription in subscriptionsDueWithin60)
            totalsByCategory[subscription.Category] = totalsByCategory[subscription.Category] + subscription.Bill.Amount;

        var totalCash = Money.Sum(reading.Cash.Select(c => c.Balance), currency);

        var cashFlowSeries = BuildCashFlowSeries(reading, receivable, asOf, cashFlowWeeks, currency, totalCash);

        return new AccountsSnapshot(
            true, null, null, reading.ReadAt, reading.Connector, asOf,
            invoicedTotal, overdueTotal, due30Total, due90Total,
            overdue, due30, due90,
            billsDueWithin30, subscriptionsDueWithin60, totalsByCategory,
            reading.Cash, totalCash, cashFlowSeries);
    }

    private static IReadOnlyList<CashFlowWeek> BuildCashFlowSeries(
        AccountsReading reading, IReadOnlyList<ReceivableInvoice> receivable, DateOnly asOf, int weeks, CurrencyCode currency, Money openingCash)
    {
        var series = new List<CashFlowWeek>(weeks);
        var running = openingCash;

        for (var week = 0; week < weeks; week++)
        {
            var weekStart = asOf.AddDays(7 * week);
            var weekEnd = weekStart.AddDays(6);

            var receivableIn = Money.Sum(receivable.Where(r => r.DueDate >= weekStart && r.DueDate <= weekEnd).Select(r => r.Amount), currency);

            var billsOut = reading.Bills.Where(b => b.Due >= weekStart && b.Due <= weekEnd).Select(b => b.Amount);
            var subscriptionsOut = reading.RepeatingBills.Where(b => b.Bill.NextDue >= weekStart && b.Bill.NextDue <= weekEnd).Select(b => b.Bill.Amount);
            var payableOut = Money.Sum(billsOut.Concat(subscriptionsOut), currency);

            running = running + receivableIn - payableOut;
            series.Add(new CashFlowWeek(weekStart, receivableIn, payableOut, running));
        }

        return series;
    }

    /// <summary>
    /// The single currency every total on this snapshot is stated in —
    /// taken from the cash position, then the bills, then the repeating
    /// bills, then the receivable list, whichever is non-empty first; GBP
    /// (this platform's own fixture currency throughout) when the reading
    /// and the receivable list are both empty. A live reading mixing
    /// currencies throws <see cref="CurrencyMismatchException"/> from
    /// <see cref="Money.Sum"/> exactly as combining £100 with €100 anywhere
    /// else in this platform does — refused, not silently converted
    /// (`Money`'s own remarks); multi-currency accounts aggregation is not
    /// in this Work Package's own scope.
    /// </summary>
    private static CurrencyCode ResolveReportingCurrency(AccountsReading reading, IReadOnlyList<ReceivableInvoice> receivable) =>
        reading.Cash.Select(c => c.Balance.Currency)
            .Concat(reading.Bills.Select(b => b.Amount.Currency))
            .Concat(reading.RepeatingBills.Select(b => b.Bill.Amount.Currency))
            .Concat(receivable.Select(r => r.Amount.Currency))
            .DefaultIfEmpty(CurrencyCode.Gbp)
            .First();

    private static Dictionary<AccountsCategory, Money> EmptyCategoryTotals(CurrencyCode currency) => new()
    {
        [AccountsCategory.Hardware] = Money.Zero(currency),
        [AccountsCategory.Software] = Money.Zero(currency),
        [AccountsCategory.Premises] = Money.Zero(currency),
        [AccountsCategory.Other] = Money.Zero(currency),
    };
}

/// <summary>The read model the Business dashboard (`WP 19.7B`) will call (`WP 19.8B`, scope §3).</summary>
public interface IAccountsReadModel
{
    /// <summary>Reads the current <see cref="AccountsSnapshot"/>.</summary>
    Task<AccountsSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>The only <see cref="IAccountsReadModel"/> implementation (`WP 19.8B`, scope §3), and — since `v0.24.0` X7 — the only <see cref="IForwardCashReadModel"/> one too.</summary>
public sealed class AccountsReadModel : IAccountsReadModel, IForwardCashReadModel
{
    /// <summary>The Kind string of a milestone — <c>Tempest.Workspace.CanonicalObjectKinds.Milestone</c>'s own value, repeated because <c>Tempest.Core</c> cannot reference <c>Tempest.Workspace</c> (as <c>QuotationService</c> does).</summary>
    private const string MilestoneKind = "Milestone";

    /// <summary>The Kind string of a deliverable — <c>Tempest.Workspace.CanonicalObjectKinds.Deliverable</c>'s own value, repeated for the same reason.</summary>
    private const string DeliverableKind = "Deliverable";

    /// <summary>The name the Xero connector records on a request it sent (<c>XeroConnector.Name</c>) — only those requests have a Xero badge to ask about.</summary>
    private const string XeroConnectorName = "Xero";

    private readonly IAccountsReadingStore _store;
    private readonly EngineeringDomainContext _context;
    private readonly AccountsRefreshService? _refreshService;
    private readonly TimeProvider _time;
    private readonly IXeroSyncService? _xeroSync;

    /// <summary>Initialises a new instance of the <see cref="AccountsReadModel"/> class.</summary>
    /// <param name="refreshService">Where the reason and time of the most recent failed refresh come from, for the <see cref="AccountsSnapshot.Unavailable"/> case — the same singleton <see cref="AccountsRefreshService"/> the hosted-service manager starts and stops. <see langword="null"/> is honoured (a test exercising this read model alone need not stand one up); the "no reading yet" reason is used regardless.</param>
    /// <remarks>
    /// No longer takes an <see cref="Tempest.Core.Configuration.IConfigurationProvider"/>
    /// (`WP 20.1B`, `TD-180`): due date used to be one configured
    /// days-after-issue guess every client shared (<c>Accounts:PaymentTermsDays</c>,
    /// default 30); it now reads each request's own frozen
    /// <see cref="InvoiceRequest.DueOn"/>, so there is nothing left here to
    /// configure.
    /// </remarks>
    /// <param name="store">Where the last accounts reading is kept.</param>
    /// <param name="context">The engineering domain, for TempestOS's own invoice requests, quotations, deliverables and milestones.</param>
    /// <param name="timeProvider">The clock "today" is read from; <see langword="null"/> for the system clock.</param>
    /// <param name="xeroSync">
    /// `v0.24.0` X7: the Xero sync engine (X6), asked — never over the network —
    /// for each sent invoice's badge, so the forward cash picture knows when
    /// its status was last read back from Xero and leaves out one Xero
    /// already shows paid or voided. <see langword="null"/> (no Xero
    /// connector) is honoured: each invoice request's own status, as last
    /// reconciled, is used alone.
    /// </param>
    public AccountsReadModel(
        IAccountsReadingStore store, EngineeringDomainContext context,
        AccountsRefreshService? refreshService = null, TimeProvider? timeProvider = null, IXeroSyncService? xeroSync = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(context);

        _store = store;
        _context = context;
        _refreshService = refreshService;
        _time = timeProvider ?? TimeProvider.System;
        _xeroSync = xeroSync;
    }

    /// <inheritdoc />
    public async Task<AccountsSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var asOf = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var reading = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (reading is null)
        {
            var reason = _refreshService?.LastFailureReason ?? "No accounts reading yet.";
            var since = _refreshService?.LastFailureAtUtc;

            return AccountsSnapshot.Unavailable(reason, since, asOf);
        }

        // `TD-88`/`WP 21.5B`: `Status`/`IssuedDate`/`DueOn`/`Total` are all
        // `InvoiceRequest`-own fields, not on the index row, so every
        // request of this Kind is materialised before it can be filtered.
        var entries = await _context.Repository.ListByKindAsync(InvoiceRequest.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var all = await _context.Repository.MaterialiseAsync<InvoiceRequest>(entries, cancellationToken).ConfigureAwait(false);

        // `TD-180`, `WP 20.1B`: due date is the request's own `DueOn` — its
        // client's own payment terms, frozen at raise — not a single
        // configured days-after-issue guess every client shared alike.
        var receivable = all
            .Where(r => r.Status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted && r.IssuedDate is not null)
            .Select(r => new ReceivableInvoice(r.Id, r.ClientOrganisationId, r.IssuedDate!.Value, r.DueOn ?? r.IssuedDate!.Value, r.Total))
            .ToList();

        return AccountsSnapshot.From(reading, receivable, asOf);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Never a network call: the accounting package's figures come from the
    /// last saved <see cref="AccountsReading"/> (so offline shows the last
    /// reading, with when it was read), invoice statuses from what the
    /// read-back last recorded, and the pipeline from TempestOS's own
    /// records. <see cref="ForwardCashProjection"/> does the arithmetic.
    /// </remarks>
    public async Task<ForwardCashPicture> ReadForwardCashAsync(CancellationToken cancellationToken = default)
    {
        var asOf = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var reading = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);

        var requestEntries = await _context.Repository.ListByKindAsync(InvoiceRequest.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var requests = (await _context.Repository.MaterialiseAsync<InvoiceRequest>(requestEntries, cancellationToken).ConfigureAwait(false))
            .Where(IsLive)
            .ToList();

        var invoices = await ReadInvoicesDueAsync(requests, asOf, cancellationToken).ConfigureAwait(false);
        var (pipeline, exclusions) = await ReadPipelineAsync(requests, cancellationToken).ConfigureAwait(false);

        return ForwardCashProjection.Build(new ForwardCashInputs(asOf, reading, invoices, pipeline)
        {
            ReadingUnavailableReason = reading is null ? _refreshService?.LastFailureReason ?? "No accounts reading yet." : null,
            RefreshFailureReason = reading is null ? null : _refreshService?.LastFailureReason,
            RefreshFailedAtUtc = reading is null ? null : _refreshService?.LastFailureAtUtc,
            Exclusions = exclusions,
        });
    }

    /// <summary>
    /// Every sales invoice TempestOS raised that the accounting package holds
    /// and has not been paid: a request <see cref="InvoiceRequestStatus.Sent"/>
    /// (in Xero as a draft, not yet approved) or
    /// <see cref="InvoiceRequestStatus.Accepted"/> (approved, awaiting
    /// payment), with no <see cref="InvoiceRequest.PaidDate"/> read back and
    /// not shown paid, voided or deleted there. Gross: the request's own
    /// <see cref="InvoiceRequest.GrossTotal"/> (net plus each line's VAT),
    /// which is what the client is asked to pay.
    /// </summary>
    private async Task<IReadOnlyList<ForwardCashInvoice>> ReadInvoicesDueAsync(
        IReadOnlyList<InvoiceRequest> requests, DateOnly asOf, CancellationToken cancellationToken)
    {
        var invoices = new List<ForwardCashInvoice>();

        foreach (var request in requests)
        {
            if (request.Status is not (InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted) || request.PaidDate is not null)
                continue;

            if (request.ExternalStatus is { } external && ForwardCashProjection.IsSettledStatus(external))
                continue;

            XeroSyncStatus? status = null;
            if (_xeroSync is not null && string.Equals(request.Connector, XeroConnectorName, StringComparison.Ordinal))
            {
                status = await _xeroSync.GetStatusAsync(XeroDocumentRef.For(XeroDocumentKind.Invoice, request.Id), cancellationToken).ConfigureAwait(false);
                if (status.Badge is XeroSyncBadge.Paid or XeroSyncBadge.Voided)
                    continue;
            }

            var connector = request.Connector ?? "Accounting package";
            var number = request.ExternalInvoiceNumber ?? request.Identifier ?? request.DisplayName;
            var awaitingApproval = request.Status == InvoiceRequestStatus.Sent;

            invoices.Add(new ForwardCashInvoice(
                request.Id,
                $"Invoice {number} — client {request.ClientOrganisationId}",
                request.DueOn ?? request.IssuedDate ?? asOf,
                request.GrossTotal,
                new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, $"{connector} invoice", status?.AsOfUtc),
                awaitingApproval));
        }

        return invoices;
    }

    /// <summary>
    /// The accepted-but-not-invoiced pipeline: every fixed-price line of a
    /// live <see cref="QuotationStatus.Accepted"/> quotation whose deliverable
    /// is still live and has not been invoiced into the accounting package —
    /// no completion of it billed (<see cref="DeliverableCompletion.InvoicedBy"/>)
    /// or carried by a request the package already holds (sent, accepted, or
    /// carrying the package's own id). Dated by the deliverable's completion
    /// (completed, so ready to invoice now) or else its milestone's target
    /// date; with neither, undated. An hourly line is left out and reported:
    /// time is billed from timesheets as it is booked, so its quoted value
    /// would double-count invoices already raised from that time.
    /// </summary>
    private async Task<(IReadOnlyList<ForwardCashPipelineLine> Lines, IReadOnlyList<ForwardCashExclusion> Exclusions)> ReadPipelineAsync(
        IReadOnlyList<InvoiceRequest> requests, CancellationToken cancellationToken)
    {
        var repository = _context.Repository;

        var quotations = (await repository.MaterialiseAsync<Quotation>(
                await repository.ListByKindAsync(Quotation.CanonicalKind, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
            .Where(q => IsLive(q) && q.Status == QuotationStatus.Accepted)
            .ToList();

        if (quotations.Count == 0)
            return ([], []);

        var deliverables = (await repository.MaterialiseAsync<Deliverable>(
                await repository.ListByKindAsync(DeliverableKind, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
            .Where(IsLive)
            .ToDictionary(d => d.Id);

        var milestones = (await repository.MaterialiseAsync<Milestone>(
                await repository.ListByKindAsync(MilestoneKind, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
            .Where(IsLive)
            .ToDictionary(m => m.Id);

        var completionsByDeliverable = (await repository.MaterialiseAsync<DeliverableCompletion>(
                await repository.ListByKindAsync(DeliverableCompletion.CanonicalKind, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
            .Where(IsLive)
            .GroupBy(c => c.DeliverableId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // A completion is "in the accounting package" once a request carrying
        // it is there: sent or accepted, or holding the package's own id.
        var inPackage = new HashSet<Guid>();
        var draftedOnly = new HashSet<Guid>();
        foreach (var request in requests)
        {
            var heldByPackage = request.ExternalId is not null || request.Status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted;
            var liveDraft = request.Status is InvoiceRequestStatus.Draft or InvoiceRequestStatus.Sending or InvoiceRequestStatus.Unknown or InvoiceRequestStatus.Reauthorise;

            foreach (var line in request.Lines.Where(l => string.Equals(l.SourceKind, DeliverableCompletion.CanonicalKind, StringComparison.Ordinal)))
            {
                if (heldByPackage)
                    inPackage.Add(line.SourceId);
                else if (liveDraft)
                    draftedOnly.Add(line.SourceId);
            }
        }

        var lines = new List<ForwardCashPipelineLine>();
        var hourly = new List<Money>();

        foreach (var quote in quotations)
        {
            foreach (var line in quote.Lines)
            {
                if (line.Basis == QuotationLineBasis.Hourly)
                {
                    hourly.Add(line.GrossAmount);
                    continue;
                }

                var description = $"{quote.Reference}: {line.Description}";

                if (line.DeliverableId is not { } deliverableId)
                {
                    lines.Add(new ForwardCashPipelineLine(quote.Id, description, null, "no deliverable recorded", line.GrossAmount));
                    continue;
                }

                // A deliverable since deleted is no longer planned work.
                if (!deliverables.TryGetValue(deliverableId, out var deliverable))
                    continue;

                var completions = completionsByDeliverable.TryGetValue(deliverableId, out var found) ? found : [];
                if (completions.Any(c => c.InvoicedBy is not null || inPackage.Contains(c.Id)))
                    continue;

                if (completions.Count > 0)
                {
                    var completion = completions.MaxBy(c => c.CompletedOn)!;
                    var basis = draftedOnly.Contains(completion.Id)
                        ? $"completed {Iso(completion.CompletedOn)}; invoice drafted in TempestOS, not yet in the accounting package"
                        : $"completed {Iso(completion.CompletedOn)}; not yet invoiced";
                    lines.Add(new ForwardCashPipelineLine(quote.Id, description, completion.CompletedOn, basis, line.GrossAmount));
                    continue;
                }

                if (milestones.TryGetValue(deliverable.MilestoneId, out var milestone))
                {
                    var target = DateOnly.FromDateTime(milestone.TargetDate.UtcDateTime);
                    lines.Add(new ForwardCashPipelineLine(
                        quote.Id, description, target, $"milestone '{milestone.DisplayName}' target date {Iso(target)}", line.GrossAmount));
                    continue;
                }

                lines.Add(new ForwardCashPipelineLine(quote.Id, description, null, "no planned or target date", line.GrossAmount));
            }
        }

        var exclusions = new List<ForwardCashExclusion>();
        foreach (var group in hourly.GroupBy(m => m.Currency))
        {
            exclusions.Add(new ForwardCashExclusion(
                "Hourly lines on accepted quotes — billed from timesheets as time is booked, so not projected here",
                group.Count(), Money.Sum(group, group.Key)));
        }

        return (lines, exclusions);
    }

    private static bool IsLive(IEngineeringObject item) => item is not IDeletable { IsDeleted: true };

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

// ============================================================================
// `v0.24.0` X7 — the forward cash picture: the accounting package's actuals
// (bank balances, bills, repeating bills, sales invoices awaiting payment)
// combined with TempestOS's accepted-but-not-invoiced pipeline, month by
// month for the next six months. Quotes never touch the package's cash (D2):
// only an accepted quote's work, not yet invoiced, is projected — and only
// here, as TempestOS's own expectation, labelled as such.
// ============================================================================

/// <summary>The read the Business dashboard's forward cash panel calls (`v0.24.0` X7).</summary>
public interface IForwardCashReadModel
{
    /// <summary>Reads the forward cash picture as of today — never a network call.</summary>
    Task<ForwardCashPicture> ReadForwardCashAsync(CancellationToken cancellationToken = default);
}

/// <summary>Where a forward cash figure comes from.</summary>
public enum ForwardCashSourceKind
{
    /// <summary>Read from the accounting package (Xero) — when, is stated.</summary>
    AccountingPackage,

    /// <summary>TempestOS's own records — accepted quotes not yet invoiced.</summary>
    TempestOS,

    /// <summary>Worked out from other figures in the same month (money in, money out, opening and closing cash).</summary>
    Derived,
}

/// <summary>Where one forward cash figure or item comes from, and when it was read.</summary>
/// <param name="Kind">The kind of source.</param>
/// <param name="Label">What the source is, in words (for example "Xero bank balances").</param>
/// <param name="ReadAt">When the accounting package was read for it; <see langword="null"/> for TempestOS's own figures, derived figures, or a package figure whose read time is not known.</param>
public sealed record ForwardCashSource(ForwardCashSourceKind Kind, string Label, DateTimeOffset? ReadAt = null)
{
    /// <summary>TempestOS's accepted-but-not-invoiced pipeline.</summary>
    public static ForwardCashSource Pipeline { get; } = new(ForwardCashSourceKind.TempestOS, "TempestOS — accepted quotes not yet invoiced");

    /// <summary>A figure worked out from the others in its month.</summary>
    public static ForwardCashSource Derived { get; } = new(ForwardCashSourceKind.Derived, "Worked out from the figures above");

    /// <summary>The source in words, with the read time when known: "Xero bank balances, read at 2026-10-02 09:00 UTC".</summary>
    public string Describe() => ReadAt is { } readAt
        ? $"{Label}, read at {readAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC"
        : Label;
}

/// <summary>
/// One figure of the forward cash picture: an amount, or — when its source
/// has no reading — <see cref="IsAvailable"/> <see langword="false"/> with the
/// reason, never a zero that would read as "nothing due".
/// </summary>
/// <param name="Amount">The amount, gross (including VAT); <see langword="null"/> when unavailable.</param>
/// <param name="Source">Where the figure comes from.</param>
/// <param name="UnavailableReason">Why the figure is unavailable; <see langword="null"/> when it is available.</param>
public sealed record ForwardCashFigure(Money? Amount, ForwardCashSource Source, string? UnavailableReason = null)
{
    /// <summary>Whether <see cref="Amount"/> is known.</summary>
    public bool IsAvailable => Amount is not null;

    /// <summary>An unavailable figure from <paramref name="source"/>, for <paramref name="reason"/>.</summary>
    public static ForwardCashFigure Unavailable(ForwardCashSource source, string reason) => new(null, source, reason);
}

/// <summary>What one forward cash item is.</summary>
public enum ForwardCashItemKind
{
    /// <summary>A sales invoice in the accounting package, not yet paid (money in, on its due date).</summary>
    InvoiceDue,

    /// <summary>An accepted quote's line not yet invoiced (money in, expected — TempestOS's own figure).</summary>
    ExpectedMilestone,

    /// <summary>An approved bill in the accounting package (money out, on its due date).</summary>
    BillDue,

    /// <summary>One occurrence of a repeating bill (money out).</summary>
    RepeatingBill,
}

/// <summary>One dated amount behind a forward cash month's figures.</summary>
/// <param name="Kind">What the item is.</param>
/// <param name="Description">The item in words (number, supplier or client, quote reference and line).</param>
/// <param name="Date">The date it is expected (due date, milestone target, completion); <see langword="null"/> for an undated item.</param>
/// <param name="Gross">The amount, gross (including VAT).</param>
/// <param name="Source">Where it comes from.</param>
/// <param name="ObjectId">The TempestOS record it opens (an invoice request or a quotation); <see langword="null"/> for an accounting-package-only item.</param>
/// <param name="ObjectKind">The Kind of <paramref name="ObjectId"/>.</param>
/// <param name="Note">How it was dated or why it is where it is (overdue, draft, projected repeat); <see langword="null"/> when nothing needs saying.</param>
public sealed record ForwardCashItem(
    ForwardCashItemKind Kind, string Description, DateOnly? Date, Money Gross, ForwardCashSource Source,
    Guid? ObjectId = null, string? ObjectKind = null, string? Note = null);

/// <summary>Something the projection deliberately left out, with how much.</summary>
/// <param name="Reason">Why it is left out, in words.</param>
/// <param name="Count">How many items.</param>
/// <param name="Total">Their total, gross, when they share one currency; <see langword="null"/> otherwise.</param>
public sealed record ForwardCashExclusion(string Reason, int Count, Money? Total);

/// <summary>One calendar month of the forward cash picture.</summary>
/// <param name="MonthStart">The first day of the calendar month.</param>
/// <param name="From">The first day counted — today for the current month, otherwise <paramref name="MonthStart"/>.</param>
/// <param name="To">The last day of the month.</param>
/// <param name="OpeningCash">Bank balances (first month) or the previous month's closing cash.</param>
/// <param name="InvoicesDue">Sales invoices in the accounting package due this month (the first month also carries any overdue).</param>
/// <param name="ExpectedMilestones">Accepted-but-not-invoiced work expected this month (the first month also carries any whose date has passed).</param>
/// <param name="MoneyIn"><paramref name="InvoicesDue"/> plus <paramref name="ExpectedMilestones"/>.</param>
/// <param name="BillsDue">Approved bills due this month (the first month also carries any overdue).</param>
/// <param name="RepeatingBills">Repeating bill occurrences this month.</param>
/// <param name="MoneyOut"><paramref name="BillsDue"/> plus <paramref name="RepeatingBills"/>.</param>
/// <param name="ClosingCash"><paramref name="OpeningCash"/> plus <paramref name="MoneyIn"/> minus <paramref name="MoneyOut"/>.</param>
/// <param name="Items">Every item behind the figures, by date.</param>
public sealed record ForwardCashMonth(
    DateOnly MonthStart, DateOnly From, DateOnly To,
    ForwardCashFigure OpeningCash,
    ForwardCashFigure InvoicesDue, ForwardCashFigure ExpectedMilestones, ForwardCashFigure MoneyIn,
    ForwardCashFigure BillsDue, ForwardCashFigure RepeatingBills, ForwardCashFigure MoneyOut,
    ForwardCashFigure ClosingCash,
    IReadOnlyList<ForwardCashItem> Items);

/// <summary>
/// The forward cash picture (`v0.24.0` X7): month by month for the next six
/// calendar months, opening cash, money in, money out and closing cash, each
/// figure stating its source; plus the undated pipeline and anything left
/// out. Built by <see cref="ForwardCashProjection.Build"/>.
/// </summary>
/// <param name="AsOf">The day the picture is worked out from.</param>
/// <param name="Currency">The one currency every figure is stated in.</param>
/// <param name="Months">The months, earliest first.</param>
/// <param name="Undated">Accepted-but-not-invoiced work with no date to place it — in no month, and in no total.</param>
/// <param name="Later">Dated items after the last month — in no month's total.</param>
/// <param name="Exclusions">Anything else left out, and why.</param>
/// <param name="AccountsReadAt">When the accounting package's figures (bank, bills, repeating bills) were read; <see langword="null"/> with no reading.</param>
/// <param name="AccountsConnector">Which accounting package they were read from; <see langword="null"/> with no reading.</param>
/// <param name="AccountsUnavailableReason">Why there is no reading; <see langword="null"/> when there is one.</param>
/// <param name="RefreshFailureReason">With a reading: why the latest attempt to refresh it failed (offline, say), so the figures are the last reading's; <see langword="null"/> when the latest attempt succeeded.</param>
/// <param name="RefreshFailedAtUtc">When that refresh failed.</param>
public sealed record ForwardCashPicture(
    DateOnly AsOf, CurrencyCode Currency, IReadOnlyList<ForwardCashMonth> Months,
    IReadOnlyList<ForwardCashItem> Undated, IReadOnlyList<ForwardCashItem> Later, IReadOnlyList<ForwardCashExclusion> Exclusions,
    DateTimeOffset? AccountsReadAt, string? AccountsConnector, string? AccountsUnavailableReason,
    string? RefreshFailureReason = null, DateTimeOffset? RefreshFailedAtUtc = null)
{
    /// <summary>How VAT is treated — shown with the picture.</summary>
    public string VatBasis => ForwardCashProjection.VatBasis;

    /// <summary>The total of <see cref="Undated"/>, when there is any.</summary>
    public Money UndatedTotal => Money.Sum(Undated.Select(i => i.Gross), Currency);
}

/// <summary>A sales invoice the accounting package holds, not yet paid — one input to <see cref="ForwardCashProjection"/>.</summary>
/// <param name="RequestId">The TempestOS invoice request.</param>
/// <param name="Description">The invoice in words.</param>
/// <param name="DueDate">When it is due.</param>
/// <param name="Gross">What the client is asked to pay, including VAT.</param>
/// <param name="Source">Where its status was read, and when.</param>
/// <param name="AwaitingApproval">Whether the package still holds it as a draft (not yet approved and sent by the Product Owner).</param>
public sealed record ForwardCashInvoice(Guid RequestId, string Description, DateOnly DueDate, Money Gross, ForwardCashSource Source, bool AwaitingApproval);

/// <summary>One accepted quote line not yet invoiced — one input to <see cref="ForwardCashProjection"/>.</summary>
/// <param name="QuotationId">The accepted quotation.</param>
/// <param name="Description">The quote reference and line, in words.</param>
/// <param name="ExpectedDate">When it is expected to be invoiced; <see langword="null"/> when undated.</param>
/// <param name="DateBasis">How <paramref name="ExpectedDate"/> was arrived at (or why there is none), in words.</param>
/// <param name="Gross">The line's own value, gross: its net amount plus its VAT rate.</param>
public sealed record ForwardCashPipelineLine(Guid QuotationId, string Description, DateOnly? ExpectedDate, string DateBasis, Money Gross);

/// <summary>Everything <see cref="ForwardCashProjection.Build"/> needs.</summary>
/// <param name="AsOf">Today.</param>
/// <param name="Reading">The last accounts reading; <see langword="null"/> when there has never been one.</param>
/// <param name="Invoices">Sales invoices in the accounting package awaiting payment.</param>
/// <param name="Pipeline">Accepted quote lines not yet invoiced.</param>
/// <param name="Months">How many calendar months, counting the current one.</param>
public sealed record ForwardCashInputs(
    DateOnly AsOf, AccountsReading? Reading, IReadOnlyList<ForwardCashInvoice> Invoices, IReadOnlyList<ForwardCashPipelineLine> Pipeline,
    int Months = ForwardCashProjection.DefaultMonths)
{
    /// <summary>Why there is no reading, when <see cref="Reading"/> is <see langword="null"/>.</summary>
    public string? ReadingUnavailableReason { get; init; }

    /// <summary>With a reading: why the latest refresh failed.</summary>
    public string? RefreshFailureReason { get; init; }

    /// <summary>With a reading: when the latest refresh failed.</summary>
    public DateTimeOffset? RefreshFailedAtUtc { get; init; }

    /// <summary>What the reader already left out, and why.</summary>
    public IReadOnlyList<ForwardCashExclusion> Exclusions { get; init; } = [];
}

/// <summary>
/// The forward cash arithmetic (`v0.24.0` X7) — pure, so every figure can be
/// hand-checked.
/// </summary>
/// <remarks>
/// <para>
/// <b>Months.</b> Month 1 runs from today to the end of the current calendar
/// month; months 2–6 are whole calendar months. Opening cash for month 1 is
/// the sum of the bank balances last read; every later month opens on the
/// previous month's closing cash. Closing = opening + money in − money out.
/// </para>
/// <para>
/// <b>Dates.</b> An item is counted in the month its date falls in. One dated
/// before today — an overdue invoice or bill, a milestone whose target has
/// passed or a deliverable completed and not invoiced — is counted in month 1,
/// with a note saying so. One dated after month 6 is listed under
/// <see cref="ForwardCashPicture.Later"/>; an undated pipeline line under
/// <see cref="ForwardCashPicture.Undated"/>; neither is in any month's total.
/// </para>
/// <para>
/// <b>Repeating bills.</b> The next occurrence is the accounting package's
/// own; later ones are projected from its schedule word — <c>WEEKLY</c>,
/// <c>FORTNIGHTLY</c>, <c>MONTHLY</c>, <c>QUARTERLY</c>, <c>YEARLY</c>/<c>ANNUALLY</c>,
/// <c>DAILY</c> — assuming every one period (the reading records the
/// schedule's unit, not its period or end date), and noted as projected. A
/// schedule word not understood is counted once, at its next occurrence.
/// </para>
/// <para>
/// <b>Honesty.</b> With no accounts reading, opening cash, bills, repeating
/// bills, money out and every closing/opening after are unavailable with the
/// reason, never zero; invoices and the pipeline, which are TempestOS's own
/// records, still show. Quotes never touch the package's cash: only an
/// accepted quote's not-yet-invoiced work is projected, labelled TempestOS.
/// </para>
/// <para>
/// <b>Currency.</b> Every figure is in one currency — the bank balances',
/// then the bills', repeating bills', invoices', pipeline's, else GBP. An
/// item in any other currency is left out and listed in
/// <see cref="ForwardCashPicture.Exclusions"/>, never converted.
/// </para>
/// </remarks>
public static class ForwardCashProjection
{
    /// <summary>How many calendar months the picture covers by default, counting the current one.</summary>
    public const int DefaultMonths = 6;

    /// <summary>The VAT basis every figure is stated on.</summary>
    public const string VatBasis =
        "All amounts are gross, including VAT: bills and repeating bills at the totals Xero states (tax included); "
        + "invoices and expected milestones at their net amount plus each line's VAT rate. "
        + "VAT owed to or reclaimed from HMRC is not projected.";

    /// <summary>Whether <paramref name="externalStatus"/> (the accounting package's own word) means the invoice needs no further payment: paid, voided or deleted.</summary>
    public static bool IsSettledStatus(string externalStatus) =>
        externalStatus.Equals("PAID", StringComparison.OrdinalIgnoreCase)
        || externalStatus.Equals("VOIDED", StringComparison.OrdinalIgnoreCase)
        || externalStatus.Equals("DELETED", StringComparison.OrdinalIgnoreCase);

    /// <summary>Works out the forward cash picture from <paramref name="inputs"/>.</summary>
    public static ForwardCashPicture Build(ForwardCashInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentOutOfRangeException.ThrowIfLessThan(inputs.Months, 1);

        var asOf = inputs.AsOf;
        var reading = inputs.Reading;
        var currency = ResolveCurrency(inputs);
        var firstMonth = new DateOnly(asOf.Year, asOf.Month, 1);
        var horizonEnd = firstMonth.AddMonths(inputs.Months).AddDays(-1);

        var items = new List<ForwardCashItem>();
        var undated = new List<ForwardCashItem>();

        foreach (var invoice in inputs.Invoices)
        {
            var note = invoice.AwaitingApproval ? "draft in the accounting package — not yet approved" : "approved — awaiting payment";
            if (invoice.DueDate < asOf)
                note += $"; overdue (due {Iso(invoice.DueDate)})";
            items.Add(new ForwardCashItem(
                ForwardCashItemKind.InvoiceDue, invoice.Description, invoice.DueDate, invoice.Gross, invoice.Source,
                invoice.RequestId, InvoiceRequest.CanonicalKind, note));
        }

        foreach (var line in inputs.Pipeline)
        {
            var note = line.ExpectedDate is { } expected && expected < asOf ? $"{line.DateBasis} — date passed, counted now" : line.DateBasis;
            var item = new ForwardCashItem(
                ForwardCashItemKind.ExpectedMilestone, line.Description, line.ExpectedDate, line.Gross, ForwardCashSource.Pipeline,
                line.QuotationId, Quotation.CanonicalKind, line.ExpectedDate is null ? $"undated — {line.DateBasis}" : note);
            if (line.ExpectedDate is null)
                undated.Add(item);
            else
                items.Add(item);
        }

        ForwardCashSource? cashSource = null, billsSource = null, repeatingSource = null;
        if (reading is not null)
        {
            cashSource = new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, $"{reading.Connector} bank balances", reading.ReadAt);
            billsSource = new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, $"{reading.Connector} bills", reading.ReadAt);
            repeatingSource = new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, $"{reading.Connector} repeating bills", reading.ReadAt);

            foreach (var bill in reading.Bills)
            {
                var note = bill.Due < asOf ? $"overdue (due {Iso(bill.Due)})" : null;
                items.Add(new ForwardCashItem(
                    ForwardCashItemKind.BillDue, $"{bill.Supplier} ({bill.Reference})", bill.Due, bill.Amount, billsSource, Note: note));
            }

            foreach (var repeating in reading.RepeatingBills)
                items.AddRange(Occurrences(repeating.Bill, asOf, horizonEnd, repeatingSource));
        }

        // One currency throughout: anything else is left out, never converted.
        var exclusions = new List<ForwardCashExclusion>(inputs.Exclusions);
        var foreign = items.Concat(undated).Where(i => i.Gross.Currency != currency).ToList();
        if (reading is not null)
        {
            var foreignCash = reading.Cash.Where(c => c.Balance.Currency != currency).ToList();
            if (foreignCash.Count > 0)
                exclusions.Add(new ForwardCashExclusion($"Bank balances not in {currency} — not converted", foreignCash.Count, SameCurrencyTotal(foreignCash.Select(c => c.Balance))));
        }

        foreach (var group in foreign.GroupBy(i => i.Gross.Currency))
            exclusions.Add(new ForwardCashExclusion($"Figures in {group.Key}, not {currency} — not converted", group.Count(), Money.Sum(group.Select(i => i.Gross), group.Key)));

        items.RemoveAll(i => i.Gross.Currency != currency);
        undated.RemoveAll(i => i.Gross.Currency != currency);

        var later = items.Where(i => i.Date > horizonEnd).OrderBy(i => i.Date).ToList();

        var months = new List<ForwardCashMonth>(inputs.Months);
        ForwardCashFigure opening = reading is null
            ? ForwardCashFigure.Unavailable(new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, "Bank balances"), Unavailable(inputs))
            : new ForwardCashFigure(Money.Sum(reading.Cash.Where(c => c.Balance.Currency == currency).Select(c => c.Balance), currency), cashSource!);

        for (var index = 0; index < inputs.Months; index++)
        {
            var monthStart = firstMonth.AddMonths(index);
            var from = index == 0 ? asOf : monthStart;
            var to = monthStart.AddMonths(1).AddDays(-1);

            var inMonth = items
                .Where(i => i.Date is { } date && (index == 0 ? date <= to : date >= monthStart && date <= to))
                .OrderBy(i => i.Date)
                .ThenBy(i => i.Kind)
                .ToList();

            var invoicesDue = new ForwardCashFigure(SumOf(inMonth, ForwardCashItemKind.InvoiceDue, currency), InvoiceSource(inMonth, inputs.Invoices, reading));
            var milestones = new ForwardCashFigure(SumOf(inMonth, ForwardCashItemKind.ExpectedMilestone, currency), ForwardCashSource.Pipeline);
            var moneyIn = new ForwardCashFigure(invoicesDue.Amount!.Value + milestones.Amount!.Value, ForwardCashSource.Derived);

            ForwardCashFigure billsDue, repeatingBills, moneyOut;
            if (reading is null)
            {
                var reason = Unavailable(inputs);
                billsDue = ForwardCashFigure.Unavailable(new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, "Bills"), reason);
                repeatingBills = ForwardCashFigure.Unavailable(new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, "Repeating bills"), reason);
                moneyOut = ForwardCashFigure.Unavailable(ForwardCashSource.Derived, reason);
            }
            else
            {
                billsDue = new ForwardCashFigure(SumOf(inMonth, ForwardCashItemKind.BillDue, currency), billsSource!);
                repeatingBills = new ForwardCashFigure(SumOf(inMonth, ForwardCashItemKind.RepeatingBill, currency), repeatingSource!);
                moneyOut = new ForwardCashFigure(billsDue.Amount!.Value + repeatingBills.Amount!.Value, ForwardCashSource.Derived);
            }

            var closing = opening.Amount is { } open && moneyOut.Amount is { } spent
                ? new ForwardCashFigure(open + moneyIn.Amount!.Value - spent, ForwardCashSource.Derived)
                : ForwardCashFigure.Unavailable(ForwardCashSource.Derived, opening.UnavailableReason ?? moneyOut.UnavailableReason ?? Unavailable(inputs));

            months.Add(new ForwardCashMonth(monthStart, from, to, opening, invoicesDue, milestones, moneyIn, billsDue, repeatingBills, moneyOut, closing, inMonth));

            opening = closing.IsAvailable
                ? new ForwardCashFigure(closing.Amount, ForwardCashSource.Derived)
                : ForwardCashFigure.Unavailable(ForwardCashSource.Derived, closing.UnavailableReason!);
        }

        return new ForwardCashPicture(
            asOf, currency, months, undated, later, exclusions,
            reading?.ReadAt, reading?.Connector, reading is null ? Unavailable(inputs) : null,
            inputs.RefreshFailureReason, inputs.RefreshFailedAtUtc);
    }

    /// <summary>
    /// The occurrences of <paramref name="bill"/> from its next one up to
    /// <paramref name="horizonEnd"/>: the next as the package states it (even
    /// if already past), then each later one on or after <paramref name="asOf"/>,
    /// stepped from the next by the schedule word, assuming every one period.
    /// </summary>
    internal static IEnumerable<ForwardCashItem> Occurrences(RepeatingBill bill, DateOnly asOf, DateOnly horizonEnd, ForwardCashSource source)
    {
        var description = $"{bill.Supplier}: {bill.Description}";
        var nextNote = bill.NextDue < asOf ? $"next scheduled, overdue (due {Iso(bill.NextDue)})" : "next scheduled";

        if (bill.NextDue > horizonEnd)
        {
            yield return new ForwardCashItem(ForwardCashItemKind.RepeatingBill, description, bill.NextDue, bill.Amount, source, Note: nextNote);
            yield break;
        }

        var step = StepFor(bill.Frequency);
        if (step is null)
        {
            yield return new ForwardCashItem(
                ForwardCashItemKind.RepeatingBill, description, bill.NextDue, bill.Amount, source,
                Note: $"{nextNote}; schedule '{bill.Frequency}' not understood — later occurrences not projected");
            yield break;
        }

        yield return new ForwardCashItem(ForwardCashItemKind.RepeatingBill, description, bill.NextDue, bill.Amount, source, Note: nextNote);

        // Stepped from the next date each time (never from the previous
        // step), so a schedule on the 31st does not drift to the 28th.
        for (var k = 1; k < 1000; k++)
        {
            var date = step(bill.NextDue, k);
            if (date > horizonEnd)
                yield break;
            if (date < asOf)
                continue;

            yield return new ForwardCashItem(
                ForwardCashItemKind.RepeatingBill, description, date, bill.Amount, source,
                Note: $"projected from the {bill.Frequency.ToUpperInvariant()} schedule");
        }
    }

    private static Func<DateOnly, int, DateOnly>? StepFor(string frequency) => frequency.Trim().ToUpperInvariant() switch
    {
        "DAILY" => (d, k) => d.AddDays(k),
        "WEEKLY" => (d, k) => d.AddDays(7 * k),
        "FORTNIGHTLY" => (d, k) => d.AddDays(14 * k),
        "MONTHLY" => (d, k) => d.AddMonths(k),
        "QUARTERLY" => (d, k) => d.AddMonths(3 * k),
        "YEARLY" or "ANNUALLY" => (d, k) => d.AddYears(k),
        _ => null,
    };

    private static Money SumOf(IEnumerable<ForwardCashItem> items, ForwardCashItemKind kind, CurrencyCode currency) =>
        Money.Sum(items.Where(i => i.Kind == kind).Select(i => i.Gross), currency);

    /// <summary>
    /// The source of a month's invoices-due figure: the accounting package
    /// the invoices were sent to, read at the oldest of their read-back times
    /// (so the figure is never described as fresher than its stalest part);
    /// no time when any of them has never been read back.
    /// </summary>
    private static ForwardCashSource InvoiceSource(IReadOnlyList<ForwardCashItem> inMonth, IReadOnlyList<ForwardCashInvoice> all, AccountsReading? reading)
    {
        var invoices = inMonth.Where(i => i.Kind == ForwardCashItemKind.InvoiceDue).ToList();
        var package = reading?.Connector
            ?? all.Select(i => i.Source.Label).FirstOrDefault()?.Replace(" invoice", string.Empty, StringComparison.Ordinal)
            ?? "Accounting package";
        var label = $"{package} invoices raised in TempestOS, status as read back";

        if (invoices.Count == 0)
            return new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, label);

        var readAt = invoices.Any(i => i.Source.ReadAt is null) ? null : invoices.Min(i => i.Source.ReadAt);
        return new ForwardCashSource(ForwardCashSourceKind.AccountingPackage, label, readAt);
    }

    private static CurrencyCode ResolveCurrency(ForwardCashInputs inputs)
    {
        var reading = inputs.Reading;
        IEnumerable<CurrencyCode> candidates = reading is null
            ? []
            : reading.Cash.Select(c => c.Balance.Currency)
                .Concat(reading.Bills.Select(b => b.Amount.Currency))
                .Concat(reading.RepeatingBills.Select(r => r.Bill.Amount.Currency));

        return candidates
            .Concat(inputs.Invoices.Select(i => i.Gross.Currency))
            .Concat(inputs.Pipeline.Select(p => p.Gross.Currency))
            .DefaultIfEmpty(CurrencyCode.Gbp)
            .First();
    }

    private static Money? SameCurrencyTotal(IEnumerable<Money> amounts)
    {
        var list = amounts.ToList();
        return list.Select(m => m.Currency).Distinct().Count() == 1 ? Money.Sum(list, list[0].Currency) : null;
    }

    private static string Unavailable(ForwardCashInputs inputs) => inputs.ReadingUnavailableReason ?? "No accounts reading yet.";

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
