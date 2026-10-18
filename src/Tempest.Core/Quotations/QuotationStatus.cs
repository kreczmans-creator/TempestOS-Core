namespace Tempest.Core.Quotations;

/// <summary>
/// A <see cref="Quotation"/>'s own lifecycle position — the vocabulary the
/// Product Owner asked for in as many words (comment item 4: "accepted/
/// declined"), not the eight canonical states (`ADR-0074`): a quotation is
/// never "Released", it is reviewed by a second person, sent to a client
/// and answered (`WP 19.5A`, `ADR-0152`, runbook C3).
/// </summary>
public enum QuotationStatus
{
    /// <summary>Built, not yet approved. Lines may still be added, changed or removed; every change is saved as it is made (runbook C3).</summary>
    Draft,

    /// <summary>Sent to the client. Lines are fixed. Only an <see cref="Approved"/> revision can be sent (runbook C3).</summary>
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

    // Runbook C3 (PO: "save it as a draft and then review by second
    // person, then export becomes R1"). Appended, never inserted: the
    // value is persisted by name, and `QuotationNodeProvider.GroupNodeId`
    // derives a group id from the ordinal, so the four older values keep
    // theirs.

    /// <summary>Submitted for review by a second person. Lines are fixed until it is approved or returned to draft (runbook C3).</summary>
    InReview,

    /// <summary>
    /// Approved by a second person — a numbered revision (<c>R1</c>,
    /// <c>R2</c>, …, <see cref="Quotation.RevisionNumber"/>) ready to
    /// export and send. Changing a line starts a new draft; the next
    /// approval issues the next revision (runbook C3).
    /// </summary>
    Approved,
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
            // Runbook C3: Draft → In review → Approved (Rn) → Sent. A
            // reviewer returns to Draft; an edit after approval reopens a
            // new Draft (the next approval issues Rn+1).
            [QuotationStatus.Draft] = new HashSet<QuotationStatus> { QuotationStatus.InReview },
            [QuotationStatus.InReview] = new HashSet<QuotationStatus> { QuotationStatus.Draft, QuotationStatus.Approved },
            [QuotationStatus.Approved] = new HashSet<QuotationStatus> { QuotationStatus.Sent, QuotationStatus.Draft },
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
        [QuotationStatus.Draft, QuotationStatus.InReview, QuotationStatus.Approved, QuotationStatus.Sent, QuotationStatus.Accepted, QuotationStatus.Declined];
}
