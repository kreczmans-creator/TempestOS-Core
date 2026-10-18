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
/// construction as well as by the safety handler. No line of code under
/// <c>src/</c> other than the read-side lines below carries the words a
/// forbidden Xero write would need — <c>"AUTHORISED"</c>, <c>"SUBMITTED"</c>
/// (quoted or JSON-escaped), <c>SentToContact</c> or an <c>/Email</c> path —
/// and the Xero <see cref="HttpClient"/> that
/// <c>TempestHost</c> builds starts with <see cref="XeroWriteSafetyHandler"/>,
/// shared by every Xero caller.
/// </summary>
/// <remarks>
/// <b>Adding a read-side line.</b> A line that must <i>read</i> one of these
/// words (mapping Xero's status back onto a TempestOS record, filtering a
/// <c>GET</c>) is added to <see cref="ReadSideLines"/> — file and exact
/// trimmed line — with its reason; never a line that builds a request body.
/// The allowance is per line, never per file, so a forbidden word added
/// anywhere else in the same file (a write path such as
/// <c>XeroConnector.cs</c>) is still caught. An allowance whose line no
/// longer exists fails <see cref="EveryReadSideLine_StillExists"/>.
/// </remarks>
public sealed class XeroSafetyArchitectureTests
{
    /// <summary>The individual lines allowed to carry a forbidden word, each for reading only (or, for the handler, to refuse it): repository-relative file, exact trimmed line, reason.</summary>
    private static readonly IReadOnlyList<(string File, string Line, string Reason)> ReadSideLines =
    [
        ("src/Tempest.Core/Invoicing/Xero/Api/XeroWriteSafetyHandler.cs",
            """if (string.Equals(property.Name, "SentToContact", StringComparison.OrdinalIgnoreCase)""",
            "the guard itself: names SentToContact to refuse it"),
        ("src/Tempest.Core/Invoicing/Xero/XeroConnector.cs",
            """var where = Uri.EscapeDataString("Type==\"ACCPAY\"&&Status==\"AUTHORISED\"");""",
            "WP 19.8B bills-due read: a GET filter on approved bills"),
        ("src/Tempest.Core/Invoicing/FakeInvoicingConnector.cs",
            """: new InvoiceStatusReading("SUBMITTED", null, null, null);""",
            "the fake connector's simulated status reading"),
        ("src/Tempest.Core/Invoicing/QuickBooksOnline/QuickBooksOnlineConnector.cs",
            """return "SUBMITTED";""",
            "QuickBooks' own status word, read back (not Xero)"),
    ];

    private static readonly Regex[] ForbiddenInCode =
    [
        // Quoted, JSON-escaped (\"AUTHORISED\") or both.
        new(@"\\?""AUTHORISED\\?""", RegexOptions.CultureInvariant),
        new(@"\\?""SUBMITTED\\?""", RegexOptions.CultureInvariant),
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
            if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal))
                continue;

            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var code = CodeOf(line);
                if (code.Length == 0 || IsReadSideLine(relative, code))
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

    // Re-verifier B1 defect 3: a line starting with "/*" was skipped whole, so code after the closing "*/" went unscanned.
    [Fact]
    public void TheScan_ReadsCodeAfterABlockCommentOnTheSameLine()
    {
        Assert.Contains(ForbiddenInCode, p => p.IsMatch(CodeOf("""/* x */ var s = "AUTHORISED";""")));
        Assert.Contains(ForbiddenInCode, p => p.IsMatch(CodeOf("""   /* a */ /* b */ SentToContact = true,""")));
        Assert.Contains(ForbiddenInCode, p => p.IsMatch(CodeOf(""" * end of a comment */ var s = "SUBMITTED";""")));
        Assert.Empty(CodeOf("""/* "AUTHORISED" is never written"""));
        Assert.Empty(CodeOf("""/* "AUTHORISED" */"""));
        Assert.Empty(CodeOf(""" * "AUTHORISED" stays in the comment"""));
    }

    [Fact]
    public void TheScan_CatchesAJsonEscapedStatusWrite()
    {
        Assert.Contains(ForbiddenInCode, p => p.IsMatch("""var body = "{\"Status\":\"AUTHORISED\"}";"""));
        Assert.Contains(ForbiddenInCode, p => p.IsMatch("""var body = "{\"Status\":\"SUBMITTED\"}";"""));
    }

    [Fact]
    public void TheAllowance_IsPerLine_SoAForbiddenWriteInAReadSideFileIsStillCaught()
    {
        // XeroConnector.cs is the invoice write path (X4): only its one GET
        // filter line is allowed; a body built anywhere else in it is not.
        const string Connector = "src/Tempest.Core/Invoicing/Xero/XeroConnector.cs";

        Assert.True(IsReadSideLine(Connector, """var where = Uri.EscapeDataString("Type==\"ACCPAY\"&&Status==\"AUTHORISED\"");"""));
        Assert.False(IsReadSideLine(Connector, """var invoice = new { Status = "AUTHORISED" };"""));
        Assert.False(IsReadSideLine(Connector, """SentToContact = true,"""));
        Assert.False(IsReadSideLine("src/Tempest.Core/Invoicing/InvoicingService.cs", """return "AUTHORISED";"""));
    }

    [Fact]
    public void EveryReadSideLine_StillExists()
    {
        foreach (var (file, line, _) in ReadSideLines)
        {
            var path = Path.Combine(RepositoryPaths.RepositoryRoot, file);
            Assert.True(File.Exists(path), $"{file} is in the read-side allow-list but does not exist; remove its line.");
            Assert.True(
                File.ReadLines(path).Any(candidate => string.Equals(candidate.Trim(), line, StringComparison.Ordinal)),
                $"{file} no longer has the read-side line `{line}`; remove or update its allowance.");
            Assert.Contains(ForbiddenInCode, p => p.IsMatch(line));
        }
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

    private static bool IsReadSideLine(string relative, string code) =>
        ReadSideLines.Any(allowed => string.Equals(allowed.File, relative, StringComparison.Ordinal) && string.Equals(allowed.Line, code.Trim(), StringComparison.Ordinal));

    /// <summary>
    /// A line's code: empty for a whole-line comment (<c>//</c>, <c>///</c>, or a <c>*</c> block-comment line); a leading
    /// block comment that closes on the line (<c>/* x */ code</c>, <c>* x */ code</c>) is removed and the rest is scanned.
    /// </summary>
    private static string CodeOf(string line)
    {
        var trimmed = line.TrimStart();
        while (true)
        {
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
                return string.Empty;

            var opens = trimmed.StartsWith("/*", StringComparison.Ordinal);
            if (!opens && !trimmed.StartsWith('*'))
                return trimmed;

            var end = trimmed.IndexOf("*/", opens ? 2 : 0, StringComparison.Ordinal);
            if (end < 0)
                return string.Empty;

            trimmed = trimmed[(end + 2)..].TrimStart();
        }
    }

    /// <summary>The <see cref="HttpClient"/> one of TempestOS's own Xero callers holds (its private field — our own type, read for this test only).</summary>
    private static HttpClient HttpClientOf(object caller) =>
        (HttpClient)caller.GetType().GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(caller)!;
}
