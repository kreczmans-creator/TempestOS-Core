namespace Tempest.Core.Quotations;

/// <summary>Why an <see cref="IQuotationService"/> act was refused, or <see cref="None"/> if it was not.</summary>
/// <remarks>
/// A refusal is a first-class answer here, exactly as
/// <c>Tempest.Core.Invoicing.InvoiceRequestRefusal</c> is (`WP 19.5A`,
/// `ADR-0152`).
/// </remarks>
public enum QuotationRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No project is registered under the requested id.</summary>
    ProjectNotFound,

    /// <summary>No quotation is registered under the requested id.</summary>
    QuotationNotFound,

    /// <summary>No line with the requested id exists on this quotation.</summary>
    LineNotFound,

    /// <summary>A line was neither hours-and-a-rate nor a fixed price, or carried both.</summary>
    InvalidLine,

    /// <summary>A line's own rate or fixed price is in a different currency than the quotation itself.</summary>
    LineCurrencyMismatch,

    /// <summary>This quotation's own <see cref="Quotation.Status"/> is not <see cref="QuotationStatus.Draft"/>, so its lines cannot be changed.</summary>
    QuotationNotDraft,

    /// <summary>This quotation carries no lines, so there is nothing to send.</summary>
    NothingToSend,

    /// <summary>This quotation's own <see cref="Quotation.Status"/> does not permit the requested act.</summary>
    TransitionNotPermitted,

    /// <summary>The project this quotation belongs to is Archive — closed 90 days or more ago — and read-only (`WP 19.5C`, Product Owner comment item 6).</summary>
    ProjectArchived,

    /// <summary>A line's own carried deliverable id does not identify a live <c>Deliverable</c> (`WP 20.10E`).</summary>
    DeliverableNotFound,

    /// <summary>Opening, editing, submitting or approving a quotation needs a signed-in person, and nobody is (runbook C3, colour review board B1).</summary>
    NoPrincipalSignedIn,

    /// <summary>The person approving is the one who opened or submitted the quotation, or changed its lines since the last approval — a review needs a second person (runbook C3, colour review board B1). Only while second-person sign-off is on (`ADR-0161`).</summary>
    ReviewerMustDifferFromAuthor,

    /// <summary>Returning a quotation to draft needs the reviewer's own comment (runbook C3).</summary>
    CommentRequired,

    /// <summary>A line's rate-card service code does not name an hourly entry on the project's own pinned rate card (runbook C3).</summary>
    RateCardEntryNotFound,

    /// <summary>
    /// Who opened or who submitted the quotation is not on record (a
    /// submission or a pre-runbook-C3 quotation with no author and no
    /// attributable first revision), so no approver can be shown to be a
    /// second person — the approval is refused rather than assumed
    /// (colour review board B1). Only while second-person sign-off is on
    /// (`ADR-0161`): with it off nobody needs to be shown to differ.
    /// </summary>
    AuthorUnknown,
}

/// <summary>The outcome of an <see cref="IQuotationService"/> act: either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="QuotationRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Quotation">The quotation acted on, when it could be resolved.</param>
public sealed record QuotationResult(QuotationRefusal Refusal, string? Reason, Quotation? Quotation)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == QuotationRefusal.None;
}
