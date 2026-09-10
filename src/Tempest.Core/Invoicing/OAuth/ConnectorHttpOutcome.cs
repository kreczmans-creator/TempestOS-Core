using System.Net;

namespace Tempest.Core.Invoicing.OAuth;

/// <summary>
/// The one HTTP-status-code-to-<see cref="ConnectorOutcome"/> mapping
/// <c>XeroConnector</c> and <c>QuickBooksOnlineConnector</c> both apply,
/// verbatim, to every call (`WP 19.1A` part 2 brief §2): 2xx is
/// <see cref="ConnectorOutcome.Ok"/>; 400/422 is
/// <see cref="ConnectorOutcome.Rejected"/> (the reason is read from the
/// body — provider-specific, so not this class's concern); 401/403 is
/// <see cref="ConnectorOutcome.Reauthorise"/>; 429 and every 5xx is
/// <see cref="ConnectorOutcome.Unavailable"/>; anything else this mapping
/// does not recognise is <see cref="ConnectorOutcome.Unknown"/> — a response
/// came back, so the call was plainly sent, but its shape is not one either
/// connector understands.
/// </summary>
internal static class ConnectorHttpOutcome
{
    /// <summary>Maps <paramref name="statusCode"/> to the outcome both connectors report for it.</summary>
    public static ConnectorOutcome Classify(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;

        if (code is >= 200 and < 300)
            return ConnectorOutcome.Ok;

        if (code is 400 or 422)
            return ConnectorOutcome.Rejected;

        if (code is 401 or 403)
            return ConnectorOutcome.Reauthorise;

        if (code == 429 || code >= 500)
            return ConnectorOutcome.Unavailable;

        return ConnectorOutcome.Unknown;
    }

    /// <summary>
    /// Maps a transport-level failure — the request never got a response at
    /// all (DNS, refused connection, a client-side timeout before or during
    /// send) — to <see cref="ConnectorOutcome.Unavailable"/>, the brief's
    /// own "timeouts/DNS" bucket (§2). Never called for a caller-driven
    /// cancellation: a connector's own <c>catch</c> checks
    /// <see cref="CancellationToken.IsCancellationRequested"/> against its
    /// own token first and rethrows rather than reporting an outcome for
    /// that case — cancelling a call is not a fact about the connector.
    /// </summary>
    public static string DescribeTransportFailure(Exception exception) => exception switch
    {
        HttpRequestException httpRequestException => httpRequestException.Message,
        OperationCanceledException => "The request timed out before a response was received.",
        _ => exception.Message,
    };

    /// <summary>Reads a response's own body as text, defensively — an empty string if the body cannot be read at all, never a thrown exception this far down the pipeline.</summary>
    public static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
