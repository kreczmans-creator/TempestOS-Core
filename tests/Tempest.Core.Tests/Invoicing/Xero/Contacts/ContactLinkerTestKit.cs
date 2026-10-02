using System.Text.Json.Nodes;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

namespace Tempest.Core.Tests.Invoicing.Xero.Contacts;

/// <summary>
/// Loses the answer to the next <see cref="LoseWrites"/> writes after the
/// simulator has committed them (the lost-response case), and fails the next
/// <see cref="FailReadsAfterLoss"/> reads after a loss with a transport error —
/// only writes, so the linker's look-ups before a create are unaffected
/// (the simulator's own fault injection matches by path, which a
/// <c>GET Contacts</c> look-up shares with <c>PUT Contacts</c>).
/// </summary>
internal sealed class LostResponseHandler : DelegatingHandler
{
    public int LoseWrites { get; set; }

    public int FailReadsAfterLoss { get; set; }

    private int _failReads;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get && _failReads > 0)
        {
            _failReads--;
            throw new HttpRequestException("Simulated: the network dropped the look-up.");
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (request.Method != HttpMethod.Get && LoseWrites > 0)
        {
            LoseWrites--;
            _failReads = FailReadsAfterLoss;
            response.Dispose();
            throw new HttpRequestException("Simulated: the answer was lost after Xero committed the write.");
        }

        return response;
    }
}

/// <summary>
/// The X2 linker end to end: <see cref="XeroContactLinker"/> →
/// <see cref="XeroAccountingApi"/> → <see cref="XeroWriteSafetyHandler"/> →
/// <see cref="XeroRateLimiter"/> → (optionally a lost-response handler) →
/// <see cref="XeroApiSimulator"/>, over a real organisation catalog and the
/// B2 link store on an in-memory persistence store. No network, a hand-moved
/// clock.
/// </summary>
internal sealed class ContactLinkerTestKit : IDisposable
{
    public const string TenantId = XeroTestAuthoriser.TenantId;

    private readonly HttpClient _client;

    private ContactLinkerTestKit(
        XeroApiSimulator simulator, XeroSimulatorClock clock, HttpClient client, XeroAccountingApi api, InMemorySecretStore secretStore,
        OrganisationCatalog organisations, PersistenceXeroLinkStore links, RecordingAuditRecorder audit, RecordingAuditRecorder safetyAudit,
        LostResponseHandler lost, FakeSettingsReader settings, XeroContactLinkerOptions options)
    {
        Simulator = simulator;
        Clock = clock;
        _client = client;
        Api = api;
        SecretStore = secretStore;
        Organisations = organisations;
        Links = links;
        Audit = audit;
        SafetyAudit = safetyAudit;
        Lost = lost;
        Settings = settings;
        Linker = new XeroContactLinker(api, links, organisations, secretStore, audit, clock, options);
    }

    public XeroApiSimulator Simulator { get; }

    public XeroSimulatorClock Clock { get; }

    public XeroAccountingApi Api { get; }

    public InMemorySecretStore SecretStore { get; }

    public OrganisationCatalog Organisations { get; }

    public PersistenceXeroLinkStore Links { get; }

    public RecordingAuditRecorder Audit { get; }

    public RecordingAuditRecorder SafetyAudit { get; }

    public LostResponseHandler Lost { get; }

    public FakeSettingsReader Settings { get; }

    public XeroContactLinker Linker { get; }

    /// <summary>A mark in the simulator's request log, for <see cref="RequestsSince"/>.</summary>
    public int Mark => Simulator.Requests.Count;

    /// <summary>The requests the simulator received since <paramref name="mark"/>.</summary>
    public IReadOnlyList<XeroSimulatedRequest> RequestsSince(int mark) => [.. Simulator.Requests.Skip(mark)];

    /// <summary>The writes (anything but a read) the simulator received since <paramref name="mark"/>.</summary>
    public IReadOnlyList<XeroSimulatedRequest> WritesSince(int mark) => [.. RequestsSince(mark).Where(r => r.Method != HttpMethod.Get)];

    public static async Task<ContactLinkerTestKit> CreateAsync(
        XeroContactLinkerOptions? options = null, bool isDemoCompany = true, IReadOnlyList<string>? grantedScopes = null)
    {
        var clock = new XeroSimulatorClock();
        var simulator = new XeroApiSimulator(
            new XeroSimulatorOptions(TenantId: TenantId, AccessToken: XeroTestAuthoriser.AccessToken, GrantedScopes: grantedScopes),
            clock);

        var settings = new FakeSettingsReader { Cached = FakeSettingsReader.Reading(TenantId, isDemoCompany) };
        var safetyAudit = new RecordingAuditRecorder();
        var lost = new LostResponseHandler { InnerHandler = simulator };
        var rateLimiter = new XeroRateLimiter(clock) { InnerHandler = lost };
        var safety = new XeroWriteSafetyHandler(() => settings, _ => Task.FromResult(false), () => safetyAudit, timeProvider: clock)
        {
            InnerHandler = rateLimiter,
        };
        var client = new HttpClient(safety) { BaseAddress = XeroApiSimulator.BaseAddress };

        var (authoriser, secretStore) = await XeroTestAuthoriser.CreateAsync(TenantId);
        var api = new XeroAccountingApi(client, authoriser, clock);

        return new ContactLinkerTestKit(
            simulator, clock, client, api, secretStore, OperationsFixtures.BuildOrganisationCatalog(),
            new PersistenceXeroLinkStore(new YieldingInMemoryPersistenceStore()), new RecordingAuditRecorder(), safetyAudit, lost, settings,
            options ?? new XeroContactLinkerOptions());
    }

    public void Dispose()
    {
        _client.Dispose();
        Simulator.Dispose();
    }

    /// <summary>Registers a TempestOS customer or supplier (fictional).</summary>
    public async Task<Organisation> AddOrganisationAsync(
        string reference = "ACME1", string name = "Acme Engineering Ltd", string? customerCode = "ACME1", string? vat = null,
        string? companyNumber = "00000001", string? email = "accounts@acme.example", OrganisationTradingType type = OrganisationTradingType.Customer)
    {
        var organisation = new Organisation
        {
            Reference = reference,
            Name = name,
            CustomerCode = customerCode,
            TaxRegistration = vat,
            RegistrationNumber = companyNumber,
            EmailAddress = email,
            Status = RelationshipStatus.Active,
        };
        organisation = organisation with { Roles = organisation.RolesFor(type) };

        await OperationsFixtures.RegisterAsync(Organisations, reference, organisation);
        return organisation;
    }

    /// <summary>A contact entered in Xero by hand, with whatever extra fields the test gives (addresses, payment terms) — sent straight to the simulator, outside TempestOS's pipeline.</summary>
    public async Task<string> SeedContactAsync(JsonObject contact)
    {
        using var raw = new SimulatorTestKitClient(Simulator);
        return await raw.PutContactAsync(contact);
    }

    /// <summary>The contact as the simulator holds it.</summary>
    public JsonObject Contact(string contactId) => Simulator.Find("Contacts", contactId)!.Body;

    /// <summary>The <c>Contacts</c> the simulator holds, deleted ones excluded.</summary>
    public IReadOnlyList<XeroSimulatedDocument> LiveContacts => [.. Simulator.All("Contacts").Where(c => c.Status != "DELETED")];

    /// <summary>Asserts the simulator saw no contract or safety violation.</summary>
    public void AssertNoViolations() => Assert.Empty(Simulator.Violations);
}

/// <summary>A well-formed client straight onto the simulator, for seeding what a person entered in Xero by hand. Its requests are in the simulator's log too, so tests count TempestOS's own requests from a mark (<see cref="ContactLinkerTestKit.RequestsSince"/>).</summary>
internal sealed class SimulatorTestKitClient : IDisposable
{
    private static int _seeds;
    private readonly HttpClient _client;
    private readonly XeroApiSimulator _simulator;

    public SimulatorTestKitClient(XeroApiSimulator simulator)
    {
        _simulator = simulator;
        _client = simulator.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    public async Task<string> PutContactAsync(JsonObject contact)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "Contacts?summarizeErrors=true")
        {
            Content = new StringContent(new JsonObject { ["Contacts"] = new JsonArray { contact } }.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _simulator.Options.AccessToken);
        request.Headers.Add("xero-tenant-id", _simulator.Options.TenantId);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Idempotency-Key", $"seed:{Interlocked.Increment(ref _seeds)}");

        using var response = await _client.SendAsync(request);
        var reply = await SimulatorTestKit.ReadAsync(response);
        Assert.Equal(System.Net.HttpStatusCode.OK, reply.Status);
        return reply.First("Contacts")["ContactID"]!.GetValue<string>();
    }
}
