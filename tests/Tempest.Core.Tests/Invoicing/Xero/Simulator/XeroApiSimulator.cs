using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Tempest.Core.Invoicing.Xero;

namespace Tempest.Core.Tests.Invoicing.Xero.Simulator;

// ============================================================================
// `v0.24.0` task S1 — the in-process Xero Accounting API simulator
// (`docs/releases/v0.24.0/Xero Technical Design.md` §10.1). This file holds
// the agreed public surface (§12) and the request pipeline: authentication,
// rate limits, scopes, Idempotency-Key, the TempestOS safety detectors
// (D3/D4/D7), fault injection, the request and violation logs, and the
// back-office acts. Routing and read handlers are in
// `XeroApiSimulator.Routing.cs`; document validation and storage in
// `XeroApiSimulator.Documents.cs`; wire helpers in `XeroApiSimulator.Wire.cs`;
// the UK Demo Company seed in `SimulatorSeed.UkDemo.cs`.
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
/// <param name="StatusCode">The status code answered; <c>0</c> when an injected <see cref="XeroFaultKind.TransportFailure"/> meant nothing was answered.</param>
/// <param name="AtUtc">When it was received, by the simulator's <see cref="TimeProvider"/>.</param>
internal sealed record XeroSimulatedRequest(
    HttpMethod Method, string Path, IReadOnlyDictionary<string, string> Query, JsonNode? JsonBody, long? BinaryBodyLength,
    string? IdempotencyKey, int StatusCode, DateTimeOffset AtUtc);

/// <summary>
/// A request that broke Xero's documented contract or a TempestOS safety
/// rule. Every end-to-end test asserts <see cref="XeroApiSimulator.Violations"/>
/// is empty; the D3/D4/D7 tests assert the specific rule fired.
/// </summary>
/// <param name="Rule">The rule's stable id (for example <c>"D3.invoice-status-not-draft"</c>, <c>"D4.email-endpoint"</c>, <c>"D4.sent-to-contact"</c>, <c>"D7.live-organisation-write"</c>, <c>"contract.required-field"</c>, <c>"contract.quote-transition"</c>, <c>"contract.idempotency-key-reused"</c>, <c>"contract.scope"</c>). The full list is <see cref="XeroSimulatorRules"/>.</param>
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
/// The stable ids of every rule <see cref="XeroApiSimulator"/> records in
/// <see cref="XeroApiSimulator.Violations"/>. <c>D3.*</c>, <c>D4.*</c>,
/// <c>D7.*</c> and <c>safety.*</c> are TempestOS's own rules (`ADR-0162`);
/// <c>contract.*</c> are Xero's documented API contract as §10.1 of the
/// technical design states it.
/// </summary>
internal static class XeroSimulatorRules
{
    /// <summary>An invoice or bill written with a status other than <c>DRAFT</c> (create) or <c>DELETED</c> (update) — <c>SUBMITTED</c>, <c>AUTHORISED</c>, <c>VOIDED</c> (D3).</summary>
    public const string InvoiceStatusNotDraft = "D3.invoice-status-not-draft";

    /// <summary>A purchase order written with a status other than <c>DRAFT</c>/<c>DELETED</c> (D3, Q2).</summary>
    public const string PurchaseOrderStatusNotDraft = "D3.po-status-not-draft";

    /// <summary>A request to an <c>…/Email</c> endpoint (D4).</summary>
    public const string EmailEndpoint = "D4.email-endpoint";

    /// <summary>A body carrying <c>SentToContact: true</c> (D4).</summary>
    public const string SentToContact = "D4.sent-to-contact";

    /// <summary>Any write while the organisation is not the Demo Company (D7).</summary>
    public const string LiveOrganisationWrite = "D7.live-organisation-write";

    /// <summary>A write to an endpoint outside TempestOS's write allow-list (payments, bank transactions, settings, …).</summary>
    public const string WriteAllowList = "safety.write-allow-list";

    /// <summary>A document's contact given by <c>Name</c> alone, so Xero matched or created it silently (`ADR-0162` §1: link by <c>ContactID</c>).</summary>
    public const string ContactByName = "safety.contact-by-name";

    /// <summary>A required field missing or empty.</summary>
    public const string RequiredField = "contract.required-field";

    /// <summary>A field longer than Xero allows.</summary>
    public const string FieldLength = "contract.field-length";

    /// <summary>A field of the wrong type or format (a date that does not parse, a non-numeric amount, an unknown enum word).</summary>
    public const string FieldFormat = "contract.field-format";

    /// <summary>A <c>ContactID</c> that does not exist, is deleted or archived.</summary>
    public const string UnknownContact = "contract.unknown-contact";

    /// <summary>A <c>TaxType</c> that does not exist, is not <c>ACTIVE</c>, or cannot apply to the document's side (sales/purchases).</summary>
    public const string TaxType = "contract.tax-type";

    /// <summary>An <c>AccountCode</c> that does not exist, is not <c>ACTIVE</c>, or is a bank account.</summary>
    public const string AccountCode = "contract.account-code";

    /// <summary>A number (<c>QuoteNumber</c>, ACCREC <c>InvoiceNumber</c>, <c>PurchaseOrderNumber</c>) or contact <c>Name</c> already in use.</summary>
    public const string Duplicate = "contract.duplicate";

    /// <summary>A quote status change outside Xero's transition table.</summary>
    public const string QuoteTransition = "contract.quote-transition";

    /// <summary>An invoice or bill status change Xero refuses.</summary>
    public const string InvoiceTransition = "contract.invoice-transition";

    /// <summary>A purchase order status change Xero refuses (for example <c>DELETED</c> once <c>BILLED</c>).</summary>
    public const string PurchaseOrderTransition = "contract.po-transition";

    /// <summary>A content edit to a document whose status no longer allows it.</summary>
    public const string NotEditable = "contract.not-editable";

    /// <summary>An <c>Idempotency-Key</c> reused with a different request.</summary>
    public const string IdempotencyKeyReused = "contract.idempotency-key-reused";

    /// <summary>An <c>Idempotency-Key</c> longer than 128 characters.</summary>
    public const string IdempotencyKeyTooLong = "contract.idempotency-key-too-long";

    /// <summary>A <c>PUT</c>/<c>POST</c> without an <c>Idempotency-Key</c> (design §3: every write carries one).</summary>
    public const string IdempotencyKeyMissing = "contract.idempotency-key-missing";

    /// <summary>A request to an endpoint none of whose scopes the token carries.</summary>
    public const string Scope = "contract.scope";

    /// <summary>A request without <c>Accept: application/json</c> (Xero would answer XML).</summary>
    public const string AcceptJson = "contract.accept-json";

    /// <summary>A method the endpoint does not support (for example <c>PUT Invoices/{id}</c>).</summary>
    public const string Method = "contract.method";

    /// <summary>A body that is not the JSON the endpoint expects.</summary>
    public const string MalformedBody = "contract.malformed-body";

    /// <summary>A <c>where=</c> clause the simulator (and TempestOS's documented usage) does not understand.</summary>
    public const string Where = "contract.where";

    /// <summary>An attachment upload that is empty, too large, or one too many for its document.</summary>
    public const string Attachment = "contract.attachment";
}

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
/// <remarks>
/// <para>
/// <b>Order of checks</b> for every request: injected fault; concurrency
/// (429 <c>concurrent</c>); bearer token (401); <c>xero-tenant-id</c> (403);
/// minute and day limits (429 with <c>Retry-After</c>); routing (404/405);
/// scope (403 <c>AuthorizationUnsuccessful</c>); for a write,
/// <c>Idempotency-Key</c> (replay, or 400 for reuse or length) and then the
/// safety detectors and the endpoint's own validation (400 in Xero's
/// <c>ValidationException</c> shape). Every 400/403/405/413 the simulator
/// answers is also recorded as a <see cref="XeroContractViolation"/>; a 404
/// is not (a lookup that finds nothing is ordinary).
/// </para>
/// <para>
/// <b>Idempotency-Key.</b> Every write the simulator processes (any answer
/// but a 5xx; a request refused before processing — 401, 403, 429 — is not
/// cached) is remembered per key with its fingerprint: method, path, query
/// and the SHA-256 of the body. A repeat with the same fingerprint replays
/// the first answer byte for byte and changes nothing; a different
/// fingerprint is 400. Keys do not expire within a simulator's life.
/// </para>
/// <para>
/// <b>Attachments.</b> <c>POST …/Attachments/{FileName}</c> replaces an
/// attachment of the same name; <c>PUT</c> adds another even when the name
/// is taken (Xero's behaviour here is unconfirmed; design §3 checks it in
/// the live smoke test). <c>IncludeOnline</c> is honoured on invoices only.
/// </para>
/// <para>
/// <b>Contacts by name.</b> A document whose <c>Contact</c> carries only a
/// <c>Name</c> is matched to an active contact of that name or creates one,
/// as Xero does, and records <c>safety.contact-by-name</c>.
/// </para>
/// <para>
/// <b>Detectors, not guards.</b> A D3/D4/D7 breach is recorded and then
/// applied as real Xero would apply it, so a test without
/// <c>XeroWriteSafetyHandler</c> can see the damage the handler prevents.
/// </para>
/// </remarks>
internal sealed partial class XeroApiSimulator : HttpMessageHandler
{
    /// <summary>The base address every client over this handler uses.</summary>
    public static readonly Uri BaseAddress = new("https://api.xero.com/api.xro/2.0/");

    private const int AppMinuteLimit = 10_000;

    private readonly object _sync = new();
    private readonly List<XeroSimulatedRequest> _requests = [];
    private readonly List<XeroContractViolation> _violations = [];
    private readonly List<FaultSlot> _faults = [];
    private readonly Dictionary<string, CachedResponse> _idempotency = new(StringComparer.Ordinal);
    private readonly Queue<DateTimeOffset> _minuteCalls = new();
    private readonly Queue<DateTimeOffset> _dayCalls = new();
    private readonly HashSet<string> _grantedScopes;
    private int _inFlight;
    private TaskCompletionSource? _hold;

    /// <summary>Creates a simulator for one tenant.</summary>
    /// <param name="options">The tenant's set-up; <see langword="null"/> for the defaults.</param>
    /// <param name="timeProvider">The clock rate limits, <c>UpdatedDateUTC</c> and <c>If-Modified-Since</c> use; <see langword="null"/> for the system clock.</param>
    public XeroApiSimulator(XeroSimulatorOptions? options = null, TimeProvider? timeProvider = null)
    {
        Options = options ?? new XeroSimulatorOptions();
        Time = timeProvider ?? TimeProvider.System;
        _grantedScopes = new HashSet<string>(Options.GrantedScopes ?? XeroScopes.Required, StringComparer.Ordinal);
        _taxRates = SimulatorSeed.UkDemoTaxRates();
        _accounts = SimulatorSeed.UkDemoAccounts();
    }

    /// <summary>The tenant's set-up.</summary>
    public XeroSimulatorOptions Options { get; }

    /// <summary>The simulator's clock.</summary>
    public TimeProvider Time { get; }

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<XeroSimulatedRequest> Requests
    {
        get
        {
            lock (_sync)
                return [.. _requests];
        }
    }

    /// <summary>Every contract or safety-rule violation seen, in order.</summary>
    public IReadOnlyList<XeroContractViolation> Violations
    {
        get
        {
            lock (_sync)
                return [.. _violations];
        }
    }

    /// <summary>How many requests are being processed right now (held by <see cref="HoldRequests"/> or not).</summary>
    public int InFlight
    {
        get
        {
            lock (_sync)
                return _inFlight;
        }
    }

    /// <summary>An <see cref="HttpClient"/> over this handler with <see cref="BaseAddress"/> set.</summary>
    /// <remarks>The client does not own the handler; disposing it leaves the simulator usable.</remarks>
    public HttpClient CreateClient() => new(this, disposeHandler: false) { BaseAddress = BaseAddress };

    /// <summary>Queues <paramref name="fault"/> for the next matching request(s).</summary>
    public void Inject(XeroFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fault.Times);
        lock (_sync)
            _faults.Add(new FaultSlot(fault, fault.Times));
    }

    /// <summary>
    /// Holds every request admitted from now on (after the concurrency
    /// check, before it is processed) until the returned handle is disposed —
    /// so a test can put exactly N requests in flight and prove the
    /// concurrency limit without timing.
    /// </summary>
    public IDisposable HoldRequests()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync)
            _hold = gate;
        return new HoldHandle(this, gate);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var raw = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var context = RequestContext.Parse(request, raw);

        // 1. Injected fault.
        XeroFault? fault;
        lock (_sync)
            fault = TakeFault(context.Path);

        if (fault is not null && fault.Kind != XeroFaultKind.DropResponseAfterCommit)
            return AnswerFault(context, fault);

        // 2. Concurrency.
        TaskCompletionSource? hold;
        lock (_sync)
        {
            if (_inFlight >= Options.ConcurrentLimit)
            {
                var refused = RateLimited("concurrent", TimeSpan.FromSeconds(1));
                return Finish(context, refused);
            }

            _inFlight++;
            hold = _hold;
        }

        try
        {
            if (hold is not null)
                await hold.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            HttpResponseMessage response;
            lock (_sync)
                response = Finish(context, Process(context));

            if (fault?.Kind == XeroFaultKind.DropResponseAfterCommit)
            {
                response.Dispose();
                throw new HttpRequestException($"Simulated: the response to {context.Method} {context.Path} was lost after Xero committed it.");
            }

            return response;
        }
        finally
        {
            lock (_sync)
                _inFlight--;
        }
    }

    private HttpResponseMessage AnswerFault(RequestContext context, XeroFault fault)
    {
        lock (_sync)
        {
            switch (fault.Kind)
            {
                case XeroFaultKind.ServiceUnavailable:
                    return Finish(context, SimResponse.Text(HttpStatusCode.ServiceUnavailable, "The Xero API is temporarily unavailable (simulated)."));
                case XeroFaultKind.RateLimitedMinute:
                    return Finish(context, RateLimited("minute", fault.RetryAfter ?? TimeSpan.FromSeconds(60)));
                case XeroFaultKind.RateLimitedDay:
                    return Finish(context, RateLimited("day", fault.RetryAfter ?? TimeSpan.FromHours(1)));
                case XeroFaultKind.Unauthorised:
                    return Finish(context, Unauthorised());
                case XeroFaultKind.TransportFailure:
                    Record(context, 0);
                    throw new HttpRequestException($"Simulated transport failure for {context.Method} {context.Path}.");
                default:
                    throw new InvalidOperationException($"Fault {fault.Kind} is not answered up front.");
            }
        }
    }

    private XeroFault? TakeFault(string path)
    {
        for (var i = 0; i < _faults.Count; i++)
        {
            var slot = _faults[i];
            if (slot.Fault.PathContains is not null && !path.Contains(slot.Fault.PathContains, StringComparison.Ordinal))
                continue;

            slot.Remaining--;
            if (slot.Remaining == 0)
                _faults.RemoveAt(i);
            return slot.Fault;
        }

        return null;
    }

    /// <summary>Runs every check and the endpoint itself; called under <see cref="_sync"/>.</summary>
    private SimResponse Process(RequestContext context)
    {
        if (!IsAuthorised(context.Request))
            return Unauthorised();

        if (!TenantMatches(context.Request))
            return SimResponse.Problem(HttpStatusCode.Forbidden, "Forbidden", "AuthenticationUnsuccessful: the xero-tenant-id header is missing or names an organisation this token is not connected to.");

        var now = Time.GetUtcNow();
        PruneWindows(now);
        if (_dayCalls.Count >= Options.DayLimit)
            return RateLimited("day", _dayCalls.Peek() + TimeSpan.FromDays(1) - now);
        if (_minuteCalls.Count >= Options.MinuteLimit)
            return RateLimited("minute", _minuteCalls.Peek() + TimeSpan.FromMinutes(1) - now);
        _minuteCalls.Enqueue(now);
        _dayCalls.Enqueue(now);

        var route = Route(context);
        if (route is null)
        {
            if (!context.IsRead)
                context.Violate(XeroSimulatorRules.WriteAllowList, $"{context.Method} {context.Path} is not an endpoint TempestOS may write to.");
            return SimResponse.NotFound();
        }

        if (route.AnswersJson && !context.AcceptsJson)
        {
            context.Violate(XeroSimulatorRules.AcceptJson, $"{context.Method} {context.Path} did not send Accept: application/json; Xero would answer XML.");
        }

        if (route.Handler is null)
        {
            context.Violate(XeroSimulatorRules.Method, $"{context.Method} is not supported on {context.Path}.");
            return SimResponse.Text(HttpStatusCode.MethodNotAllowed, $"The method {context.Method} is not allowed on this resource.");
        }

        var scopes = context.IsRead ? route.ReadScopes : route.WriteScopes;
        if (!scopes.Any(_grantedScopes.Contains))
        {
            context.Violate(XeroSimulatorRules.Scope, $"{context.Method} {context.Path} needs one of [{string.Join(", ", scopes)}]; the token carries [{string.Join(", ", _grantedScopes.Order(StringComparer.Ordinal))}].");
            return SimResponse.Problem(HttpStatusCode.Forbidden, "Forbidden", "AuthorizationUnsuccessful: the access token does not carry a scope this endpoint needs.");
        }

        if (context.IsRead)
            return route.Handler(context);

        // A write: Idempotency-Key first, so a replay neither re-applies nor re-records.
        string? fingerprint = null;
        if (context.IdempotencyKey is null)
        {
            context.Violate(XeroSimulatorRules.IdempotencyKeyMissing, $"{context.Method} {context.Path} carried no Idempotency-Key.");
        }
        else if (context.IdempotencyKey.Length > 128)
        {
            context.Violate(XeroSimulatorRules.IdempotencyKeyTooLong, $"Idempotency-Key is {context.IdempotencyKey.Length} characters; Xero allows 128.");
            return SimResponse.ValidationException("The Idempotency-Key header must be 128 characters or fewer.");
        }
        else
        {
            fingerprint = context.Fingerprint();
            if (_idempotency.TryGetValue(context.IdempotencyKey, out var cached))
            {
                if (cached.Fingerprint == fingerprint)
                    return cached.Response;

                context.Violate(XeroSimulatorRules.IdempotencyKeyReused, $"Idempotency-Key '{context.IdempotencyKey}' was first used for {cached.Description} and is now reused for {context.Method} {context.Path} with a different body.");
                return SimResponse.ValidationException("The Idempotency-Key has already been used for a different request.");
            }
        }

        if (!Options.IsDemoCompany)
            context.Violate(XeroSimulatorRules.LiveOrganisationWrite, $"{context.Method} {context.Path} wrote to '{Options.OrganisationName}', which is not the Demo Company.");

        if (context.Json is not null && ContainsSentToContact(context.Json))
            context.Violate(XeroSimulatorRules.SentToContact, $"{context.Method} {context.Path} set SentToContact: true.");

        var response = route.Handler(context);

        if (fingerprint is not null && (int)response.Status < 500)
            _idempotency[context.IdempotencyKey!] = new CachedResponse(fingerprint, $"{context.Method} {context.Path}", response);

        return response;
    }

    private bool IsAuthorised(HttpRequestMessage request) =>
        request.Headers.Authorization is { Scheme: var scheme, Parameter: var token }
        && string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
        && string.Equals(token, Options.AccessToken, StringComparison.Ordinal);

    private bool TenantMatches(HttpRequestMessage request) =>
        request.Headers.TryGetValues("xero-tenant-id", out var values)
        && values.Count() == 1
        && string.Equals(values.Single().Trim(), Options.TenantId, StringComparison.OrdinalIgnoreCase);

    private static SimResponse Unauthorised() =>
        SimResponse.Problem(HttpStatusCode.Unauthorized, "Unauthorized", "TokenInvalid: the access token is missing, expired, revoked or not the one issued for this tenant.")
            .WithHeader("WWW-Authenticate", "Bearer error=\"invalid_token\"");

    private static SimResponse RateLimited(string problem, TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return SimResponse.Problem((HttpStatusCode)429, "Too Many Requests", $"Rate limit exceeded ({problem}).")
            .WithHeader("X-Rate-Limit-Problem", problem)
            .WithHeader("Retry-After", seconds.ToString(CultureInfo.InvariantCulture));
    }

    private void PruneWindows(DateTimeOffset now)
    {
        while (_minuteCalls.Count > 0 && _minuteCalls.Peek() <= now - TimeSpan.FromMinutes(1))
            _minuteCalls.Dequeue();
        while (_dayCalls.Count > 0 && _dayCalls.Peek() <= now - TimeSpan.FromDays(1))
            _dayCalls.Dequeue();
    }

    private static bool ContainsSentToContact(JsonNode node) => node switch
    {
        JsonObject obj => obj.Any(p =>
            (string.Equals(p.Key, "SentToContact", StringComparison.OrdinalIgnoreCase) && p.Value is JsonValue v && v.TryGetValue<bool>(out var sent) && sent)
            || (p.Value is not null && ContainsSentToContact(p.Value))),
        JsonArray array => array.Any(n => n is not null && ContainsSentToContact(n)),
        _ => false,
    };

    /// <summary>Records the request (and its violations) and turns the answer into an <see cref="HttpResponseMessage"/>; called under <see cref="_sync"/>.</summary>
    private HttpResponseMessage Finish(RequestContext context, SimResponse answer)
    {
        Record(context, (int)answer.Status);

        var response = new HttpResponseMessage(answer.Status) { RequestMessage = context.Request };
        if (answer.Binary is not null)
        {
            response.Content = new ByteArrayContent(answer.Binary);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(answer.ContentType);
        }
        else if (answer.Body is not null)
        {
            response.Content = new StringContent(answer.Body, System.Text.Encoding.UTF8, answer.ContentType);
        }

        foreach (var (name, value) in answer.Headers)
        {
            if (string.Equals(name, "Retry-After", StringComparison.OrdinalIgnoreCase))
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(int.Parse(value, CultureInfo.InvariantCulture)));
            else if (!response.Headers.TryAddWithoutValidation(name, value))
                response.Content?.Headers.TryAddWithoutValidation(name, value);
        }

        PruneWindows(Time.GetUtcNow());
        response.Headers.TryAddWithoutValidation("X-MinLimit-Remaining", Math.Max(0, Options.MinuteLimit - _minuteCalls.Count).ToString(CultureInfo.InvariantCulture));
        response.Headers.TryAddWithoutValidation("X-DayLimit-Remaining", Math.Max(0, Options.DayLimit - _dayCalls.Count).ToString(CultureInfo.InvariantCulture));
        response.Headers.TryAddWithoutValidation("X-AppMinLimit-Remaining", Math.Max(0, AppMinuteLimit - _minuteCalls.Count).ToString(CultureInfo.InvariantCulture));
        return response;
    }

    private void Record(RequestContext context, int statusCode)
    {
        var record = new XeroSimulatedRequest(
            context.Method, context.Path, context.Query, context.Json?.DeepClone(), context.Binary?.LongLength, context.IdempotencyKey, statusCode, Time.GetUtcNow());
        _requests.Add(record);
        foreach (var (rule, detail) in context.Violations)
            _violations.Add(new XeroContractViolation(rule, detail, record));
    }

    private sealed class FaultSlot(XeroFault fault, int remaining)
    {
        public XeroFault Fault { get; } = fault;

        public int Remaining { get; set; } = remaining;
    }

    private sealed record CachedResponse(string Fingerprint, string Description, SimResponse Response);

    private sealed class HoldHandle(XeroApiSimulator owner, TaskCompletionSource gate) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._sync)
            {
                if (ReferenceEquals(owner._hold, gate))
                    owner._hold = null;
            }

            gate.TrySetResult();
        }
    }

    /// <summary>One request, parsed once.</summary>
    private sealed class RequestContext
    {
        private RequestContext(HttpRequestMessage request) => Request = request;

        public HttpRequestMessage Request { get; }

        public HttpMethod Method => Request.Method;

        public bool IsRead => Method == HttpMethod.Get || Method == HttpMethod.Head;

        public string Path { get; private set; } = string.Empty;

        public IReadOnlyList<string> Segments { get; private set; } = [];

        public IReadOnlyDictionary<string, string> Query { get; private set; } = new Dictionary<string, string>();

        public JsonNode? Json { get; private set; }

        public bool BodyMalformed { get; private set; }

        public byte[]? Binary { get; private set; }

        public byte[]? Raw { get; private set; }

        public string? ContentType { get; private set; }

        public string? IdempotencyKey { get; private set; }

        public bool AcceptsJson => Request.Headers.Accept.Any(a => string.Equals(a.MediaType, "application/json", StringComparison.OrdinalIgnoreCase));

        public List<(string Rule, string Detail)> Violations { get; } = [];

        public void Violate(string rule, string detail) => Violations.Add((rule, detail));

        public string Fingerprint()
        {
            var hash = Convert.ToHexString(SHA256.HashData(Raw ?? []));
            return $"{Method} {Path}?{string.Join('&', Query.OrderBy(q => q.Key, StringComparer.OrdinalIgnoreCase).Select(q => $"{q.Key}={q.Value}"))} {hash}";
        }

        public string? QueryValue(string name) => Query.TryGetValue(name, out var value) ? value : null;

        public static RequestContext Parse(HttpRequestMessage request, byte[]? raw)
        {
            var context = new RequestContext(request) { Raw = raw };
            var uri = request.RequestUri ?? throw new InvalidOperationException("A request to the Xero simulator needs a RequestUri.");
            if (!uri.IsAbsoluteUri)
                uri = new Uri(BaseAddress, uri);

            var absolutePath = uri.AbsolutePath;
            var basePath = BaseAddress.AbsolutePath;
            var relative = string.Equals(uri.Host, BaseAddress.Host, StringComparison.OrdinalIgnoreCase)
                && absolutePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)
                ? absolutePath[basePath.Length..]
                : "\u0000" + absolutePath; // never routes
            var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();
            context.Segments = segments;
            context.Path = string.Join('/', segments);
            context.Query = XeroWire.ParseQuery(uri.Query);

            if (request.Headers.TryGetValues("Idempotency-Key", out var keys))
                context.IdempotencyKey = keys.FirstOrDefault();

            context.ContentType = request.Content?.Headers.ContentType?.MediaType;
            if (raw is { Length: > 0 })
            {
                if (context.ContentType is not null && context.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        context.Json = JsonNode.Parse(raw, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        context.BodyMalformed = true;
                    }
                }
                else
                {
                    context.Binary = raw;
                }
            }

            return context;
        }
    }

    /// <summary>What the simulator answers, kept independent of any one <see cref="HttpResponseMessage"/> so an idempotent replay can answer it again.</summary>
    private sealed record SimResponse(HttpStatusCode Status, string? Body, string ContentType, byte[]? Binary = null)
    {
        public List<(string Name, string Value)> Headers { get; init; } = [];

        public SimResponse WithHeader(string name, string value) => this with { Headers = [.. Headers, (name, value)] };

        public static SimResponse Json(HttpStatusCode status, JsonNode body) => new(status, body.ToJsonString(), "application/json");

        public static SimResponse Ok(JsonNode body) => Json(HttpStatusCode.OK, body);

        public static SimResponse Text(HttpStatusCode status, string text) => new(status, text, "text/plain");

        public static SimResponse Bytes(byte[] content, string mimeType) => new(HttpStatusCode.OK, null, mimeType, content);

        public static SimResponse NotFound() => Text(HttpStatusCode.NotFound, "The resource you're looking for cannot be found");

        public static SimResponse Problem(HttpStatusCode status, string title, string detail) => Json(status, new JsonObject
        {
            ["Type"] = null,
            ["Title"] = title,
            ["Status"] = (int)status,
            ["Detail"] = detail,
        });

        public static SimResponse ValidationException(string message, JsonArray? elements = null) => Json(HttpStatusCode.BadRequest, new JsonObject
        {
            ["ErrorNumber"] = 10,
            ["Type"] = "ValidationException",
            ["Message"] = message,
            ["Elements"] = elements ?? [],
        });
    }
}
