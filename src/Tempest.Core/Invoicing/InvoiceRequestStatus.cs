namespace Tempest.Core.Invoicing;

/// <summary>
/// An <see cref="InvoiceRequest"/>'s own lifecycle position — the vocabulary
/// the `WP 19.1A` row names exactly (`ADR-0151`), not the eight canonical
/// states (`ADR-0074`): a request is never "Released" or "Approved", it is
/// sent to an accounting system and read back.
/// </summary>
public enum InvoiceRequestStatus
{
    /// <summary>Built, not yet sent.</summary>
    Draft,

    /// <summary>Being sent — the transient state <see cref="InvoicingService.SendAsync"/> holds a request in while its own connector call is outstanding.</summary>
    Sending,

    /// <summary>Sent; the connector's own external id is known.</summary>
    Sent,

    /// <summary>The accounting system reports the invoice approved or authorised.</summary>
    Accepted,

    /// <summary>The accounting system refused the call outright, with a reason.</summary>
    Rejected,

    /// <summary>Voided — either locally, while still <see cref="Draft"/> or <see cref="Rejected"/>, or read back once voided at the provider.</summary>
    Voided,

    /// <summary>Sent, but the response was lost — neither success nor failure is known until reconciliation resolves it.</summary>
    Unknown,

    /// <summary>The connector's own stored token is expired or revoked; nothing further is sent until the operator re-authorises.</summary>
    Reauthorise,

    /// <summary>
    /// Part of this Kind's own vocabulary (the `WP 19.1A` row's own words),
    /// naming what <see cref="InvoicingService.SendAsync"/> found — the
    /// connector could not be reached. Never itself a stored status: that
    /// method's own remarks explain why a request in this condition is
    /// left <see cref="Draft"/> rather than moved here.
    /// </summary>
    Unavailable,
}

/// <summary>
/// The permitted <see cref="InvoiceRequestStatus"/> transition table — the
/// sole enforcement point <see cref="InvoicingService"/>'s own status-moving
/// acts check against, mirroring <c>Tempest.Core.Evidence.EvidenceStatusTransitions</c>
/// exactly (`WP 19.1A`, `ADR-0151`).
/// </summary>
internal static class InvoiceRequestStatusTransitions
{
    private static readonly IReadOnlyDictionary<InvoiceRequestStatus, IReadOnlySet<InvoiceRequestStatus>> Permitted =
        new Dictionary<InvoiceRequestStatus, IReadOnlySet<InvoiceRequestStatus>>
        {
            // VoidAsync: only a request that never reached the provider —
            // one that did is voided there, and read back (Sent/Accepted
            // -> Voided below).
            [InvoiceRequestStatus.Draft] = new HashSet<InvoiceRequestStatus> { InvoiceRequestStatus.Sending, InvoiceRequestStatus.Voided },

            // SendAsync's own five connector outcomes, verbatim: Ok -> Sent,
            // Rejected -> Rejected, Reauthorise -> Reauthorise, Unavailable
            // -> Draft (SendAsync's own remarks: never -> Unavailable),
            // Unknown -> Unknown.
            [InvoiceRequestStatus.Sending] = new HashSet<InvoiceRequestStatus>
            {
                InvoiceRequestStatus.Sent, InvoiceRequestStatus.Rejected, InvoiceRequestStatus.Reauthorise,
                InvoiceRequestStatus.Draft, InvoiceRequestStatus.Unknown,
            },

            // ReconcileAsync, for Sent/Accepted: Accepted when the provider
            // reports approved/authorised; Voided when voided there.
            [InvoiceRequestStatus.Sent] = new HashSet<InvoiceRequestStatus> { InvoiceRequestStatus.Accepted, InvoiceRequestStatus.Voided },
            [InvoiceRequestStatus.Accepted] = new HashSet<InvoiceRequestStatus> { InvoiceRequestStatus.Voided },

            // VoidAsync: the other request that never reached the provider.
            [InvoiceRequestStatus.Rejected] = new HashSet<InvoiceRequestStatus> { InvoiceRequestStatus.Voided },

            // ReconcileAsync, for Unknown: found by reference -> Sent (links
            // applied); not found -> Draft.
            [InvoiceRequestStatus.Unknown] = new HashSet<InvoiceRequestStatus> { InvoiceRequestStatus.Sent, InvoiceRequestStatus.Draft },

            // Terminal: no act in this Work Package's own scope clears a
            // request the accounting system voided.
            [InvoiceRequestStatus.Voided] = new HashSet<InvoiceRequestStatus>(),

            // Terminal, in this Work Package's own scope: "nothing is sent
            // until the operator re-authorises" (the row's own words) names
            // a manual act outside this part's scope — the OAuth
            // re-authorisation flow itself is `WP 19.1A` parts 2/3. A later
            // Work Package that lets a re-authorised request retry adds the
            // edge back to Sending here, rather than opening a second table.
            [InvoiceRequestStatus.Reauthorise] = new HashSet<InvoiceRequestStatus>(),

            // Isolated, deliberately: no transition in this Work Package's
            // own scope ever leads here or away from here — see
            // InvoiceRequestStatus.Unavailable's own remarks. Declared so
            // the vocabulary is complete and this table's own exhaustive
            // test finds the gap named rather than an undeclared key.
            [InvoiceRequestStatus.Unavailable] = new HashSet<InvoiceRequestStatus>(),
        };

    /// <summary>Whether transitioning from <paramref name="from"/> to <paramref name="to"/> is permitted. A same-to-same request is not special-cased — permitted only where the table itself lists it.</summary>
    public static bool IsPermitted(InvoiceRequestStatus from, InvoiceRequestStatus to) =>
        Permitted[from].Contains(to);

    /// <summary>Every <see cref="InvoiceRequestStatus"/> value, for a test to walk exhaustively.</summary>
    public static IReadOnlyList<InvoiceRequestStatus> AllStatuses { get; } =
    [
        InvoiceRequestStatus.Draft, InvoiceRequestStatus.Sending, InvoiceRequestStatus.Sent, InvoiceRequestStatus.Accepted,
        InvoiceRequestStatus.Rejected, InvoiceRequestStatus.Voided, InvoiceRequestStatus.Unknown,
        InvoiceRequestStatus.Reauthorise, InvoiceRequestStatus.Unavailable,
    ];
}
