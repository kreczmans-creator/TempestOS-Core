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
    /// The same journey through TempestOS's production path (review board
    /// m20): the X2 linker, the X3/X5 planners, mappers and handlers, the X4
    /// invoicing service and the X6 engine, over the same pipeline — no
    /// hand-built request body. Refuses to write unless Xero reports
    /// <c>IsDemoCompany</c>; skipped without the Demo Company credentials.
    /// </summary>
    [XeroLiveFact]
    public async Task DemoCompany_ProductionPath_WritesOnlyDraftsAndCopies()
    {
        var settings = XeroLiveSettings.FromEnvironment();
        using var connection = await ConnectAsync(settings);

        await using var journey = new XeroProductionSmokeJourney(
            connection,
            new XeroDemoSmokeOptions(settings.Keep, (delay, cancellationToken) => Task.Delay(delay, cancellationToken), TimeProvider.System, settings.UsesSuppliedToken));
        var report = await journey.RunAsync();

        Publish(report, settings, suffix: "-production");

        Assert.False(report.RefusedToWrite, "The connected organisation is not the Xero Demo Company: nothing was written. Connect the Demo Company and run again.");
        Assert.True(report.Passed, "Production-path steps failed:" + Environment.NewLine + string.Join(Environment.NewLine, report.Failures.Select(f => $"{f.Id} {f.Title}: {f.Detail}")));
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
        await SignInOnceIfAskedAsync(settings, XeroLiveSignInGate.PerRun, connection.Authoriser.AuthoriseAsync, output.WriteLine);
        return connection;
    }

    /// <summary>
    /// The <c>-Connect</c> browser sign-in, at most once per test run however
    /// many live tests connect (verifier round 1, defect 1): the first test
    /// to connect signs in and stores the tokens in the data folder's secrets;
    /// every later test's connection reads them from there. A sign-in that did
    /// not complete is not repeated either: a second consent page the
    /// operator is not expecting would only wait out the sign-in timeout.
    /// </summary>
    /// <param name="settings">What the operator asked for.</param>
    /// <param name="gate">The once-per-run gate (<see cref="XeroLiveSignInGate.PerRun"/> outside tests).</param>
    /// <param name="authorise">The browser sign-in.</param>
    /// <param name="log">The test output.</param>
    /// <returns><see langword="true"/> when this call signed in.</returns>
    internal static Task<bool> SignInOnceIfAskedAsync(
        XeroLiveSettings settings, XeroLiveSignInGate gate, Func<CancellationToken, Task<OAuthResult>> authorise, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(authorise);
        ArgumentNullException.ThrowIfNull(log);

        if (!settings.Connect || settings.UsesSuppliedToken)
            return Task.FromResult(false);

        return gate.RunOnceAsync(async () =>
        {
            log("Connecting: a browser window opens; sign in and choose the Demo Company.");
            var connected = await authorise(CancellationToken.None);
            log(connected.Outcome == OAuthOutcome.Ok ? "Connected." : $"Connect did not complete: {connected.Outcome} {connected.Reason}");
        });
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

/// <summary>
/// Runs an action at most once, however many callers (and however
/// concurrently): the live smoke tests' <c>-Connect</c> sign-in, which must
/// open the browser once per run, not once per test (verifier round 1,
/// defect 1). Later callers wait for the first to finish, so none connects
/// before the tokens are stored.
/// </summary>
internal sealed class XeroLiveSignInGate
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _ran;

    /// <summary>The gate shared by every live test in this test run.</summary>
    public static XeroLiveSignInGate PerRun { get; } = new();

    /// <summary>Runs <paramref name="action"/> unless it has run (or been tried) already.</summary>
    /// <param name="action">The once-only action.</param>
    /// <returns><see langword="true"/> when this call ran it.</returns>
    public async Task<bool> RunOnceAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ran)
                return false;

            _ran = true;
            await action().ConfigureAwait(false);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
