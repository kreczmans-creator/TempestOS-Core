using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tempest.Core.Invoicing.OAuth;

namespace Tempest.Core.Invoicing.Xero.Api;

/// <summary>
/// The typed Xero Accounting API client (`v0.24.0` task B1, `ADR-0162`):
/// one call, one <see cref="XeroApiResult{T}"/> — never an exception for
/// anything Xero or the network did. This file is the transport every
/// resource shares; each resource (attachments, settings, contacts,
/// quotes, invoices, purchase orders, bills) is its own <c>partial</c> file
/// owned by its own build task (<c>docs/releases/v0.24.0/Xero Technical
/// Design.md</c> §11).
/// </summary>
/// <remarks>
/// <para>
/// <b>Every call</b> is relative to the <see cref="HttpClient"/>'s
/// <see cref="HttpClient.BaseAddress"/> (<c>https://api.xero.com/api.xro/2.0/</c>)
/// and carries <c>Authorization: Bearer</c>, <c>xero-tenant-id</c> and
/// <c>Accept: application/json</c>. Every <c>PUT</c>/<c>POST</c> also
/// carries an <c>Idempotency-Key</c> (≤ 128 characters, §7.2) and
/// <c>?summarizeErrors=true</c> (§3). Every call on a document with line
/// items carries <c>?unitdp=4</c> (<see cref="UnitAmountDecimalPlaces"/>).
/// </para>
/// <para>
/// <b>Below this client</b> the <see cref="HttpClient"/>'s own pipeline holds
/// <see cref="XeroWriteSafetyHandler"/> (D3/D4/D7) and
/// <see cref="XeroRateLimiter"/> (§6.5), shared with the `WP 19.1A`
/// <see cref="XeroConnector"/>: a request this client builds is still
/// refused by the handler if it breaks a rule, and arrives back here as an
/// ordinary <see cref="ConnectorOutcome.Rejected"/>.
/// </para>
/// <para>
/// <b>Outcomes</b> (§6.6): 2xx → Ok (a 2xx whose documents carry
/// <c>HasErrors</c>/<c>ValidationErrors</c> → Rejected; an empty or
/// unreadable 2xx → Unknown); 400/422 → Rejected with Xero's validation
/// messages; 401 → Reauthorise; 403 → Reauthorise, with
/// <see cref="XeroApiResult{T}.MissingScope"/> when the body names
/// authorisation or a scope; 404 → Rejected with
/// <see cref="XeroApiResult{T}.NotFound"/>; 429 → Unavailable with
/// <c>Retry-After</c> and <c>X-Rate-Limit-Problem</c>; 5xx and transport
/// failures → Unavailable; anything else → Unknown.
/// </para>
/// </remarks>
public sealed partial class XeroAccountingApi
{
    /// <summary>The longest <c>Idempotency-Key</c> Xero accepts; a longer one is answered 400 by Xero, so it is refused here before sending.</summary>
    public const int MaximumIdempotencyKeyLength = 128;

    /// <summary>
    /// The decimal places TempestOS's unit amounts carry in Xero (`v0.24.0`
    /// review M7). Every call on a document that holds line items
    /// (<c>Invoices</c>, <c>Quotes</c>, <c>PurchaseOrders</c>, <c>CreditNotes</c>)
    /// sends <c>?unitdp=4</c>: without it Xero rounds a unit amount to two
    /// places on the way in and shows two on the way out, so
    /// <c>1000 × £0.125</c> would be £125.00 in TempestOS and £130.00 in Xero.
    /// </summary>
    public const int UnitAmountDecimalPlaces = 4;

    /// <summary>How the reason of a request refused by <see cref="XeroWriteSafetyHandler"/> begins (then the rule, then Xero-shaped messages).</summary>
    public const string BlockedReasonPrefix = "TempestOS blocked the request";

    private readonly HttpClient _httpClient;
    private readonly OAuthAuthoriser _authoriser;
    private readonly TimeProvider _time;

    /// <summary>The clock this client reads Xero's answers on — the host's Xero clock, so services built over the client share it.</summary>
    internal TimeProvider Time => _time;

    /// <summary>Initialises a new instance of the <see cref="XeroAccountingApi"/> class.</summary>
    /// <param name="httpClient">The Xero <see cref="HttpClient"/> — its <see cref="HttpClient.BaseAddress"/> is the API root, and its pipeline holds <see cref="XeroWriteSafetyHandler"/> and <see cref="XeroRateLimiter"/> (`TempestHost`), or the in-process simulator in tests.</param>
    /// <param name="authoriser">The Xero <see cref="OAuthAuthoriser"/>; every call asks it for a current access token and the connected tenant.</param>
    /// <param name="timeProvider">The clock a <c>Retry-After</c> date is measured against; <see langword="null"/> for the system clock.</param>
    public XeroAccountingApi(HttpClient httpClient, OAuthAuthoriser authoriser, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(authoriser);

        _httpClient = httpClient;
        _authoriser = authoriser;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Sends a <c>GET</c> and reads the answer as <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The answer's wire shape.</typeparam>
    /// <param name="path">The path relative to the API root (for example <c>"Invoices"</c>), unescaped segments already escaped by the caller.</param>
    /// <param name="query">Query parameters (unescaped); <see langword="null"/> for none. A <see langword="null"/> value is skipped.</param>
    /// <param name="ifModifiedSince">Sent as <c>If-Modified-Since</c> when not <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the call; cancellation is rethrown, never reported as an outcome.</param>
    internal Task<XeroApiResult<T>> GetAsync<T>(
        string path, IEnumerable<KeyValuePair<string, string?>>? query = null, DateTimeOffset? ifModifiedSince = null, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, path, query, content: null, idempotencyKey: null, ifModifiedSince, cancellationToken);

    /// <summary>Sends a <c>PUT</c> (Xero's create) with a JSON body.</summary>
    /// <typeparam name="T">The answer's wire shape.</typeparam>
    /// <param name="path">The path relative to the API root.</param>
    /// <param name="body">The request body, serialised with <see cref="XeroWire.JsonOptions"/>.</param>
    /// <param name="idempotencyKey">The fixed key for this write (§6.4, §7.2).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    internal Task<XeroApiResult<T>> PutJsonAsync<T>(string path, object body, string idempotencyKey, CancellationToken cancellationToken = default) =>
        SendJsonWriteAsync<T>(HttpMethod.Put, path, body, idempotencyKey, cancellationToken);

    /// <summary>Sends a <c>POST</c> (Xero's update, or create-or-update) with a JSON body.</summary>
    /// <typeparam name="T">The answer's wire shape.</typeparam>
    /// <param name="path">The path relative to the API root.</param>
    /// <param name="body">The request body, serialised with <see cref="XeroWire.JsonOptions"/>.</param>
    /// <param name="idempotencyKey">The fixed key for this write.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    internal Task<XeroApiResult<T>> PostJsonAsync<T>(string path, object body, string idempotencyKey, CancellationToken cancellationToken = default) =>
        SendJsonWriteAsync<T>(HttpMethod.Post, path, body, idempotencyKey, cancellationToken);

    /// <summary>Sends a <c>PUT</c> or <c>POST</c> with a binary body (an attachment upload).</summary>
    /// <typeparam name="T">The answer's wire shape.</typeparam>
    /// <param name="method"><see cref="HttpMethod.Put"/> or <see cref="HttpMethod.Post"/>.</param>
    /// <param name="path">The path relative to the API root.</param>
    /// <param name="query">Extra query parameters (for example <c>IncludeOnline</c>); <see langword="null"/> for none.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="contentType">The MIME type.</param>
    /// <param name="idempotencyKey">The fixed key for this write.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    internal Task<XeroApiResult<T>> SendBinaryAsync<T>(
        HttpMethod method, string path, IEnumerable<KeyValuePair<string, string?>>? query, ReadOnlyMemory<byte> content, string contentType,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var body = new ByteArrayContent(content.ToArray());
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return SendAsync<T>(method, path, WithSummarizeErrors(query), body, idempotencyKey, ifModifiedSince: null, cancellationToken);
    }

    private Task<XeroApiResult<T>> SendJsonWriteAsync<T>(HttpMethod method, string path, object body, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        var json = JsonSerializer.Serialize(body, body.GetType(), XeroWire.JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        return SendAsync<T>(method, path, WithSummarizeErrors(null), content, idempotencyKey, ifModifiedSince: null, cancellationToken);
    }

    private async Task<XeroApiResult<T>> SendAsync<T>(
        HttpMethod method, string path, IEnumerable<KeyValuePair<string, string?>>? query, HttpContent? content, string? idempotencyKey,
        DateTimeOffset? ifModifiedSince, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var ownedContent = content;

        if (method != HttpMethod.Get)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Failure<T>(ConnectorOutcome.Rejected, null, "A Xero write needs an Idempotency-Key; none was given.");

            if (idempotencyKey.Length > MaximumIdempotencyKeyLength)
                return Failure<T>(ConnectorOutcome.Rejected, null, $"The Idempotency-Key is {idempotencyKey.Length} characters; Xero accepts at most {MaximumIdempotencyKeyLength}.");
        }

        var access = await _authoriser.EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (access.Outcome == AccessTokenOutcome.Unavailable)
        {
            // `v0.24.0` review M2: an expired token that could not be renewed
            // because the sign-in service is unreachable is an outage, not a
            // refused grant — the write stays queued.
            return Failure<T>(ConnectorOutcome.Unavailable, null, access.Reason ?? "Xero's sign-in service could not be reached.");
        }

        if (access.Outcome != AccessTokenOutcome.Ok)
        {
            return Failure<T>(ConnectorOutcome.Reauthorise, null, access.Outcome switch
            {
                AccessTokenOutcome.NotAuthorised => "Xero has never been authorised.",
                AccessTokenOutcome.NotConfigured => "Xero is not configured (no client id).",
                _ => access.Reason ?? "Xero needs re-authorising.",
            });
        }

        if (string.IsNullOrEmpty(access.TenantId))
            return Failure<T>(ConnectorOutcome.Reauthorise, null, "No Xero organisation is connected; re-authorise to select one.");

        if (CarriesUnitAmounts(path))
            query = [.. query ?? [], new("unitdp", UnitAmountDecimalPlaces.ToString(CultureInfo.InvariantCulture))];

        using var request = new HttpRequestMessage(method, BuildRelativeUri(path, query)) { Content = ownedContent };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
        request.Headers.Add("xero-tenant-id", access.TenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (idempotencyKey is not null && method != HttpMethod.Get)
            request.Headers.Add("Idempotency-Key", idempotencyKey);

        if (ifModifiedSince is { } since)
            request.Headers.IfModifiedSince = since;

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await ConnectorHttpOutcome.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

            return Interpret<T>(response, body, _time.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return Failure<T>(ConnectorOutcome.Unavailable, null, ConnectorHttpOutcome.DescribeTransportFailure(ex));
        }
    }

    /// <summary>Whether a call on <paramref name="path"/> reads or writes a document's line items, so carries <c>unitdp</c> (<see cref="UnitAmountDecimalPlaces"/>): the document resources, not their attachments.</summary>
    /// <param name="path">The path relative to the API root.</param>
    internal static bool CarriesUnitAmounts(string path)
    {
        var resource = path.Split('/', '?')[0];
        return resource is "Invoices" or "Quotes" or "PurchaseOrders" or "CreditNotes"
               && !path.Contains("/Attachments", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds the relative URI for <paramref name="path"/> and <paramref name="query"/>, escaping each query name and value.</summary>
    /// <param name="path">The path relative to the API root.</param>
    /// <param name="query">The query parameters; a <see langword="null"/> value is skipped.</param>
    internal static Uri BuildRelativeUri(string path, IEnumerable<KeyValuePair<string, string?>>? query)
    {
        var pairs = (query ?? [])
            .Where(pair => pair.Value is not null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}")
            .ToList();

        var relative = pairs.Count == 0 ? path : $"{path}{(path.Contains('?', StringComparison.Ordinal) ? '&' : '?')}{string.Join('&', pairs)}";

        return new Uri(relative, UriKind.Relative);
    }

    /// <summary>
    /// Classifies one Xero response (§6.6) — see the type's own remarks for
    /// the table. Pure: no I/O, so every mapping is unit-tested directly.
    /// </summary>
    /// <typeparam name="T">The answer's wire shape.</typeparam>
    /// <param name="response">The response (status and headers are read; its body is <paramref name="body"/>).</param>
    /// <param name="body">The response body, already read.</param>
    /// <param name="now">The time a <c>Retry-After</c> date is measured against.</param>
    internal static XeroApiResult<T> Interpret<T>(HttpResponseMessage response, string body, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(response);

        var status = (int)response.StatusCode;
        var outcome = ConnectorHttpOutcome.Classify(response.StatusCode);

        if (outcome == ConnectorOutcome.Ok)
            return InterpretSuccess<T>(status, body);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return Failure<T>(ConnectorOutcome.Rejected, status, "Xero has no such record (404); it may have been deleted in Xero.", notFound: true);

        switch (outcome)
        {
            case ConnectorOutcome.Rejected:
            {
                var errors = ReadValidationErrors(body);
                var blockedRule = response.Headers.TryGetValues(XeroWriteSafetyHandler.BlockedHeader, out var rules) ? rules.FirstOrDefault() : null;
                var summary = errors.Count > 0 ? string.Join("; ", errors) : ReadErrorMessage(body) ?? "Xero rejected the request.";
                var reason = blockedRule is null ? summary : $"{BlockedReasonPrefix} ({blockedRule}): {summary}";
                return Failure<T>(ConnectorOutcome.Rejected, status, reason, errors);
            }

            case ConnectorOutcome.Reauthorise:
            {
                if (response.StatusCode == HttpStatusCode.Forbidden && NamesMissingScope(body))
                {
                    return Failure<T>(
                        ConnectorOutcome.Reauthorise, status,
                        "Xero refused the call: the stored authorisation does not include a scope this call needs; re-authorise Xero.",
                        missingScope: true);
                }

                return Failure<T>(ConnectorOutcome.Reauthorise, status, response.StatusCode == HttpStatusCode.Forbidden
                    ? "Xero refused the call for this organisation (403); re-authorise Xero."
                    : "Xero refused the stored access token (401); re-authorise Xero.");
            }

            case ConnectorOutcome.Unavailable when status == 429:
            {
                var retryAfter = ReadRetryAfter(response, now);
                var problem = response.Headers.TryGetValues("X-Rate-Limit-Problem", out var problems) ? problems.FirstOrDefault() : null;
                return new XeroApiResult<T>(
                    ConnectorOutcome.Unavailable, default, status,
                    $"Xero's rate limit was reached ({problem ?? "unspecified"}); retry after {retryAfter?.TotalSeconds.ToString(CultureInfo.InvariantCulture) ?? "a minute"} s.",
                    [], retryAfter, problem);
            }

            case ConnectorOutcome.Unavailable:
                return Failure<T>(ConnectorOutcome.Unavailable, status, $"Xero returned {status} {response.ReasonPhrase}.".TrimEnd(' ', '.') + ".");

            default:
                return Failure<T>(ConnectorOutcome.Unknown, status, $"Xero returned an unexpected status {status}.");
        }
    }

    private static XeroApiResult<T> InterpretSuccess<T>(int status, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Failure<T>(ConnectorOutcome.Unknown, status, $"Xero answered {status} with no body; whether the call took effect is unknown.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return Failure<T>(ConnectorOutcome.Unknown, status, $"Xero answered {status} with a body that is not JSON; whether the call took effect is unknown.");
        }

        using (document)
        {
            var elementErrors = ReadPerElementErrors(document.RootElement, out var anyHasErrors);
            if (elementErrors.Count > 0 || anyHasErrors)
            {
                var reason = elementErrors.Count > 0 ? string.Join("; ", elementErrors) : "Xero reported errors on the document.";
                return Failure<T>(ConnectorOutcome.Rejected, status, reason, elementErrors);
            }

            try
            {
                var value = document.RootElement.Deserialize<T>(XeroWire.JsonOptions);
                return value is null
                    ? Failure<T>(ConnectorOutcome.Unknown, status, $"Xero answered {status} with an empty answer.")
                    : new XeroApiResult<T>(ConnectorOutcome.Ok, value, status, null, []);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                return Failure<T>(ConnectorOutcome.Unknown, status, $"Xero answered {status} with a body this build cannot read ({ex.GetType().Name}).");
            }
        }
    }

    /// <summary>Xero's <c>Elements[].ValidationErrors[].Message</c> values from a 400 body (and any per-document errors in the same body); empty when none or unreadable.</summary>
    private static IReadOnlyList<string> ReadValidationErrors(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return [];

        try
        {
            using var document = JsonDocument.Parse(body);
            return ReadPerElementErrors(document.RootElement, out _);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadErrorMessage(string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize<XeroWireError>(body, XeroWire.JsonOptions);
            return string.IsNullOrWhiteSpace(error?.Message) ? null : error.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Collects every <c>ValidationErrors[].Message</c> on each document of a root object's arrays (Xero's <c>Elements</c> on a 400; <c>Invoices</c>, <c>Quotes</c>… on a 200 with per-element errors), and whether any document says <c>HasErrors: true</c>.</summary>
    private static List<string> ReadPerElementErrors(JsonElement root, out bool anyHasErrors)
    {
        anyHasErrors = false;
        var messages = new List<string>();

        if (root.ValueKind != JsonValueKind.Object)
            return messages;

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var element in property.Value.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var field in element.EnumerateObject())
                {
                    if (field.NameEquals("HasErrors") && field.Value.ValueKind == JsonValueKind.True)
                        anyHasErrors = true;

                    if (!field.NameEquals("ValidationErrors") || field.Value.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var error in field.Value.EnumerateArray())
                    {
                        if (error.ValueKind == JsonValueKind.Object
                            && error.TryGetProperty("Message", out var message)
                            && message.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(message.GetString()))
                        {
                            messages.Add(message.GetString()!);
                        }
                    }
                }
            }
        }

        return messages;
    }

    /// <summary>Whether a 403 body names authorisation or a scope — Xero answers <c>AuthorizationUnsuccessful</c> for a call the token's scopes do not cover, distinct from <c>AuthenticationUnsuccessful</c> (a token not valid for the tenant).</summary>
    private static bool NamesMissingScope(string body) =>
        body.Contains("scope", StringComparison.OrdinalIgnoreCase)
        || body.Contains("AuthorizationUnsuccessful", StringComparison.OrdinalIgnoreCase)
        || body.Contains("AuthorisationUnsuccessful", StringComparison.OrdinalIgnoreCase);

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response, DateTimeOffset now)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
            return delta;

        if (header?.Date is { } date)
            return date > now ? date - now : TimeSpan.Zero;

        return null;
    }

    private static IEnumerable<KeyValuePair<string, string?>> WithSummarizeErrors(IEnumerable<KeyValuePair<string, string?>>? query) =>
        [new("summarizeErrors", "true"), .. query ?? []];

    /// <summary>Carries a non-Ok result across to another answer type, every fact but the (absent) value unchanged.</summary>
    /// <typeparam name="TFrom">The original answer type.</typeparam>
    /// <typeparam name="TTo">The new answer type.</typeparam>
    /// <param name="result">A result whose <see cref="XeroApiResult{T}.Outcome"/> is not <see cref="ConnectorOutcome.Ok"/>.</param>
    internal static XeroApiResult<TTo> Retype<TFrom, TTo>(XeroApiResult<TFrom> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new XeroApiResult<TTo>(
            result.Outcome, default, result.HttpStatus, result.Reason, result.ValidationErrors,
            result.RetryAfter, result.RateLimitProblem, result.NotFound, result.MissingScope);
    }

    private static XeroApiResult<T> Failure<T>(
        ConnectorOutcome outcome, int? status, string reason, IReadOnlyList<string>? validationErrors = null, bool notFound = false, bool missingScope = false) =>
        new(outcome, default, status, reason, validationErrors ?? [], NotFound: notFound, MissingScope: missingScope);
}
