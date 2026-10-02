using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tempest.Core.Audit;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Settings;

namespace Tempest.Core.Invoicing.Xero.Api;

/// <summary>
/// The one place below every Xero caller where D3, D4 and D7 are enforced
/// (`v0.24.0` task B1, `ADR-0162` decision 2;
/// <c>docs/releases/v0.24.0/Xero Technical Design.md</c> §7.1): a
/// <see cref="DelegatingHandler"/> in the Xero <see cref="HttpClient"/>
/// pipeline, shared by <see cref="XeroAccountingApi"/> and the `WP 19.1A`
/// <see cref="XeroConnector"/>. It inspects every outbound request and,
/// instead of sending a forbidden one, answers a synthetic <b>400</b>
/// carrying <see cref="BlockedHeader"/> and a Xero-shaped
/// <c>ValidationException</c> body — so every caller reports Rejected
/// through its ordinary path — and audits <see cref="BlockedAuditAction"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rules</b> (checked in this order; the first that fails blocks):
/// </para>
/// <list type="bullet">
/// <item><see cref="RuleEmail"/> — any request to a path ending <c>/Email</c> (D4).</item>
/// <item><see cref="RuleWriteAllowList"/> — any non-GET other than <c>PUT</c>/<c>POST</c> to <c>Contacts[/id]</c>, <c>Quotes[/id]</c>, <c>Invoices[/id]</c>, <c>PurchaseOrders[/id]</c> or <c>{Quotes,Invoices,PurchaseOrders}/{id}/Attachments/{file}</c> under the API root; any such write whose body is not readable JSON (an XML or form body could carry a status this handler cannot see); and any body with an object holding the same key twice, compared case-insensitively (this handler would read the first, Xero's serialiser the last).</item>
/// <item><see cref="RuleSentToContact"/> — any body with a <c>SentToContact</c> that is not JSON <c>false</c> or <c>null</c> (D4): <c>true</c>, <c>"true"</c>, <c>1</c> and anything else Xero's serialiser might read as true.</item>
/// <item><see cref="RuleInvoiceStatus"/> — an <c>Invoices</c> write whose <c>Status</c> is anything but <c>DRAFT</c>, or <c>DELETED</c> on an existing invoice (D3). The documents checked are the root object itself and every element of its envelope (<c>{ "Invoices": [ … ] }</c>, or an envelope sent as a single object), or every element of a bare array; an envelope or element that is not an object is blocked, since its status cannot be checked.</item>
/// <item><see cref="RulePurchaseOrderStatus"/> — a <c>PurchaseOrders</c> write whose <c>Status</c> is anything but <c>DRAFT</c>/<c>DELETED</c> (D3, Q2).</item>
/// <item><see cref="RuleQuoteStatus"/> — a <c>Quotes</c> write whose <c>Status</c> is outside <c>DRAFT, SENT, ACCEPTED, DECLINED</c>.</item>
/// <item><see cref="RuleLiveOrganisation"/> — any non-GET while the cached organisation is not Xero's Demo Company and <see cref="AllowLiveOrganisationSettingKey"/> is off (D7). An unknown organisation (no reading, or a reading of another tenant) is read first; still unknown blocks — and a failed reading is not retried for that tenant for <see cref="UnknownOrganisationRetryAfter"/>, so blocked writes do not spend Xero's call budget re-reading it.</item>
/// </list>
/// <para>
/// <b>GETs pass</b> (other than to <c>/Email</c>) — reading is never what D3,
/// D4 or D7 restrict, and the D7 reading itself is a GET through this same
/// pipeline.
/// </para>
/// </remarks>
public sealed class XeroWriteSafetyHandler : DelegatingHandler
{
    /// <summary>The response header naming the rule a synthetic 400 was answered for.</summary>
    public const string BlockedHeader = "X-Tempest-Blocked";

    /// <summary>The audit action recorded for every blocked request (§6.7).</summary>
    public const string BlockedAuditAction = "xero.push.blocked-by-rule";

    /// <summary>The Settings key that lets TempestOS write to an organisation that is not Xero's Demo Company (D7). Default off; turning it on is audited by Settings.</summary>
    public const string AllowLiveOrganisationSettingKey = "Xero.AllowLiveOrganisation";

    /// <summary>D3: an invoice or bill written with a status other than <c>DRAFT</c> (or <c>DELETED</c> on an existing one).</summary>
    public const string RuleInvoiceStatus = "D3.invoice-status";

    /// <summary>D3/Q2: a purchase order written with a status other than <c>DRAFT</c>/<c>DELETED</c>.</summary>
    public const string RulePurchaseOrderStatus = "D3.po-status";

    /// <summary>D4: a request to an email endpoint.</summary>
    public const string RuleEmail = "D4.email";

    /// <summary>D4: a body asking Xero to mark a document as sent to the contact.</summary>
    public const string RuleSentToContact = "D4.sent-to-contact";

    /// <summary>A quote written with a status outside TempestOS's own walk.</summary>
    public const string RuleQuoteStatus = "quote-status";

    /// <summary>A write to anything but the allowed documents and their attachments.</summary>
    public const string RuleWriteAllowList = "write-allow-list";

    /// <summary>D7: a write to an organisation that is not the Demo Company while the live organisation is not allowed.</summary>
    public const string RuleLiveOrganisation = "D7.live-organisation";

    /// <summary>The display name of the <see cref="AllowLiveOrganisationSettingKey"/> setting.</summary>
    public const string AllowLiveOrganisationDisplayName = "Xero — allow writes to the live organisation";

    /// <summary>How long a failed reading of the organisation stands before a write reads it again (D7). The cached reading is still consulted on every write.</summary>
    public static readonly TimeSpan UnknownOrganisationRetryAfter = TimeSpan.FromMinutes(1);

    private const string ApiRootSegment = "/api.xro/2.0/";

    private static readonly HashSet<string> WritableDocuments = new(StringComparer.OrdinalIgnoreCase) { "Contacts", "Quotes", "Invoices", "PurchaseOrders" };
    private static readonly HashSet<string> AttachableDocuments = new(StringComparer.OrdinalIgnoreCase) { "Quotes", "Invoices", "PurchaseOrders" };
    private static readonly HashSet<string> AllowedQuoteStatuses = new(StringComparer.Ordinal) { "DRAFT", "SENT", "ACCEPTED", "DECLINED" };
    private static readonly HashSet<string> AllowedPurchaseOrderStatuses = new(StringComparer.Ordinal) { "DRAFT", "DELETED" };

    private readonly Func<IXeroSettingsReader?> _settingsReader;
    private readonly Func<CancellationToken, Task<bool>> _allowLiveOrganisation;
    private readonly Func<IAuditRecorder?> _auditRecorder;
    private readonly ILogger? _logger;
    private readonly TimeProvider _time;
    private readonly Lock _failedReadingGate = new();
    private (string TenantId, DateTimeOffset Until)? _failedReading;

    /// <summary>Initialises a new instance of the <see cref="XeroWriteSafetyHandler"/> class. Its <see cref="DelegatingHandler.InnerHandler"/> is set by the pipeline's composer.</summary>
    /// <param name="settingsReader">Resolves the X1 settings reader whose cached organisation says whether this is the Demo Company; resolved per write (the container is built after the pipeline). <see langword="null"/> from it means no reader — the organisation is unknown, so every write is blocked by D7 unless the live organisation is allowed.</param>
    /// <param name="allowLiveOrganisation">Reads <see cref="AllowLiveOrganisationSettingKey"/>; any failure is treated as off.</param>
    /// <param name="auditRecorder">Resolves the audit recorder; <see langword="null"/> from it records nothing (the block still happens).</param>
    /// <param name="logger">Where a block is logged; <see langword="null"/> for nowhere.</param>
    /// <param name="timeProvider">The clock that times <see cref="UnknownOrganisationRetryAfter"/>; <see langword="null"/> for the system clock.</param>
    public XeroWriteSafetyHandler(
        Func<IXeroSettingsReader?> settingsReader,
        Func<CancellationToken, Task<bool>> allowLiveOrganisation,
        Func<IAuditRecorder?> auditRecorder,
        ILogger? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(settingsReader);
        ArgumentNullException.ThrowIfNull(allowLiveOrganisation);
        ArgumentNullException.ThrowIfNull(auditRecorder);

        _settingsReader = settingsReader;
        _allowLiveOrganisation = allowLiveOrganisation;
        _auditRecorder = auditRecorder;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Registers the <see cref="AllowLiveOrganisationSettingKey"/> definition
    /// (default <c>false</c>) with <paramref name="settings"/> unless it is
    /// registered already — idempotent, so <c>TempestHost</c> (at start-up),
    /// the handler's own read and the Settings UI may each call it.
    /// </summary>
    /// <param name="settings">The settings provider.</param>
    public static void EnsureAllowLiveOrganisationDefinition(ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            settings.RegisterDefinition(new SettingDefinition(AllowLiveOrganisationSettingKey, AllowLiveOrganisationDisplayName, "false"));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Registered already.
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var verdict = await InspectAsync(request, cancellationToken).ConfigureAwait(false);
        if (verdict is { } blocked)
            return await BlockAsync(request, blocked.Rule, blocked.Reason, cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string Rule, string Reason)?> InspectAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var segments = ApiPathSegments(request.RequestUri);

        if (segments is { Count: > 0 } && string.Equals(FullyUnescaped(segments[^1]).Split(PathSeparators)[^1], "Email", StringComparison.OrdinalIgnoreCase))
            return (RuleEmail, "TempestOS never emails a client; the Product Owner sends from Xero (D4).");

        if (request.Method == HttpMethod.Get)
            return null;

        if (segments is null)
            return (RuleWriteAllowList, "TempestOS writes only to Xero's Accounting API documents.");

        // An escaped '/' or '\' (%2F, %5C) inside a segment would read as one segment here but as two to anything that decodes it later.
        if (segments.Any(segment => segment.IndexOfAny(PathSeparators) >= 0))
            return (RuleWriteAllowList, "A Xero write's path must not hide a '/' or '\\' inside an escaped segment.");

        // A segment that still holds an escape after unescaping was escaped twice (abc%252FEmail): one more decode further on could
        // reveal a separator. A lone '%' that escapes nothing (a receipt named "50% off.jpg") is left alone.
        if (segments.Any(segment => segment.Contains('%', StringComparison.Ordinal) && Uri.UnescapeDataString(segment) != segment))
            return (RuleWriteAllowList, "A Xero write's path must not be escaped twice: a segment still holds an escape after unescaping.");

        var isAttachment = IsAttachmentPath(segments);
        if (!(request.Method == HttpMethod.Put || request.Method == HttpMethod.Post) || !(isAttachment || IsDocumentPath(segments)))
            return (RuleWriteAllowList, $"TempestOS never sends {request.Method} {string.Join('/', segments)}; it writes only contacts, quotes, invoices, bills, purchase orders and their attachments.");

        if (!isAttachment)
        {
            var body = await ReadJsonBodyAsync(request, cancellationToken).ConfigureAwait(false);
            if (body is null)
                return (RuleWriteAllowList, "A Xero document write must carry a readable JSON body.");

            using (body)
            {
                if (HasDuplicateKey(body.RootElement))
                    return (RuleWriteAllowList, "A Xero document write must not repeat a key: TempestOS would check one value and Xero would use another.");

                if (ContainsSentToContact(body.RootElement))
                    return (RuleSentToContact, "TempestOS never marks a document as sent to the contact (D4).");

                if (CheckStatuses(segments, body.RootElement) is { } statusBlock)
                    return statusBlock;
            }
        }

        return await CheckLiveOrganisationAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>The path's segments after the API root (<c>/api.xro/2.0/</c>), unescaped; <see langword="null"/> when the request is not to the API root at all.</summary>
    private static List<string>? ApiPathSegments(Uri? uri)
    {
        if (uri is null)
            return null;

        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : "/api.xro/2.0/" + uri.OriginalString.Split('?', 2)[0].TrimStart('/');
        var root = path.IndexOf(ApiRootSegment, StringComparison.OrdinalIgnoreCase);
        if (root < 0)
            return null;

        return [.. path[(root + ApiRootSegment.Length)..].Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString)];
    }

    /// <summary>Unescapes <paramref name="segment"/> until it stops changing (bounded), so a separator escaped more than once still shows.</summary>
    private static string FullyUnescaped(string segment)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var next = Uri.UnescapeDataString(segment);
            if (next == segment)
                break;
            segment = next;
        }

        return segment;
    }

    private static bool IsDocumentPath(List<string> segments) =>
        segments.Count is 1 or 2 && WritableDocuments.Contains(segments[0]);

    private static bool IsAttachmentPath(List<string> segments) =>
        segments.Count == 4
        && AttachableDocuments.Contains(segments[0])
        && string.Equals(segments[2], "Attachments", StringComparison.OrdinalIgnoreCase);

    private static async Task<JsonDocument?> ReadJsonBodyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
            return null;

        var mediaType = request.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
            return null;

        await request.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
        var text = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ContainsSentToContact(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    // Only an explicit false (or null) is safe: Xero's
                    // serialiser reads 1, "true", "1" and more as true.
                    if (string.Equals(property.Name, "SentToContact", StringComparison.OrdinalIgnoreCase)
                        && property.Value.ValueKind is not (JsonValueKind.False or JsonValueKind.Null))
                    {
                        return true;
                    }

                    if (ContainsSentToContact(property.Value))
                        return true;
                }

                return false;

            case JsonValueKind.Array:
                return element.EnumerateArray().Any(ContainsSentToContact);

            default:
                return false;
        }
    }

    /// <summary>Whether any object in <paramref name="element"/> holds the same key twice, compared case-insensitively (as Xero's serialiser matches them).</summary>
    private static bool HasDuplicateKey(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    if (!seen.Add(property.Name) || HasDuplicateKey(property.Value))
                        return true;
                }

                return false;

            case JsonValueKind.Array:
                return element.EnumerateArray().Any(HasDuplicateKey);

            default:
                return false;
        }
    }

    /// <summary>Checks the <c>Status</c> of every document in the body: the root object and each element of its <c>{ "Invoices": [ … ] }</c>-style envelope (or the envelope itself when sent as one object), or each element of a bare array.</summary>
    private static (string Rule, string Reason)? CheckStatuses(List<string> segments, JsonElement root)
    {
        var resource = segments[0];
        var pathHasId = segments.Count == 2;

        var documents = Documents(resource, root);
        if (documents is null)
            return (StatusRuleFor(resource), $"A {resource} write carried a document that is not an object, so its Status cannot be checked.");

        foreach (var document in documents)
        {
            if (!TryGetProperty(document, "Status", out var statusElement))
                continue; // Xero's default on create is DRAFT.

            if (statusElement.ValueKind != JsonValueKind.String)
                return (StatusRuleFor(resource), $"A {resource} write carried a Status Xero would not read as a word.");

            var status = statusElement.GetString()!.Trim().ToUpperInvariant();

            if (string.Equals(resource, "Invoices", StringComparison.OrdinalIgnoreCase))
            {
                var existing = pathHasId || TryGetProperty(document, "InvoiceID", out _);
                if (!(status == "DRAFT" || (status == "DELETED" && existing)))
                {
                    return (RuleInvoiceStatus, status == "DELETED"
                        ? "Only an existing draft can be deleted."
                        : $"TempestOS writes invoices and bills only as DRAFT; never {status} (D3). The Product Owner approves in Xero.");
                }
            }
            else if (string.Equals(resource, "PurchaseOrders", StringComparison.OrdinalIgnoreCase))
            {
                if (!AllowedPurchaseOrderStatuses.Contains(status))
                    return (RulePurchaseOrderStatus, $"TempestOS writes purchase orders only as DRAFT (or deletes its own); never {status} (D3, Q2).");
            }
            else if (string.Equals(resource, "Quotes", StringComparison.OrdinalIgnoreCase))
            {
                if (!AllowedQuoteStatuses.Contains(status))
                    return (RuleQuoteStatus, $"TempestOS moves a Xero quote only along DRAFT, SENT, ACCEPTED, DECLINED; never {status}.");
            }
        }

        return null;
    }

    private static string StatusRuleFor(string resource) =>
        string.Equals(resource, "Invoices", StringComparison.OrdinalIgnoreCase) ? RuleInvoiceStatus
        : string.Equals(resource, "PurchaseOrders", StringComparison.OrdinalIgnoreCase) ? RulePurchaseOrderStatus
        : string.Equals(resource, "Quotes", StringComparison.OrdinalIgnoreCase) ? RuleQuoteStatus
        : RuleWriteAllowList;

    /// <summary>The documents a body carries; <see langword="null"/> when one of them is not an object (its status could not be checked).</summary>
    private static List<JsonElement>? Documents(string resource, JsonElement root)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                return ObjectsOrNull(root);

            case JsonValueKind.Object:
                List<JsonElement> documents = [root];
                if (!TryGetProperty(root, resource, out var envelope))
                    return documents;

                switch (envelope.ValueKind)
                {
                    case JsonValueKind.Array:
                        return ObjectsOrNull(envelope) is { } elements ? [.. documents, .. elements] : null;
                    case JsonValueKind.Object:
                        documents.Add(envelope);
                        return documents;
                    case JsonValueKind.Null:
                        return documents;
                    default:
                        return null;
                }

            default:
                return null;
        }
    }

    private static List<JsonElement>? ObjectsOrNull(JsonElement array)
    {
        var elements = array.EnumerateArray().ToList();
        return elements.TrueForAll(e => e.ValueKind == JsonValueKind.Object) ? elements : null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private async Task<(string Rule, string Reason)?> CheckLiveOrganisationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (await IsLiveOrganisationAllowedAsync(cancellationToken).ConfigureAwait(false))
            return null;

        var tenantId = request.Headers.TryGetValues("xero-tenant-id", out var tenants) ? tenants.FirstOrDefault() : null;
        var reading = await ReadOrganisationAsync(tenantId, cancellationToken).ConfigureAwait(false);

        if (reading is null)
        {
            return (RuleLiveOrganisation,
                "Xero's organisation has not been read, so TempestOS cannot confirm it is the Demo Company; refresh Xero in Settings, or allow the live organisation (D7).");
        }

        return reading.Organisation.IsDemoCompany
            ? null
            : (RuleLiveOrganisation,
                $"'{reading.Organisation.Name}' is not Xero's Demo Company; TempestOS writes to it only once Settings allows the live organisation (D7).");
    }

    private async Task<bool> IsLiveOrganisationAllowedAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _allowLiveOrganisation(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Reading {Setting} failed; treating it as off.", AllowLiveOrganisationSettingKey);
            return false;
        }
    }

    /// <summary>The cached reading for <paramref name="tenantId"/>, reading Xero first when there is none (or only another tenant's); <see langword="null"/> when still unknown.</summary>
    private async Task<XeroSettingsReading?> ReadOrganisationAsync(string? tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(tenantId))
            return null;

        IXeroSettingsReader? reader;
        try
        {
            reader = _settingsReader();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Resolving the Xero settings reader failed; the organisation is unknown.");
            return null;
        }

        if (reader is null)
            return null;

        try
        {
            var cached = await reader.ReadCachedAsync(cancellationToken).ConfigureAwait(false);
            if (cached is not null && string.Equals(cached.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                return cached;

            if (RecentlyFailedToRead(tenantId))
                return null;

            var refreshed = await reader.RefreshAsync(cancellationToken).ConfigureAwait(false);
            var reading = refreshed.Outcome == ConnectorOutcome.Ok
                && refreshed.Value is { } fresh
                && string.Equals(fresh.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                    ? fresh
                    : null;

            RememberReading(tenantId, failed: reading is null);
            return reading;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Reading Xero's organisation failed; the organisation is unknown.");
            RememberReading(tenantId, failed: true);
            return null;
        }
    }

    private bool RecentlyFailedToRead(string tenantId)
    {
        lock (_failedReadingGate)
        {
            return _failedReading is { } failed
                && string.Equals(failed.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                && _time.GetUtcNow() < failed.Until;
        }
    }

    private void RememberReading(string tenantId, bool failed)
    {
        lock (_failedReadingGate)
        {
            _failedReading = failed ? (tenantId, _time.GetUtcNow() + UnknownOrganisationRetryAfter) : null;
        }
    }

    private async Task<HttpResponseMessage> BlockAsync(HttpRequestMessage request, string rule, string reason, CancellationToken cancellationToken)
    {
        var path = request.RequestUri is { IsAbsoluteUri: true } uri ? uri.AbsolutePath : request.RequestUri?.OriginalString ?? string.Empty;

        _logger?.LogWarning("Blocked {Method} {Path} by TempestOS safety rule {Rule}: {Reason}", request.Method, path, rule, reason);

        try
        {
            if (_auditRecorder() is { } audit)
            {
                await audit.RecordAsync(
                    BlockedAuditAction,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["rule"] = rule,
                        ["method"] = request.Method.Method,
                        ["path"] = path,
                        ["reason"] = reason,
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The block stands whether or not it could be audited.
            _logger?.LogWarning(ex, "Auditing the blocked request failed.");
        }

        var error = new XeroWireError(
            ErrorNumber: 10,
            Type: "ValidationException",
            Message: "A validation exception occurred",
            Elements: [new XeroWireElement(HasErrors: true, ValidationErrors: [new XeroWireValidationError($"Blocked by TempestOS safety rule {rule}: {reason}")])]);

        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = request,
            Content = new StringContent(JsonSerializer.Serialize(error, XeroWire.JsonOptions), Encoding.UTF8, "application/json"),
        };
        response.Headers.Add(BlockedHeader, rule);

        return response;
    }
}
