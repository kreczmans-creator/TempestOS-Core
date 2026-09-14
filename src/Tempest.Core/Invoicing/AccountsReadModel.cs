using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Invoicing;

/// <summary>One of Tempest's own sent invoice requests, as the receivable side of <see cref="AccountsSnapshot"/> sees it (`WP 19.8B`, scope §3).</summary>
/// <param name="RequestId">The <see cref="InvoiceRequest.Id"/> this figure comes from.</param>
/// <param name="ClientOrganisationId">The client this invoice was raised against.</param>
/// <param name="IssuedDate">When the invoice was issued, as the connector reported it (<see cref="InvoiceRequest.IssuedDate"/>).</param>
/// <param name="DueDate"><paramref name="IssuedDate"/> plus <see cref="AccountsReadModel.PaymentTermsDaysConfigurationKey"/>.</param>
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

/// <summary>The only <see cref="IAccountsReadModel"/> implementation (`WP 19.8B`, scope §3).</summary>
public sealed class AccountsReadModel : IAccountsReadModel
{
    /// <summary>The <see cref="IConfigurationProvider"/> key naming how many days after issue a Tempest-raised invoice falls due.</summary>
    public const string PaymentTermsDaysConfigurationKey = "Accounts:PaymentTermsDays";

    /// <summary>The payment terms used when <see cref="PaymentTermsDaysConfigurationKey"/> is not configured, or is configured to something other than a positive integer.</summary>
    public const int DefaultPaymentTermsDays = 30;

    private readonly IAccountsReadingStore _store;
    private readonly EngineeringDomainContext _context;
    private readonly IConfigurationProvider _configuration;
    private readonly AccountsRefreshService? _refreshService;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="AccountsReadModel"/> class.</summary>
    /// <param name="refreshService">Where the reason and time of the most recent failed refresh come from, for the <see cref="AccountsSnapshot.Unavailable"/> case — the same singleton <see cref="AccountsRefreshService"/> the hosted-service manager starts and stops. <see langword="null"/> is honoured (a test exercising this read model alone need not stand one up); the "no reading yet" reason is used regardless.</param>
    public AccountsReadModel(
        IAccountsReadingStore store, EngineeringDomainContext context, IConfigurationProvider configuration,
        AccountsRefreshService? refreshService = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(configuration);

        _store = store;
        _context = context;
        _configuration = configuration;
        _refreshService = refreshService;
        _time = timeProvider ?? TimeProvider.System;
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

        var termsDays = ResolvePaymentTermsDays();
        var all = await _context.Repository.ListByKindAsync(InvoiceRequest.CanonicalKind, cancellationToken).ConfigureAwait(false);

        var receivable = all.OfType<InvoiceRequest>()
            .Where(r => r.Status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted && r.IssuedDate is not null)
            .Select(r => new ReceivableInvoice(r.Id, r.ClientOrganisationId, r.IssuedDate!.Value, r.IssuedDate!.Value.AddDays(termsDays), r.Total))
            .ToList();

        return AccountsSnapshot.From(reading, receivable, asOf);
    }

    private int ResolvePaymentTermsDays() =>
        _configuration.TryGetValue(PaymentTermsDaysConfigurationKey, out var raw) && int.TryParse(raw, out var days) && days > 0
            ? days
            : DefaultPaymentTermsDays;
}
