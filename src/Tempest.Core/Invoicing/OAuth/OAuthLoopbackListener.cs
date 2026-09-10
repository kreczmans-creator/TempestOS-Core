using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Tempest.Core.Invoicing.OAuth;

/// <summary>
/// What the provider's own redirect back to the loopback carried: the
/// authorisation code and the state this run started with (RFC 6749 §4.1.2),
/// or an <see cref="Error"/> instead when the operator denied consent —
/// plus, for QuickBooks Online specifically, the <c>realmId</c> query
/// parameter Intuit's own redirect adds (the company/tenant id, named
/// generically here since <see cref="OAuthAuthoriser"/> treats it exactly
/// as Xero's own tenant id — <c>WP 19.1A</c> part 2).
/// </summary>
internal sealed record OAuthCallback(string? Code, string? State, string? Error, string? RealmId);

/// <summary>
/// The loopback half of the authorisation-code flow: an
/// <see cref="HttpListener"/> on a free, ephemeral <c>127.0.0.1</c> port,
/// under the fixed <c>/callback/</c> path every provider's own redirect URI
/// is registered against (`WP 19.1A` part 2, brief §1(a)/(d)).
/// </summary>
/// <remarks>
/// <see cref="HttpListener"/> binds a specific loopback address/port
/// (rather than the wildcard <c>+</c>/<c>*</c> host Windows reserves for
/// administrators only) without needing a URL ACL reservation or an
/// elevated process — confirmed empirically against this platform's own
/// build machine before this class was written the way it is.
/// </remarks>
internal sealed class OAuthLoopbackListener : IDisposable
{
    private readonly HttpListener _listener;

    /// <summary>Starts listening immediately, on a freshly chosen free port.</summary>
    public OAuthLoopbackListener()
    {
        RedirectUri = new Uri($"http://127.0.0.1:{FindFreePort()}/callback/");

        _listener = new HttpListener();
        _listener.Prefixes.Add(RedirectUri.ToString());
        _listener.Start();
    }

    /// <summary>The redirect URI this run's own authorisation URL carries, and the sandbox app must register — <c>http://127.0.0.1:&lt;port&gt;/callback/</c>, a fresh port every run.</summary>
    public Uri RedirectUri { get; }

    /// <summary>Waits for the provider's own single redirect, answers it with a plain confirmation page, and returns what it carried.</summary>
    public async Task<OAuthCallback> WaitForCallbackAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(static state => ((HttpListener)state!).Stop(), _listener);

        HttpListenerContext context;
        try
        {
            context = await _listener.GetContextAsync().ConfigureAwait(false);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        var query = context.Request.QueryString;
        var callback = new OAuthCallback(query["code"], query["state"], query["error"], query["realmId"]);

        await RespondAsync(context, callback, cancellationToken).ConfigureAwait(false);

        return callback;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_listener.IsListening)
            _listener.Stop();

        ((IDisposable)_listener).Dispose();
    }

    private static async Task RespondAsync(HttpListenerContext context, OAuthCallback callback, CancellationToken cancellationToken)
    {
        var html = callback.Error is null
            ? "<html><body>Authorisation complete. You can close this window and return to TempestOS.</body></html>"
            : $"<html><body>Authorisation failed: {WebUtility.HtmlEncode(callback.Error)}</body></html>";

        var buffer = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;

        try
        {
            await context.Response.OutputStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            context.Response.OutputStream.Close();
        }
    }

    private static int FindFreePort()
    {
        // The standard "ask the OS for a free port, then rebind" trick:
        // TcpListener(..., 0) has the OS allocate an ephemeral port, which
        // is released the instant it stops so HttpListener can claim it
        // immediately after — a small, accepted race no different from
        // every other loopback-redirect OAuth client's own approach.
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }
}
