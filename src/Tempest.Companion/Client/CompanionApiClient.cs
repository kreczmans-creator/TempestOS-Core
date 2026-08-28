using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Tempest.Companion.Contracts;

namespace Tempest.Companion.Client;

/// <summary>
/// The concrete <see cref="ICompanionApiClient"/> — <see cref="HttpClient"/>
/// against the platform's REST API, asserting identity through the
/// existing <c>X-Identity-Id</c> header exactly as <c>ADR-0052</c>
/// defines it (the Companion introduces no parallel identity mechanism;
/// the header model's own disclosed limitations, <c>TD-13</c>/<c>TD-14</c>,
/// bound where this client may be pointed — see the `WP 14.0A` security
/// review). Every failure is normalised to
/// <see cref="CompanionApiException"/> with a typed reason.
/// </summary>
public sealed class CompanionApiClient : ICompanionApiClient, IDisposable
{
    /// <summary>The identity request header — the exact name <c>ApiRequestHandler.IdentityHeaderName</c> serves, declared here because this process deliberately carries no Tempest.Core reference.</summary>
    public const string IdentityHeaderName = "X-Identity-Id";

    private readonly HttpClient _httpClient;
    private readonly string _identityId;

    /// <summary>
    /// Initialises a new instance of the <see cref="CompanionApiClient"/> class.
    /// </summary>
    /// <param name="serverUrl">The TempestOS host's own REST API base URL.</param>
    /// <param name="identityId">The identity id asserted on every request.</param>
    /// <param name="timeout">The per-request timeout, or <see langword="null"/> for the 10-second default — short, deliberately: a phone must fail over to its cache quickly rather than hang.</param>
    /// <exception cref="ArgumentException"><paramref name="serverUrl"/> is not an absolute http/https URL.</exception>
    public CompanionApiClient(string serverUrl, string identityId, TimeSpan? timeout = null)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var baseUri) || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Server URL must be an absolute http:// or https:// URL.", nameof(serverUrl));

        _identityId = identityId ?? string.Empty;
        _httpClient = new HttpClient { BaseAddress = baseUri, Timeout = timeout ?? TimeSpan.FromSeconds(10) };
    }

    /// <inheritdoc />
    public Task<CockpitSummaryDto> GetCockpitAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CockpitSummaryDto>(CompanionApiRoutes.Cockpit, cancellationToken);

    /// <inheritdoc />
    public Task<ProjectListDto> GetProjectsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ProjectListDto>(CompanionApiRoutes.Projects, cancellationToken);

    /// <inheritdoc />
    public Task<AttentionDto> GetAttentionAsync(CancellationToken cancellationToken = default) =>
        GetAsync<AttentionDto>(CompanionApiRoutes.Attention, cancellationToken);

    /// <inheritdoc />
    public Task<ActivityDto> GetActivityAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ActivityDto>(CompanionApiRoutes.Activity, cancellationToken);

    /// <inheritdoc />
    public Task<NotificationListDto> GetNotificationsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<NotificationListDto>(CompanionApiRoutes.Notifications, cancellationToken);

    /// <inheritdoc />
    public async Task<CompanionActionOutcome> SetDocumentStatusAsync(SetObjectStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, CompanionApiRoutes.SetDocumentStatus)
            {
                Content = new StringContent(JsonSerializer.Serialize(request, CompanionJson.Options), Encoding.UTF8, "application/json"),
            },
            cancellationToken).ConfigureAwait(false);

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // 200 = the command succeeded; 400 = the command ran and
            // reported a foreseeable failure OR the body failed binding -
            // both are user-presentable outcomes, not boundary faults.
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest)
                return new CompanionActionOutcome(response.StatusCode == HttpStatusCode.OK, string.IsNullOrWhiteSpace(body) ? null : body);

            throw FailureFrom(response.StatusCode, body);
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose" />
    public void Dispose() => _httpClient.Dispose();

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path), cancellationToken).ConfigureAwait(false);

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
                throw FailureFrom(response.StatusCode, body);

            try
            {
                return JsonSerializer.Deserialize<T>(body, CompanionJson.Options)
                    ?? throw new CompanionApiException(CompanionApiFailureReason.ServerError, "The server's response was empty.", (int)response.StatusCode);
            }
            catch (JsonException ex)
            {
                throw new CompanionApiException(CompanionApiFailureReason.ServerError, "The server's response could not be read — the server and app versions may not match.", (int)response.StatusCode, ex);
            }
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        var request = requestFactory();

        if (!string.IsNullOrWhiteSpace(_identityId))
            request.Headers.TryAddWithoutValidation(IdentityHeaderName, _identityId);

        try
        {
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // HttpClient surfaces its own timeout as cancellation the
            // caller never requested - normalise it to the offline path.
            throw new CompanionApiException(CompanionApiFailureReason.Unreachable, "TempestOS did not respond in time.", innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CompanionApiException(CompanionApiFailureReason.Unreachable, "TempestOS could not be reached.", innerException: ex);
        }
        finally
        {
            request.Dispose();
        }
    }

    private static CompanionApiException FailureFrom(HttpStatusCode statusCode, string body)
    {
        var detail = string.IsNullOrWhiteSpace(body) ? null : body.Trim();

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => new(CompanionApiFailureReason.Unauthorized, "No identity is configured, or the server rejected it.", (int)statusCode),
            HttpStatusCode.Forbidden => new(CompanionApiFailureReason.Forbidden, detail ?? "This identity does not hold the required Companion permission.", (int)statusCode),
            HttpStatusCode.NotFound => new(CompanionApiFailureReason.NotFound, "This TempestOS host does not serve the Companion API — check the server URL and platform version.", (int)statusCode),
            HttpStatusCode.BadRequest => new(CompanionApiFailureReason.BadRequest, detail ?? "The server rejected the request.", (int)statusCode),
            _ => new(CompanionApiFailureReason.ServerError, "TempestOS reported an internal error.", (int)statusCode),
        };
    }
}
