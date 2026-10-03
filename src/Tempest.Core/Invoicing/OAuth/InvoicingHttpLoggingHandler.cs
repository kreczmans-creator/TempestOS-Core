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
        : this(logger, new HttpClientHandler())
    {
    }

    /// <summary>Initialises a new instance of the <see cref="InvoicingHttpLoggingHandler"/> class over <paramref name="innerHandler"/> (tests).</summary>
    /// <param name="logger">Where the diagnostics go.</param>
    /// <param name="innerHandler">The handler that sends the request.</param>
    internal InvoicingHttpLoggingHandler(ILogger logger, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <summary>
    /// What the log says about a request's target: its path only — never
    /// its query string, which can carry search terms such as a contact's
    /// name or VAT number (`v0.24.0` review n2) — nor its scheme or host's
    /// user info.
    /// </summary>
    /// <param name="uri">The request URI.</param>
    internal static string DescribeTarget(Uri? uri)
    {
        if (uri is null)
            return "(no uri)";

        if (!uri.IsAbsoluteUri)
        {
            var raw = uri.OriginalString;
            var cut = raw.IndexOfAny(['?', '#']);
            return cut < 0 ? raw : raw[..cut];
        }

        return $"{uri.Host}{uri.AbsolutePath}";
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var target = DescribeTarget(request.RequestUri);
        _logger.LogDebug("{Method} {Path}", request.Method, target);

        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("{Method} {Path} -> {StatusCode}", request.Method, target, (int)response.StatusCode);
            return response;
        }
        catch (Exception ex)
        {
            // The exception's type only, never the exception itself: some
            // transport failures quote the full request URI, query included.
            _logger.LogWarning("{Method} {Path} failed: {Failure}", request.Method, target,
                ex is HttpRequestException http ? $"{ex.GetType().Name} ({http.HttpRequestError})" : ex.GetType().Name);
            throw;
        }
    }
}
