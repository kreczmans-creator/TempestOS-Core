namespace Tempest.Core.Invoicing.Xero.Contacts;

// ============================================================================
// `v0.24.0` X2: customers and suppliers linked to Xero contacts by
// `ContactID`. Contracts only; see
// `docs/releases/v0.24.0/Xero Technical Design.md` §6.
// ============================================================================

/// <summary>One Xero contact offered as a match for a TempestOS organisation, for the Product Owner to confirm.</summary>
/// <param name="ContactId">Xero's <c>ContactID</c>.</param>
/// <param name="Name">The contact's name.</param>
/// <param name="TaxNumber">The contact's VAT number (Xero <c>TaxNumber</c>); <see langword="null"/> when not set.</param>
/// <param name="ContactNumber">Xero's <c>ContactNumber</c> (an external system's id); <see langword="null"/> when not set.</param>
/// <param name="EmailAddress">The contact's email address; <see langword="null"/> when not set.</param>
/// <param name="IsCustomer">Xero's <c>IsCustomer</c> — set by Xero itself once a sales invoice exists; never set by TempestOS.</param>
/// <param name="IsSupplier">Xero's <c>IsSupplier</c> — set by Xero itself once a bill exists; never set by TempestOS.</param>
/// <param name="ContactStatus">Xero's status word (<c>"ACTIVE"</c>, <c>"ARCHIVED"</c>, <c>"GDPRREQUEST"</c>); only <c>ACTIVE</c> may be linked.</param>
/// <param name="MatchedOn">Why it was offered: <c>"vat-number"</c>, <c>"contact-number"</c>, <c>"exact-name"</c> or <c>"similar-name"</c>, strongest first.</param>
public sealed record XeroContactCandidate(
    string ContactId, string Name, string? TaxNumber, string? ContactNumber, string? EmailAddress,
    bool IsCustomer, bool IsSupplier, string ContactStatus, string MatchedOn);

/// <summary>The billing details Xero holds for a linked contact, read back and never pushed (source-of-truth table).</summary>
/// <param name="ContactId">Xero's <c>ContactID</c>.</param>
/// <param name="Name">The contact's name in Xero.</param>
/// <param name="TaxNumber">The VAT number; <see langword="null"/> when not set.</param>
/// <param name="BillingAddress">The <c>POBOX</c> (billing) address lines, falling back to <c>STREET</c>; empty when none.</param>
/// <param name="EmailAddress">The email address invoices go to; <see langword="null"/> when not set.</param>
/// <param name="SalesPaymentTermsDays">The sales payment terms as days after invoice date, when Xero's terms are of a days-based type; <see langword="null"/> otherwise.</param>
/// <param name="SalesPaymentTermsType">Xero's own sales payment-terms type word, verbatim (for example <c>"DAYSAFTERBILLDATE"</c>, <c>"OFFOLLOWINGMONTH"</c>); <see langword="null"/> when none.</param>
/// <param name="ReadAtUtc">When it was read.</param>
public sealed record XeroContactDetails(
    string ContactId, string Name, string? TaxNumber, IReadOnlyList<string> BillingAddress, string? EmailAddress,
    int? SalesPaymentTermsDays, string? SalesPaymentTermsType, DateTimeOffset ReadAtUtc);

/// <summary>
/// Links TempestOS organisations to Xero contacts (X2). A link is made only
/// by the Product Owner's confirmation (<see cref="LinkExistingAsync"/>) or
/// an explicit create (<see cref="CreateAsync"/>) — never silently by a
/// push, so a document for an unlinked organisation is
/// <see cref="Sync.XeroPushOutcome.Blocked"/> until it is linked.
/// </summary>
public interface IXeroContactLinker
{
    /// <summary>The existing link for <paramref name="organisationReference"/> in the connected tenant, or <see langword="null"/> — no network call.</summary>
    Task<Sync.XeroLink?> FindLinkAsync(string organisationReference, CancellationToken cancellationToken = default);

    /// <summary>Xero contacts that may be <paramref name="organisationReference"/>, matched by VAT number, <c>ContactNumber</c> and name (<c>GET /Contacts?searchTerm=…</c>), strongest first.</summary>
    Task<ConnectorResult<IReadOnlyList<XeroContactCandidate>>> FindCandidatesAsync(string organisationReference, CancellationToken cancellationToken = default);

    /// <summary>Links <paramref name="organisationReference"/> to the existing Xero contact <paramref name="contactId"/>, after reading it back to confirm it exists and is <c>ACTIVE</c>. Writes nothing to Xero.</summary>
    Task<ConnectorResult<Sync.XeroLink>> LinkExistingAsync(string organisationReference, string contactId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Xero contact for <paramref name="organisationReference"/>
    /// (<c>PUT /Contacts</c>: <c>Name</c>, <c>ContactNumber</c> = the
    /// organisation's reference, VAT and company number when known) and
    /// links it. Before creating, looks the contact up by
    /// <c>ContactNumber</c>, so a lost response never creates a second one.
    /// </summary>
    Task<ConnectorResult<Sync.XeroLink>> CreateAsync(string organisationReference, CancellationToken cancellationToken = default);

    /// <summary>Reads the linked contact's billing details from Xero (and caches them with the link's read time).</summary>
    Task<ConnectorResult<XeroContactDetails>> ReadDetailsAsync(string organisationReference, CancellationToken cancellationToken = default);
}
