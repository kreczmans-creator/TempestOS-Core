using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Web;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Workspace.Integration.DashboardExport;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// <c>accounting.json</c> (Tempest-Dashboard <c>docs/DATA-CONTRACTS.md</c>
/// §Accounting): rows not totals, never capped, every Xero page walked, and
/// all or nothing when a read fails.
/// </summary>
public sealed class AccountingExportAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static string XeroDate(int year, int month, int day) =>
        $"/Date({new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()}+0000)/";

    private static JsonObject Invoice(string id, string number, string client, decimal due, decimal paid = 0, string? fullyPaid = null) => new()
    {
        ["InvoiceID"] = id,
        ["InvoiceNumber"] = number,
        ["Contact"] = new JsonObject { ["ContactID"] = "c-" + id, ["Name"] = client },
        ["AmountDue"] = due,
        ["AmountPaid"] = paid,
        ["Total"] = due + paid,
        ["CurrencyCode"] = "GBP",
        ["Date"] = XeroDate(2026, 9, 20),
        ["DueDate"] = XeroDate(2026, 10, 20),
        ["FullyPaidOnDate"] = fullyPaid,
    };

    /// <summary>A Xero that answers each `where` from its own list, 100 a page, as Xero does.</summary>
    private static async Task<(XeroAccountingApi Api, TerminalHandler Network)> XeroWithAsync(
        IReadOnlyList<JsonObject> receivable, IReadOnlyList<JsonObject> payable, IReadOnlyList<JsonObject> paid, HttpStatusCode status = HttpStatusCode.OK)
    {
        var network = new TerminalHandler
        {
            Respond = request =>
            {
                if (status != HttpStatusCode.OK)
                    return TerminalHandler.Json(status, """{"Message":"unavailable"}""");

                var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
                var where = query["where"]!;
                var page = int.Parse(query["page"]!, CultureInfo.InvariantCulture);
                var source = where.Contains("ACCPAY", StringComparison.Ordinal) ? payable
                    : where.Contains("PAID", StringComparison.Ordinal) ? paid
                    : receivable;
                var batch = new JsonArray([.. source.Skip((page - 1) * 100).Take(100).Select(i => (JsonNode)i.DeepClone())]);
                return TerminalHandler.Json(HttpStatusCode.OK, new JsonObject { ["Invoices"] = batch }.ToJsonString());
            },
        };
        var (authoriser, _) = await XeroTestAuthoriser.CreateAsync();
        var client = new HttpClient(network) { BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/") };
        return (new XeroAccountingApi(client, authoriser), network);
    }

    private static async Task<JsonNode> ExportAsync(XeroAccountingApi api)
    {
        using var stream = new MemoryStream();
        await new AccountingExportAdapter(api, new ManualClock(Now)).ExportAsync(stream);
        return JsonNode.Parse(stream.ToArray())!;
    }

    [Fact]
    public async Task EveryRow_InTheDashboardsShape_WithCashNull()
    {
        var (api, _) = await XeroWithAsync(
            [Invoice("i1", "INV-0042", "Harbour Trust", 4820.00m)],
            [Invoice("b1", "BILL-7", "Steel Supplies", 310.50m)],
            [Invoice("p1", "INV-0039", "Acme", 0m, paid: 1200m, fullyPaid: XeroDate(2026, 10, 4))]);

        var json = await ExportAsync(api);

        Assert.Equal(1, (int)json["schemaVersion"]!);
        Assert.Equal("2026-10-22T09:00:00Z", (string)json["generatedAt"]!);
        Assert.Null(json["cash"]);
        Assert.True(json.AsObject().ContainsKey("cash"));

        var invoice = json["invoices"]![0]!;
        Assert.Equal("i1", (string)invoice["id"]!);
        Assert.Equal("INV-0042", (string)invoice["reference"]!);
        Assert.Equal("Harbour Trust", (string)invoice["client"]!);
        Assert.Equal(4820.00m, (decimal)invoice["amountDue"]!);
        Assert.Equal("GBP", (string)invoice["currency"]!);
        Assert.Equal("2026-09-20", (string)invoice["issueDate"]!);
        Assert.Equal("2026-10-20", (string)invoice["dueDate"]!);
        Assert.Equal("xero", (string)invoice["source"]!);
        Assert.Equal("https://go.xero.com/app/invoicing/edit/i1", (string)invoice["url"]!);

        Assert.Equal("BILL-7", (string)json["bills"]![0]!["reference"]!);

        var paid = json["paid"]![0]!;
        Assert.Equal(1200m, (decimal)paid["amountPaid"]!);
        Assert.Equal("2026-10-04", (string)paid["paidDate"]!);
    }

    [Fact]
    public async Task EveryPageIsWalked_AndNothingIsCapped()
    {
        var many = Enumerable.Range(1, 250).Select(n => Invoice($"i{n}", $"INV-{n:0000}", "Client", 10m)).ToList();
        var (api, network) = await XeroWithAsync(many, [], []);

        var json = await ExportAsync(api);

        Assert.Equal(250, json["invoices"]!.AsArray().Count);
        var receivablePages = network.Received
            .Select(r => HttpUtility.ParseQueryString(r.Request.RequestUri!.Query))
            .Where(q => q["where"] == """Type=="ACCREC" AND Status=="AUTHORISED" """.Trim())
            .Select(q => q["page"])
            .ToList();
        Assert.Equal(["1", "2", "3"], receivablePages);
    }

    [Fact]
    public async Task TheQueriesMatchTheDashboards_AndPaidReachesBack30Days()
    {
        var (api, network) = await XeroWithAsync([], [], []);

        await ExportAsync(api);

        var queries = network.Received.Select(r => HttpUtility.ParseQueryString(r.Request.RequestUri!.Query)).ToList();
        Assert.Contains(queries, q => q["where"] == """Type=="ACCREC" AND Status=="AUTHORISED" """.Trim() && q["order"] == "DueDate");
        Assert.Contains(queries, q => q["where"] == """Type=="ACCPAY" AND Status=="AUTHORISED" """.Trim() && q["order"] == "DueDate");
        Assert.Contains(queries, q => q["where"] == """Type=="ACCREC" AND Status=="PAID" AND FullyPaidOnDate>=DateTime(2026,9,22)""" && q["order"] == "UpdatedDateUTC DESC");
        Assert.All(network.Received, r => Assert.Equal(HttpMethod.Get, r.Request.Method));
    }

    [Fact]
    public async Task AFailedRead_WritesNothing_SoTheLastGoodFileStands()
    {
        var (api, _) = await XeroWithAsync([], [], [], HttpStatusCode.ServiceUnavailable);
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<AccountingExportUnavailableException>(
            () => new AccountingExportAdapter(api, new ManualClock(Now)).ExportAsync(stream));

        Assert.Equal(0, stream.Length);
    }
}
