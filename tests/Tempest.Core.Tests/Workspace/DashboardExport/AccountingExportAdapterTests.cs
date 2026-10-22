using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Web;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
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

        Assert.Equal(2, (int)json["schemaVersion"]!);
        Assert.Equal("2026-10-22T09:00:00Z", (string)json["generatedAt"]!);

        // No accounts read model: the money pictures are honestly absent, never zero.
        foreach (var key in new[] { "cash", "cashFlow", "forwardCash" })
        {
            Assert.True(json.AsObject().ContainsKey(key), key);
            Assert.Null(json[key]);
        }

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

    [Fact]
    public async Task CashCashFlowAndForwardCash_AreCopiedFromCoresAccountsReadModel()
    {
        var (api, network) = await XeroWithAsync([], [], []);
        var asOf = new DateOnly(2026, 10, 22);
        static Money Gbp(decimal amount) => new(amount, CurrencyCode.Gbp);
        var reading = new AccountsReading(
            Bills: [new BillDue("Steel Supplies", "B1", asOf, asOf.AddDays(10), Gbp(400m), "AUTHORISED")],
            RepeatingBills: [],
            Cash: [new CashAccountBalance("Business Current", Gbp(9000m), asOf)],
            ReadAt: Now,
            Connector: "Xero");
        var snapshot = AccountsSnapshot.From(reading, [], asOf);
        ForwardCashFigure Figure(decimal? amount) => amount is { } a ? new(Gbp(a), ForwardCashSource.Derived) : new(null, ForwardCashSource.Derived, "No reading.");
        var month = new ForwardCashMonth(
            new DateOnly(2026, 10, 1), asOf, new DateOnly(2026, 10, 31),
            Figure(9000m), Figure(0m), Figure(0m), Figure(1500m), Figure(400m), Figure(0m), Figure(400m), Figure(null), []);
        var forward = new ForwardCashPicture(asOf, CurrencyCode.Gbp, [month], [], [], [], Now, "Xero", null);
        var accounts = new FakeAccounts(snapshot, forward);
        using var stream = new MemoryStream();

        await new AccountingExportAdapter(api, new ManualClock(Now), accounts).ExportAsync(stream);
        var json = JsonNode.Parse(stream.ToArray())!;

        Assert.Equal(9000m, (decimal)json["cash"]!["balance"]!);
        Assert.Equal("GBP", (string)json["cash"]!["currency"]!);
        Assert.Equal("Business Current", (string)json["cash"]!["accounts"]![0]!["name"]!);

        var weeks = json["cashFlow"]!["weeks"]!.AsArray();
        Assert.Equal(snapshot.CashFlowSeries.Count, weeks.Count);
        Assert.Equal(snapshot.CashFlowSeries[^1].ClosingCash.Amount, (decimal)weeks[^1]!["closingCash"]!);
        Assert.Equal(400m, weeks.Sum(w => (decimal)w!["payableOut"]!));

        var months = json["forwardCash"]!["months"]!.AsArray();
        Assert.Equal("2026-10", (string)months[0]!["month"]!);
        Assert.Equal(1500m, (decimal)months[0]!["moneyIn"]!);
        Assert.Null(months[0]!["closingCash"]);

        // Still read only: the money pictures add no Xero call.
        Assert.All(network.Received, r => Assert.Equal(HttpMethod.Get, r.Request.Method));
        Assert.Equal(3, network.Received.Count);
    }

    private sealed class FakeAccounts(AccountsSnapshot snapshot, ForwardCashPicture forward) : IAccountsReadModel, IForwardCashReadModel
    {
        public Task<AccountsSnapshot> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);

        public Task<ForwardCashPicture> ReadForwardCashAsync(CancellationToken cancellationToken = default) => Task.FromResult(forward);
    }
}
