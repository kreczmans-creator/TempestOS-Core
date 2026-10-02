using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.Xero.Api;

// `v0.24.0` task X2: the Contacts resource (design §3, §5). Reads (search,
// lookups by VAT number and ContactNumber, read by ContactID) and the two
// writes TempestOS ever makes to a contact: a create (PUT Contacts) and,
// under Q7, filling an empty ContactNumber (POST Contacts/{id}). Nothing
// else about a contact is ever pushed — billing details are Xero's (D6).
public sealed partial class XeroAccountingApi
{
    /// <summary>Xero's page size for <c>GET Contacts</c> with <c>page=</c> (§6.5).</summary>
    public const int ContactsPageSize = 100;

    /// <summary>The most pages one contact search reads (500 contacts) — a search that wide is not a match any more, and the rate limit matters more.</summary>
    public const int MaximumContactPages = 5;

    /// <summary>Xero's documented maximum length of <c>ContactNumber</c>.</summary>
    public const int MaximumContactNumberLength = 50;

    /// <summary>
    /// Searches contacts with Xero's <c>searchTerm</c> (<c>GET Contacts?searchTerm=…&amp;page=n</c>),
    /// which matches <c>Name</c>, <c>FirstName</c>, <c>LastName</c>,
    /// <c>ContactNumber</c> and <c>EmailAddress</c> (§3). Pages until a short
    /// page, at most <see cref="MaximumContactPages"/>.
    /// </summary>
    /// <param name="searchTerm">The text to search for.</param>
    /// <param name="includeArchived">Whether archived contacts are included (Xero hides them by default).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<XeroApiResult<IReadOnlyList<XeroWireContact>>> SearchContactsAsync(
        string searchTerm, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);

        return ListContactsAsync([new("searchTerm", searchTerm.Trim())], includeArchived, cancellationToken);
    }

    /// <summary>The contacts whose <c>TaxNumber</c> (VAT number) is exactly <paramref name="taxNumber"/> (<c>GET Contacts?where=TaxNumber=="…"</c>).</summary>
    /// <param name="taxNumber">The VAT number, as it is expected to be stored in Xero.</param>
    /// <param name="includeArchived">Whether archived contacts are included.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<XeroApiResult<IReadOnlyList<XeroWireContact>>> FindContactsByTaxNumberAsync(
        string taxNumber, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taxNumber);

        return ListContactsAsync([new("where", WhereEquals("TaxNumber", taxNumber.Trim()))], includeArchived, cancellationToken);
    }

    /// <summary>
    /// The contacts whose <c>ContactNumber</c> is exactly
    /// <paramref name="contactNumber"/> (<c>GET Contacts?where=ContactNumber=="…"</c>)
    /// — the natural-key lookup before a create and after a lost response
    /// (§6.4). A filter rather than <c>GET Contacts/{ContactNumber}</c>, so a
    /// number held by two contacts is seen as two, and a number that happens
    /// to look like a <c>ContactID</c> is never mistaken for one.
    /// </summary>
    /// <param name="contactNumber">The number (TempestOS's customer code).</param>
    /// <param name="includeArchived">Whether archived contacts are included.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<XeroApiResult<IReadOnlyList<XeroWireContact>>> FindContactsByContactNumberAsync(
        string contactNumber, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactNumber);

        return ListContactsAsync([new("where", WhereEquals("ContactNumber", contactNumber.Trim()))], includeArchived, cancellationToken);
    }

    /// <summary>Reads one contact by its <c>ContactID</c> (<c>GET Contacts/{ContactID}</c>); a deleted contact answers <see cref="XeroApiResult{T}.NotFound"/>.</summary>
    /// <param name="contactId">Xero's <c>ContactID</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireContact>> GetContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);

        var result = await GetAsync<XeroWireContactsEnvelope>($"Contacts/{Uri.EscapeDataString(contactId.Trim())}", cancellationToken: cancellationToken).ConfigureAwait(false);
        return SingleContact(result, "read");
    }

    /// <summary>
    /// Creates a contact (<c>PUT Contacts</c>). The write model carries only
    /// what TempestOS may create a contact with (§3) — never
    /// <c>IsCustomer</c>/<c>IsSupplier</c>, which Xero sets itself, and never
    /// billing details, which are Xero's.
    /// </summary>
    /// <param name="contact">The contact to create.</param>
    /// <param name="idempotencyKey">The fixed key for this create; a repeat replays Xero's cached answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireContact>> CreateContactAsync(
        XeroWireContactCreate contact, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentException.ThrowIfNullOrWhiteSpace(contact.Name);

        if (contact.ContactNumber is { Length: > MaximumContactNumberLength })
            return Failure<XeroWireContact>(ConnectorOutcome.Rejected, null, $"ContactNumber '{contact.ContactNumber}' is longer than Xero's {MaximumContactNumberLength} characters.");

        var result = await PutJsonAsync<XeroWireContactsEnvelope>("Contacts", new XeroWireContactsWriteEnvelope<XeroWireContactCreate>([contact]), idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleContact(result, "create");
    }

    /// <summary>
    /// Sets the <c>ContactNumber</c> of an existing contact
    /// (<c>POST Contacts/{ContactID}</c> carrying only <c>ContactID</c> and
    /// <c>ContactNumber</c>) — Q7: TempestOS writes its customer code there
    /// only when the field is empty. The only update TempestOS ever makes to
    /// a contact.
    /// </summary>
    /// <param name="contactId">Xero's <c>ContactID</c>.</param>
    /// <param name="contactNumber">The number to write (at most <see cref="MaximumContactNumberLength"/> characters).</param>
    /// <param name="idempotencyKey">The fixed key for this write.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<XeroApiResult<XeroWireContact>> SetContactNumberAsync(
        string contactId, string contactNumber, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactNumber);

        if (contactNumber.Length > MaximumContactNumberLength)
            return Failure<XeroWireContact>(ConnectorOutcome.Rejected, null, $"ContactNumber '{contactNumber}' is longer than Xero's {MaximumContactNumberLength} characters.");

        var body = new XeroWireContactsWriteEnvelope<XeroWireContactNumberUpdate>([new XeroWireContactNumberUpdate(contactId.Trim(), contactNumber)]);
        var result = await PostJsonAsync<XeroWireContactsEnvelope>($"Contacts/{Uri.EscapeDataString(contactId.Trim())}", body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return SingleContact(result, "update");
    }

    /// <summary>A Xero <c>where=</c> equality on a string field, the value quoted and its <c>"</c> and <c>\</c> escaped.</summary>
    /// <param name="field">The field name.</param>
    /// <param name="value">The value.</param>
    internal static string WhereEquals(string field, string value)
    {
        var escaped = new StringBuilder(value.Length + 2);
        foreach (var c in value)
        {
            if (c is '"' or '\\')
                escaped.Append('\\');
            escaped.Append(c);
        }

        return $"{field}==\"{escaped}\"";
    }

    private async Task<XeroApiResult<IReadOnlyList<XeroWireContact>>> ListContactsAsync(
        IReadOnlyList<KeyValuePair<string, string?>> filter, bool includeArchived, CancellationToken cancellationToken)
    {
        var contacts = new List<XeroWireContact>();
        XeroApiResult<XeroWireContactsEnvelope>? last = null;

        for (var page = 1; page <= MaximumContactPages; page++)
        {
            IEnumerable<KeyValuePair<string, string?>> query =
            [
                .. filter,
                new("includeArchived", includeArchived ? "true" : null),
                new("page", page.ToString(CultureInfo.InvariantCulture)),
            ];

            last = await GetAsync<XeroWireContactsEnvelope>("Contacts", query, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (last.Outcome != ConnectorOutcome.Ok)
                return Retype<XeroWireContactsEnvelope, IReadOnlyList<XeroWireContact>>(last);

            var items = last.Value!.Contacts ?? [];
            contacts.AddRange(items.Where(c => !string.IsNullOrWhiteSpace(c.ContactID)));
            if (items.Count < ContactsPageSize)
                break;
        }

        return new XeroApiResult<IReadOnlyList<XeroWireContact>>(ConnectorOutcome.Ok, contacts, last?.HttpStatus, null, []);
    }

    private static XeroApiResult<XeroWireContact> SingleContact(XeroApiResult<XeroWireContactsEnvelope> result, string act)
    {
        if (result.Outcome != ConnectorOutcome.Ok)
            return Retype<XeroWireContactsEnvelope, XeroWireContact>(result);

        var contact = result.Value!.Contacts?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.ContactID));
        return contact is null
            ? Failure<XeroWireContact>(ConnectorOutcome.Unknown, result.HttpStatus, $"Xero answered the contact {act} with no contact; whether it took effect is unknown.")
            : new XeroApiResult<XeroWireContact>(ConnectorOutcome.Ok, contact, result.HttpStatus, null, []);
    }
}

/// <summary>Xero's <c>{ "Contacts": [ … ] }</c> envelope, as read.</summary>
/// <param name="Contacts">The contacts.</param>
public sealed record XeroWireContactsEnvelope([property: JsonPropertyName("Contacts")] IReadOnlyList<XeroWireContact>? Contacts);

/// <summary>Xero's <c>{ "Contacts": [ … ] }</c> envelope, as written (one contact per request, §3).</summary>
/// <typeparam name="T">The write model.</typeparam>
/// <param name="Contacts">The contacts.</param>
public sealed record XeroWireContactsWriteEnvelope<T>([property: JsonPropertyName("Contacts")] IReadOnlyList<T> Contacts);

/// <summary>
/// A contact as TempestOS creates it (<c>PUT Contacts</c>, §3): exactly the
/// identifying fields — never <c>IsCustomer</c>/<c>IsSupplier</c> (Xero
/// sets them once a sales invoice or bill exists), never an address or
/// payment terms (Xero is their master).
/// </summary>
/// <param name="Name">The organisation's name (unique among Xero's active contacts).</param>
/// <param name="ContactNumber">TempestOS's customer code — the natural key a lost response is reconciled by (§6.4); at most 50 characters.</param>
/// <param name="TaxNumber">The VAT number, when TempestOS records one.</param>
/// <param name="CompanyNumber">The company registration number, when recorded.</param>
/// <param name="EmailAddress">The organisation's main email address, when recorded.</param>
public sealed record XeroWireContactCreate(
    [property: JsonPropertyName("Name")] string Name,
    [property: JsonPropertyName("ContactNumber")] string? ContactNumber,
    [property: JsonPropertyName("TaxNumber")] string? TaxNumber = null,
    [property: JsonPropertyName("CompanyNumber")] string? CompanyNumber = null,
    [property: JsonPropertyName("EmailAddress")] string? EmailAddress = null);

/// <summary>The one update TempestOS makes to an existing contact (Q7): its empty <c>ContactNumber</c> set to the customer code.</summary>
/// <param name="ContactID">Xero's <c>ContactID</c>.</param>
/// <param name="ContactNumber">The customer code.</param>
public sealed record XeroWireContactNumberUpdate(
    [property: JsonPropertyName("ContactID")] string ContactID,
    [property: JsonPropertyName("ContactNumber")] string ContactNumber);

/// <summary>A Xero contact, as Xero answers it (the fields X2 reads).</summary>
/// <param name="ContactID">Xero's <c>ContactID</c>.</param>
/// <param name="ContactNumber">The external system's id (TempestOS's customer code, when TempestOS wrote it).</param>
/// <param name="ContactStatus"><c>ACTIVE</c>, <c>ARCHIVED</c> or <c>GDPRREQUEST</c>.</param>
/// <param name="Name">The contact's name.</param>
/// <param name="FirstName">A person's first name, when the contact is a person.</param>
/// <param name="LastName">A person's last name.</param>
/// <param name="EmailAddress">The email address.</param>
/// <param name="TaxNumber">The VAT number.</param>
/// <param name="CompanyNumber">The company registration number.</param>
/// <param name="IsCustomer">Set by Xero once a sales invoice exists.</param>
/// <param name="IsSupplier">Set by Xero once a bill exists.</param>
/// <param name="Addresses">The contact's addresses (<c>POBOX</c> is the billing address, <c>STREET</c> the delivery one).</param>
/// <param name="PaymentTerms">The contact's own payment terms, when set.</param>
/// <param name="UpdatedDateUTC">When Xero last changed it (Microsoft JSON date).</param>
public sealed record XeroWireContact(
    [property: JsonPropertyName("ContactID")] string? ContactID,
    [property: JsonPropertyName("ContactNumber")] string? ContactNumber = null,
    [property: JsonPropertyName("ContactStatus")] string? ContactStatus = null,
    [property: JsonPropertyName("Name")] string? Name = null,
    [property: JsonPropertyName("FirstName")] string? FirstName = null,
    [property: JsonPropertyName("LastName")] string? LastName = null,
    [property: JsonPropertyName("EmailAddress")] string? EmailAddress = null,
    [property: JsonPropertyName("TaxNumber")] string? TaxNumber = null,
    [property: JsonPropertyName("CompanyNumber")] string? CompanyNumber = null,
    [property: JsonPropertyName("IsCustomer")] bool? IsCustomer = null,
    [property: JsonPropertyName("IsSupplier")] bool? IsSupplier = null,
    [property: JsonPropertyName("Addresses")] IReadOnlyList<XeroWireContactAddress>? Addresses = null,
    [property: JsonPropertyName("PaymentTerms")] XeroWirePaymentTerms? PaymentTerms = null,
    [property: JsonPropertyName("UpdatedDateUTC")] string? UpdatedDateUTC = null);

/// <summary>One address on a Xero contact.</summary>
/// <param name="AddressType"><c>POBOX</c> (billing) or <c>STREET</c> (delivery).</param>
/// <param name="AddressLine1">Line 1.</param>
/// <param name="AddressLine2">Line 2.</param>
/// <param name="AddressLine3">Line 3.</param>
/// <param name="AddressLine4">Line 4.</param>
/// <param name="City">The town or city.</param>
/// <param name="Region">The region or county.</param>
/// <param name="PostalCode">The postcode.</param>
/// <param name="Country">The country, as entered.</param>
/// <param name="AttentionTo">Who it is for.</param>
public sealed record XeroWireContactAddress(
    [property: JsonPropertyName("AddressType")] string? AddressType,
    [property: JsonPropertyName("AddressLine1")] string? AddressLine1 = null,
    [property: JsonPropertyName("AddressLine2")] string? AddressLine2 = null,
    [property: JsonPropertyName("AddressLine3")] string? AddressLine3 = null,
    [property: JsonPropertyName("AddressLine4")] string? AddressLine4 = null,
    [property: JsonPropertyName("City")] string? City = null,
    [property: JsonPropertyName("Region")] string? Region = null,
    [property: JsonPropertyName("PostalCode")] string? PostalCode = null,
    [property: JsonPropertyName("Country")] string? Country = null,
    [property: JsonPropertyName("AttentionTo")] string? AttentionTo = null);

/// <summary>A contact's payment terms: <c>Sales</c> (what the contact pays TempestOS's invoices on) and <c>Bills</c>.</summary>
/// <param name="Sales">Terms on sales invoices to the contact.</param>
/// <param name="Bills">Terms on bills from the contact.</param>
public sealed record XeroWirePaymentTerms(
    [property: JsonPropertyName("Sales")] XeroWirePaymentTerm? Sales = null,
    [property: JsonPropertyName("Bills")] XeroWirePaymentTerm? Bills = null);

/// <summary>One payment term: a day number and how Xero reads it.</summary>
/// <param name="Day">The day: a number of days for <c>DAYSAFTERBILLDATE</c>/<c>DAYSAFTERBILLMONTH</c>, a day of the month for <c>OFCURRENTMONTH</c>/<c>OFFOLLOWINGMONTH</c>.</param>
/// <param name="Type">Xero's type word, verbatim.</param>
public sealed record XeroWirePaymentTerm(
    [property: JsonPropertyName("Day")] int? Day = null,
    [property: JsonPropertyName("Type")] string? Type = null);
