using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.Quotations;

/// <summary>
/// The acts a <see cref="Quotation"/> supports: create, add/update/remove a
/// line, send, accept, decline (`WP 19.5A`, `ADR-0152`, Product Owner
/// comment item 4: "a quote is opened with the project; the quote defines
/// the initial requirement set and defines the deliverables"). Every act is
/// one or more transactions with an audit row; whether an act is
/// <em>permitted</em> is decided here, before <see cref="Quotation"/>'s own
/// mutators ever run, and reported back as a refusal result rather than an
/// exception, mirroring <c>Tempest.Core.Invoicing.IInvoicingService</c>.
/// </summary>
public interface IQuotationService
{
    /// <summary>
    /// Creates a new, empty <see cref="Quotation"/> under
    /// <paramref name="projectId"/> — dated today, in the project's own
    /// pinned rate card's currency (GBP if none is pinned), valid 30 days.
    /// </summary>
    /// <param name="projectId">The project this quotation is opened with.</param>
    /// <param name="reference">A reference to use verbatim, or <see langword="null"/> to generate <c>Q-&lt;yyyy&gt;-&lt;nnn&gt;</c> (or <c>CO-&lt;yyyy&gt;-&lt;nnn&gt;</c> for a <see cref="QuotationKind.ChangeOrder"/>) from a per-year count of existing quotations of that same kind's own prefix.</param>
    /// <param name="clientOrganisationId">The client to quote, or <see langword="null"/> to default to the project's own client (which may itself be unset).</param>
    /// <param name="kind">Whether this is an ordinary quotation or a change order (`WP 20.10E`). Defaults to <see cref="QuotationKind.Quotation"/> — unchanged behaviour.</param>
    /// <remarks>Refused, as a result, when <paramref name="projectId"/> does not identify a live project.</remarks>
    Task<QuotationResult> CreateAsync(
        Guid projectId, string? reference = null, string? clientOrganisationId = null, QuotationKind kind = QuotationKind.Quotation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a line to <paramref name="quotationId"/>'s own quotation —
    /// either <paramref name="hours"/> and <paramref name="rate"/>
    /// (hourly), or <paramref name="fixedPrice"/> alone, never both.
    /// </summary>
    /// <param name="carriedDeliverableId">
    /// On a <see cref="QuotationKind.ChangeOrder"/> only: the id of an
    /// already-existing, live deliverable this line carries — recorded on
    /// the line immediately, rather than left for <see cref="AcceptAsync"/>
    /// to fill in (`WP 20.10E`). <see langword="null"/> (the default) for
    /// an ordinary new-work line, the only shape an ordinary
    /// <see cref="QuotationKind.Quotation"/> line ever takes.
    /// </param>
    /// <param name="vatRate">
    /// This line's own VAT treatment (`WP 21.3B`). <see langword="null"/>
    /// (the default) resolves to the consultant's own default, set in
    /// Settings → Organisation identity (<see cref="QuotationService.DefaultVatRateSettingKey"/>) —
    /// <see cref="Core.BusinessGovernance.VatRate.OutOfScope"/> until they
    /// change it.
    /// </param>
    /// <remarks>Refused, as a result, when the quotation is not Draft, when neither pricing shape (nor both) is given, when a supplied rate/fixed price is not in the quotation's own currency, when <paramref name="carriedDeliverableId"/> is given on a quotation that is not a change order, or when it does not identify a live deliverable.</remarks>
    Task<QuotationResult> AddLineAsync(
        Guid quotationId, string description, decimal? hours, Money? rate, Money? fixedPrice, Guid? carriedDeliverableId = null,
        VatRate? vatRate = null, CancellationToken cancellationToken = default);

    /// <summary>Replaces the line identified by <paramref name="lineId"/> — the same rules as <see cref="AddLineAsync"/>, including <paramref name="vatRate"/>'s own default.</summary>
    /// <remarks>Refused, as a result, when the quotation is not Draft, when <paramref name="lineId"/> does not identify a line on it, or for the same pricing-shape/currency reasons as <see cref="AddLineAsync"/>.</remarks>
    Task<QuotationResult> UpdateLineAsync(
        Guid quotationId, Guid lineId, string description, decimal? hours, Money? rate, Money? fixedPrice,
        VatRate? vatRate = null, CancellationToken cancellationToken = default);

    /// <summary>Removes the line identified by <paramref name="lineId"/>.</summary>
    /// <remarks>Refused, as a result, when the quotation is not Draft, or when <paramref name="lineId"/> does not identify a line on it.</remarks>
    Task<QuotationResult> RemoveLineAsync(Guid quotationId, Guid lineId, CancellationToken cancellationToken = default);

    /// <summary>Sends <paramref name="quotationId"/>'s own quotation — records <see cref="Quotation.SentOn"/> as today.</summary>
    /// <remarks>Refused, as a result, when the quotation is not Draft, or carries no lines.</remarks>
    Task<QuotationResult> SendAsync(Guid quotationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts <paramref name="quotationId"/>'s own quotation: for a line
    /// with no carried deliverable, creates a Deliverable under a
    /// milestone named after this quotation's own
    /// <see cref="Quotation.Reference"/> (created, if absent, with
    /// <see cref="Quotation.QuoteDate"/> plus
    /// <see cref="Quotation.ValidityDays"/> as its target); for a line that
    /// already carries one (a <see cref="QuotationKind.ChangeOrder"/>
    /// line — `WP 20.10E`), creates nothing, the carried id is used as-is.
    /// Either way, one Requirement per line, titled from the line and
    /// associated to the project. Each creation is its own transaction and
    /// audit row, in order; the quotation then records every line's own
    /// deliverable id (created or carried) in one further transaction
    /// alongside the move to <see cref="QuotationStatus.Accepted"/>.
    /// </summary>
    /// <remarks>Refused, as a result, when the quotation is not Sent — including a quotation already Accepted, so a second Accept is refused rather than creating a second set of objects.</remarks>
    Task<QuotationResult> AcceptAsync(Guid quotationId, CancellationToken cancellationToken = default);

    /// <summary>Declines <paramref name="quotationId"/>'s own quotation. Creates nothing.</summary>
    /// <remarks>Refused, as a result, when the quotation is not Sent.</remarks>
    Task<QuotationResult> DeclineAsync(Guid quotationId, CancellationToken cancellationToken = default);
}
