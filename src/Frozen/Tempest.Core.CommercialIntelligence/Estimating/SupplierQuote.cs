using Tempest.Core.BusinessGovernance;
using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Estimating;

/// <summary>How firm a supplier's figure is.</summary>
/// <remarks>
/// Suppliers say very different things in very similar words, and the
/// difference decides whether the number can be relied on. Recorded as
/// its own axis so the distinction survives being read back a year later.
/// </remarks>
public enum QuoteFirmness
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>A number offered over the phone or in a mail, subject to everything.</summary>
    Indicative,

    /// <summary>A written figure, subject to stated conditions.</summary>
    Budgetary,

    /// <summary>A written figure the supplier will hold for a stated period.</summary>
    Firm,

    /// <summary>A firm figure the supplier has confirmed against the actual drawing or specification.</summary>
    FirmAgainstSpecification
}

/// <summary>What a supplier said one line of a job would cost.</summary>
/// <param name="Reference">The line's own identifier within the quote. Required.</param>
/// <param name="Description">What the supplier is pricing. Required.</param>
/// <param name="Quantity">The quantity the price is for.</param>
/// <param name="UnitPrice">The price of one unit, as the supplier stated it. Required.</param>
/// <param name="LeadTime">The lead time the supplier stated for this line. <see langword="null"/> where none was.</param>
/// <param name="ProcessRecordId">The `A7` process the line corresponds to, where it does. <see langword="null"/> otherwise.</param>
/// <param name="Notes">Anything the supplier attached to the line. <see langword="null"/> if nothing.</param>
public sealed record SupplierQuoteLine(
    string Reference,
    string Description,
    decimal Quantity,
    Money UnitPrice,
    LeadTimeDuration? LeadTime = null,
    string? ProcessRecordId = null,
    string? Notes = null)
{
    /// <summary>The line's own identifier within the quote.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A quote line must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What the supplier is pricing.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A quote line must say what is being priced.", nameof(Description))
        : Description.Trim();

    /// <summary>The quantity the price is for.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Quantity"/> is not positive.</exception>
    public decimal Quantity { get; } = Quantity <= 0m
        ? throw new ArgumentOutOfRangeException(nameof(Quantity), Quantity, "A quote line must price a positive quantity.")
        : Quantity;

    /// <summary>The line total, as the supplier's own arithmetic gives it.</summary>
    public Money LineTotal => UnitPrice * Quantity;
}

/// <summary>
/// What a named supplier actually offered, in writing, on a date.
/// </summary>
/// <remarks>
/// <para>
/// <b>The second of the four things D4 keeps apart.</b> A supplier quote
/// is not an estimate: the organisation did not derive it, a third party
/// asserted it, and it is that third party's figure whether or not
/// anybody here thinks it reasonable. It is not a customer quotation
/// either — nothing here has been offered to a customer, and a supplier's
/// price is not a selling price (`ADR-0134`).
/// </para>
/// <para>
/// Holding a firm quote does not place an order and does not commit the
/// organisation to anything. `D4` records what was offered; accepting it
/// is an act of purchasing authority `P03` does not hold (`ADR-0135`).
/// </para>
/// </remarks>
public sealed record SupplierQuote
{
    /// <summary>The reference the quote is known by inside TempestOS. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>The supplier who gave it. Required.</summary>
    public required string SupplierRecordId { get; init; }

    /// <summary>The supplier's own quotation number, as printed. <see langword="null"/> where the supplier gave none.</summary>
    public string? SupplierQuotationNumber { get; init; }

    /// <summary>What was asked for. Required.</summary>
    public required string Subject { get; init; }

    /// <summary>The currency the supplier quoted in. Required.</summary>
    public required CurrencyCode Currency { get; init; }

    /// <summary>How firm the figure is.</summary>
    public QuoteFirmness Firmness { get; init; } = QuoteFirmness.Unspecified;

    /// <summary>The lines. Never <see langword="null"/>.</summary>
    public IReadOnlyList<SupplierQuoteLine> Lines { get; init; } = [];

    /// <summary>The date the supplier put on it. <see langword="null"/> where it carried none.</summary>
    public DateOnly? QuotedOn { get; init; }

    /// <summary>How long the supplier said it would hold the price. <see langword="null"/> where nobody said.</summary>
    public EffectivePeriod? Validity { get; init; }

    /// <summary>The lead time the supplier stated overall. <see langword="null"/> where none was stated.</summary>
    public LeadTimeDuration? StatedLeadTime { get; init; }

    /// <summary>The conditions the supplier attached. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Conditions { get; init; } = [];

    /// <summary>What the supplier said it excludes. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Exclusions { get; init; } = [];

    /// <summary>Payment terms as stated. <see langword="null"/> where none were.</summary>
    public string? PaymentTerms { get; init; }

    /// <summary>Delivery terms — Incoterms or the supplier's own words. <see langword="null"/> where none were stated.</summary>
    public string? DeliveryTerms { get; init; }

    /// <summary>The quotation document itself, and anything else supporting the record. Never <see langword="null"/>.</summary>
    public IReadOnlyList<BusinessEvidence> Evidence { get; init; } = [];

    /// <summary>Who inside the organisation received it. <see langword="null"/> where unrecorded.</summary>
    public string? ReceivedByPrincipalId { get; init; }

    /// <summary>Anything else about the quote. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The quote total, in the quote's own currency.</summary>
    /// <exception cref="CurrencyMismatchException">A line is stated in a currency other than the quote's.</exception>
    public Money Total => Lines.Aggregate(new Money(0m, Currency), (running, line) => running + line.LineTotal);

    /// <summary>Whether the supplier is bound by the figure — firm, in writing, and still inside its own validity.</summary>
    /// <remarks>
    /// Deliberately not a property: whether a quote still stands depends
    /// on when you ask, and a property invites callers to cache the answer
    /// past the date it was true.
    /// </remarks>
    public bool IsBindingAt(DateOnly asAt) =>
        Firmness is QuoteFirmness.Firm or QuoteFirmness.FirmAgainstSpecification
        && Validity is { } validity
        && validity.Contains(asAt);

    /// <summary>Whether the quote has run past its own validity as at <paramref name="asAt"/>.</summary>
    public bool IsExpiredAt(DateOnly asAt) => Validity?.HasExpiredBy(asAt) ?? false;

    /// <summary>Whether the quotation document itself is on file.</summary>
    /// <remarks>
    /// A supplier quote nobody can produce the paperwork for is a
    /// recollection of a price, and the validation service says so.
    /// </remarks>
    public bool IsEvidenced => Evidence.Any(e => e.IsLocatable);

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }

    /// <summary>The pin identifying this quote's supplier at a stated revision, for an estimate to cite.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revisionNumber"/> is not positive.</exception>
    public ReferencePin PinSupplier(int revisionNumber) =>
        new(SupplierCatalog.SupplierLibraryName, SupplierRecordId, revisionNumber);
}
