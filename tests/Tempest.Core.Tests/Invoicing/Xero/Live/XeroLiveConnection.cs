using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Persistence;
using Tempest.Core.Secrets;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Invoicing.Xero.Api;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>One request that left TempestOS's Xero pipeline below the safety handler — what actually reached Xero (or the simulator).</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="PathAndQuery">The path and query, as sent.</param>
/// <param name="JsonBody">The JSON body; <see langword="null"/> for none or a binary (attachment) body.</param>
public sealed record XeroOutgoingRequest(string Method, string PathAndQuery, string? JsonBody);

/// <summary>
/// Records every request that got past <see cref="XeroWriteSafetyHandler"/>
/// and <see cref="XeroRateLimiter"/> — the live run's own equivalent of the
/// simulator's <c>Violations</c> log: the smoke journey checks afterwards
/// that nothing that left the machine approved, emailed or wrote outside the
/// allow-list (D3, D4). Holds no header, so never a token.
/// </summary>
internal sealed class XeroRequestJournal : DelegatingHandler
{
    private readonly List<XeroOutgoingRequest> _requests = [];
    private readonly Lock _gate = new();

    /// <summary>Every request recorded so far, in order.</summary>
    public IReadOnlyList<XeroOutgoingRequest> Requests
    {
        get
        {
            lock (_gate)
                return [.. _requests];
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = null;
        if (request.Content?.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        lock (_gate)
            _requests.Add(new XeroOutgoingRequest(request.Method.Method, request.RequestUri?.PathAndQuery ?? string.Empty, body));

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// The Xero pipeline the smoke journey runs over, composed exactly as
/// <c>XeroServiceRegistration.Compose</c> composes TempestOS's own:
/// <see cref="XeroAccountingApi"/> → <see cref="XeroWriteSafetyHandler"/>
/// (with <b>Allow the live organisation</b> hard-wired off — D7 can never be
/// relaxed by a test) → <see cref="XeroRateLimiter"/> →
/// <see cref="XeroRequestJournal"/> → the network (live) or the in-process
/// simulator (CI). The real <see cref="XeroSettingsReader"/> feeds the
/// safety handler, over a cache in a temporary folder so the app's own
/// cache is never touched.
/// </summary>
internal sealed class XeroLiveConnection : IDisposable
{
    private readonly HttpClient _apiClient;
    private readonly HttpClient _tokenClient;
    private readonly string _cacheFolder;

    private XeroLiveConnection(
        XeroAccountingApi api, OAuthAuthoriser authoriser, XeroSettingsReader settingsReader, XeroRequestJournal journal,
        RecordingAuditRecorder audit, HttpClient apiClient, HttpClient tokenClient, string cacheFolder)
    {
        Api = api;
        Authoriser = authoriser;
        SettingsReader = settingsReader;
        Journal = journal;
        Audit = audit;
        _apiClient = apiClient;
        _tokenClient = tokenClient;
        _cacheFolder = cacheFolder;
    }

    /// <summary>The typed client.</summary>
    public XeroAccountingApi Api { get; }

    /// <summary>The OAuth authoriser over the chosen secret store.</summary>
    public OAuthAuthoriser Authoriser { get; }

    /// <summary>The X1 settings reader (also what the safety handler reads for D7).</summary>
    public XeroSettingsReader SettingsReader { get; }

    /// <summary>What reached Xero.</summary>
    public XeroRequestJournal Journal { get; }

    /// <summary>The audit rows written by the safety handler and the settings reader.</summary>
    public RecordingAuditRecorder Audit { get; }

    /// <summary>
    /// The pipeline over the real network, with credentials chosen by
    /// <paramref name="settings"/> (see <see cref="XeroLiveSettings"/>).
    /// </summary>
    /// <param name="settings">What the operator asked for.</param>
    public static async Task<XeroLiveConnection> CreateLiveAsync(XeroLiveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var configuration = BuildConfiguration(settings);
        ISecretStore secretStore;

        if (settings.UsesSuppliedToken)
        {
            var supplied = new InMemorySecretStore();
            await supplied.SetAsync("Invoicing:Xero:AccessToken", settings.Trimmed(XeroLiveSettings.AccessTokenVariable)!);
            // A refresh token must exist for the authoriser to use the access
            // token at all; this one is never sent (the token is used until it
            // expires, then the run reports Reauthorise).
            await supplied.SetAsync("Invoicing:Xero:RefreshToken", "supplied-access-token-is-not-refreshed");
            await supplied.SetAsync("Invoicing:Xero:ExpiresAtUtc", DateTimeOffset.UtcNow.AddMinutes(25).ToString("O", CultureInfo.InvariantCulture));
            if (settings.Trimmed(XeroLiveSettings.TenantIdVariable) is { } tenant)
                await supplied.SetAsync("Invoicing:Xero:TenantId", tenant);
            secretStore = supplied;
        }
        else
        {
            // The same store TempestHost picks, over the same data folder.
            secretStore = OperatingSystem.IsWindows()
                ? new WindowsDpapiSecretStore(configuration)
                : new FileSecretStore(configuration);
        }

        var tokenClient = new HttpClient(new InvoicingHttpLoggingHandler(NullLogger.Instance));
        var authoriser = new OAuthAuthoriser(XeroServiceRegistration.OAuthProfile, configuration, secretStore, new SystemBrowserLauncher(), tokenClient);

        return Compose(authoriser, secretStore, new HttpClientHandler(), XeroServiceRegistration.ApiBaseAddress, TimeProvider.System, tokenClient);
    }

    /// <summary>The same pipeline over <paramref name="network"/> (the in-process simulator) and <paramref name="authoriser"/>.</summary>
    /// <param name="authoriser">An authoriser holding the simulator's token and tenant.</param>
    /// <param name="secretStore">The authoriser's secret store (the settings reader reads the tenant from it).</param>
    /// <param name="network">The innermost handler.</param>
    /// <param name="baseAddress">The API root.</param>
    /// <param name="time">The clock shared with the simulator.</param>
    public static XeroLiveConnection CreateOver(OAuthAuthoriser authoriser, ISecretStore secretStore, HttpMessageHandler network, Uri baseAddress, TimeProvider time) =>
        Compose(authoriser, secretStore, network, baseAddress, time, new HttpClient(new TerminalHandler { Throw = new InvalidOperationException("No token endpoint in this test.") }));

    private static XeroLiveConnection Compose(
        OAuthAuthoriser authoriser, ISecretStore secretStore, HttpMessageHandler network, Uri baseAddress, TimeProvider time, HttpClient tokenClient)
    {
        var audit = new RecordingAuditRecorder();
        var journal = new XeroRequestJournal { InnerHandler = network };
        var rateLimiter = new XeroRateLimiter(time) { InnerHandler = journal };

        XeroSettingsReader? reader = null;
        var safety = new XeroWriteSafetyHandler(
            settingsReader: () => reader,
            allowLiveOrganisation: _ => Task.FromResult(false),
            auditRecorder: () => audit,
            timeProvider: time)
        {
            InnerHandler = rateLimiter,
        };

        var apiClient = new HttpClient(safety) { BaseAddress = baseAddress };
        var api = new XeroAccountingApi(apiClient, authoriser, time);

        var cacheFolder = Path.Combine(Path.GetTempPath(), "tempest-xero-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheFolder);
        reader = new XeroSettingsReader(api, new FileXeroSettingsCache(cacheFolder), secretStore, audit, time);

        return new XeroLiveConnection(api, authoriser, reader, journal, audit, apiClient, tokenClient, cacheFolder);
    }

    private static IConfigurationProvider BuildConfiguration(XeroLiveSettings settings)
    {
        var entries = new List<KeyValuePair<string, string>>();

        if (settings.DataFolder is { } folder)
            entries.Add(new(SqlitePersistenceStore.RootPathConfigurationKey, folder));
        if (settings.Trimmed(XeroLiveSettings.ClientIdVariable) is { } clientId)
            entries.Add(new("Invoicing:Xero:ClientId", clientId));
        if (settings.Trimmed(XeroLiveSettings.ClientSecretVariable) is { } clientSecret)
            entries.Add(new("Invoicing:Xero:ClientSecret", clientSecret));

        return new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(entries)).Build();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _apiClient.Dispose();
        _tokenClient.Dispose();

        try
        {
            Directory.Delete(_cacheFolder, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder left behind is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // Likewise.
        }
    }

    /// <summary>The journal as text lines (method and path only), for the report.</summary>
    public string DescribeJournal()
    {
        var text = new StringBuilder();
        foreach (var request in Journal.Requests)
            text.Append(request.Method).Append(' ').AppendLine(request.PathAndQuery);
        return text.ToString();
    }
}
