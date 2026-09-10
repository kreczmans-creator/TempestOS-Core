using Tempest.Core.Invoicing.OAuth;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// The test double for <see cref="IBrowserLauncher"/> the brief itself
/// names: "a fake browser launcher that calls the loopback itself"
/// (`WP 19.1A` part 2 brief §3). Never opens a real browser — reads the
/// authorisation URL's own <c>redirect_uri</c> and <c>state</c> query
/// parameters and issues a real, local HTTP GET straight back to the
/// loopback listener with a scripted <c>code</c>/<c>error</c>/<c>realmId</c>,
/// simulating the operator's own consent (or refusal) inside the same
/// process — no browser, no network beyond <c>127.0.0.1</c>.
/// </summary>
internal sealed class FakeBrowserLauncher : IBrowserLauncher
{
    private readonly HttpClient _httpClient = new();

    /// <summary>The authorisation code the simulated redirect carries. Ignored when <see cref="Error"/> is set.</summary>
    public string? Code { get; set; } = "test-authorisation-code";

    /// <summary>When set, the simulated redirect carries this <c>error</c> instead of a code — the operator denying consent.</summary>
    public string? Error { get; set; }

    /// <summary>When set, the simulated redirect also carries this as its own <c>realmId</c> query parameter (QuickBooks Online's own tenant id, carried on the redirect itself).</summary>
    public string? RealmId { get; set; }

    /// <summary>When <see langword="true"/>, the simulated redirect carries a <c>state</c> value that does not match what the authorisation URL itself carried — <see cref="OAuthAuthoriser.AuthoriseAsync"/>'s own state check must refuse it.</summary>
    public bool CorruptState { get; set; }

    /// <summary>The authorisation URL the last <see cref="Open"/> call received.</summary>
    public Uri? LastAuthorizationUrl { get; private set; }

    /// <summary>The background task simulating the redirect — a test awaits this after the authoriser's own call completes, to surface any exception the simulated GET itself raised rather than have it vanish silently.</summary>
    public Task? LastSimulatedRedirect { get; private set; }

    /// <inheritdoc />
    public void Open(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        LastAuthorizationUrl = url;

        // Fire-and-forget, deliberately: OAuthAuthoriser.AuthoriseAsync
        // calls Open synchronously and then awaits the loopback's own
        // WaitForCallbackAsync immediately afterward — blocking here for
        // this GET's own response would deadlock, since nothing answers it
        // until that later await runs.
        LastSimulatedRedirect = SimulateProviderRedirectAsync(url);
    }

    private async Task SimulateProviderRedirectAsync(Uri url)
    {
        var query = ParseQuery(url.Query);
        var redirectUri = query["redirect_uri"];
        var state = query["state"];

        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var callbackState = CorruptState ? "wrong-state-injected-by-test" : state;
        var callbackUrl = new Uri(redirectUri).AbsoluteUri + $"?state={Uri.EscapeDataString(callbackState)}";

        if (Error is not null)
            callbackUrl += $"&error={Uri.EscapeDataString(Error)}";
        else if (Code is not null)
            callbackUrl += $"&code={Uri.EscapeDataString(Code)}";

        if (RealmId is not null)
            callbackUrl += $"&realmId={Uri.EscapeDataString(RealmId)}";

        using var response = await _httpClient.GetAsync(callbackUrl).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var trimmed = query.TrimStart('?');

        if (trimmed.Length == 0)
            return result;

        foreach (var pair in trimmed.Split('&'))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
                continue;

            result[Uri.UnescapeDataString(pair[..separatorIndex])] = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
        }

        return result;
    }
}
