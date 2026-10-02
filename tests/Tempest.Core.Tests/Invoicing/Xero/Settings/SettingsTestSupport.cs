using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing.Xero.Settings;

// ============================================================================
// `v0.24.0` X1 — the end-to-end rig for the settings reader: the real typed
// client over the real `XeroWriteSafetyHandler` over the S1 simulator, with
// a header-recording hop between them (the simulator's request log keeps the
// query, not `If-Modified-Since`), a real file cache in a temporary
// directory, and the simulator's hand-moved clock. No network, no sleeps.
// ============================================================================

/// <summary>Records each request's path and <c>If-Modified-Since</c> on its way to the simulator.</summary>
internal sealed class HeaderRecordingHandler : DelegatingHandler
{
    private readonly List<(string Path, DateTimeOffset? IfModifiedSince)> _seen = [];

    public IReadOnlyList<(string Path, DateTimeOffset? IfModifiedSince)> Seen
    {
        get
        {
            lock (_seen)
                return [.. _seen];
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_seen)
            _seen.Add((request.RequestUri!.AbsolutePath, request.Headers.IfModifiedSince));
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>An in-memory <see cref="ISettingsProvider"/> that behaves like the real one: definitions required, duplicates refused.</summary>
internal sealed class InMemorySettingsProvider : ISettingsProvider
{
    private readonly Dictionary<string, ISettingDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ISettingDefinition> Definitions => _definitions.Values;

    public void RegisterDefinition(ISettingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!_definitions.TryAdd(definition.Key, definition))
            throw new DuplicateSettingDefinitionException(definition.Key);
    }

    public Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default) =>
        _definitions.TryGetValue(key, out var definition)
            ? Task.FromResult(_values.TryGetValue(key, out var value) ? value : definition.DefaultValue)
            : throw new SettingNotFoundException(key);

    public Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (!_definitions.ContainsKey(key))
            throw new SettingNotFoundException(key);
        _values[key] = value;
        return Task.CompletedTask;
    }
}

/// <summary>
/// One simulated Xero tenant and a TempestOS-side reader over it. Several
/// readers may be created over the one cache directory (a restart).
/// </summary>
internal sealed class SettingsRig : IAsyncDisposable
{
    private readonly List<HttpClient> _clients = [];

    private SettingsRig(SimulatorTestKit kit, OAuthAuthoriser authoriser, InMemorySecretStore secretStore, TempDirectory directory)
    {
        Kit = kit;
        Authoriser = authoriser;
        SecretStore = secretStore;
        Directory = directory;
        Cache = new FileXeroSettingsCache(System.IO.Path.Combine(directory.Path, "accounts"));
    }

    public SimulatorTestKit Kit { get; }

    public XeroApiSimulator Simulator => Kit.Simulator;

    public XeroSimulatorClock Clock => Kit.Clock;

    public OAuthAuthoriser Authoriser { get; }

    public InMemorySecretStore SecretStore { get; }

    public TempDirectory Directory { get; }

    public FileXeroSettingsCache Cache { get; }

    public RecordingAuditRecorder Audit { get; } = new();

    public HeaderRecordingHandler Headers { get; private set; } = new();

    /// <summary>The reader the safety handler of the last <see cref="NewReader"/> pipeline resolves (D7).</summary>
    public IXeroSettingsReader? HandlerReader { get; set; }

    /// <summary>Whether Settings allows the live organisation (D7), as the safety handler reads it.</summary>
    public bool AllowLiveOrganisation { get; set; }

    public static async Task<SettingsRig> CreateAsync(bool isDemoCompany = true, IReadOnlyList<string>? grantedScopes = null)
    {
        var kit = new SimulatorTestKit(new XeroSimulatorOptions(
            TenantId: XeroTestAuthoriser.TenantId,
            AccessToken: XeroTestAuthoriser.AccessToken,
            IsDemoCompany: isDemoCompany,
            GrantedScopes: grantedScopes));
        var (authoriser, secretStore) = await XeroTestAuthoriser.CreateAsync();
        return new SettingsRig(kit, authoriser, secretStore, new TempDirectory());
    }

    /// <summary>A typed client over safety handler → header recorder → simulator.</summary>
    public XeroAccountingApi NewApi()
    {
        Headers = new HeaderRecordingHandler { InnerHandler = Simulator };
        var safety = new XeroWriteSafetyHandler(
            () => HandlerReader,
            _ => Task.FromResult(AllowLiveOrganisation),
            () => Audit,
            logger: null,
            timeProvider: Clock)
        {
            InnerHandler = Headers,
        };
        var client = new HttpClient(safety, disposeHandler: false) { BaseAddress = XeroApiSimulator.BaseAddress };
        _clients.Add(client);
        return new XeroAccountingApi(client, Authoriser, Clock);
    }

    /// <summary>A fresh reader (a fresh process) over the rig's cache file, also the one the safety handler resolves.</summary>
    public XeroSettingsReader NewReader(IXeroSettingsCache? cache = null)
    {
        var reader = new XeroSettingsReader(NewApi(), cache ?? Cache, SecretStore, Audit, Clock);
        HandlerReader = reader;
        return reader;
    }

    /// <summary>The requests to the settings endpoints, by path.</summary>
    public IReadOnlyList<string> SettingsCalls() =>
        [.. Simulator.Requests.Select(r => r.Path).Where(p => p is "Organisation" or "TaxRates" or "Accounts")];

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
            client.Dispose();
        Kit.Dispose();
        Directory.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>A cache whose writes always fail (a full or read-only disk).</summary>
internal sealed class FailingWriteCache : IXeroSettingsCache
{
    public Task<XeroSettingsReading?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<XeroSettingsReading?>(null);

    public Task SaveAsync(XeroSettingsReading reading, CancellationToken cancellationToken = default) => throw new IOException("Disk full.");
}
