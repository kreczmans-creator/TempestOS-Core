using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations.Purchasing;

/// <summary>Where a requisition has got to.</summary>
public enum RequisitionState
{
    /// <summary>Somebody has asked for something.</summary>
    Requested,

    /// <summary>Being sourced — quotes sought, options compared.</summary>
    Sourcing,

    /// <summary>Sourced and awaiting somebody's authority to commit.</summary>
    AwaitingAuthority,

    /// <summary>Authorised, and an order has been raised.</summary>
    Ordered,

    /// <summary>Declined, with a reason.</summary>
    Declined,

    /// <summary>Withdrawn by whoever asked.</summary>
    Withdrawn
}

/// <summary>Where an order has got to.</summary>
public enum PurchaseOrderState
{
    /// <summary>Raised and not yet sent.</summary>
    Draft,

    /// <summary>Sent to the supplier.</summary>
    Placed,

    /// <summary>Some of it has arrived.</summary>
    PartiallyReceived,

    /// <summary>All of it has arrived.</summary>
    Received,

    /// <summary>Received and invoiced.</summary>
    Invoiced,

    /// <summary>Finished.</summary>
    Closed,

    /// <summary>Cancelled, with a reason.</summary>
    Cancelled
}

/// <summary>One thing being bought.</summary>
/// <param name="Reference">The line's own identifier within the order. Required.</param>
/// <param name="Description">What is being bought. Required.</param>
/// <param name="Quantity">How many.</param>
/// <param name="UnitPrice">The agreed price of one. Required.</param>
/// <param name="QuantityReceived">How many have arrived.</param>
/// <param name="ExpectedOn">When it is expected. <see langword="null"/> where nobody said.</param>
/// <param name="ProcessRecordId">The `A7` process this line buys, where it buys one. <see langword="null"/> otherwise.</param>
public sealed record PurchaseOrderLine(
    string Reference,
    string Description,
    decimal Quantity,
    Money UnitPrice,
    decimal QuantityReceived = 0m,
    DateOnly? ExpectedOn = null,
    string? ProcessRecordId = null)
{
    /// <summary>The line's own identifier within the order.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A purchase order line must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What is being bought.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A purchase order line must say what is being bought.", nameof(Description))
        : Description.Trim();

    /// <summary>How many.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Quantity"/> is not positive.</exception>
    public decimal Quantity { get; } = Quantity <= 0m
        ? throw new ArgumentOutOfRangeException(nameof(Quantity), Quantity, "A purchase order line must order a positive quantity.")
        : Quantity;

    /// <summary>How many have arrived.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="QuantityReceived"/> is negative.</exception>
    public decimal QuantityReceived { get; init; } = QuantityReceived < 0m
        ? throw new ArgumentOutOfRangeException(nameof(QuantityReceived), QuantityReceived, "A negative quantity cannot have been received.")
        : QuantityReceived;

    /// <summary>The line total at the agreed price.</summary>
    public Money LineTotal => UnitPrice * Quantity;

    /// <summary>Whether everything ordered has arrived.</summary>
    public bool IsFullyReceived => QuantityReceived >= Quantity;

    /// <summary>How many are still to come.</summary>
    public decimal QuantityOutstanding => Math.Max(0m, Quantity - QuantityReceived);

    /// <summary>Whether more arrived than was ordered.</summary>
    /// <remarks>
    /// Reported rather than refused. Over-delivery happens, and a system
    /// that will not record it produces a receipt note nobody can file.
    /// </remarks>
    public bool IsOverReceived => QuantityReceived > Quantity;

    /// <summary>Whether the line is late as at <paramref name="asAt"/>.</summary>
    public bool IsLateAt(DateOnly asAt) => !IsFullyReceived && ExpectedOn is { } expected && expected < asAt;
}

/// <summary>
/// Somebody's request that the business buy something.
/// </summary>
/// <remarks>
/// The step before an order, and the one that carries the engineering
/// need. A requisition says what is wanted and why; the order says what
/// was agreed with whom. Keeping them apart means the need survives a
/// change of supplier.
/// </remarks>
public sealed record PurchaseRequisition
{
    /// <summary>The reference the requisition is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What is wanted. Required.</summary>
    public required string Requirement { get; init; }

    /// <summary>Where it has got to.</summary>
    public RequisitionState State { get; init; } = RequisitionState.Requested;

    /// <summary>Why it is wanted. <see langword="null"/> where nobody said.</summary>
    public string? Justification { get; init; }

    /// <summary>What it is expected to cost. <see langword="null"/> where nobody has estimated.</summary>
    public Money? EstimatedValue { get; init; }

    /// <summary>The budget it is to draw on, by reference. <see langword="null"/> where unallocated.</summary>
    public string? BudgetReference { get; init; }

    /// <summary>The `P03` sourcing comparison behind it, at the revision relied on. <see langword="null"/> where none was made.</summary>
    /// <remarks>
    /// The seam between `P03` and `P04`. `P03` compares suppliers and
    /// recommends; the requisition records which comparison a person acted
    /// on, at the revision they read (`ADR-0142`).
    /// </remarks>
    public ReferencePin? SourcingComparisonPin { get; init; }

    /// <summary>The `P03` supplier quote being acted on, at the revision relied on. <see langword="null"/> where none.</summary>
    public ReferencePin? SupplierQuotePin { get; init; }

    /// <summary>Why the requisition was declined, where it was. <see langword="null"/> otherwise.</summary>
    public string? DeclineReason { get; init; }

    /// <summary>The order raised from it, by reference. <see langword="null"/> until one is.</summary>
    public string? PurchaseOrderReference { get; init; }

    /// <summary>Who owns it, and where it stands.</summary>
    public OperationalFacts Facts { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether it has resulted in an order.</summary>
    public bool IsOrdered => State == RequisitionState.Ordered && PurchaseOrderReference is not null;

    /// <summary>Whether anybody compared options before committing.</summary>
    public bool WasSourced => SourcingComparisonPin is not null || SupplierQuotePin is not null;

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
/// An order the business placed with a supplier.
/// </summary>
/// <remarks>
/// <para>
/// <b>TempestOS does not place it.</b> `P03` compares suppliers and
/// recommends and never orders (`ADR-0135`); `P04` records the order a
/// person raised, with the authority they held. Nothing here sends
/// anything, and <see cref="PlacedUnderAuthority"/> must be present before
/// an order counts as placed — validation raises an **error** where it is
/// not (`ADR-0142`).
/// </para>
/// <para>
/// <b>Not an ERP.</b> No goods-receipt workflow, no three-way match, no
/// purchase ledger, no payment run. The order records what was agreed and
/// what has arrived, and stops.
/// </para>
/// </remarks>
public sealed record PurchaseOrder
{
    /// <summary>The order number. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>Who it was placed with. Required.</summary>
    public required PartyReference Supplier { get; init; }

    /// <summary>What it is for. Required.</summary>
    public required string Subject { get; init; }

    /// <summary>The currency it is placed in. Required.</summary>
    public required CurrencyCode Currency { get; init; }

    /// <summary>Where it has got to.</summary>
    public PurchaseOrderState State { get; init; } = PurchaseOrderState.Draft;

    /// <summary>The lines. Never <see langword="null"/>.</summary>
    public IReadOnlyList<PurchaseOrderLine> Lines { get; init; } = [];

    /// <summary>The requisition it arises from, by reference. <see langword="null"/> where it arises from none.</summary>
    public string? RequisitionReference { get; init; }

    /// <summary>The `P03` supplier quote it was placed against, at the revision relied on. <see langword="null"/> where none.</summary>
    /// <remarks>
    /// Pinned rather than referenced, so an order placed against
    /// revision 2 of a quote keeps saying revision 2 after the quote is
    /// revised.
    /// </remarks>
    public ReferencePin? SupplierQuotePin { get; init; }

    /// <summary>
    /// The person who placed it and the authority they held.
    /// <see langword="null"/> until they do.
    /// </summary>
    public BusinessAuthorisation? PlacedUnderAuthority { get; init; }

    /// <summary>When it was placed. <see langword="null"/> until it is.</summary>
    public DateOnly? PlacedOn { get; init; }

    /// <summary>The payment terms agreed. <see langword="null"/> where none were.</summary>
    public string? PaymentTerms { get; init; }

    /// <summary>The delivery terms agreed. <see langword="null"/> where none were.</summary>
    public string? DeliveryTerms { get; init; }

    /// <summary>The budget it draws on, by reference. <see langword="null"/> where unallocated.</summary>
    public string? BudgetReference { get; init; }

    /// <summary>Why it was cancelled, where it was. <see langword="null"/> otherwise.</summary>
    public string? CancellationReason { get; init; }

    /// <summary>Who owns it, and where it stands.</summary>
    public OperationalFacts Facts { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The order total.</summary>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the order's.</exception>
    public Money Total => Lines.Aggregate(new Money(0m, Currency), (running, line) => running + line.LineTotal);

    /// <summary>The value of what has not yet arrived.</summary>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the order's.</exception>
    public Money OutstandingValue =>
        Lines.Aggregate(new Money(0m, Currency), (running, line) => running + line.UnitPrice * line.QuantityOutstanding);

    /// <summary>Whether the order has actually been placed with anybody.</summary>
    public bool HasBeenPlaced => State is not (PurchaseOrderState.Draft or PurchaseOrderState.Cancelled);

    /// <summary>Whether everything ordered has arrived.</summary>
    public bool IsFullyReceived => Lines.Count > 0 && Lines.All(l => l.IsFullyReceived);

    /// <summary>Lines where more arrived than was ordered.</summary>
    public IReadOnlyList<PurchaseOrderLine> OverReceivedLines => Lines.Where(l => l.IsOverReceived).ToList();

    /// <summary>Lines that have not arrived and are past their expected date.</summary>
    public IReadOnlyList<PurchaseOrderLine> LateLines(DateOnly asAt) => Lines.Where(l => l.IsLateAt(asAt)).ToList();

    /// <summary>
    /// Whether the order is recorded as placed with nobody named as
    /// having placed it.
    /// </summary>
    /// <remarks>
    /// The property this type exists to make checkable. An order commits
    /// the business's money, and one the business cannot attribute to a
    /// person is a commitment nobody is accountable for.
    /// </remarks>
    public bool IsPlacedWithoutAuthority => HasBeenPlaced && PlacedUnderAuthority is null;

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
