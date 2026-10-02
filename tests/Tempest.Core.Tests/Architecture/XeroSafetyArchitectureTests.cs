using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Architecture;

/// <summary>
/// `v0.24.0` §7.3 item 4 (`ADR-0162` decision 2): D3 and D4 hold by
/// construction as well as by the safety handler. No source file outside the
/// read-side mapping files below carries the words a forbidden Xero write
/// would need — <c>"AUTHORISED"</c>, <c>"SUBMITTED"</c>, <c>SentToContact</c>
/// or an <c>/Email</c> path — and the Xero <see cref="HttpClient"/> that
/// <c>TempestHost</c> builds starts with <see cref="XeroWriteSafetyHandler"/>,
/// shared by every Xero caller.
/// </summary>
/// <remarks>
/// <b>Adding a read-side file.</b> A file that must <i>read</i> one of these
/// words (mapping Xero's status back onto a TempestOS record, filtering a
/// <c>GET</c>) is added to <see cref="ReadSideFiles"/> with its reason —
/// never a file that builds a request body.
/// </remarks>
public sealed class XeroSafetyArchitectureTests
{
    /// <summary>The files allowed to carry a forbidden word, each for reading only (or, for the handler, to refuse it).</summary>
    private static readonly IReadOnlyDictionary<string, string> ReadSideFiles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["src/Tempest.Core/Invoicing/Xero/Api/XeroWriteSafetyHandler.cs"] = "the guard itself: names SentToContact and /Email to refuse them",
        ["src/Tempest.Core/Invoicing/Xero/XeroConnector.cs"] = "WP 19.8B bills-due read: a GET filter on Status==\"AUTHORISED\"",
        ["src/Tempest.Core/Invoicing/InvoicingService.cs"] = "InterpretStatus: maps a Xero status read back onto an InvoiceRequest (ADR-0151, X4)",
        ["src/Tempest.Core/Invoicing/Xero/Sync/XeroReadBack.cs"] = "X6 read-back: maps Xero statuses onto badges",
        ["src/Tempest.Core/Invoicing/FakeInvoicingConnector.cs"] = "the fake connector's simulated status reading",
        ["src/Tempest.Core/Invoicing/QuickBooksOnline/QuickBooksOnlineConnector.cs"] = "QuickBooks' own status word, read back (not Xero)",
    };

    private static readonly Regex[] ForbiddenInCode =
    [
        new("\"AUTHORISED\"", RegexOptions.CultureInvariant),
        new("\"SUBMITTED\"", RegexOptions.CultureInvariant),
        new(@"\bSentToContact\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
        new("\"[^\"\\r\\n]*/Email\\b[^\"\\r\\n]*\"", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
    ];

    [Fact]
    public void NoSourceFileOutsideTheReadSide_CarriesAForbiddenXeroWriteWord()
    {
        var root = RepositoryPaths.RepositoryRoot;
        var offences = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal) || ReadSideFiles.ContainsKey(relative))
                continue;

            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var code = CodeOf(line);
                if (code.Length == 0)
                    continue;

                foreach (var pattern in ForbiddenInCode)
                {
                    if (pattern.IsMatch(code))
                        offences.Add($"{relative}:{lineNumber}: {line.Trim()}");
                }
            }
        }

        Assert.True(offences.Count == 0, "A forbidden Xero write word outside the read-side files (ADR-0162, D3/D4):" + Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    [Fact]
    public void TheScan_WouldCatchAForbiddenWrite()
    {
        Assert.Contains(ForbiddenInCode, p => p.IsMatch("""var body = new { Status = "AUTHORISED" };"""));
        Assert.Contains(ForbiddenInCode, p => p.IsMatch("""status = "SUBMITTED";"""));
        Assert.Contains(ForbiddenInCode, p => p.IsMatch("public bool? SentToContact { get; init; }"));
        Assert.Contains(ForbiddenInCode, p => p.IsMatch("""await PostAsync($"Invoices/{id}/Email", body);"""));
        Assert.Empty(CodeOf("""/// <c>"AUTHORISED"</c> is never written."""));
        Assert.Empty(CodeOf("""   // SentToContact stays false"""));
    }

    [Fact]
    public void EveryReadSideFile_Exists_OrIsAPlannedV024File()
    {
        string[] planned = ["src/Tempest.Core/Invoicing/Xero/Sync/XeroReadBack.cs"];

        foreach (var file in ReadSideFiles.Keys.Except(planned))
            Assert.True(File.Exists(Path.Combine(RepositoryPaths.RepositoryRoot, file)), $"{file} is in the read-side allow-list but does not exist; remove it.");
    }

    [Fact]
    public void TheXeroPipeline_StartsWithTheSafetyHandler_AndEveryXeroCallerSharesIt()
    {
        var context = XeroServiceRegistration.Compose(
            new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([])).Build(),
            new InMemorySecretStore(),
            NullLoggerFactory.Instance,
            () => null);

        Assert.IsType<XeroRateLimiter>(context.SafetyHandler.InnerHandler);
        Assert.IsType<InvoicingHttpLoggingHandler>(((DelegatingHandler)context.SafetyHandler.InnerHandler!).InnerHandler);
        Assert.Same(context.ApiClient, HttpClientOf(context.Api));
        Assert.Same(context.ApiClient, HttpClientOf(context.Connector));
        Assert.Equal(new Uri("https://api.xero.com/api.xro/2.0/"), context.ApiClient.BaseAddress);
        Assert.Equal(XeroScopes.Required, XeroServiceRegistration.OAuthProfile.Scopes);
    }

    [Fact]
    public async Task TheHostsXeroClient_RefusesAnEmailRequest_BeforeItLeavesTheMachine()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(
            temp.Path,
            new(InvoicingService.ConnectorConfigurationKey, "Xero"),
            new("Invoicing:Xero:ClientId", "test-client-id"));

        try
        {
            var api = (XeroAccountingApi)host.Services!.GetService(typeof(XeroAccountingApi));
            Assert.IsType<XeroRateLimiter>(host.Services.GetService(typeof(XeroRateLimiter)));
            var connector = Assert.IsType<XeroConnector>(ConnectorHostFixture.Connector(host));
            var client = HttpClientOf(api);
            Assert.Same(client, HttpClientOf(connector));

            using var request = new HttpRequestMessage(HttpMethod.Post, "Invoices/inv-1/Email") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            request.Headers.Add("xero-tenant-id", "tenant-1");
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(XeroWriteSafetyHandler.RuleEmail, response.Headers.GetValues(XeroWriteSafetyHandler.BlockedHeader).Single());
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>A line's code, or empty for a whole-line comment (<c>//</c>, <c>///</c>, or a <c>*</c> block-comment line).</summary>
    private static string CodeOf(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("/*", StringComparison.Ordinal) || trimmed.StartsWith('*')
            ? string.Empty
            : trimmed;
    }

    /// <summary>The <see cref="HttpClient"/> one of TempestOS's own Xero callers holds (its private field — our own type, read for this test only).</summary>
    private static HttpClient HttpClientOf(object caller) =>
        (HttpClient)caller.GetType().GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(caller)!;
}
