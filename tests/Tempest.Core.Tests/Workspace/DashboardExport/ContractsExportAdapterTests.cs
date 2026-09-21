using System.Text.Json.Nodes;
using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Contracts;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// Proves <see cref="ContractsExportAdapter"/> against a real, running
/// host's own <see cref="IIssuedContractCatalog"/>: the envelope the
/// dashboard's <c>contracts</c> connector reads (<c>{ contracts: [ … ] }</c>),
/// the documented <c>Contract</c> field set, the <see cref="ContractStatus"/>
/// mapping and the <c>yyyy-MM-dd</c> date format its <c>daysUntil</c>
/// parses.
/// </summary>
public class ContractsExportAdapterTests
{
    private static readonly DateOnly Today = BusinessGovernanceFixtures.Today;

    private static async Task<JsonNode> ExportAsync(ITempestHost host)
    {
        var adapter = new ContractsExportAdapter(DashboardExportTestHost.Contracts(host));

        using var stream = new MemoryStream();
        await adapter.ExportAsync(stream);
        stream.Position = 0;

        return JsonNode.Parse(stream) ?? throw new InvalidOperationException("Export produced no JSON.");
    }

    private static IssuedContract Contract(string reference, ContractStatus status, EffectivePeriod? term = null) => new()
    {
        Reference = reference,
        Title = $"{reference} fixture engagement",
        Parties = BusinessGovernanceFixtures.Parties(),
        Governance = BusinessGovernanceFixtures.Governance(),
        Status = status,
        Term = term,
        ExecutedOn = ContractStatuses.HasBeenExecuted(status) ? term?.From : null,
    };

    [Fact]
    public async Task ExportAsync_NoData_WritesTheEnvelopeWithAnEmptyArray()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var json = await ExportAsync(host);

        Assert.Equal(1, json["schemaVersion"]!.GetValue<int>());
        Assert.EndsWith("Z", json["generatedAt"]!.GetValue<string>());
        Assert.Empty(json["contracts"]!.AsArray());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_WritesTheDashboardsContractShape_Exactly()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var contracts = DashboardExportTestHost.Contracts(host);

        var term = new EffectivePeriod(new DateOnly(2026, 1, 15), new DateOnly(2026, 12, 31));
        await contracts.RegisterAsync("con-1", Contract("CON-1", ContractStatus.Executed, term), BusinessGovernanceFixtures.Verified());

        var json = await ExportAsync(host);
        var entry = Assert.Single(json["contracts"]!.AsArray())!;

        Assert.Equal(
            ["id", "reference", "client", "title", "status", "sentDate", "startDate", "endDate", "renewalDate", "source", "url"],
            entry.AsObject().Select(p => p.Key));

        Assert.Equal("con-1", entry["id"]!.GetValue<string>());
        Assert.Equal("CON-1", entry["reference"]!.GetValue<string>());
        Assert.Equal("Fictional Client Ltd", entry["client"]!.GetValue<string>());
        Assert.Equal("CON-1 fixture engagement", entry["title"]!.GetValue<string>());
        Assert.Equal("active", entry["status"]!.GetValue<string>());
        Assert.Null(entry["sentDate"]);
        Assert.Equal("2026-01-15", entry["startDate"]!.GetValue<string>());
        Assert.Equal("2026-12-31", entry["endDate"]!.GetValue<string>());
        Assert.Null(entry["renewalDate"]);
        Assert.Equal("tempestos", entry["source"]!.GetValue<string>());
        Assert.Null(entry["url"]);

        await manager.ShutdownAsync();
    }

    [Theory]
    [InlineData(ContractStatus.Draft, "draft")]
    [InlineData(ContractStatus.InNegotiation, "awaiting-signature")]
    [InlineData(ContractStatus.AwaitingSignature, "awaiting-signature")]
    [InlineData(ContractStatus.Executed, "active")]
    [InlineData(ContractStatus.Expired, "expired")]
    [InlineData(ContractStatus.Terminated, "expired")]
    [InlineData(ContractStatus.Lapsed, "expired")]
    [InlineData(ContractStatus.Superseded, "expired")]
    public void MapStatus_CoversEveryCoreStatus_WithADashboardWord(ContractStatus status, string expected)
    {
        Assert.Equal(expected, ContractsExportAdapter.MapStatus(status));
    }

    [Fact]
    public void MapStatus_OnlyEverProducesTheDashboardsVocabulary()
    {
        var dashboard = new HashSet<string>(["active", "awaiting-signature", "draft", "on-hold", "expired"]);

        foreach (var status in ContractStatuses.All)
            Assert.Contains(ContractsExportAdapter.MapStatus(status), dashboard);
    }

    [Fact]
    public async Task ExportAsync_OrdersByReference_AndLeavesOutSupersededRecords()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var contracts = (IssuedContractCatalog)DashboardExportTestHost.Contracts(host);

        await contracts.RegisterAsync("con-b", Contract("CON-B", ContractStatus.Draft), BusinessGovernanceFixtures.Verified());
        await contracts.RegisterAsync("con-a", Contract("CON-A", ContractStatus.AwaitingSignature), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(contracts, "con-a");
        await contracts.RegisterAsync("con-c", Contract("CON-C", ContractStatus.AwaitingSignature), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(contracts, "con-c");
        await contracts.SupersedeAsync("con-a", "con-c", "Replaced by CON-C.");

        var json = await ExportAsync(host);

        Assert.Equal(["CON-B", "CON-C"], json["contracts"]!.AsArray().Select(e => e!["reference"]!.GetValue<string>()));

        await manager.ShutdownAsync();
    }

    [Fact]
    public void ClientOf_PrefersThePartyContractingAsClient_ElseTheFirstParty()
    {
        var withClient = Contract("CON-1", ContractStatus.Draft);
        var noClientRole = withClient with
        {
            Parties = [new ContractParty("Acme Supplies Ltd", "Supplier"), new ContractParty("TestFixture Engineering Ltd", "Consultant")],
        };

        Assert.Equal("Fictional Client Ltd", ContractsExportAdapter.ClientOf(withClient));
        Assert.Equal("Acme Supplies Ltd", ContractsExportAdapter.ClientOf(noClientRole));
        _ = Today;
    }
}
