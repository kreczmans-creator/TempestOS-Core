namespace Tempest.Core.Quotations;

/// <summary>
/// A <see cref="Quotation"/>'s own lifecycle position — the vocabulary the
/// Product Owner asked for in as many words (comment item 4: "accepted/
/// declined"), not the eight canonical states (`ADR-0074`): a quotation is
/// never "Released" or "Approved", it is sent to a client and answered
/// (`WP 19.5A`, `ADR-0152`).
/// </summary>
public enum QuotationStatus
{
    /// <summary>Built, not yet sent. Lines may still be added, changed or removed.</summary>
    Draft,

    /// <summary>Sent to the client. Lines are fixed.</summary>
    Sent,

    /// <summary>
    /// The client accepted — one Deliverable and one Requirement now exist
    /// for every line, under a milestone named after this quotation's own
    /// <see cref="Quotation.Reference"/>. Terminal: no revision of an
    /// accepted quotation exists in this release (`ADR-0152`) — a change is
    /// a new quotation.
    /// </summary>
    Accepted,

    /// <summary>The client declined. Terminal, and creates nothing.</summary>
    Declined,
}

/// <summary>
/// The permitted <see cref="QuotationStatus"/> transition table — the sole
/// enforcement point <see cref="QuotationService"/>'s own status-moving
/// acts check against, mirroring <c>Tempest.Core.Invoicing.InvoiceRequestStatusTransitions</c>
/// exactly (`WP 19.5A`, `ADR-0152`).
/// </summary>
internal static class QuotationStatusTransitions
{
    private static readonly IReadOnlyDictionary<QuotationStatus, IReadOnlySet<QuotationStatus>> Permitted =
        new Dictionary<QuotationStatus, IReadOnlySet<QuotationStatus>>
        {
            [QuotationStatus.Draft] = new HashSet<QuotationStatus> { QuotationStatus.Sent },
            [QuotationStatus.Sent] = new HashSet<QuotationStatus> { QuotationStatus.Accepted, QuotationStatus.Declined },

            // Terminal: no revision of an accepted or declined quotation in
            // this release (`ADR-0152`) — a change is a new quotation.
            [QuotationStatus.Accepted] = new HashSet<QuotationStatus>(),
            [QuotationStatus.Declined] = new HashSet<QuotationStatus>(),
        };

    /// <summary>Whether transitioning from <paramref name="from"/> to <paramref name="to"/> is permitted. A same-to-same request is not special-cased — permitted only where the table itself lists it.</summary>
    public static bool IsPermitted(QuotationStatus from, QuotationStatus to) => Permitted[from].Contains(to);

    /// <summary>Every <see cref="QuotationStatus"/> value, for a test to walk exhaustively.</summary>
    public static IReadOnlyList<QuotationStatus> AllStatuses { get; } =
        [QuotationStatus.Draft, QuotationStatus.Sent, QuotationStatus.Accepted, QuotationStatus.Declined];
}
