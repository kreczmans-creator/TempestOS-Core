using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.BusinessOperations.Finance;

/// <summary>What a financial figure is.</summary>
/// <remarks>
/// The four states money passes through, and the distinction that makes
/// the whole package worth having. A budget is what was allowed, a
/// commitment is what has been promised away, an actual is what has been
/// paid, and the difference between commitment and actual is the money
/// that is gone without appearing in any invoice yet (`ADR-0142`).
/// </remarks>
public enum FinancialPosture
{
    /// <summary>Set aside, not yet promised to anybody.</summary>
    Budgeted,

    /// <summary>Promised — an order placed, a contract signed. Not yet paid.</summary>
    Committed,

    /// <summary>Invoiced and payable, or received and receivable.</summary>
    Accrued,

    /// <summary>Money that has actually moved.</summary>
    Actual
}

/// <summary>Which way the money goes.</summary>
public enum CashDirection
{
    /// <summary>Money leaving the business.</summary>
    Outgoing,

    /// <summary>Money coming in.</summary>
    Incoming
}

/// <summary>
/// One line of a budget — an amount set aside for something.
/// </summary>
/// <param name="Reference">The line's own identifier within the budget. Required.</param>
/// <param name="Description">What it is for. Required.</param>
/// <param name="Amount">How much. Required.</param>
/// <param name="Direction">Which way the money goes.</param>
/// <param name="Category">The organisation's own category — an open string, because no chart of accounts ships with the platform.</param>
public sealed record BudgetLine(
    string Reference,
    string Description,
    Money Amount,
    CashDirection Direction = CashDirection.Outgoing,
    string? Category = null)
{
    /// <summary>The line's own identifier within the budget.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A budget line must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What it is for.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A budget line must say what it is for.", nameof(Description))
        : Description.Trim();
}

/// <summary>
/// An amount set aside, and what has happened to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Distinct from `P07`'s `C5` forecasting.</b> A `FinancialScenario`
/// is a view of a possible future; a budget is a decision about the
/// present that somebody is accountable for. `C5` models what might
/// happen, `WP04.3` records what was allowed and what has gone.
/// </para>
/// <para>
/// <b>Not accounting.</b> `P04` holds no chart of accounts, applies no
/// tax rules, computes no VAT, keeps no ledger and enforces no double
/// entry. It answers "how much of this budget is left?" and stops
/// (`ADR-0142`).
/// </para>
/// </remarks>
public sealed record Budget
{
    /// <summary>The reference the budget is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What it is for. Required.</summary>
    public required string Name { get; init; }

    /// <summary>The currency every figure in it is stated in. Required.</summary>
    public required CurrencyCode Currency { get; init; }

    /// <summary>The lines. Never <see langword="null"/>.</summary>
    public IReadOnlyList<BudgetLine> Lines { get; init; } = [];

    /// <summary>The period it covers. <see langword="null"/> where it is open-ended.</summary>
    public EffectivePeriod? Period { get; init; }

    /// <summary>
    /// The person who set the budget, and the authority they held.
    /// <see langword="null"/> until somebody does.
    /// </summary>
    /// <remarks>
    /// Reuses `P07`'s <see cref="BusinessAuthorisation"/>. Setting a
    /// budget commits the organisation to spending, which is an act of
    /// authority a named person performs; `P04` records it and confers
    /// none.
    /// </remarks>
    public BusinessAuthorisation? SetUnderAuthority { get; init; }

    /// <summary>Who owns it, and where it stands.</summary>
    public OperationalFacts Facts { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The total set aside for money going out.</summary>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the budget's.</exception>
    public Money TotalOutgoing => Sum(CashDirection.Outgoing);

    /// <summary>The total expected in.</summary>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the budget's.</exception>
    public Money TotalIncoming => Sum(CashDirection.Incoming);

    /// <summary>Whether anybody with authority actually set it.</summary>
    public bool IsAuthorised => SetUnderAuthority is not null;

    /// <summary>Whether it is in force on <paramref name="asAt"/>.</summary>
    public bool IsCurrentAt(DateOnly asAt) => Period is not { } period || period.Contains(asAt);

    /// <summary>The line carrying <paramref name="reference"/>, or <see langword="null"/> where none does.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public BudgetLine? FindLine(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return Lines.FirstOrDefault(l => string.Equals(l.Reference, reference.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private Money Sum(CashDirection direction) =>
        Lines.Where(l => l.Direction == direction)
            .Aggregate(new Money(0m, Currency), (running, line) => running + line.Amount);

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}

/// <summary>
/// Money promised, invoiced or paid, recorded against the budget it
/// draws on.
/// </summary>
/// <remarks>
/// One type for commitments, accruals and actuals, separated by
/// <see cref="Posture"/>, because they are the same money at three
/// moments in its life and modelling them as three types would mean
/// copying an amount between records as it progressed — which is exactly
/// how the totals stop agreeing.
/// </remarks>
public sealed record FinancialEntry
{
    /// <summary>The reference the entry is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What it is for. Required.</summary>
    public required string Description { get; init; }

    /// <summary>How much. Required.</summary>
    public required Money Amount { get; init; }

    /// <summary>What stage the money is at.</summary>
    public FinancialPosture Posture { get; init; } = FinancialPosture.Committed;

    /// <summary>Which way it goes.</summary>
    public CashDirection Direction { get; init; } = CashDirection.Outgoing;

    /// <summary>The budget it draws on, by reference. <see langword="null"/> where it draws on none.</summary>
    public string? BudgetReference { get; init; }

    /// <summary>The budget line it draws on. <see langword="null"/> where unallocated.</summary>
    public string? BudgetLineReference { get; init; }

    /// <summary>Who the money is going to, or coming from. <see langword="null"/> where unrecorded.</summary>
    public PartyReference? Party { get; init; }

    /// <summary>The purchase order it arises from, by reference. <see langword="null"/> where none.</summary>
    public string? PurchaseOrderReference { get; init; }

    /// <summary>When it was committed, invoiced or paid.</summary>
    public DateOnly? OccurredOn { get; init; }

    /// <summary>The supplier's or customer's own document number. <see langword="null"/> where unrecorded.</summary>
    public string? ExternalReference { get; init; }

    /// <summary>Who owns it, and where it stands.</summary>
    public OperationalFacts Facts { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the money has actually moved.</summary>
    public bool IsRealised => Posture == FinancialPosture.Actual;

    /// <summary>Whether the money is promised away without having moved.</summary>
    /// <remarks>
    /// The figure a business most often does not have and most needs: an
    /// order placed is money gone, whatever the bank statement says.
    /// </remarks>
    public bool IsPromisedNotPaid => Posture is FinancialPosture.Committed or FinancialPosture.Accrued;

    /// <summary>Whether the entry says which budget it draws on.</summary>
    public bool IsAllocated => !string.IsNullOrWhiteSpace(BudgetReference);

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}

/// <summary>
/// How a budget stands: what was allowed, what is promised, what has
/// gone, and what is left.
/// </summary>
/// <remarks>
/// <b>Remaining is computed against commitments, not payments.</b> A
/// business that measures its budget against what it has paid discovers
/// its overspend when the invoices arrive. Both figures are reported, and
/// <see cref="UncommittedRemaining"/> is the one to act on.
/// </remarks>
/// <param name="BudgetReference">The budget.</param>
/// <param name="Currency">Its currency.</param>
/// <param name="Allowed">What was set aside.</param>
/// <param name="Committed">What has been promised away, paid or not.</param>
/// <param name="Actual">What has actually been paid.</param>
/// <param name="UnallocatedEntryCount">Entries that name no budget line.</param>
public sealed record BudgetPosition(
    string BudgetReference,
    CurrencyCode Currency,
    Money Allowed,
    Money Committed,
    Money Actual,
    int UnallocatedEntryCount)
{
    /// <summary>What is left before the budget is fully promised away.</summary>
    public Money UncommittedRemaining => Allowed - Committed;

    /// <summary>What is left before the budget is fully spent.</summary>
    public Money UnspentRemaining => Allowed - Actual;

    /// <summary>Money promised and not yet paid.</summary>
    public Money OutstandingCommitment => Committed - Actual;

    /// <summary>Whether more has been promised than was allowed.</summary>
    public bool IsOvercommitted => Committed > Allowed;

    /// <summary>Whether more has been paid than was allowed.</summary>
    public bool IsOverspent => Actual > Allowed;

    /// <summary>
    /// How much of the budget is promised away, from 0 to 1, or
    /// <see langword="null"/> where the budget is zero.
    /// </summary>
    public decimal? CommittedProportion =>
        Allowed.Amount == 0m ? null : Committed.Amount / Allowed.Amount;
}
