using System.Net.Http.Headers;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>One HTTP call this handler answered, recorded for a test to assert against.</summary>
internal sealed record RecordedHttpCall(HttpMethod Method, Uri Uri, string? Body, HttpRequestHeaders Headers);

/// <summary>
/// The recorded-response stub every contract test in this folder drives a
/// connector through — never a real <see cref="HttpClientHandler"/> or
/// socket (`WP 19.1A` part 2 brief §3). Routes are matched in registration
/// order by a caller-supplied predicate; a request matching none of them
/// throws rather than falling through to any real transport — the network
/// guard the brief's own acceptance names explicitly: "the handler stub
/// throws on any unrecorded URL."
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, string?, Task<HttpResponseMessage>> Respond)> _routes = [];
    private readonly List<RecordedHttpCall> _calls = [];
    private readonly object _gate = new();

    /// <summary>Every call this handler has answered (or refused as unrecorded), in call order.</summary>
    public IReadOnlyList<RecordedHttpCall> Calls
    {
        get { lock (_gate) return [.. _calls]; }
    }

    /// <summary>Registers a route matching every request whose own URL contains <paramref name="urlContains"/>, for <paramref name="method"/>.</summary>
    public void When(HttpMethod method, string urlContains, Func<HttpRequestMessage, string?, HttpResponseMessage> respond) =>
        WhenAsync(method, urlContains, (request, body) => Task.FromResult(respond(request, body)));

    /// <summary>As <see cref="When"/>, for a response that itself needs to be asynchronous (rare — every fixture in this folder is synthetic).</summary>
    public void WhenAsync(HttpMethod method, string urlContains, Func<HttpRequestMessage, string?, Task<HttpResponseMessage>> respond) =>
        _routes.Add((r => r.Method == method && r.RequestUri!.ToString().Contains(urlContains, StringComparison.Ordinal), respond));

    /// <summary>Registers a route that always throws <paramref name="exception"/> — simulating a transport-level failure (DNS, connection refused, a client-side timeout) rather than any HTTP response at all.</summary>
    public void WhenThrows(HttpMethod method, string urlContains, Exception exception) =>
        _routes.Add((r => r.Method == method && r.RequestUri!.ToString().Contains(urlContains, StringComparison.Ordinal), (_, _) => throw exception));

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        lock (_gate)
            _calls.Add(new RecordedHttpCall(request.Method, request.RequestUri!, body, request.Headers));

        var route = _routes.FirstOrDefault(r => r.Match(request));

        if (route.Respond is null)
        {
            throw new InvalidOperationException(
                $"Network guard: no recorded response for {request.Method} {request.RequestUri}. "
                + "This stub never contacts a real host — every scenario a test drives must register its own route first.");
        }

        return await route.Respond(request, body).ConfigureAwait(false);
    }
}
