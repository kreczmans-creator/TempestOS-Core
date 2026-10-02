using System.Text.Json.Nodes;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

// ============================================================================
// `v0.24.0` task S1 — the in-process Xero Accounting API simulator. This
// file is the agreed public surface only (`docs/releases/v0.24.0/Xero
// Technical Design.md` §10.1); task S1 replaces every
// `NotImplementedException` with the behaviour that section specifies. No
// test uses it until then.
// ============================================================================

/// <summary>How a simulated tenant is set up.</summary>
/// <param name="TenantId">The <c>xero-tenant-id</c> every request must carry.</param>
/// <param name="AccessToken">The bearer token every request must carry.</param>
/// <param name="OrganisationName">The organisation's <c>Name</c>/<c>LegalName</c>.</param>
/// <param name="IsDemoCompany">The organisation's <c>IsDemoCompany</c> flag (D7 tests flip it to prove writes to a live organisation are refused).</param>
/// <param name="GrantedScopes">The scopes the token carries; a request to an endpoint none of whose scopes is granted answers 403. <see langword="null"/> grants <c>XeroScopes.Required</c>.</param>
/// <param name="MinuteLimit">Calls per rolling minute before 429 (Xero: 60 per tenant).</param>
/// <param name="DayLimit">Calls per rolling 24 hours before 429 (Xero: 5,000 per tenant).</param>
/// <param name="ConcurrentLimit">Calls in flight at once before 429 (Xero: 5 per tenant).</param>
internal sealed record XeroSimulatorOptions(
    string TenantId = "00000000-0000-0000-0000-00000000d3e0",
    string AccessToken = "simulated-access-token",
    string OrganisationName = "Demo Company (UK)",
    bool IsDemoCompany = true,
    IReadOnlyList<string>? GrantedScopes = null,
    int MinuteLimit = 60,
    int DayLimit = 5000,
    int ConcurrentLimit = 5);

/// <summary>One request the simulator received, and what it answered.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path relative to <c>api.xro/2.0/</c> (for example <c>"Invoices"</c>, <c>"Quotes/{id}/Attachments/P0012-Q-001.pdf"</c>).</param>
/// <param name="Query">The query string, decoded.</param>
/// <param name="JsonBody">The parsed JSON body; <see langword="null"/> for none or a binary (attachment) body.</param>
/// <param name="BinaryBodyLength">The binary body's length, for an attachment upload; <see langword="null"/> otherwise.</param>
/// <param name="IdempotencyKey">The <c>Idempotency-Key</c> header; <see langword="null"/> when absent.</param>
/// <param name="StatusCode">The status code answered.</param>
/// <param name="AtUtc">When it was received, by the simulator's <see cref="TimeProvider"/>.</param>
internal sealed record XeroSimulatedRequest(
    HttpMethod Method, string Path, IReadOnlyDictionary<string, string> Query, JsonNode? JsonBody, long? BinaryBodyLength,
    string? IdempotencyKey, int StatusCode, DateTimeOffset AtUtc);

/// <summary>
/// A request that broke Xero's documented contract or a TempestOS safety
/// rule. Every end-to-end test asserts <see cref="XeroApiSimulator.Violations"/>
/// is empty; the D3/D4/D7 tests assert the specific rule fired.
/// </summary>
/// <param name="Rule">The rule's stable id (for example <c>"D3.invoice-status-not-draft"</c>, <c>"D4.email-endpoint"</c>, <c>"D4.sent-to-contact"</c>, <c>"D7.live-organisation-write"</c>, <c>"contract.required-field"</c>, <c>"contract.quote-transition"</c>, <c>"contract.idempotency-key-reused"</c>, <c>"contract.scope"</c>).</param>
/// <param name="Detail">What was wrong, readably.</param>
/// <param name="Request">The offending request.</param>
internal sealed record XeroContractViolation(string Rule, string Detail, XeroSimulatedRequest Request);

/// <summary>A failure the simulator injects instead of answering normally.</summary>
internal enum XeroFaultKind
{
    /// <summary>Answer 503 without changing state.</summary>
    ServiceUnavailable,

    /// <summary>Answer 429 with <c>X-Rate-Limit-Problem: minute</c> and the given <c>Retry-After</c>.</summary>
    RateLimitedMinute,

    /// <summary>Answer 429 with <c>X-Rate-Limit-Problem: day</c> and the given <c>Retry-After</c>.</summary>
    RateLimitedDay,

    /// <summary>Apply the request (the record is created), then throw <see cref="HttpRequestException"/> — the lost-response case that must never produce a second record.</summary>
    DropResponseAfterCommit,

    /// <summary>Throw <see cref="HttpRequestException"/> without changing state (DNS, refused connection).</summary>
    TransportFailure,

    /// <summary>Answer 401 (token expired or revoked).</summary>
    Unauthorised,
}

/// <summary>One injected fault.</summary>
/// <param name="Kind">What to inject.</param>
/// <param name="PathContains">Only requests whose path contains this (ordinal); <see langword="null"/> for any request.</param>
/// <param name="Times">How many matching requests it applies to.</param>
/// <param name="RetryAfter">The <c>Retry-After</c> to send with a 429.</param>
internal sealed record XeroFault(XeroFaultKind Kind, string? PathContains = null, int Times = 1, TimeSpan? RetryAfter = null);

/// <summary>A document as the simulator holds it.</summary>
/// <param name="Id">Xero's id for it.</param>
/// <param name="Resource">The resource name (<c>"Contacts"</c>, <c>"Quotes"</c>, <c>"Invoices"</c>, <c>"PurchaseOrders"</c>).</param>
/// <param name="Number">Its number (<c>QuoteNumber</c>, <c>InvoiceNumber</c>, <c>PurchaseOrderNumber</c>, <c>ContactNumber</c>).</param>
/// <param name="Status">Its status word.</param>
/// <param name="Body">Its full JSON, as a GET would return it.</param>
/// <param name="Attachments">Its attachments: file name, length, <c>IncludeOnline</c>.</param>
internal sealed record XeroSimulatedDocument(
    string Id, string Resource, string? Number, string Status, JsonObject Body, IReadOnlyList<(string FileName, long Length, bool IncludeOnline)> Attachments);

/// <summary>
/// An in-process Xero Accounting API (<c>https://api.xero.com/api.xro/2.0/</c>)
/// for tests: an <see cref="HttpMessageHandler"/> that holds one tenant's
/// state in memory and validates every request against Xero's documented
/// contract — authentication headers, scope per endpoint, required fields,
/// status rules, Idempotency-Key semantics, rate limits, paging,
/// <c>If-Modified-Since</c> and Xero's error format — plus TempestOS's own
/// D3/D4/D7 rules. Seeded with the UK Demo Company's tax rates and a subset
/// of its chart of accounts.
/// </summary>
internal sealed class XeroApiSimulator : HttpMessageHandler
{
    /// <summary>The base address every client over this handler uses.</summary>
    public static readonly Uri BaseAddress = new("https://api.xero.com/api.xro/2.0/");

    /// <summary>Creates a simulator for one tenant.</summary>
    /// <param name="options">The tenant's set-up; <see langword="null"/> for the defaults.</param>
    /// <param name="timeProvider">The clock rate limits, <c>UpdatedDateUTC</c> and <c>If-Modified-Since</c> use; <see langword="null"/> for the system clock.</param>
    public XeroApiSimulator(XeroSimulatorOptions? options = null, TimeProvider? timeProvider = null)
    {
        Options = options ?? new XeroSimulatorOptions();
        Time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The tenant's set-up.</summary>
    public XeroSimulatorOptions Options { get; }

    /// <summary>The simulator's clock.</summary>
    public TimeProvider Time { get; }

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<XeroSimulatedRequest> Requests => throw new NotImplementedException("Task S1.");

    /// <summary>Every contract or safety-rule violation seen, in order.</summary>
    public IReadOnlyList<XeroContractViolation> Violations => throw new NotImplementedException("Task S1.");

    /// <summary>An <see cref="HttpClient"/> over this handler with <see cref="BaseAddress"/> set.</summary>
    public HttpClient CreateClient() => throw new NotImplementedException("Task S1.");

    /// <summary>Queues <paramref name="fault"/> for the next matching request(s).</summary>
    public void Inject(XeroFault fault) => throw new NotImplementedException($"Task S1 ({fault}).");

    /// <summary>Adds an existing contact, as if entered in Xero by hand; returns its <c>ContactID</c>.</summary>
    public string SeedContact(string name, string? taxNumber = null, string? contactNumber = null) =>
        throw new NotImplementedException($"Task S1 ({name}, {taxNumber}, {contactNumber}).");

    /// <summary>The document <paramref name="id"/> in <paramref name="resource"/>, or <see langword="null"/>.</summary>
    public XeroSimulatedDocument? Find(string resource, string id) => throw new NotImplementedException($"Task S1 ({resource}, {id}).");

    /// <summary>Every document in <paramref name="resource"/>.</summary>
    public IReadOnlyList<XeroSimulatedDocument> All(string resource) => throw new NotImplementedException($"Task S1 ({resource}).");

    /// <summary>The Product Owner approves the invoice or bill in Xero (<c>DRAFT</c> → <c>AUTHORISED</c>) — a back-office act, never reachable through the HTTP surface by TempestOS.</summary>
    public void ApproveInXero(string invoiceId) => throw new NotImplementedException($"Task S1 ({invoiceId}).");

    /// <summary>The invoice or bill is paid in full in Xero (<c>AUTHORISED</c> → <c>PAID</c>, <c>FullyPaidOnDate</c> set).</summary>
    public void PayInXero(string invoiceId, DateOnly paidOn) => throw new NotImplementedException($"Task S1 ({invoiceId}, {paidOn}).");

    /// <summary>The invoice is voided in Xero (<c>AUTHORISED</c> → <c>VOIDED</c>).</summary>
    public void VoidInXero(string invoiceId) => throw new NotImplementedException($"Task S1 ({invoiceId}).");

    /// <summary>The document is deleted in Xero by hand (status <c>DELETED</c>; a later GET by id answers 404 for a contact, the record with <c>DELETED</c> otherwise).</summary>
    public void DeleteInXero(string resource, string id) => throw new NotImplementedException($"Task S1 ({resource}, {id}).");

    /// <summary>The quote is turned into an invoice in Xero by hand (quote <c>ACCEPTED</c> → <c>INVOICED</c>; a new <c>ACCREC</c> draft exists) — the double-invoicing risk X3 must surface.</summary>
    public string ConvertQuoteToInvoiceInXero(string quoteId) => throw new NotImplementedException($"Task S1 ({quoteId}).");

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"Task S1 ({request.Method} {request.RequestUri}).");
}
