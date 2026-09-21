using System.Text.Json.Nodes;
using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Core.BusinessGovernance.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// Proves <see cref="QuotesExportAdapter"/> against a real, running host's
/// own <see cref="IQuotationCatalog"/>: the envelope the dashboard's
/// <c>quotes</c> connector reads (<c>{ quotes: [ … ] }</c>), the
/// documented <c>Quote</c> field set, the four-word status vocabulary and
/// the <c>yyyy-MM-dd</c> date format.
/// </summary>
public class QuotesExportAdapterTests
{
    private static async Task<JsonNode> ExportAsync(ITempestHost host)
    {
        var adapter = new QuotesExportAdapter(DashboardExportTestHost.Quotations(host));

        using var stream = new MemoryStream();
        await adapter.ExportAsync(stream);
        stream.Position = 0;

        return JsonNode.Parse(stream) ?? throw new InvalidOperationException("Export produced no JSON.");
    }

    [Fact]
    public async Task ExportAsync_NoData_WritesTheEnvelopeWithAnEmptyArray()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var json = await ExportAsync(host);

        Assert.Equal(1, json["schemaVersion"]!.GetValue<int>());
        Assert.EndsWith("Z", json["generatedAt"]!.GetValue<string>());
        Assert.Empty(json["quotes"]!.AsArray());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_WritesTheDashboardsQuoteShape_Exactly()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var quotations = DashboardExportTestHost.Quotations(host);

        var quotation = BusinessGovernanceFixtures.Quote("QUO-034", QuotationStatus.Submitted) with
        {
            Amount = BusinessGovernanceFixtures.Gbp_(8_500m),
            SubmittedOn = new DateOnly(2026, 2, 15),
            FollowUpOn = new DateOnly(2026, 2, 28),
        };

        await quotations.RegisterAsync("quo-034", quotation, BusinessGovernanceFixtures.Verified());

        var json = await ExportAsync(host);
        var entry = Assert.Single(json["quotes"]!.AsArray())!;

        Assert.Equal(
            ["id", "reference", "client", "amount", "currency", "status", "submittedDate", "followUpDate", "source"],
            entry.AsObject().Select(p => p.Key));

        Assert.Equal("quo-034", entry["id"]!.GetValue<string>());
        Assert.Equal("QUO-034", entry["reference"]!.GetValue<string>());
        Assert.Equal("Fictional Client Ltd", entry["client"]!.GetValue<string>());
        Assert.Equal(8500m, entry["amount"]!.GetValue<decimal>());
        Assert.Equal("GBP", entry["currency"]!.GetValue<string>());
        Assert.Equal("submitted", entry["status"]!.GetValue<string>());
        Assert.Equal("2026-02-15", entry["submittedDate"]!.GetValue<string>());
        Assert.Equal("2026-02-28", entry["followUpDate"]!.GetValue<string>());
        Assert.Equal("tempestos", entry["source"]!.GetValue<string>());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_ADraft_HasNoSubmittedOrFollowUpDate()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        await DashboardExportTestHost.Quotations(host)
            .RegisterAsync("quo-1", BusinessGovernanceFixtures.Quote(), BusinessGovernanceFixtures.Verified());

        var json = await ExportAsync(host);
        var entry = Assert.Single(json["quotes"]!.AsArray())!;

        Assert.Equal("draft", entry["status"]!.GetValue<string>());
        Assert.Null(entry["submittedDate"]);
        Assert.Null(entry["followUpDate"]);

        await manager.ShutdownAsync();
    }

    [Theory]
    [InlineData(QuotationStatus.Draft, "draft")]
    [InlineData(QuotationStatus.Submitted, "submitted")]
    [InlineData(QuotationStatus.Accepted, "accepted")]
    [InlineData(QuotationStatus.Declined, "declined")]
    public void MapStatus_IsTheDashboardsOwnFourWords(QuotationStatus status, string expected)
    {
        Assert.Equal(expected, QuotesExportAdapter.MapStatus(status));
    }

    [Fact]
    public async Task ExportAsync_OrdersByReference_AndLeavesOutSupersededRecords()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var quotations = (QuotationCatalog)DashboardExportTestHost.Quotations(host);

        await quotations.RegisterAsync("quo-b", BusinessGovernanceFixtures.Quote("QUO-B"), BusinessGovernanceFixtures.Verified());
        await quotations.RegisterAsync("quo-a", BusinessGovernanceFixtures.Quote("QUO-A"), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(quotations, "quo-a");
        await quotations.RegisterAsync("quo-c", BusinessGovernanceFixtures.Quote("QUO-C"), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(quotations, "quo-c");
        await quotations.SupersedeAsync("quo-a", "quo-c", "Re-quoted as QUO-C.");

        var json = await ExportAsync(host);

        Assert.Equal(["QUO-B", "QUO-C"], json["quotes"]!.AsArray().Select(e => e!["reference"]!.GetValue<string>()));

        await manager.ShutdownAsync();
    }
}
