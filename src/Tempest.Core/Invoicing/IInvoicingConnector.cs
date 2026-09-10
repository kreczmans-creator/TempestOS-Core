namespace Tempest.Core.Invoicing;

/// <summary>
/// The outcome of one call to an <see cref="IInvoicingConnector"/> — a
/// result, never an exception (`WP 19.1A`, `ADR-0151`).
/// </summary>
public enum ConnectorOutcome
{
    /// <summary>The call succeeded; the result carries the answer.</summary>
    Ok,

    /// <summary>The accounting system refused the call outright, with a reason.</summary>
    Rejected,

    /// <summary>The stored token is expired or revoked; nothing further is sent until the operator re-authorises.</summary>
    Reauthorise,

    /// <summary>The connector could not be reached at all (network, outage). Not retried automatically.</summary>
    Unavailable,

    /// <summary>The call was sent but its response was lost — neither success nor failure is known.</summary>
    Unknown,
}

/// <summary>
/// The result of one <see cref="IInvoicingConnector"/> call: the outcome,
/// and — only when <see cref="Outcome"/> is <see cref="ConnectorOutcome.Ok"/> —
/// the answer itself (`WP 19.1A`, `ADR-0151`).
/// </summary>
/// <remarks>
/// Mirrors <c>Tempest.Core.Timesheets.TimesheetResult</c>'s own
/// refusal-as-result shape, widened from two states (succeeded/refused) to
/// the five a network-backed connector genuinely has. A connector
/// implementation never throws to report any of these — see
/// <see cref="IInvoicingConnector"/>'s own remarks.
/// </remarks>
public sealed class ConnectorResult<T>
{
    private ConnectorResult(ConnectorOutcome outcome, T? value, string? reason)
    {
        Outcome = outcome;
        Value = value;
        Reason = reason;
    }

    /// <summary>What happened.</summary>
    public ConnectorOutcome Outcome { get; }

    /// <summary>The answer, when <see cref="Outcome"/> is <see cref="ConnectorOutcome.Ok"/>. Undefined otherwise.</summary>
    public T? Value { get; }

    /// <summary>Why, for <see cref="ConnectorOutcome.Rejected"/> — an accounting-system message an engineer can read. <see langword="null"/> for every other outcome unless the connector has something more specific to add.</summary>
    public string? Reason { get; }

    /// <summary>A successful call, carrying <paramref name="value"/> — which may itself be <see langword="null"/> for a query that legitimately found nothing (<see cref="IInvoicingConnector.FindByReferenceAsync"/>).</summary>
    public static ConnectorResult<T> Ok(T value) => new(ConnectorOutcome.Ok, value, reason: null);

    /// <summary>The accounting system refused the call, with <paramref name="reason"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is null, empty, or whitespace.</exception>
    public static ConnectorResult<T> Rejected(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new(ConnectorOutcome.Rejected, default, reason);
    }

    /// <summary>The stored token is expired or revoked.</summary>
    public static ConnectorResult<T> Reauthorise(string? reason = null) => new(ConnectorOutcome.Reauthorise, default, reason);

    /// <summary>The connector could not be reached.</summary>
    public static ConnectorResult<T> Unavailable(string? reason = null) => new(ConnectorOutcome.Unavailable, default, reason);

    /// <summary>The call was sent but its response was lost.</summary>
    public static ConnectorResult<T> Unknown(string? reason = null) => new(ConnectorOutcome.Unknown, default, reason);
}

/// <summary>What a draft invoice was created as, by <see cref="IInvoicingConnector.CreateDraftInvoiceAsync"/>.</summary>
/// <param name="ExternalId">The accounting system's own identity for the created invoice.</param>
/// <param name="ExternalInvoiceNumber">The accounting system's own invoice number, where it assigns one immediately. <see langword="null"/> where it is assigned later (many systems number an invoice only on approval).</param>
/// <param name="Reference">The invoice's own reference field, as the accounting system stored it — always the idempotency key this call was made with, echoed back so a caller can confirm the write actually carried it.</param>
public sealed record CreatedInvoice(string ExternalId, string? ExternalInvoiceNumber, string Reference);

/// <summary>What <see cref="IInvoicingConnector.ReadStatusAsync"/> reads back for one invoice.</summary>
/// <param name="ExternalStatus">The accounting system's own status word, verbatim (for example <c>"AUTHORISED"</c>, <c>"PAID"</c>) — never mapped to <see cref="InvoiceRequestStatus"/> by the connector itself; <see cref="InvoicingService"/> alone decides what a status word means for this Kind's own lifecycle.</param>
/// <param name="ExternalInvoiceNumber">The accounting system's own invoice number, now that one may exist.</param>
/// <param name="IssuedDate">When the invoice was issued to the client, as the accounting system records it. <see langword="null"/> if it has not been issued.</param>
/// <param name="PaidDate">When the invoice was paid, as the accounting system records it. <see langword="null"/> until it is. <b>Read from the connector only — nothing in TempestOS ever sets this.</b></param>
public sealed record InvoiceStatusReading(string ExternalStatus, string? ExternalInvoiceNumber, DateOnly? IssuedDate, DateOnly? PaidDate);

/// <summary>One contact <see cref="IInvoicingConnector.ListContactsAsync"/> reads from the accounting system — a client the connector already knows about, offered so an engineer picks an existing contact rather than the connector inventing one.</summary>
/// <param name="ExternalId">The accounting system's own identity for the contact.</param>
/// <param name="Name">The contact's own display name.</param>
public sealed record ConnectorContact(string ExternalId, string Name);

/// <summary>Whether a connector currently has a usable, current authorisation.</summary>
public enum ConnectorAuthorisation
{
    /// <summary>A current token is stored and usable.</summary>
    Authorised,

    /// <summary>No token is stored — the operator has never authorised this connector.</summary>
    NotAuthorised,

    /// <summary>A token is stored but is expired or has been revoked.</summary>
    Expired,
}

/// <summary>The answer <see cref="IInvoicingConnector.AuthorisationStateAsync"/> reports.</summary>
/// <param name="Status">Whether the connector can currently be called.</param>
/// <param name="Detail">Anything more specific the connector has to say (for example, when the token expires). <see langword="null"/> if it has nothing to add.</param>
public sealed record ConnectorAuthorisationState(ConnectorAuthorisation Status, string? Detail = null);

/// <summary>
/// One outbound accounting integration — a draft invoice raised, its
/// status read back, reconciled by reference, and its contacts listed
/// (`WP 19.1A`, `ADR-0151`). <see cref="FakeInvoicingConnector"/> is the
/// only implementation this Work Package ships; a real Xero or QuickBooks
/// Online connector over <c>HttpClient</c> and OAuth 2.0 is `WP 19.1A`
/// parts 2 and 3 — this interface, the idempotency rule and the request
/// lifecycle are the seam those parts implement against, unchanged.
/// </summary>
/// <remarks>
/// <para>
/// <b>A result, never an exception (`ADR-0151`).</b> Every member returns
/// a <see cref="ConnectorResult{T}"/> (or, for <see cref="AuthorisationStateAsync"/>,
/// a plain <see cref="ConnectorAuthorisationState"/>) rather than throwing
/// for anything a network call to a third party can ordinarily produce —
/// rejection, an expired token, an unreachable host, a lost response. A
/// thrown exception out of an implementation is a defect in that
/// implementation, exactly as a thrown exception out of a
/// <c>CommandBinding.Build</c> lambda is a defect in the binding
/// (`Tempest.Core.Commands.CommandBinding`'s own remarks) — never a normal
/// outcome <see cref="InvoicingService"/> is expected to catch.
/// </para>
/// <para>
/// <b>Idempotency is the request's own id, not a header this interface
/// invents.</b> <see cref="InvoicingService.SendAsync"/> passes
/// <c>InvoiceRequest.Id</c> itself as <paramref name="idempotencyKey"/> —
/// never a fresh value per attempt — and every implementation writes that
/// same string into the created invoice's own reference field
/// (<see cref="CreatedInvoice.Reference"/>). <see cref="FindByReferenceAsync"/>
/// is what resolves a lost response: reconciliation looks the invoice up
/// by that reference <em>before</em> any retry, rather than risking a
/// second invoice for one request.
/// </para>
/// </remarks>
public interface IInvoicingConnector
{
    /// <summary>This connector's own name (for example <c>"Fake"</c>, <c>"Xero"</c>, <c>"QuickBooks Online"</c>) — <see cref="InvoiceRequest.Connector"/> records it verbatim.</summary>
    string Name { get; }

    /// <summary>
    /// Creates a draft invoice from <paramref name="request"/>, writing
    /// <paramref name="idempotencyKey"/> into the invoice's own reference
    /// field (and, where the accounting system's own API supports one, an
    /// idempotency header) so a lost response can be resolved by
    /// <see cref="FindByReferenceAsync"/> rather than by retrying blind.
    /// </summary>
    Task<ConnectorResult<CreatedInvoice>> CreateDraftInvoiceAsync(
        InvoiceRequestSnapshot request, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Reads the current status of the invoice known as <paramref name="externalId"/>.</summary>
    Task<ConnectorResult<InvoiceStatusReading>> ReadStatusAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the invoice whose own reference field is <paramref name="reference"/>
    /// — our own request id, written by <see cref="CreateDraftInvoiceAsync"/> —
    /// so a lost response is resolved by lookup rather than by risking a
    /// second invoice. <see cref="ConnectorResult{T}.Value"/> is
    /// <see langword="null"/> when the call succeeded but nothing carries
    /// that reference.
    /// </summary>
    Task<ConnectorResult<CreatedInvoice?>> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every contact the accounting system already knows about.</summary>
    Task<ConnectorResult<IReadOnlyList<ConnectorContact>>> ListContactsAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether this connector currently has a usable, current authorisation.</summary>
    Task<ConnectorAuthorisationState> AuthorisationStateAsync(CancellationToken cancellationToken = default);
}
