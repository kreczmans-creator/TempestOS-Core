using System.Net;
using System.Text;
using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Tests.Invoicing.Connectors;

namespace Tempest.Core.Tests.Invoicing.Xero.Api;

// ============================================================================
// `v0.24.0` task B1 — small, local fakes for the transport, safety-handler
// and rate-limiter tests. The full in-process Xero simulator is task S1's;
// these tests only need a terminal handler that records what reached "the
// network" and answers what each test says.
// ============================================================================

/// <summary>A clock tests move by hand — rate limits are measured on it, never on wall time.</summary>
internal sealed class ManualClock : TimeProvider
{
    public ManualClock(DateTimeOffset start) => Now = start;

    public DateTimeOffset Now { get; private set; }

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>The innermost handler: records every request that got this far (and its body) and answers with <see cref="Respond"/>.</summary>
internal sealed class TerminalHandler : HttpMessageHandler
{
    private readonly List<(HttpRequestMessage Request, string? Body)> _received = [];

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => Json(HttpStatusCode.OK, "{}");

    public Exception? Throw { get; set; }

    public IReadOnlyList<(HttpRequestMessage Request, string? Body)> Received => _received;

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null || request.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.Ordinal) != true
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        _received.Add((request, body));

        if (Throw is not null)
            throw Throw;

        var response = Respond(request);
        response.RequestMessage ??= request;
        return response;
    }
}

/// <summary>An <see cref="IXeroSettingsReader"/> answering whatever reading the test sets, counting refreshes.</summary>
internal sealed class FakeSettingsReader : IXeroSettingsReader
{
    public XeroSettingsReading? Cached { get; set; }

    /// <summary>What a refresh produces (and caches); <see langword="null"/> makes the refresh answer Unavailable.</summary>
    public XeroSettingsReading? OnRefresh { get; set; }

    public int Refreshes { get; private set; }

    public Task<XeroSettingsReading?> ReadCachedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Cached);

    public Task<ConnectorResult<XeroSettingsReading>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Refreshes++;

        if (OnRefresh is null)
            return Task.FromResult(ConnectorResult<XeroSettingsReading>.Unavailable("offline"));

        Cached = OnRefresh;
        return Task.FromResult(ConnectorResult<XeroSettingsReading>.Ok(OnRefresh));
    }

    public static XeroSettingsReading Reading(string tenantId, bool isDemoCompany, string name = "Demo Company (UK)") => new(
        XeroSettingsReading.CurrentSchemaVersion,
        tenantId,
        new XeroOrganisationProfile("org-1", name, name, null, null, null, null, null, "GBP", "GB", true, isDemoCompany, []),
        [],
        [],
        DateTimeOffset.UnixEpoch);
}

/// <summary>An <see cref="IAuditRecorder"/> that keeps every row.</summary>
internal sealed class RecordingAuditRecorder : IAuditRecorder
{
    public List<(string Action, IReadOnlyDictionary<string, string>? Detail)> Rows { get; } = [];

    public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default)
    {
        Rows.Add((action, detail));
        return Task.CompletedTask;
    }
}

/// <summary>Builds an <see cref="OAuthAuthoriser"/> for Xero with a current token stored — no network.</summary>
internal static class XeroTestAuthoriser
{
    public const string AccessToken = "seeded-access-token";
    public const string TenantId = "tenant-1";

    public static async Task<(OAuthAuthoriser Authoriser, InMemorySecretStore SecretStore)> CreateAsync(
        string? tenantId = TenantId, bool authorised = true, IReadOnlyList<string>? grantedScopes = null)
    {
        var secretStore = new InMemorySecretStore();

        if (authorised)
        {
            await secretStore.SetAsync("Invoicing:Xero:AccessToken", AccessToken);
            await secretStore.SetAsync("Invoicing:Xero:RefreshToken", "seeded-refresh-token");
            await secretStore.SetAsync("Invoicing:Xero:ExpiresAtUtc", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
            if (grantedScopes is not null)
                await secretStore.SetAsync($"Invoicing:Xero:{OAuthAuthoriser.GrantedScopesKeySuffix}", string.Join(' ', grantedScopes));
        }

        if (tenantId is not null)
            await secretStore.SetAsync("Invoicing:Xero:TenantId", tenantId);

        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>("Invoicing:Xero:ClientId", "client-abc")]))
            .Build();

        // The token client never answers: a test that needed a refresh would fail loudly, not reach a network.
        var tokenClient = new HttpClient(new TerminalHandler { Throw = new InvalidOperationException("No token endpoint in this test.") });

        return (new OAuthAuthoriser(XeroServiceRegistration.OAuthProfile, configuration, secretStore, new FakeBrowserLauncher(), tokenClient), secretStore);
    }
}
