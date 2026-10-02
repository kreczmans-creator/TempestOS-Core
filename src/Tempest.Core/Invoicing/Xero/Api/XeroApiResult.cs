namespace Tempest.Core.Invoicing.Xero.Api;

/// <summary>
/// The result of one call to the Xero Accounting API through the typed
/// client (<c>XeroAccountingApi</c>, `v0.24.0` task B1) — the
/// <see cref="ConnectorOutcome"/> vocabulary of `ADR-0151`, widened with
/// what the sync engine needs to retry well: the HTTP status, Xero's own
/// validation messages, and the 429 rate-limit facts.
/// </summary>
/// <typeparam name="T">The parsed answer.</typeparam>
/// <param name="Outcome">What happened, classified exactly as <c>ConnectorHttpOutcome.Classify</c> does (2xx Ok; 400 Rejected; 401/403 Reauthorise; 429/5xx/transport Unavailable; anything else, or a lost or unparseable response, Unknown). A 404 is Rejected with <see cref="NotFound"/> set.</param>
/// <param name="Value">The answer, when <see cref="Outcome"/> is <see cref="ConnectorOutcome.Ok"/>; otherwise <see langword="default"/>.</param>
/// <param name="HttpStatus">The HTTP status code received; <see langword="null"/> when no response arrived.</param>
/// <param name="Reason">An engineer-readable reason for every outcome but Ok.</param>
/// <param name="ValidationErrors">Xero's own <c>Elements[].ValidationErrors[].Message</c> values (also when a 200 response carries per-element errors); empty when none.</param>
/// <param name="RetryAfter">The 429 response's <c>Retry-After</c>, in seconds as Xero sent it; <see langword="null"/> when absent.</param>
/// <param name="RateLimitProblem">The 429 response's <c>X-Rate-Limit-Problem</c> header (<c>"minute"</c>, <c>"day"</c>, <c>"appminute"</c> or <c>"concurrent"</c>), verbatim; <see langword="null"/> when absent.</param>
/// <param name="NotFound">Whether Xero answered 404 — the linked record no longer exists in Xero.</param>
/// <param name="MissingScope">Whether a 403 named a missing scope (the grant predates a scope this build requests); the remedy is re-authorising, not retrying.</param>
public sealed record XeroApiResult<T>(
    ConnectorOutcome Outcome,
    T? Value,
    int? HttpStatus,
    string? Reason,
    IReadOnlyList<string> ValidationErrors,
    TimeSpan? RetryAfter = null,
    string? RateLimitProblem = null,
    bool NotFound = false,
    bool MissingScope = false);
