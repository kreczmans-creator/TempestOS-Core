using System.Text.Json.Nodes;
using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Runtime;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// Proves <see cref="DashboardExportHostedService"/> end-to-end through a
/// real, running <see cref="ITempestHost"/> — discovery finds it with no
/// manual registration, it writes both files on <see cref="DashboardExportHostedService.ExportOnceAsync"/>,
/// atomically (no <c>.tmp</c> left behind), and an export failure is
/// isolated rather than Host-fatal. Mirrors <c>TempestHostHostedServiceTests</c>'s
/// own real-host convention.
/// </summary>
public class DashboardExportHostedServiceTests
{
    /// <summary>
    /// Mirrors <c>TempestHostHostedServiceTests.BuilderWithHostedServices</c>'s
    /// own established convention exactly: an explicit hosted-service
    /// candidate list, evaluated through the same, real
    /// <c>HostedServiceDiscoveryService.DiscoverHostedServiceTypes(candidateTypes)</c>
    /// filtering-and-registration path a full <c>AppDomain</c> scan also
    /// uses — deterministic and fast, and not a weaker proof: the general
    /// "every loaded assembly is scanned" mechanism is
    /// <c>HostedServiceDiscoveryServiceTests</c>'s own concern, not this
    /// type's.
    /// </summary>
    private static ITempestHostBuilder BuilderFor(string persistenceRoot, string exportDirectory, int? intervalSeconds = null)
    {
        var entries = new List<KeyValuePair<string, string>>
        {
            new(SqlitePersistenceStore.RootPathConfigurationKey, persistenceRoot),
            new(DashboardExportOptions.ExportDirectoryConfigurationKey, exportDirectory),
        };

        if (intervalSeconds is { } seconds)
            entries.Add(new(DashboardExportOptions.IntervalSecondsConfigurationKey, seconds.ToString()));

        return new TempestHostBuilder(
                discoveryCandidateTypesOverride: Type.EmptyTypes,
                pluginsRootPathOverride: null,
                hostedServiceCandidateTypesOverride: [typeof(DashboardExportHostedService)])
            .AddConfigurationSource(new MemoryConfigurationSource(entries));
    }

    private static DashboardExportHostedService GetService(ITempestHost host) =>
        (DashboardExportHostedService)host.Services!.GetService(typeof(DashboardExportHostedService));

    [Fact]
    public async Task Discovery_RegistersDashboardExportHostedService_WithNoExplicitDiRegistration()
    {
        using var temp = new TempDirectory();
        using var exportDir = new TempDirectory();
        var host = BuilderFor(temp.Path, exportDir.Path).Build();

        var runTask = host.RunAsync();
        await RunningHostFixture.WaitUntilRunningAsync(host);

        Assert.NotNull(GetService(host));

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task ExportOnceAsync_WritesEveryFile_ValidJson_NoTempFilesLeftBehind()
    {
        using var temp = new TempDirectory();
        using var exportDir = new TempDirectory();
        var host = BuilderFor(temp.Path, exportDir.Path).Build();

        var runTask = host.RunAsync();
        await RunningHostFixture.WaitUntilRunningAsync(host);

        var service = GetService(host);
        await service.ExportOnceAsync();

        Assert.True(service.LastExportSucceeded, service.LastExportException?.ToString() ?? "Export did not report success, and left no exception to explain why.");

        var statusPath = Path.Combine(exportDir.Path, "engineering-status.json");
        var programmePath = Path.Combine(exportDir.Path, "programme.json");
        var contractsPath = Path.Combine(exportDir.Path, "contracts.json");
        var quotesPath = Path.Combine(exportDir.Path, "quotes.json");
        var reviewsPath = Path.Combine(exportDir.Path, "reviews.json");

        foreach (var path in new[] { statusPath, programmePath, contractsPath, quotesPath, reviewsPath })
        {
            Assert.True(File.Exists(path), path);
            Assert.False(File.Exists(path + ".tmp"), path);
            Assert.NotNull(JsonNode.Parse(await File.ReadAllTextAsync(path)));
        }

        Assert.Empty(JsonNode.Parse(await File.ReadAllTextAsync(contractsPath))!["contracts"]!.AsArray());
        Assert.Empty(JsonNode.Parse(await File.ReadAllTextAsync(quotesPath))!["quotes"]!.AsArray());
        Assert.Empty(JsonNode.Parse(await File.ReadAllTextAsync(reviewsPath))!["items"]!.AsArray());

        // No Xero connector here: no ledger at all, never an empty one the dashboard would read as live.
        Assert.False(File.Exists(Path.Combine(exportDir.Path, "accounting.json")));

        Assert.NotNull(service.LastExportAttemptedAt);

        await host.StopAsync();
        await runTask;
    }

    /// <summary>A Xero read that fails keeps the last good <c>accounting.json</c>, and never stops the other five files.</summary>
    [Fact]
    public async Task AFailedLedgerRead_KeepsTheLastAccountingFile_AndWritesTheRest()
    {
        using var temp = new TempDirectory();
        using var exportDir = new TempDirectory();
        var host = BuilderFor(temp.Path, exportDir.Path).Build();
        var runTask = host.RunAsync();
        await RunningHostFixture.WaitUntilRunningAsync(host);

        var network = new Tempest.Core.Tests.Invoicing.Xero.Api.TerminalHandler
        {
            Respond = _ => Tempest.Core.Tests.Invoicing.Xero.Api.TerminalHandler.Json(System.Net.HttpStatusCode.ServiceUnavailable, "{}"),
        };
        var (authoriser, _) = await Tempest.Core.Tests.Invoicing.Xero.Api.XeroTestAuthoriser.CreateAsync();
        var xero = new Tempest.Core.Invoicing.Xero.Api.XeroAccountingApi(new HttpClient(network) { BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/") }, authoriser);

        T Get<T>() => (T)host.Services!.GetService(typeof(T));
        var service = new DashboardExportHostedService(
            Get<Tempest.Core.EngineeringDomain.EngineeringDomainContext>(), Get<Tempest.Core.Requirements.IRequirementsService>(),
            Get<Tempest.Core.Requirements.IRequirementValidationService>(), Get<Tempest.Core.Navigation.INavigationProvider>(),
            Get<Tempest.Core.Commands.ICommandRegistry>(), Get<Tempest.Core.BusinessGovernance.Contracts.IIssuedContractCatalog>(),
            Get<Tempest.Core.BusinessGovernance.Quotations.IQuotationCatalog>(), Get<IConfigurationProvider>(), xeroApi: xero);

        // The host's own export service writes the same five files on its
        // first tick; let it finish (the next is 300 s away) so the two never
        // race on the same .tmp paths.
        await GetService(host).ExportOnceAsync();

        var accountingPath = Path.Combine(exportDir.Path, "accounting.json");
        await File.WriteAllTextAsync(accountingPath, "{\"last\":\"good\"}");

        await service.ExportOnceAsync();

        Assert.True(service.LastExportSucceeded, service.LastExportException?.ToString());
        Assert.IsType<AccountingExportUnavailableException>(service.LastAccountingExportException);
        Assert.Equal("{\"last\":\"good\"}", await File.ReadAllTextAsync(accountingPath));
        Assert.False(File.Exists(accountingPath + ".tmp"));
        Assert.True(File.Exists(Path.Combine(exportDir.Path, "reviews.json")));

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task ExportOnceAsync_DirectoryUnwritable_IsIsolated_NeverThrows()
    {
        using var temp = new TempDirectory();

        // A file, not a directory, at the configured export path —
        // Directory.CreateDirectory over it throws IOException, proving the
        // catch-and-log isolation rather than a Host-fatal escalation.
        var blockedPath = Path.Combine(Path.GetTempPath(), $"dashboard-export-blocked-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(blockedPath, "not a directory");
        try
        {
            var host = BuilderFor(temp.Path, blockedPath).Build();

            var runTask = host.RunAsync();
            await RunningHostFixture.WaitUntilRunningAsync(host);

            var service = GetService(host);

            await service.ExportOnceAsync();

            Assert.False(service.LastExportSucceeded);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            File.Delete(blockedPath);
        }
    }

    [Fact]
    public async Task StartAsync_TimerFires_ProducesAtLeastOneExport()
    {
        using var temp = new TempDirectory();
        using var exportDir = new TempDirectory();
        var host = BuilderFor(temp.Path, exportDir.Path, intervalSeconds: 1).Build();

        var runTask = host.RunAsync();
        await RunningHostFixture.WaitUntilRunningAsync(host);

        var statusPath = Path.Combine(exportDir.Path, "engineering-status.json");
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(statusPath) && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        Assert.True(File.Exists(statusPath));

        await host.StopAsync();
        await runTask;
    }
}
