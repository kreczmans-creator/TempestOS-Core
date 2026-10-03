using Tempest.Core.Invoicing.OAuth;
using Xunit.Abstractions;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// The live smoke test against the Xero <b>Demo Company</b> (`v0.24.0` task
/// X8, design §10.2, D7). Run by <c>scripts/xero-demo-smoke.ps1</c>; skipped
/// (never failed) everywhere else, including CI, because it needs the
/// Product Owner's Demo Company credentials (<see cref="XeroLiveSettings"/>).
/// </summary>
/// <remarks>
/// <para>
/// It refuses to write unless Xero reports <c>IsDemoCompany: true</c>: the
/// journey reads the organisation before anything else and stops, and the
/// pipeline's <c>XeroWriteSafetyHandler</c> runs with <i>Allow the live
/// organisation</i> hard-wired off.
/// </para>
/// <para>
/// Each run writes a Markdown report (steps, the design's open items, and a
/// Xero link for every record) to <see cref="XeroLiveSettings.ReportVariable"/>
/// or the temp folder, and prints it to the test output.
/// </para>
/// </remarks>
[Trait("Category", "XeroLive")]
public sealed class XeroLiveSmokeTests(ITestOutputHelper output)
{
    /// <summary>The full journey: connect, read, contact, quote, invoice, purchase order, bill, read-back, open items, clean-up.</summary>
    [XeroLiveFact]
    public async Task DemoCompany_FullJourney_WritesOnlyDraftsAndCopies()
    {
        var settings = XeroLiveSettings.FromEnvironment();
        using var connection = await ConnectAsync(settings);

        var journey = NewJourney(connection, settings);
        var report = await journey.RunAsync();

        Publish(report, settings, suffix: string.Empty);

        Assert.False(report.RefusedToWrite, "The connected organisation is not the Xero Demo Company: nothing was written. Connect the Demo Company and run again.");
        Assert.True(report.Passed, "Smoke steps failed:" + Environment.NewLine + string.Join(Environment.NewLine, report.Failures.Select(f => $"{f.Id} {f.Title}: {f.Detail}")));
    }

    /// <summary>
    /// Open item F1 (X5 key lifetime): a create repeated under the same
    /// <c>Idempotency-Key</c> 5½ minutes later is still replayed. Waits about
    /// seven minutes, so it also needs <see cref="XeroLiveSettings.KeyWindowVariable"/>.
    /// </summary>
    [XeroLiveFact(AlsoRequires = XeroLiveSettings.KeyWindowVariable)]
    public async Task DemoCompany_IdempotencyKey_OutlastsTheFiveMinuteWindow()
    {
        var settings = XeroLiveSettings.FromEnvironment();
        using var connection = await ConnectAsync(settings);

        var journey = NewJourney(connection, settings);
        var report = await journey.RunKeyRetentionProbeAsync();

        Publish(report, settings, suffix: "-key-window");

        Assert.False(report.RefusedToWrite, "The connected organisation is not the Xero Demo Company: nothing was written.");
        Assert.True(report.Passed, "Key-retention probe failed:" + Environment.NewLine + string.Join(Environment.NewLine, report.Failures.Select(f => $"{f.Id} {f.Title}: {f.Detail}")));
    }

    private async Task<XeroLiveConnection> ConnectAsync(XeroLiveSettings settings)
    {
        var connection = await XeroLiveConnection.CreateLiveAsync(settings);

        if (settings.Connect && !settings.UsesSuppliedToken)
        {
            output.WriteLine("Connecting: a browser window opens; sign in and choose the Demo Company.");
            var connected = await connection.Authoriser.AuthoriseAsync();
            output.WriteLine(connected.Outcome == OAuthOutcome.Ok ? "Connected." : $"Connect did not complete: {connected.Outcome} {connected.Reason}");
        }

        return connection;
    }

    private static XeroDemoSmokeJourney NewJourney(XeroLiveConnection connection, XeroLiveSettings settings) => new(
        connection.Api,
        connection.Authoriser,
        connection.SettingsReader,
        () => connection.Journal.Requests,
        new XeroDemoSmokeOptions(settings.Keep, (delay, cancellationToken) => Task.Delay(delay, cancellationToken), TimeProvider.System, settings.UsesSuppliedToken));

    private void Publish(XeroDemoSmokeReport report, XeroLiveSettings settings, string suffix)
    {
        output.WriteLine(report.ToConsoleText());

        var path = settings.ReportPath is { } chosen
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(chosen)) ?? ".", Path.GetFileNameWithoutExtension(chosen) + suffix + Path.GetExtension(chosen))
            : Path.Combine(Path.GetTempPath(), $"tempest-xero-smoke-{report.Stamp}{suffix}.md");

        File.WriteAllText(path, report.ToMarkdown());
        output.WriteLine($"Report: {path}");
    }
}
