using Microsoft.Extensions.Logging;

namespace Tempest.Core.Invoicing.OAuth;

/// <summary>
/// Wraps a fresh <see cref="HttpClientHandler"/> with request/response
/// diagnostics through <c>Microsoft.Extensions.Logging.ILogger</c> — the
/// type <c>Tempest.Core.Logging.TempestLoggerProvider</c>'s own remarks
/// name, verbatim, as "any future library code's" convention for exactly
/// this (an accounting connector's <c>HttpClient</c> diagnostics) — so a
/// real connector's own HTTP traffic lands in this platform's own log
/// sinks, category for category, rather than nowhere (`WP 19.1A` part 2).
/// Shared transport plumbing for both <c>XeroConnector</c> and
/// <c>QuickBooksOnlineConnector</c>; not itself part of the OAuth protocol,
/// but composed alongside <see cref="OAuthAuthoriser"/> at the same
/// <c>Tempest.Core.Runtime.TempestHost</c> call site, so it lives beside it.
/// </summary>
public sealed class InvoicingHttpLoggingHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    /// <summary>Initialises a new instance of the <see cref="InvoicingHttpLoggingHandler"/> class, wrapping a fresh <see cref="HttpClientHandler"/>.</summary>
    public InvoicingHttpLoggingHandler(ILogger logger)
        : base(new HttpClientHandler())
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("{Method} {Uri}", request.Method, request.RequestUri);

        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("{Method} {Uri} -> {StatusCode}", request.Method, request.RequestUri, (int)response.StatusCode);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Method} {Uri} failed", request.Method, request.RequestUri);
            throw;
        }
    }
}
