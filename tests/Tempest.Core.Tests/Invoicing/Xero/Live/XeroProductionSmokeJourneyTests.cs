using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// The production-path smoke journey (<see cref="XeroProductionSmokeJourney"/>,
/// review board m20) run against the in-process Xero simulator on every CI
/// run — no network, a hand-moved clock — over the same pipeline the live run
/// uses (<see cref="XeroLiveConnection.CreateOver"/>). Proves the journey the
/// Product Owner runs on the Demo Company passes, and the simulator's
/// <c>Violations</c> log checks every request the production code makes. Not
/// in the <c>XeroLive</c> category.
/// </summary>
public sealed class XeroProductionSmokeJourneyTests
{
    private static readonly string[] Steps = ["P01", "P02", "P03", "P04", "P05", "P06", "P07", "P08", "P09", "P10"];

    [Fact]
    public async Task ProductionJourney_OnTheDemoCompany_PassesEveryStep_AndLeavesNoViolations()
    {
        using var kit = await Kit.CreateAsync();
        await using var journey = kit.Journey();

        var report = await journey.RunAsync();

        Assert.True(report.Passed, Failures(report));
        Assert.Equal(Steps, report.Steps.Select(s => s.Id));
        Assert.Empty(kit.Simulator.Violations);
        Assert.DoesNotContain(kit.Connection.Audit.Rows, r => r.Action.Contains("refused", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProductionJourney_MakesOneDraftOfEach_ThroughTheProductionMappers_AndCleansUp()
    {
        using var kit = await Kit.CreateAsync();
        await using var journey = kit.Journey();

        var report = await journey.RunAsync();
        Assert.True(report.Passed, Failures(report));

        // The quote went DRAFT -> SENT -> ACCEPTED and keeps its PDF; nothing else is left live.
        var quote = Assert.Single(kit.Simulator.All("Quotes"));
        Assert.Equal(journey.QuoteNumber, quote.Number);
        Assert.Equal("ACCEPTED", quote.Status);
        Assert.Single(quote.Attachments);

        var invoices = kit.Simulator.All("Invoices");
        var sales = Assert.Single(invoices, i => i.Body["Type"]?.GetValue<string>() == "ACCREC");
        var bill = Assert.Single(invoices, i => i.Body["Type"]?.GetValue<string>() == "ACCPAY");
        Assert.Equal($"{journey.ProjectCode}-INV-001", sales.Number);
        Assert.Equal(journey.BillNumber, bill.Number);
        Assert.False(Assert.Single(sales.Attachments).IncludeOnline);
        Assert.Single(bill.Attachments);
        Assert.Equal("DELETED", sales.Status);
        Assert.Equal("DELETED", bill.Status);
        var order = Assert.Single(kit.Simulator.All("PurchaseOrders"));
        Assert.Equal(journey.OrderNumber, order.Number);
        Assert.Equal("DELETED", order.Status);

        // The contact was created by the production linker, with the customer code as its number (Q7).
        Assert.Single(kit.Simulator.All("Contacts"), c => c.Number == XeroDemoSmokeJourney.ContactNumber);

        // Nothing the production code wrote approved, emailed or carried a forbidden status.
        Assert.Empty(journey.JournalProblems());
        Assert.DoesNotContain(kit.Simulator.Requests, r => r.Path.Contains("/Email", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task ProductionJourney_Kept_LeavesEachDraftInXero()
    {
        using var kit = await Kit.CreateAsync();
        await using var journey = kit.Journey(keep: true);

        var report = await journey.RunAsync();

        Assert.True(report.Passed, Failures(report));
        Assert.All(kit.Simulator.All("Invoices"), i => Assert.Equal("DRAFT", i.Status));
        Assert.Equal("DRAFT", Assert.Single(kit.Simulator.All("PurchaseOrders")).Status);
        Assert.Contains(report.Records, r => r.Kind == "Bill" && r.Status == "DRAFT");
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task ProductionJourney_OnALiveOrganisation_RefusesBeforeAnyWrite()
    {
        using var kit = await Kit.CreateAsync(isDemoCompany: false);
        await using var journey = kit.Journey();

        var report = await journey.RunAsync();

        Assert.True(report.RefusedToWrite);
        Assert.False(report.Passed);
        Assert.Equal("P01", Assert.Single(report.Steps).Id);
        Assert.DoesNotContain(kit.Simulator.Requests, r => r.Method != HttpMethod.Get);
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task ProductionJourney_UnderATightRateLimit_WaitsOnTheClock_AndStillPasses()
    {
        using var kit = await Kit.CreateAsync(minuteLimit: 20);
        await using var journey = kit.Journey();
        var started = kit.Clock.GetUtcNow();

        var report = await journey.RunAsync();

        Assert.True(report.Passed, Failures(report));
        Assert.True(kit.Clock.GetUtcNow() - started >= TimeSpan.FromMinutes(1));
        Assert.Empty(kit.Simulator.Violations);
    }

    [Fact]
    public async Task ProductionJourney_CallCount_IsTheOneTheScriptStates()
    {
        using var kit = await Kit.CreateAsync();
        await using var journey = kit.Journey();

        var report = await journey.RunAsync();
        Assert.True(report.Passed, Failures(report));

        var script = File.ReadAllText(Path.Combine(Tempest.Core.Tests.Templates.RepositoryPaths.RepositoryRoot, "scripts", "xero-demo-smoke.ps1"));
        var stated = System.Text.RegularExpressions.Regex.Match(script, @"production path\s+\(about (\d+) more calls");
        Assert.True(stated.Success, "The script's help should say about how many calls the production-path journey makes.");
        var calls = kit.Simulator.Requests.Count;
        Assert.InRange(int.Parse(stated.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), calls - 5, calls + 5);
    }

    private static string Failures(XeroDemoSmokeReport report) =>
        string.Join(Environment.NewLine, report.Failures.Select(f => $"{f.Id} {f.Title}: {f.Detail}"));

    /// <summary>The production journey's pipeline over the simulator, built exactly as the live one.</summary>
    private sealed class Kit : IDisposable
    {
        private Kit(XeroApiSimulator simulator, XeroSimulatorClock clock, XeroLiveConnection connection)
        {
            Simulator = simulator;
            Clock = clock;
            Connection = connection;
        }

        public XeroApiSimulator Simulator { get; }

        public XeroSimulatorClock Clock { get; }

        public XeroLiveConnection Connection { get; }

        public static async Task<Kit> CreateAsync(bool isDemoCompany = true, int minuteLimit = 60)
        {
            var clock = new XeroSimulatorClock();
            var simulator = new XeroApiSimulator(
                new XeroSimulatorOptions(TenantId: XeroTestAuthoriser.TenantId, AccessToken: XeroTestAuthoriser.AccessToken, IsDemoCompany: isDemoCompany, MinuteLimit: minuteLimit),
                clock);
            var (authoriser, secretStore) = await XeroTestAuthoriser.CreateAsync(grantedScopes: XeroScopes.Required);

            return new Kit(simulator, clock, XeroLiveConnection.CreateOver(authoriser, secretStore, simulator, XeroApiSimulator.BaseAddress, clock));
        }

        public XeroProductionSmokeJourney Journey(bool keep = false) => new(
            Connection,
            new XeroDemoSmokeOptions(keep, (by, _) =>
            {
                Clock.Advance(by);
                return Task.CompletedTask;
            }, Clock),
            new XeroSyncOptions { Jitter = () => 0.5 });

        public void Dispose()
        {
            Connection.Dispose();
            Simulator.Dispose();
        }
    }
}
