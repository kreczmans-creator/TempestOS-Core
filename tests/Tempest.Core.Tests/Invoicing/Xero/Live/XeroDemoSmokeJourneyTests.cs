using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Templates;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// The live smoke journey (`v0.24.0` task X8) run against the in-process
/// Xero simulator on every CI run — no network, a hand-moved clock — so the
/// smoke test itself is proven before the Product Owner runs it on the Demo
/// Company, and the simulator's <c>Violations</c> log checks every request it
/// makes. Not in the <c>XeroLive</c> category.
/// </summary>
public sealed class XeroDemoSmokeJourneyTests
{
    private static readonly string[] JourneySteps =
        ["S01", "S02", "S03", "S04", "S05", "S06", "S07", "S07a", "S08", "S08a", "S09", "S09a", "S10", "S11", "S11a", "S12", "S12a", "S13", "S14", "S15", "S15a", "S16", "S17", "S17a", "S18", "S19", "S20"];

    [Fact]
    public async Task Journey_OnTheDemoCompany_PassesEveryStep_AndLeavesNoViolations()
    {
        using var kit = await SmokeKit.CreateAsync();

        var report = await kit.Journey().RunAsync();

        Assert.True(report.Passed, Failures(report));
        Assert.False(report.RefusedToWrite);
        Assert.Equal(JourneySteps, report.Steps.Select(s => s.Id));
        Assert.Empty(kit.Simulator.Violations);
        Assert.Equal("Demo Company (UK)", report.OrganisationName);
    }

    [Fact]
    public async Task Journey_WritesOnlyDraftsAndCopies_AndNeverEmails()
    {
        using var kit = await SmokeKit.CreateAsync();

        await kit.Journey(keep: true).RunAsync();

        var quote = Assert.Single(kit.Simulator.All("Quotes"));
        Assert.Equal("ACCEPTED", quote.Status);
        Assert.Single(quote.Attachments);

        var invoices = kit.Simulator.All("Invoices");
        var sales = Assert.Single(invoices, i => i.Body["Type"]?.GetValue<string>() == "ACCREC");
        var bill = Assert.Single(invoices, i => i.Body["Type"]?.GetValue<string>() == "ACCPAY");
        Assert.Equal("DRAFT", sales.Status);
        Assert.Equal("DRAFT", bill.Status);
        Assert.False(Assert.Single(sales.Attachments).IncludeOnline);
        Assert.Single(bill.Attachments);
        Assert.Equal("DRAFT", Assert.Single(kit.Simulator.All("PurchaseOrders")).Status);

        Assert.DoesNotContain(kit.Simulator.Requests, r => r.Path.Contains("/Email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(kit.Simulator.Requests, r => r.Method != HttpMethod.Get && r.JsonBody?.ToJsonString().Contains("AUTHORISED", StringComparison.Ordinal) == true);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task Journey_RepeatsEveryCreateUnderItsKey_AndXeroHoldsOneOfEach()
    {
        using var kit = await SmokeKit.CreateAsync();

        var report = await kit.Journey(keep: true).RunAsync();

        foreach (var id in new[] { "S07a", "S08a", "S12a", "S15a", "S17a" })
            Assert.True(report.Steps.Single(s => s.Id == id).Passed, id);

        Assert.Single(kit.Simulator.All("Contacts"), c => c.Number == XeroDemoSmokeJourney.ContactNumber);
        Assert.Single(kit.Simulator.All("Quotes"));
        Assert.Single(kit.Simulator.All("PurchaseOrders"));
        Assert.Equal(2, kit.Simulator.All("Invoices").Count);
    }

    [Fact]
    public async Task Journey_DeletesItsDrafts_UnlessKept_AndLeavesTheQuote()
    {
        using var kit = await SmokeKit.CreateAsync();

        var report = await kit.Journey().RunAsync();

        Assert.True(report.Steps.Single(s => s.Id == "S19").Passed);
        Assert.All(kit.Simulator.All("Invoices"), i => Assert.Equal("DELETED", i.Status));
        Assert.Equal("DELETED", Assert.Single(kit.Simulator.All("PurchaseOrders")).Status);
        Assert.Equal("ACCEPTED", Assert.Single(kit.Simulator.All("Quotes")).Status);
        Assert.Contains(report.Records, r => r.Kind == "Invoice" && r.Status == "DELETED");
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task Journey_OnALiveOrganisation_RefusesBeforeAnyWrite()
    {
        using var kit = await SmokeKit.CreateAsync(isDemoCompany: false);

        var report = await kit.Journey().RunAsync();

        Assert.True(report.RefusedToWrite);
        Assert.False(report.Passed);
        Assert.False(report.Steps.Single(s => s.Id == "S04").Passed);
        Assert.DoesNotContain(kit.Simulator.Requests, r => r.Method != HttpMethod.Get);
        Assert.Empty(kit.Simulator.Violations);
        Assert.Contains("REFUSED", report.ToMarkdown(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Journey_RecordsTheOpenItems()
    {
        using var kit = await SmokeKit.CreateAsync();

        var report = await kit.Journey().RunAsync();

        // F2: the simulator replaces an attachment posted under the same name.
        Assert.Equal(XeroDemoSmokeReport.Confirmed, report.Findings.Single(f => f.Key == "F2").Verdict);
        // F3: the grant is exactly XeroScopes.Required — no openid/profile/email.
        Assert.Equal(XeroDemoSmokeReport.Confirmed, report.Findings.Single(f => f.Key == "F3").Verdict);
        // F4: the contact was created under accounting.contacts.
        Assert.Equal(XeroDemoSmokeReport.Confirmed, report.Findings.Single(f => f.Key == "F4").Verdict);
        Assert.DoesNotContain(report.Findings, f => f.Key == "F1");
    }

    [Fact]
    public async Task Journey_RunTwice_LinksTheExistingContact()
    {
        using var kit = await SmokeKit.CreateAsync();
        await kit.Journey().RunAsync();
        kit.Clock.Advance(TimeSpan.FromMinutes(2));

        var second = await kit.Journey().RunAsync();

        Assert.True(second.Passed, Failures(second));
        Assert.Contains("linked existing contact", second.Steps.Single(s => s.Id == "S07").Detail, StringComparison.Ordinal);
        Assert.Equal(XeroDemoSmokeReport.NotRun, second.Findings.Single(f => f.Key == "F4").Verdict);
        Assert.Single(kit.Simulator.All("Contacts"), c => c.Number == XeroDemoSmokeJourney.ContactNumber);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task Journey_WithAGrantFromBeforeV024_FailsTheScopeStep()
    {
        using var kit = await SmokeKit.CreateAsync(recordGrant: false);

        var report = await kit.Journey().RunAsync();

        var scopes = report.Steps.Single(s => s.Id == "S02");
        Assert.False(scopes.Passed);
        Assert.Contains("re-authorise", scopes.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(report.Findings, f => f.Key == "F3");
    }

    [Fact]
    public async Task Journey_WithASuppliedTokenThatSaysNoScopes_ReportsTheScopeStepNotChecked_AndStillPasses()
    {
        using var kit = await SmokeKit.CreateAsync(recordGrant: false);

        var report = await kit.Journey(suppliedToken: true).RunAsync();

        var scopes = report.Steps.Single(s => s.Id == "S02");
        Assert.True(scopes.Passed);
        Assert.StartsWith("not checked (supplied token)", scopes.Detail, StringComparison.Ordinal);
        Assert.True(report.Passed, Failures(report));
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task Journey_WhenNotConnected_StopsAtTheFirstStep_WithNoRequest()
    {
        using var kit = await SmokeKit.CreateAsync(authorised: false);

        var report = await kit.Journey().RunAsync();

        Assert.False(report.Passed);
        Assert.Equal("S01", Assert.Single(report.Steps).Id);
        Assert.Empty(kit.Simulator.Requests);
    }

    [Fact]
    public async Task Journey_UnderATightRateLimit_WaitsOutRetryAfter_AndStillPasses()
    {
        using var kit = await SmokeKit.CreateAsync(minuteLimit: 15);

        var started = kit.Clock.GetUtcNow();

        var report = await kit.Journey().RunAsync();

        // The rate limiter held calls back (Xero's X-MinLimit-Remaining fell to its floor) and the journey waited, on the clock, rather than failing.
        Assert.True(report.Passed, Failures(report));
        Assert.True(kit.Clock.GetUtcNow() - started >= TimeSpan.FromSeconds(30));
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task KeyProbe_ReplaysInsideTheWindow_AndObservesXeroForgettingTheKeyLater()
    {
        // Emulate Xero's documented retention: the simulator forgets every key 6 minutes after the probe starts.
        using var kit = await SmokeKit.CreateAsync();
        var elapsed = TimeSpan.Zero;
        var forgotten = false;

        var report = await kit.Journey(delay: by =>
        {
            elapsed += by;
            if (!forgotten && elapsed > TimeSpan.FromMinutes(6))
            {
                kit.Simulator.ForgetIdempotencyKeys();
                forgotten = true;
            }
        }).RunKeyRetentionProbeAsync();

        Assert.True(report.Passed, Failures(report));
        var finding = report.Findings.Single(f => f.Key == "F1");
        Assert.Equal(XeroDemoSmokeReport.Confirmed, finding.Verdict);
        Assert.Contains("processed as a new request", finding.Observed, StringComparison.Ordinal);

        // Both bills the probe made are deleted at the end.
        var bills = kit.Simulator.All("Invoices").Where(i => i.Body["Type"]?.GetValue<string>() == "ACCPAY").ToList();
        Assert.Equal(2, bills.Count);
        Assert.All(bills, b => Assert.Equal("DELETED", b.Status));
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task KeyProbe_WhenXeroForgetsTheKeyTooSoon_SaysTheAssumptionDiffers()
    {
        using var kit = await SmokeKit.CreateAsync();

        var report = await kit.Journey(delay: _ => kit.Simulator.ForgetIdempotencyKeys()).RunKeyRetentionProbeAsync();

        Assert.False(report.Passed);
        Assert.Equal(XeroDemoSmokeReport.Differs, report.Findings.Single(f => f.Key == "F1").Verdict);
        Assert.False(report.Steps.Single(s => s.Id == "K02").Passed);
    }

    [Fact]
    public async Task Report_NamesEveryStepFindingAndRecord_WithXeroLinks()
    {
        using var kit = await SmokeKit.CreateAsync();

        var report = await kit.Journey(keep: true).RunAsync();
        var markdown = report.ToMarkdown();
        var console = report.ToConsoleText();

        Assert.Contains("Result: PASSED", markdown, StringComparison.Ordinal);
        foreach (var step in report.Steps)
            Assert.Contains($"| {step.Id} |", markdown, StringComparison.Ordinal);
        Assert.Contains("https://go.xero.com/AccountsReceivable/View.aspx?InvoiceID=", markdown, StringComparison.Ordinal);
        Assert.Contains("https://go.xero.com/AccountsPayable/View.aspx?InvoiceID=", markdown, StringComparison.Ordinal);
        Assert.Contains("[PASS] S12", console, StringComparison.Ordinal);
        Assert.DoesNotContain(XeroTestAuthoriser.AccessToken, markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(XeroTestAuthoriser.AccessToken, console, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Journey_KeepsTheRecordedBillVat_WhichIsNotWhatXeroWouldCompute()
    {
        using var kit = await SmokeKit.CreateAsync();

        var report = await kit.Journey(keep: true).RunAsync();

        // 20% of the net 50 is 10.00; the bill records 9.99 so S18 can tell kept from recomputed.
        Assert.NotEqual(10m, XeroDemoSmokeJourney.RecordedBillVat);
        var bill = Assert.Single(kit.Simulator.All("Invoices"), i => i.Body["Type"]?.GetValue<string>() == "ACCPAY");
        Assert.Equal(XeroDemoSmokeJourney.RecordedBillVat, bill.Body["TotalTax"]!.GetValue<decimal>());
        var step = report.Steps.Single(s => s.Id == "S18");
        Assert.True(step.Passed, step.Detail);
        Assert.Contains("kept", step.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Journey_WhenXeroRecomputesTheBillVat_FailsTheReadBackStep()
    {
        using var kit = await SmokeKit.CreateAsync(wrapNetwork: network => new RecomputeBillVat { InnerHandler = network });

        var report = await kit.Journey().RunAsync();

        var step = report.Steps.Single(s => s.Id == "S18");
        Assert.False(step.Passed);
        Assert.Contains("NOT kept", step.Detail, StringComparison.Ordinal);
        Assert.False(report.Passed);
    }

    [Fact]
    public async Task Journey_WithAnExpiredSuppliedToken_SaysSupplyAFreshOne_NotConnect()
    {
        using var kit = await SmokeKit.CreateAsync(authorised: false);

        var report = await kit.Journey(suppliedToken: true).RunAsync();

        var step = Assert.Single(report.Steps);
        Assert.Equal("S01", step.Id);
        Assert.False(step.Passed);
        Assert.Contains("supplied token expired: supply a fresh one", step.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("-Connect", step.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("-ClientId", step.Detail, StringComparison.Ordinal);
        Assert.Empty(kit.Simulator.Requests);
    }

    [Fact]
    public async Task Journey_CallCount_IsTheOneTheScriptStates()
    {
        using var kit = await SmokeKit.CreateAsync();

        var report = await kit.Journey().RunAsync();
        Assert.True(report.Passed, Failures(report));

        var script = File.ReadAllText(Path.Combine(RepositoryPaths.RepositoryRoot, "scripts", "xero-demo-smoke.ps1"));
        var stated = Regex.Match(script, @"about (\d+) calls");
        Assert.True(stated.Success, "The script's help should say about how many calls the journey makes.");
        var calls = kit.Simulator.Requests.Count;
        Assert.InRange(int.Parse(stated.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), calls - 3, calls + 3);
        Assert.True(calls < 60, $"The journey ({calls} calls) should fit inside Xero's 60 a minute.");
    }

    [Fact]
    public void SmokePdf_IsAWellFormedPdf_NamedAfterTheNumber()
    {
        var pdf = XeroDemoSmokeJourney.Pdf("SMOKE-Q-1 R2");

        Assert.Equal("SMOKE-Q-1.pdf", pdf.FileName);
        Assert.Equal("application/pdf", pdf.ContentType);
        var text = System.Text.Encoding.ASCII.GetString(pdf.Content.Span);
        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
        Assert.NotEqual(XeroDemoSmokeJourney.Pdf("SMOKE-Q-1 R1").Sha256, pdf.Sha256);
    }

    private static string Failures(XeroDemoSmokeReport report) =>
        string.Join(Environment.NewLine, report.Failures.Select(f => $"{f.Id} {f.Title}: {f.Detail}"));

    /// <summary>The smoke journey's pipeline over the simulator, built exactly as the live one (<see cref="XeroLiveConnection"/>).</summary>
    private sealed class SmokeKit : IDisposable
    {
        private SmokeKit(XeroApiSimulator simulator, XeroSimulatorClock clock, XeroLiveConnection connection)
        {
            Simulator = simulator;
            Clock = clock;
            Connection = connection;
        }

        public XeroApiSimulator Simulator { get; }

        public XeroSimulatorClock Clock { get; }

        public XeroLiveConnection Connection { get; }

        public static async Task<SmokeKit> CreateAsync(
            bool isDemoCompany = true, bool recordGrant = true, bool authorised = true, int minuteLimit = 60, Func<HttpMessageHandler, HttpMessageHandler>? wrapNetwork = null)
        {
            var clock = new XeroSimulatorClock();
            var simulator = new XeroApiSimulator(
                new XeroSimulatorOptions(TenantId: XeroTestAuthoriser.TenantId, AccessToken: XeroTestAuthoriser.AccessToken, IsDemoCompany: isDemoCompany, MinuteLimit: minuteLimit),
                clock);

            var (authoriser, secretStore) = await XeroTestAuthoriser.CreateAsync(
                authorised: authorised, grantedScopes: recordGrant ? XeroScopes.Required : null);

            return new SmokeKit(simulator, clock, XeroLiveConnection.CreateOver(authoriser, secretStore, wrapNetwork?.Invoke(simulator) ?? simulator, XeroApiSimulator.BaseAddress, clock));
        }

        public XeroDemoSmokeJourney Journey(bool keep = false, Action<TimeSpan>? delay = null, bool suppliedToken = false) => new(
            Connection.Api,
            Connection.Authoriser,
            Connection.SettingsReader,
            () => Connection.Journal.Requests,
            new XeroDemoSmokeOptions(keep, (by, _) =>
            {
                Clock.Advance(by);
                delay?.Invoke(by);
                return Task.CompletedTask;
            }, Clock, suppliedToken));

        public void Dispose()
        {
            Connection.Dispose();
            Simulator.Dispose();
        }
    }

    /// <summary>Stands in for a Xero that ignored the sent <c>TaxAmount</c>: every bill read back carries the 20% VAT Xero would compute.</summary>
    private sealed class RecomputeBillVat : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method != HttpMethod.Get || response.Content is null
                || response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.Ordinal) != true)
                return response;

            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json?["Invoices"] is not JsonArray invoices)
                return response;

            foreach (var invoice in invoices.OfType<JsonObject>().Where(i => i["Type"]?.GetValue<string>() == "ACCPAY"))
                invoice["TotalTax"] = 10m;

            var rewritten = new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(json.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
            foreach (var header in response.Headers)
                rewritten.Headers.TryAddWithoutValidation(header.Key, header.Value);
            response.Dispose();
            return rewritten;
        }
    }
}
