namespace Tempest.Companion.Client;

/// <summary>
/// A typed failure from the Companion API boundary — carries what a
/// caller can branch on (<see cref="Reason"/>, <see cref="StatusCode"/>)
/// and a user-presentable message; never a raw transport stack trace
/// (those stay in the inner exception for diagnostics).
/// </summary>
public sealed class CompanionApiException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CompanionApiException"/> class.
    /// </summary>
    /// <param name="reason">The failure's own category.</param>
    /// <param name="message">A user-presentable description.</param>
    /// <param name="statusCode">The HTTP status code, or <see langword="null"/> when the server was never reached.</param>
    /// <param name="innerException">The underlying transport exception, if any.</param>
    public CompanionApiException(CompanionApiFailureReason reason, string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
        StatusCode = statusCode;
    }

    /// <summary>Gets the failure's own category.</summary>
    public CompanionApiFailureReason Reason { get; }

    /// <summary>Gets the HTTP status code, or <see langword="null"/> when the server was never reached.</summary>
    public int? StatusCode { get; }
}

/// <summary>The categories a Companion API call can fail in — each drives a distinct UI state.</summary>
public enum CompanionApiFailureReason
{
    /// <summary>The server could not be reached at all — the offline path.</summary>
    Unreachable,

    /// <summary>No identity is configured, or the server rejected it (401).</summary>
    Unauthorized,

    /// <summary>The configured identity lacks the required permission (403).</summary>
    Forbidden,

    /// <summary>The route does not exist on this server (404) — a version/URL mismatch.</summary>
    NotFound,

    /// <summary>The server rejected the request as malformed (400).</summary>
    BadRequest,

    /// <summary>The server failed (5xx), or the response could not be parsed.</summary>
    ServerError,
}
