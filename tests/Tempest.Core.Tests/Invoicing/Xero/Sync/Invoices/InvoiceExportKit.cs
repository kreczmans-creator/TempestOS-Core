using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Tempest.Workspace;
using Tempest.Workspace.Projects;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Settings;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Invoices;

/// <summary>How <see cref="InvoiceWriteLossHandler"/> loses an answer Xero has already committed.</summary>
internal enum AnswerLoss
{
    /// <summary>The connection drops: a transport failure (the typed client reports Unavailable).</summary>
    Dropped,

    /// <summary>Xero answers 200 with an empty body (the typed client reports Unknown).</summary>
    EmptyBody,
}

/// <summary>
/// Loses the answer to the next <see cref="LoseInvoiceCreates"/> invoice
/// creates (<c>PUT Invoices</c>) after the simulator has committed them — only
/// creates, so the number look-up before them is unaffected (the simulator's
/// own fault injection matches by path, which <c>GET Invoices</c> shares).
/// </summary>
internal sealed class InvoiceWriteLossHandler : DelegatingHandler
{
    public int LoseInvoiceCreates { get; set; }

    public AnswerLoss Loss { get; set; } = AnswerLoss.Dropped;

    /// <summary>Runs just before each request reaches the simulator — a back-office act racing TempestOS's own call.</summary>
    public Action<HttpRequestMessage>? BeforeSend { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        BeforeSend?.Invoke(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.EndsWith("/Invoices", StringComparison.Ordinal) && LoseInvoiceCreates > 0)
        {
            LoseInvoiceCreates--;
            response.Dispose();
            if (Loss == AnswerLoss.Dropped)
                throw new HttpRequestException("Simulated: the answer was lost after Xero committed the invoice.");

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty, Encoding.UTF8, "application/json") };
        }

        return response;
    }
}

/// <summary>An <see cref="IXeroDocumentFileSource"/> holding whatever PDFs a test saves (X6 provides the real one).</summary>
internal sealed class FakeDocumentFiles : IXeroDocumentFileSource
{
    private readonly Dictionary<XeroDocumentRef, XeroDocumentFile> _files = [];

    public XeroDocumentFile Save(Guid requestId, string fileName, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var file = new XeroDocumentFile(fileName, "application/pdf", bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        _files[XeroInvoiceDrafts.DocumentFor(requestId)] = file;
        return file;
    }

    public Task<XeroDocumentFile?> FindAsync(XeroDocumentRef document, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.TryGetValue(document, out var file) ? file : null);
}

/// <summary>
/// X4 end to end: a real host (projects, completions, invoice requests) and
/// an <see cref="InvoicingService"/> bound to the real <see cref="XeroConnector"/>
/// and <see cref="XeroInvoiceDrafts"/> over the typed client →
/// <see cref="XeroWriteSafetyHandler"/> → <see cref="XeroRateLimiter"/> →
/// <see cref="InvoiceWriteLossHandler"/> → <see cref="XeroApiSimulator"/>,
/// with the real X1 reader (refreshed once from the simulator), the real X2
/// linker and the real B2 link store and outbox. No network; the simulator's
/// hand-moved clock.
/// </summary>
internal sealed class InvoiceExportKit : IAsyncDisposable
{
    public const string TenantId = XeroTestAuthoriser.TenantId;
    public static readonly DateOnly Week = new(2026, 3, 2);

    private readonly HttpClient _client;

    private InvoiceExportKit(
        TempDirectory temp, ITempestHost host, WorkspaceManager manager, XeroApiSimulator simulator, XeroSimulatorClock clock, HttpClient client,
        XeroAccountingApi api, XeroConnector connector, InvoiceWriteLossHandler loss, XeroSettingsReader reader, InMemorySettingsProvider settings)
    {
        Temp = temp;
        Host = host;
        Manager = manager;
        Simulator = simulator;
        Clock = clock;
        _client = client;
        Api = api;
        Connector = connector;
        Loss = loss;
        Reader = reader;
        Settings = settings;
        Store = new YieldingInMemoryPersistenceStore();
        Links = new PersistenceXeroLinkStore(Store);
        Outbox = new PersistenceXeroOutbox(Store, null, clock);
        Files = new FakeDocumentFiles();
    }

    public TempDirectory Temp { get; }

    public ITempestHost Host { get; }

    public WorkspaceManager Manager { get; }

    public XeroApiSimulator Simulator { get; }

    public XeroSimulatorClock Clock { get; }

    public XeroAccountingApi Api { get; }

    public XeroConnector Connector { get; }

    public InvoiceWriteLossHandler Loss { get; }

    public XeroSettingsReader Reader { get; }

    public InMemorySettingsProvider Settings { get; }

    public YieldingInMemoryPersistenceStore Store { get; }

    public PersistenceXeroLinkStore Links { get; }

    public PersistenceXeroOutbox Outbox { get; }

    public FakeDocumentFiles Files { get; }

    public RecordingAuditRecorder Audit { get; } = new();

    public RecordingAuditRecorder SafetyAudit { get; } = new();

    public Tempest.Core.Secrets.ISecretStore SecretStore { get; private set; } = null!;

    public XeroContactLinker Linker { get; private set; } = null!;

    public XeroInvoiceDrafts Drafts { get; private set; } = null!;

    public InvoicingService Service { get; private set; } = null!;

    public EngineeringDomainContext Domain => InvoicingTestHost.Domain(Host);

    /// <param name="tokenEndpoint">`v0.24.0` F2 follow-up (additive): what answers the token endpoint; <see langword="null"/> for one that fails the test loudly.</param>
    public static async Task<InvoiceExportKit> CreateAsync(HttpMessageHandler? tokenEndpoint = null)
    {
        var temp = new TempDirectory();
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);

        var clock = new XeroSimulatorClock();
        var simulator = new XeroApiSimulator(new XeroSimulatorOptions(TenantId: TenantId, AccessToken: XeroTestAuthoriser.AccessToken), clock);

        IXeroSettingsReader? handlerReader = null;
        var safetyAudit = new RecordingAuditRecorder();
        var loss = new InvoiceWriteLossHandler { InnerHandler = simulator };
        var rateLimiter = new XeroRateLimiter(clock) { InnerHandler = loss };
        var safety = new XeroWriteSafetyHandler(() => handlerReader, _ => Task.FromResult(false), () => safetyAudit, timeProvider: clock) { InnerHandler = rateLimiter };
        var client = new HttpClient(safety) { BaseAddress = XeroApiSimulator.BaseAddress };

        var (authoriser, secretStore) = await XeroTestAuthoriser.CreateAsync(TenantId, tokenEndpoint: tokenEndpoint);
        var api = new XeroAccountingApi(client, authoriser, clock);
        var connector = new XeroConnector(client, authoriser);
        var reader = new XeroSettingsReader(api, new FileXeroSettingsCache(Path.Combine(temp.Path, "accounts")), secretStore, null, clock);
        handlerReader = reader;
        Assert.Equal(ConnectorOutcome.Ok, (await reader.RefreshAsync()).Outcome);

        var kit = new InvoiceExportKit(temp, host, manager, simulator, clock, client, api, connector, loss, reader, new InMemorySettingsProvider());
        kit.SecretStore = secretStore;
        kit.Linker = new XeroContactLinker(
            api, kit.Links, InvoicingTestHost.Organisations(host), secretStore, kit.Audit, clock,
            new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false }, attemptStore: new YieldingInMemoryPersistenceStore());
        kit.Drafts = kit.NewDrafts();
        kit.Service = kit.NewService(kit.Drafts);
        return kit;
    }

    /// <summary>A drafts seam over the kit's stores — a fresh one is TempestOS restarted.</summary>
    public XeroInvoiceDrafts NewDrafts() => new(
        Connector, Linker, new XeroTaxTypeResolver(Reader, Settings), new XeroAccountCodeMap(Reader, Settings), Links, Outbox, Files, Settings, Audit, Clock,
        new XeroPurchasingCreateLog(Store)); // `v0.24.0` review m16: the durable create log is the proof an invoice is TempestOS's own

    /// <summary>An invoicing service bound to the Xero connector; <paramref name="drafts"/> <see langword="null"/> is a build with no X4 seam.</summary>
    public InvoicingService NewService(IInvoiceDraftSync? drafts) => new(
        Domain, InvoicingTestHost.RateCards(Host), InvoicingTestHost.Timesheets(Host), InvoicingTestHost.Deliverables(Host),
        Connector, InvoicingTestHost.Organisations(Host), Clock, expenses: null, draftSync: drafts);

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        Simulator.Dispose();
        await Manager.ShutdownAsync();
        await Host.DisposeAsync();
        Temp.Dispose();
    }

    /// <summary>A client organisation, a project (project-centric number <paramref name="projectIdentifier"/>), a released rate card.</summary>
    public async Task<(Guid ProjectId, string OrganisationId)> AddProjectAsync(string suffix, string projectIdentifier = "ACME1-BRIDG1", PaymentTerms terms = PaymentTerms.Days30)
    {
        var organisationId = $"INV-CLIENT-{suffix}";
        var rateCardId = $"INV-CARD-{suffix}";

        var organisations = InvoicingTestHost.Organisations(Host);
        await organisations.RegisterAsync(
            organisationId, OperationsFixtures.Organisation(organisationId) with { PaymentTerms = terms }, OperationsFixtures.Verified());

        var rateCards = InvoicingTestHost.RateCards(Host);
        await rateCards.RegisterAsync(rateCardId, OneGradeCard(rateCardId), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, rateCardId);

        var projectId = await InvoicingTestHost.CreateProjectAsync(Host, projectIdentifier);
        var commercial = InvoicingTestHost.ProjectCommercial(Host);
        Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
        Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);

        return (projectId, organisationId);
    }

    /// <summary>The client exists in Xero (entered by hand) and the Product Owner linked it (X2); returns its <c>ContactID</c>.</summary>
    public async Task<string> LinkClientAsync(string organisationId)
    {
        var contactId = Simulator.SeedContact("Fictional Client Ltd");
        var linked = await Linker.LinkExistingAsync(organisationId, contactId);
        Assert.Equal(ConnectorOutcome.Ok, linked.Outcome);
        return contactId;
    }

    /// <summary>Completes a fixed-price deliverable named "Deliverable {suffix}"; the completion hook raises the Draft request.</summary>
    public async Task<InvoiceRequest> RaiseAsync(Guid projectId, string suffix, decimal fixedPrice = 1200m)
    {
        var milestones = new ProjectMilestoneService(Domain);
        var milestone = await milestones.CreateMilestoneAsync(projectId, $"MS-{suffix}", $"Milestone {suffix}", DateTimeOffset.UtcNow.AddDays(30));
        var deliverable = await milestones.CreateDeliverableAsync(projectId, milestone.Id, $"DEL-{suffix}", $"Deliverable {suffix}");

        var completion = await InvoicingTestHost.Deliverables(Host).CompleteAsync(deliverable.Id, projectId, Week, fixedPriceValue: new Money(fixedPrice, CurrencyCode.Gbp));
        Assert.True(completion.Succeeded);

        return await InvoicingTestHost.RequestRaisedByCompletionAsync(Host, completion.Completion!.Id);
    }

    /// <summary>Everything to a sent request: project, linked client, raised request, sent through Xero.</summary>
    public async Task<(InvoiceRequest Request, string ContactId)> SendNewAsync(string suffix, string projectIdentifier = "ACME1-BRIDG1")
    {
        var (projectId, organisationId) = await AddProjectAsync(suffix, projectIdentifier);
        var contactId = await LinkClientAsync(organisationId);
        var request = await RaiseAsync(projectId, suffix);

        var sent = await Service.SendAsync(request.Id);
        Assert.True(sent.Succeeded, sent.Reason);
        Assert.Equal(InvoiceRequestStatus.Sent, sent.Request!.Status);
        return (sent.Request, contactId);
    }

    /// <summary>Renames the deliverable whose completion <paramref name="request"/> bills — what its Xero <c>Reference</c> is built from.</summary>
    public async Task RenameDeliverableAsync(InvoiceRequest request, string newName)
    {
        var line = Assert.Single(request.Lines, l => l.SourceKind == DeliverableCompletion.CanonicalKind);
        var completion = Assert.IsType<DeliverableCompletion>(await Domain.Repository.FindAsync(line.SourceId));
        var deliverable = Assert.IsAssignableFrom<EngineeringObjectBase>(await Domain.Repository.FindAsync(completion.DeliverableId));
        await deliverable.RenameAsync(newName);
    }

    /// <summary>The ACCREC invoices the simulator holds that are not deleted or voided.</summary>
    public IReadOnlyList<XeroSimulatedDocument> LiveSalesInvoices =>
        [.. SalesInvoices.Where(d => d.Status is not ("DELETED" or "VOIDED"))];

    /// <summary>The request as stored now.</summary>
    public async Task<InvoiceRequest> ReloadAsync(Guid requestId) => (InvoiceRequest)(await Domain.Repository.FindAsync(requestId))!;

    /// <summary>The ACCREC invoices the simulator holds (deleted ones included).</summary>
    public IReadOnlyList<XeroSimulatedDocument> SalesInvoices =>
        [.. Simulator.All("Invoices").Where(d => (string?)d.Body["Type"] == "ACCREC")];

    public XeroSimulatedDocument Invoice(string invoiceId) => Simulator.Find("Invoices", invoiceId)!;

    public Task<XeroLink?> LinkAsync(Guid requestId) => Links.FindAsync(TenantId, XeroInvoiceDrafts.DocumentFor(requestId));

    /// <summary>A mark in the simulator's request log.</summary>
    public int Mark => Simulator.Requests.Count;

    public IReadOnlyList<XeroSimulatedRequest> RequestsSince(int mark) => [.. Simulator.Requests.Skip(mark)];

    /// <summary>
    /// The safety assertions every X4 test ends with: no contract or safety
    /// violation (D3/D4/D7, contact-by-name), no request to an <c>/Email</c>
    /// path, no invoice write naming a contact other than by <c>ContactID</c>,
    /// and no invoice that TempestOS moved past <c>DRAFT</c>.
    /// </summary>
    public void AssertSafe()
    {
        Assert.Empty(Simulator.Violations);
        Assert.DoesNotContain(Simulator.Requests, r => r.Path.EndsWith("/Email", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(SafetyAudit.Rows);

        foreach (var write in Simulator.Requests.Where(r => r.Method != HttpMethod.Get && r.Path.StartsWith("Invoices", StringComparison.Ordinal) && r.JsonBody is not null))
        {
            foreach (var invoice in write.JsonBody!["Invoices"]!.AsArray())
            {
                var status = (string?)invoice!["Status"];
                Assert.True(status is null or "DRAFT" or "DELETED", $"TempestOS wrote invoice status {status}.");
                Assert.Null(invoice["SentToContact"]);
                if (invoice["Contact"] is JsonObject contact)
                {
                    Assert.False(string.IsNullOrWhiteSpace((string?)contact["ContactID"]));
                    Assert.Null(contact["Name"]);
                }
            }
        }
    }

    /// <summary>A person enters an invoice in Xero by hand (outside TempestOS's pipeline); returns its id.</summary>
    public async Task<string> EnterInvoiceInXeroAsync(string contactId, string number, string reference)
    {
        using var raw = Simulator.CreateClient();
        var body = new JsonObject
        {
            ["Invoices"] = new JsonArray
            {
                new JsonObject
                {
                    ["Type"] = "ACCREC",
                    ["Contact"] = new JsonObject { ["ContactID"] = contactId },
                    ["InvoiceNumber"] = number,
                    ["Reference"] = reference,
                    ["Date"] = "2026-09-01",
                    ["DueDate"] = "2026-10-01",
                    ["LineAmountTypes"] = "Exclusive",
                    ["LineItems"] = new JsonArray { SimulatorTestKit.Line("Hand-entered work", 1m, 100m, "OUTPUT2", "200") },
                },
            },
        };
        using var request = new HttpRequestMessage(HttpMethod.Put, "Invoices?summarizeErrors=true")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Simulator.Options.AccessToken);
        request.Headers.Add("xero-tenant-id", Simulator.Options.TenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Idempotency-Key", $"hand:{Guid.NewGuid():N}");

        using var response = await raw.SendAsync(request);
        var reply = await SimulatorTestKit.ReadAsync(response);
        Assert.Equal(HttpStatusCode.OK, reply.Status);
        return reply.First("Invoices")["InvoiceID"]!.GetValue<string>();
    }

    private static RateCard OneGradeCard(string code) => new()
    {
        Code = code,
        Name = "X4 rate card",
        EffectivePeriod = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
        Currency = CurrencyCode.Gbp,
        Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
        Entries =
        [
            new RateCardEntry("ENG-1", "Senior engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), new Money(90m, CurrencyCode.Gbp), Grade: "Senior"),
        ],
    };
}
